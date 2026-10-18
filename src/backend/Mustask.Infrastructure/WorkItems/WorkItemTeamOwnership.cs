using Mustask.Application.Features.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// <see cref="IWorkItemTeamOwnership"/> over <c>work_items</c> (mod-017 BR-OT-12, feat-028 BR-OL-8, BR-OL-9; plan-backend § 7).
/// Set-based statements over a <b>set of projects</b>, so a team that owns 5 000 items is reassigned within the 5 seconds whatever
/// the number of projects it is linked to: the lock names its rows by the owner key's index
/// (<c>ix_work_items_project_id_owning_team_id_type_id</c>), and the reassignment is one <c>UPDATE</c>. Raw SQL only where LINQ
/// cannot say it — row locking (backend.md, "Raw SQL").
/// </summary>
internal sealed class WorkItemTeamOwnership(MustaskDbContext context) : IWorkItemTeamOwnership
{
    // Every state: a deleted item still belongs to its team until the team's deletion releases it (BR-OT-12, mod-005 BR-DR-9).
    public Task<int> CountOwnedAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        WorkItemSets.InAnyState(context).CountAsync(item => item.ProjectId == projectId && item.OwningTeamId == teamId, cancellationToken);

    public async Task<IReadOnlyList<ProjectCount>> CountOwnedByProjectAsync(
        Guid teamId, IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return [];
        }

        var ids = projectIds.Distinct().ToArray();

        // Every state, as the unlink's count is (feat-028 SC-09, doc-012 BR-MX-12): a deleted item is owned until a release moves
        // it, so the deletion's counts say what the deletion would release, deleted items included (mod-005 BR-DR-9).
        return await WorkItemSets.InAnyState(context)
            .AsNoTracking()
            .Where(item => item.OwningTeamId == teamId && ids.Contains(item.ProjectId))
            .GroupBy(item => item.ProjectId)
            .Select(group => new ProjectCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
    }

    // Two statements for any number of projects, in the database's own order of project ids (WorkItemHierarchyLocks).
    public Task LockHierarchiesAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
        WorkItemHierarchyLocks.AcquireAsync(context, projectIds, cancellationToken);

    public Task LockOwnedAsync(IReadOnlyCollection<Guid> projectIds, Guid teamId, CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        var ids = projectIds.Distinct().ToArray();

        // ORDER BY id before the lock: one fixed order for every caller, whatever the number of projects.
        return context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM work_items WHERE project_id = ANY({ids}) AND owning_team_id = {teamId} ORDER BY id FOR UPDATE",
            cancellationToken);
    }

    public async Task<int> ReassignOwnedAsync(
        IReadOnlyCollection<Guid> projectIds, Guid teamId, Guid? destinationTeamId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return 0;
        }

        var ids = projectIds.Distinct().ToArray();

        // The destination is written as a nullable parameter: «Без команды» is NULL, and the owner key is MATCH SIMPLE, so a null
        // owner leaves it unchecked. A destination is a team linked to the item's project (the caller holds that link locked).
        return await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE work_items
             SET owning_team_id = {destinationTeamId}, changed_at = {now}
             WHERE project_id = ANY({ids}) AND owning_team_id = {teamId}
             """,
            cancellationToken);
    }
}
