using System.Buffers.Binary;
using Mustask.Domain.Images;

namespace Mustask.Infrastructure.Images.Intake.Headers;

/// <summary>
/// The PNG chunk walk (plan-backend § 4.1): length checked against the bytes left, IHDR first and sized at once, <c>acTL</c>
/// marks an animation (ADR-0045 D-2), each <c>fcTL</c> adds a frame, <c>eXIf</c> yields the orientation, <c>IEND</c> is required.
/// </summary>
internal static class PngHeaderReader
{
    private const int MaxChunks = 100_000;

    public static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool TryRead(ReadOnlySpan<byte> data, out ImageHeader header, out ImageRefusal refusal)
    {
        header = null!;
        refusal = ImageRefusal.Format;

        if (data.Length < 8 + 25 || !data[..8].SequenceEqual(Signature))
        {
            return false;
        }

        var position = 8;
        var width = 0L;
        var height = 0L;
        var frames = 0;
        var total = 0L;
        var orientation = 1;
        var seenHeader = false;
        var chunks = 0;

        while (true)
        {
            if (++chunks > MaxChunks || data.Length - position < 12)
            {
                return false;
            }

            var length = BinaryPrimitives.ReadUInt32BigEndian(data[position..]);
            if (length > (uint)(data.Length - position - 12))
            {
                return false;
            }

            var type = data.Slice(position + 4, 4);
            var body = data.Slice(position + 8, (int)length);

            if (!seenHeader)
            {
                if (!type.SequenceEqual("IHDR"u8) || length != 13)
                {
                    return false;
                }

                width = BinaryPrimitives.ReadUInt32BigEndian(body);
                height = BinaryPrimitives.ReadUInt32BigEndian(body[4..]);

                if (ImageCaps.CheckCanvas(width, height) is { } canvasRefusal)
                {
                    refusal = canvasRefusal;
                    return false;
                }

                seenHeader = true;
            }
            else if (type.SequenceEqual("fcTL"u8) && length >= 26)
            {
                var frameWidth = BinaryPrimitives.ReadUInt32BigEndian(body[4..]);
                var frameHeight = BinaryPrimitives.ReadUInt32BigEndian(body[8..]);
                var frameLeft = BinaryPrimitives.ReadUInt32BigEndian(body[12..]);
                var frameTop = BinaryPrimitives.ReadUInt32BigEndian(body[16..]);

                if (!ImageCaps.FrameFits(frameLeft, frameTop, frameWidth, frameHeight, width, height))
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
            else if (type.SequenceEqual("eXIf"u8) && orientation == 1)
            {
                orientation = ExifOrientation.Read(body);
            }

            position += 12 + (int)length;

            if (type.SequenceEqual("IEND"u8))
            {
                break;
            }
        }

        var animated = frames > 1;

        header = new ImageHeader(
            ImageFormat.Png,
            (int)width,
            (int)height,
            animated ? frames : 1,
            animated ? total : width * height,
            animated,
            IsLossless: true,
            orientation,
            position);

        return true;
    }
}
