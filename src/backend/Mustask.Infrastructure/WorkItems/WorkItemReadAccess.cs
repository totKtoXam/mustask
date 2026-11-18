using Microsoft.EntityFrameworkCore;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// <see cref="IWorkItemReadAccess"/> from the same pieces the item's read goes through: the membership, the project's
/// reachability and <see cref="EffectivePermissions"/> with the grants that reach the project (mod-016, mod-019).
/// </summary>
internal sealed class WorkItemReadAccess(
    MustaskDbContext context,
    IOrganizationAccessReadModel accessReadModel,
    IScopedAccessReadModel scopedAccess,
    IOrganizationStructure structure) : IWorkItemReadAccess
{
    public async Task<bool> MayReadAsync(Guid workItemId, string subjectId, CancellationToken cancellationToken)
    {
        var projectId = await WorkItemSets.InAnyState(context)
            .AsNoTracking()
            .Where(item => item.Id == workItemId)
            .Select(item => (Guid?)item.ProjectId)
            .SingleOrDefaultAsync(cancellationToken);

        if (projectId is not { } project)
        {
            return false;
        }

        var organizationId = await context.Projects
            .AsNoTracking()
            .Where(candidate => candidate.Id == project)
            .Select(candidate => (Guid?)candidate.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);

        if (organizationId is not { } organization)
        {
            return false;
        }

        var row = await accessReadModel.FindAsync(organization, subjectId, cancellationToken);

        if (row is not { Status: OrganizationStatus.Active, Role: { } role })
        {
            return false;
        }

        if (await structure.FindProjectAsync(organization, project, cancellationToken) is not { IsReachable: true })
        {
            return false;
        }

        IReadOnlyList<ReachingGrant> reaching = [];

        if (role != OrganizationRole.Admin)
        {
            reaching = await scopedAccess.ReachingGrantsAsync(organization, subjectId, ResourceScope.Project, project, cancellationToken) ?? [];
        }

        return EffectivePermissions.Holds(role, ResourceScope.Project, reaching, PermissionCatalog.WorkItemsWorkItemRead);
    }
}
