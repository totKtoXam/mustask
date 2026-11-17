using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Images;

namespace Mustask.Api.IntegrationTests.Features.Images;

/// <summary>
/// <c>POST …/workitems/{identifier}/images</c> and <c>…/images/reductions</c> (feat-078 api-contract § 1-2, US-2, US-6): the
/// order of answers, the refusals that store nothing, and the stored upload that is the caller's and unbound.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class UploadDescriptionImageEndpointTests(MustaskApiFactory factory)
{
    private async Task<int> RowsAsync(BoardsWorld world, Item item) =>
        await ImagesTestData.CountForAsync(factory, await ItemIdAsync(world, item));

    private static Task<Guid> ItemIdAsync(BoardsWorld world, Item item) => Task.FromResult(item.Id);

    // US-2 · "A valid upload": 201, only the opaque fields, a v4 id, stored unbound for the caller.
    [Fact]
    public async Task Upload_Png_201_StoredUnboundForTheCaller_OnlyOpaqueFields()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var png = ImagesTestData.Png(300, 200);

        var answer = await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, png);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        var body = answer.Body;
        Assert.Equal(["byteLength", "format", "height", "id", "width"], body.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal("png", body.GetProperty("format").GetString());
        Assert.Equal(300, body.GetProperty("width").GetInt32());
        Assert.Equal(200, body.GetProperty("height").GetInt32());

        var id = body.GetProperty("id").GetGuid();
        Assert.Equal(4, id.Version);
        Assert.NotEqual(item.Id, id);
        Assert.NotEqual(world.OrganizationId, id);
        Assert.NotEqual(world.ProjectId, id);

        var row = await ImagesTestData.FindAsync(factory, id);
        Assert.NotNull(row);
        Assert.Null(row.BoundAt);
        Assert.Equal(item.Id, row.WorkItemId);
        Assert.Equal(ImagePurpose.WorkItemDescription, row.Purpose);
        Assert.Equal(body.GetProperty("byteLength").GetInt32(), row.ByteLength);
    }

    // US-2 · "Format from content": the declared type is ignored.
    [Fact]
    public async Task Upload_PngDeclaredJpeg_StoredAsPng()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);

        var answer = await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(40, 30), contentType: "image/jpeg");

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal("png", answer.Body.GetProperty("format").GetString());
    }

    [Fact]
    public async Task Upload_SvgNamedPng_400Format_NothingStored()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);

        var answer = await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Svg(), contentType: "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("images.upload.format_unsupported", answer.ErrorCode);
        Assert.Equal(0, await RowsAsync(world, item));
    }

    // US-2 · "Caps": the boundary of 5 242 880 bytes.
    [Fact]
    public async Task Upload_5242880Bytes_201()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);

        var answer = await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.PaddedTo(ImagesTestData.Png(40, 30), ImageLimits.MaxStoredBytes));

        Assert.Equal(HttpStatusCode.Created, answer.Status);
    }

    [Fact]
    public async Task Upload_5242881Bytes_400TooLarge_NothingStored()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);

        var answer = await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.PaddedTo(ImagesTestData.Png(40, 30), ImageLimits.MaxStoredBytes + 1));

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("images.upload.too_large", answer.ErrorCode);
        Assert.Equal(0, await RowsAsync(world, item));
    }

    [Fact]
    public async Task Upload_BombPng_400Dimensions()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);

        var answer = await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.PngHeaderOnly(8000, 8000));

        Assert.Equal("images.upload.too_many_pixels", answer.ErrorCode);
        Assert.Equal(0, await RowsAsync(world, item));
    }

    // US-2 · "Hostile files": the route answers for a polyglot (a PNG with a script after it) and stores the PNG only.
    [Fact]
    public async Task Upload_PngWithScriptTail_StoredWithoutTheTail()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var png = ImagesTestData.Png(40, 30);
        byte[] polyglot = [.. png, .. System.Text.Encoding.ASCII.GetBytes("<script>alert(1)</script>")];

        var answer = await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, polyglot);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.True(answer.Body.GetProperty("byteLength").GetInt32() <= png.Length);
    }

    // US-2 · "Who may upload": a viewer 403; no token 401; a stranger 404; an unknown item 404; each stores nothing.
    [Fact]
    public async Task Upload_Viewer_403_NothingStored()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);

        var answer = await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(40, 30), viewer.Token);

        Assert.Equal(HttpStatusCode.Forbidden, answer.Status);
        Assert.Equal(0, await RowsAsync(world, item));
    }

    [Fact]
    public async Task Upload_NoToken_401_NothingStored()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);

        var answer = await ImagesTestData.SendAsync(factory, HttpMethod.Post, ImagesTestData.UploadPath(world.Items, item.Identifier), ImagesTestData.Png(40, 30));

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal(0, await RowsAsync(world, item));
    }

    [Fact]
    public async Task Upload_Stranger_404_UnknownItem_404()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);

        var stranger = await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(40, 30), OrganizationsTokens.Stranger());
        var unknown = await ImagesTestData.UploadAsync(factory, world.Items, "MSK-US-9999", ImagesTestData.Png(40, 30));

        Assert.Equal(HttpStatusCode.NotFound, stranger.Status);
        Assert.Equal(HttpStatusCode.NotFound, unknown.Status);
        Assert.Equal(0, await RowsAsync(world, item));
    }

    // US-2 · "Who may upload": an item in «Удалена» is 409 workitems.workitem.deleted (the write inventory's proof).
    [Fact]
    public async Task Upload_DeletedItem_409Deleted_NothingStored()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        await LinksTestData.DeleteAsync(factory, world.Items, item);

        var answer = await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(40, 30));

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("workitems.workitem.deleted", answer.ErrorCode);
        Assert.Equal(0, await RowsAsync(world, item));
    }

    [Fact]
    public async Task Reduce_DeletedItem_409Deleted_NothingStored()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        await LinksTestData.DeleteAsync(factory, world.Items, item);

        var answer = await ImagesTestData.ReduceAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(40, 30));

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("workitems.workitem.deleted", answer.ErrorCode);
        Assert.Equal(0, await RowsAsync(world, item));
    }
}
