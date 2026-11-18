using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Mustask.Api.Features.Images;
using Mustask.Domain.Images;
using Mustask.Shared.Exceptions;

namespace Mustask.Api.IntegrationTests.Features.Images;

/// <summary>
/// feat-078 QA delta after code review (qa.md, "Backend delta — intake fix"): <see cref="HttpUploadBody"/> over a plain
/// <see cref="DefaultHttpContext"/>, no host and no database. The cap and cap + 1 with and without a declared length, a
/// Content-Length that lies in either direction, the read bound of min(length, cap) + 1, and the body slots (F-2, F-7).
/// </summary>
public sealed class HttpUploadBodyQaTests
{
    private const int UploadCap = ImageLimits.MaxStoredBytes;
    private const int ReductionCap = ImageLimits.MaxReductionInputBytes;

    /// <summary>A body that hands out <paramref name="available"/> bytes, counts what was asked, then ends or fails.</summary>
    private sealed class BodyStream(long available, Exception? failAtEnd = null) : Stream
    {
        public long Served { get; private set; }

        public int Reads { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => Served; set => throw new NotSupportedException(); }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Reads++;
            var left = available - Served;
            if (left <= 0)
            {
                return failAtEnd is null ? ValueTask.FromResult(0) : ValueTask.FromException<int>(failAtEnd);
            }

            // Serve in pieces, as a socket would.
            var count = (int)Math.Min(Math.Min(left, buffer.Length), 64 * 1024);
            buffer.Span[..count].Fill(0x5A);
            Served += count;

            return ValueTask.FromResult(count);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class MaxBodySize : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;

        public long? MaxRequestBodySize { get; set; } = 30_000_000;
    }

    private static (HttpUploadBody Body, BodyStream Stream, MaxBodySize Limit) Build(long? declared, long available, UploadBodySlots? slots = null, Exception? failAtEnd = null)
    {
        var context = new DefaultHttpContext();
        var stream = new BodyStream(available, failAtEnd);
        var limit = new MaxBodySize();
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(limit);
        context.Request.Body = stream;
        context.Request.ContentLength = declared;

        return (new HttpUploadBody(context.Request, slots ?? new UploadBodySlots()), stream, limit);
    }

    // Exactly the cap is read whole, with and without a declared length; the server's own limit is set to cap + 1.
    [Theory]
    [InlineData(UploadCap, true)]
    [InlineData(UploadCap, false)]
    [InlineData(ReductionCap, true)]
    [InlineData(ReductionCap, false)]
    public async Task ExactlyTheCap_IsRead(int cap, bool declared)
    {
        var (body, _, limit) = Build(declared ? cap : null, cap);

        using var answer = await body.ReadAsync(cap, CancellationToken.None);

        Assert.False(answer.TooLarge);
        Assert.Equal(cap, answer.Content.Length);
        Assert.Equal(cap + 1L, limit.MaxRequestBodySize);
    }

    // cap + 1 declared is refused without a byte read; cap + 1 sent chunked is read up to cap + 1 and refused.
    [Theory]
    [InlineData(UploadCap)]
    [InlineData(ReductionCap)]
    public async Task CapPlusOne_IsTooLarge(int cap)
    {
        var (declared, declaredStream, _) = Build(cap + 1L, cap + 1L);
        using (var answer = await declared.ReadAsync(cap, CancellationToken.None))
        {
            Assert.True(answer.TooLarge);
            Assert.Equal(0, declaredStream.Reads);
        }

        var (chunked, chunkedStream, _) = Build(null, 3L * cap);
        using (var answer = await chunked.ReadAsync(cap, CancellationToken.None))
        {
            Assert.True(answer.TooLarge);
            Assert.True(answer.Content.IsEmpty);
            Assert.Equal(cap + 1L, chunkedStream.Served);
        }
    }

