using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// <see cref="IWorkItemRepository"/> over <c>work_items</c> and
/// <c>work_item_counters</c>. Every lock is raw SQL taken before the regular
/// tracking load (ADR-0010 "lock by id, load with the regular query"), so the
/// loaded <c>xmin</c> is current and the flush never meets a concurrency
/// conflict: concurrent writers wait on the lock instead (BR-M3-25).
/// </summary>
internal sealed class WorkItemRepository(MustaskDbContext context) : IWorkItemRepository
{
    public async Task LockNumberSequenceAsync(Guid projectId, CancellationToken cancellationToken)
    {
        // A concurrent first creation of the project waits here on the other's uncommitted key, then does nothing.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO work_item_counters (project_id, last_number) VALUES ({projectId}, 0) ON CONFLICT (project_id) DO NOTHING",
            cancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM work_item_counters WHERE project_id = {projectId} FOR UPDATE",
            cancellationToken);
    }

    public async Task<int> TakeNextNumberAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE work_item_counters SET last_number = last_number + 1 WHERE project_id = {projectId}",
            cancellationToken);

        // The row is locked by this transaction, so this reads the number it has just issued.
        return await context.Database
            .SqlQuery<int>($"SELECT last_number AS \"Value\" FROM work_item_counters WHERE project_id = {projectId}")
            .SingleAsync(cancellationToken);
    }

    public async Task<Guid?> LockTeamDefaultAreaAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM teams WHERE id = {teamId} AND project_id = {projectId} FOR SHARE",
            cancellationToken);

        // A new statement under READ COMMITTED: a team deleted, or a default moved, by the lock's previous holder reads so.
        return await context.Teams
            .AsNoTracking()
            .Where(team => team.Id == teamId && team.ProjectId == projectId && team.Status == TeamStatus.Active)
            .Select(team => context.Areas.Where(area => area.DefaultOfTeamId == team.Id).Select(area => (Guid?)area.Id).FirstOrDefault())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Guid?> FindCreatedByRequestAsync(Guid projectId, Guid requestId, CancellationToken cancellationToken) =>
        context.WorkItems
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.CreationRequestId == requestId)
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<WorkItem?> LockAsync(Guid projectId, int number, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM work_items WHERE project_id = {projectId} AND number = {number} FOR UPDATE",
            cancellationToken);

        return await context.WorkItems.SingleOrDefaultAsync(item => item.ProjectId == projectId && item.Number == number, cancellationToken);
    }

    public async Task<bool> LockAreaAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM areas WHERE id = {areaId} AND project_id = {projectId} FOR SHARE",
            cancellationToken);

        // A new statement under READ COMMITTED: an area deleted by the lock's previous holder is gone.
        return await context.Areas.AnyAsync(area => area.Id == areaId && area.ProjectId == projectId, cancellationToken);
    }

    public void Add(WorkItem item) => context.WorkItems.Add(item);

    public Task SaveAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
