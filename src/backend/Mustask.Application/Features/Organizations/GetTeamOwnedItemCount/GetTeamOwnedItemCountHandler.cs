using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetTeamOwnedItemCount;

/// <summary>
/// <c>GET …/projects/{projectId}/teams/{teamId}/unlink-count</c> (feat-028 api-contract § 2.4, BR-OL-8; mod-017 US-7 for its earlier
/// form): the number of items a team owns <b>in this project</b>, in any state, read when the unlink's confirmation opens. An item
/// given to the team afterwards is not in a count already read, and is reassigned all the same. The route declares
/// <c>organizations.project.link</c> and the handler asks <c>organizations.team.link</c> first of all (D-7): it reveals only a number
/// to a role that may do the unlink.
/// </summary>
/// <remarks>
/// The order of answers (api-contract § 0.5): the second code (403) before any id is looked at; then the address from the outside
/// in — organization, project, team, each through the reachability specification — where <b>a team that is not linked to the project
/// is <see cref="OrganizationErrorCodes.TeamNotFound"/></b> (api-contract § 0.3), the same as a deleted team, a team of another
/// organization and a malformed id. One statement for the count, no lock and no transaction: it is a read.
/// </remarks>
public sealed class GetTeamOwnedItemCountHandler(
    IProjectReadModel projects,
    ITeamReadModel teams,
    IWorkItemTeamOwnership ownership,
    IOrganizationAccess access)
{
    public async Task<GetTeamOwnedItemCountResult> Handle(GetTeamOwnedItemCountQuery query, CancellationToken cancellationToken)
    {
        // 0. The second code (D-7), first of all: a role without it learns nothing about any id.
        if (!access.Holds(PermissionCatalog.OrganizationsTeamLink))
        {
            throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
        }

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
            || await teams.FindLinkedAsync(organizationId, projectId, teamId, cancellationToken) is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        return new GetTeamOwnedItemCountResult(await ownership.CountOwnedAsync(projectId, teamId, cancellationToken));
    }
}
