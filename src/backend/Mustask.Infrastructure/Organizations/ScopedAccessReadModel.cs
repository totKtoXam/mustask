using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The grants that reach an object, for the caller (ADR-0029 § 6, Reach, as ADR-0031 rule 7 and ADR-0037 § 2 read it), by the
/// caller's grants alone: a few indexed statements, each over tens of rows among thousands, nothing cached beyond the request.
/// </summary>
/// <remarks>
/// <b>Reach.</b> A grant on a team reaches that team only. A grant on a project reaches the project and, for area-scope codes, its
/// node and every sub-area below it, whatever the switches. A department grant reaches the department, then each child while that
/// child's «Наследовать доступ» is on: first a project node, then its sub-areas, each through its parent. The walk follows parent
/// pointers upward from the addressed node and stops at the first node whose switch is off; the tree is at most 10 levels deep, and
/// the loop bounds itself anyway. A department's own stored switch never takes effect (BR-SA-6): the walk never asks it.
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
                var start = await context.Areas.AsNoTracking()
                    .Where(area => area.OrganizationId == organizationId && area.Id == objectId)
                    .Select(area => new AreaStep(area.Id, area.Type, area.ParentId, area.ProjectId, area.InheritsAccess))
                    .SingleOrDefaultAsync(cancellationToken);

                if (start is null)
                {
                    return null;
                }

                var reaching = new List<ReachingGrant>();

                // A project grant reaches its node and every sub-area, whatever the switches (ADR-0037 § 2).
                if (start.ProjectId is { } projectId)
                {
                    reaching.AddRange(await ProjectGrantsAsync(projectId, userId, cancellationToken));
                }

                var current = start;

                for (var depth = 0; depth < MaxDepth; depth++)
                {
                    if (current.Type == AreaType.Department)
                    {
                        reaching.AddRange(await DepartmentGrantsAsync(current.Id, userId, cancellationToken));

                        break;
                    }

                    // A department's stored switch is not read; every other node must have its switch on to inherit from above.
                    if (!current.InheritsAccess || current.ParentId is not { } parentId)
                    {
                        break;
                    }

                    current = await context.Areas.AsNoTracking()
                        .Where(area => area.OrganizationId == organizationId && area.Id == parentId)
                        .Select(area => new AreaStep(area.Id, area.Type, area.ParentId, area.ProjectId, area.InheritsAccess))
                        .SingleAsync(cancellationToken);
                }

                return reaching;

            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "Not a scope a grant reaches.");
        }
    }

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

    private sealed record AreaStep(Guid Id, AreaType Type, Guid? ParentId, Guid? ProjectId, bool InheritsAccess);
}
