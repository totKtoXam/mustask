using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// mod-005 (ADR-0030 rule 2): the named entry points of every read of <c>work_items</c> — the one place the context's set is
/// named (the text rule <c>WorkItemAccessRules</c> keeps it so, with the two backlog memberships as the allow-listed
/// exceptions), instead of a global query filter, which ADR-0026 rule 4 and ADR-0029 § 7 rule out. A read that returns or
/// counts a set of items starts from one of them, so it states its intent by name:
/// <list type="bullet">
/// <item><see cref="Live"/> is the default: lists, pickers, children, checklists, counts, queries.</item>
/// <item><see cref="Deleted"/> is for a filter that names «Удалена» (feat-003); this line has no caller.</item>
/// <item><see cref="InAnyState"/> is for the card by address, a live child's stored parent, a team's owned count, Restore,
/// Purge, the clean-ups and the locks.</item>
/// </list>
/// "Deleted" is the item's state being in the Removed category — one of the three expressions of the one definition that
/// <c>DeletedStateParityTests</c> pins equal (in memory <see cref="WorkItemTypeStates.IsRemoved"/>, here in LINQ, and in
/// the backlogs' raw SQL and memberships, which take the scope's shown-state ids). The queries are not <c>AsNoTracking</c>:
/// the caller adds it.
/// </summary>
internal static class WorkItemSets
{
    /// <summary>The ids of every state of the Removed category («Удалена»), as a subquery: the one LINQ copy of the definition.</summary>
    public static IQueryable<Guid> RemovedStateIds(MustaskDbContext context) =>
        context.StateDefinitions.Where(state => state.Category == StateCategory.Removed).Select(state => state.Id);

    /// <summary>Every work item, whatever its state.</summary>
    public static IQueryable<WorkItem> InAnyState(MustaskDbContext context) => context.WorkItems;

    /// <summary>
    /// <see cref="InAnyState"/> over a raw SQL source (the recursive ancestry read, which LINQ cannot say). A raw read is
    /// never filtered, so it has no other entry point; the SQL names the columns after the properties.
    /// </summary>
    public static IQueryable<WorkItem> InAnyStateFromSql(MustaskDbContext context, FormattableString sql) =>
        context.WorkItems.FromSqlInterpolated(sql);

    /// <summary>The work items outside the Removed category: a work item list, a picker, a count — the default.</summary>
    public static IQueryable<WorkItem> Live(MustaskDbContext context)
    {
        var removed = RemovedStateIds(context);

        return context.WorkItems.Where(item => !removed.Contains(item.StateId));
    }

    /// <summary>The work items in the Removed category: «Удалена» (the category, never the name).</summary>
    public static IQueryable<WorkItem> Deleted(MustaskDbContext context)
    {
        var removed = RemovedStateIds(context);

        return context.WorkItems.Where(item => removed.Contains(item.StateId));
    }
}
