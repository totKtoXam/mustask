using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The three grant tables behind <see cref="IGrantRepository"/> (mod-019 plan-backend P-2). The object is resolved by the module's
/// own repositories, so a department, a project and a team are locked the way every other writer that binds to them locks them
/// (ADR-0026 rule 8): the department and the team <c>FOR SHARE</c>, the project <c>FOR NO KEY UPDATE</c>. The grant rows are
/// written through the change tracker (the <c>xmin</c> convention makes a change under another change fail) and removed with one
/// set-based statement.
/// </summary>
internal sealed class GrantRepository(
    MustaskDbContext context,
    IAreaRepository areas,
    IProjectRepository projects,
    ITeamRepository teams) : IGrantRepository
{
    public async Task<GrantObjectState> ResolveAsync(
        ResourceScope scope, Guid organizationId, Guid objectId, bool lockRow, CancellationToken cancellationToken)
    {
        switch (scope)
        {
            case ResourceScope.Area:
                var area = lockRow
                    ? await areas.LockForShareAsync(organizationId, objectId, cancellationToken)
                    : await areas.FindAsync(organizationId, objectId, cancellationToken);

                return area is null
                    ? GrantObjectState.NotFound
                    : area.Type == AreaType.Department ? GrantObjectState.Ok : GrantObjectState.ScopeNotAllowed;

            case ResourceScope.Project:
                if (lockRow)
                {
                    var project = await projects.LockAsync(organizationId, objectId, cancellationToken);

                    return project is { IsActive: true } ? GrantObjectState.Ok : GrantObjectState.NotFound;
                }

                return await projects.IsReachableAsync(organizationId, objectId, cancellationToken) ? GrantObjectState.Ok : GrantObjectState.NotFound;

            case ResourceScope.Team:
                var team = await teams.LockForShareAsync(organizationId, objectId, cancellationToken);

                return team is { IsActive: true } ? GrantObjectState.Ok : GrantObjectState.NotFound;

            default:
                throw new ArgumentOutOfRangeException(nameof(scope), scope, "Not a grant scope.");
        }
    }

    public async Task<IReadOnlyList<GrantRecord>> ListAsync(ResourceScope scope, Guid objectId, CancellationToken cancellationToken)
    {
        var rows = scope switch
        {
            ResourceScope.Area => await context.AreaGrants.AsNoTracking().Where(grant => grant.ObjectId == objectId)
                .Select(grant => new { grant.UserId, grant.Role, grant.GrantedBy, grant.GrantedAt }).ToListAsync(cancellationToken),
            ResourceScope.Project => await context.ProjectGrants.AsNoTracking().Where(grant => grant.ObjectId == objectId)
                .Select(grant => new { grant.UserId, grant.Role, grant.GrantedBy, grant.GrantedAt }).ToListAsync(cancellationToken),
            ResourceScope.Team => await context.TeamGrants.AsNoTracking().Where(grant => grant.ObjectId == objectId)
                .Select(grant => new { grant.UserId, grant.Role, grant.GrantedBy, grant.GrantedAt }).ToListAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Not a grant scope."),
        };

        return rows.Select(row => new GrantRecord(row.UserId, row.Role, row.GrantedBy, row.GrantedAt)).ToList();
    }

    public async Task<GrantRecord?> FindAsync(ResourceScope scope, Guid objectId, string userId, CancellationToken cancellationToken)
    {
        var row = scope switch
        {
            ResourceScope.Area => await context.AreaGrants.AsNoTracking().Where(grant => grant.ObjectId == objectId && grant.UserId == userId)
                .Select(grant => new GrantRecord(grant.UserId, grant.Role, grant.GrantedBy, grant.GrantedAt)).SingleOrDefaultAsync(cancellationToken),
            ResourceScope.Project => await context.ProjectGrants.AsNoTracking().Where(grant => grant.ObjectId == objectId && grant.UserId == userId)
                .Select(grant => new GrantRecord(grant.UserId, grant.Role, grant.GrantedBy, grant.GrantedAt)).SingleOrDefaultAsync(cancellationToken),
            ResourceScope.Team => await context.TeamGrants.AsNoTracking().Where(grant => grant.ObjectId == objectId && grant.UserId == userId)
                .Select(grant => new GrantRecord(grant.UserId, grant.Role, grant.GrantedBy, grant.GrantedAt)).SingleOrDefaultAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Not a grant scope."),
        };

        return row;
    }

    public async Task<ProjectInheritance?> FindProjectInheritanceAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        var node = await context.Areas.AsNoTracking()
            .Where(area => area.OrganizationId == organizationId && area.ProjectId == projectId && area.Type == AreaType.Project)
            .Select(area => new { area.ParentId, area.InheritsAccess })
            .SingleOrDefaultAsync(cancellationToken);

        if (node?.ParentId is not { } parentId)
        {
            return null;
        }

        var department = await context.Areas.AsNoTracking()
            .Where(area => area.OrganizationId == organizationId && area.Id == parentId)
            .Select(area => area.Name)
            .SingleAsync(cancellationToken);

        return new ProjectInheritance(parentId, department, node.InheritsAccess);
    }

    public async Task AddAsync(
        ResourceScope scope, Guid organizationId, Guid objectId, string userId, GrantRole role, string grantedBy, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        switch (scope)
        {
            case ResourceScope.Area:
                context.AreaGrants.Add(AreaGrant.Give(organizationId, objectId, userId, role, grantedBy, now));
                break;
            case ResourceScope.Project:
                context.ProjectGrants.Add(ProjectGrant.Give(organizationId, objectId, userId, role, grantedBy, now));
                break;
            case ResourceScope.Team:
                context.TeamGrants.Add(TeamGrant.Give(organizationId, objectId, userId, role, grantedBy, now));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scope), scope, "Not a grant scope.");
        }

        await OrganizationsSaveChanges.SaveAsync(context, cancellationToken);
    }

    public async Task<bool> ChangeRoleAsync(
        ResourceScope scope, Guid objectId, string userId, GrantRole role, string grantedBy, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        switch (scope)
        {
            case ResourceScope.Area:
                var area = await context.AreaGrants.SingleOrDefaultAsync(grant => grant.ObjectId == objectId && grant.UserId == userId, cancellationToken);
                area?.Replace(role, grantedBy, now);
                break;
            case ResourceScope.Project:
                var project = await context.ProjectGrants.SingleOrDefaultAsync(grant => grant.ObjectId == objectId && grant.UserId == userId, cancellationToken);
                project?.Replace(role, grantedBy, now);
                break;
            case ResourceScope.Team:
                var team = await context.TeamGrants.SingleOrDefaultAsync(grant => grant.ObjectId == objectId && grant.UserId == userId, cancellationToken);
                team?.Replace(role, grantedBy, now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scope), scope, "Not a grant scope.");
        }

        if (!context.ChangeTracker.HasChanges())
        {
            return false;
        }

        try
        {
            await OrganizationsSaveChanges.SaveAsync(context, cancellationToken);

            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Changed or removed under this write: the caller answers grant.changed.
            return false;
        }
    }

    public async Task<bool> RemoveAsync(ResourceScope scope, Guid objectId, string userId, CancellationToken cancellationToken)
    {
        var removed = scope switch
        {
            ResourceScope.Area => await context.AreaGrants.Where(grant => grant.ObjectId == objectId && grant.UserId == userId).ExecuteDeleteAsync(cancellationToken),
            ResourceScope.Project => await context.ProjectGrants.Where(grant => grant.ObjectId == objectId && grant.UserId == userId).ExecuteDeleteAsync(cancellationToken),
            ResourceScope.Team => await context.TeamGrants.Where(grant => grant.ObjectId == objectId && grant.UserId == userId).ExecuteDeleteAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Not a grant scope."),
        };

        return removed > 0;
    }
}
