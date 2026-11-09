using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The grants that reach an object, for the caller (ADR-0029 § 6, Reach, as ADR-0031 rule 7 and ADR-0038 § 2 read it), by the
/// caller's grants alone: a few indexed statements, each over tens of rows among thousands, nothing cached beyond the request.
/// </summary>
/// <remarks>
/// <b>Reach.</b> A grant on a team reaches that team only. A grant on a project reaches the project and, for area-scope codes, its
/// node and every sub-area below it, whatever the switches. A department grant reaches the department, then each child while that
/// child's «Наследовать доступ» is on: first a project node, then its sub-areas, each through its parent. The walk follows parent
/// pointers upward from the addressed node and stops at the first node whose switch is off, all in one recursive statement; the tree is at most 10 levels deep, and
/// the walk bounds itself anyway. A department's own stored switch never takes effect (BR-SA-6): the walk never asks it.
/// </remarks>
internal sealed class ScopedAccessReadModel(MustaskDbContext context) : IScopedAccessReadModel
{
    private const int MaxDepth = 10;

    public async Task<IReadOnlyList<ReachingGrant>?> ReachingGrantsAsync(
        Guid organizationId, string userId, ResourceScope target, Guid objectId, CancellationToken cancellationToken)
    {
        switch (target)
        {
            case ResourceScope.Team:
                if (!await context.Teams.AsNoTracking().AnyAsync(team => team.OrganizationId == organizationId && team.Id == objectId, cancellationToken))
                {
                    return null;
                }

                return (await context.TeamGrants.AsNoTracking()
                        .Where(grant => grant.ObjectId == objectId && grant.UserId == userId)
                        .Select(grant => grant.Role).ToListAsync(cancellationToken))
                    .Select(role => new ReachingGrant(ResourceScope.Team, role)).ToList();

            case ResourceScope.Project:
                if (!await context.Projects.AsNoTracking().AnyAsync(project => project.OrganizationId == organizationId && project.Id == objectId, cancellationToken))
                {
                    return null;
                }

                var held = await ProjectGrantsAsync(objectId, userId, cancellationToken);
                var node = await context.Areas.AsNoTracking()
                    .Where(area => area.OrganizationId == organizationId && area.ProjectId == objectId && area.Type == AreaType.Project)
                    .Select(area => new { area.Id, area.ParentId, area.InheritsAccess })
                    .SingleOrDefaultAsync(cancellationToken);

                // The department above the node reaches the project while the node's switch is on; a top-level node has none.
                if (node is { InheritsAccess: true, ParentId: { } department })
                {
                    held.AddRange(await DepartmentGrantsAsync(department, userId, cancellationToken));
                }

                return held;

            case ResourceScope.Area:
                // One recursive statement walks the chain upward from the addressed node, so the count does not grow with the depth.
                var chain = await WalkAsync(organizationId, objectId, cancellationToken);

                if (chain.Count == 0)
                {
                    return null;
                }

                var reaching = new List<ReachingGrant>();

                // A project grant reaches its node and every sub-area, whatever the switches (ADR-0038 § 2).
                if (chain[0].ProjectId is { } projectId)
                {
                    reaching.AddRange(await ProjectGrantsAsync(projectId, userId, cancellationToken));
                }

                // The walk ends at a department only when every node below it passes access on; a department's own switch is never read.
                var top = chain[^1];

                if (top.Type == (short)AreaType.Department)
                {
                    reaching.AddRange(await DepartmentGrantsAsync(top.Id, userId, cancellationToken));
                }

                return reaching;

            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "Not a scope a grant reaches.");
        }
    }

    public async Task<AccessSet> LoadSetAsync(Guid organizationId, string userId, OrganizationRole role, CancellationToken cancellationToken)
    {
        // The administrator holds every code on every object: no grant or tree changes the answer, so nothing is read.
        if (role == OrganizationRole.Admin)
        {
            return new AccessSet(role, [], Group([]), Group([]), Group([]));
        }

        var nodes = await context.Areas.AsNoTracking().Where(area => area.OrganizationId == organizationId)
            .Select(area => new AccessNode(area.Id, area.Type, area.ParentId, area.ProjectId, area.InheritsAccess))
            .ToListAsync(cancellationToken);
        var areaGrants = (await context.AreaGrants.AsNoTracking().Where(g => g.OrganizationId == organizationId && g.UserId == userId)
            .Select(g => new { g.ObjectId, g.Role }).ToListAsync(cancellationToken)).Select(g => (g.ObjectId, g.Role));
        var projectGrants = (await context.ProjectGrants.AsNoTracking().Where(g => g.OrganizationId == organizationId && g.UserId == userId)
            .Select(g => new { g.ObjectId, g.Role }).ToListAsync(cancellationToken)).Select(g => (g.ObjectId, g.Role));
        var teamGrants = (await context.TeamGrants.AsNoTracking().Where(g => g.OrganizationId == organizationId && g.UserId == userId)
            .Select(g => new { g.ObjectId, g.Role }).ToListAsync(cancellationToken)).Select(g => (g.ObjectId, g.Role));

        return new AccessSet(role, nodes, Group(areaGrants), Group(projectGrants), Group(teamGrants));
    }

    private static IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>> Group(IEnumerable<(Guid Id, GrantRole Role)> rows) =>
        rows.GroupBy(row => row.Id).ToDictionary(group => group.Key, group => (IReadOnlyList<GrantRole>)group.Select(row => row.Role).ToList());

    private async Task<List<ReachingGrant>> ProjectGrantsAsync(Guid projectId, string userId, CancellationToken cancellationToken) =>
        (await context.ProjectGrants.AsNoTracking()
            .Where(grant => grant.ObjectId == projectId && grant.UserId == userId)
            .Select(grant => grant.Role).ToListAsync(cancellationToken))
        .Select(role => new ReachingGrant(ResourceScope.Project, role)).ToList();

    private async Task<List<ReachingGrant>> DepartmentGrantsAsync(Guid departmentId, string userId, CancellationToken cancellationToken) =>
        (await context.AreaGrants.AsNoTracking()
            .Where(grant => grant.ObjectId == departmentId && grant.UserId == userId)
            .Select(grant => grant.Role).ToListAsync(cancellationToken))
        .Select(role => new ReachingGrant(ResourceScope.Area, role)).ToList();

    // The chain from the addressed node up to the first node that does not pass access on (BR-SA-6: a department stops it and its
    // stored switch is not read). The depth bound is the tree's 10 levels; the walk follows (organization_id, parent_id), so a parent
    // of another organization cannot appear. Empty when the node is not in the organization.
    private async Task<List<AreaStepRow>> WalkAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken) =>
        await context.Database
            .SqlQuery<AreaStepRow>(
                $"""
                 WITH RECURSIVE walk AS (
                     SELECT a.id, a.type, a.parent_id, a.project_id, a.inherits_access, 0 AS depth
                     FROM areas AS a
                     WHERE a.organization_id = {organizationId} AND a.id = {areaId}
                   UNION ALL
                     SELECT p.id, p.type, p.parent_id, p.project_id, p.inherits_access, walk.depth + 1
                     FROM walk
                     JOIN areas AS p ON p.organization_id = {organizationId} AND p.id = walk.parent_id
                     WHERE walk.type <> 1 AND walk.inherits_access AND walk.depth < {MaxDepth}
                 )
                 SELECT id AS "Id", type AS "Type", project_id AS "ProjectId" FROM walk ORDER BY depth
                 """)
            .ToListAsync(cancellationToken);

    private sealed class AreaStepRow
    {
        public Guid Id { get; init; }

        public short Type { get; init; }

        public Guid? ProjectId { get; init; }
    }
}
