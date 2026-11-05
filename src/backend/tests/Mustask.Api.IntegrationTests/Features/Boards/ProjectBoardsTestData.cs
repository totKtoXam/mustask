using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// The project backlog's and board's requests and reads (feat-039): the routes beside the team's, under
/// <c>/projects/{projectId}</c> with no team. The world is mod-006's (<see cref="BoardsTestData.CreateWorldAsync"/>):
/// project «Мастаск» with the first team «Мастаск» (the root area) and «Платформа» (its area and «API» beneath it), which
/// stand in for US-1's MARS and VENUS.
/// </summary>
internal static class ProjectBoardsTestData
{
    public static string Project(BoardsWorld world) => $"/api/organizations/{world.OrganizationId}/projects/{world.ProjectId}";

    public static string Backlog(BoardsWorld world, string level) => $"{Project(world)}/backlogs/{level}";

    public static string Board(BoardsWorld world, string level) => $"{Project(world)}/boards/{level}";

    public static Task<Answer> GetBacklogAsync(MustaskApiFactory factory, BoardsWorld world, string level = "stories", string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, Backlog(world, level), token: token ?? OrganizationsTokens.SignedIn());

    public static Task<Answer> GetBoardAsync(MustaskApiFactory factory, BoardsWorld world, string level = "stories", string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, Board(world, level), token: token ?? OrganizationsTokens.SignedIn());

    /// <summary>The backlog's top-level rows by identifier, in order; fails the test unless the read answers 200 with «Порядок» 1..n.</summary>
    public static async Task<string[]> RowsAsync(MustaskApiFactory factory, BoardsWorld world, string level = "stories")
    {
        var answer = await GetBacklogAsync(factory, world, level);
        Assert.Equal(HttpStatusCode.OK, answer.Status);

        var items = answer.Body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(Enumerable.Range(1, items.Count), items.Select(item => item.GetProperty("order").GetInt32()));

        return items.Select(item => item.GetProperty("identifier").GetString()!).ToArray();
    }

    /// <summary>The backlog as one line, the rows with their nested rows in brackets (<see cref="BacklogTreeTestData.Shape"/>).</summary>
    public static async Task<string> ShapeAsync(MustaskApiFactory factory, BoardsWorld world, string level = "stories")
    {
        var answer = await GetBacklogAsync(factory, world, level);
        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return BacklogTreeTestData.Shape(answer.Body.GetProperty("items").EnumerateArray());
    }

    /// <summary>A reorder of the project's backlog as the panel sends it.</summary>
    public static Task<Answer> ReorderAsync(
        MustaskApiFactory factory, BoardsWorld world, object body, string level = "stories", CommitGate? gate = null, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{Backlog(world, level)}/reorder", body, token ?? OrganizationsTokens.SignedIn(), gate);

    /// <summary>Reorders and fails the test unless the reorder answers 204.</summary>
    public static async Task MoveAsync(MustaskApiFactory factory, BoardsWorld world, object body, string level = "stories")
    {
        var answer = await ReorderAsync(factory, world, body, level);
        Assert.True(answer.Status == HttpStatusCode.NoContent, $"The project reorder answered {answer.Status} {answer.ErrorCode}.");
    }

    /// <summary>Puts the project's backlog in exactly <paramref name="order"/> by moving each item to the bottom in turn.</summary>
    public static async Task ReorderToAsync(MustaskApiFactory factory, BoardsWorld world, params Item[] order)
    {
        foreach (var item in order)
        {
            await MoveAsync(factory, world, BoardsTestData.Bottom(item));
        }
    }

    /// <summary>The project's places, straight from the table, by item.</summary>
    public static async Task<List<StoredProjectPlace>> PlacesAsync(MustaskApiFactory factory, BoardsWorld world)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Database
            .SqlQuery<StoredProjectPlace>(
                $"SELECT work_item_id AS \"ItemId\", position AS \"Position\" FROM project_backlog_places WHERE project_id = {world.ProjectId} ORDER BY work_item_id")
            .ToListAsync();
    }

    /// <summary>Every team place of the project's items, straight from the table, by item.</summary>
    public static async Task<List<StoredTeamPlace>> TeamPlacesAsync(MustaskApiFactory factory, BoardsWorld world)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Database
            .SqlQuery<StoredTeamPlace>(
                $"""
                 SELECT place.work_item_id AS "ItemId", place.team_id AS "TeamId", place.position AS "Position"
                 FROM backlog_places place JOIN work_items item ON item.id = place.work_item_id
                 WHERE item.project_id = {world.ProjectId} ORDER BY place.work_item_id
                 """)
            .ToListAsync();
    }

    /// <summary>The identifiers of the project's backlog in the order a place list gives them.</summary>
    public static string[] ByPosition(IEnumerable<StoredProjectPlace> places, params Item[] items) =>
        places
            .Join(items, place => place.ItemId, item => item.Id, (place, item) => (place.Position, item.Identifier))
            .OrderBy(pair => pair.Position)
            .Select(pair => pair.Identifier)
            .ToArray();

    /// <summary>Sets the item's creation time, for what no endpoint does (the order of unplaced items, BR-PB-4).</summary>
    public static Task SetCreatedAtAsync(MustaskApiFactory factory, Item item, DateTimeOffset createdAt) =>
        WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET created_at = {createdAt} WHERE id = {item.Id}");

    /// <summary>The cards of every column of a board answer, by identifier, in column order then card order.</summary>
    public static string[] AllCards(System.Text.Json.JsonElement board) =>
        board.GetProperty("columns").EnumerateArray().SelectMany(column => BoardsTestData.Cards(column)).ToArray();
}

/// <summary>A project place as the table holds it.</summary>
internal sealed record StoredProjectPlace(Guid ItemId, int Position);

/// <summary>A team place as the table holds it.</summary>
internal sealed record StoredTeamPlace(Guid ItemId, Guid TeamId, int Position);
