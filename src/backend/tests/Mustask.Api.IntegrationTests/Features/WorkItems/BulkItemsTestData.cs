using Mustask.Api.IntegrationTests.Features.Boards;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// Items put straight into the table, for the tests that need more of them than the endpoints could create in
/// reasonable time or a shape no write makes any more (feat-027 D-9: a chain at the depth bound, built one call per
/// link, would be 64 round trips per test). The project's number counter is moved past every number used, so the next
/// creation through the endpoint takes the next number.
/// </summary>
internal static class BulkItemsTestData
{
    /// <summary>
    /// A chain of <paramref name="length"/> items of <paramref name="typeKey"/>, each beneath the one before, the first
    /// beneath <paramref name="beneath"/> (or at the top), numbered from <paramref name="firstNumber"/>, in
    /// <paramref name="areaId"/>. Returns the chain from its top item down, with the identifiers the endpoints know.
    /// </summary>
    public static async Task<Item[]> InsertChainAsync(
        MustaskApiFactory factory,
        WorkItemsWorld world,
        Guid areaId,
        int length,
        int firstNumber = 1,
        Guid? beneath = null,
        string typeKey = "TASK")
    {
        var type = world.Types[typeKey];
        var ids = Enumerable.Range(0, length).Select(_ => Guid.CreateVersion7()).ToArray();

        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, parent_id)
            SELECT link.id, {world.ProjectId}, CAST({firstNumber} + link.ord - 1 AS integer), {type.Id}, {type.StateId("Новая")}, {areaId},
                   'Звено ' || link.ord, '', gen_random_uuid(), now(), 'chain-test', now(),
                   CASE WHEN link.ord = 1 THEN CAST({beneath} AS uuid) ELSE (CAST({ids} AS uuid[]))[link.ord - 1] END
            FROM unnest(CAST({ids} AS uuid[])) WITH ORDINALITY AS link(id, ord)
            """);
        await MoveCounterPastAsync(factory, world, firstNumber + length - 1);

        return ids.Select((id, index) => new Item(id, $"{world.Key}-{typeKey}-{firstNumber + index}")).ToArray();
    }

    /// <summary>
    /// <paramref name="assignees"/>' worth of items of <paramref name="typeKey"/> beneath <paramref name="beneath"/> (or
    /// at the top), item k assigned to the k-th user id, numbered from <paramref name="firstNumber"/>, in
    /// <paramref name="areaId"/>. Returns the identifiers in the order of the user ids.
    /// </summary>
    public static async Task<string[]> InsertAssignedAsync(
        MustaskApiFactory factory,
        WorkItemsWorld world,
        Guid areaId,
        IReadOnlyList<string> assignees,
        int firstNumber,
        Guid? beneath = null,
        string typeKey = "TASK")
    {
        var type = world.Types[typeKey];

        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, parent_id, assignee_id)
            SELECT gen_random_uuid(), {world.ProjectId}, CAST({firstNumber} + assigned.ord - 1 AS integer), {type.Id}, {type.StateId("Новая")}, {areaId},
                   'Исполнитель ' || assigned.ord, '', gen_random_uuid(), now(), 'assignee-test', now(), CAST({beneath} AS uuid), assigned.assignee
            FROM unnest(CAST({assignees.ToArray()} AS text[])) WITH ORDINALITY AS assigned(assignee, ord)
            """);
        await MoveCounterPastAsync(factory, world, firstNumber + assignees.Count - 1);

        return Enumerable.Range(0, assignees.Count).Select(index => $"{world.Key}-{typeKey}-{firstNumber + index}").ToArray();
    }

    private static Task MoveCounterPastAsync(MustaskApiFactory factory, WorkItemsWorld world, int lastNumber) =>
        WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_item_counters (project_id, last_number) VALUES ({world.ProjectId}, {lastNumber})
            ON CONFLICT (project_id) DO UPDATE SET last_number = GREATEST(work_item_counters.last_number, excluded.last_number)
            """);
}
