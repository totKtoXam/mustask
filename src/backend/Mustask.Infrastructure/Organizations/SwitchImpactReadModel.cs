using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The impact of turning «Наследовать доступ» off on a project node or a sub-area (mod-019 BR-SA-7, OQ-6, OQ-D1). A department's grants
/// reach a project node while its switch is on, and a sub-area through its parent while each switch up to the department is on; the users
/// who stop being reached are the department's grantees whose role holds a code that applies on the node, its project or sub-area
/// (a department «Читатель» holds no area-scope code, so a sub-area switch does not touch it), minus the organization administrators and the
/// holders of a project grant on the owning project, which the switch does not cut.
/// </summary>
internal sealed class SwitchImpactReadModel(MustaskDbContext context) : ISwitchImpactReadModel
{
    private const int MaxDepth = 10;

    public async Task<SwitchImpactData?> ReadAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken)
    {
        var node = await FindAsync(organizationId, areaId, cancellationToken);

        if (node is null)
        {
            return null;
        }

        if (node.Type == AreaType.Department || !node.InheritsAccess)
        {
            return Empty(node);
        }

        // The department whose grants reach the node now: the parent of a project node, or the first department up the chain while
        // every node on the way has its switch on.
        Guid? department = null;
        var current = node.ParentId is { } start ? await FindAsync(organizationId, start, cancellationToken) : null;

        if (node.Type == AreaType.Project)
        {
            department = current?.Type == AreaType.Department ? current.Id : null;
        }
        else
        {
            for (var depth = 0; depth < MaxDepth && current is not null; depth++)
            {
                if (current.Type == AreaType.Department)
                {
                    department = current.Id;
                    break;
                }

                if (!current.InheritsAccess || current.ParentId is not { } parent)
                {
                    break;
                }

                current = await FindAsync(organizationId, parent, cancellationToken);
            }
        }

        if (department is not { } reachedDepartment || node.ProjectId is not { } projectId)
        {
            return Empty(node);
        }

        var grants = await context.AreaGrants.AsNoTracking()
            .Where(grant => grant.ObjectId == reachedDepartment)
            .Select(grant => new { grant.UserId, grant.Role })
            .ToListAsync(cancellationToken);

        // A project node's switch cuts the department's codes on the project and on the node; a sub-area's cuts its area-scope codes only.
        var candidates = grants
            .Where(grant => ScopedRoleBundles.CodesOf(ResourceScope.Area, grant.Role).Any(code =>
                PermissionCatalog.ResourceScopeOf(code) == ResourceScope.Area
                || (node.Type == AreaType.Project && PermissionCatalog.ResourceScopeOf(code) == ResourceScope.Project)))
            .Select(grant => grant.UserId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (candidates.Count == 0)
        {
            return Empty(node);
        }

        var administrators = await context.OrganizationMemberships.AsNoTracking()
            .Where(membership => membership.OrganizationId == organizationId && membership.Role == OrganizationRole.Admin && candidates.Contains(membership.UserId))
            .Select(membership => membership.UserId)
            .ToListAsync(cancellationToken);
        var projectHolders = await context.ProjectGrants.AsNoTracking()
            .Where(grant => grant.ObjectId == projectId)
            .Select(grant => grant.UserId)
            .ToListAsync(cancellationToken);
        var affected = candidates.Except(administrators, StringComparer.Ordinal).Except(projectHolders, StringComparer.Ordinal).ToList();

        var given = context.ProjectGrants.AsNoTracking().Where(grant => grant.ObjectId == projectId && affected.Contains(grant.GrantedBy));
        var total = await given.CountAsync(cancellationToken);
        var listed = (await given.OrderBy(grant => grant.GrantedAt).ThenBy(grant => grant.UserId)
                .Take(SwitchImpactLimits.GivenGrantsListed)
                .Select(grant => new { grant.UserId, grant.Role, grant.GrantedBy })
                .ToListAsync(cancellationToken))
            .Select(grant => new GivenGrantRecord(grant.UserId, grant.Role, grant.GrantedBy))
            .ToList();

        return new SwitchImpactData(node.Type, node.ParentId, node.InheritsAccess, affected, listed, total);
    }

    private static SwitchImpactData Empty(Step node) => new(node.Type, node.ParentId, node.InheritsAccess, [], [], 0);

    private Task<Step?> FindAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        context.Areas.AsNoTracking()
            .Where(area => area.OrganizationId == organizationId && area.Id == id)
            .Select(area => new Step(area.Id, area.Type, area.ParentId, area.ProjectId, area.InheritsAccess))
            .SingleOrDefaultAsync(cancellationToken);

    private sealed record Step(Guid Id, AreaType Type, Guid? ParentId, Guid? ProjectId, bool InheritsAccess);
}
