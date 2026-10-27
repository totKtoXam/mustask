using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Infrastructure;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// feat-028 US-6, "one team, many projects" (BR-OL-5; D-13): MARS is linked to «Альфа» and «Бета» at once. It is one team —
/// one name, one list of people — and in each project it has that project's default and that project's items: an unlink from one
/// project leaves the other's items and setting. Its backlog and board are one view over both (feat-040 BR-TS-1, BR-TS-2: they are
/// at the team's address; the tests of BR-OL-11, the views at a project's address holding that project's items, and of D-13, a reorder
/// writing one project's places, are retired with the rule and live on as <c>TeamBacklogAcrossProjectsEndpointTests</c> and
/// <c>TeamOrderAcrossProjectsEndpointTests</c>). Every test builds its own world.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class TeamAcrossProjectsEndpointTests(MustaskApiFactory factory)
{
    private sealed record Both(OwningTeamWorld World, BoardsWorld Alpha, BoardsWorld Beta);

    /// <summary>The world with MARS linked to «Бета» as well as to «Альфа», and each project's view of it.</summary>
    private async Task<Both> BothAsync()
    {
        var world = await CreateWorldAsync(factory);
        await OrganizationsTestData.LinkAsync(factory, world.OtherProjectId, world.Mars);
        var beta = new BoardsWorld(world.Items with { ProjectId = world.OtherProjectId, Key = "BET" }, Guid.Empty, world.Mars, Guid.Empty, Guid.Empty, Guid.Empty);

        return new Both(world, BoardsViewOf(world), beta);
    }
    private async Task<Item> StoryAsync(BoardsWorld view, string title, Guid? team)
    {
        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(view.Items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = view.Items.Types["US"].Id.ToString(),
                title,
                team = new { teamId = team?.ToString() },
            },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    // feat-028 US-6 · "Rename_ShowsInEveryProjectsList": one team, one name — renamed once, it is renamed in both projects' lists, the
    // organization's list and the team's own page.
    [Fact]
    public async Task Rename_ShowsInEveryProjectsList()
    {
        var both = await BothAsync();
        var world = both.World;

        var renamed = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, $"{Team(world.OrganizationId, world.Mars)}/name", new { name = "МАРС" }, OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, renamed.Status);
        foreach (var project in new[] { world.ProjectId, world.OtherProjectId })
        {
            var list = await GetAsync(factory, OrganizationsApi.Teams(world.OrganizationId, project));
            Assert.Contains("МАРС", list.Body.EnumerateArray().Select(team => team.GetProperty("name").GetString()));
        }

        var organizationList = await GetAsync(factory, OrganizationsApi.OrgTeams(world.OrganizationId));
        Assert.Contains("МАРС", organizationList.Body.EnumerateArray().Select(team => team.GetProperty("name").GetString()));
    }

    // feat-028 US-6 · "Members_AreMarkedInEveryProject": the team's people are the team's, so a person in MARS is a member of it in each
    // project's list.
    [Fact]
    public async Task Members_AreMarkedInEveryProject()
    {
        var both = await BothAsync();
        var world = both.World;
        var person = await MembershipTestData.NewNamedMemberAsync(factory, world.OrganizationId, OrganizationRole.Member, "Вера", "Новикова");
        await MembershipTestData.AddTeamMemberAsync(factory, world.OrganizationId, world.Mars, person.Id);

        foreach (var project in new[] { world.ProjectId, world.OtherProjectId })
        {
            var list = await GetAsync(factory, OrganizationsApi.Teams(world.OrganizationId, project), person.Token);
            var mars = list.Body.EnumerateArray().Single(team => team.GetProperty("id").GetGuid() == world.Mars);

            Assert.True(mars.GetProperty("isMember").GetBoolean());
        }
    }

    // feat-028 US-6 · "Default_IsPerProject": naming MARS the default of «Альфа» names nothing in «Бета», which has JUPITER too (so no
    // inference either); naming it in «Бета» as well gives each project its own setting.
    [Fact]
    public async Task Default_IsPerProject()
    {
        var both = await BothAsync();
        var world = both.World;
        var betaProject = OrganizationsApi.Project(world.OrganizationId, world.OtherProjectId);

        var named = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Project(world.OrganizationId, world.ProjectId)}/default-team",
            new { teamId = world.Mars.ToString() },
            OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.NoContent, named.Status);
        Assert.Equal(world.Mars, await SettingAsync(factory, world.ProjectId));
        Assert.Null(await SettingAsync(factory, world.OtherProjectId));
        Assert.Null(await MarkOfMarsAsync(world, world.OtherProjectId));
        Assert.Equal("named", await MarkOfMarsAsync(world, world.ProjectId));

        var namedInBeta = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, $"{betaProject}/default-team", new { teamId = world.Mars.ToString() }, OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.NoContent, namedInBeta.Status);
        Assert.Equal(world.Mars, await SettingAsync(factory, world.OtherProjectId));
        Assert.Equal("named", await MarkOfMarsAsync(world, world.OtherProjectId));
    }

    // feat-028 US-6 · "Unlink_FromOneProject_LeavesTheOthersSettingAndItems": taking MARS off «Альфа» clears «Альфа»'s setting and moves
    // «Альфа»'s items; «Бета»'s setting, items, places and link stay.
    [Fact]
    public async Task Unlink_FromOneProject_LeavesTheOthersSettingAndItems()
    {
        var both = await BothAsync();
        var world = both.World;
        var alpha = await StoryAsync(both.Alpha, "Альфа", world.Mars);
        var beta = await StoryAsync(both.Beta, "Бета", world.Mars);
        await BoardsTestData.ReorderToAsync(factory, both.Beta, world.Mars, beta);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET default_team_id = {world.Mars} WHERE id IN ({world.ProjectId}, {world.OtherProjectId})");

        var answer = await TeamTestData.UnlinkAsync(factory, world.OrganizationId, world.ProjectId, world.Mars, To(null));

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Null(await SettingAsync(factory, world.ProjectId));
        Assert.Null((await StoredAsync(factory, world.ProjectId, alpha.Identifier)).TeamId);
        Assert.Equal(world.Mars, await SettingAsync(factory, world.OtherProjectId));
        Assert.Equal(world.Mars, (await StoredAsync(factory, world.OtherProjectId, beta.Identifier)).TeamId);
        Assert.Equal(1, await TeamPlacesOfAsync(factory, beta.Id));
        Assert.Equal([world.OtherProjectId], (await LinksOfTeamAsync(factory, world.Mars)).Select(link => link.ProjectId));
        Assert.Equal([beta.Identifier], await BoardsTestData.RowsAsync(factory, both.Beta, world.Mars));
    }

    // feat-028 US-6 · "Unlink_TeamBecomes404AtTheProjectsAddress", rewritten by feat-040 (BR-TS-1, BR-TS-2): the team's views are at its own
    // address and hold what it owns in the projects it is still linked to. After the unlink from «Альфа» the team's backlog and board
    // answer 200 and hold «Бета»'s item only («Альфа»'s went to «Без команды»); its own page still reads; and the views of the team at the
    // project's old address are not served (404 http.not_found, RetiredTeamViewRoutesEndpointTests).
    [Fact]
    public async Task Unlink_TeamBecomes404AtTheProjectsAddress_ButIsStillReadAtItsOwn()
    {
        var both = await BothAsync();
        var world = both.World;
        await StoryAsync(both.Alpha, "Альфа", world.Mars);
        var beta = await StoryAsync(both.Beta, "Бета", world.Mars);

        await TeamTestData.UnlinkAsync(factory, world.OrganizationId, world.ProjectId, world.Mars, To(null));

        var backlog = await BoardsTestData.GetBacklogAsync(factory, both.Alpha, world.Mars);
        var board = await BoardsTestData.GetBoardAsync(factory, both.Alpha, world.Mars);
        var own = await GetAsync(factory, Team(world.OrganizationId, world.Mars));
        var oldAddress = await GetAsync(factory, $"{ProjectTeam(world.OrganizationId, world.ProjectId, world.Mars)}/backlogs/stories");

        Assert.Equal(HttpStatusCode.OK, backlog.Status);
        Assert.Equal([beta.Identifier], await BoardsTestData.RowsAsync(factory, both.Alpha, world.Mars));
        Assert.Equal([beta.Identifier], ProjectBoardsTestData.AllCards(board.Body));
        Assert.Equal(HttpStatusCode.OK, own.Status);
        Assert.Equal((HttpStatusCode.NotFound, "http.not_found"), (oldAddress.Status, oldAddress.ErrorCode));
    }

    private async Task<string?> MarkOfMarsAsync(OwningTeamWorld world, Guid projectId)
    {
        var list = await GetAsync(factory, OrganizationsApi.Teams(world.OrganizationId, projectId));

        return list.Body.EnumerateArray().Single(team => team.GetProperty("id").GetGuid() == world.Mars).GetProperty("defaultMark").GetString();
    }

    private async Task<string> PlacesOfAsync(Guid projectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Database
            .SqlQuery<string>(
                $"""
                 SELECT place.work_item_id::text || ':' || place.position::text AS "Value"
                 FROM backlog_places AS place JOIN work_items AS item ON item.id = place.work_item_id
                 WHERE item.project_id = {projectId} ORDER BY place.work_item_id
                 """)
            .ToListAsync();

        return string.Join(",", rows);
    }
}
