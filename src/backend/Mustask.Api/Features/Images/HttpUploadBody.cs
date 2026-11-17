using System.Buffers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Mustask.Application.Abstractions.Images;

namespace Mustask.Api.Features.Images;

/// <summary>
/// <see cref="IUploadBody"/> over the request (plan-backend § 6.2): a declared <c>Content-Length</c> over the cap is refused
/// without reading, otherwise the body is read into a pooled buffer counting bytes and stops at cap + 1. The declared
/// <c>Content-Type</c> is never looked at (BR-IM-2).
/// </summary>
internal sealed class HttpUploadBody(HttpRequest request) : IUploadBody
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

        var owner = MemoryPool<byte>.Shared.Rent(maxBytes + 1);
        var length = 0;

        try
        {
            while (length <= maxBytes)
            {
                var read = await request.Body.ReadAsync(owner.Memory[length..(maxBytes + 1)], cancellationToken);

                if (read == 0)
                {
                    break;
                }

                length += read;
            }
        }
        catch (BadHttpRequestException)
        {
            owner.Dispose();

            return new UploadBodyAnswer(ReadOnlyMemory<byte>.Empty, tooLarge: true, lease: null);
        }
        catch
        {
            owner.Dispose();
            throw;
        }

        if (length > maxBytes)
        {
            owner.Dispose();

            return new UploadBodyAnswer(ReadOnlyMemory<byte>.Empty, tooLarge: true, lease: null);
        }

        return new UploadBodyAnswer(owner.Memory[..length], tooLarge: false, owner);
    }
}
