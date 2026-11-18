using System.Text;
using Mustask.Application.Abstractions.Images;
using Mustask.Domain.Images;
using Mustask.Infrastructure.Images;
using Mustask.Infrastructure.Images.Intake;
using Mustask.Infrastructure.Images.Intake.Headers;
using Mustask.Infrastructure.Images.Intake.Processing;

namespace Mustask.Infrastructure.Tests.Images;

/// <summary>The step-4 tests plan-backend § 13 owes: the loaders' set, the spy codec, the hostile corpus, GIF palettes, reductions.</summary>
public sealed class ImageIntakeEdgeTests : IDisposable
{
    private readonly DecodeGate _gate = new(new ImagesOptions().Intake);
    private readonly ImageIntake _intake;

    public ImageIntakeEdgeTests() => _intake = new ImageIntake(new VipsCodec(new ImagesOptions()), _gate);

    public void Dispose() => _gate.Dispose();

    private Task<ImageIntakeResult> Accept(byte[] data, ImageIntakeMode mode = ImageIntakeMode.Upload) =>
        _intake.AcceptAsync(data, mode, CancellationToken.None);

    // feat-078 review F-1 · defence in depth: a decoder that produces another size than the header said is refused before the pixels are copied.
    [Fact]
    public void Decode_SizeDifferentFromTheHeader_IsRefused()
    {
        var codec = new VipsCodec(new ImagesOptions());
        var png = TestImages.Png(40, 30);
        ImageHeaderReader.TryRead(png, out var header, out _);

        Assert.Throws<ImageDecodeException>(() => codec.Decode(png, header with { Width = 1, Height = 1 }));
        codec.Decode(png, header).Dispose();
    }

    // feat-078 review F-6 · the GIF loader sizes the canvas from the frames when the screen is over 2048 px: that GIF still decodes,
    // and one that loads bigger than its header is refused.
    [Fact]
    public void Decode_GifScreenLargerThanItsFrames_StillDecodes()
    {
        var codec = new VipsCodec(new ImagesOptions());
        var gif = TestImages.Photo(1000, 1000).GifsaveBuffer(dither: 0);
        gif[6] = 3000 & 0xFF;
        gif[7] = 3000 >> 8;
        gif[8] = 2000 & 0xFF;
        gif[9] = 2000 >> 8;
        Assert.True(ImageHeaderReader.TryRead(gif, out var header, out _));
        Assert.Equal(3000, header.Width);

        using var picture = codec.Decode(gif, header);

        Assert.True(picture.Width <= 3000 && picture.Height <= 2000);
        Assert.True(picture.Width >= 1);
    }

    [Fact]
    public void Decode_GifLoadedBiggerThanItsHeader_IsRefused()
    {
        var codec = new VipsCodec(new ImagesOptions());
        var gif = TestImages.Photo(40, 30).GifsaveBuffer(dither: 0);
        ImageHeaderReader.TryRead(gif, out var header, out _);

        Assert.Throws<ImageDecodeException>(() => codec.Decode(gif, header with { Width = 10, Height = 10 }));
    }

