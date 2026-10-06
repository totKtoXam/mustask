using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;

namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// feat-001's link end (<see cref="WorkItemRefView"/>, api-contract § 2.1) as
/// every reader builds it — the form's parent and children, the picker, the
/// backlog's «Родитель» cells: one projection of the items with their project
/// key, type, level rank and state, and one mapping into the wire shape.
/// </summary>
internal static class WorkItemRefs
{
    /// <summary><paramref name="items"/> as link ends, in one statement.</summary>
    public static IQueryable<WorkItemRefRow> Rows(MustaskDbContext context, IQueryable<WorkItem> items) =>
        from item in items
        join project in context.Projects on item.ProjectId equals project.Id
        join type in context.WorkItemTypes on item.TypeId equals type.Id
        join level in context.BacklogLevels on type.BacklogLevelId equals level.Id
        join state in context.StateDefinitions on item.StateId equals state.Id
        select new WorkItemRefRow(
            item.Id,
            project.Key,
            item.Number,
            item.ParentId,
            type.Id,
            type.Key,
            type.Name,
            type.Appearance,
            level.Rank,
            item.Title,
            state.Id,
            state.Name,
            state.Category,
            item.ChangedAt);

    /// <summary>The ids of every state of the Removed category («Удалена»), as a subquery.</summary>
    public static IQueryable<Guid> RemovedStateIds(MustaskDbContext context) =>
        context.StateDefinitions.Where(state => state.Category == StateCategory.Removed).Select(state => state.Id);

    public static WorkItemRefView View(WorkItemRefRow row) =>
        new(
            row.Id,
            WorkItemIdentifier.Format(row.ProjectKey, row.TypeKey, row.Number),
            new WorkItemTypeView(row.TypeId, row.TypeKey, row.TypeName, row.TypeAppearance.ToWireName()),
            row.Title,
            new WorkItemStateView(
                row.StateId,
                row.StateName,
                new WorkItemStateCategoryView(row.StateCategory.ToString(), StateCategoryLabels.Of(row.StateCategory))));
}

/// <summary>What <see cref="WorkItemRefs.Rows"/> reads of one item.</summary>
internal sealed record WorkItemRefRow(
    Guid Id,
    string ProjectKey,
    int Number,
    Guid? ParentId,
    Guid TypeId,
    string TypeKey,
    string TypeName,
    AppearanceToken TypeAppearance,
    int LevelRank,
    string Title,
    Guid StateId,
    string StateName,
    StateCategory StateCategory,
    DateTimeOffset ChangedAt);
