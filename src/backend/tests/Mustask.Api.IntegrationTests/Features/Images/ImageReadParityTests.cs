using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Images;

/// <summary>The image read answers 200 exactly when the item's own read does (feat-078 BR-IM-10, -11).</summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ImageReadParityTests(MustaskApiFactory factory)
{
    private async Task AssertParityAsync(BoardsWorld world, Item item, Guid imageId, string token)
    {
        var itemRead = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, WorkItemsTestData.Item(world.Items, item.Identifier), token: token);
        var imageRead = await ImagesTestData.GetAsync(factory, imageId, token);

        Assert.Equal(itemRead.Status == HttpStatusCode.OK, imageRead.Status == HttpStatusCode.OK);
    }

    [Fact]
    public async Task ImageRead_FollowsTheItemRead_ForEveryKindOfReader()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = await ImagesTestData.UploadBoundAsync(factory, world.Items, item.Identifier);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);

        foreach (var token in new[] { OrganizationsTokens.SignedIn(), member.Token, viewer.Token, OrganizationsTokens.Stranger() })
        {
            await AssertParityAsync(world, item, id, token);
        }
    }

    [Fact]
    public async Task ImageRead_FollowsTheItemRead_DeactivatedProject()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = await ImagesTestData.UploadBoundAsync(factory, world.Items, item.Identifier);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, world.ProjectId, ProjectStatus.Deactivated);

        foreach (var token in new[] { OrganizationsTokens.SignedIn(), member.Token })
        {
            await AssertParityAsync(world, item, id, token);
        }
    }

    [Fact]
    public async Task ImageRead_FollowsTheItemRead_DeactivatedOrganization()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = await ImagesTestData.UploadBoundAsync(factory, world.Items, item.Identifier);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        await OrganizationsTestData.SetOrganizationStatusAsync(factory, world.OrganizationId, OrganizationStatus.Deactivated);

        foreach (var token in new[] { OrganizationsTokens.SignedIn(), member.Token })
        {
            await AssertParityAsync(world, item, id, token);
        }
    }
}
