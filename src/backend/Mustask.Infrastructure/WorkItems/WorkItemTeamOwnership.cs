using Mustask.Application.Features.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// <see cref="IWorkItemTeamOwnership"/> over <c>work_items</c> (mod-017 BR-OT-12, plan-backend § 7). Set-based statements, so
/// a team that owns 5 000 items is released within the deletion's 5 seconds: the lock names its rows by the owner key's index,
/// and the release is one <c>UPDATE</c>. Raw SQL only where LINQ cannot say it — row locking (backend.md, "Raw SQL").
/// </summary>
internal sealed class WorkItemTeamOwnership(MustaskDbContext context) : IWorkItemTeamOwnership
{
    // Every state: a deleted item still belongs to its team until the team's deletion releases it (BR-OT-12, mod-005 BR-DR-9).
    public Task<int> CountOwnedAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        WorkItemSets.InAnyState(context).CountAsync(item => item.ProjectId == projectId && item.OwningTeamId == teamId, cancellationToken);

    public Task LockOwnedAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM work_items WHERE project_id = {projectId} AND owning_team_id = {teamId} ORDER BY id FOR UPDATE",
            cancellationToken);

    public Task<int> ReleaseOwnedAsync(Guid projectId, Guid teamId, DateTimeOffset now, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE work_items SET owning_team_id = NULL, changed_at = {now} WHERE project_id = {projectId} AND owning_team_id = {teamId}",
            cancellationToken);
}
