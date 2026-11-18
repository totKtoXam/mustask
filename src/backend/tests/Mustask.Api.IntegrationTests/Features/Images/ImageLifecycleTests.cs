using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Infrastructure;

namespace Mustask.Api.IntegrationTests.Features.Images;

/// <summary>An item's images live and die with it (feat-078 US-5, BR-IM-11, -17): a deleted item still shows them, a purge removes them.</summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ImageLifecycleTests(MustaskApiFactory factory)
{
    private async Task<int> ContentRowsAsync(Guid imageId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().ImageContents.CountAsync(content => content.ImageId == imageId);
    }

    private Task<Answer> PurgeAsync(BoardsWorld world, string identifier) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world.Items, identifier)}/purge", token: OrganizationsTokens.SignedIn());

    // US-5 · "A deleted item keeps its images": readable by whoever may read the item, while it sits in «Удалена».
    [Fact]
    public async Task DeletedItem_ImagesStayReadable()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = await ImagesTestData.UploadBoundAsync(factory, world.Items, item.Identifier);
        await LinksTestData.DeleteAsync(factory, world.Items, item);

        Assert.Equal(HttpStatusCode.OK, (await ImagesTestData.GetAsync(factory, id, OrganizationsTokens.SignedIn())).Status);
    }

    // US-5 · "A purge takes the item's images with it": bound and unbound, bytes included; another item's images stay.
    [Fact]
    public async Task Purge_RemovesTheItemsImagesWithTheirBytes_OthersStay()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var other = await BoardsTestData.CreateAsync(factory, world, "US", "Соседняя", world.PlatformAreaId);
        var bound = await ImagesTestData.UploadBoundAsync(factory, world.Items, item.Identifier);
        var unbound = (await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(30, 30))).Body.GetProperty("id").GetGuid();
        var neighbour = await ImagesTestData.UploadBoundAsync(factory, world.Items, other.Identifier);
        await LinksTestData.DeleteAsync(factory, world.Items, item);

        var purge = await PurgeAsync(world, item.Identifier);

        Assert.Equal(HttpStatusCode.NoContent, purge.Status);
        Assert.Equal(0, await ImagesTestData.CountForAsync(factory, item.Id));
        Assert.Equal(0, await ContentRowsAsync(bound));
        Assert.Equal(0, await ContentRowsAsync(unbound));
        Assert.Equal(HttpStatusCode.NotFound, (await ImagesTestData.GetAsync(factory, bound, OrganizationsTokens.SignedIn())).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await ImagesTestData.GetAsync(factory, unbound, OrganizationsTokens.SignedIn())).Status);
        Assert.Equal(HttpStatusCode.OK, (await ImagesTestData.GetAsync(factory, neighbour, OrganizationsTokens.SignedIn())).Status);
        Assert.Equal(1, await ContentRowsAsync(neighbour));
    }

    // US-5 · a purge of an item with no image still works (the cascade finds nothing).
    [Fact]
    public async Task Purge_ItemWithoutImages_StillRemovesTheItem()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        await LinksTestData.DeleteAsync(factory, world.Items, item);

        Assert.Equal(HttpStatusCode.NoContent, (await PurgeAsync(world, item.Identifier)).Status);
    }
}
