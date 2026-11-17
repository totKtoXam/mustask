using System.Buffers.Binary;
using Mustask.Domain.Images;

namespace Mustask.Infrastructure.Images.Intake.Headers;

/// <summary>
/// The WebP RIFF walk (plan-backend § 4.1): the RIFF size must fit the buffer; <c>VP8 </c> (lossy, size from the frame header),
/// <c>VP8L</c> (lossless, 14-bit sizes), <c>VP8X</c> (canvas, flags), <c>ANIM</c> and <c>ANMF</c> (frame sizes and count).
/// </summary>
internal static class WebpHeaderReader
{
    public const byte AnimationFlag = 0x02;

    public static bool IsWebp(ReadOnlySpan<byte> data) =>
        data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8);

    public static bool TryRead(ReadOnlySpan<byte> data, out ImageHeader header, out ImageRefusal refusal)
    {
        header = null!;
        refusal = ImageRefusal.Format;

        if (!IsWebp(data))
        {
            return false;
        }

        var riffSize = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        if (riffSize < 4 || riffSize > (uint)(data.Length - 8))
        {
            return false;
        }

        var end = 8 + (int)riffSize;
        var position = 12;
        long width = 0;
        long height = 0;
        var extended = false;
        var animationFlag = false;
        var lossless = false;
        var seenImage = false;
        var frames = 0;
        var total = 0L;
        var orientation = 1;
        var chunks = 0;

        while (position < end)
        {
            if (++chunks > 100_000 || end - position < 8)
            {
                return false;
            }

            var type = data.Slice(position, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(data[(position + 4)..]);
            if (size > (uint)(end - position - 8))
            {
                return false;
            }

            var body = data.Slice(position + 8, (int)size);

            if (type.SequenceEqual("VP8X"u8))
            {
                if (size < 10 || extended)
                {
                    return false;
                }

                extended = true;
                animationFlag = (body[0] & AnimationFlag) != 0;
                width = 1 + (body[4] | (body[5] << 8) | (body[6] << 16));
                height = 1 + (body[7] | (body[8] << 8) | (body[9] << 16));

                if (ImageCaps.CheckCanvas(width, height) is { } canvasRefusal)
                {
                    refusal = canvasRefusal;
                    return false;
                }
            }
            else if (type.SequenceEqual("VP8 "u8))
            {
                if (size < 10 || body[3] != 0x9D || body[4] != 0x01 || body[5] != 0x2A)
                {
                    return false;
                }

                var imageWidth = BinaryPrimitives.ReadUInt16LittleEndian(body[6..]) & 0x3FFF;
                var imageHeight = BinaryPrimitives.ReadUInt16LittleEndian(body[8..]) & 0x3FFF;
                if (!TakeImage(ref seenImage, extended, ref width, ref height, imageWidth, imageHeight, out refusal))
                {
                    return false;
                }
            }
            else if (type.SequenceEqual("VP8L"u8))
            {
                if (size < 5 || body[0] != 0x2F)
                {
                    return false;
                }

                var bits = BinaryPrimitives.ReadUInt32LittleEndian(body[1..]);
                if (!TakeImage(ref seenImage, extended, ref width, ref height, (int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1, out refusal))
                {
                    return false;
                }

                lossless = true;
            }
            else if (type.SequenceEqual("ANMF"u8))
            {
                if (size < 16 || !extended)
                {
                    return false;
                }

                var frameWidth = 1 + (body[6] | (body[7] << 8) | (body[8] << 16));
                var frameHeight = 1 + (body[9] | (body[10] << 8) | (body[11] << 16));
                frames++;
                total += (long)frameWidth * frameHeight;

                if (ImageCaps.CheckFrames(frames, total) is { } frameRefusal)
                {
                    refusal = frameRefusal;
                    return false;
                }
            }
            else if (type.SequenceEqual("EXIF"u8) && orientation == 1)
            {
                // A leading "Exif\0\0" is tolerated, as some writers add it.
                orientation = ExifOrientation.Read(body.Length > 6 && body[..6].SequenceEqual(JpegHeaderReader.ExifPrefix) ? body[6..] : body);
            }

            position += 8 + (int)size + (int)(size & 1);
        }

        var animated = animationFlag && frames > 1;
        if (!animated && !seenImage)
        {
            return false;
        }

        header = new ImageHeader(
            ImageFormat.Webp,
            (int)width,
            (int)height,
            animated ? frames : 1,
            animated ? total : width * height,
            animated,
            lossless,
            orientation,
            end + (int)(riffSize & 1));

        return true;
    }

    /// <summary>Records a still image chunk's size; in a simple file it is the canvas, in an extended one it must fit it.</summary>
    private static bool TakeImage(ref bool seenImage, bool extended, ref long width, ref long height, int imageWidth, int imageHeight, out ImageRefusal refusal)
    {
        refusal = ImageRefusal.Format;

        if (seenImage)
        {
            return false;
        }

        seenImage = true;

        if (!extended)
        {
            width = imageWidth;
            height = imageHeight;

            if (ImageCaps.CheckCanvas(width, height) is { } canvasRefusal)
            {
                refusal = canvasRefusal;
                return false;
            }
        }

        return true;
    }
}
