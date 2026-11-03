using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Boards.GetBacklogGroupRows;

/// <summary>
/// A page of one group of the aggregate backlog (feat-048 BR-AV-6, BR-AV-9, ADR-0036): by project the project's rank order, every
/// other grouping change time descending, number descending, project key; 50 at most after the cursor. A by-project group whose
/// project is not an active project of the organization is the project's not-found (OQ-4). Rows are flat with their live parent:
/// the project's nested tree (feat-027) is not paged by a cursor (plan-backend).
/// </summary>
public sealed class GetBacklogGroupRowsHandler(
    IAggregateViewReadModel views,
    IBacklogReadModel backlogs,
    IProcessDefinitionReadModel processes,
    IOrganizationAccess access,
    ICurrentUser currentUser)
{
    public async Task<BacklogGroupRowsResponse> Handle(GetBacklogGroupRowsQuery query, CancellationToken cancellationToken)
    {
        var organizationId = AggregateSupport.OrganizationOf(query.Request.OrganizationId);
        var parsed = AggregateParameters.Parse(query.Request, AggregateView.Backlogs, groupRead: true);
        var level = AggregateSupport.LevelOf(await processes.FindByOrganizationAsync(organizationId, cancellationToken), query.Request.Level);

        var caller = new CallerAccess(organizationId, currentUser.SubjectId, access.Role);
        var scope = AggregateSupport.ScopeOf(AggregateView.Backlogs, level, parsed);

        var page = await views.ReadGroupPageAsync(caller, scope, parsed.Group, parsed.After, parsed.Limit, cancellationToken);

        if (!page.ProjectFound)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        var rows = await AggregateSupport.ItemsOfAsync(page.Rows, organizationId, backlogs, cancellationToken);

        return new BacklogGroupRowsResponse(rows, page.Next is null ? null : GroupCursor.Encode(page.Next, parsed.Fingerprint));
    }
}
