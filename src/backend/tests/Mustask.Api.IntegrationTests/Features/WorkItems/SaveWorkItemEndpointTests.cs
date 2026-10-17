using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// <c>PATCH /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}</c>
/// (mod-003 api-contract § 2.4): the form's one save — Заголовок and Описание
/// (US-3), Исполнитель and Область (US-4), Статус (US-5) — exactly the fields
/// sent, all of them or none, with no conflict reported (BR-M3-25).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SaveWorkItemEndpointTests(MustaskApiFactory factory)
{
    private static string? Text(JsonElement item, params string[] path)
    {
        foreach (var name in path)
        {
            item = item.GetProperty(name);
        }

        return item.ValueKind == JsonValueKind.Null ? null : item.GetString();
    }

    private async Task<(WorkItemsWorld World, string Bug)> BugAsync(string title = "Падение при входе")
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", title);

        return (world, bug);
    }

    private static void AssertRefused(Answer answer, HttpStatusCode status, string code)
    {
        Assert.True(answer.Status == status, $"Expected {status} {code}, got {answer.Status} {answer.ErrorCode}.");
        Assert.Equal(code, answer.ErrorCode);
    }

    // --- US-3 ------------------------------------------------------------------------------------------------------

    // US-3 · "A title and a description are changed in one save": 200 with both, the identifier unchanged; reopening
    // shows both; the list's «Изменён» is the save's time.
    [Fact]
    public async Task Save_TitleAndDescription_Returns200_Stored_ChangedAtIsTheSaves()
    {
        var (world, bug) = await BugAsync();
        var before = await WorkItemsTestData.ReadAsync(factory, world, bug);

        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { title = "  Падение при входе через SSO ", description = "Шаги:\n1. Открыть" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(("MSK-BUG-1", "Падение при входе через SSO", "Шаги:\n1. Открыть"), (
            Text(answer.Body, "identifier"), Text(answer.Body, "title"), Text(answer.Body, "description")));
        Assert.True(answer.Body.GetProperty("changedAt").GetDateTimeOffset() > before.GetProperty("changedAt").GetDateTimeOffset());
        Assert.Equal(before.GetProperty("createdAt").GetDateTimeOffset(), answer.Body.GetProperty("createdAt").GetDateTimeOffset());

        var reopened = await WorkItemsTestData.ReadAsync(factory, world, bug);
        Assert.Equal(("Падение при входе через SSO", "Шаги:\n1. Открыть"), (Text(reopened, "title"), Text(reopened, "description")));
        var row = (await WorkItemsTestData.ListAsync(factory, world)).Body.GetProperty("data")[0];
        Assert.Equal(answer.Body.GetProperty("changedAt").GetDateTimeOffset(), row.GetProperty("changedAt").GetDateTimeOffset());
    }

    // US-3 · "Nothing to save" and US-5 · "Choosing the current state changes nothing": an empty save, or one sending the
    // stored values, answers 200 and moves nothing, «Изменён» included.
    [Fact]
    public async Task Save_NothingOrTheStoredValues_Returns200_ChangesNothing()
    {
        var (world, bug) = await BugAsync();
        var before = await WorkItemsTestData.ReadAsync(factory, world, bug);

        var empty = await WorkItemsTestData.SaveAsync(factory, world, bug, new { });
        var same = await WorkItemsTestData.SaveAsync(factory, world, bug, new
        {
            title = Text(before, "title"),
            description = "",
            stateId = Text(before, "state", "id"),
            assignee = new { userId = (string?)null },
            areaId = Text(before, "area", "id"),
        });

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (empty.Status, same.Status));
        Assert.Equal(before.GetProperty("changedAt").GetDateTimeOffset(), same.Body.GetProperty("changedAt").GetDateTimeOffset());
        Assert.Equal(before.GetRawText(), (await WorkItemsTestData.ReadAsync(factory, world, bug)).GetRawText());
    }

    // US-3 · "An invalid value is refused and nothing is stored": the field's own text, every field at once, and the
    // other fields sent with it are not stored either (all or none).
    [Theory]
    [InlineData("", null, "title", "workitems.workitem.title_required", "Введите заголовок.")]
    [InlineData("256", null, "title", "workitems.workitem.title_too_long", "Не более 255 символов.")]
    [InlineData(null, "100001", "description", "workitems.workitem.description_too_long", "Не более 100 000 символов.")]
    public async Task Save_InvalidValue_Returns400_WithTheFieldsText_NothingStored(string? title, string? description, string field, string code, string text)
    {
        var (world, bug) = await BugAsync();
        var sentTitle = title == "256" ? new string('ж', 256) : title;
        var sentDescription = description == "100001" ? new string('ж', 100_001) : description;
        var before = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);

        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new
        {
            title = sentTitle ?? "Другой заголовок",
            description = sentDescription ?? "Другое описание",
            stateId = world.Types["BUG"].StateId("Активна").ToString(),
        });

        AssertRefused(answer, HttpStatusCode.BadRequest, "workitems.validation_failed");
        var reason = Assert.Single(answer.Body.GetProperty("errors").GetProperty(field).EnumerateArray());
        Assert.Equal((code, text), (reason.GetProperty("code").GetString(), reason.GetProperty("detail").GetString()));
        Assert.Equal(before, await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
    }

    [Fact]
    public async Task Save_TitleAndDescriptionBothInvalid_BothReasonsAtOnce()
    {
        var (world, bug) = await BugAsync();

        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { title = "Две\nстроки", description = new string('ж', 100_001) });

        AssertRefused(answer, HttpStatusCode.BadRequest, "workitems.validation_failed");
        Assert.Equal("workitems.workitem.title_multiline", answer.Body.GetProperty("errors").GetProperty("title")[0].GetProperty("code").GetString());
        Assert.Equal("workitems.workitem.description_too_long", answer.Body.GetProperty("errors").GetProperty("description")[0].GetProperty("code").GetString());
    }

    // US-3 · "The description is plain text": stored and answered exactly as entered, markup and line break kept; 100 000
    // characters are accepted.
    [Fact]
    public async Task Save_Description_IsKeptAsEntered()
    {
        var (world, bug) = await BugAsync();

        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "<b>важно</b>\nшаг 2" });
        var longest = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = new string('ж', 100_000) });

        Assert.Equal("<b>важно</b>\nшаг 2", Text(answer.Body, "description"));
        Assert.Equal(HttpStatusCode.OK, longest.Status);
        Assert.Equal(100_000, Text(longest.Body, "description")!.Length);
    }

    // US-3 · "Two users save the same form one after the other" (BR-M3-25): A's title and B's description both stay, and
    // had B sent the title instead, the later one stays; no conflict either way.
    [Fact]
    public async Task Save_TwoUsersOneAfterTheOther_EachKeepsTheirFields_LaterTitleWins()
    {
        var (world, bug) = await BugAsync();

        var a = await WorkItemsTestData.SaveAsync(factory, world, bug, new { title = "Первый" });
        var b = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "Шаги" });

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (a.Status, b.Status));
        Assert.Equal(("Первый", "Шаги"), (Text(b.Body, "title"), Text(b.Body, "description")));

        var later = await WorkItemsTestData.SaveAsync(factory, world, bug, new { title = "Второй" });
        Assert.Equal(("Второй", "Шаги"), (Text(later.Body, "title"), Text(later.Body, "description")));
    }

    // BR-M3-25 under a real overlap: A's save holds the item at its commit; B's save waits on the item's lock, then
    // applies its own field to what A stored. Both answer 200 and both fields are kept.
    [Fact]
    public async Task Save_TwoAtTheSameMoment_SecondWaitsOnTheItem_BothFieldsKept()
    {
        var (world, bug) = await BugAsync();

        using var gate = factory.CommitGates.Open();
        var first = WorkItemsTestData.SaveAsync(factory, world, bug, new { title = "Первый" }, gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first save finished before reaching its commit gate.");

        var second = WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "Шаги" });
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second save did not wait on the first one's lock on the item.");

        gate.Release();
        var (a, b) = (await first, await second);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (a.Status, b.Status));
        var stored = await WorkItemsTestData.ReadAsync(factory, world, bug);
        Assert.Equal(("Первый", "Шаги"), (Text(stored, "title"), Text(stored, "description")));
    }

    // US-3: the address resolves as the read's does; a save never reaches an item the read would not.
    [Theory]
    [InlineData("MSK-TASK-1")]
    [InlineData("MSK-BUG-2")]
    [InlineData("ABC-BUG-1")]
    [InlineData("msk-bug-1")]
    public async Task Save_IdentifierResolvingToNothing_Returns404WorkItemNotFound(string identifier)
    {
        var (world, _) = await BugAsync();

        AssertRefused(await WorkItemsTestData.SaveAsync(factory, world, identifier, new { title = "Иное" }), HttpStatusCode.NotFound, "workitems.workitem.not_found");
    }

    [Fact]
    public async Task Save_DeactivatedProject_Returns404ProjectNotFound_NothingStored()
    {
        var (world, bug) = await BugAsync();
        await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, world.ProjectId, ProjectStatus.Deactivated);

        AssertRefused(await WorkItemsTestData.SaveAsync(factory, world, bug, new { title = "Иное" }), HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.Equal("Падение при входе", (await WorkItemsTestData.StoredAsync(factory, world.ProjectId))[0].Title);
    }

    [Fact]
    public async Task Save_NoToken_Returns401()
    {
        var (world, bug) = await BugAsync();

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Patch, WorkItemsTestData.Item(world, bug), new { title = "Иное" });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
    }

    // --- US-4 ------------------------------------------------------------------------------------------------------

    // US-4 · "An assignee is found by name and saved" and "An assignment is cleared".
    [Fact]
    public async Task Save_Assignee_SetThenCleared()
    {
        var (world, bug) = await BugAsync();
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world, "Анна", "Смирнова", "anna.smirnova@example.com");

        var assigned = await WorkItemsTestData.SaveAsync(factory, world, bug, new { assignee = new { userId = anna } });

        Assert.Equal(HttpStatusCode.OK, assigned.Status);
        Assert.Equal((anna, "Анна Смирнова", "anna.smirnova@example.com"), (
            Text(assigned.Body, "assignee", "id"), Text(assigned.Body, "assignee", "displayName"), Text(assigned.Body, "assignee", "email")));

        var cleared = await WorkItemsTestData.SaveAsync(factory, world, bug, new { assignee = new { userId = (string?)null } });

        Assert.Equal(JsonValueKind.Null, cleared.Body.GetProperty("assignee").ValueKind);
        Assert.Null((await WorkItemsTestData.StoredAsync(factory, world.ProjectId))[0].AssigneeId);
    }

    // api-contract § 2.4: "assignee": null, or no assignee at all, leaves it as it is.
    [Fact]
    public async Task Save_AssigneeNullOrAbsent_LeavesItAsItIs()
    {
        var (world, bug) = await BugAsync();
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world, "Анна", "Смирнова");
        await WorkItemsTestData.SaveAsync(factory, world, bug, new { assignee = new { userId = anna } });

        await WorkItemsTestData.SaveAsync(factory, world, bug, new { title = "Иное", assignee = (object?)null });

        Assert.Equal(anna, (await WorkItemsTestData.StoredAsync(factory, world.ProjectId))[0].AssigneeId);
    }

    // US-4 · "An inactive user cannot be newly assigned": set directly at the server it is refused with its text and
    // nothing changes, the title sent with it included. An id the directory does not know is refused the same way.
    [Fact]
    public async Task Save_InactiveOrUnknownUser_Returns409AssigneeUnavailable_NothingStored()
    {
        var (world, bug) = await BugAsync();
        var petr = await WorkItemsTestData.AddMemberAsync(factory, world, "Пётр", "Иванов", active: false);

        foreach (var userId in new[] { petr, Guid.NewGuid().ToString(), "" })
        {
            var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { title = "Иное", assignee = new { userId } });

            AssertRefused(answer, HttpStatusCode.Conflict, "workitems.workitem.assignee_unavailable");
            Assert.Equal("Этого пользователя нельзя назначить исполнителем.", answer.Detail);
        }

        var stored = (await WorkItemsTestData.StoredAsync(factory, world.ProjectId))[0];
        Assert.Equal(("Падение при входе", (string?)null), (stored.Title, stored.AssigneeId));
    }

    // US-4 · "An assignee whose account is disabled later stays": a later save of the title keeps her, re-sent or not,
    // and the answer marks nothing.
    [Fact]
    public async Task Save_AssigneeDisabledLater_StaysOnOtherSaves()
    {
        var (world, bug) = await BugAsync();
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world, "Анна", "Смирнова");
        await WorkItemsTestData.SaveAsync(factory, world, bug, new { assignee = new { userId = anna } });
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE directory_users SET enabled = false, is_active = false WHERE id = {anna}");

        var titleOnly = await WorkItemsTestData.SaveAsync(factory, world, bug, new { title = "Иное" });
        var resent = await WorkItemsTestData.SaveAsync(factory, world, bug, new { title = "Ещё иное", assignee = new { userId = anna } });

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (titleOnly.Status, resent.Status));
        Assert.Equal((anna, "Анна Смирнова"), (Text(resent.Body, "assignee", "id"), Text(resent.Body, "assignee", "displayName")));

        // The answer marks nothing (the «неактивен» mark is feat-017's); mod-016 adds only the membership flag (api-contract § 3.3).
        Assert.Equal(["displayName", "email", "id", "isMember"], resent.Body.GetProperty("assignee").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.True(resent.Body.GetProperty("assignee").GetProperty("isMember").GetBoolean());
    }

    // US-4 · "An area is chosen and saved": the form and the list show its path.
    [Fact]
    public async Task Save_Area_Returns200_WithItsPath()
    {
        var (world, bug) = await BugAsync();
        var platform = await OrganizationsTestData.AddAreaAsync(factory, world.ProjectId, world.RootAreaId, "Платформа");
        var api = await OrganizationsTestData.AddAreaAsync(factory, world.ProjectId, platform.Id, "API");

        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { areaId = api.Id.ToString() });

        Assert.Equal((api.Id.ToString(), "Платформа / API"), (Text(answer.Body, "area", "id"), Text(answer.Body, "area", "path")));
        var row = (await WorkItemsTestData.ListAsync(factory, world)).Body.GetProperty("data")[0];
        Assert.Equal("Платформа / API", Text(row, "area", "path"));
    }

    // US-4 · "An area of another project is refused": «Склад» of «Операции» sent for MSK-BUG-1 is «Область не найдена.»
    // and the area is unchanged; a malformed or unknown area answers the same.
    [Fact]
    public async Task Save_AreaOfAnotherProjectMalformedOrUnknown_Returns404AreaNotFound_Unchanged()
    {
        var (world, bug) = await BugAsync();
        var operations = await WorkItemsTestData.AddProjectAsync(factory, world, "OPS", "Операции");
        var store = await OrganizationsTestData.AddAreaAsync(factory, operations.ProjectId, operations.RootAreaId, "Склад");

        foreach (var areaId in new[] { store.Id.ToString(), "abc", Guid.CreateVersion7().ToString() })
        {
            var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { areaId });

            AssertRefused(answer, HttpStatusCode.NotFound, "organizations.area.not_found");
            Assert.Equal("Область не найдена.", answer.Detail);
        }

        Assert.Equal(world.RootAreaId, (await WorkItemsTestData.StoredAsync(factory, world.ProjectId))[0].AreaId);
    }

    // US-4 · "The item follows its area's changes": «API» renamed to «Шлюз» and moved under «Отчёты» reads «Отчёты / Шлюз»;
    // deleting the team that owns the item (mod-017: area edits and a team deletion are apart) leaves the item's area as it is.
    // Renamed (D-16, BR-OT-10): Save_AreaRenamedMovedAndItsTeamDeleted_ItemFollowsTheArea -> Save_AreaRenamedMoved_ThenTheItemsTeamDeleted_ItemKeepsItsArea.
    [Fact]
    public async Task Save_AreaRenamedMoved_ThenTheItemsTeamDeleted_ItemKeepsItsArea()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Падение при входе");
        var team = await OrganizationsTestData.AddTeamAsync(factory, world.ProjectId, "Платформа");
        var api = await OrganizationsTestData.AddAreaAsync(factory, world.ProjectId, world.RootAreaId, "API");
        var reports = await OrganizationsTestData.AddAreaAsync(factory, world.ProjectId, world.RootAreaId, "Отчёты");
        await WorkItemsTestData.SaveAsync(factory, world, bug, new { areaId = api.Id.ToString() });
        var areaRoute = $"{OrganizationsApi.Areas(world.OrganizationId, world.ProjectId)}/{api.Id}";

        Assert.Equal(HttpStatusCode.OK, (await OrganizationsApi.SendAsync(factory, HttpMethod.Put, $"{areaRoute}/name", new { name = "Шлюз" }, OrganizationsTokens.SignedIn())).Status);
        Assert.Equal(HttpStatusCode.OK, (await OrganizationsApi.SendAsync(factory, HttpMethod.Put, $"{areaRoute}/parent", new { parentId = reports.Id.ToString() }, OrganizationsTokens.SignedIn())).Status);

        Assert.Equal("Отчёты / Шлюз", Text(await WorkItemsTestData.ReadAsync(factory, world, bug), "area", "path"));
        Assert.Equal("Отчёты / Шлюз", Text((await WorkItemsTestData.ListAsync(factory, world)).Body.GetProperty("data")[0], "area", "path"));

        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET owning_team_id = {team.Id} WHERE project_id = {world.ProjectId}");
        await OrganizationsTestData.DeleteTeamAsync(factory, world.ProjectId, team.Id);

        var after = await WorkItemsTestData.ReadAsync(factory, world, bug);
        Assert.Equal((api.Id.ToString(), "Отчёты / Шлюз"), (Text(after, "area", "id"), Text(after, "area", "path")));
    }

    // --- US-5 ------------------------------------------------------------------------------------------------------

    // US-5 · "A forward change is saved", "A backward change needs no reason", "A change may skip states".
    [Fact]
    public async Task Save_State_ForwardBackwardAndSkipping()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Баг");
        var story = await WorkItemsTestData.CreateItemAsync(factory, world, "US", "История");
        var task = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Задача");

        var resolved = await WorkItemsTestData.SaveAsync(factory, world, bug, new { stateId = world.Types["BUG"].StateId("Разрешена").ToString() });
        await WorkItemsTestData.SaveAsync(factory, world, story, new { stateId = world.Types["US"].StateId("Закрыта").ToString() });
        var reopened = await WorkItemsTestData.SaveAsync(factory, world, story, new { stateId = world.Types["US"].StateId("Новая").ToString() });
        var skipped = await WorkItemsTestData.SaveAsync(factory, world, task, new { stateId = world.Types["TASK"].StateId("Закрыта").ToString() });

        Assert.Equal(("Разрешена", "Resolved"), (Text(resolved.Body, "state", "name"), Text(resolved.Body, "state", "category", "code")));
        Assert.Equal("Новая", Text(reopened.Body, "state", "name"));
        Assert.Equal(("Закрыта", "Completed"), (Text(skipped.Body, "state", "name"), Text(skipped.Body, "state", "category", "code")));
        var row = (await WorkItemsTestData.ListAsync(factory, world)).Body.GetProperty("data").EnumerateArray().Single(r => Text(r, "identifier") == bug);
        Assert.Equal("Разрешена", Text(row, "state", "name"));
    }

    // US-5 · "A state change touches no other field".
    [Fact]
    public async Task Save_StateOnly_LeavesAssigneeAndAreaAsTheyAre()
    {
        var (world, bug) = await BugAsync();
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world, "Анна", "Смирнова");
        var platform = await OrganizationsTestData.AddAreaAsync(factory, world.ProjectId, world.RootAreaId, "Платформа");
        await WorkItemsTestData.SaveAsync(factory, world, bug, new
        {
            stateId = world.Types["BUG"].StateId("Активна").ToString(),
            assignee = new { userId = anna },
            areaId = platform.Id.ToString(),
        });

        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { stateId = world.Types["BUG"].StateId("Закрыта").ToString() });

        Assert.Equal(("Закрыта", anna, platform.Id.ToString(), "Падение при входе"), (
            Text(answer.Body, "state", "name"), Text(answer.Body, "assignee", "id"), Text(answer.Body, "area", "id"), Text(answer.Body, "title")));
    }

    // US-5 · "A state outside the type's own is refused": «Задача»'s «Удалена», «Баг»'s «Разрешена», another
    // organization's «Задача» «Активна», and a value that is no id at all; the item stays in «Активна».
    [Fact]
    public async Task Save_StateOutsideTheTypesOwn_Returns409StateNotAllowed_StateUnchanged()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var other = await WorkItemsTestData.CreateWorldAsync(factory);
        var task = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Задача");
        var active = world.Types["TASK"].StateId("Активна");
        await WorkItemsTestData.SaveAsync(factory, world, task, new { stateId = active.ToString() });

        foreach (var stateId in new[]
                 {
                     world.Types["TASK"].StateId(StateCategory.Removed).ToString(),
                     world.Types["BUG"].StateId("Разрешена").ToString(),
                     other.Types["TASK"].StateId("Активна").ToString(),
                     "abc",
                 })
        {
            var answer = await WorkItemsTestData.SaveAsync(factory, world, task, new { stateId, title = "Иное" });

            AssertRefused(answer, HttpStatusCode.Conflict, "workitems.workitem.state_not_allowed");
            Assert.Equal("Этот статус недоступен для рабочего элемента этого типа.", answer.Detail);
        }

        var stored = (await WorkItemsTestData.StoredAsync(factory, world.ProjectId))[0];
        Assert.Equal((active, "Задача"), (stored.StateId, stored.Title));
    }

    // api-contract § 0.4: values (400) before the area (404) before the state (409) before the assignee (409).
    [Fact]
    public async Task Save_SeveralFaults_AnsweredInTheContractsOrder()
    {
        var (world, bug) = await BugAsync();
        var petr = await WorkItemsTestData.AddMemberAsync(factory, world, "Пётр", "Иванов", active: false);
        var removed = world.Types["BUG"].StateId(StateCategory.Removed).ToString();
        var unknownArea = Guid.CreateVersion7().ToString();

        var all = await WorkItemsTestData.SaveAsync(factory, world, bug, new { title = "", areaId = unknownArea, stateId = removed, assignee = new { userId = petr } });
        var noValue = await WorkItemsTestData.SaveAsync(factory, world, bug, new { areaId = unknownArea, stateId = removed, assignee = new { userId = petr } });
        var noArea = await WorkItemsTestData.SaveAsync(factory, world, bug, new { stateId = removed, assignee = new { userId = petr } });

        AssertRefused(all, HttpStatusCode.BadRequest, "workitems.validation_failed");
        AssertRefused(noValue, HttpStatusCode.NotFound, "organizations.area.not_found");
        AssertRefused(noArea, HttpStatusCode.Conflict, "workitems.workitem.state_not_allowed");
    }
}
