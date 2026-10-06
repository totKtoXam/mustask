using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/backlogs/{level}</c>
/// (mod-006 api-contract § 3.1): a team's backlog of one level — US-1, and
/// US-7's backlog scenarios, where a change made elsewhere moves items on and
/// off it at the next read.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetBacklogEndpointTests(MustaskApiFactory factory)
{
    // --- US-1 ------------------------------------------------------------------------------------------------------

    // US-1 · "A team's backlog holds its own items of the level": the «Истории» backlog of «Платформа» holds its story and
    // its bug (the bug's area inherits from «Платформа»), the «Эпики» and «Фичи» backlogs the epic and the feature; the
    // root's story is on «Мастаск»'s backlog, and the task is on no backlog of this cut.
    [Fact]
    public async Task Get_HoldsTheTeamsItemsOfTheLevel_AndNothingElse()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var epic = await BoardsTestData.CreateAsync(factory, world, "EPIC", "Эпик", world.PlatformAreaId);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        var task = await BoardsTestData.CreateAsync(factory, world, "TASK", "Задача", world.PlatformAreaId);
        var bug = await BoardsTestData.CreateAsync(factory, world, "BUG", "Баг", world.ApiAreaId);
        var rootStory = await BoardsTestData.CreateAsync(factory, world, "US", "История в корне");
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);

        Assert.Equal([story.Identifier, bug.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
        Assert.Equal([epic.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId, "epics"));
        Assert.Equal([feature.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId, "features"));
        Assert.Equal([rootStory.Identifier], await BoardsTestData.RowsAsync(factory, world, world.FirstTeamId));

        foreach (var team in new[] { world.PlatformId, world.FirstTeamId })
        {
            foreach (var level in new[] { "epics", "features", "stories" })
            {
                Assert.DoesNotContain(task.Identifier, await BoardsTestData.RowsAsync(factory, world, team, level));
            }
        }
    }

    // US-1 · "An area with no owner of its own counts for its nearest owner": «Отчёты» inherits from the root.
    [Fact]
    public async Task Get_AreaWithoutOwner_CountsForTheNearestOwner()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Отчёт", world.ReportsAreaId);

        Assert.Equal([story.Identifier], await BoardsTestData.RowsAsync(factory, world, world.FirstTeamId));
        Assert.Empty(await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // US-1 · "Closed and resolved items stay on the backlog" (OQ-2): every state outside Removed.
    [Fact]
    public async Task Get_ClosedAndResolvedItems_StayOnTheBacklog()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Закрытая", world.PlatformAreaId);
        var bug = await BoardsTestData.CreateAsync(factory, world, "BUG", "Разрешённый", world.PlatformAreaId);
        await BoardsTestData.SetStateAsync(factory, world, story, "US", "Закрыта");
        await BoardsTestData.SetStateAsync(factory, world, bug, "BUG", "Разрешена");

        Assert.Equal([story.Identifier, bug.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // US-1 · "Items nobody has ordered yet are listed by number" (BR-M4-13): «Порядок» 1, 2, 3 by identifier number.
    [Fact]
    public async Task Get_NeverReordered_ListsByNumber()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var first = await BoardsTestData.CreateAsync(factory, world, "US", "Первая");
        var bug = await BoardsTestData.CreateAsync(factory, world, "BUG", "Баг");
        var last = await BoardsTestData.CreateAsync(factory, world, "US", "Последняя");

        // Moved into «Платформа» in reverse: the moves change nothing in the order, the numbers decide it.
        foreach (var item in new[] { last, bug, first })
        {
            await BoardsTestData.MoveToAreaAsync(factory, world, item, world.PlatformAreaId);
        }

        Assert.Equal([first.Identifier, bug.Identifier, last.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // US-1 · "A row shows the item at a glance and opens it" (BR-M4-14): «Порядок», id, identifier, type with its icon
    // token, title, «Не назначен», and the state with its category; and an assignee's display data.
    [Fact]
    public async Task Get_Row_CarriesWhatTheRowShows()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var bug = await BoardsTestData.CreateAsync(factory, world, "BUG", "Падение при входе", world.PlatformAreaId);
        await BoardsTestData.SetStateAsync(factory, world, bug, "BUG", "Активна");
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Назначенная", world.PlatformAreaId);
        var anna = await WorkItemsTestData.AddUserAsync(factory, "Анна", "Смирнова", "anna@example.org");
        await WorkItemsTestData.SaveAsync(factory, world.Items, story.Identifier, new { assignee = new { userId = anna } });

        var answer = await BoardsTestData.GetBacklogAsync(factory, world, world.PlatformId);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var row = answer.Body.GetProperty("items")[0];
        Assert.Equal(1, row.GetProperty("order").GetInt32());
        Assert.Equal(bug.Id, row.GetProperty("id").GetGuid());
        Assert.Equal(bug.Identifier, row.GetProperty("identifier").GetString());
        Assert.Equal(int.Parse(bug.Identifier.Split('-')[2]), row.GetProperty("number").GetInt32());
        Assert.Equal(("BUG", "Баг", "bug"), (
            row.GetProperty("type").GetProperty("key").GetString(),
            row.GetProperty("type").GetProperty("name").GetString(),
            row.GetProperty("type").GetProperty("appearance").GetString()));
        Assert.Equal("Падение при входе", row.GetProperty("title").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, row.GetProperty("assignee").ValueKind);
        Assert.Equal(("Активна", "InProgress", "В работе"), (
            row.GetProperty("state").GetProperty("name").GetString(),
            row.GetProperty("state").GetProperty("category").GetProperty("code").GetString(),
            row.GetProperty("state").GetProperty("category").GetProperty("label").GetString()));

        var assigned = answer.Body.GetProperty("items")[1].GetProperty("assignee");
        Assert.Equal((anna, "Анна Смирнова"), (assigned.GetProperty("id").GetString(), assigned.GetProperty("displayName").GetString()));
    }

    // US-1 · "The level is chosen by tabs": the response names the team with its default area (the dialog's text) and the
    // level with its types in display order and their initial state (the dialog's picker and text, BR-M4-21).
    [Fact]
    public async Task Get_NamesTheTeamAndTheLevelWithItsTypes()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);

        var stories = (await BoardsTestData.GetBacklogAsync(factory, world, world.PlatformId)).Body;
        var epics = (await BoardsTestData.GetBacklogAsync(factory, world, world.FirstTeamId, "epics")).Body;

        Assert.Equal((world.PlatformId, "Платформа"), (stories.GetProperty("team").GetProperty("id").GetGuid(), stories.GetProperty("team").GetProperty("name").GetString()));
        Assert.Equal((world.PlatformAreaId, "Платформа"), (
            stories.GetProperty("team").GetProperty("defaultArea").GetProperty("id").GetGuid(),
            stories.GetProperty("team").GetProperty("defaultArea").GetProperty("path").GetString()));
        Assert.Equal(("stories", "Истории"), (stories.GetProperty("level").GetProperty("code").GetString(), stories.GetProperty("level").GetProperty("name").GetString()));
        Assert.Equal(
            ["US Пользовательская история story Новая", "BUG Баг bug Новая"],
            stories.GetProperty("level").GetProperty("types").EnumerateArray().Select(type =>
                $"{type.GetProperty("key").GetString()} {type.GetProperty("name").GetString()} {type.GetProperty("appearance").GetString()} "
                + type.GetProperty("initialState").GetProperty("name").GetString()));

        Assert.Equal("Мастаск", epics.GetProperty("team").GetProperty("defaultArea").GetProperty("path").GetString());
        Assert.Equal(["EPIC"], epics.GetProperty("level").GetProperty("types").EnumerateArray().Select(type => type.GetProperty("key").GetString()));
    }

    // US-1 · "An empty backlog shows the empty frame": a team added today answers 200 with no rows.
    [Fact]
    public async Task Get_NewTeam_EmptyBacklog()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var (analytics, _) = await BoardsTestData.AddTeamAsync(factory, world, "Аналитика");

        var answer = await BoardsTestData.GetBacklogAsync(factory, world, analytics, "epics");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(0, answer.Body.GetProperty("items").GetArrayLength());
    }

    // US-1 · "An address whose context is not found", and the level of the address (api-contract § 0.3): `tasks` and any
    // other spelling answer the level's code.
    [Theory]
    [InlineData("team-malformed", "organizations.team.not_found", "Команда не найдена.")]
    [InlineData("team-deleted", "organizations.team.not_found", "Команда не найдена.")]
    [InlineData("team-of-another-project", "organizations.team.not_found", "Команда не найдена.")]
    [InlineData("project-deactivated", "organizations.project.not_found", "Проект не найден.")]
    [InlineData("organization-unknown", "organizations.organization.not_found", "Организация не найдена.")]
    [InlineData("level-tasks", "boards.level.not_found", "Уровень бэклога не найден.")]
    [InlineData("level-capitalized", "boards.level.not_found", "Уровень бэклога не найден.")]
    public async Task Get_ContextNotFound_AnswersItsLevelsCode(string part, string code, string detail)
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, await NotFoundAddressAsync(factory, world, part, "backlogs"), token: OrganizationsTokens.SignedIn());

        BoardsTestData.AssertRefused(answer, HttpStatusCode.NotFound, code);
        Assert.Equal(detail, answer.Detail);
    }

    /// <summary>The address of US-1's not-found outline, for <c>backlogs</c> or <c>boards</c>.</summary>
    internal static async Task<string> NotFoundAddressAsync(MustaskApiFactory factory, BoardsWorld world, string part, string surface)
    {
        string Address(object organization, object project, object team, string level) =>
            $"/api/organizations/{organization}/projects/{project}/teams/{team}/{surface}/{level}";

        switch (part)
        {
            case "team-malformed":
                return Address(world.OrganizationId, world.ProjectId, "not-a-team", "stories");
            case "team-deleted":
                var (deleted, _) = await BoardsTestData.AddTeamAsync(factory, world, "Удаляемая");
                await OrganizationsTestData.DeleteTeamAsync(factory, world.ProjectId, deleted);
                return Address(world.OrganizationId, world.ProjectId, deleted, "stories");
            case "team-of-another-project":
                var other = await WorkItemsTestData.AddProjectAsync(factory, world.Items, "OTH", "Другой проект");
                var foreign = await OrganizationsTestData.AddTeamAsync(factory, other.ProjectId, "Чужая");
                return Address(world.OrganizationId, world.ProjectId, foreign.Team.Id, "stories");
            case "project-deactivated":
                await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, world.ProjectId, ProjectStatus.Deactivated);
                return Address(world.OrganizationId, world.ProjectId, world.PlatformId, "stories");
            case "organization-unknown":
                return Address(Guid.CreateVersion7(), world.ProjectId, world.PlatformId, "stories");
            case "level-tasks":
                return Address(world.OrganizationId, world.ProjectId, world.PlatformId, "tasks");
            case "level-capitalized":
                return Address(world.OrganizationId, world.ProjectId, world.PlatformId, "Stories");
            default:
                throw new ArgumentOutOfRangeException(nameof(part), part, "Unknown part.");
        }
    }

    // --- US-7, the backlog's scenarios ----------------------------------------------------------------------------

    /// <summary>US-7's data: the «Истории» backlog of «Мастаск», reordered, reads its two root stories.</summary>
    private async Task<(BoardsWorld World, Item Us5, Item Us7)> FirstTeamReorderedAsync()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var us5 = await BoardsTestData.CreateAsync(factory, world, "US", "Пятая");
        var us7 = await BoardsTestData.CreateAsync(factory, world, "US", "Седьмая", world.ReportsAreaId);
        await BoardsTestData.ReorderToAsync(factory, world, world.FirstTeamId, us5, us7);

        return (world, us5, us7);
    }

    // US-7 · "A new Область moves the item to the other team's backlog": it lands below every placed item there, though its
    // number is the lowest (BR-M4-6, OQ-5).
    [Fact]
    public async Task Get_AreaSetOnTheForm_MovesTheItemToTheOtherTeam()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var us3 = await BoardsTestData.CreateAsync(factory, world, "US", "Третья", world.PlatformAreaId);
        var us5 = await BoardsTestData.CreateAsync(factory, world, "US", "Пятая");
        var us7 = await BoardsTestData.CreateAsync(factory, world, "US", "Седьмая");
        await BoardsTestData.ReorderToAsync(factory, world, world.FirstTeamId, us5, us7);

        await BoardsTestData.MoveToAreaAsync(factory, world, us3, world.ReportsAreaId);

        Assert.Empty(await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
        Assert.Equal([us5.Identifier, us7.Identifier, us3.Identifier], await BoardsTestData.RowsAsync(factory, world, world.FirstTeamId));
    }

    // US-7 · "An area given another owner takes its items along" (mod-002's owner endpoint).
    [Fact]
    public async Task Get_AreaReOwned_TakesItsItemsAlong()
    {
        var (world, us5, us7) = await FirstTeamReorderedAsync();
        var us = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.ApiAreaId);
        var bug = await BoardsTestData.CreateAsync(factory, world, "BUG", "Баг", world.ApiAreaId);
        Assert.Equal([us.Identifier, bug.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId));

        var owner = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Areas(world.OrganizationId, world.ProjectId)}/{world.ApiAreaId}/owner",
            new { ownerTeamId = world.FirstTeamId.ToString() },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.OK, owner.Status);

        Assert.Empty(await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
        Assert.Equal(
            [us5.Identifier, us7.Identifier, us.Identifier, bug.Identifier],
            await BoardsTestData.RowsAsync(factory, world, world.FirstTeamId));
    }

    // US-7 · "An area moved under another parent takes its items along" (mod-002's move endpoint).
    [Fact]
    public async Task Get_AreaMoved_TakesItsItemsAlong()
    {
        var (world, us5, us7) = await FirstTeamReorderedAsync();
        var us = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.ApiAreaId);

        var moved = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Areas(world.OrganizationId, world.ProjectId)}/{world.ApiAreaId}/parent",
            new { parentId = world.RootAreaId.ToString() },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.OK, moved.Status);

        Assert.Equal([us5.Identifier, us7.Identifier, us.Identifier], await BoardsTestData.RowsAsync(factory, world, world.FirstTeamId));
        Assert.Empty(await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // US-7 · "An item that comes back before anything was ordered returns": its place on «Платформа» was never dropped.
    [Fact]
    public async Task Get_ItemBackWithNoReorderMeanwhile_ReturnsToItsPlace()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var us3 = await BoardsTestData.CreateAsync(factory, world, "US", "Третья", world.PlatformAreaId);
        var us9 = await BoardsTestData.CreateAsync(factory, world, "US", "Девятая", world.PlatformAreaId);
        var us11 = await BoardsTestData.CreateAsync(factory, world, "US", "Одиннадцатая", world.PlatformAreaId);
        await BoardsTestData.ReorderToAsync(factory, world, world.PlatformId, us9, us3, us11);

        await BoardsTestData.MoveToAreaAsync(factory, world, us9, world.ReportsAreaId);
        Assert.Equal([us3.Identifier, us11.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
        await BoardsTestData.MoveToAreaAsync(factory, world, us9, world.PlatformAreaId);

        Assert.Equal([us9.Identifier, us3.Identifier, us11.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // US-7 · "An item placed elsewhere meanwhile comes back last": the reorder of «Мастаск» placed it there, so it lost its
    // place on «Платформа».
    [Fact]
    public async Task Get_ItemPlacedElsewhereMeanwhile_ComesBackLast()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var us3 = await BoardsTestData.CreateAsync(factory, world, "US", "Третья", world.PlatformAreaId);
        var us9 = await BoardsTestData.CreateAsync(factory, world, "US", "Девятая", world.PlatformAreaId);
        var us11 = await BoardsTestData.CreateAsync(factory, world, "US", "Одиннадцатая", world.PlatformAreaId);
        var rootStory = await BoardsTestData.CreateAsync(factory, world, "US", "В корне");
        await BoardsTestData.ReorderToAsync(factory, world, world.PlatformId, us9, us3, us11);

        await BoardsTestData.MoveToAreaAsync(factory, world, us9, world.ReportsAreaId);
        await BoardsTestData.MoveAsync(factory, world, world.FirstTeamId, BoardsTestData.Top(rootStory));
        await BoardsTestData.MoveToAreaAsync(factory, world, us9, world.PlatformAreaId);

        Assert.Equal([us3.Identifier, us11.Identifier, us9.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // US-7 · "A deleted team's items go to the first team, in creation order" (BR-M4-20): the deleted team's addresses
    // answer «Команда не найдена», and its items come onto «Мастаск»'s backlog unplaced, by number.
    [Fact]
    public async Task Get_DeletedTeam_ItsItemsGoToTheFirstTeamByNumber()
    {
        var (world, us5, us7) = await FirstTeamReorderedAsync();
        var (analytics, analyticsArea) = await BoardsTestData.AddTeamAsync(factory, world, "Аналитика");
        var us21 = await BoardsTestData.CreateAsync(factory, world, "US", "Двадцать первая", analyticsArea);
        var us30 = await BoardsTestData.CreateAsync(factory, world, "US", "Тридцатая", analyticsArea);
        await BoardsTestData.ReorderToAsync(factory, world, analytics, us30, us21);

        var deleted = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Delete, BoardsTestData.Team(world, analytics), token: OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.NoContent, deleted.Status);

        BoardsTestData.AssertRefused(await BoardsTestData.GetBacklogAsync(factory, world, analytics), HttpStatusCode.NotFound, "organizations.team.not_found");
        BoardsTestData.AssertRefused(await BoardsTestData.GetBoardAsync(factory, world, analytics), HttpStatusCode.NotFound, "organizations.team.not_found");
        Assert.Equal(
            [us5.Identifier, us7.Identifier, us21.Identifier, us30.Identifier],
            await BoardsTestData.RowsAsync(factory, world, world.FirstTeamId));
    }

    // US-7 · "An item created from the project's list goes to the first team": no team, the root area, the last row.
    [Fact]
    public async Task Get_ItemCreatedFromTheList_IsTheFirstTeamsLastRow()
    {
        var (world, us5, us7) = await FirstTeamReorderedAsync();

        var bug = await BoardsTestData.CreateAsync(factory, world, "BUG", "Из списка");

        Assert.Equal([us5.Identifier, us7.Identifier, bug.Identifier], await BoardsTestData.RowsAsync(factory, world, world.FirstTeamId));
    }
}
