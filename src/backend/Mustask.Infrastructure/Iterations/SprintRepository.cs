using Mustask.Application.Abstractions.Iterations;
using Mustask.Domain.Iterations;
using Mustask.Domain.Process;
using Mustask.Infrastructure.Dictionaries;
using Mustask.Infrastructure.WorkItems;
using Mustask.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Iterations;

/// <inheritdoc cref="ISprintRepository"/>
/// <remarks>
/// Every lock is a fixed raw statement taken before the tracking load (ADR-0010), and carries the organization (and the team where
/// the caller names one), so a sprint of another organization or team locks nothing and loads as <c>null</c>. The close's and the
/// removal's statements are set-based: one statement per step whatever the number of items (NFR: 5 000 items).
/// </remarks>
internal sealed class SprintRepository(MustaskDbContext context) : ISprintRepository
{
    public async Task<Sprint?> LockForUpdateAsync(Guid organizationId, Guid teamId, Guid sprintId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM sprints WHERE id = {sprintId} AND team_id = {teamId} AND organization_id = {organizationId} FOR UPDATE",
            cancellationToken);

        return await context.Sprints.SingleOrDefaultAsync(
            sprint => sprint.Id == sprintId && sprint.TeamId == teamId && sprint.OrganizationId == organizationId, cancellationToken);
    }

    public async Task<SprintFacts?> LockForShareAsync(Guid organizationId, Guid sprintId, CancellationToken cancellationToken)
    {
        // The statement returns the row it locked: that answer, not a second read, is what the caller judges by.
        var rows = await context.Database
            .SqlQuery<FactsRow>(
                $"""
                 SELECT id AS "Id", team_id AS "TeamId", state AS "State"
                 FROM sprints
                 WHERE id = {sprintId} AND organization_id = {organizationId}
                 FOR SHARE
                 """)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new SprintFacts(row.Id, row.TeamId, Enum.Parse<SprintState>(row.State))).SingleOrDefault();
    }

    public async Task<int> TakeNextNumberAsync(Guid teamId, CancellationToken cancellationToken)
    {
        // A concurrent first creation of the team waits here on the other's uncommitted key, then does nothing.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO sprint_counters (team_id, last_number) VALUES ({teamId}, 0) ON CONFLICT (team_id) DO NOTHING",
            cancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE sprint_counters SET last_number = last_number + 1 WHERE team_id = {teamId}",
            cancellationToken);

        // The row is locked by this transaction, so this reads the number it has just issued.
        return await context.Database
            .SqlQuery<int>($"SELECT last_number AS \"Value\" FROM sprint_counters WHERE team_id = {teamId}")
            .SingleAsync(cancellationToken);
    }

    public Task<bool> IsNameTakenAsync(Guid teamId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        context.Sprints.AnyAsync(
            sprint => sprint.TeamId == teamId && sprint.NormalizedName == normalizedName && (exceptId == null || sprint.Id != exceptId),
            cancellationToken);

    public Task<bool> HasActiveAsync(Guid teamId, Guid exceptId, CancellationToken cancellationToken) =>
        context.Sprints.AnyAsync(
            sprint => sprint.TeamId == teamId && sprint.State == SprintState.Active && sprint.Id != exceptId, cancellationToken);

    public void Add(Sprint sprint) => context.Sprints.Add(sprint);

    public void Remove(Sprint sprint) => context.Sprints.Remove(sprint);

    public async Task<int> LockItemsAsync(Guid sprintId, CancellationToken cancellationToken)
    {
        // The hierarchy locks of the projects the items live in come first (ADR-0028 rule 8 step 5), then the items by id: the order
        // a team's unlink takes. No item can join the sprint meanwhile: a save that sets it holds the sprint FOR SHARE, and the caller
        // holds it FOR UPDATE.
        var projectIds = await WorkItemSets.InAnyState(context)
            .AsNoTracking()
            .Where(item => item.SprintId == sprintId)
            .Select(item => item.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);

        await WorkItemHierarchyLocks.AcquireAsync(context, projectIds, cancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM work_items WHERE sprint_id = {sprintId} ORDER BY id FOR UPDATE",
            cancellationToken);

        return await WorkItemSets.Live(context).CountAsync(item => item.SprintId == sprintId, cancellationToken);
    }

    public Task ClearItemsAsync(Guid sprintId, CancellationToken cancellationToken) =>
        SprintItemPointers.ClearAsync(context, sprintId, cancellationToken);

    public async Task WriteSnapshotAsync(Guid sprintId, CancellationToken cancellationToken)
    {
        // The categories are named by the ids of the states that hold them: the process tables are read by the process sub-slice's
        // own SQL only (ProcessStateWritesRules), and a state id is unique to its type.
        var states = await context.StateDefinitions
            .AsNoTracking()
            .Select(state => new { state.Id, state.Category })
            .ToListAsync(cancellationToken);

        var completed = states.Where(state => state.Category == StateCategory.Completed).Select(state => state.Id).ToArray();
        var removed = states.Where(state => state.Category == StateCategory.Removed).Select(state => state.Id).ToArray();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO sprint_snapshot_items
                 (sprint_id, work_item_id, project_id, type_id, number, level_id, assignee_id, story_points, is_done)
             SELECT item.sprint_id, item.id, item.project_id, item.type_id, item.number, type.backlog_level_id,
                    item.assignee_id, COALESCE(item.story_points, 0), (item.state_id = ANY({completed}))
             FROM work_items AS item
             JOIN work_item_types AS type ON type.id = item.type_id
             WHERE item.sprint_id = {sprintId} AND item.state_id <> ALL({removed})
             """,
            cancellationToken);
    }

    public Task MoveUnfinishedAsync(Guid sprintId, Guid? destinationId, DateTimeOffset now, CancellationToken cancellationToken) =>
        SprintItemPointers.MoveUnfinishedAsync(context, sprintId, destinationId, now, cancellationToken);

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (PostgresConflictDetection.IsUniqueViolationOnConstraint(exception, IterationsConstraints.TeamName))
        {
            throw new ConflictException(SprintErrorCodes.NameTaken);
        }
        catch (DbUpdateException exception) when (PostgresConflictDetection.IsUniqueViolationOnConstraint(exception, IterationsConstraints.TeamActive))
        {
            throw new ConflictException(SprintErrorCodes.ActiveExists);
        }
    }

    private sealed class FactsRow
    {
        public Guid Id { get; set; }

        public Guid TeamId { get; set; }

        public string State { get; set; } = null!;
    }
}
