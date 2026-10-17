using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetTeam;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}</c>
/// (mod-017 api-contract § 1.3; US-4 "The project's teams" and "A team identifier that
/// names no team of the project"). One active team with its default mark (BR-OT-4).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3, api-contract § 0.4): the
/// address from the outermost level in, each level through the reachability
/// specification (ADR-0026 rules 3 and 5): the organization, then the project,
/// then the team. There is nothing else to refuse: no rule about the team alone,
/// no body.
/// </para>
/// <para>
/// <b>No locks and no unit of work</b> (plan-backend § 8): a read takes the
/// committed rows of two statements. A team has no area (mod-017 BR-OT-10), so no tree is read.
/// </para>
/// </remarks>
public sealed class GetTeamHandler(IProjectReadModel projects, ITeamReadModel teams)
{
    public async Task<GetTeamResult> Handle(GetTeamQuery query, CancellationToken cancellationToken)
    {
        // 1. The address, from the outermost level in. The scoped check has answered for the
        // organization already; the handler parses it again only to name its container in the next read.
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.ProjectId, out var projectId)
            || await projects.FindReachableAsync(organizationId, projectId, cancellationToken) is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        // A malformed, unknown, deleted team and one of another project or organization all answer alike (BR-M1-20, BR-M1-22).
        var team = AddressedId.TryParse(query.TeamId, out var teamId)
            ? await teams.FindReachableAsync(organizationId, projectId, teamId, cancellationToken)
            : null;

        if (team is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        return new GetTeamResult(new TeamResponse(team.Id, team.ProjectId, team.Name, team.Mark.ToWireName(), team.IsMember));
    }
}
