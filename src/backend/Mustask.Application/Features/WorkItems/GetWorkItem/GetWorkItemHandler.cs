using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.WorkItems.GetWorkItem;

/// <summary>
/// One item by its address (US-3): the identifier is resolved inside the
/// address's organization, under the addressed project (BR-M3-5, D-5).
/// </summary>
/// <remarks>
/// Not found from the outside in (api-contract § 0.3): the organization (the
/// filter has answered), then the project — malformed, unknown, of another
/// organization or not reachable — then the item: a malformed identifier, a
/// project key that is not the addressed project's, an unknown number, or a
/// type key that differs from the item's type. Reads only, no transaction.
/// </remarks>
public sealed class GetWorkItemHandler(IOrganizationStructure structure, IWorkItemReadModel readModel)
{
    public async Task<GetWorkItemResult> Handle(GetWorkItemQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.ProjectId, out var projectId)
            || await structure.FindProjectAsync(projectId, cancellationToken) is not { IsReachable: true } project
            || project.OrganizationId != organizationId)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        if (!WorkItemIdentifier.TryParse(query.Identifier, out var identifier)
            || !string.Equals(identifier.ProjectKey, project.Key, StringComparison.Ordinal))
        {
            throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);
        }

        var item = await readModel.FindAsync(projectId, identifier.Number, cancellationToken);

        if (item is null || !string.Equals(item.Type.Key, identifier.TypeKey, StringComparison.Ordinal))
        {
            throw new NotFoundException(WorkItemErrorCodes.WorkItemNotFound);
        }

        return new GetWorkItemResult(item);
    }
}
