using Gridify;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.WorkItems.GetWorkItems;

/// <summary>
/// The project's list (US-2, BR-M3-29): the items of the project outside «Удалена» (mod-005 BR-DR-6), most recently changed
/// first, then by number descending, 50 a page, each with its owning team (mod-017 BR-OT-7). The filters are the team's
/// (mod-017 BR-OT-8) and feat-003's state (which also reaches «Удалена»), assignee and keyword (BR-WS-2); they combine with
/// AND, and a value is never validated and never an error.
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
        // meaning; the state, the assignee and the keyword (feat-003) are the shared specification's other dimensions. A value
        // is never validated and never an error. The count and the page both follow the filter; the project's key lets the
        // keyword tell the identifier of an item of this project from any other text.
        var filter = WorkItemFilter.Of(project.Key, query.TeamIds, query.States, query.AssigneeIds, query.Search);

        var result = await readModel.GetPageAsync(projectId, page, WorkItemLimits.ListPageSize, filter, cancellationToken);

        return new GetWorkItemsResult(new Paging<WorkItemListItemView>(result.Count, result.Items));
    }
}
