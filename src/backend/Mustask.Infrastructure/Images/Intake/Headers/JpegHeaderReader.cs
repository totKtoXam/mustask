using System.Buffers.Binary;
using Mustask.Domain.Images;

namespace Mustask.Infrastructure.Images.Intake.Headers;

/// <summary>The JPEG marker walk to the frame header, the first Exif orientation, the scans, and the required EOI (plan-backend § 4.1).</summary>
internal static class JpegHeaderReader
{
    public static ReadOnlySpan<byte> ExifPrefix => "Exif\0\0"u8;

    public static bool TryRead(ReadOnlySpan<byte> data, out ImageHeader header, out ImageRefusal refusal)
    {
        header = null!;
        refusal = ImageRefusal.Format;

        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8 || data[2] != 0xFF)
        {
            return false;
        }

        var position = 2;
        var width = 0;
        var height = 0;
        var orientation = 1;
        var seenFrame = false;
        var seenScan = false;

        while (true)
        {
            if (!JpegWalker.TryNext(data, ref position, out var segment))
            {
                return false;
            }

            if (segment.IsEndOfImage)
            {
                if (!seenFrame || !seenScan)
                {
                    return false;
                }

                break;
            }

            if (segment.IsStartOfFrame)
            {
                var payload = segment.Payload(data);
                if (seenFrame || payload.Length < 6)
                {
                    return false;
                }

                height = BinaryPrimitives.ReadUInt16BigEndian(payload[1..]);
                width = BinaryPrimitives.ReadUInt16BigEndian(payload[3..]);

                // Height 0 announces a DNL marker after the first scan: refused (plan-backend § 4.1).
                if (height == 0 || width == 0)
                {
                    return false;
                }

                if (ImageCaps.CheckCanvas(width, height) is { } canvasRefusal)
                {
                    refusal = canvasRefusal;
                    return false;
                }

                seenFrame = true;
            }
            else if (segment.Marker == 0xE1 && orientation == 1)
            {
                var payload = segment.Payload(data);
                if (payload.Length > ExifPrefix.Length && payload[..ExifPrefix.Length].SequenceEqual(ExifPrefix))
                {
                    orientation = ExifOrientation.Read(payload[ExifPrefix.Length..]);
                }
            }
            else if (segment.IsStartOfScan)
            {
                if (!seenFrame)
                {
                    return false;
                }

                seenScan = true;
            }
        }

        header = new ImageHeader(ImageFormat.Jpeg, width, height, 1, (long)width * height, false, false, orientation, position);

        return true;
    }
}
