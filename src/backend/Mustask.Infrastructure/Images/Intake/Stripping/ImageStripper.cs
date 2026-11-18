using Mustask.Domain.Images;
using Mustask.Infrastructure.Images.Intake.Headers;

namespace Mustask.Infrastructure.Images.Intake.Stripping;

/// <summary>
/// ADR-0045 decision 2 step 3: the container is rewritten to an allow-list, so the bytes that are stored hold the pixels, the
/// colour profile, the animation timing and (when not 1) the one orientation field, and nothing else (BR-IM-4 (1)). Only a file
/// the header reader accepted is passed in. The output is pixel-identical by construction.
/// </summary>
internal static class ImageStripper
{
    public static byte[] Strip(ReadOnlySpan<byte> data, ImageHeader header)
    {
        var orientation = header.Orientation;

        return header.Format switch
        {
            ImageFormat.Png => PngStripper.Strip(data, orientation),
            ImageFormat.Jpeg => JpegStripper.Strip(data, orientation),
            ImageFormat.Gif => GifStripper.Strip(data),
            ImageFormat.Webp => WebpStripper.Strip(data, header, orientation),
            _ => throw new ArgumentOutOfRangeException(nameof(header)),
        };
    }
}
