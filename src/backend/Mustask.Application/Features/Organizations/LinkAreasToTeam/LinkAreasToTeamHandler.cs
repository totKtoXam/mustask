using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.LinkAreasToTeam;

/// <summary>The command of <c>POST /api/organizations/{organizationId}/teams/{teamId}/areas</c>.</summary>
public sealed record LinkAreasToTeamCommand(string? OrganizationId, string? TeamId, IReadOnlyList<string?>? AreaIds);

/// <summary>
/// <c>POST /api/organizations/{organizationId}/teams/{teamId}/areas</c> (mod-018 api-contract § 3, US-4, BR-AW-6): links the team to
/// the areas. All or none; a pair already linked is left as it is; an absent or empty list changes nothing.
/// </summary>
/// <remarks>
/// <b>The order of answers</b>: the second code (<c>area.link</c>) first of all; the organization; the team (404, a deleted one
/// included, read unlocked); the areas (404). <b>Locks</b>, in rule 8's order: the areas <c>FOR SHARE</c> in one statement ordered by
/// id, then the team <c>FOR SHARE</c> (the binder's lock) and its status judged again under it — a team deleted meanwhile answers
/// not found and no link is left for it.
/// </remarks>
public sealed class LinkAreasToTeamHandler(
    IOrganizationReadModel organizations,
    ITeamRepository teams,
    IOrganizationStructure structure,
    IAreaRepository areas,
    IAreaLinkRepository areaLinks,
    IOrganizationAccess access,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(LinkAreasToTeamCommand command, CancellationToken cancellationToken)
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

                if (!AddressedId.TryParse(command.TeamId, out var teamId)
                    || await structure.FindTeamAsync(organizationId, teamId, ct) is not { IsReachable: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
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

                // Teams link to a department only (OQ-8 A, OQ-11 A): one node of another type refuses the whole request.
                if (locked.Values.Any(type => type != AreaType.Department))
                {
                    throw new ConflictException(OrganizationErrorCodes.AreaLinkTypeNotAllowed);
                }

                if (await teams.LockForShareAsync(organizationId, teamId, ct) is not { IsActive: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                await areaLinks.LinkTeamsAsync(
                    organizationId, areaIds.Select(id => (id, teamId)).ToList(), currentUser.SubjectId, clock.UtcNow, ct);

                return true;
            },
            cancellationToken);
    }
}
