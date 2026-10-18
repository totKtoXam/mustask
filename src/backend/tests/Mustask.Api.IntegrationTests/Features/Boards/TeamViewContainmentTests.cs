using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using static Mustask.Api.IntegrationTests.Features.Boards.TeamAcrossProjectsTestData;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// Tenant containment, access and the answer order of the three team-view routes (feat-040 api-contract §§ 0, 8.2; US-7;
/// BR-TS-13; tech-011 m3): on each of the backlog read, the board read and the reorder at <c>/api/organizations/{o}/teams/{t}</c> a
/// team, project or item id of another organization answers as an id that names nothing does — identical in status, code and
/// text — and no answer says whether the other object exists; a non-member, an instance administrator who is no member included,
/// learns nothing of the team; the answers come in mod-016's order; the project views keep their boundary; the route inventory
/// lists the three new routes and not the three old.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class TeamViewContainmentTests(MustaskApiFactory factory)
{
    private const string Stale = "boards.backlog.reorder_stale";

    /// <summary>The three team-view routes of a team, as <c>(name, send)</c>: the reads and a reorder naming an item.</summary>
    private static readonly string[] Routes = ["backlog", "board", "reorder"];

    private Task<Answer> SendAsync(string route, Guid organizationId, object team, object? item = null, string? token = null, string? query = null) =>
        route switch
        {
            "backlog" => OrganizationsApi.SendAsync(
                factory, HttpMethod.Get, $"/api/organizations/{organizationId}/teams/{team}/backlogs/stories{query}", token: token ?? OrganizationsTokens.SignedIn()),
            "board" => OrganizationsApi.SendAsync(
                factory, HttpMethod.Get, $"/api/organizations/{organizationId}/teams/{team}/boards/stories{query}", token: token ?? OrganizationsTokens.SignedIn()),
            _ => OrganizationsApi.SendAsync(
                factory,
                HttpMethod.Post,
                $"/api/organizations/{organizationId}/teams/{team}/backlogs/stories/reorder",
                new { itemId = (item ?? Guid.CreateVersion7()).ToString(), position = "top" },
                token ?? OrganizationsTokens.SignedIn()),
        };

    private static (HttpStatusCode, string?, string?) Shape(Answer answer) => (answer.Status, answer.ErrorCode, answer.Detail);

    // US-7 · "A non-member learns nothing": Oleg (no member of «Ромашка») and Ira (an instance administrator carrying every code, no
    // member either) call each route for MARS, for an unknown team and for Orion — every answer is «Организация не найдена.»,
    // identical in status and body whatever the team (ADR-0027).
    [Theory]
    [InlineData("stranger")]
    [InlineData("every code")]
    public async Task NonMember_LearnsNothing_OnEveryRoute_WhateverTheTeam(string who)
    {
        var cast = await CreateAsync(factory);
        var story = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var token = who == "stranger"
            ? OrganizationsTokens.Stranger()
            : KeycloakTestRealm.CreateToken([.. PermissionCatalog.AllCodes], options => options.Subject = OrganizationsTokens.NewSubject());

        foreach (var route in Routes)
        {
            var answers = new[]
            {
                await SendAsync(route, cast.OrganizationId, cast.Mars, story.Id, token),
                await SendAsync(route, cast.OrganizationId, Guid.CreateVersion7(), story.Id, token),
                await SendAsync(route, cast.OrganizationId, cast.World.OtherOrganizationTeam, story.Id, token),
            };

            Assert.All(answers, answer => Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found", "Организация не найдена."), Shape(answer)));
        }
    }

    // US-7 · "The answer order": Vera reorders the backlog of an unknown team — 403 before any not-found; Max reads a level the
    // process lacks — «Уровень бэклога не найден.»; that level of an unknown team — «Команда не найдена.», the team first; a
    // malformed body — 400 and nothing changes; an item that left meanwhile — 409.
    [Fact]
    public async Task TheAnswerOrder_IsMod016s_ForTheTeamViewRoutes()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var b1 = await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        var vera = await MembershipTestData.NewMemberAsync(factory, cast.OrganizationId, OrganizationRole.Viewer);
        await OrderAsync(factory, cast, cast.Mars, a1, b1);
        await OwningTeamTestData.SetOwningTeamAsync(factory, b1.Id, cast.Jupiter);

        var viewerOnUnknownTeam = await SendAsync("reorder", cast.OrganizationId, Guid.CreateVersion7(), a1.Id, vera.Token);
        var levelOfKnownTeam = await GetBacklogAsync(factory, cast, cast.Mars, "tasks");
        var levelOfUnknownTeam = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"{TeamTestData.Team(cast.OrganizationId, Guid.CreateVersion7())}/backlogs/tasks", token: OrganizationsTokens.SignedIn());
        var malformed = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{Team(cast, cast.Mars)}/backlogs/stories/reorder", new { itemId = a1.Id.ToString(), position = "sideways" }, OrganizationsTokens.SignedIn());
        var empty = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{Team(cast, cast.Mars)}/backlogs/stories/reorder", new { }, OrganizationsTokens.SignedIn());
        var left = await ReorderAsync(factory, cast, cast.Mars, BoardsTestData.Top(b1));

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (viewerOnUnknownTeam.Status, viewerOnUnknownTeam.ErrorCode));
        BoardsTestData.AssertRefused(levelOfKnownTeam, HttpStatusCode.NotFound, "boards.level.not_found");
        Assert.Equal("Уровень бэклога не найден.", levelOfKnownTeam.Detail);
        BoardsTestData.AssertRefused(levelOfUnknownTeam, HttpStatusCode.NotFound, "organizations.team.not_found");
        BoardsTestData.AssertRefused(malformed, HttpStatusCode.BadRequest, "boards.validation_failed");
        Assert.Equal("boards.backlog.position_invalid", TeamTestData.ReasonOf(malformed, "position"));
        BoardsTestData.AssertRefused(empty, HttpStatusCode.BadRequest, "boards.validation_failed");
        BoardsTestData.AssertRefused(left, HttpStatusCode.Conflict, Stale);
        Assert.Equal([a1.Identifier], await RowsAsync(factory, cast, cast.Mars));
    }

    // US-7 · "Another organization's ids name nothing on every team-view route" (BR-TS-13; tech-011 m3): Orion's id as the team on
    // each route answers like an unknown id — identical in status, code and text; a filter naming Zeta's project selects nothing; a
    // reorder naming Zeta's item as the moved item and then as the anchor answers like an unknown item id (stale), and nothing changes
    // anywhere; Orion's own backlog in «Другая» is unchanged and no answer says whether Orion, Zeta or the item exist.
    [Fact]
    public async Task AnotherOrganizationsIds_NameNothing_OnEveryTeamViewRoute()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var foreign = await BoardsTestData.CreateWorldAsync(factory);
        var zeta = await BoardsTestData.CreateAsync(factory, foreign, "US", "Зета", foreign.PlatformAreaId);
        var orionBefore = await BoardsTestData.RowsAsync(factory, foreign, foreign.PlatformId);
        var placesBefore = await TeamPlacesOfAsync(factory, zeta.Id);

        foreach (var route in Routes)
        {
            var orion = await SendAsync(route, cast.OrganizationId, foreign.PlatformId, zeta.Id);
            var unknown = await SendAsync(route, cast.OrganizationId, Guid.CreateVersion7(), zeta.Id);

            Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found", "Команда не найдена."), Shape(orion));
            Assert.Equal(Shape(unknown), Shape(orion));
        }

        var backlogOfZeta = await GetBacklogAsync(factory, cast, cast.Mars, query: Filter(foreign.ProjectId));
        var boardOfZeta = await GetBoardAsync(factory, cast, cast.Mars, query: Filter(foreign.ProjectId));
        var movedZeta = await ReorderAsync(factory, cast, cast.Mars, BoardsTestData.Top(zeta));
        var anchoredZeta = await ReorderAsync(factory, cast, cast.Mars, BoardsTestData.After(a1, zeta));
        var movedUnknown = await ReorderAsync(factory, cast, cast.Mars, BoardsTestData.Top(new Item(Guid.CreateVersion7(), "X-US-1")));

        Assert.Empty(backlogOfZeta.Body.GetProperty("items").EnumerateArray());
        Assert.All(boardOfZeta.Body.GetProperty("columns").EnumerateArray(), column => Assert.Empty(column.GetProperty("items").EnumerateArray()));
        Assert.Equal((HttpStatusCode.Conflict, Stale), (movedZeta.Status, movedZeta.ErrorCode));
        Assert.Equal(Shape(movedUnknown), Shape(movedZeta));
        Assert.Equal(Shape(movedUnknown), Shape(anchoredZeta));
        Assert.Equal([a1.Identifier], await RowsAsync(factory, cast, cast.Mars));
        Assert.Equal(orionBefore, await BoardsTestData.RowsAsync(factory, foreign, foreign.PlatformId));
        Assert.Equal(placesBefore, await TeamPlacesOfAsync(factory, zeta.Id));
    }

    // US-7 · "The project views keep their boundary" (feat-039 kept): Альфа's backlog, board and reorder with Zeta's id as the project
    // each answer «Проект не найден.», identical to an unknown project id; a team filter of Orion's id on Альфа's backlog selects nothing.
    [Fact]
    public async Task TheProjectViews_KeepTheirBoundary()
    {
        var cast = await CreateAsync(factory);
        await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var foreign = await BoardsTestData.CreateWorldAsync(factory);

        foreach (var (method, suffix, body) in new (HttpMethod, string, object?)[]
        {
            (HttpMethod.Get, "backlogs/stories", null),
            (HttpMethod.Get, "boards/stories", null),
            (HttpMethod.Post, "backlogs/stories/reorder", new { itemId = Guid.CreateVersion7().ToString(), position = "top" }),
        })
        {
            var zeta = await OrganizationsApi.SendAsync(
                factory, method, $"{OrganizationsApi.Project(cast.OrganizationId, foreign.ProjectId)}/{suffix}", body, OrganizationsTokens.SignedIn());
            var unknown = await OrganizationsApi.SendAsync(
                factory, method, $"{OrganizationsApi.Project(cast.OrganizationId, Guid.CreateVersion7())}/{suffix}", body, OrganizationsTokens.SignedIn());

            Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found", "Проект не найден."), Shape(zeta));
            Assert.Equal(Shape(unknown), Shape(zeta));
        }

        var filtered = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"{OrganizationsApi.Project(cast.OrganizationId, cast.Alpha.ProjectId)}/backlogs/stories?teamId={foreign.PlatformId}",
            token: OrganizationsTokens.SignedIn());
        Assert.Empty(filtered.Body.GetProperty("items").EnumerateArray());
    }

    // US-7 · "The roles on a team's views" (spec § Roles): Ann, Max and Vera each read MARS's backlog and board and see the same
    // items; Vera's reorder, her state write and her creation in Бета are 403, nothing changes.
    [Fact]
    public async Task Roles_EveryMemberReadsTheSameItems_ViewerCannotWrite()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var b1 = await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        var ann = await MembershipTestData.NewMemberAsync(factory, cast.OrganizationId, OrganizationRole.Admin);
        var max = await MembershipTestData.NewMemberAsync(factory, cast.OrganizationId, OrganizationRole.Member);
        var vera = await MembershipTestData.NewMemberAsync(factory, cast.OrganizationId, OrganizationRole.Viewer);
        await OrderAsync(factory, cast, cast.Mars, a1, b1);

        foreach (var member in new[] { ann, max, vera })
        {
            Assert.Equal([a1.Identifier, b1.Identifier], await RowsAsync(factory, cast, cast.Mars, token: member.Token));
            Assert.Equal([a1.Identifier, b1.Identifier], await CardsAsync(factory, cast, cast.Mars, token: member.Token));
        }

        var reorder = await ReorderAsync(factory, cast, cast.Mars, BoardsTestData.Top(b1), token: vera.Token);
        var move = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Patch,
            WorkItemsTestData.Item(cast.Beta.Items, b1.Identifier),
            new { stateId = cast.Beta.Items.Types["US"].StateId("Активна").ToString() },
            vera.Token);
        var create = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(cast.Beta.Items),
            new { requestId = Guid.NewGuid().ToString(), typeId = cast.Beta.Items.Types["US"].Id.ToString(), title = "Новая", team = new { teamId = cast.Mars.ToString() } },
            vera.Token);

        Assert.All([reorder, move, create], answer => Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (answer.Status, answer.ErrorCode)));
        Assert.Equal([a1.Identifier, b1.Identifier], await RowsAsync(factory, cast, cast.Mars));
        Assert.Equal(2, (await WorkItemsTestData.StoredAsync(factory, cast.Beta.ProjectId)).Count + (await WorkItemsTestData.StoredAsync(factory, cast.Alpha.ProjectId)).Count);
    }

    // US-7 · "The route inventory holds the new routes and not the old ones" (BR-TS-1, -13; mod-016 BR-M1-33): the three team-addressed
    // routes are listed, each with its code, the three project-nested ones are not, and the count is unchanged (59).
    [Fact]
    public void TheRouteInventory_ListsTheThreeTeamAddressedRoutes_AndNotTheProjectNestedOnes()
    {
        const string Team = "/api/organizations/{organizationId}/teams/{teamId}";
        var routes = OrganizationAddressedRoutes.All.ToDictionary(route => route.Key, route => route.Code);

        Assert.Equal(PermissionCatalog.WorkItemsWorkItemRead, routes["GET " + Team + "/backlogs/{level}"]);
        Assert.Equal(PermissionCatalog.BoardsBacklogReorder, routes["POST " + Team + "/backlogs/{level}/reorder"]);
        Assert.Equal(PermissionCatalog.WorkItemsWorkItemRead, routes["GET " + Team + "/boards/{level}"]);
        Assert.DoesNotContain(
            OrganizationAddressedRoutes.All,
            route => route.Template.Contains("/projects/{projectId}/teams/{teamId}/backlogs", StringComparison.Ordinal)
                || route.Template.Contains("/projects/{projectId}/teams/{teamId}/boards", StringComparison.Ordinal));
        Assert.Equal(59, OrganizationAddressedRoutes.All.Count);
    }
}