    // ADR-0045 decision 2 · only the four loaders are on: a GIF decodes, an SVG is refused by libvips itself.
    [Fact]
    public void VipsSetup_LoadersBlocked_GifDecodes_SvgRefused()
    {
        VipsSetup.EnsureInitialized(2);

        using var gif = NetVips.Image.GifloadBuffer(TestImages.AnimatedGif(2));
        Assert.Equal(8, gif.Width);

        var svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\"/></svg>");
        Assert.ThrowsAny<Exception>(() => NetVips.Image.NewFromBuffer(svg).Dispose());
    }

    private sealed class SpyCodec : IImageCodec
    {
        public int Decodes { get; private set; }

        public IPicture Decode(byte[] stripped, ImageHeader header)
        {
            Decodes++;
            throw new InvalidOperationException("The codec must not be reached.");
        }

        public byte[] DecodeLuma(byte[] encoded, ImageFormat format, out int width, out int height)
        {
            Decodes++;
            throw new InvalidOperationException("The codec must not be reached.");
        }
    }

    // US-2 · "Caps": an over-cap header never reaches the codec.
    [Theory]
    [InlineData(20001u, 100u)]
    [InlineData(8000u, 8000u)]
    [InlineData(100u, 20001u)]
    public async Task Accept_OverCapHeader_CodecNeverCalled(uint width, uint height)
    {
        var spy = new SpyCodec();
        var intake = new ImageIntake(spy, _gate);

        var result = await intake.AcceptAsync(TestImages.PngHeaderOnly(width, height), ImageIntakeMode.Upload, CancellationToken.None);

        Assert.Equal(ImageIntakeOutcome.Dimensions, result.Outcome);
        Assert.Equal(0, spy.Decodes);
    }

    // US-2 · "Hostile files": each is refused, or stored with no metadata and no tail.
    [Fact]
    public async Task HostileCorpus_RefusedOrStoredClean()
    {
        var png = TestImages.Png(40, 30);
        var jpeg = TestImages.Jpeg(60, 40);
        var tail = Encoding.ASCII.GetBytes("<script>alert(1)</script>PK");
        var corpus = new List<byte[]>
        {
            TestImages.PngWithMetadataAndTail(png, tail),
            TestImages.JpegWithCameraData(jpeg, 6, tail),
            png[..(png.Length / 2)],
            jpeg[..(jpeg.Length / 2)],
            Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"),
            Encoding.UTF8.GetBytes("<html><body>x</body></html>"),
            new byte[] { 0x50, 0x4B, 0x03, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            TestImages.PngHeaderOnly(60000, 60000),
            TestImages.GifOfFrames(100, 100, 1001, 10, 10),
            Array.Empty<byte>(),
        };

        foreach (var file in corpus)
        {
            var result = await Accept(file);

            if (result.Outcome != ImageIntakeOutcome.Accepted)
            {
                Assert.Null(result.Content);
                continue;
            }

            Assert.True(ImageHeaderReader.TryRead(result.Content!, out var header, out _));
            Assert.Equal(result.Content!.Length, header.ContainerEnd);
            Assert.DoesNotContain("<script>", Encoding.Latin1.GetString(result.Content));
        }
    }

    // ADR-0045 D-3 · a nearest-neighbour scaled GIF keeps only colours it had (more than a handful).
    [Fact]
    public void Gif_ManyColours_ScaledNearest_PaletteStaysExact()
    {
        const int size = 128;
        var palette = Enumerable.Range(0, 200).Select(i => (R: (byte)(i * 7 % 256), G: (byte)(i * 13 % 256), B: (byte)(i * 29 % 256))).Distinct().ToArray();
        var pixels = new byte[size * size * 3];

        for (var i = 0; i < size * size; i++)
        {
            var (r, g, b) = palette[((i / 3) + (i / size)) % palette.Length];
            pixels[i * 3] = r;
            pixels[(i * 3) + 1] = g;
            pixels[(i * 3) + 2] = b;
        }

        var source = NetVips.Image.NewFromMemory(pixels, size, size, 3, NetVips.Enums.BandFormat.Uchar).GifsaveBuffer(dither: 0, effort: 1);
        Assert.True(ImageHeaderReader.TryRead(source, out var header, out _));

        var codec = new VipsCodec(new ImagesOptions());
        using var picture = codec.Decode(source, header);
        using var half = picture.Resize(size / 2, size / 2, ImageFormat.Gif);
        var scaled = half.Encode(new PictureEncoding(ImageFormat.Gif, 100, Lossless: true));

        var original = Colours(source);
        var result = Colours(scaled);

        Assert.True(original.Count > 100);
        Assert.True(result.Count > 50);
        Assert.True(result.IsSubsetOf(original));
    }

    private static HashSet<(byte, byte, byte)> Colours(byte[] gif)
    {
        using var image = NetVips.Image.GifloadBuffer(gif);
        var bands = image.Bands;
        var bytes = image.WriteToMemory<byte>();
        var colours = new HashSet<(byte, byte, byte)>();

        for (var i = 0; i + bands <= bytes.Length; i += bands)
        {
            colours.Add((bytes[i], bytes[i + 1], bytes[i + 2]));
        }

        return colours;
    }

    // US-6 · "No offer for animations": an animated file over 5 MB is refused as animated, not reduced.
    [Fact]
    public async Task Reduction_AnimatedOver5Mb_IsRefusedAsAnimated()
    {
        var padded = TestImages.AnimatedGif(4).Concat(new byte[ImageLimits.MaxStoredBytes + 1]).ToArray();

        var result = await Accept(padded, ImageIntakeMode.Reduction);

        Assert.Equal(ImageIntakeOutcome.AnimatedReduction, result.Outcome);
        Assert.Null(result.Content);
    }

    // US-6 · "Cannot reach 5 MB within the floors": nothing is stored.
    [Fact]
    public async Task Reduction_NoisePng_CannotReduce()
    {
        var result = await Accept(TestImages.Png(3600, 2700, sigma: 90), ImageIntakeMode.Reduction);

        Assert.Equal(ImageIntakeOutcome.CannotReduce, result.Outcome);
        Assert.Null(result.Content);
    }

    // US-6 · "Screenshot lossless": a PNG is only scaled, stays a PNG, never below half.
    [Fact]
    public async Task Reduction_LargePng_StaysPng_Scaled_NotBelowHalf()
    {
        var source = TestImages.Png(3000, 2000, sigma: 20);
        Assert.True(source.Length > ImageLimits.MaxStoredBytes);

        var result = await Accept(source, ImageIntakeMode.Reduction);

        Assert.Equal(ImageIntakeOutcome.Accepted, result.Outcome);
        Assert.Equal(ImageFormat.Png, result.Stored!.Format);
        Assert.True(result.Content!.Length <= ImageLimits.MaxStoredBytes);
        Assert.True(result.Stored.Width >= 1500 && result.Stored.Width < 3000);
        Assert.Equal(3000, result.Original!.Width);
    }
}
