using System.Buffers.Binary;

namespace Mustask.Infrastructure.Images.Intake.Headers;

/// <summary>
/// The one EXIF field the store keeps: the orientation, IFD0 tag 0x0112 (BR-IM-4 (1)). Bounded: it reads the TIFF header,
/// the entry count of IFD0 and that many entries inside the buffer, nothing else; no other IFD is followed.
/// </summary>
internal static class ExifOrientation
{
    private const ushort OrientationTag = 0x0112;
    private const ushort ShortType = 3;

    /// <summary>The orientation 1..8 in a TIFF block (the bytes after <c>Exif\0\0</c> in a JPEG), or 1 when absent or malformed.</summary>
    public static int Read(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 14)
        {
            return 1;
        }

        bool bigEndian;
        if (tiff[0] == (byte)'M' && tiff[1] == (byte)'M')
        {
            bigEndian = true;
        }
        else if (tiff[0] == (byte)'I' && tiff[1] == (byte)'I')
        {
            bigEndian = false;
        }
        else
        {
            return 1;
        }

        if (U16(tiff, bigEndian, 2) != 42)
        {
            return 1;
        }

        var ifd = U32(tiff, bigEndian, 4);
        if (ifd < 8 || ifd > tiff.Length - 2)
        {
            return 1;
        }

        var count = U16(tiff, bigEndian, (int)ifd);
        var entries = (int)ifd + 2;

        for (var i = 0; i < count; i++)
        {
            var entry = entries + (i * 12);
            if (entry + 12 > tiff.Length)
            {
                return 1;
            }

            if (U16(tiff, bigEndian, entry) == OrientationTag)
            {
                if (U16(tiff, bigEndian, entry + 2) != ShortType || U32(tiff, bigEndian, entry + 4) != 1)
                {
                    return 1;
                }

                var value = U16(tiff, bigEndian, entry + 8);

                return value is >= 1 and <= 8 ? value : 1;
            }
        }

        return 1;
    }

    private static ushort U16(ReadOnlySpan<byte> tiff, bool bigEndian, int offset) => bigEndian
        ? BinaryPrimitives.ReadUInt16BigEndian(tiff[offset..])
        : BinaryPrimitives.ReadUInt16LittleEndian(tiff[offset..]);

    private static uint U32(ReadOnlySpan<byte> tiff, bool bigEndian, int offset) => bigEndian
        ? BinaryPrimitives.ReadUInt32BigEndian(tiff[offset..])
        : BinaryPrimitives.ReadUInt32LittleEndian(tiff[offset..]);

    /// <summary>The minimal TIFF block: one IFD0 holding only the orientation (26 bytes, big-endian).</summary>
    public static byte[] Write(int orientation)
    {
        if (orientation is < 2 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(orientation), orientation, "Only a changed orientation (2..8) is written.");
        }

        var tiff = new byte[26];
        tiff[0] = (byte)'M';
        tiff[1] = (byte)'M';
        BinaryPrimitives.WriteUInt16BigEndian(tiff.AsSpan(2), 42);
        BinaryPrimitives.WriteUInt32BigEndian(tiff.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16BigEndian(tiff.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16BigEndian(tiff.AsSpan(10), OrientationTag);
        BinaryPrimitives.WriteUInt16BigEndian(tiff.AsSpan(12), ShortType);
        BinaryPrimitives.WriteUInt32BigEndian(tiff.AsSpan(14), 1);
        BinaryPrimitives.WriteUInt16BigEndian(tiff.AsSpan(18), (ushort)orientation);

        // Bytes 20..21 are the value's padding, 22..25 the offset of the next IFD (none).
        return tiff;
    }
}
