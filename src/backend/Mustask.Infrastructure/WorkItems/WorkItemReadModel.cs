using System.Globalization;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// <see cref="IWorkItemReadModel"/>. Each read is a fixed set of queries,
/// <c>AsNoTracking</c> with projections: the items joined to their project,
/// type and state (one statement; the list adds its count), then the project's
/// area nodes, whose paths <see cref="AreaTree"/> computes exactly as mod-002's
/// tree read does (DC-9), then one lookup of the page's assignees through
/// mod-014's <see cref="IUserDirectory"/>, never its tables. One item adds one
/// statement for its links (feat-001): its parent and its children together.
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

    public Task<WorkItemHead?> FindHeadAsync(Guid projectId, int number, CancellationToken cancellationToken) =>
        context.WorkItems
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.Number == number)
            .Select(item => new WorkItemHead(item.Id, item.TypeId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<WorkItemRefView> GetRefAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        var row = await RefRows(context.WorkItems.Where(item => item.Id == workItemId)).SingleAsync(cancellationToken);

        return Ref(row);
    }

    public async Task<ParentCandidates> FindParentCandidatesAsync(ParentCandidateSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentOutOfRangeException.ThrowIfLessThan(search.Limit, 1);

        var typeIds = search.TypeIds.ToList();
        var removed = StateCategory.Removed;

        var candidates =
            from item in context.WorkItems
            join type in context.WorkItemTypes on item.TypeId equals type.Id
            join state in context.StateDefinitions on item.StateId equals state.Id
            where item.ProjectId == search.ProjectId
                && item.Id != search.ExcludedItemId
                && typeIds.Contains(item.TypeId)
                && state.Category != removed
            select new { Item = item, TypeKey = type.Key };

        if (search.Text.Length > 0)
        {
            // Letter case is folded by ICU (the collation the project's lists sort by), so Cyrillic folds as Latin does
            // whatever the database's own locale; the identifier is ASCII and built as the API writes it.
            var text = search.Text;
            var prefix = search.ProjectKey + "-";

            candidates = candidates.Where(candidate =>
                EF.Functions.Collate(candidate.Item.Title, OrganizationsCollation.Russian).ToLower().Contains(text)
                || (prefix + candidate.TypeKey + "-" + candidate.Item.Number.ToString()).ToLower().Contains(text));
        }

        // One more than the limit says whether more matched; the order and the cut are taken in SQL.
        var page = candidates
            .OrderByDescending(candidate => candidate.Item.ChangedAt)
            .ThenByDescending(candidate => candidate.Item.Number)
            .Take(search.Limit + 1)
            .Select(candidate => candidate.Item);

        var rows = await RefRows(page).ToListAsync(cancellationToken);

        // The joins do not promise the page's order, so the page, already narrowed in SQL, is put back in it here.
        var items = rows
            .OrderByDescending(row => row.ChangedAt)
            .ThenByDescending(row => row.Number)
            .Take(search.Limit)
            .Select(Ref)
            .ToList();

        return new ParentCandidates(items, rows.Count > search.Limit);
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
    /// <paramref name="items"/> with their project key, type and state, and
    /// feat-022's fields, in one statement. The list leaves the description and
    /// the type's texts out: each can hold 100 000 characters a row and the list
    /// never shows them.
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
            item.ParentId,
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
            item.ChangedAt,
            new FieldsRow(
                item.Priority,
                item.Risk,
                item.StoryPoints,
                item.Effort,
                item.BusinessValue,
                item.TimeCriticality,
                item.StartDate,
                item.TargetDate,
                item.Severity,
                item.Activity,
                item.OriginalEstimate,
                item.RemainingWork,
                item.CompletedWork,
                item.ValueArea,
                includeDescription ? item.AcceptanceCriteria : null,
                includeDescription ? item.ReproSteps : null,
                includeDescription ? item.SystemInfo : null));

    /// <summary><paramref name="items"/> as link ends, with their project key, type, level rank and state, in one statement.</summary>
    private IQueryable<WorkItemRefRow> RefRows(IQueryable<WorkItem> items) => WorkItemRefs.Rows(context, items.AsNoTracking());

    /// <summary>
    /// feat-001: the item's stored parent, whatever its state (BR-H-8), and its
    /// direct children outside «Удалена» (BR-H-7), in one statement; the children
    /// by level rank descending, then by number.
    /// </summary>
    private async Task<(WorkItemRefView? Parent, IReadOnlyList<WorkItemRefView> Children)> LinksAsync(ItemRow row, CancellationToken cancellationToken)
    {
        var parentId = row.ParentId;
        var removedStates = WorkItemRefs.RemovedStateIds(context);

        var links = await RefRows(context.WorkItems.Where(item =>
                item.Id == parentId
                || (item.ProjectId == row.ProjectId && item.ParentId == row.Id && !removedStates.Contains(item.StateId))))
            .ToListAsync(cancellationToken);

        var parent = parentId is { } id ? links.Single(link => link.Id == id) : null;

        var children = links
            .Where(link => link.ParentId == row.Id)
            .OrderByDescending(link => link.LevelRank)
            .ThenBy(link => link.Number)
            .Select(Ref)
            .ToList();

        return (parent is null ? null : Ref(parent), children);
    }

    private async Task<WorkItemView> ViewAsync(ItemRow row, CancellationToken cancellationToken)
    {
        var paths = await AreaPathsAsync(row.ProjectId, cancellationToken);
        var assignees = await AssigneesAsync([row], cancellationToken);
        var (parent, children) = await LinksAsync(row, cancellationToken);

        var fields = row.Fields;

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
            row.ChangedAt,
            parent,
            children)
        {
            Priority = fields.Priority,
            Risk = fields.Risk,
            StoryPoints = Shortest(fields.StoryPoints),
            Effort = Shortest(fields.Effort),
            BusinessValue = fields.BusinessValue,
            TimeCriticality = Shortest(fields.TimeCriticality),
            StartDate = fields.StartDate,
            TargetDate = fields.TargetDate,
            Severity = fields.Severity,
            Activity = fields.Activity?.ToString(),
            OriginalEstimate = Shortest(fields.OriginalEstimate),
            RemainingWork = Shortest(fields.RemainingWork),
            CompletedWork = Shortest(fields.CompletedWork),
            ValueArea = fields.ValueArea?.ToString(),
            AcceptanceCriteria = fields.AcceptanceCriteria,
            ReproSteps = fields.ReproSteps,
            SystemInfo = fields.SystemInfo,
        };
    }

    /// <summary>
    /// feat-022 api-contract § 1: a number in its shortest form. The column keeps two decimals, so a stored 2.5 reads
    /// back as 2.50; the API answers 2.5 (and 8, not 8.00). The value itself never changes.
    /// </summary>
    private static decimal? Shortest(decimal? value) =>
        value is { } number
            ? decimal.Parse(number.ToString("0.##", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            : null;

    private static WorkItemRefView Ref(WorkItemRefRow row) => WorkItemRefs.View(row);

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
        Guid? ParentId,
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
        DateTimeOffset ChangedAt,
        FieldsRow Fields);

    /// <summary>feat-022's fields as stored; the texts are <c>null</c> in the list's rows, which leave them out.</summary>
    private sealed record FieldsRow(
        int Priority,
        int? Risk,
        decimal? StoryPoints,
        decimal? Effort,
        int? BusinessValue,
        decimal? TimeCriticality,
        DateOnly? StartDate,
        DateOnly? TargetDate,
        int? Severity,
        WorkItemActivity? Activity,
        decimal? OriginalEstimate,
        decimal? RemainingWork,
        decimal? CompletedWork,
        WorkItemValueArea? ValueArea,
        string? AcceptanceCriteria,
        string? ReproSteps,
        string? SystemInfo);
}
