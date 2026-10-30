using System.Linq.Expressions;
using Mustask.Domain.Process;

namespace Mustask.Domain.WorkItems;

/// <summary>
/// ADR-0030 rule 2 as specifications: which items are «live», which are «deleted», and which a team owns, decided in the domain and
/// unit-testable without a database. The persistence layer names the <c>work_items</c> set in exactly one place
/// (<c>WorkItemSets</c>) and applies these to it; there is still no global query filter (ADR-0026 rule 4). "Deleted" is the item's
/// state being in the Removed category, one of the three expressions <c>DeletedStateParityTests</c> pins equal.
/// </summary>
public static class WorkItemSetRules
{
    /// <summary>A state of the Removed category («Удалена»), the category and never the name.</summary>
    public static Expression<Func<StateDefinition, bool>> RemovedState { get; } = state => state.Category == StateCategory.Removed;

    /// <summary>The same decision on a category in memory.</summary>
    public static bool IsRemoved(StateCategory category) => category == StateCategory.Removed;

    /// <summary>An item outside the Removed category: <paramref name="removedStateIds"/> is the subquery of the removed states.</summary>
    public static Expression<Func<WorkItem, bool>> Live(IQueryable<Guid> removedStateIds) =>
        item => !removedStateIds.Contains(item.StateId);

    /// <summary>An item in the Removed category.</summary>
    public static Expression<Func<WorkItem, bool>> Deleted(IQueryable<Guid> removedStateIds) =>
        item => removedStateIds.Contains(item.StateId);

    /// <summary>An item of one project.</summary>
    public static Expression<Func<WorkItem, bool>> OfProject(Guid projectId) => item => item.ProjectId == projectId;
}
