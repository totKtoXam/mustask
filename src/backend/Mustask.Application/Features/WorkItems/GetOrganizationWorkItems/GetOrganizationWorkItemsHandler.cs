using Gridify;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Boards;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.WorkItems.GetOrganizationWorkItems;

/// <summary>
/// The organization-wide work-item list (feat-048, BR-NV-12): the items of every active project the selectors reach, 50 a page,
/// each once with its project and owning team, change time descending, number descending, project key, and the total of the
/// filtered list. A deleted item is listed only when a chosen state names its own, as the project's list does.
/// </summary>
public sealed class GetOrganizationWorkItemsHandler(
    IAggregateViewReadModel views,
    IBacklogReadModel backlogs,
    IOrganizationAccess access,
    ICurrentUser currentUser)
{
    public async Task<Paging<AggregateItemView>> Handle(GetOrganizationWorkItemsQuery query, CancellationToken cancellationToken)
    {
        var organizationId = AggregateSupport.OrganizationOf(query.Request.OrganizationId);
        var parsed = AggregateParameters.Parse(query.Request with { Grouping = null }, AggregateView.WorkItems, groupRead: false);
        var page = query.Page ?? 1;

        if (page < 1)
        {
            throw new ValidationException(
                WorkItemErrorCodes.ValidationFailed,
                new Dictionary<string, IReadOnlyList<FieldError>> { ["page"] = [new FieldError(WorkItemErrorCodes.InvalidPage)] });
        }

        var caller = new CallerAccess(organizationId, currentUser.SubjectId, access.Role);
        var scope = AggregateSupport.ScopeOf(AggregateView.WorkItems, null, parsed);
        var result = await views.ReadFlatPageAsync(caller, scope, page, WorkItemLimits.ListPageSize, cancellationToken);

        var items = await AggregateSupport.ItemsOfAsync(result.Rows, organizationId, backlogs, cancellationToken);

        return new Paging<AggregateItemView>(result.Count, items);
    }
}
