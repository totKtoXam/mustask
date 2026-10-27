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
/// <c>GET …/teams/{teamId}/deletion-counts</c> (feat-028 api-contract § 1.5, § 2.1; US-5 "the confirmation counts per project"): what
/// a deletion would reassign, per linked project — deactivated projects and zeros included, items in any state. Requires
/// <c>organizations.team.delete</c>. Every test builds its own world.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetTeamDeletionCountsEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> CountsAsync(Guid organizationId, string teamId, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"{OrganizationsApi.OrgTeams(organizationId)}/{Uri.EscapeDataString(teamId)}/deletion-counts",
            token: token ?? OrganizationsTokens.SignedIn());

    private async Task<Item> StoryAsync(OwningTeamWorld world, Guid projectId, string key, string title, Guid? team)
    {
        var items = world.Items with { ProjectId = projectId, Key = key };
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

    private static List<(string Name, bool Active, int Count)> Read(Answer answer) =>
        answer.Body.GetProperty("projects").EnumerateArray()
            .Select(project => (
                project.GetProperty("name").GetString()!,
                project.GetProperty("active").GetBoolean(),
                project.GetProperty("count").GetInt32()))
            .ToList();

    // feat-028 US-5 · "the confirmation counts per project": every project the team is linked to, by project name, deactivated and
    // empty ones included, with the items it owns there in any state — Removed and closed included — and none of another team's.
    [Fact]
    public async Task Counts_PerLinkedProject_DeactivatedAndZerosIncluded_AnyState()
    {
        var world = await CreateWorldAsync(factory);
        var gamma = await OrganizationsTestData.CreateBareProjectAsync(factory, world.OrganizationId, "Гамма", "GAM");
        var delta = await OrganizationsTestData.CreateBareProjectAsync(factory, world.OrganizationId, "Дельта", "DEL");
        await OrganizationsTestData.LinkAsync(factory, world.OtherProjectId, world.Mars);
        await OrganizationsTestData.LinkAsync(factory, gamma.Id, world.Mars);
        await OrganizationsTestData.LinkAsync(factory, delta.Id, world.Mars);
        await StoryAsync(world, world.ProjectId, "ALP", "Открытая", world.Mars);
        var closed = await StoryAsync(world, world.ProjectId, "ALP", "Закрытая", world.Mars);
        var removed = await StoryAsync(world, world.ProjectId, "ALP", "Удалённая", world.Mars);
        await StoryAsync(world, world.ProjectId, "ALP", "Венера", world.Venus);
        await StoryAsync(world, world.ProjectId, "ALP", "Ничья", null);
        await StoryAsync(world, gamma.Id, "GAM", "Гамма один", world.Mars);
        await StoryAsync(world, delta.Id, "DEL", "Дельта один", world.Mars);
        await StoryAsync(world, delta.Id, "DEL", "Дельта два", world.Mars);
        await WorkItemsTestData.SaveAsync(
            factory, world.Items, closed.Identifier, new { stateId = world.Items.Types["US"].StateId(StateCategory.Completed).ToString() });
        await LinksTestData.DeleteAsync(factory, world.Items, removed);
        await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, delta.Id, ProjectStatus.Deactivated);

        var answer = await CountsAsync(world.OrganizationId, world.Mars.ToString());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(["projects"], PropertyNames(answer.Body));
        Assert.Equal(
            [("Альфа", true, 3), ("Бета", true, 0), ("Гамма", true, 1), ("Дельта", false, 2)],
            Read(answer));
        Assert.Equal(["active", "count", "name", "projectId"], PropertyNames(answer.Body.GetProperty("projects")[0]));
    }

    // feat-028 US-5 · "a team linked to no project": an empty list, not a not-found.
    [Fact]
    public async Task Counts_TeamLinkedToNone_IsEmpty()
    {
        var world = await CreateWorldAsync(factory);
        var alone = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, world.OrganizationId, "Ничья");

        var answer = await CountsAsync(world.OrganizationId, alone.Id.ToString());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Empty(answer.Body.GetProperty("projects").EnumerateArray());
    }

    // feat-028 api-contract § 1.5 · "an item given to the team after a count was read is not in it": the count is of the moment of the
    // read.
    [Fact]
    public async Task Counts_AreOfTheMomentOfTheRead()
    {
        var world = await CreateWorldAsync(factory);
        await StoryAsync(world, world.ProjectId, "ALP", "Раньше", world.Mars);

        var read = await CountsAsync(world.OrganizationId, world.Mars.ToString());
        await StoryAsync(world, world.ProjectId, "ALP", "Позже", world.Mars);
        var later = await CountsAsync(world.OrganizationId, world.Mars.ToString());

        Assert.Equal([("Альфа", true, 1)], Read(read));
        Assert.Equal([("Альфа", true, 2)], Read(later));
    }

    // feat-028 US-5 · "only an administrator deletes": organizations.team.delete is the administrator's alone.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Counts_MemberAndViewer_Return403(OrganizationRole role)
    {
        var world = await CreateWorldAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, role);

        var answer = await CountsAsync(world.OrganizationId, world.Mars.ToString(), caller.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (answer.Status, answer.ErrorCode));
    }

    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Counts_NoToken_Returns401()
    {
        var world = await CreateWorldAsync(factory);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{Team(world.OrganizationId, world.Mars)}/deletion-counts");

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
    }

    // api-contract § 0.4 · a deleted team, an unknown one, another organization's and a malformed id answer team.not_found, alike.
    [Theory]
    [InlineData("deleted")]
    [InlineData("unknown")]
    [InlineData("foreign")]
    [InlineData("malformed")]
    public async Task Counts_NotAnActiveTeamOfTheOrganization_Returns404TeamNotFound(string which)
    {
        var world = await CreateWorldAsync(factory);
        var teamId = which switch
        {
            "deleted" => world.DeletedTeam.ToString(),
            "foreign" => world.OtherOrganizationTeam.ToString(),
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => "not-an-id",
        };

        var answer = await CountsAsync(world.OrganizationId, teamId);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (answer.Status, answer.ErrorCode));
    }
}
