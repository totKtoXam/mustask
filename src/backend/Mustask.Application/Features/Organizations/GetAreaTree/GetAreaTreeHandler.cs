using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetAreaTree;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/areas</c>
/// (US-5 "tree registry"; mod-017 US-6: the tree shows names and places, nothing owned). The whole tree of a
/// reachable project in pre-order, each node with its path and its depth (mod-017 api-contract § 1.6).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3): only the address
/// applies. The scoped check has answered for the organization
/// already; the handler parses it again only to name the container of the next
/// read. A malformed, unknown, deactivated or foreign project, or one of a
/// deactivated organization, is <see cref="OrganizationErrorCodes.ProjectNotFound"/>
/// (BR-M1-20, BR-M1-22, ADR-0026 rule 5).
/// </para>
/// <para>
/// <b>Reads, no locks, no transaction.</b> The read model returns the rows of
/// one project, siblings already in display order, in two queries whatever the
/// tree's size (NFR Volumes). Everything else is assembled here in memory
/// through <see cref="AreaTree"/>, the single definition of pre-order, path
/// and depth, which the work item reads and the in-process seam use
/// too, so other modules get the tree's own answer.
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

        var items = tree.PreOrder()
            .Select(node => new AreaNodeResponse(node.Id, node.ParentId, node.Name, tree.PathOf(node.Id), tree.DepthOf(node.Id)))
            .ToList();

        return new GetAreaTreeResult(items);
    }
}
