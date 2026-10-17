using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetTeams;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams</c>
/// (feat-028 api-contract § 1.3, § 2.4; US-3 "only linked teams are offered", US-2 "a project without a team works", US-8 "The
/// switcher lists the active organizations, projects and teams"): the active teams <b>linked</b> to a reachable project,
/// <b>by name</b> (Russian collation, letter case not deciding, then id) — no team is first (mod-017 BR-OT-11) — each with its
/// default mark (BR-OL-7) and whether the caller is in it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.5): the address, from the outermost level in. The scoped check has answered for the
/// organization already; the handler parses it again only to name its container. A project that is deactivated, of another
/// organization or unknown answers <see cref="OrganizationErrorCodes.ProjectNotFound"/> (ADR-0026 rule 5); a reachable project
/// with no team — <b>every project right after the upgrade</b> — answers <c>[]</c> (BR-OL-13), no longer a not-found.
/// </para>
/// <para>
/// <b>Reads, not locks.</b> The read model applies the reachability specification (ADR-0026 rules 3 and 4), so a deleted team
/// never reaches the handler (a deletion removes its links) and the list of a deactivated project is not read. The server orders
/// the list in SQL; the handler keeps that order, and the client renders it as received. In a fixed number of statements whatever
/// the number of teams.
/// </para>
/// <para>
/// <b>Which teams the caller is in</b> (mod-016 BR-M1-27, D-5): each team carries <c>isMember</c>, the caller's own membership, read
/// with the list in the same query. The choice of the default team (the first of the list's order the caller is in, else the
/// first) is the panel's.
/// </para>
/// </remarks>
public sealed class GetTeamsHandler(IProjectReadModel projects, ITeamReadModel teams, ICurrentUser currentUser)
{
    public async Task<GetTeamsResult> Handle(GetTeamsQuery query, CancellationToken cancellationToken)
    {
        // 1. The address, from the outermost level in (BR-M1-20: a malformed id is a not-found, never a 400).
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.ProjectId, out var projectId)
            || await projects.FindReachableAsync(organizationId, projectId, cancellationToken) is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        var rows = await teams.ListLinkedAsync(organizationId, projectId, currentUser.SubjectId, cancellationToken);

        return new GetTeamsResult(rows.Select(row => new TeamResponse(row.Id, row.Name, row.Mark.ToWireName(), row.IsMember)).ToList());
    }
}
