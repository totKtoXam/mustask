using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using static Mustask.Api.IntegrationTests.Features.Boards.TeamAcrossProjectsTestData;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/{teamId}/backlogs/{level}</c> and <c>…/boards/{level}</c> (feat-040
/// api-contract § 1, § 2, § 3.1, § 3.3; US-1): a team's views hold the items it owns in every active project of its
/// organization, at an address that names the team alone; each row and card carries its «Проект»; a deactivated project's items
/// leave and come back with their places; a missing, foreign or empty team answers alike. Every test builds its own organization
/// (<see cref="TeamAcrossProjectsTestData.CreateAsync"/>): MARS in Альфа, Бета and Гамма, VENUS in Альфа alone.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class TeamBacklogAcrossProjectsEndpointTests(MustaskApiFactory factory)
{
    // US-1 · "A team's views hold its items of every project" (BR-TS-2; doc-012 US-7): MARS owns stories in all three projects,
    // VENUS one in Альфа, and one Альфа story has no owner; the backlog and the board each hold MARS's seven and nothing else.
    [Fact]
    public async Task Get_HoldsTheTeamsItemsOfEveryProject_AndNoOtherTeamsNorUnowned_OnTheBacklogAndTheBoard()
    {
        var cast = await CreateAsync(factory);
        var items = new[]
        {
            await StoryAsync(factory, cast.Alpha, "A1", cast.Mars), await StoryAsync(factory, cast.Alpha, "A2", cast.Mars),
            await StoryAsync(factory, cast.Beta, "B1", cast.Mars), await StoryAsync(factory, cast.Beta, "B2", cast.Mars), await StoryAsync(factory, cast.Beta, "B3", cast.Mars),
            await StoryAsync(factory, cast.Gamma, "G1", cast.Mars), await StoryAsync(factory, cast.Gamma, "G2", cast.Mars),
        };
        await StoryAsync(factory, cast.Alpha, "Чужая", cast.Venus);
        await StoryAsync(factory, cast.Alpha, "Ничья", null);

        var rows = await RowsAsync(factory, cast, cast.Mars);
        var cards = await CardsAsync(factory, cast, cast.Mars);

        var expected = items.Select(item => item.Identifier).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(7, rows.Length);
        Assert.Equal(expected, rows.Order(StringComparer.Ordinal));
        Assert.Equal(expected, cards.Order(StringComparer.Ordinal));
        Assert.Equal(rows.Length, cards.Length);
    }

    // US-1 · "The address names the organization, the team and the level, and no project" (BR-TS-1): the answer names the team and
    // its linked active projects by name — Альфа, Бета, Гамма — and neither Дельта (linked to no team) nor a project of another
    // organization; whatever project the page was opened from, the same items.
    [Fact]
    public async Task Get_NamesTheTeamAndItsProjects_ByName_NotDeltaNorAnotherOrganizations()
    {
        var cast = await CreateAsync(factory);
        await StoryAsync(factory, cast.Beta, "B1", cast.Mars);

        var backlog = await GetBacklogAsync(factory, cast, cast.Mars);
        var board = await GetBoardAsync(factory, cast, cast.Mars);

        Assert.Equal(HttpStatusCode.OK, backlog.Status);
        Assert.Equal((cast.Mars, "MARS"), (backlog.Body.GetProperty("team").GetProperty("id").GetGuid(), backlog.Body.GetProperty("team").GetProperty("name").GetString()));
        Assert.Equal(["id", "name"], backlog.Body.GetProperty("team").EnumerateObject().Select(member => member.Name));
        string[] expected = [Wire(cast.Alpha, "Альфа"), Wire(cast.Beta, "Бета"), Wire(cast.Gamma, "Гамма")];
        Assert.Equal(expected, ProjectsOf(backlog));
        Assert.Equal(expected, ProjectsOf(board));
        Assert.Equal(["team", "projects", "level", "items"], backlog.Body.EnumerateObject().Select(member => member.Name));
        Assert.Equal(["team", "projects", "level", "columns"], board.Body.EnumerateObject().Select(member => member.Name));
    }

    // US-1 · "A team that works on one project" (BR-TS-8): VENUS's `projects` offers Альфа alone.
    [Fact]
    public async Task Get_ProjectsOfATeamLinkedToOneProject_IsThatProjectAlone()
    {
        var cast = await CreateAsync(factory);

        var backlog = await GetBacklogAsync(factory, cast, cast.Venus);
        var board = await GetBoardAsync(factory, cast, cast.Venus);

        Assert.Equal([Wire(cast.Alpha, "Альфа")], ProjectsOf(backlog));
        Assert.Equal([Wire(cast.Alpha, "Альфа")], ProjectsOf(board));
    }

    // US-1 · "…/backlogs/epics and …/features show the epics and features MARS owns in the three projects", "tasks" is no level:
    // each level holds the items of its types alone, across projects (BR-TS-1, BR-TS-2).
    [Fact]
    public async Task Get_Levels_HoldTheEpicsFeaturesAndStoriesOfTheTeam_AcrossProjects_TasksIsNotALevel()
    {
        var cast = await CreateAsync(factory);
        var epicInBeta = await CreateAsync(factory, cast.Beta, "Эпик Б", cast.Mars, "EPIC");
        var epicInGamma = await CreateAsync(factory, cast.Gamma, "Эпик Г", cast.Mars, "EPIC");
        var featureInAlpha = await CreateAsync(factory, cast.Alpha, "Фича А", cast.Mars, "FEAT");
        var story = await StoryAsync(factory, cast.Alpha, "История", cast.Mars);
        await CreateAsync(factory, cast.Alpha, "Эпик Венеры", cast.Venus, "EPIC");

        var epics = await RowsAsync(factory, cast, cast.Mars, "epics");
        var features = await RowsAsync(factory, cast, cast.Mars, "features");
        var stories = await RowsAsync(factory, cast, cast.Mars);
        var tasks = await GetBacklogAsync(factory, cast, cast.Mars, "tasks");
        var tasksBoard = await GetBoardAsync(factory, cast, cast.Mars, "tasks");

        Assert.Equal(new[] { epicInBeta.Identifier, epicInGamma.Identifier }.Order(StringComparer.Ordinal), epics.Order(StringComparer.Ordinal));
        Assert.Equal([featureInAlpha.Identifier], features);
        Assert.Equal([story.Identifier], stories);
        BoardsTestData.AssertRefused(tasks, HttpStatusCode.NotFound, "boards.level.not_found");
        BoardsTestData.AssertRefused(tasksBoard, HttpStatusCode.NotFound, "boards.level.not_found");
    }

    // US-1 · "A view holds its level's items in every state but Removed" (BR-TS-2): a story in «Закрыта» is shown, a deleted one is
    // not, and an epic is not on the stories level.
    [Fact]
    public async Task Get_HoldsEveryStateButRemoved()
    {
        var cast = await CreateAsync(factory);
        var closed = await StoryAsync(factory, cast.Beta, "Закрытая", cast.Mars);
        var deleted = await StoryAsync(factory, cast.Beta, "Удалённая", cast.Mars);
        var epic = await CreateAsync(factory, cast.Beta, "Эпик", cast.Mars, "EPIC");
        Assert.Equal(HttpStatusCode.OK, (await BoardsTestData.SetStateAsync(factory, cast.Beta, closed, "US", "Закрыта")).Status);
        await LinksTestData.DeleteAsync(factory, cast.Beta.Items, deleted);

        var rows = await RowsAsync(factory, cast, cast.Mars);
        var cards = await CardsAsync(factory, cast, cast.Mars);

        Assert.Equal([closed.Identifier], rows);
        Assert.Equal([closed.Identifier], cards);
        Assert.DoesNotContain(epic.Identifier, rows);
    }

    // US-1 · "Children beneath a row are the team's own" (BR-TS-2; mod-017 BR-OT-5 kept): a task of MARS stands beneath its story on
    // MARS's backlog, a task of VENUS does not; Альфа's own backlog shows both.
    [Fact]
    public async Task Get_ChildrenBeneathARow_AreTheTeamsOwn_ProjectViewsShowBoth()
    {
        var cast = await CreateAsync(factory);
        var story = await StoryAsync(factory, cast.Alpha, "История", cast.Mars);
        var marsTask = await CreateAsync(factory, cast.Alpha, "Задача Марса", cast.Mars, "TASK", story.Id.ToString());
        var venusTask = await CreateAsync(factory, cast.Alpha, "Задача Венеры", cast.Mars, "TASK", story.Id.ToString());

        // A child takes its parent's team at its creation (mod-017 BR-OT-3); VENUS comes to own it by a change of owner.
        await OwningTeamTestData.SetOwningTeamAsync(factory, venusTask.Id, cast.Venus);

        var team = await GetBacklogAsync(factory, cast, cast.Mars);
        var project = await ProjectBoardsTestData.GetBacklogAsync(factory, cast.Alpha);

        Assert.Equal(
            [marsTask.Identifier],
            BacklogTreeTestData.Row(team.Body.GetProperty("items").EnumerateArray(), story.Identifier).GetProperty("children").EnumerateArray()
                .Select(child => child.GetProperty("identifier").GetString()));
        Assert.Equal(
            new[] { marsTask.Identifier, venusTask.Identifier }.Order(StringComparer.Ordinal),
            BacklogTreeTestData.Row(project.Body.GetProperty("items").EnumerateArray(), story.Identifier).GetProperty("children").EnumerateArray()
                .Select(child => child.GetProperty("identifier").GetString()!).Order(StringComparer.Ordinal));
    }

    // US-1 · "Rows and cards show their project and their owning team" (BR-TS-3): S (MARS's, in Бета) and N (Бета's, no team) on the
    // four views — MARS's backlog and board, Бета's backlog and board — each carrying `project` {id, name} right after `team`,
    // and a nested child its own project too.
    [Fact]
    public async Task Get_RowsAndCards_CarryTheirProjectAndTeam_OnTheFourViews()
    {
        var cast = await CreateAsync(factory);
        var owned = await StoryAsync(factory, cast.Beta, "Марсова", cast.Mars);
        var unowned = await StoryAsync(factory, cast.Beta, "Ничья", null);
        var child = await CreateAsync(factory, cast.Beta, "Дочерняя", cast.Mars, "TASK", owned.Id.ToString());
        var beta = Wire(cast.Beta, "Бета");

        var teamBacklog = await GetBacklogAsync(factory, cast, cast.Mars);
        var teamBoard = await GetBoardAsync(factory, cast, cast.Mars);
        var projectBacklog = await ProjectBoardsTestData.GetBacklogAsync(factory, cast.Beta);
        var projectBoard = await ProjectBoardsTestData.GetBoardAsync(factory, cast.Beta);

        var row = Row(teamBacklog, owned.Identifier);
        Assert.Equal(beta, ProjectOf(row));
        Assert.Equal("MARS", row.GetProperty("team").GetProperty("name").GetString());
        Assert.Equal(["id", "name"], row.GetProperty("project").EnumerateObject().Select(member => member.Name));
        Assert.Equal(beta, ProjectOf(Row(teamBacklog, child.Identifier)));
        Assert.Equal(beta, ProjectOf(CardOf(teamBoard, owned.Identifier)));
        Assert.Equal(beta, ProjectOf(Row(projectBacklog, owned.Identifier)));
        Assert.Equal(beta, ProjectOf(CardOf(projectBoard, owned.Identifier)));
        Assert.Equal("MARS", CardOf(projectBoard, owned.Identifier).GetProperty("team").GetProperty("name").GetString());
        Assert.Equal(beta, ProjectOf(Row(projectBacklog, unowned.Identifier)));
        Assert.Equal(beta, ProjectOf(CardOf(projectBoard, unowned.Identifier)));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, Row(projectBacklog, unowned.Identifier).GetProperty("team").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, CardOf(projectBoard, unowned.Identifier).GetProperty("team").ValueKind);
    }

    // The project name is the current one (api-contract § 2.1): a rename shows at once on a row and a card.
    [Fact]
    public async Task Get_ProjectOnARow_IsItsCurrentName()
    {
        var cast = await CreateAsync(factory);
        var story = await StoryAsync(factory, cast.Gamma, "История", cast.Mars);

        var renamed = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Project(cast.OrganizationId, cast.Gamma.ProjectId)}/name",
            new { name = "Гамма Плюс" },
            OrganizationsTokens.SignedIn());
        var backlog = await GetBacklogAsync(factory, cast, cast.Mars);
        var board = await GetBoardAsync(factory, cast, cast.Mars);

        Assert.Equal(HttpStatusCode.OK, renamed.Status);
        Assert.Equal(Wire(cast.Gamma, "Гамма Плюс"), ProjectOf(Row(backlog, story.Identifier)));
        Assert.Equal(Wire(cast.Gamma, "Гамма Плюс"), ProjectOf(CardOf(board, story.Identifier)));
        Assert.Contains(Wire(cast.Gamma, "Гамма Плюс"), ProjectsOf(backlog));
    }

    // US-1 · "A project's views still hold every item of the project and no other" (feat-039 BR-PB-2 kept): Альфа's backlog and board
    // hold its four stories whatever team owns them, none of Бета's or Гаммы's.
    [Fact]
    public async Task ProjectViews_StillHoldEveryItemOfTheProjectAndNoOther()
    {
        var cast = await CreateAsync(factory);
        var alpha = new[]
        {
            await StoryAsync(factory, cast.Alpha, "A1", cast.Mars), await StoryAsync(factory, cast.Alpha, "A2", cast.Mars),
            await StoryAsync(factory, cast.Alpha, "A3", cast.Venus), await StoryAsync(factory, cast.Alpha, "A4", null),
        };
        await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        await StoryAsync(factory, cast.Gamma, "G1", cast.Mars);

        var rows = await ProjectBoardsTestData.RowsAsync(factory, cast.Alpha);
        var cards = ProjectBoardsTestData.AllCards((await ProjectBoardsTestData.GetBoardAsync(factory, cast.Alpha)).Body);

        var expected = alpha.Select(item => item.Identifier).Order(StringComparer.Ordinal);
        Assert.Equal(expected, rows.Order(StringComparer.Ordinal));
        Assert.Equal(expected, cards.Order(StringComparer.Ordinal));
    }

    // US-1 · "A deactivated project's items leave the team's views and come back with their places" (BR-TS-2; ADR-0028 rule 9):
    // MARS's backlog ordered across the three projects; Гамма deactivated — Гамма's items and Гамма itself leave the views and
    // the options; restored — they return at their former places.
    [Fact]
    public async Task Get_ADeactivatedProjectsItemsLeave_AndComeBackWithTheirPlaces()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var b1 = await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        var g1 = await StoryAsync(factory, cast.Gamma, "G1", cast.Mars);
        var g2 = await StoryAsync(factory, cast.Gamma, "G2", cast.Mars);
        await OrderAsync(factory, cast, cast.Mars, g1, a1, g2, b1);
        Assert.Equal([g1.Identifier, a1.Identifier, g2.Identifier, b1.Identifier], await RowsAsync(factory, cast, cast.Mars));

        await DeactivateAsync(factory, cast, cast.Gamma);
        var whileDeactivated = await GetBacklogAsync(factory, cast, cast.Mars);
        var boardWhile = await CardsAsync(factory, cast, cast.Mars);
        await RestoreAsync(factory, cast, cast.Gamma);
        var restored = await RowsAsync(factory, cast, cast.Mars);

        Assert.Equal(
            [a1.Identifier, b1.Identifier],
            whileDeactivated.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()));
        Assert.Equal([a1.Identifier, b1.Identifier], boardWhile.Order(StringComparer.Ordinal));
        Assert.Equal([Wire(cast.Alpha, "Альфа"), Wire(cast.Beta, "Бета")], ProjectsOf(whileDeactivated));
        Assert.Equal([g1.Identifier, a1.Identifier, g2.Identifier, b1.Identifier], restored);
    }

    // US-1 · "A missing, foreign or empty team": a deleted team, a team of another organization, an unknown id and "not-a-guid" each
    // answer «Команда не найдена.» — alike in status, code and text, on the backlog and the board (BR-TS-2, BR-TS-13).
    [Fact]
    public async Task Get_DeletedForeignUnknownAndMalformedTeam_AnswerAlike()
    {
        var cast = await CreateAsync(factory);
        var addresses = new object[] { cast.World.DeletedTeam, cast.World.OtherOrganizationTeam, Guid.CreateVersion7(), "not-a-guid" };

        foreach (var surface in new[] { "backlogs", "boards" })
        {
            var answers = new List<Answer>();

            foreach (var team in addresses)
            {
                answers.Add(await OrganizationsApi.SendAsync(
                    factory,
                    HttpMethod.Get,
                    $"/api/organizations/{cast.OrganizationId}/teams/{team}/{surface}/stories",
                    token: OrganizationsTokens.SignedIn()));
            }

            Assert.All(answers, answer => BoardsTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.team.not_found"));
            Assert.Single(answers.Select(answer => answer.Detail).Distinct());
            Assert.Equal("Команда не найдена.", answers[0].Detail);
        }
    }

    // US-1 · "JUPITER's views show no item and offer no create command": a team linked to no project reads 200 with no item and an
    // empty `projects`; a team that owns nothing (VENUS) shows no item but still has its project; a team whose only project is
    // deactivated reads as the unlinked one does — and nothing in the answer names the project or the deactivation (ADR-0026 rule 5).
    [Fact]
    public async Task Get_ATeamLinkedToNoProject_IsEmpty_AndATeamWhoseProjectsAreAllDeactivatedReadsTheSame()
    {
        var cast = await CreateAsync(factory);

        var lonelyBacklog = await GetBacklogAsync(factory, cast, cast.Lonely);
        var lonelyBoard = await GetBoardAsync(factory, cast, cast.Lonely);
        var venusEmpty = await GetBacklogAsync(factory, cast, cast.Venus);
        await StoryAsync(factory, cast.Alpha, "Венерина", cast.Venus);
        await DeactivateAsync(factory, cast, cast.Alpha);
        var venusBacklog = await GetBacklogAsync(factory, cast, cast.Venus);
        var venusBoard = await GetBoardAsync(factory, cast, cast.Venus);

        foreach (var answer in new[] { lonelyBacklog, venusBacklog })
        {
            Assert.Equal(HttpStatusCode.OK, answer.Status);
            Assert.Empty(answer.Body.GetProperty("items").EnumerateArray());
            Assert.Empty(answer.Body.GetProperty("projects").EnumerateArray());
        }

        foreach (var answer in new[] { lonelyBoard, venusBoard })
        {
            Assert.Equal(HttpStatusCode.OK, answer.Status);
            Assert.All(answer.Body.GetProperty("columns").EnumerateArray(), column => Assert.Empty(column.GetProperty("items").EnumerateArray()));
            Assert.Empty(answer.Body.GetProperty("projects").EnumerateArray());
        }

        Assert.Equal([Wire(cast.Alpha, "Альфа")], ProjectsOf(venusEmpty));
        Assert.Empty(venusEmpty.Body.GetProperty("items").EnumerateArray());
        Assert.DoesNotContain("Альфа", venusBacklog.Body.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("Венерина", venusBacklog.Body.GetRawText(), StringComparison.Ordinal);
    }

    private static System.Text.Json.JsonElement CardOf(Answer board, string identifier) =>
        board.Body.GetProperty("columns").EnumerateArray()
            .SelectMany(column => column.GetProperty("items").EnumerateArray())
            .Single(card => card.GetProperty("identifier").GetString() == identifier);
}
