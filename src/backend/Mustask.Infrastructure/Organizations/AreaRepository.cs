using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <inheritdoc cref="OrganizationRepository"/>
internal sealed class AreaRepository(MustaskDbContext context) : IAreaRepository
{
    public Task<Area?> FindAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken) =>
        context.Areas
            .AsNoTracking()
            .SingleOrDefaultAsync(area => area.Id == areaId && area.ProjectId == projectId, cancellationToken);

    public async Task<Area?> LockForUpdateAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM areas WHERE id = {areaId} AND project_id = {projectId} FOR UPDATE",
            cancellationToken);

        return await LoadAsync(projectId, areaId, cancellationToken);
    }

    public async Task<Area?> LockForShareAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM areas WHERE id = {areaId} AND project_id = {projectId} FOR SHARE",
            cancellationToken);

        return await LoadAsync(projectId, areaId, cancellationToken);
    }

    public async Task<Area> LockRootAsync(Guid projectId, bool forUpdate, CancellationToken cancellationToken)
    {
        if (forUpdate)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM areas WHERE project_id = {projectId} AND parent_id IS NULL FOR UPDATE",
                cancellationToken);
        }
        else
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM areas WHERE project_id = {projectId} AND parent_id IS NULL FOR SHARE",
                cancellationToken);
        }

        return await context.Areas.SingleAsync(area => area.ProjectId == projectId && area.ParentId == null, cancellationToken);
    }

    public Task<bool> HasChildrenAsync(Guid areaId, CancellationToken cancellationToken) =>
        context.Areas.AnyAsync(area => area.ParentId == areaId, cancellationToken);

    public Task<bool> IsSiblingNameTakenAsync(Guid parentId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        context.Areas.AnyAsync(
            area => area.ParentId == parentId && area.NormalizedName == normalizedName && area.Id != exceptId,
            cancellationToken);

    public async Task<IReadOnlyList<AreaNode>> GetNodesAsync(Guid projectId, CancellationToken cancellationToken) =>
        await context.Areas
            .AsNoTracking()
            .Where(area => area.ProjectId == projectId)
            .OrderBy(area => EF.Functions.Collate(area.Name, OrganizationsCollation.Russian))
            .ThenBy(area => area.Id)
            .Select(area => new AreaNode(area.Id, area.ParentId, area.Name))
            .ToListAsync(cancellationToken);

    public void Add(Area area) => context.Areas.Add(area);

    public void Remove(Area area) => context.Areas.Remove(area);

    public Task SaveAsync(CancellationToken cancellationToken) => OrganizationsSaveChanges.SaveAsync(context, cancellationToken);

    private Task<Area?> LoadAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken) =>
        context.Areas.SingleOrDefaultAsync(area => area.Id == areaId && area.ProjectId == projectId, cancellationToken);
}
