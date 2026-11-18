using Mustask.Application.Abstractions.Images;
using Mustask.Domain.Images;
using Mustask.Infrastructure.Images;
using Mustask.Infrastructure.Images.Intake;
using Mustask.Infrastructure.Images.Intake.Headers;
using Mustask.Infrastructure.Images.Intake.Processing;

namespace Mustask.Infrastructure.Tests.Images;

public sealed class ImageIntakeTests : IDisposable
{
    private readonly DecodeGate _gate = new(new ImagesOptions().Intake);
    private readonly ImageIntake _intake;

    public ImageIntakeTests() => _intake = new ImageIntake(new VipsCodec(new ImagesOptions()), _gate);

    public void Dispose() => _gate.Dispose();

    private Task<ImageIntakeResult> Upload(byte[] data) => _intake.AcceptAsync(data, ImageIntakeMode.Upload, CancellationToken.None);

    // US-2 · "Small image stored as is": nothing to decode, the gate is not entered.
    [Fact]
    public async Task Upload_SmallJpeg_IsStoredStripped()
    {
        var source = TestImages.Jpeg(300, 200);

        var result = await Upload(source);

        Assert.Equal(ImageIntakeOutcome.Accepted, result.Outcome);
        Assert.Equal(300, result.Stored!.Width);
        Assert.Equal(200, result.Stored.Height);
        Assert.Equal(source.Length, result.Original!.ByteLength);
    }

    // US-2 · "Animated GIF is stored as it is".
    [Fact]
    public async Task Upload_AnimatedGif_IsStoredWithItsFrames()
    {
        var result = await Upload(TestImages.AnimatedGif(3));

        Assert.Equal(ImageIntakeOutcome.Accepted, result.Outcome);
        Assert.Equal(3, result.Stored!.FrameCount);
    }

    // US-2 · "Not an image is refused".
    [Fact]
    public async Task Upload_Garbage_IsAFormatRefusal()
    {
        var result = await Upload([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13]);

        Assert.Equal(ImageIntakeOutcome.Format, result.Outcome);
    }

    // US-2 · "A big photograph is re-encoded under the cap without visible loss".
    [Fact]
    public async Task Upload_LargeNoisyJpeg_IsReencodedSmaller()
    {
        var source = TestImages.Jpeg(2400, 1600, quality: 98, sigma: 40);
        Assert.True(source.Length > ImageLimits.ReencodeAboveBytes);

        var result = await Upload(source);

        Assert.Equal(ImageIntakeOutcome.Accepted, result.Outcome);
        Assert.True(result.Content!.Length <= ImageLimits.MaxStoredBytes);
        Assert.True(result.Content.Length < source.Length);
        Assert.Equal(2400, result.Stored!.Width);
        Assert.True(ImageHeaderReader.TryRead(result.Content, out _, out _));
    }

    // US-2 · "A huge picture is scaled to 16 777 216 pixels".
    [Fact]
    public async Task Upload_PngOverThePixelCap_IsScaledDown()
    {
        var source = TestImages.Screenshot(5000, 4000);

        var result = await Upload(source);

        Assert.Equal(ImageIntakeOutcome.Accepted, result.Outcome);
        Assert.True((long)result.Stored!.Width * result.Stored.Height <= ImageLimits.MaxPixelsAfterScaling);
        Assert.Equal(5000, result.Original!.Width);
    }

    // US-6 · "A reduction of an over-cap photograph passes every check".
    [Fact]
    public async Task Reduction_OfALargePhoto_FitsTheCap()
    {
        var source = TestImages.Jpeg(4000, 3000, quality: 100, sigma: 60);

        var result = await _intake.AcceptAsync(source, ImageIntakeMode.Reduction, CancellationToken.None);

        Assert.Equal(ImageIntakeOutcome.Accepted, result.Outcome);
        Assert.True(result.Content!.Length <= ImageLimits.MaxStoredBytes);
        Assert.True(result.Stored!.Width >= 2000 && result.Stored.Height >= 1500);
    }

    [Fact]
    public async Task Gate_RefusesWhenTheQueueIsFull()
    {
        var options = new ImagesOptions.IntakeOptions { MaxConcurrentDecodes = 1, QueueLength = 0, MaxWaitSeconds = 1 };
        using var gate = new DecodeGate(options);

        using var first = await gate.EnterAsync(CancellationToken.None);
        var second = await gate.EnterAsync(CancellationToken.None);

        Assert.NotNull(first);
        Assert.Null(second);
    }
}
