using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Infrastructure;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// QA's probes of mod-017 (qa.md, Backend): the places where the contract states a rule that no other test pins — the JSON
/// shape of a team in a body (api-contract § 0.2), the team routes that read no filter (§ 2.5), the literal <c>none</c> and the
/// mixed values of the filter (§ 3), and a deleted team's items under the filter (BR-OT-8 with BR-OT-12). Every test builds its
/// own world (<see cref="OwningTeamTestData"/>): the database is shared and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class OwningTeamQaEndpointTests(MustaskApiFactory factory)
{
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

    private Task<Answer> GetAsync(string path) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: OrganizationsTokens.SignedIn());

    private string Project(OwningTeamWorld world) => OrganizationsApi.Project(world.OrganizationId, world.ProjectId);

    private string TeamBacklog(OwningTeamWorld world, Guid teamId) => $"{OrganizationsApi.Teams(world.OrganizationId, world.ProjectId)}/{teamId}/backlogs/stories";

    private string TeamBoard(OwningTeamWorld world, Guid teamId) => $"{OrganizationsApi.Teams(world.OrganizationId, world.ProjectId)}/{teamId}/boards/stories";

    private static string[] Rows(Answer answer) =>
        answer.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()!).ToArray();

    private static string[] ListRows(Answer answer) =>
        answer.Body.GetProperty("data").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()!).ToArray();

    private static string Query(params string[] values) => "teamId=" + string.Join("&teamId=", values.Select(Uri.EscapeDataString));

    // api-contract § 0.2 · "a body field that holds an id is a JSON string or null; a number or an object there fails binding with
    // 400 http.bad_request": a save, a creation and the default naming that carry a team of the wrong JSON shape store nothing and
    // consume no number.
    [Fact]
    public async Task Bodies_WithATeamOfTheWrongJsonShape_Return400BadRequest_StoreNothing_ConsumeNoNumber()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world, "Вход по ссылке", world.Mars);
        var before = await TeamStateAsync(factory, world.ProjectId);
        var storedBefore = await StoredAsync(factory, world.ProjectId, story.Identifier);
        var item = WorkItemsTestData.Item(world.Items, story.Identifier);
        var defaultTeam = $"{Project(world)}/default-team";

        var saveNumber = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Patch, item, new { title = "Другое", team = new { teamId = 42 } }, OrganizationsTokens.SignedIn());
        var saveString = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Patch, item, new { title = "Другое", team = world.Venus.ToString() }, OrganizationsTokens.SignedIn());
        var createNumber = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new { requestId = Guid.NewGuid().ToString(), typeId = world.Items.Types["US"].Id.ToString(), title = "Новая", team = new { teamId = 42 } },
            OrganizationsTokens.SignedIn());
        var nameNumber = await OrganizationsApi.SendAsync(factory, HttpMethod.Put, defaultTeam, new { teamId = 42 }, OrganizationsTokens.SignedIn());

        foreach (var answer in new[] { saveNumber, saveString, createNumber, nameNumber })
        {
            Assert.Equal((HttpStatusCode.BadRequest, "http.bad_request"), (answer.Status, answer.ErrorCode));
        }

        Assert.Equal(before, await TeamStateAsync(factory, world.ProjectId));
        Assert.Equal(storedBefore, await StoredAsync(factory, world.ProjectId, story.Identifier));
        Assert.Equal("Вход по ссылке", (await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)).GetProperty("title").GetString());

        var next = await StoryAsync(world, "Следующая", world.Venus);
        var dash = story.Identifier.LastIndexOf('-') + 1;
        Assert.Equal(story.Identifier[..dash] + (int.Parse(story.Identifier[dash..]) + 1), next.Identifier);
    }

    // api-contract § 2.5 · "no teamId filter is read on a team's routes (the team is the filter)": a team's backlog and board
    // answer the same with a filter parameter, naming another team, none or garbage, as without it.
    [Fact]
    public async Task TeamViews_ReadNoFilter_TheTeamIsTheFilter()
    {
        var world = await CreateWorldAsync(factory);
        var mars = await StoryAsync(world, "Марс", world.Mars);
        await StoryAsync(world, "Венера", world.Venus);
        await StoryAsync(world, "Ничья", null);

        var backlog = Rows(await GetAsync(TeamBacklog(world, world.Mars)));
        var board = await GetAsync(TeamBoard(world, world.Mars));
        var cards = ProjectBoardsTestData.AllCards(board.Body);

        Assert.Equal([mars.Identifier], backlog);
        foreach (var query in new[] { Query(world.Venus.ToString()), Query("none"), Query("garbage"), Query(world.Mars.ToString(), "none") })
        {
            var filteredBacklog = await GetAsync($"{TeamBacklog(world, world.Mars)}?{query}");
            var filteredBoard = await GetAsync($"{TeamBoard(world, world.Mars)}?{query}");

            Assert.Equal(HttpStatusCode.OK, filteredBacklog.Status);
            Assert.Equal(backlog, Rows(filteredBacklog));
            Assert.Equal(cards, ProjectBoardsTestData.AllCards(filteredBoard.Body));
        }
    }

    // api-contract § 3 · "a malformed value … selects nothing", "none is the literal": a valid value among invalid ones still
    // matches; `none` among them matches the items of no team; any other spelling of it, and an empty value, name nothing.
    // The same on the three surfaces.
    [Fact]
    public async Task Filter_MixedValues_AValidValueAmongInvalidOnesMatches_OtherSpellingsOfNoneAndEmptyDoNot()
    {
        var world = await CreateWorldAsync(factory);
        var mars = await StoryAsync(world, "Марс", world.Mars);
        await StoryAsync(world, "Венера", world.Venus);
        var none = await StoryAsync(world, "Ничья", null);

        var matching = new (string[] Values, string[] Expected)[]
        {
            ([world.Mars.ToString(), "garbage", world.OtherProjectTeam.ToString(), world.OtherOrganizationTeam.ToString(), world.DeletedTeam.ToString()], [mars.Identifier]),
            (["none", world.OtherProjectTeam.ToString(), world.OtherOrganizationTeam.ToString(), "garbage"], [none.Identifier]),
            ([world.Mars.ToString(), world.Mars.ToString().ToUpperInvariant(), "none"], [mars.Identifier, none.Identifier]),
        };

        foreach (var (values, expected) in matching)
        {
            var query = Query(values);
            var list = await GetAsync($"{WorkItemsTestData.List(world.Items)}?{query}");
            var backlog = await GetAsync($"{Project(world)}/backlogs/stories?{query}");
            var board = await GetAsync($"{Project(world)}/boards/stories?{query}");

            Assert.Equal(expected.Order(StringComparer.Ordinal), ListRows(list).Order(StringComparer.Ordinal));
            Assert.Equal(expected.Length, list.Body.GetProperty("count").GetInt32());
            Assert.Equal(expected.Order(StringComparer.Ordinal), Rows(backlog).Order(StringComparer.Ordinal));
            Assert.Equal(expected.Order(StringComparer.Ordinal), ProjectBoardsTestData.AllCards(board.Body).Order(StringComparer.Ordinal));
        }

        foreach (var nothing in new[] { Query("None"), Query("NONE"), Query(string.Empty), Query(" none"), Query("null") })
        {
            var list = await GetAsync($"{WorkItemsTestData.List(world.Items)}?{nothing}");
            var backlog = await GetAsync($"{Project(world)}/backlogs/stories?{nothing}");
            var board = await GetAsync($"{Project(world)}/boards/stories?{nothing}");

            Assert.Equal(HttpStatusCode.OK, list.Status);
            Assert.Equal((0, 0), (list.Body.GetProperty("count").GetInt32(), list.Body.GetProperty("data").GetArrayLength()));
            Assert.Empty(Rows(backlog));
            Assert.Empty(ProjectBoardsTestData.AllCards(board.Body));
        }
    }

    // US-3 · "losing the named team … and the inference applies again": a project of two teams names the second, a creation with
    // no team takes it; the second is deleted — the setting is empty, the first is marked as the only team and the next creation
    // takes it, the first creation having gone to «Без команды» with the deletion; a third team ends the inference and the next
    // creation starts with none (BR-OT-4, BR-OT-12).
    [Fact]
    public async Task DefaultTeam_NamedThenDeleted_TheOnlyTeamIsTheDefaultAgain_ASecondTeamEndsIt()
    {
        var world = await CreateWorldAsync(factory);
        var organization = world.OrganizationId;
        var two = await OrganizationsTestData.CreateProjectAsync(factory, organization, "Две", "TWO");
        var items = world.Items with { ProjectId = two.Project.Id, RootAreaId = two.Root.Id, Key = "TWO" };
        var second = await AddTeamAsync(factory, two.Project.Id, "SECOND");
        var teams = OrganizationsApi.Teams(organization, two.Project.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, $"{OrganizationsApi.Project(organization, two.Project.Id)}/default-team", new { teamId = second.ToString() }, OrganizationsTokens.SignedIn())).Status);
        var first = await WorkItemsTestData.CreateItemAsync(factory, items, "US", "Первая без поля команды");
        Assert.Equal(second, (await StoredAsync(factory, two.Project.Id, first)).TeamId);

        Assert.Equal(HttpStatusCode.NoContent, (await OrganizationsApi.SendAsync(
            factory, HttpMethod.Delete, $"{teams}/{second}", token: OrganizationsTokens.SignedIn())).Status);

        Assert.Null(await DefaultSettingOfAsync(two.Project.Id));
        Assert.Equal([(two.Team.Id, "onlyTeam")], await MarksAsync(teams));
        Assert.Null((await StoredAsync(factory, two.Project.Id, first)).TeamId);
        var next = await WorkItemsTestData.CreateItemAsync(factory, items, "US", "Вторая без поля команды");
        Assert.Equal(two.Team.Id, (await StoredAsync(factory, two.Project.Id, next)).TeamId);

        await AddTeamAsync(factory, two.Project.Id, "THIRD");

        Assert.Empty(await MarksAsync(teams));
        var last = await WorkItemsTestData.CreateItemAsync(factory, items, "US", "Третья без поля команды");
        Assert.Null((await StoredAsync(factory, two.Project.Id, last)).TeamId);
    }

    // BR-OT-13 (2) · "a parent the deletion reassigned has none, so the child takes the project default as it stands after the
    // deletion". A project of two teams that names no default has none until one of them goes; the deletion of the parent's team
    // is held at its commit and a child created beneath the parent waits on it; once the deletion commits the child must take
    // the one team that is left (BR-OT-4: the only team is the default). QA reproduction of the security pass's S-1, outcome 2:
    // `LockDefaultTeamAsync` resolved "none" before the wait and the child keeps it.
    [Fact]
    public async Task Delete_VersusChildCreation_TwoTeamsNoNamedDefault_ChildTakesTheTeamThatRemains()
    {
        var world = await CreateWorldAsync(factory);
        var two = await OrganizationsTestData.CreateProjectAsync(factory, world.OrganizationId, "Две команды", "TWT");
        var items = world.Items with { ProjectId = two.Project.Id, RootAreaId = two.Root.Id, Key = "TWT" };
        var doomed = await AddTeamAsync(factory, two.Project.Id, "DOOMED");
        var parent = await ItemOfAsync(items, "FEAT", "Родитель", doomed);
        Assert.Equal(doomed, (await StoredAsync(factory, two.Project.Id, parent.Identifier)).TeamId);
        using var gate = factory.CommitGates.Open();

        var deletion = OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Delete,
            $"{OrganizationsApi.Teams(world.OrganizationId, two.Project.Id)}/{doomed}",
            token: OrganizationsTokens.SignedIn(),
            gate: gate);
        await Task.WhenAny(gate.Reached, deletion);
        Assert.False(deletion.IsCompleted, "The deletion finished before reaching its commit gate.");

        var creation = OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = items.Types["US"].Id.ToString(),
                title = "Ребёнок",
                parentId = parent.Id.ToString(),
            },
            OrganizationsTokens.SignedIn());
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, creation);
        Assert.False(creation.IsCompleted, "The creation did not wait for the deletion.");

        gate.Release();
        var (deleted, created) = (await deletion, await creation);

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.Created), (deleted.Status, created.Status));
        Assert.Null((await StoredAsync(factory, two.Project.Id, parent.Identifier)).TeamId);
        var child = await StoredAsync(factory, two.Project.Id, created.Body.GetProperty("identifier").GetString()!);
        Assert.Equal(two.Team.Id, child.TeamId);
    }

    private async Task<Item> ItemOfAsync(WorkItemsWorld items, string typeKey, string title, Guid? team)
    {
        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = items.Types[typeKey].Id.ToString(),
                title,
                team = new { teamId = team?.ToString() },
            },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    private async Task<(Guid Id, string Mark)[]> MarksAsync(string teamsPath)
    {
        var answer = await GetAsync(teamsPath);
        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return answer.Body.EnumerateArray()
            .Where(team => team.GetProperty("defaultMark").ValueKind != System.Text.Json.JsonValueKind.Null)
            .Select(team => (team.GetProperty("id").GetGuid(), team.GetProperty("defaultMark").GetString()!))
            .ToArray();
    }

    private async Task<Guid?> DefaultSettingOfAsync(Guid projectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.DefaultTeamId)
            .SingleAsync();
    }

    // BR-OT-6 · "the two orders never affect each other", with two orders that differ from each other and from the creation order
    // (the existing `TeamAndProjectOrders_AreIndependent` ends the project on the creation order, which unplaced rows read anyway:
    // code review CR-3). The team's reorder leaves the project's order, and the project's leaves the team's.
    [Fact]
    public async Task TeamAndProjectOrders_AreIndependent_WhenBothDifferFromCreationOrder()
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var a = await StoryAsync(world, "А", world.Mars);
        var b = await StoryAsync(world, "Б", world.Mars);
        var c = await StoryAsync(world, "В", world.Mars);

        await BoardsTestData.ReorderToAsync(factory, boards, world.Mars, c, a, b);
        Assert.Equal([c.Identifier, a.Identifier, b.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Mars));
        Assert.Equal([a.Identifier, b.Identifier, c.Identifier], await ProjectBoardsTestData.RowsAsync(factory, boards));

        await ProjectBoardsTestData.ReorderToAsync(factory, boards, b, c, a);
        Assert.Equal([b.Identifier, c.Identifier, a.Identifier], await ProjectBoardsTestData.RowsAsync(factory, boards));
        Assert.Equal([c.Identifier, a.Identifier, b.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Mars));

        await BoardsTestData.ReorderToAsync(factory, boards, world.Mars, a, b, c);
        Assert.Equal([a.Identifier, b.Identifier, c.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Mars));
        Assert.Equal([b.Identifier, c.Identifier, a.Identifier], await ProjectBoardsTestData.RowsAsync(factory, boards));
    }

    // BR-OT-8 with BR-OT-12 · a deleted team's items stand under «Без команды» on the three surfaces, the filter by the deleted
    // team's id selects nothing (answering as an unknown id), and the team's address answers «Команда не найдена.»
    [Fact]
    public async Task DeletedTeam_ItsItemsAreUnderNone_TheFilterByItsIdSelectsNothing_ItsViewsAnswer404()
    {
        var world = await CreateWorldAsync(factory);
        var mars = await StoryAsync(world, "Марс", world.Mars);
        var venus = await StoryAsync(world, "Венера", world.Venus);

        var deleted = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Delete, $"{OrganizationsApi.Teams(world.OrganizationId, world.ProjectId)}/{world.Mars}", token: OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.NoContent, deleted.Status);

        var none = Query("none");
        var byDeleted = Query(world.Mars.ToString());
        var byUnknown = Query(Guid.NewGuid().ToString());

        Assert.Equal([mars.Identifier], ListRows(await GetAsync($"{WorkItemsTestData.List(world.Items)}?{none}")));
        Assert.Equal([mars.Identifier], Rows(await GetAsync($"{Project(world)}/backlogs/stories?{none}")));
        Assert.Equal([mars.Identifier], ProjectBoardsTestData.AllCards((await GetAsync($"{Project(world)}/boards/stories?{none}")).Body));

        var listByDeleted = await GetAsync($"{WorkItemsTestData.List(world.Items)}?{byDeleted}");
        var listByUnknown = await GetAsync($"{WorkItemsTestData.List(world.Items)}?{byUnknown}");
        Assert.Equal(listByUnknown.Status, listByDeleted.Status);
        Assert.Equal(listByUnknown.Body.ToString(), listByDeleted.Body.ToString());
        Assert.Equal(0, listByDeleted.Body.GetProperty("count").GetInt32());
        Assert.Empty(Rows(await GetAsync($"{Project(world)}/backlogs/stories?{byDeleted}")));

        var venusOnly = await GetAsync($"{WorkItemsTestData.List(world.Items)}?{Query(world.Venus.ToString())}");
        Assert.Equal([venus.Identifier], ListRows(venusOnly));

        foreach (var path in new[] { TeamBacklog(world, world.Mars), TeamBoard(world, world.Mars) })
        {
            var gone = await GetAsync(path);
            Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (gone.Status, gone.ErrorCode));
        }
    }
}
