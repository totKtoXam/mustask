using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Boards;

namespace Mustask.Application.Features.WorkItems.GetOrganizationAssigneeOptions;

/// <summary>
/// The people assigned to the items the selectors reach (feat-048; the project's feat-003 BR-WS-6, BR-WS-7 over the organization):
/// members, former members and disabled accounts alike, and while a chosen state names the deleted one, those of its deleted
/// items. The assignee and the keyword do not narrow their own options.
/// </summary>
public sealed class GetOrganizationAssigneeOptionsHandler(IAggregateViewReadModel views, IOrganizationAccess access, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<AssigneeFilterOptionView>> Handle(GetOrganizationAssigneeOptionsQuery query, CancellationToken cancellationToken)
    {
        var organizationId = AggregateSupport.OrganizationOf(query.Request.OrganizationId);
        var request = query.Request with { Grouping = null, AssigneeIds = null, Search = null };
        var parsed = AggregateParameters.Parse(request, AggregateView.WorkItems, groupRead: false);

        var caller = new CallerAccess(organizationId, currentUser.SubjectId, access.Role);

        return await views.ReadAssigneeOptionsAsync(caller, AggregateSupport.ScopeOf(AggregateView.WorkItems, null, parsed), cancellationToken);
    }
}
