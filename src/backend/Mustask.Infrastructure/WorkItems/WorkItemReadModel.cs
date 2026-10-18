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
    // The card by address, a write's answer and a parent's ref read the item in any state (ADR-0030 rule 2, BR-DR-6): a
    // deleted item is found by its identifier and read in full.
    public async Task<WorkItemView?> FindAsync(Guid projectId, int number, CancellationToken cancellationToken)
    {
        var row = await Rows(WorkItemSets.InAnyState(context).Where(item => item.ProjectId == projectId && item.Number == number))
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : await ViewAsync(row, cancellationToken);
    }

    public async Task<WorkItemView> GetAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        var row = await Rows(WorkItemSets.InAnyState(context).Where(item => item.Id == workItemId)).SingleAsync(cancellationToken);

        return await ViewAsync(row, cancellationToken);
    }

    public Task<WorkItemHead?> FindHeadAsync(Guid projectId, int number, CancellationToken cancellationToken) =>
        WorkItemSets.InAnyState(context)
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.Number == number)
            .Select(item => new WorkItemHead(item.Id, item.TypeId, item.StateId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<WorkItemRefView> GetRefAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        var row = await RefRows(WorkItemSets.InAnyState(context).Where(item => item.Id == workItemId)).SingleAsync(cancellationToken);

        return Ref(row);
    }

    public async Task<ParentCandidates> FindParentCandidatesAsync(ParentCandidateSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentOutOfRangeException.ThrowIfLessThan(search.Limit, 1);

        var typeIds = search.TypeIds.ToList();

        // A picker offers live items only (ADR-0030 rule 2): the entry point states it; the Ref rows join the state for the view.
        var candidates =
            from item in WorkItemSets.Live(context)
            join type in context.WorkItemTypes on item.TypeId equals type.Id
            where item.ProjectId == search.ProjectId
                && item.Id != search.ExcludedItemId
                && typeIds.Contains(item.TypeId)
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

    public async Task<WorkItemPage> GetPageAsync(
        Guid projectId, int page, int pageSize, WorkItemFilter filter, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        // The list hides a deleted item and does not count it (mod-005 BR-DR-6): it starts from the Live entry point, so the
        // count and every page follow the same set. The team filter (mod-017 BR-OT-8) is one predicate for the list, the
        // backlog and the board; the count and the page are composed on it, so a filter adds no statement.
        var matching = WorkItemFilterPredicate.Apply(WorkItemSets.Live(context).Where(item => item.ProjectId == projectId), filter);

        var count = await matching.CountAsync(cancellationToken);

        // The offset is computed in long: a page far past the end must not overflow into a negative OFFSET.
        var skip = (long)(page - 1) * pageSize;

        if (skip >= count)
        {
            return new WorkItemPage(count, []);
        }

        // The order and the page are taken in SQL, on the items themselves; the joins then only dress the page's rows.
        var pageItems = matching
            .OrderByDescending(item => item.ChangedAt)
            .ThenByDescending(item => item.Number)
            .Skip((int)skip)
            .Take(pageSize);

        var rows = await Rows(pageItems, includeDescription: false).ToListAsync(cancellationToken);

        var paths = await AreaPathsAsync(projectId, cancellationToken);
        var assignees = await AssigneesAsync(rows, projectId, cancellationToken);

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
                Team(row),
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
        join team in context.Teams on item.OwningTeamId equals team.Id into owning
        from team in owning.DefaultIfEmpty()
        // mod-005 (plan-backend D-P3): the state a deleted item held, in the item's own statement; null on every live row.
        join held in context.StateDefinitions on item.DeletedFromStateId equals held.Id into heldStates
        from held in heldStates.DefaultIfEmpty()
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
            team == null ? null : team.Id,
            team == null ? null : team.Name,
            item.Title,
            includeDescription ? item.Description : string.Empty,
            item.CreatedBy,
            item.CreatedAt,
            item.ChangedAt,
            new DeletionRow(
                held == null ? null : held.Id,
                held == null ? null : held.Name,
                held == null ? null : held.Category,
                item.DeletedBy,
                item.DeletedAt),
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
        var projectId = row.ProjectId;
        var itemId = row.Id;

        // The stored parent in any state (a live child leads to its deleted parent), the children among the live ones — each
        // end from its own entry point (ADR-0030 rule 2), in the one statement the card has always used.
        var parent = WorkItemSets.InAnyState(context).Where(item => item.Id == parentId);
        var children = WorkItemSets.Live(context).Where(item => item.ProjectId == projectId && item.ParentId == itemId);

        var links = await RefRows(parent.Concat(children)).ToListAsync(cancellationToken);

        var parentRow = parentId is { } id ? links.Single(link => link.Id == id) : null;

        var childRows = links
            .Where(link => link.ParentId == row.Id)
            .OrderByDescending(link => link.LevelRank)
            .ThenBy(link => link.Number)
            .Select(Ref)
            .ToList();

        return (parentRow is null ? null : Ref(parentRow), childRows);
    }

    private async Task<WorkItemView> ViewAsync(ItemRow row, CancellationToken cancellationToken)
    {
        var paths = await AreaPathsAsync(row.ProjectId, cancellationToken);
        var assignees = await AssigneesAsync([row], row.ProjectId, cancellationToken);
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
            Team(row),
            row.CreatedBy,
            row.CreatedAt,
            row.ChangedAt,
            parent,
            children)
        {
            Deletion = DeletionOf(row.Deletion, assignees),
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
            AcceptanceCriteria = fields.AcceptanceCriteria?.Select(c => new AcceptanceCriterionView(c.Given, c.When, c.Then)).ToList(),
            ReproSteps = fields.ReproSteps,
            SystemInfo = fields.SystemInfo,
        };
    }

    /// <summary>
    /// feat-022 api-contract § 1: a number in its shortest form. The column keeps two decimals, so a stored 2.5 reads
    /// back as 2.50; the API answers 2.5 (and 8, not 8.00). The value itself never changes. The backlog's rows
    /// (feat-027) answer «Трудоёмкость» through this one rule too.
    /// </summary>
    internal static decimal? Shortest(decimal? value) =>
        value is { } number
            ? decimal.Parse(number.ToString("0.##", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            : null;

    private static WorkItemRefView Ref(WorkItemRefRow row) => WorkItemRefs.View(row);

    private async Task<AreaTree> AreaPathsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var nodes = await context.Areas
            .AsNoTracking()
            .Where(area => area.ProjectId == projectId)
            .Select(area => new AreaNode(area.Id, area.ParentId, area.Name))
            .ToListAsync(cancellationToken);

        return new AreaTree(nodes);
    }

    /// <summary>
    /// The page's assignees — and, on the card, the user that deleted the item (mod-005 plan-backend D-P3), in the same call —
    /// who the directory knows, by one lookup, and — mod-016 BR-M1-26 — which of them are members of
    /// the project's organization right now, by one more statement (<see cref="ProjectMembers"/>).
    /// </summary>
    private async Task<Assignees> AssigneesAsync(IReadOnlyCollection<ItemRow> rows, Guid projectId, CancellationToken cancellationToken)
    {
        var ids = rows
            .SelectMany(row => new[] { row.AssigneeId, row.Deletion.DeletedBy })
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ids.Count == 0)
        {
            return new Assignees(new Dictionary<string, DirectoryUser>(StringComparer.Ordinal), []);
        }

        var known = (await directory.LookupAsync(ids, cancellationToken)).Known;
        var members = await ProjectMembers.MemberIdsAsync(context, projectId, ids, cancellationToken);

        return new Assignees(known, members);
    }

    private static string Identifier(ItemRow row) => WorkItemIdentifier.Format(row.ProjectKey, row.TypeKey, row.Number);

    /// <summary>The owning team by its current name, or none (mod-017 api-contract § 1.1).</summary>
    private static WorkItemTeamView? Team(ItemRow row) =>
        row.TeamId is { } id ? new WorkItemTeamView(id, row.TeamName!) : null;

    private static WorkItemTypeView Type(ItemRow row) => new(row.TypeId, row.TypeKey, row.TypeName, row.TypeAppearance.ToWireName());

    private static WorkItemStateView State(ItemRow row) =>
        new(row.StateId, row.StateName, new WorkItemStateCategoryView(row.StateCategory.ToString(), StateCategoryLabels.Of(row.StateCategory)));

    /// <summary>An id the directory does not know keeps its id and has no name (US-2 «Неизвестный пользователь»).</summary>
    private static WorkItemAssigneeView? Assignee(ItemRow row, Assignees assignees) =>
        row.AssigneeId switch
        {
            null => null,
            { } id when assignees.Known.TryGetValue(id, out var user) =>
                new WorkItemAssigneeView(id, user.DisplayName, user.Email, assignees.Members.Contains(id)),
            { } id => new WorkItemAssigneeView(id, null, null, assignees.Members.Contains(id)),
        };

    /// <summary>
    /// mod-005 (api-contract § 1.1): the record of the latest deletion, or <c>null</c> for a live item and for an item in
    /// «Удалена» with no marks (one put there outside Delete). The marks are all-or-none (the table's check), so one is
    /// enough to say there are three. The deleter is a user reference with the two marks of BR-13: an id the directory does
    /// not know keeps its id and has no name and is not active.
    /// </summary>
    private static WorkItemDeletionView? DeletionOf(DeletionRow marks, Assignees users)
    {
        if (marks.HeldStateId is not { } stateId || marks.DeletedBy is not { } by || marks.DeletedAt is not { } at)
        {
            return null;
        }

        var known = users.Known.TryGetValue(by, out var user);

        return new WorkItemDeletionView(
            new WorkItemStateView(
                stateId,
                marks.HeldStateName!,
                new WorkItemStateCategoryView(marks.HeldStateCategory!.Value.ToString(), StateCategoryLabels.Of(marks.HeldStateCategory.Value))),
            new WorkItemUserView(by, known ? user!.DisplayName : null, known && user!.IsActive, users.Members.Contains(by)),
            at);
    }

    /// <summary>The directory's answer for the page's assignees and the ones among them who are members of the organization.</summary>
    private sealed record Assignees(IReadOnlyDictionary<string, DirectoryUser> Known, HashSet<string> Members);

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
        Guid? TeamId,
        string? TeamName,
        string Title,
        string Description,
        string CreatedBy,
        DateTimeOffset CreatedAt,
        DateTimeOffset ChangedAt,
        DeletionRow Deletion,
        FieldsRow Fields);

    /// <summary>The marks of a deletion as stored (mod-005): all null on a live item, all set after a Delete.</summary>
    private sealed record DeletionRow(
        Guid? HeldStateId,
        string? HeldStateName,
        StateCategory? HeldStateCategory,
        string? DeletedBy,
        DateTimeOffset? DeletedAt);

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
        IReadOnlyList<AcceptanceCriterion>? AcceptanceCriteria,
        string? ReproSteps,
        string? SystemInfo);
}
