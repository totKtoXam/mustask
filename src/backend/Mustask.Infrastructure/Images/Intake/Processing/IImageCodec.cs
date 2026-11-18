using Mustask.Domain.Images;
using Mustask.Infrastructure.Images.Intake.Headers;

namespace Mustask.Infrastructure.Images.Intake.Processing;

/// <summary>How a picture is encoded: the format, and for a lossy format the quality 1..100 (lossless otherwise).</summary>
internal readonly record struct PictureEncoding(ImageFormat Format, int Quality, bool Lossless);

/// <summary>A decoded, oriented picture held in memory (Dispose releases it).</summary>
internal interface IPicture : IDisposable
{
    int Width { get; }

    int Height { get; }

    /// <summary>The picture at another size: Lanczos3, nearest-neighbour for GIF (ADR-0045 D-3).</summary>
    IPicture Resize(int width, int height, ImageFormat format);

    /// <summary>Encodes with the colour profile kept and every other metadata dropped.</summary>
    byte[] Encode(PictureEncoding encoding);

    /// <summary>The luminance plane, <c>Y = 0.299 R + 0.587 G + 0.114 B</c> of the 8-bit values, Width x Height samples.</summary>
    byte[] Luma();
}

/// <summary>
/// The only seam to the image library (plan-backend § 5): decode with scaling and orientation, and the luminance of an encoded
/// candidate. The intake's logic is written against it, and tests that must prove the library is never reached pass a spy.
/// </summary>
internal interface IImageCodec
{
    /// <summary>
    /// Decodes a stripped file: scaled down to at most <see cref="ImageLimits.MaxPixelsAfterScaling"/> pixels before anything is
    /// rotated, then the orientation applied (the picture is what the member saw), a CMYK picture converted to sRGB.
    /// </summary>
    IPicture Decode(byte[] stripped, ImageHeader header);

    /// <summary>Decodes an encoded candidate to its luminance plane.</summary>
    byte[] DecodeLuma(byte[] encoded, ImageFormat format, out int width, out int height);
}
