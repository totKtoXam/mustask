using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.SearchTeamMemberCandidates;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/{teamId}/member-candidates?text=</c> (feat-028 api-contract § 2.3, mod-016
/// api-contract § 2.2; US-3 "Only active organization members are offered and accepted", US-1 "Past 500 ids the list and the
/// member searches still answer"): the «Участник организации» search of the team page.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.5): the address from the outermost level in (the organization, the team),
/// then mod-014's own refusal of a text over 100 characters, passed through unchanged.
/// </para>
/// <para>
/// <b>Only active members of this organization are offered.</b> The search is restricted to the organization's member ids
/// with mod-014's <c>AmongIds</c>, split above 500 ids into batches and merged (<see cref="UserDirectoryBatches"/>,
/// BR-UP-17), so it finds a member among 501 and never refuses. A user of another organization, a projected user in none,
/// and a member whose account is disabled are not offered. A user already in the team is offered and refused on add. It
/// reads the projection only and works with Keycloak stopped (BR-UP-9). The search itself is
/// <see cref="TeamMemberCandidateSearch"/>, which <see cref="SearchNewTeamMemberCandidatesHandler"/> runs for a team not yet created.
/// </para>
/// </remarks>
public sealed class SearchTeamMemberCandidatesHandler(
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

        if (!AddressedId.TryParse(query.TeamId, out var teamId)
            || await teams.FindAsync(organizationId, teamId, cancellationToken) is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        // 2. The search, restricted to the organization's members (the one definition, shared with the creation form's search).
        return await TeamMemberCandidateSearch.SearchAsync(members, directory, organizationId, query.Text, cancellationToken);
    }
}
