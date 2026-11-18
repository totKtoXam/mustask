using System.Diagnostics;
using System.Net;
using Mustask.Domain.Images;
using Xunit.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.Images;

/// <summary>
/// The spec's speed scenarios end to end (US-2 "Speed", US-6 "Speed"): a 5 MB / 12 MP JPEG stored, a 20 MB / 48 MP one reduced.
/// The spec's targets (5 s, 15 s) are measured on the stand, not here (feat-078 qa.md D-1): a shared CI runner moved these
/// timings past twice the target on the same code. These tests log the time and only guard against a hang or a pathological
/// regression with a ceiling far above the target.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ImageVolumeTests(MustaskApiFactory factory, ITestOutputHelper output)
{
    // Not the spec's targets: a hang guard. The targets are 5 s and 15 s; a dev machine measured 4.9-9.5 s and 9.1-18 s,
    // and a CI runner more than 10 s and 30 s.
    private const int UploadCeilingSeconds = 60;
    private const int ReductionCeilingSeconds = 120;

    [Fact]
    public async Task Upload_5MbJpeg12Mp_Stored_TimeLoggedUnderHangCeiling()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var source = ImagesTestData.Jpeg(4000, 3000, quality: 93, sigma: 14);
        Assert.InRange(source.Length, 3_500_000, ImageLimits.MaxStoredBytes);

        // A first request warms the libvips pipeline (the first call loads native code); the scenario is the steady state.
        await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Jpeg(800, 600));
        var watch = Stopwatch.StartNew();
        var answer = await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, source);
        watch.Stop();

        output.WriteLine($"upload {source.Length} bytes 12 MP: {watch.Elapsed.TotalSeconds:F2} s (spec target 5 s, on the stand)");
        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(UploadCeilingSeconds), $"{watch.Elapsed.TotalSeconds:F2} s");
    }

    [Fact]
    public async Task Reduce_20MbJpeg48Mp_Reduced_TimeLoggedUnderHangCeiling()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var source = ImagesTestData.Jpeg(8000, 6000, quality: 90, sigma: 20);
        Assert.InRange(source.Length, ImageLimits.MaxStoredBytes + 1, ImageLimits.MaxReductionInputBytes);

        var watch = Stopwatch.StartNew();
        var answer = await ImagesTestData.ReduceAsync(factory, world.Items, item.Identifier, source);
        watch.Stop();

        output.WriteLine($"reduction {source.Length} bytes 48 MP: {watch.Elapsed.TotalSeconds:F2} s (spec target 15 s, on the stand)");
        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(ReductionCeilingSeconds), $"{watch.Elapsed.TotalSeconds:F2} s");
    }
}
