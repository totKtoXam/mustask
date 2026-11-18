namespace Mustask.Domain.Images;

/// <summary>
/// The caps of the image store (spec BR-IM-3, -4, -16, -20). The spec fixes them; the panel mirrors a few as props
/// (api-contract § 6). They are not configuration: a change is a spec change.
/// </summary>
public static class ImageLimits
{
    /// <summary>A stored image, and the body of an ordinary upload: 5 MB (BR-IM-3 (a)).</summary>
    public const int MaxStoredBytes = 5_242_880;

    /// <summary>The body of a reduction request: 20 MB (BR-IM-3 (b)).</summary>
    public const int MaxReductionInputBytes = 20_971_520;

    /// <summary>A still image, or an animation's canvas: 60 megapixels.</summary>
    public const long MaxPixels = 60_000_000;

    /// <summary>Either side of an image.</summary>
    public const int MaxSide = 20_000;

    /// <summary>The frames of an animation.</summary>
    public const int MaxFrames = 1_000;

    /// <summary>All frames of an animation together.</summary>
    public const long MaxAnimationPixels = 600_000_000;

    /// <summary>A picture is scaled down to this many pixels before it is kept or compared (BR-IM-4 (2)): 4096 x 4096.</summary>
    public const long MaxPixelsAfterScaling = 16_777_216;

    /// <summary>A lossy picture whose stripped form is larger than this is re-encoded (BR-IM-4 (4)): 1 MB.</summary>
    public const int ReencodeAboveBytes = 1_048_576;

    /// <summary>The similarity of a re-encoded picture to its reference (BR-IM-4 (4)).</summary>
    public const double MinSimilarity = 0.98;

    /// <summary>The similarity floor of a reduction (BR-IM-20 (2)).</summary>
    public const double MinReductionSimilarity = 0.95;

    /// <summary>A reduction never goes below this share of the original's width and height.</summary>
    public const double ReductionSideFloor = 0.5;

    /// <summary>The text alternative, in Unicode code points (BR-IM-7).</summary>
    public const int MaxTextAlternativeCodePoints = 255;

    /// <summary>An upload bound to nothing is removed this long after it was made (BR-IM-16).</summary>
    public static readonly TimeSpan UnboundLifetime = TimeSpan.FromHours(2);

    /// <summary>How often the sweep runs (BR-IM-16).</summary>
    public static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);
}
