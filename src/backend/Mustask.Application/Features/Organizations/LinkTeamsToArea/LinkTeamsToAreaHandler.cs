using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.LinkTeamsToArea;

/// <summary>The command of <c>POST /api/organizations/{organizationId}/areas/{areaId}/teams</c>.</summary>
public sealed record LinkTeamsToAreaCommand(string? OrganizationId, string? AreaId, IReadOnlyList<string?>? TeamIds);

/// <summary>
/// <c>POST /api/organizations/{organizationId}/areas/{areaId}/teams</c> (mod-018 api-contract § 3, US-4, BR-AW-6): links the teams
/// to the area. All or none; a pair already linked is left as it is; an absent or empty list changes nothing.
/// </summary>
/// <remarks>
/// <b>The order of answers</b>: the second code (<c>team.link</c>) first of all; the organization; the area (404); the teams (404, a
/// deleted team included). <b>Locks</b>, in rule 8's order: the area <c>FOR SHARE</c>, then the teams <c>FOR SHARE</c> in one
/// statement ordered by id — the binder's lock, so a team's deletion (which takes the team <c>FOR NO KEY UPDATE</c> and ends its area
/// links) either committed before this link reads the team as deleted or waits for it and ends the link too.
/// </remarks>
public sealed class LinkTeamsToAreaHandler(
    IOrganizationReadModel organizations,
    ITeamRepository teams,
    IAreaRepository areas,
    IAreaLinkRepository areaLinks,
    IOrganizationAccess access,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(LinkTeamsToAreaCommand command, CancellationToken cancellationToken)
    {
        // 0. The second code, first of all (D-7).
        if (!access.Holds(PermissionCatalog.OrganizationsTeamLink))
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

                var teamIds = AreaRequests.ParseIds(command.TeamIds, OrganizationErrorCodes.TeamNotFound);

                if (teamIds.Count == 0)
                {
                    return false;
                }

                var locked = await teams.LockManyForShareAsync(organizationId, teamIds, ct);

                if (locked.Count != teamIds.Count || locked.Any(team => !team.IsActive))
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                await areaLinks.LinkTeamsAsync(
                    organizationId, teamIds.Select(id => (area.Id, id)).ToList(), currentUser.SubjectId, clock.UtcNow, ct);

                return true;
            },
            cancellationToken);
    }
}
