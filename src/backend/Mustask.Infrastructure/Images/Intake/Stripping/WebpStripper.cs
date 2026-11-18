using System.Buffers.Binary;
using Mustask.Infrastructure.Images.Intake.Headers;

namespace Mustask.Infrastructure.Images.Intake.Stripping;

/// <summary>
/// Rewrites a WebP keeping <c>VP8X</c> (flags rewritten), <c>ICCP</c>, <c>ANIM</c>, <c>ANMF</c>, <c>ALPH</c>, <c>VP8 </c> and
/// <c>VP8L</c>; <c>EXIF</c>, <c>XMP </c> and unknown chunks are dropped and the RIFF size rewritten (plan-backend § 4.2). A kept
/// orientation is one minimal <c>EXIF</c> chunk with its <c>VP8X</c> flag.
/// </summary>
internal static class WebpStripper
{
    private const byte ExifFlag = 0x08;
    private const byte XmpFlag = 0x04;

    private static readonly string[] Allowed = ["ICCP", "ANIM", "ANMF", "ALPH", "VP8 ", "VP8L"];

    public static byte[] Strip(ReadOnlySpan<byte> data, ImageHeader header, int orientationToKeep)
    {
        var riffSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        var end = 8 + riffSize;
        var body = new MemoryStream(data.Length);
        var wantExif = orientationToKeep > 1;
        var hasExtended = false;
        var position = 12;

        // A simple file has no VP8X; one is made when the orientation has to be kept.
        var chunks = new List<(string Type, int Start, int Size)>();
        while (position + 8 <= end)
        {
            var type = System.Text.Encoding.ASCII.GetString(data.Slice(position, 4));
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(data[(position + 4)..]);
            chunks.Add((type, position + 8, size));
            hasExtended |= type == "VP8X";
            position += 8 + size + (size & 1);
        }

        if (!hasExtended && wantExif)
        {
            WriteChunk(body, "VP8X", MakeExtended(header, ExifFlag));
        }

        foreach (var (type, start, size) in chunks)
        {
            if (type == "VP8X")
            {
                var extended = data.Slice(start, size).ToArray();
                extended[0] = (byte)((extended[0] & ~(ExifFlag | XmpFlag)) | (wantExif ? ExifFlag : 0));
                WriteChunk(body, type, extended);
            }
            else if (Array.IndexOf(Allowed, type) >= 0)
            {
                WriteChunk(body, type, data.Slice(start, size));
            }
        }

        if (wantExif)
        {
            WriteChunk(body, "EXIF", ExifOrientation.Write(orientationToKeep));
        }

        var output = new MemoryStream((int)body.Length + 12);
        Span<byte> number = stackalloc byte[4];

        output.Write("RIFF"u8);
        BinaryPrimitives.WriteUInt32LittleEndian(number, (uint)(body.Length + 4));
        output.Write(number);
        output.Write("WEBP"u8);
        body.Position = 0;
        body.CopyTo(output);

        return output.ToArray();
    }

    private static byte[] MakeExtended(ImageHeader header, byte flags)
    {
        var body = new byte[10];
        body[0] = flags;
        var width = header.Width - 1;
        var height = header.Height - 1;
        body[4] = (byte)width;
        body[5] = (byte)(width >> 8);
        body[6] = (byte)(width >> 16);
        body[7] = (byte)height;
        body[8] = (byte)(height >> 8);
        body[9] = (byte)(height >> 16);

        return body;
    }

    private static void WriteChunk(MemoryStream output, string type, ReadOnlySpan<byte> body)
    {
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)body.Length);

        output.Write(System.Text.Encoding.ASCII.GetBytes(type));
        output.Write(size);
        output.Write(body);

        if ((body.Length & 1) != 0)
        {
            output.WriteByte(0);
        }
    }
}
