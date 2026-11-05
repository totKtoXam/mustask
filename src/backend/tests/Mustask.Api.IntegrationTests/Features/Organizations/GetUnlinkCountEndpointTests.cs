using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET …/projects/{projectId}/teams/{teamId}/unlink-count</c> (feat-028 api-contract § 2.4; US-4, SC-09): what an unlink would
/// reassign — the items the team owns in <b>this project</b> now, in any state. Requires <c>organizations.project.link</c> and, from
/// the handler, <c>organizations.team.link</c>. Replaces <c>GetTeamOwnedItemCountEndpointTests</c> (mod-017 § 2.9, the count of a
/// team's deletion over its project: the count moved to the unlink and the deletion's counts to <c>deletion-counts</c>, D-16).
/// Every test builds its own world.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetUnlinkCountEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> CountAsync(OwningTeamWorld world, Guid teamId, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"{ProjectTeam(world.OrganizationId, world.ProjectId, teamId)}/unlink-count",
            token: token ?? OrganizationsTokens.SignedIn());

    private async Task<Item> StoryAsync(OwningTeamWorld world, string title, Guid? team, Guid? projectId = null, string? key = null)
    {
        var items = projectId is null ? world.Items : world.Items with { ProjectId = projectId.Value, Key = key! };
        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(items),
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

    // US-4 · "the confirmation counts": the count is of the items the team owns in this project at the moment of the read, in every
    // state — Removed and closed included — and an item given to the team afterwards is not in the count already read.
    [Fact]
    public async Task Count_EveryState_AtTheMomentOfTheRead()
    {
        var world = await CreateWorldAsync(factory);
        await StoryAsync(world, "Открытая", world.Mars);
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
    }

    // api-contract § 1.5 · "the same number for one project": the items the team owns in ANOTHER project it is linked to are not in
    // this project's count, and an unlink there would not touch them.
    [Fact]
    public async Task Count_IsOfThisProjectOnly_ItemsInAnotherLinkedProjectAreNotCounted()
    {
        var world = await CreateWorldAsync(factory);
        await OrganizationsTestData.LinkAsync(factory, world.OtherProjectId, world.Mars);
        await StoryAsync(world, "Альфа", world.Mars);
        await StoryAsync(world, "Бета один", world.Mars, world.OtherProjectId, "BET");
        await StoryAsync(world, "Бета два", world.Mars, world.OtherProjectId, "BET");

        var here = await CountAsync(world, world.Mars);

        Assert.Equal(1, here.Body.GetProperty("count").GetInt32());
        Assert.Equal(3, await OwnedItemCountAsync(factory, world.Mars));
    }

    // US-4 · a linked team that owns nothing here counts zero: the confirmation opens all the same and the unlink moves nothing.
    [Fact]
    public async Task Count_LinkedTeamThatOwnsNothing_ReturnsZero()
    {
        var world = await CreateWorldAsync(factory);

        var answer = await CountAsync(world, world.Venus);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(0, answer.Body.GetProperty("count").GetInt32());
    }

    // US-4, the role: only an administrator holds organizations.project.link and organizations.team.link.
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

    // api-contract § 0.3 · "a team that is not linked to this project is team.not_found": a deleted team, an unknown one, one linked to
    // another project only, one linked to none, and another organization's answer alike.
    [Theory]
    [InlineData("deleted")]
    [InlineData("unknown")]
    [InlineData("linked to another project only")]
    [InlineData("linked to no project")]
    [InlineData("foreign")]
    [InlineData("of a deactivated project")]
    public async Task Count_NotALinkedActiveTeamOfTheProject_Returns404TeamNotFound(string which)
    {
        var world = await CreateWorldAsync(factory);
        var unlinked = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, world.OrganizationId, "Ничья");
        var teamId = which switch
        {
            "deleted" => world.DeletedTeam,
            "linked to another project only" => world.OtherProjectTeam,
            "linked to no project" => unlinked.Id,
            "foreign" => world.OtherOrganizationTeam,
            "of a deactivated project" => world.DeactivatedProjectTeam,
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
            $"{OrganizationsApi.Teams(world.OrganizationId, world.ProjectId)}/not-an-id/unlink-count",
            token: OrganizationsTokens.SignedIn());

        Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (answer.Status, answer.ErrorCode));
    }

    // ADR-0026 rule 5 · the project level answers before the team: a deactivated project is project.not_found, the team being fine.
    [Fact]
    public async Task Count_DeactivatedProject_Returns404ProjectNotFound()
    {
        var world = await CreateWorldAsync(factory);
        await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, world.ProjectId, ProjectStatus.Deactivated);

        var answer = await CountAsync(world, world.Mars);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found"), (answer.Status, answer.ErrorCode));
    }
}
