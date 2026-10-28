using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/{teamId}/backlogs/{level}</c>
/// (mod-006 api-contract § 3.1, moved to the team's address by feat-040 § 3.1): a team's backlog of one level — US-1, and
/// US-7's backlog scenarios, where a change made elsewhere moves items on and
/// off it at the next read.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetBacklogEndpointTests(MustaskApiFactory factory)
{
    // --- US-1 ------------------------------------------------------------------------------------------------------

    // mod-017 US-4 · "each team holds its own": the «Истории» backlog of «Платформа» holds the story and the bug the team
    // owns, the «Эпики» and «Фичи» backlogs the epic and the feature; the story «Мастаск» owns is on «Мастаск»'s backlog,
    // and the task is on no backlog of this cut. (The world puts an item in an area and gives it the team it meant by the
    // area, BoardsTestData.OwnerOf; the area decides nothing.)
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
        Assert.Equal([rootStory.Identifier], await BoardsTestData.RowsAsync(factory, world, world.ProjectTeamId));

        foreach (var team in new[] { world.PlatformId, world.ProjectTeamId })
        {
            foreach (var level in new[] { "epics", "features", "stories" })
            {
                Assert.DoesNotContain(task.Identifier, await BoardsTestData.RowsAsync(factory, world, team, level));
            }
        }
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

    // US-1 · "Items nobody has ordered yet are listed by number" (BR-M4-13): «Порядок» 1, 2, 3 by identifier number — rewritten by
    // feat-040 (BR-TS-6): a team's unplaced items stand by creation time, then project key, then number; within one project the
    // number grows with the creation time, so the order a user sees is the order they were made in. The cross-project tie-break is
    // TeamOrderAcrossProjectsEndpointTests'.
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
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world.Items, "Анна", "Смирнова", "anna@example.org");
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

        // mod-016 BR-M1-26 (api-contract § 3.3, US-5 "The reads say whether the assignee is still a member").
        Assert.True(assigned.GetProperty("isMember").GetBoolean());
    }

    // US-1 · "The level is chosen by tabs": the response names the team (mod-017 api-contract § 1.4: its id and name — a
    // team has no default area now) and the level with its types in display order and their initial state (the dialog's
    // picker and text, BR-M4-21).
    [Fact]
    public async Task Get_NamesTheTeamAndTheLevelWithItsTypes()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);

        var stories = (await BoardsTestData.GetBacklogAsync(factory, world, world.PlatformId)).Body;
        var epics = (await BoardsTestData.GetBacklogAsync(factory, world, world.ProjectTeamId, "epics")).Body;

        Assert.Equal((world.PlatformId, "Платформа"), (stories.GetProperty("team").GetProperty("id").GetGuid(), stories.GetProperty("team").GetProperty("name").GetString()));
        Assert.Equal(["id", "name"], stories.GetProperty("team").EnumerateObject().Select(member => member.Name));
        Assert.Equal(("stories", "Истории"), (stories.GetProperty("level").GetProperty("code").GetString(), stories.GetProperty("level").GetProperty("name").GetString()));
        Assert.Equal(
            ["US Пользовательская история story Новая", "BUG Баг bug Новая"],
            stories.GetProperty("level").GetProperty("types").EnumerateArray().Select(type =>
                $"{type.GetProperty("key").GetString()} {type.GetProperty("name").GetString()} {type.GetProperty("appearance").GetString()} "
                + type.GetProperty("initialState").GetProperty("name").GetString()));

        Assert.Equal((world.ProjectTeamId, "Мастаск"), (epics.GetProperty("team").GetProperty("id").GetGuid(), epics.GetProperty("team").GetProperty("name").GetString()));
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
    [InlineData("team-of-another-organization", "organizations.team.not_found", "Команда не найдена.")]
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

    /// <summary>
    /// The address of US-1's not-found outline, for <c>backlogs</c> or <c>boards</c>: the team's own, with no project in it
    /// (feat-040 BR-TS-1). A deactivated project is no case of the team's address (it has no project level, BR-TS-1); a team of
    /// <b>another organization</b> is the case that replaces mod-006's "team of another project" (BR-TS-13).
    /// </summary>
    internal static async Task<string> NotFoundAddressAsync(MustaskApiFactory factory, BoardsWorld world, string part, string surface)
    {
        string Address(object organization, object team, string level) =>
            $"/api/organizations/{organization}/teams/{team}/{surface}/{level}";

        switch (part)
        {
            case "team-malformed":
                return Address(world.OrganizationId, "not-a-team", "stories");
            case "team-deleted":
                var (deleted, _) = await BoardsTestData.AddTeamAsync(factory, world, "Удаляемая");
                await OrganizationsTestData.DeleteTeamAsync(factory, world.ProjectId, deleted);
                return Address(world.OrganizationId, deleted, "stories");
            case "team-of-another-organization":
                var other = await OrganizationsTestData.CreateOrganizationAsync(factory);
                var foreign = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, other.Id, "Другая команда");
                return Address(world.OrganizationId, foreign.Id, "stories");
            case "organization-unknown":
                return Address(Guid.CreateVersion7(), world.PlatformId, "stories");
            case "level-tasks":
                return Address(world.OrganizationId, world.PlatformId, "tasks");
            case "level-capitalized":
                return Address(world.OrganizationId, world.PlatformId, "Stories");
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
        await BoardsTestData.ReorderToAsync(factory, world, world.ProjectTeamId, us5, us7);

        return (world, us5, us7);
    }

    // mod-017 US-4 · "comes back unplaced" (BR-OT-6): an item given to «Мастаск» loses its place on «Платформа» in the same
    // write, whatever the reorder of «Мастаск» did meanwhile, and comes back to «Платформа» last, with no memory of it.
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
        await BoardsTestData.MoveAsync(factory, world, world.ProjectTeamId, BoardsTestData.Top(rootStory));
        await BoardsTestData.MoveToAreaAsync(factory, world, us9, world.PlatformAreaId);

        Assert.Equal([us3.Identifier, us11.Identifier, us9.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // mod-017 US-2 · "an item created from the list takes the project default team" (BR-OT-3): the world names «Мастаск» the
    // default, so the item is on its backlog, the last row, and in the root area.
    [Fact]
    public async Task Get_ItemCreatedFromTheList_IsTheFirstTeamsLastRow()
    {
        var (world, us5, us7) = await FirstTeamReorderedAsync();

        var answer = await WorkItemsTestData.CreateAsync(factory, world.Items, "BUG", "Из списка");

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        var bug = new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
        Assert.Equal("Мастаск", answer.Body.GetProperty("area").GetProperty("path").GetString());
        Assert.Equal([us5.Identifier, us7.Identifier, bug.Identifier], await BoardsTestData.RowsAsync(factory, world, world.ProjectTeamId));
    }
}
