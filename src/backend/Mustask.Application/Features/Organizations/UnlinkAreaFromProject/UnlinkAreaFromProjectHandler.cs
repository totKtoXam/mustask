using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.UnlinkAreaFromProject;

/// <summary>The command of <c>DELETE /api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}</c>.</summary>
public sealed record UnlinkAreaFromProjectCommand(string? OrganizationId, string? ProjectId, string? AreaId);

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}</c> (mod-018 api-contract § 3, US-4, BR-AW-6):
/// ends the link of the project and the area. A pair that is not linked is a success that changes nothing. The code is
/// <c>project.link</c> alone. <b>Locks</b>: the area <c>FOR SHARE</c>, then one <c>DELETE</c>.
/// </summary>
public sealed class UnlinkAreaFromProjectHandler(
    IOrganizationReadModel organizations,
    IProjectRepository projects,
    IAreaRepository areas,
    IAreaLinkRepository areaLinks,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(UnlinkAreaFromProjectCommand command, CancellationToken cancellationToken) =>
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

                var area = AddressedId.TryParse(command.AreaId, out var areaId)
                    ? await areas.LockForShareAsync(organizationId, areaId, ct)
                    : null;

                if (area is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                await areaLinks.RemoveProjectLinkAsync(organizationId, area.Id, projectId, ct);

                return true;
            },
            cancellationToken);
}
