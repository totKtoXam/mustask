using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/workitems</c>
/// with <c>parentId</c> (feat-027 US-4, api-contract § 3; mod-003 § 2.2): a
/// child created from a backlog row in one request — the parent's area as
/// stored, linked beneath it, the parent's «Изменён» moving — all or none,
/// idempotent by <c>requestId</c>, refused with feat-001's link codes after
/// mod-003's and mod-006's own answers. US-1's data, MSK-FEAT-6 in «Платформа /
/// API» (US-4's).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class CreateChildEndpointTests(MustaskApiFactory factory)
{
    private async Task<(BoardsWorld World, IReadOnlyDictionary<string, Item> Items)> WorldAsync()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);
        await BoardsTestData.MoveToAreaAsync(factory, world, items["MSK-FEAT-6"], world.ApiAreaId);

        return (world, items);
    }

    private Task<Answer> CreateChildAsync(BoardsWorld world, string typeKey, string title, string? parentId, Guid? requestId = null, string? teamId = null) =>
        BacklogTreeTestData.CreateChildAsync(factory, world, typeKey, title, parentId, requestId, teamId);

    private Task<string> ShapeAsync(BoardsWorld world, string level) => BacklogTreeTestData.ShapeAsync(factory, world, world.PlatformId, level);

    private Task<(Guid? ParentId, Guid StateId, DateTimeOffset ChangedAt)> StoredAsync(Item item) => LinksTestData.StoredAsync(factory, item);

    private static string AreaPath(Answer answer) => answer.Body.GetProperty("area").GetProperty("path").GetString()!;

    private static Item Created(Answer answer)
    {
        Assert.True(answer.Status == HttpStatusCode.Created, $"The creation answered {answer.Status} {answer.ErrorCode}.");

        return new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    // --- Creation ---------------------------------------------------------------------------------------------------

    // US-4 · "A story is added to a feature" and "The child takes the parent's area, state and defaults": one request names
    // the type, the title and the parent; MSK-US-24 is created beneath MSK-FEAT-6, in «Платформа / API», «Новая»,
    // unassigned, «Приоритет» 2; it stands after MSK-US-2 and MSK-BUG-4 beneath MSK-FEAT-6 and is the last top-level row
    // of «Платформа»'s «Истории»; MSK-FEAT-6's «Изменён» has moved to the creation's time, nothing else of it.
    [Fact]
    public async Task Create_AStoryBeneathAFeature_TakesItsAreaAndDefaults_StandsLastBeneathIt()
    {
        var (world, items) = await WorldAsync();
        var parentBefore = await StoredAsync(items["MSK-FEAT-6"]);

        var answer = await CreateChildAsync(world, "US", "Повторная отправка письма", items["MSK-FEAT-6"].Id.ToString());

        var created = Created(answer);
        Assert.Equal("MSK-US-24", created.Identifier);
        Assert.EndsWith($"/workitems/{created.Identifier}", answer.Location!.ToString(), StringComparison.Ordinal);
        Assert.Equal("MSK-FEAT-6", LinksTestData.IdentifierOf(answer.Body.GetProperty("parent")));
        Assert.Equal(("Платформа / API", world.ApiAreaId), (AreaPath(answer), answer.Body.GetProperty("area").GetProperty("id").GetGuid()));
        Assert.Equal("Новая", answer.Body.GetProperty("state").GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("assignee").ValueKind);
        Assert.Equal((2, "Business"), (answer.Body.GetProperty("priority").GetInt32(), answer.Body.GetProperty("valueArea").GetString()));

        Assert.Contains("MSK-FEAT-6[MSK-US-2[MSK-BUG-5 MSK-TASK-3] MSK-BUG-4 MSK-US-24]", await ShapeAsync(world, "features"));
        Assert.Equal(["MSK-US-2", "MSK-BUG-4", "MSK-US-22", "MSK-US-24"], await BoardsTestData.RowsAsync(factory, world, world.PlatformId, "stories"));

        var parentAfter = await StoredAsync(items["MSK-FEAT-6"]);
        Assert.True(parentAfter.ChangedAt > parentBefore.ChangedAt, "The parent's «Изменён» did not move.");
        Assert.Equal(answer.Body.GetProperty("changedAt").GetDateTimeOffset(), parentAfter.ChangedAt);
        Assert.Equal((parentBefore.ParentId, parentBefore.StateId), (parentAfter.ParentId, parentAfter.StateId));
    }

    // US-4 · "A task is added to a nested story on «Эпики»": beneath MSK-US-2, after MSK-BUG-5 and MSK-TASK-3, in
    // MSK-US-2's area «Платформа».
    [Fact]
    public async Task Create_ATaskBeneathANestedStory_StandsLastBeneathIt()
    {
        var (world, items) = await WorldAsync();

        var answer = await CreateChildAsync(world, "TASK", "Проверить ссылку", items["MSK-US-2"].Id.ToString());

        var created = Created(answer);
        Assert.Equal(("MSK-TASK-24", "Платформа"), (created.Identifier, AreaPath(answer)));
        Assert.Contains("MSK-US-2[MSK-BUG-5 MSK-TASK-3 MSK-TASK-24]", await ShapeAsync(world, "epics"));
    }

    // US-4 · each entry of BR-T-9 is a pair the process allows: a feature beneath an epic, a bug beneath a feature, a bug
    // beneath a story. A task beneath a bug is no row entry — D-6 OQ-6 (b) withdrew it — but a BR-H-2 pair the server
    // allows all the same, so it stays here as a server-side case.
    [Fact]
    public async Task Create_EveryPairOfTheRowsEntries_IsLinked()
    {
        var (world, items) = await WorldAsync();

        var feature = Created(await CreateChildAsync(world, "FEAT", "Вход по паролю", items["MSK-EPIC-1"].Id.ToString()));
        var bugOfFeature = Created(await CreateChildAsync(world, "BUG", "Ошибка SSO", items["MSK-FEAT-8"].Id.ToString()));
        var bugOfStory = Created(await CreateChildAsync(world, "BUG", "Ссылка битая", items["MSK-US-22"].Id.ToString()));
        var taskOfBug = Created(await CreateChildAsync(world, "TASK", "Починить ссылку", items["MSK-BUG-4"].Id.ToString()));

        Assert.Equal(
            "MSK-EPIC-1[MSK-FEAT-8[MSK-FEAT-23 MSK-BUG-25] MSK-FEAT-6[MSK-US-2[MSK-BUG-5 MSK-TASK-3] MSK-BUG-4[MSK-TASK-27]] MSK-FEAT-24 "
            + "MSK-US-22[MSK-BUG-26]] MSK-EPIC-20",
            await ShapeAsync(world, "epics"));
        Assert.Equal(
            ["MSK-FEAT-24", "MSK-BUG-25", "MSK-BUG-26", "MSK-TASK-27"],
            new[] { feature, bugOfFeature, bugOfStory, taskOfBug }.Select(item => item.Identifier));
    }

    // US-4 · "«Новый рабочий элемент» still creates a top-level item": a «Фича» from «Фичи» with the team and no parent
    // has no parent, the team (mod-017 BR-OT-3), the root area, and is the last top-level row.
    [Fact]
    public async Task Create_WithoutAParent_IsATopLevelItemAsBefore()
    {
        var (world, _) = await WorldAsync();

        var answer = await BoardsTestData.CreateFromTeamAsync(factory, world, "FEAT", "Вход по токену", world.PlatformId.ToString());

        var created = Created(answer);
        Assert.Equal((JsonValueKind.Null, "Мастаск"), (answer.Body.GetProperty("parent").ValueKind, AreaPath(answer)));
        Assert.Equal(world.PlatformId, answer.Body.GetProperty("team").GetProperty("id").GetGuid());
        Assert.Equal(["MSK-FEAT-8", "MSK-FEAT-6", created.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId, "features"));
    }

    // api-contract § 3, mod-017 BR-OT-3: a team sent with a parent is not read — a child's creation names no team — so the
    // child takes the parent's area (a classification path: «Мастаск»'s default yields to MSK-FEAT-6's «Платформа / API») and
    // the parent's team as stored («Платформа»), whatever team the body carries.
    [Fact]
    public async Task Create_WithATeamAndAParent_TakesTheParentsArea()
    {
        var (world, items) = await WorldAsync();

        var answer = await CreateChildAsync(world, "US", "История", items["MSK-FEAT-6"].Id.ToString(), teamId: world.ProjectTeamId.ToString());

        Created(answer);
        Assert.Equal("Платформа / API", AreaPath(answer));
        Assert.Equal(world.PlatformId, answer.Body.GetProperty("team").GetProperty("id").GetGuid());
    }

    // --- Idempotency (BR-M3-26) -------------------------------------------------------------------------------------

    // US-4 · "A double «Создать» creates one child": the same submission twice — 201, then 200 with the same item — one
    // item, linked once; the replay moves the parent's «Изменён» no further. A replay after the parent was deleted still
    // answers 200 with the item (api-contract § 3, step 4).
    [Fact]
    public async Task Create_Replayed_OneChildLinkedOnce_EvenAfterTheParentWasDeleted()
    {
        var (world, items) = await WorldAsync();
        var requestId = Guid.NewGuid();

        var first = Created(await CreateChildAsync(world, "US", "Повторная отправка письма", items["MSK-FEAT-6"].Id.ToString(), requestId));
        var parentAfterFirst = await StoredAsync(items["MSK-FEAT-6"]);
        var replay = await CreateChildAsync(world, "US", "Повторная отправка письма", items["MSK-FEAT-6"].Id.ToString(), requestId);
        var parentAfterReplay = await StoredAsync(items["MSK-FEAT-6"]);

        // mod-005: the deletion is the real one now, and moves the parent's «Изменён» to its own time (BR-DR-2); the late replay
        // moves it no further.
        await LinksTestData.DeleteAsync(factory, world.Items, items["MSK-FEAT-6"]);
        var parentAfterDelete = await StoredAsync(items["MSK-FEAT-6"]);
        var lateReplay = await CreateChildAsync(world, "US", "Другое", items["MSK-EPIC-1"].Id.ToString(), requestId);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (replay.Status, lateReplay.Status));
        Assert.Equal((first.Id, first.Id), (replay.Body.GetProperty("id").GetGuid(), lateReplay.Body.GetProperty("id").GetGuid()));
        Assert.Equal("MSK-FEAT-6", LinksTestData.IdentifierOf(lateReplay.Body.GetProperty("parent")));
        Assert.Equal(parentAfterFirst.ChangedAt, parentAfterReplay.ChangedAt);
        Assert.Equal(parentAfterDelete.ChangedAt, (await StoredAsync(items["MSK-FEAT-6"])).ChangedAt);
        Assert.Equal(24, (await WorkItemsTestData.StoredAsync(factory, world.ProjectId)).Count);
    }

    // --- Refusals ---------------------------------------------------------------------------------------------------

    // US-4 · "A parent gone meanwhile refuses the create": MSK-FEAT-6 placed in «Удалена» after the page was read —
    // «Удалённый рабочий элемент нельзя связывать.», nothing created, no number consumed, the parent untouched.
    [Fact]
    public async Task Create_ADeletedParent_Returns409ItemDeleted_NothingCreated()
    {
        var (world, items) = await WorldAsync();
        await LinksTestData.DeleteAsync(factory, world.Items, items["MSK-FEAT-6"]);
        var parentBefore = await StoredAsync(items["MSK-FEAT-6"]);

        var answer = await CreateChildAsync(world, "US", "Повторная отправка письма", items["MSK-FEAT-6"].Id.ToString());

        BoardsTestData.AssertRefused(answer, HttpStatusCode.Conflict, "workitems.link.item_deleted");
        Assert.Equal("Удалённый рабочий элемент нельзя связывать.", answer.Detail);
        Assert.Equal(parentBefore, await StoredAsync(items["MSK-FEAT-6"]));
        await AssertNothingCreatedAsync(world);
    }

    // BR-T-10: a parent of a lower level is refused with `parent_level_not_allowed` — a feature beneath a story, an epic
    // beneath a feature — and nothing is created.
    [Theory]
    [InlineData("FEAT", "MSK-US-2")]
    [InlineData("EPIC", "MSK-FEAT-8")]
    [InlineData("US", "MSK-TASK-3")]
    public async Task Create_ALowerLevelParent_Returns409ParentLevelNotAllowed(string typeKey, string parent)
    {
        var (world, items) = await WorldAsync();

        var answer = await CreateChildAsync(world, typeKey, "Не туда", items[parent].Id.ToString());

        BoardsTestData.AssertRefused(answer, HttpStatusCode.Conflict, "workitems.link.parent_level_not_allowed");
        Assert.Equal("Родителем может быть только рабочий элемент того же или более высокого уровня.", answer.Detail);
        await AssertNothingCreatedAsync(world);
    }

    // BR-H-1 and IDOR: a parent that is malformed, all zeros, unknown, an item of another project of the organization, or
    // of another organization, answers one 404 — never telling whether the item exists elsewhere — and the foreign item
    // is untouched.
    [Theory]
    [InlineData("malformed")]
    [InlineData("zeros")]
    [InlineData("unknown")]
    [InlineData("of-another-project")]
    [InlineData("of-another-organization")]
    public async Task Create_ParentNotFound_Returns404_NothingCreated(string kind)
    {
        var (world, _) = await WorldAsync();
        Item? foreign = null;

        switch (kind)
        {
            case "of-another-project":
                var warehouse = await WorkItemsTestData.AddProjectAsync(factory, world.Items, "WH", "Склад");
                foreign = await ForeignAsync(warehouse);
                break;
            case "of-another-organization":
                foreign = await ForeignAsync(await WorkItemsTestData.CreateWorldAsync(factory));
                break;
        }

        var foreignBefore = foreign is null ? default : await StoredAsync(foreign);
        var parentId = kind switch
        {
            "malformed" => "MSK-FEAT-6",
            "zeros" => Guid.Empty.ToString(),
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => foreign!.Id.ToString(),
        };

        var answer = await CreateChildAsync(world, "US", "История", parentId);

        BoardsTestData.AssertRefused(answer, HttpStatusCode.NotFound, "workitems.link.parent_not_found");
        Assert.Equal("Родитель не найден в этом проекте.", answer.Detail);
        await AssertNothingCreatedAsync(world);

        if (foreign is not null)
        {
            Assert.Equal(foreignBefore, await StoredAsync(foreign));
        }
    }

    // api-contract § 3, the order of answers: the values (400), the type (404) and the replay (200) come before any of the
    // parent's answers; a team the body carries is not read (mod-017 BR-OT-3), so an unknown one refuses nothing and the
    // parent's answer stands; of the parent's, «Удалена» before the level.
    [Fact]
    public async Task Create_SeveralFaults_TheFirstInTheContractsOrderAnswers()
    {
        var (world, items) = await WorldAsync();
        await LinksTestData.DeleteAsync(factory, world.Items, items["MSK-FEAT-6"]);
        var deleted = items["MSK-FEAT-6"].Id.ToString();
        var requestId = Guid.NewGuid();
        Created(await CreateChildAsync(world, "US", "Первая", items["MSK-FEAT-8"].Id.ToString(), requestId));

        var invalid = await CreateChildAsync(world, "US", "  ", "не-id");
        var unknownType = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new { requestId = Guid.NewGuid().ToString(), typeId = Guid.CreateVersion7().ToString(), title = "История", parentId = deleted },
            OrganizationsTokens.SignedIn());
        var replay = await CreateChildAsync(world, "US", "Первая", deleted, requestId);
        var unknownTeam = await CreateChildAsync(world, "US", "История", deleted, teamId: Guid.CreateVersion7().ToString());
        var deletedBeforeLevel = await CreateChildAsync(world, "EPIC", "Эпик", deleted);

        BoardsTestData.AssertRefused(invalid, HttpStatusCode.BadRequest, "workitems.validation_failed");
        BoardsTestData.AssertRefused(unknownType, HttpStatusCode.NotFound, "workitems.workitem.type_not_found");
        Assert.Equal(HttpStatusCode.OK, replay.Status);
        BoardsTestData.AssertRefused(unknownTeam, HttpStatusCode.Conflict, "workitems.link.item_deleted");
        BoardsTestData.AssertRefused(deletedBeforeLevel, HttpStatusCode.Conflict, "workitems.link.item_deleted");
    }

    // --- Concurrency (spec § NFR) -----------------------------------------------------------------------------------

    // api-contract § 3, "Concurrency"; mod-017 BR-OT-3, BR-OT-13: a save moving MSK-FEAT-6 into «Платформа» and to the team
    // «Мастаск» holds at its commit; the creation beneath MSK-FEAT-6 waits on the parent's lock, then takes the area and the
    // team the save committed — the parent's team is read when the child is saved.
    [Fact]
    public async Task Create_WhileTheParentsAreaIsBeingChanged_WaitsThenTakesTheNewArea()
    {
        var (world, items) = await WorldAsync();

        using var gate = factory.CommitGates.Open();
        var save = WorkItemsTestData.SaveAsync(
            factory,
            world.Items,
            "MSK-FEAT-6",
            new { areaId = world.PlatformAreaId.ToString(), team = new { teamId = world.ProjectTeamId.ToString() } },
            gate);
        await Task.WhenAny(gate.Reached, save);
        Assert.False(save.IsCompleted, "The save finished before reaching its commit gate.");

        var creation = CreateChildAsync(world, "US", "История", items["MSK-FEAT-6"].Id.ToString());
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, creation);
        Assert.False(creation.IsCompleted, "The creation did not wait on the parent's lock.");

        gate.Release();
        Assert.Equal(HttpStatusCode.OK, (await save).Status);
        var answer = await creation;
        Created(answer);
        Assert.Equal("Платформа", AreaPath(answer));
        Assert.Equal(world.ProjectTeamId, answer.Body.GetProperty("team").GetProperty("id").GetGuid());
    }

    // api-contract § 3, "Concurrency": a parent write that moves MSK-FEAT-6 itself — beneath MSK-EPIC-20 — holds the item
    // at its commit, and the creation beneath MSK-FEAT-6 waits on that ROW (the parent's lock), then links beneath it as
    // committed. This race alone cannot tell the row's lock from the hierarchy's, as both conflict here: the next test
    // does (review B-5).
    [Fact]
    public async Task Create_WhileTheParentItselfIsBeingMoved_WaitsOnTheParentsRow_ThenLinksBeneathItAsCommitted()
    {
        var (world, items) = await WorldAsync();

        using var gate = factory.CommitGates.Open();
        var write = LinksTestData.SetParentAsync(factory, world.Items, "MSK-FEAT-6", items["MSK-EPIC-20"].Id, items["MSK-EPIC-1"].Id, gate);
        await Task.WhenAny(gate.Reached, write);
        Assert.False(write.IsCompleted, "The parent write finished before reaching its commit gate.");

        var creation = CreateChildAsync(world, "BUG", "Баг", items["MSK-FEAT-6"].Id.ToString());
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, creation);
        Assert.False(creation.IsCompleted, "The creation did not wait on the parent's row.");

        gate.Release();
        Assert.Equal(HttpStatusCode.OK, (await write).Status);
        Created(await creation);
        Assert.Contains("MSK-EPIC-20[MSK-FEAT-6[MSK-US-2[MSK-BUG-5 MSK-TASK-3] MSK-BUG-4 MSK-BUG-24]]", await ShapeAsync(world, "epics"));
    }

    // api-contract § 3, "Concurrency": the creation takes the project's HIERARCHY lock, which only a parent write of the
    // project shares with it. MSK-US-22 is moved beneath MSK-EPIC-20 — a write that touches neither MSK-FEAT-6 nor any row
    // the creation locks — and holds at its commit; the creation beneath MSK-FEAT-6 waits all the same, on the hierarchy
    // lock, and succeeds after the write. Without the creation's lock on the hierarchy it would not wait (review B-5).
    [Fact]
    public async Task Create_WhileAParentWriteOfAnotherItemHoldsTheHierarchy_WaitsForIt()
    {
        var (world, items) = await WorldAsync();

        using var gate = factory.CommitGates.Open();
        var write = LinksTestData.SetParentAsync(factory, world.Items, "MSK-US-22", items["MSK-EPIC-20"].Id, items["MSK-EPIC-1"].Id, gate);
        await Task.WhenAny(gate.Reached, write);
        Assert.False(write.IsCompleted, "The parent write finished before reaching its commit gate.");

        var creation = CreateChildAsync(world, "BUG", "Баг", items["MSK-FEAT-6"].Id.ToString());
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, creation);
        Assert.False(creation.IsCompleted, "The creation did not wait on the hierarchy's lock.");

        gate.Release();
        Assert.Equal(HttpStatusCode.OK, (await write).Status);
        Created(await creation);
        Assert.Contains("MSK-EPIC-20[MSK-US-22]", await ShapeAsync(world, "epics"));
        Assert.Contains("MSK-FEAT-6[MSK-US-2[MSK-BUG-5 MSK-TASK-3] MSK-BUG-4 MSK-BUG-24]", await ShapeAsync(world, "epics"));
    }

    /// <summary>An epic of <paramref name="other"/>'s project.</summary>
    private async Task<Item> ForeignAsync(WorkItemsWorld other)
    {
        var answer = await WorkItemsTestData.CreateAsync(factory, other, "EPIC", "Чужой эпик");
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    /// <summary>The project still holds its 23 items, and the next creation takes number 24: none was consumed.</summary>
    private async Task AssertNothingCreatedAsync(BoardsWorld world)
    {
        Assert.Equal(23, (await WorkItemsTestData.StoredAsync(factory, world.ProjectId)).Count);
        Assert.Equal("MSK-US-24", Created(await CreateChildAsync(world, "US", "Следующая", parentId: null)).Identifier);
    }
}
