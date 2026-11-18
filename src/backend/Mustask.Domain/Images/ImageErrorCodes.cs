using Mustask.Shared.Errors;

namespace Mustask.Domain.Images;

/// <summary>
/// Error codes of the image store (feat-078 api-contract § 5; ADR-0005 registry). Each has Russian and Kazakh texts in
/// <c>contracts/errors/images.json</c>.
/// </summary>
[ErrorCodes]
public static class ImageErrorCodes
{
    /// <summary>The body is over 5 MB on an ordinary upload.</summary>
    public const string UploadTooLarge = "images.upload.too_large";

    /// <summary>The content is not a PNG, JPEG, GIF or WebP, or is a damaged one.</summary>
    public const string FormatUnsupported = "images.upload.format_unsupported";

    /// <summary>Over 60 megapixels or 20 000 pixels a side.</summary>
    public const string TooManyPixels = "images.upload.too_many_pixels";

    /// <summary>An animation over 1 000 frames, or over 600 000 000 pixels in all frames.</summary>
    public const string AnimationTooLarge = "images.upload.animation_too_large";

    /// <summary>The body of a reduction is over 20 MB.</summary>
    public const string ReductionTooLarge = "images.reduction.too_large";

    /// <summary>A reduction of an animated image is not offered.</summary>
    public const string ReductionAnimated = "images.reduction.animated";

    /// <summary>No step reaches 5 MB within the floors.</summary>
    public const string CannotReduce = "images.reduction.cannot_reduce";

    /// <summary>The decode gate is full (503).</summary>
    public const string Busy = "images.busy";

    /// <summary>The one answer of the read for every refusal (404).</summary>
    public const string ImageNotFound = "images.image.not_found";
}
