using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.UnlinkAreaFromTeam;

/// <summary>The command of <c>DELETE /api/organizations/{organizationId}/teams/{teamId}/areas/{areaId}</c>.</summary>
public sealed record UnlinkAreaFromTeamCommand(string? OrganizationId, string? TeamId, string? AreaId);

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/teams/{teamId}/areas/{areaId}</c> (mod-018 api-contract § 3, US-4, BR-AW-6): ends
/// the link of the team and the area. A pair that is not linked is a success that changes nothing. The code is <c>team.link</c>
/// alone. A deleted team is a 404. <b>Locks</b>: the area <c>FOR SHARE</c>, then one <c>DELETE</c>.
/// </summary>
public sealed class UnlinkAreaFromTeamHandler(
    IOrganizationReadModel organizations,
    IOrganizationStructure structure,
    IAreaRepository areas,
    IAreaLinkRepository areaLinks,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(UnlinkAreaFromTeamCommand command, CancellationToken cancellationToken) =>
        _ = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId)
                    || await organizations.FindActiveAsync(organizationId, ct) is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.TeamId, out var teamId)
                    || await structure.FindTeamAsync(organizationId, teamId, ct) is not { IsReachable: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                var area = AddressedId.TryParse(command.AreaId, out var areaId)
                    ? await areas.LockForShareAsync(organizationId, areaId, ct)
                    : null;

                if (area is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                await areaLinks.RemoveTeamLinkAsync(organizationId, area.Id, teamId, ct);

                return true;
            },
            cancellationToken);
}
