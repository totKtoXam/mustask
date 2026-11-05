using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Process;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// feat-001's data and requests. The stories' items keep their identifiers:
/// they are created through mod-003's create in number order, the numbers the
/// stories do not name taken by filler tasks, so MSK-FEAT-6 is MSK-FEAT-6 and a
/// cycle's path reads as US-2 quotes it. «Удалена» is reached through mod-005's real Delete
/// (<see cref="DeleteAsync"/>); the one set-up that still writes the state straight in the table is an item in
/// «Удалена» with no marks (<see cref="PutInRemovedWithNoMarksAsync"/>).
/// </summary>
internal static class LinksTestData
{
    /// <summary>US-1's items (1–8), US-2's tasks (10–12) and US-4's (20–23), by number; any other number is a filler task.</summary>
    private static readonly IReadOnlyDictionary<int, (string Type, string Title)> StoryItems = new Dictionary<int, (string, string)>
    {
        [1] = ("EPIC", "Вход и регистрация"),
        [2] = ("US", "Письмо со ссылкой"),
        [3] = ("TASK", "Шаблон письма"),
        [4] = ("BUG", "Ссылка истекает раньше срока"),
        [6] = ("FEAT", "Вход по ссылке"),
        [8] = ("FEAT", "Вход через SSO"),
        [10] = ("TASK", "Подготовить шаблон"),
        [11] = ("TASK", "Сверстать письмо"),
        [12] = ("TASK", "Проверить отправку"),
        [20] = ("EPIC", "Отчётность"),
        [21] = ("FEAT", "Аудит входа"),
        [22] = ("US", "Запомнить устройство"),
        [23] = ("FEAT", "Вход по коду"),
    };

    /// <summary>
    /// The project's items 1..<paramref name="upTo"/>, by identifier («MSK-FEAT-6»); <paramref name="overrides"/> names
    /// the items another task's stories add over these (feat-027's MSK-BUG-5, MSK-TASK-7).
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, Item>> CreateItemsAsync(
        MustaskApiFactory factory, WorkItemsWorld world, int upTo, IReadOnlyDictionary<int, (string Type, string Title)>? overrides = null)
    {
        var items = new Dictionary<string, Item>(StringComparer.Ordinal);

        for (var number = 1; number <= upTo; number++)
        {
            var (type, title) = overrides is not null && overrides.TryGetValue(number, out var added) ? added
                : StoryItems.TryGetValue(number, out var known) ? known
                : ("TASK", $"Заполнитель {number}");
            var answer = await WorkItemsTestData.CreateAsync(factory, world, type, title);
            Assert.Equal(HttpStatusCode.Created, answer.Status);

            var item = new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
            Assert.Equal($"{world.Key}-{type}-{number}", item.Identifier);
            items[item.Identifier] = item;
        }

        return items;
    }

    public static string Parent(WorkItemsWorld world, string identifier) => $"{WorkItemsTestData.Item(world, identifier)}/parent";

    public static string Candidates(WorkItemsWorld world, string identifier, string? text = null, string? levelAbove = null)
    {
        var query = new List<string>();

        if (text is not null)
        {
            query.Add($"text={Uri.EscapeDataString(text)}");
        }

        if (levelAbove is not null)
        {
            query.Add($"levelAbove={levelAbove}");
        }

        return $"{WorkItemsTestData.Item(world, identifier)}/parent-candidates" + (query.Count > 0 ? "?" + string.Join('&', query) : string.Empty);
    }

    /// <summary>The parent write as the backlog's menu sends it (api-contract § 3.2).</summary>
    public static Task<Answer> SetParentAsync(
        MustaskApiFactory factory, WorkItemsWorld world, string identifier, Guid? parentId, Guid? seenParentId, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            Parent(world, identifier),
            new { parentId = parentId?.ToString(), seenParentId = seenParentId?.ToString() },
            OrganizationsTokens.SignedIn(),
            gate);

