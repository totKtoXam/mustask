using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Process;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET …/teams/{teamId}/owned-items/count</c> (mod-017 api-contract § 2.9; US-7, BR-OT-12): what a deletion would reassign — the
/// items the team owns now, in any state. Requires <c>organizations.team.write</c>. Every test builds its own world.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetTeamOwnedItemCountEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> CountAsync(OwningTeamWorld world, Guid teamId, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"{OrganizationsApi.Teams(world.OrganizationId, world.ProjectId)}/{teamId}/owned-items/count",
            token: token ?? OrganizationsTokens.SignedIn());

    private async Task<Item> StoryAsync(OwningTeamWorld world, string title, Guid? team)
    {
        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = world.Items.Types["US"].Id.ToString(),
                title,
                team = new { teamId = team?.ToString() },
            },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    // US-7 · "the confirmation counts": the count is of the items the team owns at the moment of the read, in every state —
    // Removed and closed included — and an item given to the team afterwards is not in the count already read.
    [Fact]
    public async Task Count_EveryState_AtTheMomentOfTheRead()
    {
        var world = await CreateWorldAsync(factory);
        var open = await StoryAsync(world, "Открытая", world.Mars);
        var closed = await StoryAsync(world, "Закрытая", world.Mars);
        var removed = await StoryAsync(world, "Удалённая", world.Mars);
        await StoryAsync(world, "Венера", world.Venus);
        await StoryAsync(world, "Ничья", null);
        await WorkItemsTestData.SaveAsync(
            factory, world.Items, closed.Identifier, new { stateId = world.Items.Types["US"].StateId(StateCategory.Completed).ToString() });
        await LinksTestData.DeleteAsync(factory, world.Items, removed);

        var read = await CountAsync(world, world.Mars);
        await StoryAsync(world, "Позже", world.Mars);
        var later = await CountAsync(world, world.Mars);

        Assert.Equal(HttpStatusCode.OK, read.Status);
        Assert.Equal(3, read.Body.GetProperty("count").GetInt32());
        Assert.Equal(4, later.Body.GetProperty("count").GetInt32());
        Assert.Equal(["count"], read.Body.EnumerateObject().Select(member => member.Name));
        Assert.Equal(0, (await CountAsync(world, world.OwnTeam)).Body.GetProperty("count").GetInt32());
        Assert.NotNull(open);
    }

    // US-7 · "the confirmation counts", the role: only an administrator holds organizations.team.write.
    [Fact]
    public async Task Count_NonAdministrator_Returns403()
    {
        var world = await CreateWorldAsync(factory);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        var reader = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);

        var asMember = await CountAsync(world, world.Mars, member.Token);
        var asReader = await CountAsync(world, world.Mars, reader.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (asMember.Status, asMember.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (asReader.Status, asReader.ErrorCode));
    }

    // api-contract § 2.9 · a deleted team, an unknown one, another project's and another organization's answer team.not_found,
    // alike.
    [Theory]
    [InlineData("deleted")]
    [InlineData("unknown")]
    [InlineData("otherProject")]
    [InlineData("foreign")]
    [InlineData("deactivatedProject")]
    public async Task Count_NotATeamOfTheProject_Returns404TeamNotFound(string which)
    {
        var world = await CreateWorldAsync(factory);
        var teamId = which switch
        {
            "deleted" => world.DeletedTeam,
            "otherProject" => world.OtherProjectTeam,
            "foreign" => world.OtherOrganizationTeam,
            "deactivatedProject" => world.DeactivatedProjectTeam,
            _ => Guid.CreateVersion7(),
        };

        var answer = await CountAsync(world, teamId);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (answer.Status, answer.ErrorCode));
    }

    // BR-OT-16 · and the malformed id answers the same.
    [Fact]
    public async Task Count_MalformedTeamId_Returns404TeamNotFound()
    {
        var world = await CreateWorldAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"{OrganizationsApi.Teams(world.OrganizationId, world.ProjectId)}/not-an-id/owned-items/count",
            token: OrganizationsTokens.SignedIn());

        Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (answer.Status, answer.ErrorCode));
    }
}
