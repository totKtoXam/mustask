using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.WorkItems.GetAssigneeOptions;

/// <summary>
/// The options of the list's «Исполнитель» filter (feat-003 US-2, BR-WS-6, BR-WS-7): the people assigned to the project's items —
/// members, former members and disabled accounts alike — and, while «Статус» names «Удалена», those of its deleted items too.
/// The read model judges "names «Удалена»" by data and category, so the panel computes nothing; this handler only keeps the state
/// names a request can mean.
/// </summary>
/// <remarks>
/// The order of answers is the list's (mod-003 api-contract § 0.3): the scoped check has answered for the organization; the
/// project must be the organization's and reachable. A state value is never validated and never an error. Reads only.
/// </remarks>
public sealed class GetAssigneeOptionsHandler(IOrganizationStructure structure, IWorkItemReadModel readModel)
{
    public async Task<GetAssigneeOptionsResult> Handle(GetAssigneeOptionsQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.ProjectId, out var projectId)
            || await structure.FindProjectAsync(organizationId, projectId, cancellationToken) is not { IsReachable: true })
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        var chosen = WorkItemFilter.OfStates(query.States);

        return new GetAssigneeOptionsResult(await readModel.GetAssigneeOptionsAsync(projectId, chosen.StateNames, cancellationToken));
    }
}
