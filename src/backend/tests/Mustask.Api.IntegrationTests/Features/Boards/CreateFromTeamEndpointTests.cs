using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/workitems</c>
/// with <c>team</c> (mod-017 api-contract §§ 0.2, 2.2, mod-003 § 2.2): a creation
/// from a team's backlog (US-3) or board (US-6) is owned by that team (BR-OT-3) and lands last on
/// that team's backlog; the item's area is the root, a classification path. mod-006's flat
/// <c>teamId</c> and the team's default area are gone. Every other rule is mod-003's.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class CreateFromTeamEndpointTests(MustaskApiFactory factory)
{
    private static string AreaPath(Answer answer) => answer.Body.GetProperty("area").GetProperty("path").GetString()!;

    private static Item Created(Answer answer)
    {
        Assert.True(answer.Status == HttpStatusCode.Created, $"The creation answered {answer.Status} {answer.ErrorCode}.");

        return new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    // --- US-3 ------------------------------------------------------------------------------------------------------

    // US-3 · "Creation rules stay mod-003's": an empty title is refused with its text and nothing is created; the same
    // submission twice leaves one item (201, then 200 with that item).
    [Fact]
    public async Task Create_FromATeam_KeepsMod003sRules()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);

        var empty = await BoardsTestData.CreateFromTeamAsync(factory, world, "US", "  ", world.PlatformId.ToString());
        BoardsTestData.AssertRefused(empty, HttpStatusCode.BadRequest, "workitems.validation_failed");
        Assert.Equal("Введите заголовок.", empty.Body.GetProperty("errors").GetProperty("title")[0].GetProperty("detail").GetString());
        Assert.Empty(await WorkItemsTestData.StoredAsync(factory, world.ProjectId));

        var requestId = Guid.NewGuid();
        var first = await BoardsTestData.CreateFromTeamAsync(factory, world, "US", "Один раз", world.PlatformId.ToString(), requestId);
        var second = await BoardsTestData.CreateFromTeamAsync(factory, world, "US", "Один раз", world.PlatformId.ToString(), requestId);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.OK), (first.Status, second.Status));
        Assert.Equal(first.Body.GetProperty("id").GetGuid(), second.Body.GetProperty("id").GetGuid());
        Assert.Single(await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
    }

    // US-3 and US-6 · "The team was deleted while the dialog was open": «Команда не найдена.», nothing created, no number
    // consumed. A team id that is malformed or unknown answers the same; an active team of another project of the
    // organization answers 409 «Команда не участвует в проекте.» (mod-017 api-contract § 0.2, BR-OT-2).
    [Theory]
    [InlineData("deleted", HttpStatusCode.NotFound, "organizations.team.not_found", "Команда не найдена.")]
    [InlineData("malformed", HttpStatusCode.NotFound, "organizations.team.not_found", "Команда не найдена.")]
    [InlineData("unknown", HttpStatusCode.NotFound, "organizations.team.not_found", "Команда не найдена.")]
    [InlineData("of-another-project", HttpStatusCode.Conflict, "organizations.team.not_in_project", "Команда не участвует в проекте.")]
    public async Task Create_TeamNotFound_Returns404_NothingCreated(string kind, HttpStatusCode status, string code, string detail)
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        string teamId;

        switch (kind)
        {
            case "deleted":
                await OrganizationsTestData.DeleteTeamAsync(factory, world.ProjectId, world.PlatformId);
                teamId = world.PlatformId.ToString();
                break;
            case "malformed":
                teamId = "платформа";
                break;
            case "unknown":
                teamId = Guid.CreateVersion7().ToString();
                break;
            default:
                var other = await WorkItemsTestData.AddProjectAsync(factory, world.Items, "OTH", "Другой проект");
                teamId = (await OrganizationsTestData.AddTeamAsync(factory, other.ProjectId, "Чужая")).Team.Id.ToString();
                break;
        }

        var answer = await BoardsTestData.CreateFromTeamAsync(factory, world, "US", "История", teamId);

        BoardsTestData.AssertRefused(answer, status, code);
        Assert.Equal(detail, answer.Detail);
        Assert.Empty(await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
        var next = await BoardsTestData.CreateAsync(factory, world, "US", "Следующая");
        Assert.Equal("MSK-US-1", next.Identifier);
    }

    // US-3 and US-6 · "The team was deleted while the dialog was open", under a real overlap: the deletion holds the team at its
    // commit; the creation waits on the team's lock, then sees it deleted and creates nothing.
    [Fact]
    public async Task Create_TeamBeingDeleted_WaitsThenReturns404()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);

        using var gate = factory.CommitGates.Open();
        var deletion = OrganizationsApi.SendAsync(
            factory, HttpMethod.Delete, BoardsTestData.Team(world, world.PlatformId), token: OrganizationsTokens.SignedIn(), gate: gate);
        await Task.WhenAny(gate.Reached, deletion);
        Assert.False(deletion.IsCompleted, "The deletion finished before reaching its commit gate.");

        var creation = BoardsTestData.CreateFromTeamAsync(factory, world, "US", "История", world.PlatformId.ToString());
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, creation);
        Assert.False(creation.IsCompleted, "The creation did not wait on the team's lock.");

        gate.Release();
        Assert.Equal(HttpStatusCode.NoContent, (await deletion).Status);
        BoardsTestData.AssertRefused(await creation, HttpStatusCode.NotFound, "organizations.team.not_found");
        Assert.Empty(await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
    }

    // api-contract § 7: a replay answers its item whatever its team has become since.
    [Fact]
    public async Task Create_ReplayAfterTheTeamWasDeleted_Returns200WithTheItem()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var requestId = Guid.NewGuid();
        var first = Created(await BoardsTestData.CreateFromTeamAsync(factory, world, "BUG", "Баг", world.PlatformId.ToString(), requestId));
        await OrganizationsTestData.DeleteTeamAsync(factory, world.ProjectId, world.PlatformId);

        var replay = await BoardsTestData.CreateFromTeamAsync(factory, world, "BUG", "Баг", world.PlatformId.ToString(), requestId);

        Assert.Equal(HttpStatusCode.OK, replay.Status);
        Assert.Equal(first.Id, replay.Body.GetProperty("id").GetGuid());
    }

    // mod-017 api-contract § 0.2: mod-006's flat `teamId` is retired — a body that still carries it has it ignored, as any
    // unknown property is, so even an id that names no team is no refusal: the item takes the project default team ("Мастаск"
    // in this world) in the root, and `team: { teamId: null }` is «Без команды», explicitly.
    [Fact]
    public async Task Create_TeamIdNull_TakesTheRoot()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);

        var flat = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new { requestId = Guid.NewGuid().ToString(), typeId = world.TypeId("US").ToString(), title = "Плоский", teamId = Guid.CreateVersion7().ToString() },
            OrganizationsTokens.SignedIn());
        var none = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new { requestId = Guid.NewGuid().ToString(), typeId = world.TypeId("US").ToString(), title = "Без команды", team = new { teamId = (string?)null } },
            OrganizationsTokens.SignedIn());

        Created(flat);
        Created(none);
        Assert.Equal(("Мастаск", "Мастаск"), (AreaPath(flat), AreaPath(none)));
        Assert.Equal(world.FirstTeamId, flat.Body.GetProperty("team").GetProperty("id").GetGuid());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, none.Body.GetProperty("team").ValueKind);
    }

    // --- US-6 ------------------------------------------------------------------------------------------------------

    // US-6 · "The new card is the last of «Новые», owned by the team" (the column reads «Новая», bug-004 D-3): the
    // «Фичи» board of «Платформа»; the item's area is the root, whatever the team (mod-017 BR-OT-3, BR-OT-10).
    [Fact]
    public async Task Create_FromTheBoard_IsTheLastCardOfNew_AndTheLastRow()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var feat6 = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Шестая", world.PlatformAreaId);

        var answer = await BoardsTestData.CreateFromTeamAsync(factory, world, "FEAT", "Отчёты по командам", world.PlatformId.ToString());

        var created = Created(answer);
        Assert.Equal("Мастаск", AreaPath(answer));
        Assert.Equal(world.PlatformId, answer.Body.GetProperty("team").GetProperty("id").GetGuid());
        var board = (await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId, "features")).Body;
        Assert.Equal($"Новая: {feat6.Identifier} {created.Identifier}", BoardsTestData.Columns(board)[0]);
        Assert.Equal([feat6.Identifier, created.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId, "features"));
    }

    // US-6 · "Creating from the empty board": the «Эпики» board of «Аналитика» has its four columns, all empty; after the
    // creation its card is in «Новые» (the column reads «Новая», bug-004 D-3). US-3 · "Creating from the empty backlog":
    // the backlog then has the one row.
    [Fact]
    public async Task Create_FromTheEmptyBoard_CardInNew()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var (analytics, _) = await BoardsTestData.AddTeamAsync(factory, world, "Аналитика");
        var before = (await BoardsTestData.GetBoardAsync(factory, world, analytics, "epics")).Body;
        Assert.Equal(["Новая:", "Активна:", "Разрешена:", "Закрыта:"], BoardsTestData.Columns(before));

        var created = Created(await BoardsTestData.CreateFromTeamAsync(factory, world, "EPIC", "Миграция данных", analytics.ToString()));

        var after = (await BoardsTestData.GetBoardAsync(factory, world, analytics, "epics")).Body;
        Assert.Equal([$"Новая: {created.Identifier}", "Активна:", "Разрешена:", "Закрыта:"], BoardsTestData.Columns(after));
        Assert.Equal([created.Identifier], await BoardsTestData.RowsAsync(factory, world, analytics, "epics"));
    }
}
