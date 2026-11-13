using Mustask.Application.Abstractions.Iterations;
using Mustask.Domain.Iterations;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Infrastructure.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Iterations;

/// <inheritdoc cref="ISprintReadModel"/>
/// <remarks>
/// Projections only (<c>AsNoTracking</c>, no entity materialized), every figure computed by the database in a grouped statement,
/// so the number of statements is fixed whatever the data (NFR). Reachability (BR-9) is the join to an Active project; whose
/// projects a caller may see beyond that is feat-041's.
/// </remarks>
internal sealed class SprintReadModel(MustaskDbContext context) : ISprintReadModel
{
    private IQueryable<SprintSnapshotItem> Snapshot => context.Set<SprintSnapshotItem>().AsNoTracking();

    private IQueryable<SprintSnapshotItem> ReachableSnapshot =>
        Snapshot.Where(item => context.Projects.Any(project => project.Id == item.ProjectId && project.Status == ProjectStatus.Active));

    public Task<SprintView?> FindAsync(Guid teamId, Guid sprintId, CancellationToken cancellationToken) =>
        context.Sprints
            .AsNoTracking()
            .Where(sprint => sprint.TeamId == teamId && sprint.Id == sprintId)
            .Select(sprint => new SprintView(
                sprint.Id,
                sprint.TeamId,
                sprint.Number,
                sprint.Name,
                sprint.State.ToString(),
                sprint.StartDate,
                sprint.EndDate,
                sprint.CreatedAt,
                sprint.CreatedBy,
                sprint.ClosedAt,
                sprint.ClosedBy))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<SprintListPage> ListAsync(
        Guid teamId, int? before, bool openOnly, Guid storiesLevelId, CancellationToken cancellationToken)
    {
        var sprints = context.Sprints.AsNoTracking().Where(sprint => sprint.TeamId == teamId);

        if (before is { } cursor)
        {
            sprints = sprints.Where(sprint => sprint.Number < cursor);
        }

        if (openOnly)
        {
            sprints = sprints.Where(sprint => sprint.State != SprintState.Closed);
        }

        // One row more than a page says whether another follows.
        var page = await sprints
            .OrderByDescending(sprint => sprint.Number)
            .Take(SprintLimits.PageSize + 1)
            .Select(sprint => new { sprint.Id, sprint.Number, sprint.Name, sprint.State, sprint.StartDate, sprint.EndDate })
            .ToListAsync(cancellationToken);

        var shown = page.Take(SprintLimits.PageSize).ToList();
        var openIds = shown.Where(sprint => sprint.State != SprintState.Closed).Select(sprint => sprint.Id).ToList();
        var closedIds = shown.Where(sprint => sprint.State == SprintState.Closed).Select(sprint => sprint.Id).ToList();

        // Planned and Active: what the sprint holds now, every level, outside «Удалена», in reachable projects.
        var live = openIds.Count == 0
            ? new Dictionary<Guid, CountView>()
            : await (
                    from item in WorkItemSets.Live(context).AsNoTracking()
                    where item.SprintId != null && openIds.Contains(item.SprintId.Value)
                    where context.Projects.Any(project => project.Id == item.ProjectId && project.Status == ProjectStatus.Active)
                    group item by item.SprintId!.Value into sprintItems
                    select new { SprintId = sprintItems.Key, Items = sprintItems.Count(), StoryPoints = sprintItems.Sum(item => item.StoryPoints ?? 0m) })
                .ToDictionaryAsync(row => row.SprintId, row => new CountView(row.Items, row.StoryPoints), cancellationToken);

        var closed = closedIds.Count == 0
            ? new Dictionary<Guid, SprintCounts>()
            : (Dictionary<Guid, SprintCounts>)await CountsBySprintAsync(closedIds, storiesLevelId, cancellationToken);

        var rows = shown
            .Select(sprint =>
            {
                if (sprint.State == SprintState.Closed)
                {
                    closed.TryGetValue(sprint.Id, out var counts);

                    return new SprintRowView(
                        sprint.Id, sprint.Number, sprint.Name, sprint.State.ToString(), sprint.StartDate, sprint.EndDate,
                        counts.Items, counts.StoryPoints, counts.ClosedItems, counts.ClosedStoryPoints);
                }

                live.TryGetValue(sprint.Id, out var held);

                return new SprintRowView(
                    sprint.Id, sprint.Number, sprint.Name, sprint.State.ToString(), sprint.StartDate, sprint.EndDate,
                    held?.Items ?? 0, held?.StoryPoints ?? 0m, null, null);
            })
            .ToList();

        return new SprintListPage(rows, page.Count > SprintLimits.PageSize ? shown[^1].Number : null);

    }

