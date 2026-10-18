using Gridify;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.WorkItems.GetWorkItems;

/// <summary>
/// The project's list (US-2, BR-M3-29): every item of the project in every
/// state, most recently changed first, then by number descending, 50 a page, each
/// with its owning team (mod-017 BR-OT-7). The one filter is the team's (BR-OT-8); the
/// others, search and sort are feat-003's.
/// </summary>
/// <remarks>
/// The order of answers (api-contract § 0.4): the address from the outside in —
/// the scoped check has answered for the organization; the
/// project must be the organization's and reachable (BR-M3-27, mod-002
/// BR-M1-20) — then the page. Reads only, no transaction.
/// </remarks>
public sealed class GetWorkItemsHandler(IOrganizationStructure structure, IWorkItemReadModel readModel)
{
    public async Task<GetWorkItemsResult> Handle(GetWorkItemsQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.ProjectId, out var projectId)
            || await structure.FindProjectAsync(organizationId, projectId, cancellationToken) is not { IsReachable: true } project)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        var page = query.Page ?? 1;

        if (page < 1)
        {
            throw new ValidationException(
                WorkItemErrorCodes.ValidationFailed,
                new Dictionary<string, IReadOnlyList<FieldError>> { ["page"] = [new FieldError(WorkItemErrorCodes.InvalidPage)] });
        }

        // The team filter (mod-017 BR-OT-8) is the same filter as the project's backlog and board take, with the same
        // meaning; a value is never validated and never an error. The count and the page both follow it.
        var filter = WorkItemFilter.OfTeams(query.TeamIds);

        var result = await readModel.GetPageAsync(projectId, page, WorkItemLimits.ListPageSize, filter, cancellationToken);

        return new GetWorkItemsResult(new Paging<WorkItemListItemView>(result.Count, result.Items));
    }
}
