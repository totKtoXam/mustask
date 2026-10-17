using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetTeams;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams</c>
/// (mod-017 api-contract § 1.3, § 2.7; US-4 "The project's teams", US-8 "The switcher lists the
/// active organizations, projects and teams"): the active teams of a reachable project, <b>by name</b>
/// (Russian collation, letter case not deciding, then id) — no team is first (BR-OT-11) — each with its default mark
/// (BR-OT-4) and whether the caller is in it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3): the address, from the
/// outermost level in. The scoped check has answered for the
/// organization already; the handler parses it again only to name its container.
/// The address ends at the project, so nothing else can refuse a read.
/// </para>
/// <para>
/// <b>Reads, not locks.</b> The read model applies the reachability
/// specification (ADR-0026 rules 3 and 4, plan-backend § 6.2), so a deleted team,
/// a deactivated project and everything beneath a deactivated organization
/// never reach the handler. The server orders the list in SQL; the handler keeps
/// that order, and the client renders it as received (api-contract § 0.6). A project that is not
/// reachable gives an empty list, and the project check is the project's own read: a separate statement
/// would buy nothing, as the list of teams of a project that is not reachable is empty, so the
/// handler answers <see cref="OrganizationErrorCodes.ProjectNotFound"/> for it (a reachable project always has a team,
/// BR-OT-11).
/// </para>
/// <para>
/// <b>Which teams the caller is in</b> (mod-016 BR-M1-27, D-5; US-7): each team carries
/// <c>isMember</c>, the caller's own membership, read with the list in the same query, so a
/// change to the memberships counts from the next read of the list. The choice of the
/// default team (the first of the list's order the caller is in, else the first) is the
/// panel's.
/// </para>
/// </remarks>
public sealed class GetTeamsHandler(ITeamReadModel teams, ICurrentUser currentUser)
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

        if (rows.Count == 0)
        {
            // A project that is not reachable has no listed teams, and a reachable one always has at least one.
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        return new GetTeamsResult(
            rows.Select(row => new TeamResponse(row.Id, row.ProjectId, row.Name, row.Mark.ToWireName(), row.IsMember)).ToList());
    }
}