    public async Task<ClosePreview> ClosePreviewAsync(Guid teamId, Guid sprintId, CancellationToken cancellationToken)
    {
        var held = await (
                from item in WorkItemSets.Live(context).AsNoTracking()
                join state in context.StateDefinitions on item.StateId equals state.Id
                where item.SprintId == sprintId
                where context.Projects.Any(project => project.Id == item.ProjectId && project.Status == ProjectStatus.Active)
                group item by state.Category == StateCategory.Completed into byDone
                select new { Done = byDone.Key, Items = byDone.Count(), StoryPoints = byDone.Sum(item => item.StoryPoints ?? 0m) })
            .ToListAsync(cancellationToken);

        var destinations = await context.Sprints
            .AsNoTracking()
            .Where(sprint => sprint.TeamId == teamId && sprint.State == SprintState.Planned)
            .OrderBy(sprint => sprint.Number)
            .Select(sprint => new DestinationView(sprint.Id, sprint.Name, sprint.StartDate, sprint.EndDate))
            .ToListAsync(cancellationToken);

        var moving = held.Where(row => !row.Done).ToList();
        var staying = held.Where(row => row.Done).ToList();

        return new ClosePreview(
            new CountView(moving.Sum(row => row.Items), moving.Sum(row => row.StoryPoints)),
            new CountView(staying.Sum(row => row.Items), staying.Sum(row => row.StoryPoints)),
            destinations);
    }

    public async Task<(int Items, decimal StoryPoints)> DoneTotalsAsync(Guid sprintId, CancellationToken cancellationToken)
    {
        var rows = await ReachableSnapshot
            .Where(item => item.SprintId == sprintId && item.IsDone)
            .GroupBy(_ => 1)
            .Select(group => new { Items = group.Count(), StoryPoints = group.Sum(item => item.StoryPoints) })
            .ToListAsync(cancellationToken);

        return rows.Count == 0 ? (0, 0m) : (rows[0].Items, rows[0].StoryPoints);
    }

    public async Task<RangeSprints> FindRangeAsync(Guid teamId, Guid sprintId, SprintRange range, CancellationToken cancellationToken)
    {
        var inRange = context.Sprints
            .AsNoTracking()
            .Where(sprint => sprint.TeamId == teamId
                && sprint.State == SprintState.Closed
                && sprint.Id != sprintId
                && sprint.EndDate >= range.From
                && sprint.EndDate <= range.To);

        var total = await inRange.CountAsync(cancellationToken);

        var latest = await inRange
            .OrderByDescending(sprint => sprint.EndDate)
            .ThenByDescending(sprint => sprint.Number)
            .Take(SprintLimits.MaxRangeSprints)
            .Select(sprint => new { sprint.Id, sprint.Number, sprint.Name, sprint.EndDate })
            .ToListAsync(cancellationToken);

        return new RangeSprints(
            total,
            latest.Select(sprint => new RangeSprint(sprint.Id, sprint.Number, sprint.Name, sprint.EndDate!.Value)).ToList());
    }

