using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.SearchTeamMemberCandidates;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/member-candidates?text=</c> (api-contract
/// § 2.2; US-3 "Only active organization members are offered and accepted", US-1 "Past 500 ids the list and the member
/// searches still answer"): the «Участник организации» search of the team page.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.1): the address from the outermost level in (the organization, the
/// project, the team), then mod-014's own refusal of a text over 100 characters, passed through unchanged.
/// </para>
/// <para>
/// <b>Only active members of this organization are offered.</b> The search is restricted to the organization's member ids
/// with mod-014's <c>AmongIds</c>, split above 500 ids into batches and merged (<see cref="UserDirectoryBatches"/>,
/// BR-UP-17), so it finds a member among 501 and never refuses. A user of another organization, a projected user in none,
/// and a member whose account is disabled are not offered. A user already in the team is offered and refused on add. It
/// reads the projection only and works with Keycloak stopped (BR-UP-9).
/// </para>
/// </remarks>
public sealed class SearchTeamMemberCandidatesHandler(
    IProjectReadModel projects,
    ITeamReadModel teams,
    IMemberReadModel members,
    IUserDirectory directory)
{
    public async Task<TeamMemberCandidatesResponse> Handle(SearchTeamMemberCandidatesQuery query, CancellationToken cancellationToken)
    {
        // 1. The address, from the outermost level in.
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

        // 2. The search, restricted to the organization's members.
        var memberIds = await members.ListMemberIdsAsync(organizationId, cancellationToken);
        var result = await UserDirectoryBatches.SearchAmongAsync(directory, query.Text ?? string.Empty, memberIds, cancellationToken);

        return new TeamMemberCandidatesResponse(
            result.Users.Select(user => new TeamMemberCandidateResponse(user.Id, user.DisplayName, user.Email)).ToList(),
            result.MoreMatched);
    }
}
