using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.WorkItems;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetTeamOwnedItemCount;

/// <summary>
/// <c>GET …/teams/{teamId}/owned-items/count</c> (mod-017 US-7, BR-OT-12): the number of items a team owns, in any state, read
/// when the deletion's confirmation opens. An item given to the team afterwards is not in a count already read, and is
/// reassigned all the same. Requires <c>organizations.team.write</c> (the scoped check has answered 401/404/403): it reveals only
/// the number, to an administrator.
/// </summary>
/// <remarks>
/// The order of answers (api-contract § 0.4): the address from the outside in — organization, project, team, each through the
/// reachability specification, so a deleted team, a team of another project and a malformed id answer alike. One statement for
/// the count, no lock and no transaction: it is a read.
/// </remarks>
public sealed class GetTeamOwnedItemCountHandler(IProjectReadModel projects, ITeamReadModel teams, IWorkItemTeamOwnership ownership)
{
    public async Task<GetTeamOwnedItemCountResult> Handle(GetTeamOwnedItemCountQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.ProjectId, out var projectId)
            || await projects.FindReachableAsync(organizationId, projectId, cancellationToken) is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        if (!AddressedId.TryParse(query.TeamId, out var teamId)
            || await teams.FindReachableAsync(organizationId, projectId, teamId, cancellationToken) is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        return new GetTeamOwnedItemCountResult(await ownership.CountOwnedAsync(projectId, teamId, cancellationToken));
    }
}