    // A length that says less than is sent: the reader asks for at most min(length, cap) + 1 bytes (the rental). Kestrel
    // ends the body at the declared length, so in a real request the extra byte is never there; here the fake hands it out
    // to prove the bound, and nothing past it is taken.
    [Fact]
    public async Task ContentLengthTooSmall_ReadIsBoundedByTheDeclaredLength()
    {
        var (body, stream, _) = Build(1_000, 2L * UploadCap);

        using var answer = await body.ReadAsync(UploadCap, CancellationToken.None);

        Assert.Equal(1_001, stream.Served);
        Assert.Equal(1_001, answer.Content.Length);
    }

    // A length that says more than is sent. Kestrel fails the read with BadHttpRequestException: the answer is too_large
    // (mislabelled, review round 2), nothing is kept, and a slot it held is freed. A host that just ends the body early
    // hands over the shorter body.
    [Fact]
    public async Task ContentLengthTooLarge_TruncatedBody()
    {
        var slots = new UploadBodySlots(1, TimeSpan.FromMilliseconds(50));
        var (failing, _, _) = Build(UploadCap + 100L, 10, slots, new BadHttpRequestException("Unexpected end of request content.", 400));

        using (var answer = await failing.ReadAsync(ReductionCap, CancellationToken.None))
        {
            Assert.True(answer.TooLarge);
        }

        Assert.Equal(1, slots.Semaphore.CurrentCount);

        var (ending, _, _) = Build(1_000, 10);
        using var shorter = await ending.ReadAsync(UploadCap, CancellationToken.None);
        Assert.False(shorter.TooLarge);
        Assert.Equal(10, shorter.Content.Length);
    }

    // A slot is taken only for a body above the 5 MB upload cap (declared, or unknown on the reduction route), held until the
    // answer is disposed, released once however often it is disposed.
    [Theory]
    [InlineData(UploadCap, UploadCap + 0L, false)]
    [InlineData(ReductionCap, UploadCap + 0L, false)]
    [InlineData(ReductionCap, UploadCap + 1L, true)]
    [InlineData(ReductionCap, null, true)]
    [InlineData(UploadCap, null, false)]
    public async Task Slot_TakenOnlyForALargeBody_ReleasedOnce(int cap, long? declared, bool takesSlot)
    {
        var slots = new UploadBodySlots(2, TimeSpan.FromMilliseconds(50));
        var (body, _, _) = Build(declared, Math.Min(declared ?? 100, 100), slots);

        var answer = await body.ReadAsync(cap, CancellationToken.None);

        Assert.Equal(takesSlot ? 1 : 2, slots.Semaphore.CurrentCount);
        answer.Dispose();
        answer.Dispose();
        Assert.Equal(2, slots.Semaphore.CurrentCount);
    }

    [Fact]
    public async Task NoFreeSlot_503Busy_AndAFailedReadFreesItsSlot()
    {
        var slots = new UploadBodySlots(1, TimeSpan.FromMilliseconds(50));
        var (first, _, _) = Build(null, 10, slots, new IOException("connection reset"));

        await Assert.ThrowsAsync<IOException>(() => first.ReadAsync(ReductionCap, CancellationToken.None));
        Assert.Equal(1, slots.Semaphore.CurrentCount);

        var (holder, _, _) = Build(UploadCap + 1L, UploadCap + 1L, slots);
        using var held = await holder.ReadAsync(ReductionCap, CancellationToken.None);

        var (waiting, waitingStream, _) = Build(UploadCap + 1L, UploadCap + 1L, slots);
        var busy = await Assert.ThrowsAsync<ServiceUnavailableException>(() => waiting.ReadAsync(ReductionCap, CancellationToken.None));
        Assert.Equal(ImageErrorCodes.Busy, busy.Code);
        Assert.Equal(TimeSpan.FromSeconds(5), busy.RetryAfter);
        Assert.Equal(0, waitingStream.Reads);

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var (gone, _, _) = Build(UploadCap + 1L, UploadCap + 1L, slots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gone.ReadAsync(ReductionCap, cancelled.Token));
        Assert.Equal(0, slots.Semaphore.CurrentCount);
    }
}
