using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.LinkAreasToProject;

/// <summary>The command of <c>POST /api/organizations/{organizationId}/projects/{projectId}/areas</c>.</summary>
public sealed record LinkAreasToProjectCommand(string? OrganizationId, string? ProjectId, IReadOnlyList<string?>? AreaIds);

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/areas</c> (mod-018 api-contract § 3, US-4, BR-AW-6): links the
/// project to the areas. All or none; a pair already linked is left as it is; an absent or empty list changes nothing.
/// </summary>
/// <remarks>
/// <b>The order of answers</b>: the second code (<c>area.link</c>) first of all; the organization; the project (404, a deactivated
/// one included); the areas (404). <b>Locks</b>: the areas <c>FOR SHARE</c> in one statement ordered by id, each judged present by
/// the statement's own answer. The project row is not locked: a deactivation committing meanwhile leaves the link stored and inert
/// (BR-AW-10).
/// </remarks>
public sealed class LinkAreasToProjectHandler(
    IOrganizationReadModel organizations,
    IProjectRepository projects,
    IAreaRepository areas,
    IAreaLinkRepository areaLinks,
    IOrganizationAccess access,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(LinkAreasToProjectCommand command, CancellationToken cancellationToken)
    {
        // 0. The second code, first of all (D-7).
        if (!access.Holds(PermissionCatalog.OrganizationsAreaLink))
        {
            throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
        }

        _ = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId)
                    || await organizations.FindActiveAsync(organizationId, ct) is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.ProjectId, out var projectId)
                    || !await projects.IsReachableAsync(organizationId, projectId, ct))
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                var areaIds = AreaRequests.ParseIds(command.AreaIds, OrganizationErrorCodes.AreaNotFound);

                if (areaIds.Count == 0)
                {
                    return false;
                }

                var locked = await areas.LockManyForShareAsync(organizationId, areaIds, ct);

                if (locked.Count != areaIds.Count)
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                await areaLinks.LinkProjectsAsync(
                    organizationId, areaIds.Select(id => (id, projectId)).ToList(), currentUser.SubjectId, clock.UtcNow, ct);

                return true;
            },
            cancellationToken);
    }
}
