using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.Features.UserDirectory;
using Mustask.Domain.UserDirectory;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 security pass S3 (review.md, "Security pass — backend"): the push
/// binder runs the checks that need no body first — both headers present and
/// well-formed, the timestamp inside its window — then reads the bounded body,
/// then verifies the HMAC. Before, the body (up to 4 MiB, and a second copy) was
/// buffered for anyone, and an unsigned body over the limit was answered 400
/// <c>push_malformed</c>, not 401.
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class PushEnvelopeBeforeBodyTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private const int OverTheLimit = (4 * 1024 * 1024) + 1;

    private readonly PushTestClient _push = new(factory);

    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => Task.CompletedTask;

    private static string Oversized() => "{\"changes\":[],\"padding\":\"" + new string('x', OverTheLimit) + "\"}";

    private async Task AssertUnauthorizedAsync(HttpRequestMessage request)
    {
        using var response = await _push.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(UserDirectoryErrorCodes.PushUnauthorized, await PushTestClient.ErrorCodeAsync(response));
    }

    // S3: with no headers there is nothing to read the body for — 401, whatever its size.
    [Fact]
    public Task Unsigned_BodyOverTheLimit_IsUnauthorized_NotMalformed() =>
        AssertUnauthorizedAsync(PushTestClient.Build(HttpMethod.Post, "changes", Oversized(), signed: false));

    [Fact]
    public Task StaleTimestamp_BodyOverTheLimit_IsUnauthorized_NotMalformed() =>
        AssertUnauthorizedAsync(PushTestClient.Build(HttpMethod.Post, "changes", Oversized(), at: DateTimeOffset.UtcNow.AddSeconds(-301)));

    [Fact]
    public Task MalformedSignatureHeader_BodyOverTheLimit_IsUnauthorized_NotMalformed()
    {
        var request = PushTestClient.Build(HttpMethod.Post, "changes", Oversized(), signed: false);
        request.Headers.Add(PushSignatureVerifier.TimestampHeader, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation(PushSignatureVerifier.SignatureHeader, "v1=not-hex");

        return AssertUnauthorizedAsync(request);
    }

    // The contract is unchanged where a request is well-formed: a body over the limit that carries its headers is read
    // (to the limit) and refused as malformed — the signature is checked after the read, not before.
    [Fact]
    public async Task Signed_BodyOverTheLimit_IsStillMalformed()
    {
        using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", Oversized()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(UserDirectoryErrorCodes.PushMalformed, await PushTestClient.ErrorCodeAsync(response));
    }

    // S3's cost: the body of a request that fails the envelope is not read at all, so not buffered. Counted on the
    // server's side of the request stream, from a middleware ahead of the whole pipeline.
    [Fact]
    public async Task Unsigned_Body_IsNotReadByTheServer_ButASignedOneIs()
    {
        var body = "{\"changes\":[],\"padding\":\"" + new string('x', 3 * 1024 * 1024) + "\"}";
        var counter = new BodyBytesRead();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter>(new CountingBodyStartupFilter(counter))));
        var push = new PushTestClient(host.CreateClient());

        using (var refused = await push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", body, signed: false)))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        }

        Assert.Equal(0, counter.Bytes);

        // The control: the counter does see a body that is read.
        using (var accepted = await push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", body)))
        {
            Assert.Equal(HttpStatusCode.BadRequest, accepted.StatusCode);
        }

        Assert.True(counter.Bytes >= body.Length, $"the control request's body was read {counter.Bytes} of {body.Length} bytes");
    }

    /// <summary>What the server read of the last request's body.</summary>
    private sealed class BodyBytesRead
    {
        public long Bytes { get; set; }
    }

    private sealed class CountingBodyStartupFilter(BodyBytesRead counter) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, pipeline) =>
            {
                var counting = new CountingReadStream(context.Request.Body);
                context.Request.Body = counting;

                try
                {
                    await pipeline(context);
                }
                finally
                {
                    counter.Bytes = counting.BytesRead;
                }
            });

            next(app);
        };
    }

    private sealed class CountingReadStream(Stream inner) : Stream
    {
        public long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Count(await inner.ReadAsync(buffer, cancellationToken));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int Count(int read)
        {
            BytesRead += read;

            return read;
        }
    }
}
