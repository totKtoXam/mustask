using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetTeams;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams</c>
/// (api-contract § 3.1; US-4 "The project's teams", US-8 "The switcher lists the
/// active organizations, projects and teams" and "The default team is the
/// project's first team"): the active teams of a reachable project, the first
/// team first, then the rest in display order, each with its default area's id
/// and path.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3): the address, from the
/// outermost level in. The scoped check has answered for the
/// organization already; the handler parses it again only to name its container.
/// The address ends at the project, so nothing else can refuse a read.
/// </para>
/// <para>
/// <b>Reads, not locks.</b> The read models apply the reachability
/// specification (ADR-0026 rules 3 and 4, plan-backend § 6.2), so a deleted team,
/// a deactivated project and everything beneath a deactivated organization
/// never reach the handler. The server orders the list in SQL, first team first
/// and then by name in Russian collation (plan-backend § 6.5); the handler keeps
/// that order, and the client renders it as received (api-contract § 0.6). The
/// paths come from <see cref="AreaTree"/>, the one definition of a path (DC-9),
/// so a team's default area reads the same here as in the tree and in the seam.
/// </para>
/// <para>
/// <b>Teams first, then the tree.</b> A default area cannot be removed while its
/// team is active, so every area a listed team names is still in the tree read
/// after it. In the other order, a team added in between would name an area the
/// tree read had not seen. The tree read answers <c>null</c> for a project that is
/// not reachable (<see cref="IAreaTreeReadModel"/>) and the team read an empty
/// list, so the <c>null</c> is the project check, and no query is spent on a
/// separate one.
/// </para>
/// <para>
/// <b>Which teams the caller is in</b> (mod-016 BR-M1-27, D-5; US-7): each team carries
/// <c>isMember</c>, the caller's own membership, read with the list in the same query, so a
/// change to the memberships counts from the next read of the list. The choice of the
/// default team (the first of the list's order the caller is in, else the first) is the
/// panel's.
/// </para>
/// </remarks>
public sealed class GetTeamsHandler(ITeamReadModel teams, IAreaTreeReadModel areaTree, ICurrentUser currentUser)
{
    public async Task<GetTeamsResult> Handle(GetTeamsQuery query, CancellationToken cancellationToken)
    {
        // 1. The address, from the outermost level in (BR-M1-20: a malformed id is a not-found, never a 400).
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.ProjectId, out var projectId))
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        var rows = await teams.ListActiveAsync(organizationId, projectId, currentUser.SubjectId, cancellationToken);
        var nodes = await areaTree.GetNodesAsync(organizationId, projectId, cancellationToken)
            ?? throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);

        var tree = new AreaTree(nodes.Select(node => node.ToNode()));

        return new GetTeamsResult(
            rows.Select(row => new TeamResponse(
                    row.Id,
                    row.ProjectId,
                    row.Name,
                    row.IsFirstTeam,
                    new TeamDefaultAreaResponse(row.DefaultAreaId, tree.PathOf(row.DefaultAreaId)),
                    row.IsMember))
                .ToList());
    }
}
