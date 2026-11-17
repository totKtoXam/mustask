using System.Buffers.Binary;
using System.Text;
using Mustask.Infrastructure.Images.Intake.Stripping;
using NetVips;

namespace Mustask.Infrastructure.Tests.Images;

/// <summary>
/// Builds the pictures of the image tests (feat-078 plan-backend § 13). Nothing is checked in beyond tiny samples: large
/// pictures are generated here from fixed seeds, and the container tricks (metadata, polyglot tails, bombs, frame counts)
/// are written by hand as bytes, so a test can name exactly what is in the file.
/// </summary>
internal static class TestImages
{
    /// <summary>Gradient plus seeded noise: compressible like a photograph, and its size grows with <paramref name="sigma"/>.</summary>
    public static NetVips.Image Photo(int width, int height, double sigma = 12, int seed = 7)
    {
        var xy = NetVips.Image.Xyz(width, height);
        var gx = xy.ExtractBand(0) * (80.0 / width);
        var gy = xy.ExtractBand(1) * (80.0 / height);
        var noise = NetVips.Image.Gaussnoise(width, height, sigma: sigma, mean: 0, seed: seed).Cast(Enums.BandFormat.Float);
        var rgb = gx.Bandjoin(new NetVips.Image[] { gy, gx + gy }) + new double[] { 90, 110, 130 } + noise;

        return rgb.Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
    }

    public static byte[] Jpeg(int width, int height, int quality = 90, double sigma = 12, int seed = 7) =>
        Photo(width, height, sigma, seed).JpegsaveBuffer(q: quality);

    public static byte[] Png(int width, int height, double sigma = 12, int seed = 7) => Photo(width, height, sigma, seed).PngsaveBuffer();

    public static byte[] Webp(int width, int height, int quality = 80) => Photo(width, height).WebpsaveBuffer(q: quality);

