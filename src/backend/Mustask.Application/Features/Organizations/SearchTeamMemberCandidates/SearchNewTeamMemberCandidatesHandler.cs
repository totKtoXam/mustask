using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.SearchTeamMemberCandidates;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/member-candidates?text=</c> (feat-028 api-contract § 2.3a; design § 5.1; the human's
/// gate-3 choice «Серверный поиск сейчас»): the search of the «Участники» field of «Новая команда». It answers what a team's own search
/// answers — the organization's <b>active members</b> found by name or e-mail, same rules, same shape — for a team that is not created
/// yet, so there is no team to address.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.1, § 0.5): the scoped check (401, the organization's 404, the 403 of the route's
/// <c>organizations.team.create</c>) has run before this handler; here, <b>first of all</b>, the second code <c>organizations.team.write</c>
/// — the one a creation with <c>memberIds</c> asks (D-7), so the people offered are exactly the people the creation can accept — then the
/// address, then mod-014's own refusal of a text over 100 characters, passed through unchanged.
/// </para>
/// <para>
/// The search is <see cref="TeamMemberCandidateSearch"/>, the one the team's own route runs: it reads the projection only and works
/// with Keycloak stopped (BR-UP-9). Under today's bundles only «Администратор» holds both codes, so nothing differs from the team's
/// own search in who may ask.
/// </para>
/// </remarks>
public sealed class SearchNewTeamMemberCandidatesHandler(
    IMemberReadModel members,
    IUserDirectory directory,
    IOrganizationAccess access)
{
    public async Task<TeamMemberCandidatesResponse> Handle(SearchNewTeamMemberCandidatesQuery query, CancellationToken cancellationToken)
    {
        // 0. The second code, first of all (D-7): a role without it learns nothing.
        if (!access.Holds(PermissionCatalog.OrganizationsTeamWrite))
        {
            throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
        }

        // 1. The address: the scoped check has answered; the handler only refuses what it cannot read.
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        // 2. The search, restricted to the organization's members.
        return await TeamMemberCandidateSearch.SearchAsync(members, directory, organizationId, query.Text, cancellationToken);
    }
}
