using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// <see cref="IWorkItemReadModel"/>. Each read is a fixed set of queries,
/// <c>AsNoTracking</c> with projections: the items joined to their project,
/// type and state (one statement; the list adds its count), then the project's
/// area nodes, whose paths <see cref="AreaTree"/> computes exactly as mod-002's
/// tree read does (DC-9), then one lookup of the page's assignees through
/// mod-014's <see cref="IUserDirectory"/>, never its tables.
/// </summary>
internal sealed class WorkItemReadModel(MustaskDbContext context, IUserDirectory directory) : IWorkItemReadModel
{
    public async Task<WorkItemView?> FindAsync(Guid projectId, int number, CancellationToken cancellationToken)
    {
        var row = await Rows(context.WorkItems.Where(item => item.ProjectId == projectId && item.Number == number))
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : await ViewAsync(row, cancellationToken);
    }

    public async Task<WorkItemView> GetAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        var row = await Rows(context.WorkItems.Where(item => item.Id == workItemId)).SingleAsync(cancellationToken);

        return await ViewAsync(row, cancellationToken);
    }

    public async Task<WorkItemPage> GetPageAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var count = await context.WorkItems.CountAsync(item => item.ProjectId == projectId, cancellationToken);

        // The offset is computed in long: a page far past the end must not overflow into a negative OFFSET.
        var skip = (long)(page - 1) * pageSize;

        if (skip >= count)
        {
            return new WorkItemPage(count, []);
        }

        // The order and the page are taken in SQL, on the items themselves; the joins then only dress the page's rows.
        var pageItems = context.WorkItems
            .Where(item => item.ProjectId == projectId)
            .OrderByDescending(item => item.ChangedAt)
            .ThenByDescending(item => item.Number)
            .Skip((int)skip)
            .Take(pageSize);

        var rows = await Rows(pageItems, includeDescription: false).ToListAsync(cancellationToken);

        var paths = await AreaPathsAsync(projectId, cancellationToken);
        var assignees = await AssigneesAsync(rows, cancellationToken);

        // The joins do not promise the page's order, so the page, already narrowed in SQL, is put back in it here.
        var items = rows
            .OrderByDescending(row => row.ChangedAt)
            .ThenByDescending(row => row.Number)
            .Select(row => new WorkItemListItemView(
                row.Id,
                Identifier(row),
                row.Number,
                Type(row),
                row.Title,
                Assignee(row, assignees),
                State(row),
                new WorkItemAreaView(row.AreaId, paths.PathOf(row.AreaId)),
                row.ChangedAt))
            .ToList();

        return new WorkItemPage(count, items);
    }

    /// <summary>
    /// <paramref name="items"/> with their project key, type and state, in one
    /// statement. The list leaves the description out: it can hold 100 000
    /// characters a row and the list never shows it.
    /// </summary>
    private IQueryable<ItemRow> Rows(IQueryable<WorkItem> items, bool includeDescription = true) =>
        from item in items.AsNoTracking()
        join project in context.Projects on item.ProjectId equals project.Id
        join type in context.WorkItemTypes on item.TypeId equals type.Id
        join state in context.StateDefinitions on item.StateId equals state.Id
        select new ItemRow(
            item.Id,
            item.ProjectId,
            project.Key,
            item.Number,
            type.Id,
            type.Key,
            type.Name,
            type.Appearance,
            state.Id,
            state.Name,
            state.Category,
            item.AssigneeId,
            item.AreaId,
            item.Title,
            includeDescription ? item.Description : string.Empty,
            item.CreatedBy,
            item.CreatedAt,
            item.ChangedAt);

    private async Task<WorkItemView> ViewAsync(ItemRow row, CancellationToken cancellationToken)
    {
        var paths = await AreaPathsAsync(row.ProjectId, cancellationToken);
        var assignees = await AssigneesAsync([row], cancellationToken);

        return new WorkItemView(
            row.Id,
            Identifier(row),
            row.Number,
            row.ProjectId,
            Type(row),
            row.Title,
            row.Description,
            State(row),
            Assignee(row, assignees),
            new WorkItemAreaView(row.AreaId, paths.PathOf(row.AreaId)),
            row.CreatedBy,
            row.CreatedAt,
            row.ChangedAt);
    }

    private async Task<AreaTree> AreaPathsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var nodes = await context.Areas
            .AsNoTracking()
            .Where(area => area.ProjectId == projectId)
            .Select(area => new AreaNode(area.Id, area.ParentId, area.Name, area.OwnerTeamId, area.DefaultOfTeamId))
            .ToListAsync(cancellationToken);

        return new AreaTree(nodes);
    }

    private async Task<IReadOnlyDictionary<string, DirectoryUser>> AssigneesAsync(IReadOnlyCollection<ItemRow> rows, CancellationToken cancellationToken)
    {
        var ids = rows.Select(row => row.AssigneeId).OfType<string>().Distinct(StringComparer.Ordinal).ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<string, DirectoryUser>(StringComparer.Ordinal);
        }

        return (await directory.LookupAsync(ids, cancellationToken)).Known;
    }

    private static string Identifier(ItemRow row) => WorkItemIdentifier.Format(row.ProjectKey, row.TypeKey, row.Number);

    private static WorkItemTypeView Type(ItemRow row) => new(row.TypeId, row.TypeKey, row.TypeName, row.TypeAppearance.ToWireName());

    private static WorkItemStateView State(ItemRow row) =>
        new(row.StateId, row.StateName, new WorkItemStateCategoryView(row.StateCategory.ToString(), StateCategoryLabels.Of(row.StateCategory)));

    /// <summary>An id the directory does not know keeps its id and has no name (US-2 «Неизвестный пользователь»).</summary>
    private static WorkItemAssigneeView? Assignee(ItemRow row, IReadOnlyDictionary<string, DirectoryUser> known) =>
        row.AssigneeId switch
        {
            null => null,
            { } id when known.TryGetValue(id, out var user) => new WorkItemAssigneeView(id, user.DisplayName, user.Email),
            { } id => new WorkItemAssigneeView(id, null, null),
        };

    private sealed record ItemRow(
        Guid Id,
        Guid ProjectId,
        string ProjectKey,
        int Number,
        Guid TypeId,
        string TypeKey,
        string TypeName,
        AppearanceToken TypeAppearance,
        Guid StateId,
        string StateName,
        StateCategory StateCategory,
        string? AssigneeId,
        Guid AreaId,
        string Title,
        string Description,
        string CreatedBy,
        DateTimeOffset CreatedAt,
        DateTimeOffset ChangedAt);
}
