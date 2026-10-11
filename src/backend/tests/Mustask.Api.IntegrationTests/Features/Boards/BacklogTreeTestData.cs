using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// The backlog tree's data — feat-027 US-1's, which is feat-001 US-4's plus
/// MSK-BUG-5, MSK-TASK-3 and MSK-TASK-7: mod-006's world, the stories' items with
/// their identifiers (<see cref="LinksTestData.CreateItemsAsync"/>, numbers 5 and
/// 7 named by feat-027), those of «Платформа» moved into its area, MSK-FEAT-21
/// and MSK-TASK-7 left in the root («Мастаск»'s), and the links made through the
/// parent write.
/// </summary>
internal static class BacklogTreeTestData
{
    /// <summary>feat-027 US-1's items over feat-001's numbering.</summary>
    private static readonly IReadOnlyDictionary<int, (string Type, string Title)> Feat027Items = new Dictionary<int, (string, string)>
    {
        [5] = ("BUG", "Письмо не доходит"),
        [7] = ("TASK", "Настроить SMTP"),
    };

    /// <summary>The items of «Платформа»: every story item but MSK-FEAT-21 and MSK-TASK-7 (team «Мастаск»).</summary>
    private static readonly string[] PlatformItems =
    [
        "MSK-EPIC-1", "MSK-US-2", "MSK-TASK-3", "MSK-BUG-4", "MSK-BUG-5", "MSK-FEAT-6", "MSK-FEAT-8", "MSK-EPIC-20", "MSK-US-22",
        "MSK-FEAT-23",
    ];

    /// <summary>
    /// US-1's data: MSK-EPIC-1 is the parent of MSK-FEAT-8, MSK-FEAT-6, MSK-FEAT-21 and MSK-US-22; MSK-FEAT-8 of
    /// MSK-FEAT-23; MSK-FEAT-6 of MSK-US-2 and MSK-BUG-4; MSK-US-2 of MSK-BUG-5, MSK-TASK-3 and MSK-TASK-7. The «Фичи»
    /// backlog of «Платформа» reads MSK-FEAT-8, MSK-FEAT-6, MSK-FEAT-23; every other backlog is in number order.
    /// </summary>
    public static async Task<(BoardsWorld World, IReadOnlyDictionary<string, Item> Items)> CreateWorldAsync(MustaskApiFactory factory)
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var items = await LinksTestData.CreateItemsAsync(factory, world.Items, upTo: 23, Feat027Items);

        foreach (var identifier in PlatformItems)
        {
            await BoardsTestData.MoveToAreaAsync(factory, world, items[identifier], world.PlatformAreaId);
        }

        foreach (var (child, parent) in new[]
        {
            ("MSK-FEAT-6", "MSK-EPIC-1"), ("MSK-FEAT-8", "MSK-EPIC-1"), ("MSK-FEAT-21", "MSK-EPIC-1"),
            ("MSK-US-2", "MSK-FEAT-6"), ("MSK-BUG-4", "MSK-FEAT-6"), ("MSK-FEAT-23", "MSK-FEAT-8"), ("MSK-US-22", "MSK-EPIC-1"),
            ("MSK-BUG-5", "MSK-US-2"), ("MSK-TASK-3", "MSK-US-2"), ("MSK-TASK-7", "MSK-US-2"),
        })
        {
            await LinksTestData.LinkAsync(factory, world.Items, items[child], items[parent]);
        }

        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Top(items["MSK-FEAT-8"]), "features");

        return (world, items);
    }

    /// <summary>The backlog's top-level rows; fails the test unless the read answers 200.</summary>
    public static async Task<JsonElement[]> TopRowsAsync(MustaskApiFactory factory, BoardsWorld world, Guid teamId, string level)
    {
        var answer = await BoardsTestData.GetBacklogAsync(factory, world, teamId, level);
        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return answer.Body.GetProperty("items").EnumerateArray().ToArray();
    }

    /// <summary>
    /// The backlog as one line: the rows by identifier, each followed by its
    /// nested rows in brackets — «MSK-EPIC-1[MSK-FEAT-8 MSK-FEAT-6[MSK-US-2 MSK-BUG-4]] MSK-EPIC-20».
    /// </summary>
    public static async Task<string> ShapeAsync(MustaskApiFactory factory, BoardsWorld world, Guid teamId, string level) =>
        Shape(await TopRowsAsync(factory, world, teamId, level));

    public static string Shape(IEnumerable<JsonElement> rows) =>
        string.Join(' ', rows.Select(row =>
        {
            var children = row.GetProperty("children").EnumerateArray().ToArray();
            var identifier = row.GetProperty("identifier").GetString();

            return children.Length == 0 ? identifier : $"{identifier}[{Shape(children)}]";
        }));

    /// <summary>Every row of the tree, top-level and nested, depth first.</summary>
    public static IEnumerable<JsonElement> AllRows(IEnumerable<JsonElement> rows) =>
        rows.SelectMany(row => new[] { row }.Concat(AllRows(row.GetProperty("children").EnumerateArray())));

    /// <summary>The row of that identifier, wherever it is in the tree.</summary>
    public static JsonElement Row(IEnumerable<JsonElement> rows, string identifier) =>
        AllRows(rows).Single(row => row.GetProperty("identifier").GetString() == identifier);

    /// <summary>The «Родитель» cell of a row: the parent's identifier, or <c>null</c> for «Нет».</summary>
    public static string? ParentOf(JsonElement row) => LinksTestData.IdentifierOf(row.GetProperty("parent"));

    /// <summary>The create request as a row's «Добавить: <Тип>» sends it (api-contract § 3): the parent, no team.</summary>
    public static Task<Answer> CreateChildAsync(
        MustaskApiFactory factory, BoardsWorld world, string typeKey, string title, string? parentId, Guid? requestId = null, string? teamId = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new { requestId = (requestId ?? Guid.NewGuid()).ToString(), typeId = world.Items.Types[typeKey].Id.ToString(), title, parentId, teamId },
            OrganizationsTokens.SignedIn());
}
