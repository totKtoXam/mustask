using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetOverview;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/overview</c> (feat-043 US-2, BR-NV-9, BR-NV-15): «Обзор» in a bounded read. The only
/// refusal is <see cref="OrganizationErrorCodes.OrganizationNotFound"/> (the scoped check has answered already). The read takes the
/// caller as a required argument, so that feat-041 adds the visible set inside it; seven statements whatever the volume.
/// </summary>
public sealed class GetOverviewHandler(INavigationReadModel navigation, IOrganizationAccess access, ICurrentUser currentUser)
{
    public async Task<OverviewResponse> Handle(GetOverviewQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        var caller = new CallerAccess(organizationId, currentUser.SubjectId, access.Role);
        var rows = await navigation.ReadOverviewAsync(caller, cancellationToken);

        return new OverviewResponse(
            rows.DepartmentCount,
            rows.ProjectCount,
            rows.TeamCount,
            rows.Departments.Select(row => new OverviewRefResponse(row.Id, row.Name)).ToList(),
            rows.Projects.Select(row => new OverviewProjectResponse(row.Id, row.Name, row.Key)).ToList(),
            rows.Teams.Select(row => new OverviewTeamResponse(row.Id, row.Name, row.IsMember)).ToList(),
            rows.MyTeams.Select(row => new OverviewTeamResponse(row.Id, row.Name, row.IsMember)).ToList());
    }
}
