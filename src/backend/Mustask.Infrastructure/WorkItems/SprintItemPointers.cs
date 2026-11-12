using Mustask.Domain.Process;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// The system writes of mod-007 that touch <c>work_items</c> (BR-6, BR-8): they live here, with every other writer of the table
/// (<c>WorkItemAccessRules</c>), and are set-based, one statement whatever the number of items. They write the sprint pointer and
/// «Изменён» only, never a state or a deletion mark. The caller holds the sprint <c>FOR UPDATE</c> and the items <c>FOR UPDATE</c>.
/// </summary>
internal static class SprintItemPointers
{
    /// <summary>Clears the pointer of every item of the sprint (the removal: only items in «Удалена» are left).</summary>
    public static Task ClearAsync(MustaskDbContext context, Guid sprintId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE work_items SET sprint_id = NULL WHERE sprint_id = {sprintId}",
            cancellationToken);

    /// <summary>
    /// Moves the sprint's items in a Proposed, In Progress or Resolved category to <paramref name="destinationId"/>, or to no
    /// sprint when it is <c>null</c>, and gives them <paramref name="now"/> as «Изменён». Items in Completed and «Удалена» keep the
    /// sprint. The categories are named by the state ids that hold them, read in one statement.
    /// </summary>
    public static async Task MoveUnfinishedAsync(
        MustaskDbContext context, Guid sprintId, Guid? destinationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var unfinished = await context.StateDefinitions
            .AsNoTracking()
            .Where(state => state.Category == StateCategory.Proposed
                || state.Category == StateCategory.InProgress
                || state.Category == StateCategory.Resolved)
            .Select(state => state.Id)
            .ToArrayAsync(cancellationToken);

        // The items themselves are named by id (the raw UPDATE of the table never names a state: WorkItemAccessRules); they are
        // locked by the caller, so the set read here is the set written.
        var ids = await WorkItemSets.InAnyState(context)
            .AsNoTracking()
            .Where(item => item.SprintId == sprintId && unfinished.Contains(item.StateId))
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE work_items SET sprint_id = {destinationId}, changed_at = {now} WHERE id = ANY({ids})",
            cancellationToken);
    }
}
