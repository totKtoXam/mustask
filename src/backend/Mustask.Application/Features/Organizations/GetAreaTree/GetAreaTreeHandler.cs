using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetAreaTree;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/areas</c>
/// (US-5 "tree registry", US-6 "inherits from the nearest owned ancestor" and
/// "other modules get the same answer as the tree"). The whole tree of a
/// reachable project in pre-order, each node with its path, its depth, its
/// explicit owner, its resolved owner and the team whose default it is
/// (api-contract § 4.1).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3): only the address
/// applies. The organization-context filter has answered for the organization
/// already; the handler parses it again only to name the container of the next
/// read. A malformed, unknown, deactivated or foreign project, or one of a
/// deactivated organization, is <see cref="OrganizationErrorCodes.ProjectNotFound"/>
/// (BR-M1-20, BR-M1-22, ADR-0026 rule 5).
/// </para>
/// <para>
/// <b>Reads, no locks, no transaction.</b> The read model returns the rows of
/// one project, siblings already in display order, in two queries whatever the
/// tree's size (NFR Volumes). Everything else is assembled here in memory
/// through <see cref="AreaTree"/>, the single definition of pre-order, path,
/// depth and owner resolution, which the team reads and the in-process seam use
/// too, so other modules get the tree's own answer (US-6).
/// </para>
/// </remarks>
public sealed class GetAreaTreeHandler(IAreaTreeReadModel readModel)
{
    public async Task<GetAreaTreeResult> Handle(GetAreaTreeQuery query, CancellationToken cancellationToken)
    {
        // The address, from the outermost level in.
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.ProjectId, out var projectId))
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        var rows = await readModel.GetNodesAsync(organizationId, projectId, cancellationToken)
            ?? throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);

        var tree = new AreaTree(rows.Select(row => row.ToNode()));
        var teams = TeamNames(rows);

        var items = tree.PreOrder()
            .Select(node =>
            {
                var owner = tree.ResolveOwner(node.Id);

                return new AreaNodeResponse(
                    node.Id,
                    node.ParentId,
                    node.Name,
                    tree.PathOf(node.Id),
                    tree.DepthOf(node.Id),
                    node.OwnerTeamId,
                    TeamOf(teams, owner.TeamId),
                    owner.IsInherited,
                    node.DefaultOfTeamId is { } defaultTeamId ? TeamOf(teams, defaultTeamId) : null);
            })
            .ToList();

        return new GetAreaTreeResult(items);
    }

    /// <summary>
    /// The name of every team a node names, as its own row carries it: an area's
    /// explicit owner and the team whose default it is. A resolved owner is always
    /// the explicit owner of the node itself or of an ancestor, so it is in here.
    /// </summary>
    private static Dictionary<Guid, string> TeamNames(IReadOnlyList<AreaTreeRow> rows)
    {
        var names = new Dictionary<Guid, string>();

        foreach (var row in rows)
        {
            if (row.OwnerTeamId is { } ownerId && row.OwnerTeamName is { } ownerName)
            {
                names.TryAdd(ownerId, ownerName);
            }

            if (row.DefaultOfTeamId is { } defaultId && row.DefaultOfTeamName is { } defaultName)
            {
                names.TryAdd(defaultId, defaultName);
            }
        }

        return names;
    }

    private static AreaTeamResponse TeamOf(Dictionary<Guid, string> names, Guid teamId) =>
        names.TryGetValue(teamId, out var name)
            ? new AreaTeamResponse(teamId, name)
            : throw new InvalidOperationException($"The team {teamId} is named by an area of the tree but has no name in its rows.");
}
