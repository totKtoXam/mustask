using System.Text;
using Mustask.Infrastructure.Images.Intake.Headers;
using Mustask.Infrastructure.Images.Intake.Stripping;

namespace Mustask.Infrastructure.Tests.Images;

public sealed class ImageStripperTests
{
    private static byte[] Strip(byte[] data)
    {
        Assert.True(ImageHeaderReader.TryRead(data, out var header, out _));

        return ImageStripper.Strip(data, header);
    }

    private static bool Contains(byte[] data, string text) => data.AsSpan().IndexOf(Encoding.ASCII.GetBytes(text)) >= 0;

    // US-2 · "Metadata removed, orientation kept (600 KB)": the entropy-coded data is byte-equal, only the orientation survives.
    [Fact]
    public void Strip_CameraJpeg_KeepsScanDataAndOnlyTheOrientation()
    {
        var source = TestImages.JpegWithCameraData(TestImages.Jpeg(300, 200), orientation: 6, tail: Encoding.ASCII.GetBytes("<script>x</script>"));

        var stripped = Strip(source);

        Assert.Equal(TestImages.ScanData(source), TestImages.ScanData(stripped));
        Assert.Equal(6, ReadHeader(stripped).Orientation);
        Assert.False(Contains(stripped, "Canon"));
        Assert.False(Contains(stripped, "xmpmeta"));
        Assert.False(Contains(stripped, "Photoshop"));
        Assert.False(Contains(stripped, "camera of Alice"));
        Assert.False(Contains(stripped, "<script>"));
        Assert.True(stripped.Length < source.Length);
        Assert.Equal(stripped.Length, ReadHeader(stripped).ContainerEnd);
    }

    // US-2 · "Metadata removed": no orientation, no Exif segment at all.
    [Fact]
    public void Strip_JpegOrientationOne_HasNoExifSegment()
    {
        var stripped = Strip(TestImages.JpegWithCameraData(TestImages.Jpeg(100, 80), orientation: 1));

        Assert.False(Contains(stripped, "Exif"));
        Assert.Equal(1, ReadHeader(stripped).Orientation);
    }

    // US-2 · "Metadata removed": a PNG keeps its pixels and colour chunks, loses text, time and tail; the orientation survives.
    [Fact]
    public void Strip_Png_DropsTextTimeAndTail_KeepsIdatByteForByte_AndTheOrientation()
    {
        var source = TestImages.PngWithMetadataAndTail(TestImages.Png(64, 48), Encoding.ASCII.GetBytes("MZ-polyglot-tail"));

        var stripped = Strip(source);

        Assert.False(Contains(stripped, "Alice"));
        Assert.False(Contains(stripped, "tIME"));
        Assert.False(Contains(stripped, "polyglot"));
        Assert.Equal(TestImages.ChunkBodies(source, "IDAT"), TestImages.ChunkBodies(stripped, "IDAT"));
        Assert.Equal(6, ReadHeader(stripped).Orientation);
    }

    // US-2 · "Metadata removed": the stripped PNG still decodes to the same pixels.
    [Fact]
    public void Strip_Png_DecodesToTheSamePixels()
    {
        var source = TestImages.PngWithMetadataAndTail(TestImages.Png(64, 48), []);
        var stripped = Strip(source);

        var before = NetVips.Image.PngloadBuffer(source).WriteToMemory<byte>();
        var after = NetVips.Image.PngloadBuffer(stripped).WriteToMemory<byte>();

        Assert.Equal(before, after);
    }

    // US-2 · "Animated keeps animation": frames and delays stay, comment and tail go.
    [Fact]
    public void Strip_AnimatedGif_KeepsEveryFrameAndTheLoop_DropsCommentAndTail()
    {
        var source = TestImages.AnimatedGif(40);
        var withComment = InsertGifComment(source, "secret comment");
        var withTail = withComment.Concat(Encoding.ASCII.GetBytes("<html>tail")).ToArray();

        var stripped = Strip(withTail);

        var header = ReadHeader(stripped);
        Assert.Equal(40, header.FrameCount);
        Assert.True(header.IsAnimated);
        Assert.False(Contains(stripped, "secret comment"));
        Assert.False(Contains(stripped, "<html>"));
        Assert.True(Contains(stripped, "NETSCAPE2.0"));
        Assert.Equal(stripped.Length, header.ContainerEnd);
    }

    // US-2 · "Animated keeps animation" (ADR-0045 D-2): an APNG keeps its frame chunks.
    [Fact]
    public void Strip_Apng_KeepsFrameChunks()
    {
        var stripped = Strip(TestImages.Apng(4));

        Assert.Equal(4, ReadHeader(stripped).FrameCount);
        Assert.Equal(3, TestImages.ChunkBodies(stripped, "fdAT").Count());
    }

    // US-2 · "Metadata removed": a WebP loses EXIF and XMP chunks; the size stays right.
    [Fact]
    public void Strip_WebpWithExifAndXmp_DropsThemAndStaysValid()
    {
        var webp = TestImages.Webp(64, 48);
        var with = AddWebpChunks(webp);

        var stripped = Strip(with);

        Assert.False(Contains(stripped, "EXIF"));
        Assert.False(Contains(stripped, "XMP "));
        Assert.False(Contains(stripped, "secret"));
        var header = ReadHeader(stripped);
        Assert.Equal(64, header.Width);
        Assert.Equal(stripped.Length, header.ContainerEnd);
        Assert.Equal(64, NetVips.Image.WebploadBuffer(stripped).Width);
    }

    [Fact]
    public void Strip_WebpWithExifOrientation_KeepsOnlyTheOrientation()
    {
        var with = AddWebpChunks(TestImages.Webp(64, 48), orientation: 6);

        var stripped = Strip(with);

        Assert.Equal(6, ReadHeader(stripped).Orientation);
        Assert.False(Contains(stripped, "secret"));
        Assert.Equal(64, NetVips.Image.WebploadBuffer(stripped).Width);
    }

    private static ImageHeader ReadHeader(byte[] data)
    {
        Assert.True(ImageHeaderReader.TryRead(data, out var header, out var refusal), refusal.ToString());

        return header;
    }

    private static byte[] InsertGifComment(byte[] gif, string comment)
    {
        var flags = gif[10];
        var tableEnd = 13 + ((flags & 0x80) != 0 ? 3 * (1 << ((flags & 7) + 1)) : 0);
        var extension = new List<byte> { 0x21, 0xFE, (byte)comment.Length };
        extension.AddRange(Encoding.ASCII.GetBytes(comment));
        extension.Add(0);

        return [.. gif[..tableEnd], .. extension, .. gif[tableEnd..]];
    }

    private static byte[] AddWebpChunks(byte[] webp, int orientation = 1)
    {
        var body = new MemoryStream();
        body.Write(webp.AsSpan(12));
        var exif = TestImages.Tiff(orientation, withGps: true);
        WriteWebpChunk(body, "EXIF", exif);
        WriteWebpChunk(body, "XMP ", Encoding.ASCII.GetBytes("<x>secret</x>"));

        var output = new MemoryStream();
        output.Write("RIFF"u8);
        output.Write(BitConverter.GetBytes((uint)(body.Length + 4)));
        output.Write("WEBP"u8);
        body.Position = 0;
        body.CopyTo(output);

        return output.ToArray();
    }

    private static void WriteWebpChunk(Stream output, string type, byte[] body)
    {
        output.Write(Encoding.ASCII.GetBytes(type));
        output.Write(BitConverter.GetBytes((uint)body.Length));
        output.Write(body);
        if ((body.Length & 1) != 0)
        {
            output.WriteByte(0);
        }
    }
}
