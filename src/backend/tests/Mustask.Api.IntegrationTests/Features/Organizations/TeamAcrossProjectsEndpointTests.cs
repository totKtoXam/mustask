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
/// feat-028 US-6, "one team, many projects" (BR-OL-5, BR-OL-11; D-13): MARS is linked to «Альфа» and «Бета» at once. It is one team —
/// one name, one list of people — and in each project it has that project's items, that project's default and that project's places:
/// a reorder in one project writes that project's places only; an unlink from one project leaves the other's items and setting; a
/// project it is not linked to answers <c>team.not_found</c> at its address. Every test builds its own world.
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
        var betaRoot = await RootOfAsync(world.OtherProjectId);
        var beta = new BoardsWorld(world.Items with { ProjectId = world.OtherProjectId, RootAreaId = betaRoot, Key = "BET" }, Guid.Empty, world.Mars, Guid.Empty, Guid.Empty, Guid.Empty);

        return new Both(world, BoardsViewOf(world), beta);
    }

    private async Task<Guid> RootOfAsync(Guid projectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Areas
            .AsNoTracking()
            .Where(area => area.ProjectId == projectId && area.ParentId == null)
            .Select(area => area.Id)
            .SingleAsync();
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

    // feat-028 US-6 · "TeamViews_AtEachProjectsAddress_HoldThatProjectsItemsOnly": MARS's backlog and board at «Альфа»'s address hold its
    // «Альфа» items; at «Бета»'s, its «Бета» items — the same team, two views.
    [Fact]
    public async Task TeamViews_AtEachProjectsAddress_HoldThatProjectsItemsOnly()
    {
        var both = await BothAsync();
        var alpha = await StoryAsync(both.Alpha, "Альфа-история", both.World.Mars);
        var beta = await StoryAsync(both.Beta, "Бета-история", both.World.Mars);
        await StoryAsync(both.Alpha, "Чужая альфа", both.World.Venus);

        var alphaRows = await BoardsTestData.RowsAsync(factory, both.Alpha, both.World.Mars);
        var betaRows = await BoardsTestData.RowsAsync(factory, both.Beta, both.World.Mars);
        var alphaBoard = await BoardsTestData.GetBoardAsync(factory, both.Alpha, both.World.Mars);
        var betaBoard = await BoardsTestData.GetBoardAsync(factory, both.Beta, both.World.Mars);

        Assert.Equal([alpha.Identifier], alphaRows);
        Assert.Equal([beta.Identifier], betaRows);
        Assert.Equal([alpha.Identifier], ProjectBoardsTestData.AllCards(alphaBoard.Body));
        Assert.Equal([beta.Identifier], ProjectBoardsTestData.AllCards(betaBoard.Body));
    }

    // feat-028 D-13, US-6 · "Reorder_InOneProject_LeavesTheOthersPlacesAndProjectOrdersAlone": the team's order is one per level, but a
    // reorder in one project's view writes the places of that view's items only: the other project's places and its relative order never
    // move, and neither does either project's own backlog order.
    [Fact]
    public async Task Reorder_InOneProject_LeavesTheOthersPlacesAndProjectOrdersAlone()
    {
        var both = await BothAsync();
        var a1 = await StoryAsync(both.Alpha, "Альфа 1", both.World.Mars);
        var a2 = await StoryAsync(both.Alpha, "Альфа 2", both.World.Mars);
        var b1 = await StoryAsync(both.Beta, "Бета 1", both.World.Mars);
        var b2 = await StoryAsync(both.Beta, "Бета 2", both.World.Mars);
        await BoardsTestData.ReorderToAsync(factory, both.Alpha, both.World.Mars, a1, a2);
        await BoardsTestData.ReorderToAsync(factory, both.Beta, both.World.Mars, b1, b2);
        var betaTeamOrder = await BoardsTestData.RowsAsync(factory, both.Beta, both.World.Mars);
        var betaProjectOrder = await ProjectBoardsTestData.RowsAsync(factory, both.Beta);
        var alphaProjectOrder = await ProjectBoardsTestData.RowsAsync(factory, both.Alpha);
        var betaPlaces = await PlacesOfAsync(both.World.OtherProjectId);

        var answer = await BoardsTestData.ReorderAsync(factory, both.Alpha, both.World.Mars, BoardsTestData.Top(a2));

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal([a2.Identifier, a1.Identifier], await BoardsTestData.RowsAsync(factory, both.Alpha, both.World.Mars));
        Assert.Equal(betaTeamOrder, await BoardsTestData.RowsAsync(factory, both.Beta, both.World.Mars));
        Assert.Equal(betaPlaces, await PlacesOfAsync(both.World.OtherProjectId));
        Assert.Equal(betaProjectOrder, await ProjectBoardsTestData.RowsAsync(factory, both.Beta));
        Assert.Equal(alphaProjectOrder, await ProjectBoardsTestData.RowsAsync(factory, both.Alpha));
        Assert.Equal(0, await PlacesNamingAnotherTeamAsync(factory, both.World.ProjectId) + await PlacesNamingAnotherTeamAsync(factory, both.World.OtherProjectId));
    }

    // feat-028 D-13 · the places a reorder drops for items that left the view are those of THIS project: an item MARS no longer owns in
    // «Альфа» loses its place there, and «Бета»'s places stay.
    [Fact]
    public async Task Reorder_DropsTheDepartedPlacesOfThisProjectOnly()
    {
        var both = await BothAsync();
        var a1 = await StoryAsync(both.Alpha, "Альфа 1", both.World.Mars);
        var a2 = await StoryAsync(both.Alpha, "Альфа 2", both.World.Mars);
        var b1 = await StoryAsync(both.Beta, "Бета 1", both.World.Mars);
        await BoardsTestData.ReorderToAsync(factory, both.Alpha, both.World.Mars, a1, a2);
        await BoardsTestData.ReorderToAsync(factory, both.Beta, both.World.Mars, b1);
        await SetOwningTeamAsync(factory, a1.Id, both.World.Venus);

        var answer = await BoardsTestData.ReorderAsync(factory, both.Alpha, both.World.Mars, BoardsTestData.Top(a2));

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(0, await TeamPlacesOfAsync(factory, a1.Id));
        Assert.Equal(1, await TeamPlacesOfAsync(factory, b1.Id));
        Assert.Equal([b1.Identifier], await BoardsTestData.RowsAsync(factory, both.Beta, both.World.Mars));
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

    // feat-028 US-6 · "TeamViews_UnderAnUnlinkedProject_Return404Team", "Reorder_…_MemberIs404_ViewerIs403": at the address of a project
    // MARS is not linked to, its backlog, board and reorder answer team.not_found — also for a team that is nobody's — and a reorder by a
    // viewer is 403 first (BR-OL-11).
    [Fact]
    public async Task TeamViews_UnderAnUnlinkedProject_Return404Team_ReorderByViewerIs403First()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(BoardsViewOf(world), "Марс", world.Mars);
        var betaView = new BoardsWorld(world.Items with { ProjectId = world.OtherProjectId, Key = "BET" }, Guid.Empty, world.Mars, Guid.Empty, Guid.Empty, Guid.Empty);
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);

        var backlog = await BoardsTestData.GetBacklogAsync(factory, betaView, world.Mars);
        var board = await BoardsTestData.GetBoardAsync(factory, betaView, world.Mars);
        var asAdmin = await BoardsTestData.ReorderAsync(factory, betaView, world.Mars, BoardsTestData.Top(story));
        var asMember = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{BoardsTestData.Backlog(betaView, world.Mars, "stories")}/reorder", BoardsTestData.Top(story), member.Token);
        var asViewer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{BoardsTestData.Backlog(betaView, world.Mars, "stories")}/reorder", BoardsTestData.Top(story), viewer.Token);

        foreach (var answer in new[] { backlog, board, asAdmin, asMember })
        {
            Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (answer.Status, answer.ErrorCode));
        }

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (asViewer.Status, asViewer.ErrorCode));
        Assert.Equal(0, await TeamPlacesOfAsync(factory, story.Id));
    }

    // feat-028 US-6 · "Unlink_TeamBecomes404AtTheProjectsAddress", the switcher's fallback: after the unlink the team's views at the
    // project's address answer team.not_found, while the team itself is read as before at its own address.
    [Fact]
    public async Task Unlink_TeamBecomes404AtTheProjectsAddress_ButIsStillReadAtItsOwn()
    {
        var both = await BothAsync();

        await TeamTestData.UnlinkAsync(factory, both.World.OrganizationId, both.World.ProjectId, both.World.Mars, To(null));

        var view = await BoardsTestData.GetBacklogAsync(factory, both.Alpha, both.World.Mars);
        var own = await GetAsync(factory, Team(both.World.OrganizationId, both.World.Mars));
        var betaView = await BoardsTestData.GetBacklogAsync(factory, both.Beta, both.World.Mars);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (view.Status, view.ErrorCode));
        Assert.Equal(HttpStatusCode.OK, own.Status);
        Assert.Equal(HttpStatusCode.OK, betaView.Status);
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
