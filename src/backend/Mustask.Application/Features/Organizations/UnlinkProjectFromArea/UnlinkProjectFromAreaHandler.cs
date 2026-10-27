using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.UnlinkProjectFromArea;

/// <summary>The command of <c>DELETE /api/organizations/{organizationId}/areas/{areaId}/projects/{projectId}</c>.</summary>
public sealed record UnlinkProjectFromAreaCommand(string? OrganizationId, string? AreaId, string? ProjectId);

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/areas/{areaId}/projects/{projectId}</c> (mod-018 api-contract § 3, US-4,
/// BR-AW-6): ends the link of the area and the project. A pair that is not linked is a success that changes nothing; a deactivated
/// project is not reachable (<c>project.not_found</c>, BR-AW-10), so its stored link is kept for a restore. The code is <c>area.link</c> alone. <b>Locks</b>: the area
/// <c>FOR SHARE</c>, then one <c>DELETE</c> — an area link has no binder, so no link lock is needed.
/// </summary>
public sealed class UnlinkProjectFromAreaHandler(
    IOrganizationReadModel organizations,
    IProjectRepository projects,
    IAreaRepository areas,
    IAreaLinkRepository areaLinks,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(UnlinkProjectFromAreaCommand command, CancellationToken cancellationToken) =>
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

                if (!AddressedId.TryParse(command.ProjectId, out var projectId)
                    || !await projects.IsReachableAsync(organizationId, projectId, ct))
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                await areaLinks.RemoveProjectLinkAsync(organizationId, area.Id, projectId, ct);

                return true;
            },
            cancellationToken);
}
