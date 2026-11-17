using System.Buffers.Binary;
using Mustask.Domain.Images;

namespace Mustask.Infrastructure.Images.Intake.Headers;

/// <summary>
/// The GIF block walk (plan-backend § 4.1): logical screen, optional global table, then image descriptors (frame size counted,
/// LZW sub-blocks skipped unread) and extensions (skipped by sub-block); the trailer is required. More than one frame is animated.
/// </summary>
internal static class GifHeaderReader
{
    public static bool IsGif(ReadOnlySpan<byte> data) =>
        data.Length >= 6 && (data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8));

    public static bool TryRead(ReadOnlySpan<byte> data, out ImageHeader header, out ImageRefusal refusal)
    {
        header = null!;
        refusal = ImageRefusal.Format;

        if (!IsGif(data) || data.Length < 13)
        {
            return false;
        }

        var width = BinaryPrimitives.ReadUInt16LittleEndian(data[6..]);
        var height = BinaryPrimitives.ReadUInt16LittleEndian(data[8..]);

        if (ImageCaps.CheckCanvas(width, height) is { } canvasRefusal)
        {
            refusal = canvasRefusal;
            return false;
        }

        var flags = data[10];
        var position = 13;

        if ((flags & 0x80) != 0)
        {
            position += 3 * (1 << ((flags & 7) + 1));
        }

        var frames = 0;
        var total = 0L;

        while (true)
        {
            if (position >= data.Length)
            {
                return false;
            }

            var block = data[position++];

            if (block == 0x3B)
            {
                break;
            }

            if (block == 0x21)
            {
                if (position >= data.Length)
                {
                    return false;
                }

                position++;
                if (!TrySkipSubBlocks(data, ref position))
                {
                    return false;
                }
            }
            else if (block == 0x2C)
            {
                if (position + 9 > data.Length)
                {
                    return false;
                }

                var frameWidth = BinaryPrimitives.ReadUInt16LittleEndian(data[(position + 4)..]);
                var frameHeight = BinaryPrimitives.ReadUInt16LittleEndian(data[(position + 6)..]);
                var descriptorFlags = data[position + 8];
                position += 9;

                if ((descriptorFlags & 0x80) != 0)
                {
                    position += 3 * (1 << ((descriptorFlags & 7) + 1));
                }

                // The LZW minimum code size, then the data sub-blocks.
                position++;
                if (position > data.Length || !TrySkipSubBlocks(data, ref position))
                {
                    return false;
                }

                frames++;
                total += (long)frameWidth * frameHeight;

                if (ImageCaps.CheckFrames(frames, total) is { } frameRefusal)
                {
                    refusal = frameRefusal;
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        if (frames == 0)
        {
            return false;
        }

        var animated = frames > 1;

        header = new ImageHeader(ImageFormat.Gif, width, height, frames, animated ? total : (long)width * height, animated, false, 1, position);

        return true;
    }

    /// <summary>Advances past sub-blocks up to and including the zero terminator.</summary>
    public static bool TrySkipSubBlocks(ReadOnlySpan<byte> data, ref int position)
    {
        while (true)
        {
            if (position >= data.Length)
            {
                return false;
            }

            var size = data[position++];
            if (size == 0)
            {
                return true;
            }

            position += size;
            if (position > data.Length)
            {
                return false;
            }
        }
    }
}
