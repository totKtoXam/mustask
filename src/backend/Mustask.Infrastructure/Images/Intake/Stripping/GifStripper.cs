using Mustask.Infrastructure.Images.Intake.Headers;

namespace Mustask.Infrastructure.Images.Intake.Stripping;

/// <summary>
/// Rewrites a GIF keeping the header, the screen, the tables, the image data, the graphic control extensions (timing) and the
/// <c>NETSCAPE2.0</c> / <c>ANIMEXTS1.0</c> loop extension; comments, plain text and every other application extension are
/// dropped (plan-backend § 4.2). Bytes after the trailer are never copied.
/// </summary>
internal static class GifStripper
{
    public static byte[] Strip(ReadOnlySpan<byte> data)
    {
        var output = new MemoryStream(data.Length);
        var flags = data[10];
        var position = 13;

        if ((flags & 0x80) != 0)
        {
            position += 3 * (1 << ((flags & 7) + 1));
        }

        output.Write(data[..position]);

        while (position < data.Length)
        {
            var start = position;
            var block = data[position++];

            if (block == 0x3B)
            {
                break;
            }

            if (block == 0x21)
            {
                var label = data[position++];
                var firstBlock = position;
                GifHeaderReader.TrySkipSubBlocks(data, ref position);

                if (Keeps(data, label, firstBlock))
                {
                    output.Write(data[start..position]);
                }
            }
            else
            {
                var descriptorFlags = data[position + 8];
                position += 9;

                if ((descriptorFlags & 0x80) != 0)
                {
                    position += 3 * (1 << ((descriptorFlags & 7) + 1));
                }

                position++;
                GifHeaderReader.TrySkipSubBlocks(data, ref position);
                output.Write(data[start..position]);
            }
        }

        output.WriteByte(0x3B);

        return output.ToArray();
    }

    private static bool Keeps(ReadOnlySpan<byte> data, byte label, int firstBlock)
    {
        if (label == 0xF9)
        {
            return true;
        }

        if (label != 0xFF || data[firstBlock] != 11)
        {
            return false;
        }

        var identifier = data.Slice(firstBlock + 1, 11);

        return identifier.SequenceEqual("NETSCAPE2.0"u8) || identifier.SequenceEqual("ANIMEXTS1.0"u8);
    }
}