    public async Task<IReadOnlyDictionary<Guid, SprintCounts>> CountsBySprintAsync(
        IReadOnlyCollection<Guid> sprintIds, Guid levelId, CancellationToken cancellationToken)
    {
        var ids = sprintIds.ToArray();

        var rows = await ReachableSnapshot
            .Where(item => ids.Contains(item.SprintId) && item.LevelId == levelId)
            .GroupBy(item => item.SprintId)
            .Select(group => new
            {
                SprintId = group.Key,
                Items = group.Count(),
                StoryPoints = group.Sum(item => item.StoryPoints),
                ClosedItems = group.Count(item => item.IsDone),
                ClosedStoryPoints = group.Sum(item => item.IsDone ? item.StoryPoints : 0m),
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.SprintId,
            row => new SprintCounts(row.Items, row.StoryPoints, row.ClosedItems, row.ClosedStoryPoints));
    }

    public async Task<IReadOnlyList<ParticipantCounts>> CountsByParticipantAsync(
        IReadOnlyCollection<Guid> sprintIds, Guid levelId, CancellationToken cancellationToken)
    {
        var ids = sprintIds.ToArray();

        var rows = await ReachableSnapshot
            .Where(item => ids.Contains(item.SprintId) && item.LevelId == levelId)
            .GroupBy(item => new { item.SprintId, item.AssigneeId })
            .Select(group => new
            {
                group.Key.SprintId,
                group.Key.AssigneeId,
                Items = group.Count(),
                StoryPoints = group.Sum(item => item.StoryPoints),
                ClosedItems = group.Count(item => item.IsDone),
                ClosedStoryPoints = group.Sum(item => item.IsDone ? item.StoryPoints : 0m),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ParticipantCounts(
                row.SprintId, row.AssigneeId, new SprintCounts(row.Items, row.StoryPoints, row.ClosedItems, row.ClosedStoryPoints)))
            .ToList();
    }

    public async Task<IReadOnlySet<string>> OrganizationMemberIdsAsync(
        Guid organizationId, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.ToArray();

        var members = await context.OrganizationMemberships
            .AsNoTracking()
            .Where(membership => membership.OrganizationId == organizationId && ids.Contains(membership.UserId))
            .Select(membership => membership.UserId)
            .ToListAsync(cancellationToken);

        return members.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<OutcomeItemsPage> OutcomeItemsAsync(Guid sprintId, int offset, CancellationToken cancellationToken)
    {
        var done = ReachableSnapshot.Where(item => item.SprintId == sprintId && item.IsDone);

        var totals = await done
            .GroupBy(_ => 1)
            .Select(group => new { Items = group.Count(), StoryPoints = group.Sum(item => item.StoryPoints) })
            .ToListAsync(cancellationToken);

        // The item itself may be gone (a purge keeps the snapshot): a left join to the work item says so and gives its title.
        var rows = await (
                from item in done
                join project in context.Projects on item.ProjectId equals project.Id
                join type in context.WorkItemTypes on item.TypeId equals type.Id
                join live in WorkItemSets.InAnyState(context).AsNoTracking() on item.WorkItemId equals live.Id into lives
                from current in lives.DefaultIfEmpty()
                orderby project.Key, item.Number
                select new
                {
                    ProjectKey = project.Key,
                    TypeKey = type.Key,
                    item.Number,
                    Title = current == null ? null : current.Title,
                    Purged = current == null,
                    ProjectId = project.Id,
                    ProjectName = project.Name,
                    item.StoryPoints,
                })
            .Skip(offset)
            .Take(SprintLimits.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new OutcomeItemView(
                Mustask.Domain.WorkItems.WorkItemIdentifier.Format(row.ProjectKey, row.TypeKey, row.Number),
                row.Title,
                row.ProjectId,
                row.ProjectName,
                row.StoryPoints,
                row.Purged))
            .ToList();

        return totals.Count == 0
            ? new OutcomeItemsPage(items, 0, 0m)
            : new OutcomeItemsPage(items, totals[0].Items, totals[0].StoryPoints);
    }
}
