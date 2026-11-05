using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Boards.GetWorkItemGroupRows;

/// <summary>
/// A page of one group of the grouped work-item list (feat-048 BR-AV-8, BR-AV-9): every type, change time descending, number
/// descending, project key (BR-NV-12), for every grouping, a by-project group included (no rank in the list).
/// </summary>
public sealed class GetWorkItemGroupRowsHandler(
    IAggregateViewReadModel views,
    IBacklogReadModel backlogs,
    IOrganizationAccess access,
    ICurrentUser currentUser)
{
    public async Task<WorkItemGroupRowsResponse> Handle(GetWorkItemGroupRowsQuery query, CancellationToken cancellationToken)
    {
        var organizationId = AggregateSupport.OrganizationOf(query.Request.OrganizationId);
        var parsed = AggregateParameters.Parse(query.Request, AggregateView.WorkItems, groupRead: true);

        var caller = new CallerAccess(organizationId, currentUser.SubjectId, access.Role);
        var scope = AggregateSupport.ScopeOf(AggregateView.WorkItems, null, parsed);

        var page = await views.ReadGroupPageAsync(caller, scope, parsed.Group, parsed.After, parsed.Limit, cancellationToken);

        if (!page.ProjectFound)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        var rows = await AggregateSupport.ItemsOfAsync(page.Rows, organizationId, backlogs, cancellationToken);

        return new WorkItemGroupRowsResponse(rows, page.Next is null ? null : GroupCursor.Encode(page.Next, parsed.Fingerprint));
    }
}
