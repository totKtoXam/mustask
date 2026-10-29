using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The write side of areas (mod-018 plan-backend § 5). Every statement carries the organization (ADR-0028 rule 4). A lock statement
/// returns the rows it locked (<c>SqlQuery&lt;Guid&gt;</c>), and the caller decides "present" from that answer alone, never from a
/// later read, which under READ COMMITTED judges another snapshot (the A-9 corollary); only then is the tracked row loaded.
/// </summary>
internal sealed class AreaRepository(MustaskDbContext context) : IAreaRepository
{
    public async Task LockTreeAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO area_tree_locks (organization_id) VALUES ({organizationId})
             ON CONFLICT (organization_id) DO NOTHING
             """,
            cancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM area_tree_locks WHERE organization_id = {organizationId} FOR UPDATE",
            cancellationToken);
    }

    public Task<Area?> FindAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken) =>
        context.Areas
            .AsNoTracking()
            .SingleOrDefaultAsync(area => area.Id == areaId && area.OrganizationId == organizationId, cancellationToken);

    public async Task<Area?> LockForUpdateAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken, bool forDelete = false)
    {
        var query = forDelete
            ? context.Database.SqlQuery<Guid>(
                $"""
                 SELECT id AS "Value" FROM areas
                 WHERE id = {areaId} AND organization_id = {organizationId}
                 FOR UPDATE
                 """)
            : context.Database.SqlQuery<Guid>(
                $"""
                 SELECT id AS "Value" FROM areas
                 WHERE id = {areaId} AND organization_id = {organizationId}
                 FOR NO KEY UPDATE
                 """);
        var held = await query.ToListAsync(cancellationToken);

        return held.Count == 0 ? null : await LoadAsync(organizationId, areaId, cancellationToken);
    }

    public async Task<Area?> LockForShareAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken)
    {
        var held = await context.Database
            .SqlQuery<Guid>(
                $"""
                 SELECT id AS "Value" FROM areas
                 WHERE id = {areaId} AND organization_id = {organizationId}
                 FOR SHARE
                 """)
            .ToListAsync(cancellationToken);

        return held.Count == 0 ? null : await LoadAsync(organizationId, areaId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, AreaType>> LockManyForShareAsync(
        Guid organizationId, IReadOnlyCollection<Guid> areaIds, CancellationToken cancellationToken)
    {
        if (areaIds.Count == 0)
        {
            return new Dictionary<Guid, AreaType>();
        }

        var ids = areaIds.Distinct().ToArray();
        var held = await context.Database
            .SqlQuery<Guid>(
                $"""
                 SELECT id AS "Value" FROM areas
                 WHERE organization_id = {organizationId} AND id = ANY({ids})
                 ORDER BY id
                 FOR SHARE
                 """)
            .ToListAsync(cancellationToken);

        if (held.Count == 0)
        {
            return new Dictionary<Guid, AreaType>();
        }

        // The types of the rows the statement locked: a node's type never changes, so this read needs no lock of its own.
        var heldIds = held.ToArray();

        return await context.Areas
            .AsNoTracking()
            .Where(area => heldIds.Contains(area.Id))
            .ToDictionaryAsync(area => area.Id, area => area.Type, cancellationToken);
    }

    public async Task<Area?> LockProjectNodeAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        var held = await context.Database
            .SqlQuery<Guid>(
                $"""
                 SELECT id AS "Value" FROM areas
                 WHERE project_id = {projectId} AND organization_id = {organizationId} AND type = 2
                 FOR NO KEY UPDATE
                 """)
            .ToListAsync(cancellationToken);

        return held.Count == 0 ? null : await LoadAsync(organizationId, held[0], cancellationToken);
    }

    public Task<bool> HasChildrenAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken) =>
        context.Areas.AnyAsync(area => area.OrganizationId == organizationId && area.ParentId == areaId, cancellationToken);

    public Task<bool> IsSiblingNameTakenAsync(
        Guid organizationId, Guid? parentId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        context.Areas.AnyAsync(
            area => area.OrganizationId == organizationId
                && area.ParentId == parentId
                && area.NormalizedName == normalizedName
                && area.Id != exceptId,
            cancellationToken);

    public async Task<IReadOnlyList<AreaNode>> GetNodesAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await context.Areas
            .AsNoTracking()
            .Where(area => area.OrganizationId == organizationId)
            .OrderBy(area => EF.Functions.Collate(area.Name, OrganizationsCollation.Russian))
            .ThenBy(area => area.Id)
            .Select(area => new AreaNode(area.Id, area.ParentId, area.Name, area.Type, area.ProjectId))
            .ToListAsync(cancellationToken);

    public void Add(Area area) => context.Areas.Add(area);

    public void Remove(Area area) => context.Areas.Remove(area);

    public Task SaveAsync(CancellationToken cancellationToken) => OrganizationsSaveChanges.SaveAsync(context, cancellationToken);

    private Task<Area?> LoadAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken) =>
        context.Areas.SingleOrDefaultAsync(area => area.Id == areaId && area.OrganizationId == organizationId, cancellationToken);
}
