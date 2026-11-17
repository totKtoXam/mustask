using System.Buffers.Binary;
using Mustask.Infrastructure.Images.Intake.Headers;

namespace Mustask.Infrastructure.Images.Intake.Stripping;

/// <summary>CRC-32 of the PNG chunk format (the polynomial 0xEDB88320), for the one chunk the stripper writes.</summary>
internal static class PngCrc
{
    private static readonly uint[] Table = BuildTable();

    public static uint Compute(ReadOnlySpan<byte> type, ReadOnlySpan<byte> body)
    {
        var crc = uint.MaxValue;
        foreach (var value in type)
        {
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        foreach (var value in body)
        {
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}

/// <summary>
/// Rewrites a PNG keeping an allow-list of chunks (plan-backend § 4.2); pixel data (<c>IDAT</c>/<c>fdAT</c>) is copied byte for
/// byte. Bytes after <c>IEND</c> are never copied. A kept orientation is written as one minimal <c>eXIf</c> after <c>IHDR</c>.
/// </summary>
internal static class PngStripper
{
    private static readonly string[] Allowed =
        ["IHDR", "PLTE", "tRNS", "gAMA", "cHRM", "sRGB", "iCCP", "cICP", "sBIT", "IDAT", "acTL", "fcTL", "fdAT", "IEND"];

    public static byte[] Strip(ReadOnlySpan<byte> data, int orientationToKeep)
    {
        var output = new MemoryStream(data.Length);
        output.Write(PngHeaderReader.Signature);

        var position = 8;
        while (position + 12 <= data.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(data[position..]);
            var chunk = data.Slice(position, 12 + length);
            var type = System.Text.Encoding.ASCII.GetString(chunk.Slice(4, 4));

            if (Array.IndexOf(Allowed, type) >= 0)
            {
                output.Write(chunk);

                if (type == "IHDR" && orientationToKeep > 1)
                {
                    WriteExif(output, orientationToKeep);
                }
            }

            position += 12 + length;

            if (type == "IEND")
            {
                break;
            }
        }

        return output.ToArray();
    }

    private static void WriteExif(MemoryStream output, int orientation)
    {
        var body = ExifOrientation.Write(orientation);
        var type = "eXIf"u8;
        Span<byte> number = stackalloc byte[4];

        BinaryPrimitives.WriteUInt32BigEndian(number, (uint)body.Length);
        output.Write(number);
        output.Write(type);
        output.Write(body);
        BinaryPrimitives.WriteUInt32BigEndian(number, PngCrc.Compute(type, body));
        output.Write(number);
    }
}
