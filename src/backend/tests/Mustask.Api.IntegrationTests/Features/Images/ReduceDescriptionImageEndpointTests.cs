using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Images;

namespace Mustask.Api.IntegrationTests.Features.Images;

/// <summary>
/// <c>POST …/workitems/{identifier}/images/reductions</c> (feat-078 api-contract § 2, US-6): the reduction's answer, what it
/// stores, the lines the server holds. The picture's own checks (similarity, floors, lossless formats) are the intake's unit tests.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ReduceDescriptionImageEndpointTests(MustaskApiFactory factory)
{
    // US-6 · "A big photograph is reduced": 201, the stored result within 5 MB with its facts, the original's facts, one row.
    [Fact]
    public async Task Reduce_BigJpeg_201_ResultAndOriginalFacts_OneRowStored()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var source = ImagesTestData.Jpeg(4000, 3000, quality: 100, sigma: 60);
        Assert.True(source.Length > ImageLimits.MaxStoredBytes);

        var answer = await ImagesTestData.ReduceAsync(factory, world.Items, item.Identifier, source);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        var stored = answer.Body.GetProperty("image");
        var original = answer.Body.GetProperty("original");
        Assert.Equal("jpeg", stored.GetProperty("format").GetString());
        Assert.InRange(stored.GetProperty("byteLength").GetInt32(), 1, ImageLimits.MaxStoredBytes);
        Assert.True(stored.GetProperty("width").GetInt32() >= 2000);
        Assert.Equal(4000, original.GetProperty("width").GetInt32());
        Assert.Equal(3000, original.GetProperty("height").GetInt32());
        Assert.Equal(source.Length, original.GetProperty("byteLength").GetInt32());
        Assert.False(original.TryGetProperty("id", out _));
        Assert.Equal(1, await ImagesTestData.CountForAsync(factory, item.Id));

        var row = await ImagesTestData.FindAsync(factory, stored.GetProperty("id").GetGuid());
        Assert.Null(row!.BoundAt);
    }

    // api-contract § 2 · "A body of 5 MB or less is processed as an ordinary upload and answered in this shape".
    [Fact]
    public async Task Reduce_SmallPng_201_ProcessedAsAnOrdinaryUpload()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);

        var answer = await ImagesTestData.ReduceAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(120, 80));

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(120, answer.Body.GetProperty("image").GetProperty("width").GetInt32());
        Assert.Equal(120, answer.Body.GetProperty("original").GetProperty("width").GetInt32());
    }

    private sealed class CannotReduceIntake : Mustask.Application.Abstractions.Images.IImageIntake
    {
        public Task<Mustask.Application.Abstractions.Images.ImageIntakeResult> AcceptAsync(
            ReadOnlyMemory<byte> upload, Mustask.Application.Abstractions.Images.ImageIntakeMode mode, CancellationToken cancellationToken) =>
            Task.FromResult(Mustask.Application.Abstractions.Images.ImageIntakeResult.Refused(Mustask.Application.Abstractions.Images.ImageIntakeOutcome.CannotReduce));
    }

    // US-6 · "Cannot reach 5 MB within the floors": 400, nothing stored. Within the 20 MB intake no ordinary file is unreducible
    // (half the sides is a quarter of the bytes), so the outcome is forced through a stub intake; the real ladder's refusal is the
    // unit test ImageIntakeEdgeTests.Reduction_NoisePng_CannotReduce.
    [Fact]
    public async Task Reduce_IntakeCannotReduce_400CannotReduce_NothingStored()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        using var stubbed = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<Mustask.Application.Abstractions.Images.IImageIntake, CannotReduceIntake>()));

        using var request = new HttpRequestMessage(HttpMethod.Post, ImagesTestData.ReductionPath(world.Items, item.Identifier)) { Content = new ByteArrayContent(ImagesTestData.Png(40, 30)) };
        request.WithBearer(OrganizationsTokens.SignedIn());
        using var response = await stubbed.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("images.reduction.cannot_reduce", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, await ImagesTestData.CountForAsync(factory, item.Id));
    }

    // US-6 · "The server holds the line": an ordinary upload over 5 MB, a reduction over 20 MB, an animation over 5 MB.
    [Fact]
    public async Task Upload_6MbOrdinary_400TooLarge()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);

        var answer = await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.PaddedTo(ImagesTestData.Png(40, 30), 6 * 1024 * 1024));

        Assert.Equal("images.upload.too_large", answer.ErrorCode);
    }

    [Fact]
    public async Task Reduce_20971521Bytes_400ReductionTooLarge_NothingStored()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);

        var answer = await ImagesTestData.ReduceAsync(factory, world.Items, item.Identifier, ImagesTestData.PaddedTo(ImagesTestData.Png(40, 30), ImageLimits.MaxReductionInputBytes + 1));

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("images.reduction.too_large", answer.ErrorCode);
        Assert.Equal(0, await ImagesTestData.CountForAsync(factory, item.Id));
    }

    [Fact]
    public async Task Reduce_AnimatedOver5Mb_400Animated_NothingStored()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);

        var answer = await ImagesTestData.ReduceAsync(factory, world.Items, item.Identifier, ImagesTestData.PaddedTo(ImagesTestData.AnimatedGif(3), ImageLimits.MaxStoredBytes + 1));

        Assert.Equal("images.reduction.animated", answer.ErrorCode);
        Assert.Equal(0, await ImagesTestData.CountForAsync(factory, item.Id));
    }

    // feat-078 review F-1 · a GIF whose frame is bigger than its screen is refused from the header, before any decode, on the reduction route.
    [Fact]
    public async Task Reduce_GifFrameBiggerThanItsScreen_400FormatUnsupported_NothingStored()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var bomb = ImagesTestData.PaddedTo(ImagesTestData.GifWithBigFrame(screenSize: 1, frameSize: 16000), ImageLimits.MaxStoredBytes + 1);

        var answer = await ImagesTestData.ReduceAsync(factory, world.Items, item.Identifier, bomb);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("images.upload.format_unsupported", answer.ErrorCode);
        Assert.Equal(0, await ImagesTestData.CountForAsync(factory, item.Id));
    }
}
