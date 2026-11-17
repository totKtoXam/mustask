using Mustask.Domain.Images;

namespace Mustask.Infrastructure.Images.Intake.Headers;

/// <summary>Why the header reader refuses a file (plan-backend § 4.1); the intake maps each to its answer.</summary>
internal enum ImageRefusal
{
    /// <summary>Not a PNG, JPEG, GIF or WebP, or a structurally broken one.</summary>
    Format,

    /// <summary>Over 60 megapixels or 20 000 pixels a side.</summary>
    Dimensions,

    /// <summary>Over 1 000 frames or 600 000 000 pixels in all frames.</summary>
    Animation,
}

/// <summary>
/// What the container says about the picture, read without decoding anything (BR-IM-3 (c)). Sizes are the canvas as
/// stored; <see cref="Orientation"/> is the EXIF value 1..8 (1 when absent).
/// </summary>
internal sealed record ImageHeader(
    ImageFormat Format,
    int Width,
    int Height,
    int FrameCount,
    long TotalFramePixels,
    bool IsAnimated,
    bool IsLossless,
    int Orientation,
    int ContainerEnd)
{
    /// <summary>The width the member sees: orientations 5..8 swap the sides.</summary>
    public int DisplayWidth => Orientation is >= 5 and <= 8 ? Height : Width;

    public int DisplayHeight => Orientation is >= 5 and <= 8 ? Width : Height;
}

/// <summary>The caps of BR-IM-3 (c), applied the moment a size is known, before anything is walked further.</summary>
internal static class ImageCaps
{
    /// <summary>The canvas against the still caps.</summary>
    public static ImageRefusal? CheckCanvas(long width, long height)
    {
        if (width <= 0 || height <= 0)
        {
            return ImageRefusal.Format;
        }

        return width > ImageLimits.MaxSide || height > ImageLimits.MaxSide || width * height > ImageLimits.MaxPixels
            ? ImageRefusal.Dimensions
            : null;
    }

    /// <summary>The frames of an animation against the animation caps.</summary>
    public static ImageRefusal? CheckFrames(long frames, long totalPixels) =>
        frames > ImageLimits.MaxFrames || totalPixels > ImageLimits.MaxAnimationPixels ? ImageRefusal.Animation : null;
}
