using System.Buffers.Binary;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Images;
using Mustask.Infrastructure;

namespace Mustask.Api.IntegrationTests.Features.Images;

/// <summary>An answer to an image request: status, the headers that matter, the bytes and the JSON body when there is one.</summary>
internal sealed record ImageAnswer(HttpStatusCode Status, System.Net.Http.Headers.HttpResponseHeaders Headers, System.Net.Http.Headers.HttpContentHeaders ContentHeaders, byte[] Bytes)
{
    public JsonElement Body => Bytes.Length > 0 && ContentHeaders.ContentType?.MediaType is "application/json" or "application/problem+json"
        ? JsonDocument.Parse(Bytes).RootElement.Clone()
        : default;

    public string? ErrorCode => Body.ValueKind == JsonValueKind.Object && Body.TryGetProperty("errorCode", out var code) ? code.GetString() : null;

    public string BodyWithoutTrace()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(Bytes)!.AsObject();
        node.Remove("traceId");
        node.Remove("instance");

        return node.ToJsonString();
    }
}

/// <summary>Requests and fixtures of the image tests (feat-078). The pictures are generated: the repository carries no megabytes.</summary>
internal static class ImagesTestData
{
    public static string UploadPath(WorkItemsWorld world, string identifier) => $"{WorkItemsTestData.Item(world, identifier)}/images";

    public static string ReductionPath(WorkItemsWorld world, string identifier) => $"{UploadPath(world, identifier)}/reductions";

    public static async Task<ImageAnswer> SendAsync(
        MustaskApiFactory factory, HttpMethod method, string path, byte[]? body = null, string? token = null, string? contentType = null, string? ifNoneMatch = null)
    {
        using var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType ?? "application/octet-stream");
        }

        if (ifNoneMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        }

        if (token is not null)
        {
            request.WithBearer(token);
        }

        using var response = await factory.CreateClient().SendAsync(request);

        return new ImageAnswer(response.StatusCode, response.Headers, response.Content.Headers, await response.Content.ReadAsByteArrayAsync());
    }

    public static Task<ImageAnswer> UploadAsync(MustaskApiFactory factory, WorkItemsWorld world, string identifier, byte[] bytes, string? token = null, string? contentType = null) =>
        SendAsync(factory, HttpMethod.Post, UploadPath(world, identifier), bytes, token ?? OrganizationsTokens.SignedIn(), contentType);

    public static Task<ImageAnswer> ReduceAsync(MustaskApiFactory factory, WorkItemsWorld world, string identifier, byte[] bytes, string? token = null) =>
        SendAsync(factory, HttpMethod.Post, ReductionPath(world, identifier), bytes, token ?? OrganizationsTokens.SignedIn());

    public static Task<ImageAnswer> GetAsync(MustaskApiFactory factory, Guid imageId, string? token, string? ifNoneMatch = null) =>
        SendAsync(factory, HttpMethod.Get, $"/api/images/{imageId}", null, token, null, ifNoneMatch);

    public static async Task<(BoardsWorld World, Item Item)> WorldWithItemAsync(MustaskApiFactory factory, string title = "История")
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var item = await BoardsTestData.CreateAsync(factory, world, "US", title, world.PlatformAreaId);

        return (world, item);
    }

    // ---- Pictures ----------------------------------------------------------------------------------------------------

    public static byte[] Png(int width, int height, double sigma = 10) =>
        (NetVips.Image.Black(width, height, bands: 3) + 128 + NetVips.Image.Gaussnoise(width, height, sigma: sigma, seed: 5)).Cast(NetVips.Enums.BandFormat.Uchar).PngsaveBuffer();

    /// <summary>A PNG of seeded random bytes: nothing to compress, so no reduction within the floors can reach 5 MB.</summary>
    public static byte[] NoisePng(int width, int height)
    {
        var pixels = new byte[width * height * 3];
        new Random(11).NextBytes(pixels);

        return NetVips.Image.NewFromMemory(pixels, width, height, 3, NetVips.Enums.BandFormat.Uchar).PngsaveBuffer();
    }

    public static byte[] Jpeg(int width, int height, int quality = 90, double sigma = 12) =>
        (NetVips.Image.Black(width, height, bands: 3) + 128 + NetVips.Image.Gaussnoise(width, height, sigma: sigma, seed: 5)).Cast(NetVips.Enums.BandFormat.Uchar).JpegsaveBuffer(q: quality);

    /// <summary>A JPEG whose body is exactly <paramref name="length"/> bytes: the tail after its end is dropped by the intake.</summary>
    public static byte[] PaddedTo(byte[] image, int length) => [.. image, .. new byte[length - image.Length]];

    /// <summary>A PNG signature and an IHDR announcing the given size and nothing after: the header of a bomb.</summary>
    public static byte[] PngHeaderOnly(uint width, uint height)
    {
        var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8;
        ihdr[9] = 2;
        Chunk(output, "IHDR", ihdr);
        Chunk(output, "IEND", []);

        return output.ToArray();
    }

    private static void Chunk(Stream output, string type, ReadOnlySpan<byte> body)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)body.Length);
        output.Write(length);
        output.Write(System.Text.Encoding.ASCII.GetBytes(type));
        output.Write(body);
        output.Write(new byte[4]);
    }

    public static byte[] AnimatedGif(int frames, int size = 8)
    {
        var list = Enumerable.Range(0, frames)
            .Select(i => NetVips.Image.Black(size, size, bands: 3) + new double[] { i * 6 % 256, 255 - (i * 6 % 256), 128 })
            .ToArray();
        var joined = NetVips.Image.Arrayjoin(list, across: 1).Cast(NetVips.Enums.BandFormat.Uchar);
        var mutated = joined.Mutate(mutable =>
        {
            mutable.Set(NetVips.GValue.GIntType, "page-height", size);
            mutable.Set(NetVips.GValue.ArrayIntType, "delay", Enumerable.Repeat(100, frames).ToArray());
            mutable.Set(NetVips.GValue.GIntType, "loop", 0);
        });

        return mutated.GifsaveBuffer(dither: 0);
    }

    public static byte[] Svg() =>
        System.Text.Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"4\" height=\"4\"><script>alert(1)</script></svg>");

    // ---- The database ------------------------------------------------------------------------------------------------

    public static async Task<int> CountForAsync(MustaskApiFactory factory, Guid workItemId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Images.CountAsync(image => image.WorkItemId == workItemId);
    }

    /// <summary>Binds an image in the database: the save that binds is feat-078 step 9, the read rules do not wait for it.</summary>
    public static async Task BindAsync(MustaskApiFactory factory, Guid imageId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var image = await context.Images.SingleAsync(candidate => candidate.Id == imageId);
        image.Bind(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();
    }

    public static async Task<Guid> UploadBoundAsync(MustaskApiFactory factory, WorkItemsWorld world, string identifier, byte[]? bytes = null)
    {
        var answer = await UploadAsync(factory, world, identifier, bytes ?? Png(60, 40));
        var id = answer.Body.GetProperty("id").GetGuid();
        await BindAsync(factory, id);

        return id;
    }

    public static async Task<Image?> FindAsync(MustaskApiFactory factory, Guid imageId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Images.AsNoTracking().SingleOrDefaultAsync(image => image.Id == imageId);
    }
}
