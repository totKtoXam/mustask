using System.Text;
using Mustask.Domain.Images;
using Mustask.Infrastructure.Images.Intake.Headers;

namespace Mustask.Infrastructure.Tests.Images;

public sealed class ImageHeaderReaderTests
{
    private static (bool Ok, ImageHeader? Header, ImageRefusal Refusal) Read(byte[] data)
    {
        var ok = ImageHeaderReader.TryRead(data, out var header, out var refusal);

        return (ok, ok ? header : null, refusal);
    }

    // US-2 · "Format from content": one case per row of the outline.
    [Fact]
    public void Read_RealPngJpegGifWebp_AreRecognisedByContent()
    {
        Assert.Equal(ImageFormat.Png, Read(TestImages.Png(40, 30)).Header!.Format);
        Assert.Equal(ImageFormat.Jpeg, Read(TestImages.Jpeg(40, 30)).Header!.Format);
        Assert.Equal(ImageFormat.Gif, Read(TestImages.AnimatedGif(2)).Header!.Format);
        Assert.Equal(ImageFormat.Webp, Read(TestImages.Webp(40, 30)).Header!.Format);
    }

    public static TheoryData<string, byte[]> Foreign() => new()
    {
        { "svg", Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>") },
        { "html", Encoding.UTF8.GetBytes("<html><body><img src=x onerror=alert(1)></body></html>") },
        { "heic", [.. new byte[] { 0, 0, 0, 0x18 }, .. "ftypheic"u8, .. new byte[32]] },
        { "avif", [.. new byte[] { 0, 0, 0, 0x1C }, .. "ftypavif"u8, .. new byte[32]] },
        { "bmp", [.. "BM"u8, .. new byte[64]] },
        { "tiff", [0x49, 0x49, 0x2A, 0x00, .. new byte[64]] },
        { "ico", [0, 0, 1, 0, 1, 0, .. new byte[64]] },
        { "zip", [.. "PK\u0003\u0004"u8, .. new byte[64]] },
        { "empty", [] },
        { "one byte", [0xFF] },
    };

    // US-2 · "Format from content": anything else is refused as the format.
    [Theory]
    [MemberData(nameof(Foreign))]
    public void Read_AnythingElse_IsFormat(string name, byte[] data)
    {
        var result = Read(data);

        Assert.False(result.Ok, name);
        Assert.Equal(ImageRefusal.Format, result.Refusal);
    }

    // US-2 · "Format from content": a truncated file of a known format is a damaged one.
    [Fact]
    public void Read_TruncatedPngAndJpeg_AreFormat()
    {
        var png = TestImages.Png(40, 30);
        var jpeg = TestImages.Jpeg(40, 30);

        Assert.Equal(ImageRefusal.Format, Read(png[..(png.Length - 10)]).Refusal);
        Assert.Equal(ImageRefusal.Format, Read(jpeg[..(jpeg.Length - 10)]).Refusal);
    }

    // US-2 · "Caps": the boundary rows.
    [Theory]
    [InlineData(20_000u, 3_000u, true)]
    [InlineData(20_001u, 100u, false)]
    [InlineData(100u, 20_001u, false)]
    [InlineData(8_000u, 8_000u, false)]
    [InlineData(7_745u, 7_745u, true)]
    public void Read_PngSizeAgainstTheCaps(uint width, uint height, bool accepted)
    {
        var result = Read(TestImages.PngHeaderOnly(width, height));

        Assert.Equal(accepted, result.Ok);
        if (!accepted)
        {
            Assert.Equal(ImageRefusal.Dimensions, result.Refusal);
        }
    }

    // US-2 · "Caps": a 40 KB file that declares 64 megapixels is refused from its header, with nothing decoded.
    [Fact]
    public void Read_BombPng_IsDimensions_FromTheHeaderAlone()
    {
        var bomb = TestImages.PngHeaderOnly(8_000, 8_000);

        Assert.True(bomb.Length < 100);
        Assert.Equal(ImageRefusal.Dimensions, Read(bomb).Refusal);
    }

    // US-2 · "Caps": the animation caps, frames and total pixels.
    [Theory]
    [InlineData(1_000, 100, 100, true)]
    [InlineData(1_001, 10, 10, false)]
    [InlineData(100, 3_000, 3_000, false)]
    [InlineData(66, 3_000, 3_000, true)]
    public void Read_GifFramesAgainstTheCaps(int frames, int width, int height, bool accepted)
    {
        var result = Read(TestImages.GifOfFrames(width, height, frames, width, height));

        Assert.Equal(accepted, result.Ok);
        if (!accepted)
        {
            Assert.Equal(ImageRefusal.Animation, result.Refusal);
        }
    }

    // US-2 · "Caps": the canvas of an animation counts against the still caps too.
    [Fact]
    public void Read_GifWithOversizedCanvas_IsDimensions()
    {
        Assert.Equal(ImageRefusal.Dimensions, Read(TestImages.GifOfFrames(20_001, 10, 2, 10, 10)).Refusal);
    }

    // US-2 · "Animated keeps animation": a GIF with several frames is animated, one frame is not.
    [Fact]
    public void Read_Gif_FramesDecideAnimation()
    {
        var animated = Read(TestImages.AnimatedGif(5)).Header!;
        var still = Read(TestImages.GifOfFrames(8, 8, 1, 8, 8)).Header!;

        Assert.True(animated.IsAnimated);
        Assert.Equal(5, animated.FrameCount);
        Assert.False(still.IsAnimated);
    }

    // US-2 · "Animated keeps animation" (ADR-0045 D-2): an APNG is treated like an animated GIF.
    [Fact]
    public void Read_Apng_IsAnimated()
    {
        var header = Read(TestImages.Apng(4)).Header!;

        Assert.True(header.IsAnimated);
        Assert.Equal(4, header.FrameCount);
    }

    // US-2 · "Animated keeps animation": an animated WebP.
    [Fact]
    public void Read_AnimatedWebp_IsAnimated()
    {
        var list = Enumerable.Range(0, 3)
            .Select(i => NetVips.Image.Black(8, 8, bands: 3) + new double[] { i * 80, 10, 10 })
            .ToArray();
        var joined = NetVips.Image.Arrayjoin(list, across: 1).Cast(NetVips.Enums.BandFormat.Uchar)
            .Mutate(mutable => mutable.Set(NetVips.GValue.GIntType, "page-height", 8));
        var webp = joined.WebpsaveBuffer(q: 80);

        var header = Read(webp).Header!;

        Assert.True(header.IsAnimated);
        Assert.Equal(3, header.FrameCount);
    }

    // US-2 · "Metadata removed, orientation kept": the orientation is read from Exif (JPEG) and eXIf (PNG).
    [Theory]
    [InlineData(6)]
    [InlineData(3)]
    [InlineData(8)]
    public void Read_JpegExifOrientation(int orientation)
    {
        var jpeg = TestImages.JpegWithCameraData(TestImages.Jpeg(60, 40), orientation);

        var header = Read(jpeg).Header!;

        Assert.Equal(orientation, header.Orientation);
        Assert.Equal(60, header.Width);
    }

    [Fact]
    public void Read_JpegOrientationSixSwapsTheDisplaySides()
    {
        var header = Read(TestImages.JpegWithCameraData(TestImages.Jpeg(60, 40), 6)).Header!;

        Assert.Equal(40, header.DisplayWidth);
        Assert.Equal(60, header.DisplayHeight);
    }

    [Fact]
    public void Read_PngEXifOrientation()
    {
        var png = TestImages.PngWithMetadataAndTail(TestImages.Png(30, 20), []);

        Assert.Equal(6, Read(png).Header!.Orientation);
    }

    // US-2 · "Hostile files": bytes after the container end are located, never read as part of the picture.
    [Fact]
    public void Read_Polyglot_ReportsWhereTheContainerEnds()
    {
        var jpeg = TestImages.Jpeg(30, 20);
        var polyglot = TestImages.JpegWithCameraData(jpeg, 1, tail: Encoding.ASCII.GetBytes("<script>alert(1)</script>"));

        var header = Read(polyglot).Header!;

        Assert.Equal(polyglot.Length - 25, header.ContainerEnd);
    }

    // US-2 · "Hostile files": a lying length never reads past the buffer.
    [Fact]
    public void Read_ChunkLengthLargerThanTheFile_IsFormat()
    {
        var png = TestImages.Png(30, 20);
        var damaged = (byte[])png.Clone();
        damaged[33] = 0x7F;

        Assert.Equal(ImageRefusal.Format, Read(damaged).Refusal);
    }

    [Fact]
    public void Read_JpegWithoutEoi_IsFormat()
    {
        var jpeg = TestImages.Jpeg(30, 20);

        Assert.Equal(ImageRefusal.Format, Read(jpeg[..^2]).Refusal);
    }

    [Fact]
    public void Read_RiffSizeLargerThanTheBuffer_IsFormat()
    {
        var webp = TestImages.Webp(30, 20);
        var damaged = (byte[])webp.Clone();
        damaged[7] = 0x7F;

        Assert.Equal(ImageRefusal.Format, Read(damaged).Refusal);
    }

    // US-2 · "Caps": a lossless WebP is told from a lossy one (the quality search skips it).
    [Fact]
    public void Read_Webp_LosslessIsTold()
    {
        var lossless = TestImages.Photo(40, 30).WebpsaveBuffer(lossless: true);

        Assert.True(Read(lossless).Header!.IsLossless);
        Assert.False(Read(TestImages.Webp(40, 30)).Header!.IsLossless);
    }

    // feat-078 review F-1 · a frame outside its canvas is refused, as a format refusal, before any decoder grows the canvas to it.
    public static TheoryData<string, byte[]> FramesOutsideTheCanvas() => new()
    {
        { "gif 1x1 screen, 16000x16000 frame", TestImages.GifOfFrames(1, 1, 1, 16000, 16000) },
        { "gif 1x1 screen, 8000x8000 frame", TestImages.GifOfFrames(1, 1, 1, 8000, 8000) },
        { "gif frame past the right edge", TestImages.GifOfFrames(100, 100, 2, 60, 60, frameX: 50) },
        { "gif frame past the bottom edge", TestImages.GifOfFrames(100, 100, 2, 60, 60, frameY: 50) },
        { "gif empty frame", TestImages.GifOfFrames(100, 100, 2, 0, 10) },
        { "webp 1x1 canvas, 16000x16000 frame", TestImages.WebpOfFrames(1, 1, 2, 16000, 16000) },
        { "webp frame offset past the edge", TestImages.WebpOfFrames(100, 100, 2, 60, 60, frameX: 50) },
        { "apng 8x8 canvas, 16000x16000 frame", TestImages.Apng(2, 8, frameWidth: 16000, frameHeight: 16000) },
        { "apng frame offset past the edge", TestImages.Apng(2, 8, frameWidth: 6, frameHeight: 6, frameX: 4) },
    };

    [Theory]
    [MemberData(nameof(FramesOutsideTheCanvas))]
    public void Read_FrameOutsideTheCanvas_IsFormat(string name, byte[] data)
    {
        var result = Read(data);

        Assert.False(result.Ok, name);
        Assert.Equal(ImageRefusal.Format, result.Refusal);
    }

    [Fact]
    public void Read_FramesInsideTheCanvas_StillRead()
    {
        Assert.True(Read(TestImages.GifOfFrames(100, 100, 2, 50, 50, frameX: 50, frameY: 50)).Ok);
        Assert.True(Read(TestImages.WebpOfFrames(100, 100, 2, 50, 50, frameX: 50, frameY: 50)).Ok);
        Assert.True(Read(TestImages.Apng(2, 8, frameWidth: 4, frameHeight: 4, frameX: 4, frameY: 4)).Ok);
    }
}
