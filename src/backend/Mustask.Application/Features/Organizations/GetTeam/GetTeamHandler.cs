using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetTeam;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}</c>
/// (api-contract § 3.2; US-4 "The project's teams" and "A team identifier that
/// names no team of the project"). One active team with its default area's id
/// and path.
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
/// committed rows of three statements. The default area comes from the same
/// area snapshot as its path (not from the team row's own copy of the id), so a
/// default that changes between the statements can never leave a path that
/// names an area the snapshot no longer holds. A team whose default is no
/// longer in the snapshot was deleted meanwhile (its default mark is cleared in
/// the deleting transaction, BR-M1-18) and answers as a deleted team does.
/// </para>
/// </remarks>
public sealed class GetTeamHandler(IProjectReadModel projects, ITeamReadModel teams, IAreaTreeReadModel areaTree)
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

        // 2. The area snapshot, for the default area's path (DC-9) through the one definition of it. A project that was
        // deactivated since the check above is not reachable any more.
        var nodes = await areaTree.GetNodesAsync(organizationId, projectId, cancellationToken)
            ?? throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        var tree = new AreaTree(nodes.Select(node => node.ToNode()));

        var defaultArea = tree.PreOrder().FirstOrDefault(node => node.DefaultOfTeamId == team.Id)
            ?? throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);

        return new GetTeamResult(new TeamResponse(
            team.Id,
            team.ProjectId,
            team.Name,
            team.IsFirstTeam,
            new TeamDefaultAreaResponse(defaultArea.Id, tree.PathOf(defaultArea.Id))));
    }
}
