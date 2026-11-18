using System.Text;
using Mustask.Application.Abstractions.Images;
using Mustask.Domain.Images;
using Mustask.Infrastructure.Images;
using Mustask.Infrastructure.Images.Intake;
using Mustask.Infrastructure.Images.Intake.Processing;
using NetVips;

namespace Mustask.Infrastructure.Tests.Images;

/// <summary>
/// feat-078 backend QA (qa.md, Backend): the processing outcomes US-2 and US-6 say the test measures — similarity, rotated
/// pixels, exact screenshots, never larger, frame timing — which the developer's intake tests asserted only by size or count.
/// </summary>
public sealed class ImageIntakeQaTests : IDisposable
{
    private readonly DecodeGate _gate = new(new ImagesOptions().Intake);
    private readonly ImageIntake _intake;

    public ImageIntakeQaTests() => _intake = new ImageIntake(new VipsCodec(new ImagesOptions()), _gate);

    public void Dispose() => _gate.Dispose();

    private Task<ImageIntakeResult> Accept(byte[] data, ImageIntakeMode mode = ImageIntakeMode.Upload) =>
        _intake.AcceptAsync(data, mode, CancellationToken.None);

    /// <summary>The luminance plane of a picture (autorotated), as BR-IM-4 (4) measures it.</summary>
    private static (byte[] Plane, int Width, int Height) Luma(byte[] data)
    {
        using var image = NetVips.Image.NewFromBuffer(data).Autorot().Colourspace(Enums.Interpretation.Bw).ExtractBand(0).Cast(Enums.BandFormat.Uchar);

        return (image.WriteToMemory<byte>(), image.Width, image.Height);
    }

    private static double Similarity(byte[] source, byte[] stored)
    {
        var a = Luma(source);
        var b = Luma(stored);
        Assert.Equal((a.Width, a.Height), (b.Width, b.Height));

        return Ssim.Mean(a.Plane, b.Plane, a.Width, a.Height);
    }

    // US-2 · "A large photo is compressed without visible loss": same size, smaller, SSIM ≥ 0.98 measured by the test.
    [Fact]
    public async Task Upload_LargeJpeg_Reencoded_SimilarityAtLeast098()
    {
        var source = TestImages.Jpeg(2400, 1600, quality: 98, sigma: 40);
        Assert.True(source.Length > ImageLimits.ReencodeAboveBytes);

        var result = await Accept(source);

        Assert.Equal(ImageIntakeOutcome.Accepted, result.Outcome);
        Assert.True(result.Content!.Length < source.Length);
        Assert.True(Similarity(source, result.Content!.ToArray()) >= ImageLimits.MinSimilarity);
    }

    // US-2 · "Metadata is removed and orientation kept", the 3 MB row: re-encoded with rotated pixels, no flag, no camera data.
    [Fact]
    public async Task Upload_LargeRotatedJpeg_PixelsRotated_NoFlag_NoMetadata()
    {
        var plain = TestImages.Jpeg(2400, 1600, quality: 98, sigma: 40);
        var source = TestImages.JpegWithCameraData(plain, orientation: 6);
        Assert.True(source.Length > ImageLimits.ReencodeAboveBytes);

        var result = await Accept(source);

        Assert.Equal(ImageIntakeOutcome.Accepted, result.Outcome);
        Assert.Equal((1600, 2400), (result.Stored!.Width, result.Stored.Height));
        var stored = result.Content!.ToArray();
        using var decoded = NetVips.Image.NewFromBuffer(stored);
        Assert.Equal((1600, 2400), (decoded.Width, decoded.Height));
        Assert.True(!decoded.Contains("orientation") || (int)decoded.Get("orientation") == 1);
        var text = Encoding.Latin1.GetString(stored);
        Assert.DoesNotContain("Alice", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
        Assert.DoesNotContain("8BIM", text, StringComparison.Ordinal);
    }

    // US-2 · "A screenshot stays exact": the stored PNG decodes to exactly the source's pixels, and is not larger.
    [Fact]
    public async Task Upload_Screenshot_ExactPixels_NotLarger()
    {
        var source = TestImages.Screenshot(2560, 1440);

        var result = await Accept(source);

        Assert.Equal(ImageIntakeOutcome.Accepted, result.Outcome);
        Assert.True(result.Content!.Length <= source.Length);
        using var a = NetVips.Image.NewFromBuffer(source);
        using var b = NetVips.Image.NewFromBuffer(result.Content!.ToArray());
        Assert.Equal(a.WriteToMemory<byte>(), b.WriteToMemory<byte>());
    }

    // US-2 · "Compression never makes it bigger" (BR-IM-4 (5)), over pictures where a re-encoding at ≥ 0.98 could come out larger:
    // an already low-quality JPEG over 1 MB, and photographic PNG/WebP.
    [Theory]
    [InlineData("jpeg-low-quality")]
    [InlineData("jpeg-high-quality")]
    [InlineData("webp")]
    public async Task Upload_NeverLargerThanTheSource(string kind)
    {
        var source = kind switch
        {
            "jpeg-low-quality" => TestImages.Jpeg(3000, 2400, quality: 55, sigma: 50),
            "jpeg-high-quality" => TestImages.Jpeg(2000, 1500, quality: 100, sigma: 25),
            _ => TestImages.Webp(2400, 1600, quality: 90),
        };

        var result = await Accept(source);

        Assert.Equal(ImageIntakeOutcome.Accepted, result.Outcome);
        Assert.True(result.Content!.Length <= source.Length, $"{result.Content.Length} > {source.Length}");
    }

    // US-2 · "An animated image keeps its animation": every frame with the same timing.
    [Fact]
    public async Task Upload_AnimatedGif_KeepsFramesAndTiming()
    {
        var source = TestImages.AnimatedGif(40);

        var result = await Accept(source);

        Assert.Equal(ImageIntakeOutcome.Accepted, result.Outcome);
        using var before = NetVips.Image.NewFromBuffer(source, "n=-1");
        using var after = NetVips.Image.NewFromBuffer(result.Content!.ToArray(), "n=-1");
        Assert.Equal(40, result.Stored!.FrameCount);
        Assert.Equal((int[])before.Get("delay"), (int[])after.Get("delay"));
        Assert.Equal(before.Height, after.Height);
    }

    // US-6 · "A lossy photo loses quality before it loses size", BR-IM-20 (2): when the result keeps the dimensions its
    // similarity is at least 0.95; when it was scaled it is not below half; either way within 5 MB and still a JPEG.
    [Fact]
    public async Task Reduction_Jpeg_QualityFloor095_SideFloorHalf()
    {
        var source = TestImages.Jpeg(4000, 3000, quality: 100, sigma: 45);
        Assert.True(source.Length > ImageLimits.MaxStoredBytes);

        var result = await Accept(source, ImageIntakeMode.Reduction);

        Assert.Equal(ImageIntakeOutcome.Accepted, result.Outcome);
        Assert.Equal(ImageFormat.Jpeg, result.Stored!.Format);
        Assert.InRange(result.Content!.Length, 1, ImageLimits.MaxStoredBytes);
        Assert.True(result.Stored.Width >= 2000 && result.Stored.Height >= 1500);

        if (result.Stored.Width == 4000)
        {
            Assert.True(Similarity(source, result.Content!.ToArray()) >= ImageLimits.MinReductionSimilarity);
        }
    }
}
