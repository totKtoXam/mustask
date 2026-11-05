using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.UnlinkTeamFromArea;

/// <summary>The command of <c>DELETE /api/organizations/{organizationId}/areas/{areaId}/teams/{teamId}</c>.</summary>
public sealed record UnlinkTeamFromAreaCommand(string? OrganizationId, string? AreaId, string? TeamId);

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/areas/{areaId}/teams/{teamId}</c> (mod-018 api-contract § 3, US-4, BR-AW-6): ends
/// the link of the area and the team. A pair that is not linked is a success that changes nothing; a deactivated
/// team is not reachable (<c>team.not_found</c>), so its link is kept. The code is <c>area.link</c>
/// alone. <b>Locks</b>: the area <c>FOR SHARE</c>, then one <c>DELETE</c> (no binder keys onto an area link).
/// </summary>
public sealed class UnlinkTeamFromAreaHandler(
    IOrganizationReadModel organizations,
    IOrganizationStructure structure,
    IAreaRepository areas,
    IAreaLinkRepository areaLinks,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(UnlinkTeamFromAreaCommand command, CancellationToken cancellationToken) =>
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

                if (!AddressedId.TryParse(command.TeamId, out var teamId)
                    || await structure.FindTeamAsync(organizationId, teamId, ct) is not { IsReachable: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                await areaLinks.RemoveTeamLinkAsync(organizationId, area.Id, teamId, ct);

                return true;
            },
            cancellationToken);
}
