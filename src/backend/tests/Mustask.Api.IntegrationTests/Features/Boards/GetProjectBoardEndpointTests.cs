using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/boards/{level}</c> (feat-039 api-contract § 3.3): a
/// project's board of one level, with every card of every team in the process's columns, in the project's order — US-3. A
/// card move is mod-003's PATCH, unchanged; what this file asserts about it is that it leaves the project's order alone.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetProjectBoardEndpointTests(MustaskApiFactory factory)
{
    /// <summary>US-3's data: S1 and S3 in «Новая», S2 in «Активна»; S1 and S2 on «Мастаск»'s backlog, S3 on «Платформа»'s.</summary>
    private async Task<(BoardsWorld World, Item S1, Item S2, Item S3)> ProjectAsync()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var s1 = await BoardsTestData.CreateAsync(factory, world, "US", "Первая", world.RootAreaId);
        var s2 = await BoardsTestData.CreateAsync(factory, world, "US", "Вторая", world.RootAreaId);
        var s3 = await BoardsTestData.CreateAsync(factory, world, "US", "Третья", world.PlatformAreaId);
        Assert.Equal(HttpStatusCode.OK, (await BoardsTestData.SetStateAsync(factory, world, s2, "US", "Активна")).Status);

        return (world, s1, s2, s3);
    }

    private static string[] Ids(params Item[] items) => items.Select(item => item.Identifier).ToArray();

    // US-3 · "The project's board holds the backlog's items in the process's columns" (BR-MX-17, BR-MX-19, BR-MX-16): the four
    // columns of the seeded process, «Новая» holding S1 and S3, «Активна» S2, and nothing of another project; the cards of the
    // two teams' boards together are the project's cards, each once.
    [Fact]
    public async Task Get_HoldsTheBacklogsItems_InTheProcessesColumns_EachOnce()
    {
        var (world, s1, s2, s3) = await ProjectAsync();
        var other = await WorkItemsTestData.AddProjectAsync(factory, world.Items, "OTH", "Другой проект");
        Assert.Equal(HttpStatusCode.Created, (await WorkItemsTestData.CreateAsync(factory, other, "US", "Чужая история")).Status);

        var board = await ProjectBoardsTestData.GetBoardAsync(factory, world);

        Assert.Equal(HttpStatusCode.OK, board.Status);
        Assert.Equal(
            [$"Новая: {s1.Identifier} {s3.Identifier}", $"Активна: {s2.Identifier}", "Разрешена:", "Закрыта:"],
            BoardsTestData.Columns(board.Body));

        var cards = ProjectBoardsTestData.AllCards(board.Body);
        Assert.Equal(cards.Length, cards.Distinct().Count());

        var mars = await BoardsTestData.GetBoardAsync(factory, world, world.ProjectTeamId);
        var venus = await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId);
        Assert.Equal(
            cards.Order(StringComparer.Ordinal),
            ProjectBoardsTestData.AllCards(mars.Body).Concat(ProjectBoardsTestData.AllCards(venus.Body)).Order(StringComparer.Ordinal));
    }

    // US-3 · "The head of the project's board": the answer names the project and the level, and no team; the level's types carry the
    // initial states, as the team board's.
    [Fact]
    public async Task Get_AnswersTheProjectAndTheLevel_AndNoTeam()
    {
        var (world, _, _, _) = await ProjectAsync();

        var board = await ProjectBoardsTestData.GetBoardAsync(factory, world);

        var project = board.Body.GetProperty("project");
        Assert.Equal((world.ProjectId, "Мастаск"), (project.GetProperty("id").GetGuid(), project.GetProperty("name").GetString()));
        Assert.Equal("stories", board.Body.GetProperty("level").GetProperty("code").GetString());
        Assert.False(board.Body.TryGetProperty("team", out _));
        Assert.All(
            board.Body.GetProperty("columns").EnumerateArray(),
            column => Assert.Equal(2, column.GetProperty("targets").GetArrayLength()));
    }

    // US-3 · "A column lists its cards in the project's order" (mod-006 BR-M4-15, BR-MX-18): S3 moved to the top of the project's
    // backlog leads «Новая», then S1; a reorder of «Мастаск»'s own backlog does not change that.
    [Fact]
    public async Task Get_AColumnListsItsCardsInTheProjectsOrder_NotInATeamsOrder()
    {
        var (world, s1, s2, s3) = await ProjectAsync();
        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(s3));

        var before = await ProjectBoardsTestData.GetBoardAsync(factory, world);
        await BoardsTestData.MoveAsync(factory, world, world.ProjectTeamId, BoardsTestData.Top(s2));
        var after = await ProjectBoardsTestData.GetBoardAsync(factory, world);

        Assert.Equal(Ids(s3, s1), BoardsTestData.Cards(BoardsTestData.Column(before.Body, "Новая")));
        Assert.Equal(before.Body.GetRawText(), after.Body.GetRawText());
    }

    // US-3 · same, with no reorder at all: unplaced cards stand by creation time, then number (BR-PB-4).
    [Fact]
    public async Task Get_NeverReordered_CardsStandByCreationTimeThenNumber()
    {
        var (world, s1, _, s3) = await ProjectAsync();
        var ten = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, s1, ten.AddMinutes(1));
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, s3, ten);

        var board = await ProjectBoardsTestData.GetBoardAsync(factory, world);

        Assert.Equal(Ids(s3, s1), BoardsTestData.Cards(BoardsTestData.Column(board.Body, "Новая")));
    }

    // US-3 · "A card move writes only the item's state, and every view shows it" (BR-MX-16, BR-MX-18, mod-006 BR-M4-3): S1 to
    // «Активна» by mod-003's PATCH — still an item of the project, its project place unchanged, and the next read of the project's
    // and of «Мастаск»'s backlog and boards shows S1 once each, in «Активна».
    [Fact]
    public async Task CardMove_WritesOnlyTheState_TheProjectsOrderIsUnchanged_EveryViewShowsIt()
    {
        var (world, s1, s2, s3) = await ProjectAsync();
        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(s3));
        var placesBefore = await ProjectBoardsTestData.PlacesAsync(factory, world);

        var moved = await BoardsTestData.SetStateAsync(factory, world, s1, "US", "Активна");

        Assert.Equal(HttpStatusCode.OK, moved.Status);
        Assert.Equal(placesBefore, await ProjectBoardsTestData.PlacesAsync(factory, world));
        var project = await ProjectBoardsTestData.GetBoardAsync(factory, world);
        Assert.Equal(Ids(s3), BoardsTestData.Cards(BoardsTestData.Column(project.Body, "Новая")));
        Assert.Equal(Ids(s1, s2), BoardsTestData.Cards(BoardsTestData.Column(project.Body, "Активна")));
        var team = await BoardsTestData.GetBoardAsync(factory, world, world.ProjectTeamId);
        Assert.Equal(Ids(s1, s2), BoardsTestData.Cards(BoardsTestData.Column(team.Body, "Активна")));
        Assert.Equal(Ids(s3, s1, s2), await ProjectBoardsTestData.RowsAsync(factory, world));
        Assert.Contains(s1.Identifier, await BoardsTestData.RowsAsync(factory, world, world.ProjectTeamId));
    }

    // US-3 · "A card's checklist is the team board's" (BR-PB-6, feat-032 BR-C-1, -7): S1's two tasks, one on each team's surfaces,
    // are both in its checklist; ticking one writes that child's state only.
    [Fact]
    public async Task Get_ACardsChecklist_HoldsItsChildren_WhateverTheirTeam()
    {
        var (world, s1, _, _) = await ProjectAsync();
        var t1 = await BoardsTestData.CreateAsync(factory, world, "TASK", "Первая задача", world.RootAreaId);
        var t2 = await BoardsTestData.CreateAsync(factory, world, "TASK", "Вторая задача", world.PlatformAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, t1, s1);
        await LinksTestData.LinkAsync(factory, world.Items, t2, s1);

        var board = await ProjectBoardsTestData.GetBoardAsync(factory, world);
        var card = ProjectCard(board.Body, s1);

        Assert.Equal(Ids(t1, t2), card.GetProperty("checklist").EnumerateArray().Select(child => child.GetProperty("identifier").GetString()));

        var ticked = await BoardsTestData.SetStateAsync(factory, world, t2, "TASK", "Закрыта");
        Assert.Equal(HttpStatusCode.OK, ticked.Status);
        var after = ProjectCard((await ProjectBoardsTestData.GetBoardAsync(factory, world)).Body, s1);
        Assert.Equal(
            ["Новая", "Закрыта"],
            after.GetProperty("checklist").EnumerateArray().Select(child => child.GetProperty("state").GetProperty("name").GetString()));
    }

    // US-3 · "The board of a level holds that level" and the epics board maps its own types: an epic and a feature are on their
    // own boards and not on the stories'.
    [Fact]
    public async Task Get_EachLevelHoldsItsOwnItems()
    {
        var (world, s1, s2, s3) = await ProjectAsync();
        var epic = await BoardsTestData.CreateAsync(factory, world, "EPIC", "Эпик", world.PlatformAreaId);
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича");

        var epics = await ProjectBoardsTestData.GetBoardAsync(factory, world, "epics");
        var features = await ProjectBoardsTestData.GetBoardAsync(factory, world, "features");
        var stories = await ProjectBoardsTestData.GetBoardAsync(factory, world);

        Assert.Equal(Ids(epic), ProjectBoardsTestData.AllCards(epics.Body));
        Assert.Equal(Ids(feature), ProjectBoardsTestData.AllCards(features.Body));
        Assert.Equal(Ids(s1, s2, s3).Order(StringComparer.Ordinal), ProjectBoardsTestData.AllCards(stories.Body).Order(StringComparer.Ordinal));
    }

    // US-3 · "An empty board": the process's columns, none with a card.
    [Fact]
    public async Task Get_NoItemAtTheLevel_IsTheColumnsWithNoCard()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);

        var board = await ProjectBoardsTestData.GetBoardAsync(factory, world);

        Assert.Equal(HttpStatusCode.OK, board.Status);
        Assert.Equal(["Новая:", "Активна:", "Разрешена:", "Закрыта:"], BoardsTestData.Columns(board.Body));
    }

    // US-3 · "Who moves cards" (BR-PB-5): a viewer reads the same board; a direct write of a state by a viewer answers 403 and
    // changes nothing; a member's move takes effect; an outsider gets the organization's not-found.
    [Fact]
    public async Task Get_ByRole_ViewerReads_ButCannotWriteAState_OutsiderIsRefused()
    {
        var (world, s1, _, _) = await ProjectAsync();
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);

        var asViewer = await ProjectBoardsTestData.GetBoardAsync(factory, world, token: viewer.Token);
        var asMember = await ProjectBoardsTestData.GetBoardAsync(factory, world, token: member.Token);
        var asOutsider = await ProjectBoardsTestData.GetBoardAsync(factory, world, token: MembershipTestData.Outsider());
        var write = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Patch,
            WorkItemsTestData.Item(world.Items, s1.Identifier),
            new { stateId = world.Items.Types["US"].StateId("Активна").ToString() },
            viewer.Token);

        Assert.Equal(HttpStatusCode.OK, asViewer.Status);
        Assert.Equal(asMember.Body.GetRawText(), asViewer.Body.GetRawText());
        BoardsTestData.AssertRefused(asOutsider, HttpStatusCode.NotFound, "organizations.organization.not_found");
        BoardsTestData.AssertRefused(write, HttpStatusCode.Forbidden, "organizations.access.forbidden");
        Assert.Equal("Недостаточно прав", write.Detail);
        var board = await ProjectBoardsTestData.GetBoardAsync(factory, world);
        Assert.Contains(s1.Identifier, BoardsTestData.Cards(BoardsTestData.Column(board.Body, "Новая")));
    }

    // US-3 · "The same missing addresses as the backlog's" (BR-MX-1): the project before the level; no team can be missing.
    [Theory]
    [InlineData("organization-unknown", "organizations.organization.not_found", "Организация не найдена.")]
    [InlineData("project-malformed", "organizations.project.not_found", "Проект не найден.")]
    [InlineData("project-unknown", "organizations.project.not_found", "Проект не найден.")]
    [InlineData("project-of-another-organization", "organizations.project.not_found", "Проект не найден.")]
    [InlineData("project-deactivated", "organizations.project.not_found", "Проект не найден.")]
    [InlineData("project-and-level-both-wrong", "organizations.project.not_found", "Проект не найден.")]
    [InlineData("level-tasks", "boards.level.not_found", "Уровень бэклога не найден.")]
    [InlineData("level-sprints", "boards.level.not_found", "Уровень бэклога не найден.")]
    public async Task Get_ContextNotFound_AnswersItsLevelsCode(string part, string code, string detail)
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            await GetProjectBacklogEndpointTests.NotFoundAddressAsync(factory, world, part, "boards"),
            token: OrganizationsTokens.SignedIn());

        BoardsTestData.AssertRefused(answer, HttpStatusCode.NotFound, code);
        Assert.Equal(detail, answer.Detail);
    }

    private static System.Text.Json.JsonElement ProjectCard(System.Text.Json.JsonElement board, Item item) =>
        board.GetProperty("columns").EnumerateArray()
            .SelectMany(column => column.GetProperty("items").EnumerateArray())
            .Single(card => card.GetProperty("identifier").GetString() == item.Identifier);
}