    /// <summary>Gives <paramref name="child"/> the parent from an up-to-date view; fails the test unless it answers 200.</summary>
    public static async Task LinkAsync(MustaskApiFactory factory, WorkItemsWorld world, Item child, Item parent)
    {
        var answer = await SetParentAsync(factory, world, child.Identifier, parent.Id, await StoredParentAsync(factory, child));
        Assert.True(answer.Status == HttpStatusCode.OK, $"Linking {child.Identifier} under {parent.Identifier} answered {answer.Status} {answer.ErrorCode}.");
    }

    public static Task<Answer> CandidatesAsync(MustaskApiFactory factory, WorkItemsWorld world, string identifier, string? text = null, string? levelAbove = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, Candidates(world, identifier, text, levelAbove), token: OrganizationsTokens.SignedIn());

    /// <summary>The identifiers the picker offers, in order; fails the test unless the search answers 200.</summary>
    public static async Task<string[]> OfferedAsync(MustaskApiFactory factory, WorkItemsWorld world, string identifier, string? text = null, string? levelAbove = null)
    {
        var answer = await CandidatesAsync(factory, world, identifier, text, levelAbove);
        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return Identifiers(answer.Body.GetProperty("items"));
    }

    /// <summary>
    /// Deletes the item through the real route, as the organization's administrator (mod-005 BR-DR-18, feat-001 D-8 OQ-1): the
    /// state is «Удалена» and the three marks are set, so the scenarios that used to write the state straight in the table now
    /// start from what a user can do. Fails the test unless the delete answers 200. «Изменён» of the item takes the deletion's time.
    /// </summary>
    public static async Task DeleteAsync(MustaskApiFactory factory, WorkItemsWorld world, Item item)
    {
        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world, item.Identifier)}/delete", token: OrganizationsTokens.SignedIn());

        Assert.True(answer.Status == HttpStatusCode.OK, $"Deleting {item.Identifier} answered {answer.Status} {answer.ErrorCode}.");
    }

    /// <summary>
    /// Puts the item in its type's «Удалена» straight in the table: the state and nothing else, so the item carries no marks
    /// (mod-005 BR-DR-10) — the set-up of the restore of an item with no remembered state, which no route can produce.
    /// </summary>
    public static Task PutInRemovedWithNoMarksAsync(MustaskApiFactory factory, WorkItemsWorld world, Item item) =>
        WorkItemsTestData.ExecuteAsync(
            factory,
            $"UPDATE work_items SET state_id = {world.Types[TypeKeyOf(item)].StateId(StateCategory.Removed)} WHERE id = {item.Id}");

    /// <summary>The item's stored parent, read straight from the table.</summary>
    public static async Task<Guid?> StoredParentAsync(MustaskApiFactory factory, Item item) =>
        (await StoredAsync(factory, item)).ParentId;

    /// <summary>The item's stored parent, state and «Изменён», read straight from the table.</summary>
    public static async Task<(Guid? ParentId, Guid StateId, DateTimeOffset ChangedAt)> StoredAsync(MustaskApiFactory factory, Item item)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var row = await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().WorkItems
            .AsNoTracking()
            .Where(stored => stored.Id == item.Id)
            .Select(stored => new { stored.ParentId, stored.StateId, stored.ChangedAt })
            .SingleAsync();

        return (row.ParentId, row.StateId, row.ChangedAt);
    }

    /// <summary>The identifiers of a <c>WorkItemRef</c> array, in order.</summary>
    public static string[] Identifiers(JsonElement refs) =>
        refs.EnumerateArray().Select(item => item.GetProperty("identifier").GetString()!).ToArray();

    /// <summary>The identifier of a <c>WorkItemRef</c> or <c>null</c>.</summary>
    public static string? IdentifierOf(JsonElement reference) =>
        reference.ValueKind == JsonValueKind.Null ? null : reference.GetProperty("identifier").GetString();

    public static void AssertRefused(Answer answer, HttpStatusCode status, string code)
    {
        Assert.True(answer.Status == status, $"Expected {status} {code}, got {answer.Status} {answer.ErrorCode}.");
        Assert.Equal(code, answer.ErrorCode);
    }

    private static string TypeKeyOf(Item item) => item.Identifier.Split('-')[1];
}
