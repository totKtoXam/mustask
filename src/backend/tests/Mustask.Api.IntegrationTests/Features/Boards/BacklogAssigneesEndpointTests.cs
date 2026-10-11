using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.UserDirectory;
using Mustask.Api.IntegrationTests.Features.WorkItems;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// The assignees of a backlog page or a board (feat-027 review B-3; mod-014 api-contract § A.5): a page whose rows hold
/// more distinct assignees than the directory takes in one read (500) is looked up in batches and answers 200 with
/// every name — it used to answer 400 <c>user_directory.too_many_ids</c> to everyone on the team. The page's tree set
/// holds every level of the page and the «Задачи» level, so a team of a few hundred people crosses 500 in the ordinary
/// course. The items are put straight into the table: through the endpoints the setup alone would take minutes.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class BacklogAssigneesEndpointTests(MustaskApiFactory factory)
{
    private const int Distinct = 520;

    /// <summary><paramref name="count"/> users in the directory under fresh ids, named «Имя n Фамилия n».</summary>
    private async Task<string[]> SeedUsersAsync(int count)
    {
        var ids = Enumerable.Range(0, count).Select(_ => UserDirectoryTestData.NewId()).ToArray();
        await UserDirectoryTestData.SeedAsync(
            factory,
            ids.Select((id, index) => UserDirectoryTestData.Push(id, $"Имя{index + 1}", $"Фамилия{index + 1}", $"user{index + 1}-{id}@example.test")));

        return ids;
    }

    // review B-3 · «Истории» with 520 distinct assignees among its tasks (beneath one story) and 3 the directory does not
    // know: 200, every task carries its assignee's name, an unknown assignee keeps its id and has no name. With one
    // lookup of all of them this answered 400 `user_directory.too_many_ids`.
    [Fact]
    public async Task GetBacklog_MoreThan500DistinctAssignees_IsAnsweredWhole_EveryNameResolved()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        var users = await SeedUsersAsync(Distinct);
        var unknown = Enumerable.Range(1, 3).Select(index => $"unknown-user-{index}-{Guid.NewGuid()}").ToArray();
        await BulkItemsTestData.InsertAssignedAsync(
            factory, world.Items, world.PlatformAreaId, [.. users, .. unknown], firstNumber: 2, beneath: story.Id);

        var answer = await BoardsTestData.GetBacklogAsync(factory, world, world.PlatformId, "stories");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var row = Assert.Single(answer.Body.GetProperty("items").EnumerateArray());
        var tasks = row.GetProperty("children").EnumerateArray().ToList();
        Assert.Equal(Distinct + unknown.Length, tasks.Count);

        var named = tasks.Take(Distinct).Select(task => task.GetProperty("assignee")).ToList();
        Assert.Equal(users, named.Select(assignee => assignee.GetProperty("id").GetString()));
        Assert.Equal(
            Enumerable.Range(1, Distinct).Select(index => $"Имя{index} Фамилия{index}"),
            named.Select(assignee => assignee.GetProperty("displayName").GetString()));

        var unnamed = tasks.Skip(Distinct).Select(task => task.GetProperty("assignee")).ToList();
        Assert.Equal(unknown, unnamed.Select(assignee => assignee.GetProperty("id").GetString()));
        Assert.All(unnamed, assignee => Assert.Equal(JsonValueKind.Null, assignee.GetProperty("displayName").ValueKind));
    }

    // review B-3 · the board reads through the same statements: 505 stories of one team with 505 distinct assignees are
    // each one card, with the name of its assignee.
    [Fact]
    public async Task GetBoard_MoreThan500DistinctAssignees_IsAnsweredWhole()
    {
        const int stories = 505;
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var users = await SeedUsersAsync(stories);
        await BulkItemsTestData.InsertAssignedAsync(factory, world.Items, world.PlatformAreaId, users, firstNumber: 1, typeKey: "US");

        var answer = await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId, "stories");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var cards = answer.Body.GetProperty("columns").EnumerateArray()
            .SelectMany(column => column.GetProperty("items").EnumerateArray())
            .ToList();
        Assert.Equal(stories, cards.Count);
        Assert.Equal(
            users.Order(StringComparer.Ordinal),
            cards.Select(card => card.GetProperty("assignee").GetProperty("id").GetString()!).Order(StringComparer.Ordinal));
        Assert.All(cards, card => Assert.NotEqual(JsonValueKind.Null, card.GetProperty("assignee").GetProperty("displayName").ValueKind));
    }
}
