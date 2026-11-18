using System.Buffers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Mustask.Application.Abstractions.Images;
using Mustask.Domain.Images;
using Mustask.Shared.Exceptions;

namespace Mustask.Api.Features.Images;

/// <summary>
/// <see cref="IUploadBody"/> over the request (plan-backend § 6.2): a declared <c>Content-Length</c> over the cap is refused
/// without reading, otherwise the body is read into a pooled buffer of min(declared length, cap) + 1 bytes (the whole cap + 1
/// when no length is declared), counting bytes and stopping at cap + 1. The big bodies of the reduction route are held by
/// at most <see cref="UploadBodySlots.Count"/> requests at once; the rest wait a moment, then get 503. The declared
/// <c>Content-Type</c> is never looked at (BR-IM-2).
/// </summary>
internal sealed class HttpUploadBody(HttpRequest request, UploadBodySlots slots) : IUploadBody
{
    public async Task<UploadBodyAnswer> ReadAsync(int maxBytes, CancellationToken cancellationToken)
    {
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = maxBytes + 1L;
        }

        if (request.ContentLength is { } declared && declared > maxBytes)
        {
            return new UploadBodyAnswer(ReadOnlyMemory<byte>.Empty, tooLarge: true, lease: null);
        }

        var capacity = request.ContentLength is { } known ? (int)Math.Min(known, maxBytes) + 1 : maxBytes + 1;
        var large = capacity > ImageLimits.MaxStoredBytes + 1;

        if (large && !await slots.Semaphore.WaitAsync(slots.Wait, cancellationToken))
        {
            throw new ServiceUnavailableException(ImageErrorCodes.Busy, TimeSpan.FromSeconds(5));
        }

        var owner = MemoryPool<byte>.Shared.Rent(capacity);
        var lease = new Lease(owner, large ? slots.Semaphore : null);
        var length = 0;

        try
        {
            while (length < capacity)
            {
                var read = await request.Body.ReadAsync(owner.Memory[length..capacity], cancellationToken);

                if (read == 0)
                {
                    break;
                }

                length += read;
            }
        }
        catch (BadHttpRequestException)
        {
            lease.Dispose();

            return new UploadBodyAnswer(ReadOnlyMemory<byte>.Empty, tooLarge: true, lease: null);
        }
        catch
        {
            lease.Dispose();
            throw;
        }

        if (length > maxBytes)
        {
            lease.Dispose();

            return new UploadBodyAnswer(ReadOnlyMemory<byte>.Empty, tooLarge: true, lease: null);
        }

        return new UploadBodyAnswer(owner.Memory[..length], tooLarge: false, lease);
    }

    private sealed class Lease(IDisposable owner, SemaphoreSlim? slot) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                owner.Dispose();
                slot?.Release();
            }
        }
    }
}