    /// <summary>A flat screenshot-like picture: few colours, sharp edges.</summary>
    public static byte[] Screenshot(int width, int height)
    {
        var background = NetVips.Image.Black(width, height, bands: 3) + new double[] { 245, 245, 245 };
        var panel = NetVips.Image.Black(width / 2, height / 2, bands: 3) + new double[] { 30, 60, 200 };
        var text = NetVips.Image.Black(width / 3, 6, bands: 3) + new double[] { 20, 20, 20 };

        return background.Insert(panel, width / 8, height / 8).Insert(text, width / 8, (height * 3) / 4)
            .Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb).PngsaveBuffer();
    }

    /// <summary>An animated GIF of <paramref name="frames"/> single-colour frames with a delay each, written by libvips.</summary>
    public static byte[] AnimatedGif(int frames, int size = 8)
    {
        var list = Enumerable.Range(0, frames)
            .Select(i => NetVips.Image.Black(size, size, bands: 3) + new double[] { i * 6 % 256, 255 - (i * 6 % 256), 128 })
            .ToArray();
        var joined = NetVips.Image.Arrayjoin(list, across: 1).Cast(Enums.BandFormat.Uchar);
        var mutated = joined.Mutate(mutable =>
        {
            mutable.Set(GValue.GIntType, "page-height", size);
            mutable.Set(GValue.ArrayIntType, "delay", Enumerable.Repeat(100, frames).ToArray());
            mutable.Set(GValue.GIntType, "loop", 0);
        });

        return mutated.GifsaveBuffer(dither: 0);
    }

    // ---- Hand-written containers -------------------------------------------------------------------------------------

    /// <summary>A PNG signature and IHDR announcing the given size, nothing after it: the header of a bomb.</summary>
    public static byte[] PngHeaderOnly(uint width, uint height)
    {
        var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8;
        ihdr[9] = 2;
        WriteChunk(output, "IHDR", ihdr);
        WriteChunk(output, "IEND", []);

        return output.ToArray();
    }

    /// <summary>A GIF whose frames only have descriptors and empty LZW data: enough for the header walk, not for a decoder.</summary>
    public static byte[] GifOfFrames(int canvasWidth, int canvasHeight, int frames, int frameWidth, int frameHeight)
    {
        var output = new MemoryStream();
        output.Write("GIF89a"u8);
        Span<byte> number = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(number, (ushort)canvasWidth);
        output.Write(number);
        BinaryPrimitives.WriteUInt16LittleEndian(number, (ushort)canvasHeight);
        output.Write(number);
        output.Write([0x00, 0x00, 0x00]);

        for (var i = 0; i < frames; i++)
        {
            output.WriteByte(0x2C);
            output.Write([0, 0, 0, 0]);
            BinaryPrimitives.WriteUInt16LittleEndian(number, (ushort)frameWidth);
            output.Write(number);
            BinaryPrimitives.WriteUInt16LittleEndian(number, (ushort)frameHeight);
            output.Write(number);
            output.Write([0x00, 0x02, 0x02, 0x4C, 0x01, 0x00]);
        }

        output.WriteByte(0x3B);

        return output.ToArray();
    }

    /// <summary>An APNG made of the first image's compressed data repeated as <paramref name="frames"/> frames (not decodable as an animation, readable by the header walk).</summary>
    public static byte[] Apng(int frames, int size = 8)
    {
        var still = NetVips.Image.Black(size, size, bands: 3).Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb).PngsaveBuffer();
        var idat = ChunkBodies(still, "IDAT").First();
        var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(output, "IHDR", ChunkBodies(still, "IHDR").First());

        var control = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(control, (uint)frames);
        WriteChunk(output, "acTL", control);

        uint sequence = 0;
        for (var i = 0; i < frames; i++)
        {
            var frameControl = new byte[26];
            BinaryPrimitives.WriteUInt32BigEndian(frameControl, sequence++);
            BinaryPrimitives.WriteUInt32BigEndian(frameControl.AsSpan(4), (uint)size);
            BinaryPrimitives.WriteUInt32BigEndian(frameControl.AsSpan(8), (uint)size);
            BinaryPrimitives.WriteUInt16BigEndian(frameControl.AsSpan(20), 1);
            BinaryPrimitives.WriteUInt16BigEndian(frameControl.AsSpan(22), 10);
            WriteChunk(output, "fcTL", frameControl);

            if (i == 0)
            {
                WriteChunk(output, "IDAT", idat);
            }
            else
            {
                var frameData = new byte[4 + idat.Length];
                BinaryPrimitives.WriteUInt32BigEndian(frameData, sequence++);
                idat.CopyTo(frameData, 4);
                WriteChunk(output, "fdAT", frameData);
            }
        }

        WriteChunk(output, "IEND", []);

        return output.ToArray();
    }

    /// <summary>Adds a <c>tEXt</c> chunk, a <c>tIME</c> chunk and bytes after <c>IEND</c> to a PNG.</summary>
    public static byte[] PngWithMetadataAndTail(byte[] png, byte[] tail)
    {
        var output = new MemoryStream();
        output.Write(png.AsSpan(0, 8));
        var position = 8;

        while (position < png.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position));
            var type = Encoding.ASCII.GetString(png, position + 4, 4);
            output.Write(png.AsSpan(position, 12 + length));

            if (type == "IHDR")
            {
                WriteChunk(output, "tEXt", Encoding.ASCII.GetBytes("Author\0Alice Example"));
                WriteChunk(output, "tIME", [0x07, 0xEA, 0x01, 0x01, 0x00, 0x00, 0x00]);
                WriteChunk(output, "eXIf", Tiff(6, withGps: true));
            }

            position += 12 + length;
            if (type == "IEND")
            {
                break;
            }
        }

        output.Write(tail);

        return output.ToArray();
    }

    /// <summary>Inserts Exif (orientation and GPS), XMP, a comment and an IPTC segment after SOI of a JPEG, and a tail after EOI.</summary>
    public static byte[] JpegWithCameraData(byte[] jpeg, int orientation, byte[]? tail = null)
    {
        var output = new MemoryStream();
        output.Write(jpeg.AsSpan(0, 2));

        WriteSegment(output, 0xE1, [.. "Exif\0\0"u8, .. Tiff(orientation, withGps: true)]);
        WriteSegment(output, 0xE1, [.. "http://ns.adobe.com/xap/1.0/\0"u8, .. "<x:xmpmeta>secret</x:xmpmeta>"u8]);
        WriteSegment(output, 0xED, [.. "Photoshop 3.0\0"u8, .. "8BIM-secret"u8]);
        WriteSegment(output, 0xFE, "shot with a camera of Alice"u8);
        output.Write(jpeg.AsSpan(2));

        if (tail is not null)
        {
            output.Write(tail);
        }

        return output.ToArray();
    }

    /// <summary>A TIFF block with the orientation and, when asked, a GPS pointer and a Make string the stripper must drop.</summary>
    public static byte[] Tiff(int orientation, bool withGps)
    {
        var entries = withGps ? 3 : 1;
        var tiff = new byte[8 + 2 + (entries * 12) + 4 + 32];
        tiff[0] = (byte)'I';
        tiff[1] = (byte)'I';
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(2), 42);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(8), (ushort)entries);

        var entry = 10;
        if (withGps)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(entry), 0x010F);
            BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(entry + 2), 2);
            BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(entry + 4), 6);
            BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(entry + 8), (uint)(10 + (entries * 12) + 4));
            entry += 12;
        }

        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(entry), 0x0112);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(entry + 2), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(entry + 4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(entry + 8), (ushort)orientation);
        entry += 12;

        if (withGps)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(entry), 0x8825);
            BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(entry + 2), 4);
            BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(entry + 4), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(entry + 8), 0x1000);
            Encoding.ASCII.GetBytes("Canon\0").CopyTo(tiff, 10 + (entries * 12) + 4);
        }

        return tiff;
    }

    public static IEnumerable<byte[]> ChunkBodies(byte[] png, string type)
    {
        var position = 8;
        while (position + 12 <= png.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position));
            if (Encoding.ASCII.GetString(png, position + 4, 4) == type)
            {
                yield return png.AsSpan(position + 8, length).ToArray();
            }

            position += 12 + length;
        }
    }

    public static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> body)
    {
        Span<byte> number = stackalloc byte[4];
        var typeBytes = Encoding.ASCII.GetBytes(type);

        BinaryPrimitives.WriteUInt32BigEndian(number, (uint)body.Length);
        output.Write(number);
        output.Write(typeBytes);
        output.Write(body);
        BinaryPrimitives.WriteUInt32BigEndian(number, PngCrc.Compute(typeBytes, body));
        output.Write(number);
    }

    public static void WriteSegment(Stream output, byte marker, ReadOnlySpan<byte> payload)
    {
        Span<byte> length = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)(payload.Length + 2));

        output.WriteByte(0xFF);
        output.WriteByte(marker);
        output.Write(length);
        output.Write(payload);
    }

    /// <summary>The entropy-coded data of the first scan of a JPEG, to compare byte for byte after stripping.</summary>
    public static byte[] ScanData(byte[] jpeg)
    {
        var position = 2;
        while (Mustask.Infrastructure.Images.Intake.Headers.JpegWalker.TryNext(jpeg, ref position, out var segment) && !segment.IsEndOfImage)
        {
            if (segment.IsStartOfScan)
            {
                return jpeg.AsSpan(segment.HeaderEnd, segment.End - segment.HeaderEnd).ToArray();
            }
        }

        throw new InvalidOperationException("No scan.");
    }
}
