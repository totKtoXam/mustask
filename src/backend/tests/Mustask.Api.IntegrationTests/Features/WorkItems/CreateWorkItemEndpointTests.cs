using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/workitems</c>
/// (mod-003 api-contract § 2.2, US-1) through the real host and a real
/// PostgreSQL: the item's identifier and defaults, the title's rules, the
/// project's one number sequence under concurrent creations (BR-M3-5) and the
/// replay of one submission (BR-M3-26).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class CreateWorkItemEndpointTests(MustaskApiFactory factory)
{
    // US-1 · "A work item is created with only a title": 201 with its address; Заголовок trimmed, Статус «Новая»,
    // Область the root «Мастаск», Исполнитель «Не назначен», an empty Описание; created and changed by the caller, now.
    [Fact]
    public async Task Create_TitleOnly_Returns201_WithTheItemsDefaults()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var author = await Organizations.MembershipTestData.NewMemberAsync(factory, world.OrganizationId, Mustask.Domain.Identity.OrganizationRole.Member);
        var subject = author.Id;

        var answer = await WorkItemsTestData.CreateAsync(factory, world, "TASK", "  Настроить сборку  ", token: author.Token);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal($"/api/organizations/{world.OrganizationId}/projects/{world.ProjectId}/workitems/MSK-TASK-1", answer.Location!.OriginalString);

        var body = answer.Body;
        Assert.Equal("MSK-TASK-1", body.GetProperty("identifier").GetString());
        Assert.Equal(1, body.GetProperty("number").GetInt32());
        Assert.Equal(world.ProjectId, body.GetProperty("projectId").GetGuid());
        Assert.Equal(("TASK", "Задача", "task"), (
            body.GetProperty("type").GetProperty("key").GetString(),
            body.GetProperty("type").GetProperty("name").GetString(),
            body.GetProperty("type").GetProperty("appearance").GetString()));
        Assert.Equal(world.Types["TASK"].Id, body.GetProperty("type").GetProperty("id").GetGuid());
        Assert.Equal("Настроить сборку", body.GetProperty("title").GetString());
        Assert.Equal(string.Empty, body.GetProperty("description").GetString());
        Assert.Equal(("Новая", "Proposed", "Новые"), (
            body.GetProperty("state").GetProperty("name").GetString(),
            body.GetProperty("state").GetProperty("category").GetProperty("code").GetString(),
            body.GetProperty("state").GetProperty("category").GetProperty("label").GetString()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, body.GetProperty("assignee").ValueKind);
        Assert.False(body.TryGetProperty("area", out _));
        Assert.Equal(subject, body.GetProperty("createdBy").GetString());
        Assert.Equal(body.GetProperty("createdAt").GetDateTimeOffset(), body.GetProperty("changedAt").GetDateTimeOffset());

        // "its form shows an empty Описание": the read answers the same item.
        var read = await WorkItemsTestData.ReadAsync(factory, world, "MSK-TASK-1");
        Assert.Equal(string.Empty, read.GetProperty("description").GetString());
    }

    // US-1 · "Each type gets its own type key and its initial state": one sequence per project shared by the five types;
    // another project starts at 1.
    [Fact]
    public async Task Create_FiveTypes_ShareTheProjectsSequence_AnotherProjectStartsAtOne()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var identifiers = new List<string>();

        foreach (var type in new[] { "EPIC", "FEAT", "US", "BUG", "TASK" })
        {
            identifiers.Add(await WorkItemsTestData.CreateItemAsync(factory, world, type, $"Элемент {type}"));
        }

        Assert.Equal(["MSK-EPIC-1", "MSK-FEAT-2", "MSK-US-3", "MSK-BUG-4", "MSK-TASK-5"], identifiers);

        foreach (var identifier in identifiers)
        {
            var item = await WorkItemsTestData.ReadAsync(factory, world, identifier);
            Assert.Equal("Новая", item.GetProperty("state").GetProperty("name").GetString());
        }

        var operations = await WorkItemsTestData.AddProjectAsync(factory, world, "OPS", "Операции");
        Assert.Equal("OPS-BUG-1", await WorkItemsTestData.CreateItemAsync(factory, operations, "BUG", "Склад не отвечает"));
    }

    // US-1 · "Two creations at the same moment get two numbers" (BR-M3-5), held so that they really overlap: A holds the
    // project's counter at its commit, B is shown to wait on that lock, then A commits. B's number is the next one.
    [Fact]
    public async Task Create_TwoAtTheSameMoment_SecondWaitsOnTheCounter_GetsTheNextNumber()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Первый");

        using var gate = factory.CommitGates.Open();
        var first = WorkItemsTestData.CreateAsync(factory, world, "TASK", "Пользователь А", gate: gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first creation finished before reaching its commit gate.");

        var second = WorkItemsTestData.CreateAsync(factory, world, "TASK", "Пользователь Б");
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second creation did not wait on the first one's lock on the counter.");

        gate.Release();
        var (a, b) = (await first, await second);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (a.Status, b.Status));
        Assert.Equal(("MSK-TASK-2", "MSK-TASK-3"), (a.Body.GetProperty("identifier").GetString(), b.Body.GetProperty("identifier").GetString()));
    }

    // US-1 · "Two creations at the same moment get two numbers", as a burst: twenty creations sent at once all succeed with
    // twenty different numbers, each greater than every number the project issued before.
    [Fact]
    public async Task Create_TwentyAtOnce_TwentyDistinctNumbers_AfterTheExistingOnes()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Первый");
        await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Второй");

        var answers = await Task.WhenAll(Enumerable.Range(1, 20).Select(index => WorkItemsTestData.CreateAsync(factory, world, "TASK", $"Параллельно {index}")));

        Assert.All(answers, answer => Assert.Equal(HttpStatusCode.Created, answer.Status));
        var numbers = answers.Select(answer => answer.Body.GetProperty("number").GetInt32()).Order().ToList();
        Assert.Equal(Enumerable.Range(3, 20), numbers);
        Assert.Equal(22, (await WorkItemsTestData.StoredAsync(factory, world.ProjectId)).Count);
    }

    // US-1 · "A repeated submission creates one item" (BR-M3-26), the same submission sent again after its answer was lost:
    // the second answers 200 with the same item, and no number is consumed by it.
    [Fact]
    public async Task Create_SameSubmissionTwice_OneItem_ReplayAnswers200_NoNumberConsumed()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var requestId = Guid.NewGuid();

        var first = await WorkItemsTestData.CreateAsync(factory, world, "BUG", "Падение при входе", requestId);
        var replay = await WorkItemsTestData.CreateAsync(factory, world, "BUG", "Падение при входе", requestId);

        Assert.Equal(HttpStatusCode.Created, first.Status);
        Assert.Equal(HttpStatusCode.OK, replay.Status);
        Assert.Equal(first.Body.GetProperty("id").GetGuid(), replay.Body.GetProperty("id").GetGuid());
        Assert.Equal("MSK-BUG-1", replay.Body.GetProperty("identifier").GetString());
        Assert.Single(await WorkItemsTestData.StoredAsync(factory, world.ProjectId));

        Assert.Equal("MSK-BUG-2", await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Следующий"));
    }

    // US-1 · "A repeated submission creates one item", «Создать» activated twice in quick succession: the two requests
    // overlap — the second waits on the first's lock on the counter, then finds the item the first created.
    [Fact]
    public async Task Create_SameSubmissionAtTheSameMoment_OneItem_OneCreatedOneReplay()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var requestId = Guid.NewGuid();

        using var gate = factory.CommitGates.Open();
        var first = WorkItemsTestData.CreateAsync(factory, world, "BUG", "Двойной щелчок", requestId, gate: gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first submission finished before reaching its commit gate.");

        var second = WorkItemsTestData.CreateAsync(factory, world, "BUG", "Двойной щелчок", requestId);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second submission did not wait on the first one's lock.");

        gate.Release();
        var (a, b) = (await first, await second);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.OK), (a.Status, b.Status));
        Assert.Equal(a.Body.GetProperty("id").GetGuid(), b.Body.GetProperty("id").GetGuid());
        Assert.Single(await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
    }

    // US-1 · "An invalid title is refused and nothing is created": a request sent directly to the server is refused with
    // the dialog's own text, under «Заголовок».
    [Theory]
    [InlineData("", "workitems.workitem.title_required", "Введите заголовок.")]
    [InlineData("   ", "workitems.workitem.title_required", "Введите заголовок.")]
    [InlineData(null, "workitems.workitem.title_required", "Введите заголовок.")]
    [InlineData("256", "workitems.workitem.title_too_long", "Не более 255 символов.")]
    [InlineData("Сборка\nпадает", "workitems.workitem.title_multiline", "Заголовок должен быть в одну строку.")]
    public async Task Create_InvalidTitle_Returns400_WithTheFieldReason_NothingCreated(string? title, string reason, string text)
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var sent = title == "256" ? $"  {new string('ж', 256)}  " : title;

        var answer = await WorkItemsTestData.CreateAsync(factory, world, "BUG", sent!);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("workitems.validation_failed", answer.ErrorCode);
        var titleReason = Assert.Single(answer.Body.GetProperty("errors").GetProperty("title").EnumerateArray());
        Assert.Equal((reason, text), (titleReason.GetProperty("code").GetString(), titleReason.GetProperty("detail").GetString()));
        Assert.Empty(await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
    }

    // US-1 · "A title of exactly 255 characters is accepted".
    [Fact]
    public async Task Create_TitleOf255_IsAccepted()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var title = new string('ж', 255);

        var answer = await WorkItemsTestData.CreateAsync(factory, world, "BUG", title);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(title, answer.Body.GetProperty("title").GetString());
    }

    // BR-M3-26: the submission key is required; every field reason comes at once (api-contract § 0.4 step 2).
    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Create_MissingOrMalformedRequestId_Returns400_WithEveryReasonAtOnce(string? requestId)
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world),
            new { requestId, typeId = world.Types["BUG"].Id.ToString(), title = "" },
            OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        var errors = answer.Body.GetProperty("errors");
        Assert.Equal("workitems.workitem.request_id_invalid", errors.GetProperty("requestId")[0].GetProperty("code").GetString());
        Assert.Equal("workitems.workitem.title_required", errors.GetProperty("title")[0].GetProperty("code").GetString());
        Assert.Empty(await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
    }

    // BR-M3-1: the type is one of the organization's process; a malformed, unknown or another organization's type is
    // type_not_found (the dialog's Alert). The values are checked first.
    [Fact]
    public async Task Create_TypeNotOfTheOrganizationsProcess_Returns404TypeNotFound_AfterTheValues()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var other = await WorkItemsTestData.CreateWorldAsync(factory);

        foreach (var typeId in new[] { "abc", Guid.CreateVersion7().ToString(), other.Types["BUG"].Id.ToString(), null })
        {
            var answer = await OrganizationsApi.SendAsync(
                factory,
                HttpMethod.Post,
                WorkItemsTestData.List(world),
                new { requestId = Guid.NewGuid().ToString(), typeId, title = "Баг" },
                OrganizationsTokens.SignedIn());

            Assert.True(answer.Status == HttpStatusCode.NotFound, $"«{typeId}» answered {answer.Status}.");
            Assert.Equal("workitems.workitem.type_not_found", answer.ErrorCode);
            Assert.Equal("Тип рабочего элемента не найден.", answer.Detail);
        }

        var invalidTitleToo = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world),
            new { requestId = Guid.NewGuid().ToString(), typeId = "abc", title = "" },
            OrganizationsTokens.SignedIn());

        Assert.Equal("workitems.validation_failed", invalidTitleToo.ErrorCode);
        Assert.Empty(await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
    }

    // US-1 · "The project is deactivated while the dialog is open": «Проект не найден.» and nothing is created. A project
    // of another organization and a malformed or unknown id answer the same (BR-M3-27).
    [Fact]
    public async Task Create_ProjectDeactivatedUnknownOrForeign_Returns404ProjectNotFound_NothingCreated()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var other = await WorkItemsTestData.CreateWorldAsync(factory);
        await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, world.ProjectId, ProjectStatus.Deactivated);

        foreach (var target in new[]
                 {
                     world,
                     world with { ProjectId = other.ProjectId },
                     world with { ProjectId = Guid.CreateVersion7() },
                 })
        {
            var answer = await WorkItemsTestData.CreateAsync(factory, target, "BUG", "Баг");

            Assert.Equal(HttpStatusCode.NotFound, answer.Status);
            Assert.Equal("organizations.project.not_found", answer.ErrorCode);
            Assert.Equal("Проект не найден.", answer.Detail);
        }

        Assert.Empty(await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
        Assert.Empty(await WorkItemsTestData.StoredAsync(factory, other.ProjectId));
    }

    // spec § Roles and access: an anonymous request gets 401 and creates nothing.
    [Fact]
    public async Task Create_NoToken_Returns401_NothingCreated()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world),
            new { requestId = Guid.NewGuid().ToString(), typeId = world.Types["BUG"].Id.ToString(), title = "Баг" });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Empty(await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
    }
}
