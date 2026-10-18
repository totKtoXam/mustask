using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetOrganizationTeams;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams</c> (feat-028 api-contract § 2.1; US-1 "the organization's teams are listed",
/// US-7 "what a user sees: no team anywhere"): the organization's active teams, <b>by name</b> (Russian collation, letter case not
/// deciding, then id), each with the active projects it is linked to.
/// </summary>
/// <remarks>
/// <b>The order of answers</b> (api-contract § 0.5): the organization only; nothing else can refuse a read, and an organization with
/// no team is the first state after the upgrade, <c>[]</c> (BR-OL-13). <b>Reads, not locks, in a fixed number of statements:</b>
/// the read model runs two whatever the number of teams or projects (an organization of 200 teams lists within 2 s). A deleted team
/// and a team of another organization never reach the handler (ADR-0026 rules 3 and 4; BR-OL-1).
/// </remarks>
public sealed class GetOrganizationTeamsHandler(ITeamReadModel teams, ICurrentUser currentUser)
{
    public async Task<GetOrganizationTeamsResult> Handle(GetOrganizationTeamsQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        var rows = await teams.ListOrganizationTeamsAsync(organizationId, currentUser.SubjectId, cancellationToken);

        return new GetOrganizationTeamsResult(
            rows.Select(row => new TeamListItemResponse(
                    row.Id,
                    row.Name,
                    row.Description,
                    row.Projects.Select(project => new TeamProjectResponse(project.Id, project.Name)).ToList(),
                    row.IsMember))
                .ToList());
    }
}
