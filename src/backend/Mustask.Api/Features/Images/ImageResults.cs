using Microsoft.AspNetCore.Http;
using Mustask.Application.Features.Images.GetImage;
using Mustask.Domain.Images;

namespace Mustask.Api.Features.Images;

/// <summary>
/// The read's headers and 304 (api-contract § 3): the bytes with their type, a private revalidating cache, no sniffing and a
/// sandboxing policy, so a stored file can never run as a page; a current copy is answered without a body.
/// </summary>
internal static class ImageResults
{
    public static IResult Of(GetImageResult result) => new ImageHttpResult(result);

    private sealed class ImageHttpResult(GetImageResult result) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var response = httpContext.Response;
            response.Headers.ETag = result.ETag;
            response.Headers.CacheControl = "private, no-cache";
            response.Headers["X-Content-Type-Options"] = "nosniff";
            response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
            response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";

            if (result.Content is not { } content)
            {
                response.StatusCode = StatusCodes.Status304NotModified;

                return;
            }

            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = ImageFormats.MediaType(result.Format);
            response.ContentLength = content.Length;
            await response.Body.WriteAsync(content, httpContext.RequestAborted);
        }
    }
}
