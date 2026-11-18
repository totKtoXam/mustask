using System.Buffers.Binary;
using Mustask.Infrastructure.Images.Intake.Headers;

namespace Mustask.Infrastructure.Images.Intake.Stripping;

/// <summary>
/// Rewrites a JPEG keeping an allow-list of segments (plan-backend § 4.2): JFIF <c>APP0</c>, the ICC <c>APP2</c>, Adobe
/// <c>APP14</c>, the tables, the frame header and the scans with their entropy-coded data byte for byte. Exif, XMP, IPTC,
/// comments, other <c>APPn</c> and everything after <c>EOI</c> are dropped. A kept orientation is one minimal Exif <c>APP1</c>.
/// </summary>
internal static class JpegStripper
{
    public static byte[] Strip(ReadOnlySpan<byte> data, int orientationToKeep)
    {
        var kept = new List<(int Start, int End, bool IsJfif)>();
        var position = 2;

        while (JpegWalker.TryNext(data, ref position, out var segment) && !segment.IsEndOfImage)
        {
            var isJfif = false;
            var keep = segment.Marker switch
            {
                0xE0 => isJfif = StartsWith(segment.Payload(data), "JFIF\0"u8),
                0xE2 => StartsWith(segment.Payload(data), "ICC_PROFILE\0"u8),
                0xEE => StartsWith(segment.Payload(data), "Adobe"u8),
                0xDB or 0xC4 or 0xDD or 0xCC or 0xDA => true,
                _ => segment.IsStartOfFrame,
            };

            if (keep)
            {
                kept.Add((segment.Start, segment.End, isJfif));
            }
        }

        var output = new MemoryStream(data.Length);
        output.WriteByte(0xFF);
        output.WriteByte(0xD8);

        // Exif goes after a leading JFIF segment, which the format wants first.
        var exifAt = kept.Count > 0 && kept[0].IsJfif ? 1 : 0;

        for (var i = 0; i <= kept.Count; i++)
        {
            if (i == exifAt && orientationToKeep > 1)
            {
                WriteExif(output, orientationToKeep);
            }

            if (i < kept.Count)
            {
                output.Write(data[kept[i].Start..kept[i].End]);
            }
        }

        output.WriteByte(0xFF);
        output.WriteByte(0xD9);

        return output.ToArray();
    }

    private static bool StartsWith(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> prefix) =>
        payload.Length >= prefix.Length && payload[..prefix.Length].SequenceEqual(prefix);

    private static void WriteExif(MemoryStream output, int orientation)
    {
        var tiff = ExifOrientation.Write(orientation);
        Span<byte> length = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)(2 + JpegHeaderReader.ExifPrefix.Length + tiff.Length));

        output.WriteByte(0xFF);
        output.WriteByte(0xE1);
        output.Write(length);
        output.Write(JpegHeaderReader.ExifPrefix);
        output.Write(tiff);
    }
}
