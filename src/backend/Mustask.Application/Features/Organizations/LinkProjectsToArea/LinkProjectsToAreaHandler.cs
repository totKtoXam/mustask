using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.LinkProjectsToArea;

/// <summary>The command of <c>POST /api/organizations/{organizationId}/areas/{areaId}/projects</c>.</summary>
public sealed record LinkProjectsToAreaCommand(string? OrganizationId, string? AreaId, IReadOnlyList<string?>? ProjectIds);

/// <summary>
/// <c>POST /api/organizations/{organizationId}/areas/{areaId}/projects</c> (mod-018 api-contract § 3, US-4, BR-AW-6): links the
/// projects to the area. All or none; a pair already linked is left as it is; an absent or empty list changes nothing.
/// </summary>
/// <remarks>
/// <b>The order of answers</b>: the second code (<c>project.link</c>) first of all; the organization; the area (404); the projects
/// (404, a deactivated project included). <b>Locks</b>: the area <c>FOR SHARE</c> — a deletion of it, which takes it <c>FOR UPDATE</c>,
/// serializes with this link, so a link to an area being deleted is refused as not found; the link row has no binder and takes no
/// lock (the insert is <c>ON CONFLICT DO NOTHING</c>).
/// </remarks>
public sealed class LinkProjectsToAreaHandler(
    IOrganizationReadModel organizations,
    IProjectRepository projects,
    IAreaRepository areas,
    IAreaLinkRepository areaLinks,
    IOrganizationAccess access,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(LinkProjectsToAreaCommand command, CancellationToken cancellationToken)
    {
        // 0. The second code, first of all (D-7).
        if (!access.Holds(PermissionCatalog.OrganizationsProjectLink))
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

                var area = AddressedId.TryParse(command.AreaId, out var areaId)
                    ? await areas.LockForShareAsync(organizationId, areaId, ct)
                    : null;

                if (area is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                var projectIds = AreaRequests.ParseIds(command.ProjectIds, OrganizationErrorCodes.ProjectNotFound);

                if (projectIds.Count == 0)
                {
                    return false;
                }

                if (await projects.CountReachableAsync(organizationId, projectIds, ct) != projectIds.Count)
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                await areaLinks.LinkProjectsAsync(
                    organizationId, projectIds.Select(id => (area.Id, id)).ToList(), currentUser.SubjectId, clock.UtcNow, ct);

                return true;
            },
            cancellationToken);
    }
}
