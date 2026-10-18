using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.WorkItems;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetTeamDeletionCounts;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/{teamId}/deletion-counts</c> (feat-028 api-contract § 1.5, § 2.1; US-5 "the
/// confirmation counts per project"): the projects the team is linked to — <b>deactivated ones included</b>, a project with no item
/// of the team read as 0 — with the number of items the team owns in each, in any state (Removed included), read when the deletion's
/// confirmation opens. An item given to the team afterwards is not in a count already read, and is reassigned all the same.
/// </summary>
/// <remarks>
/// The order of answers (api-contract § 0.5): the organization, then the team (404 for a deleted team, a team of another organization
/// and a malformed id alike). <b>Three statements, whatever the number of projects</b> (2 or 50): the team, its links joined to the
/// projects of any status, and one <c>GROUP BY</c> over the items; merged by project id here. A read: no lock, no transaction. It reveals
/// the names of the team's projects, deactivated ones too, to the holder of <c>organizations.team.delete</c> — «Администратор»,
/// until <c>feat-041-project-visibility</c> filters it.
/// </remarks>
public sealed class GetTeamDeletionCountsHandler(ITeamReadModel teams, IWorkItemTeamOwnership ownership)
{
    public async Task<GetTeamDeletionCountsResult> Handle(GetTeamDeletionCountsQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.TeamId, out var teamId)
            || await teams.FindAsync(organizationId, teamId, cancellationToken) is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        var linkedProjects = await teams.ListLinksWithProjectsAsync(organizationId, teamId, cancellationToken);

        if (linkedProjects.Count == 0)
        {
            return new GetTeamDeletionCountsResult([]);
        }

        var counts = (await ownership.CountOwnedByProjectAsync(teamId, linkedProjects.Select(link => link.ProjectId).ToList(), cancellationToken))
            .ToDictionary(count => count.ProjectId, count => count.Count);

        return new GetTeamDeletionCountsResult(
            linkedProjects
                .Select(link => new ProjectDeletionCount(link.ProjectId, link.Name, link.IsActive, counts.GetValueOrDefault(link.ProjectId)))
                .ToList());
    }
}
