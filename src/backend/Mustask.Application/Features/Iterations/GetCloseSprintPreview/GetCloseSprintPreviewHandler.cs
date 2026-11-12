using Mustask.Application.Abstractions.Iterations;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Iterations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Iterations.GetCloseSprintPreview;

/// <summary>
/// What the close would do (US-5, BR-6): the items that would move and those that stay, and the team's Planned sprints to move
/// to. Of an Active sprint only; the numbers are a preview, the close counts again under its locks.
/// </summary>
public sealed class GetCloseSprintPreviewHandler(IOrganizationStructure structure, ISprintReadModel sprints)
{
    public async Task<ClosePreview> Handle(GetCloseSprintPreviewQuery query, CancellationToken cancellationToken)
    {
        var organizationId = SprintAddress.Organization(query.OrganizationId);
        var teamId = await SprintAddress.TeamAsync(structure, organizationId, query.TeamId, cancellationToken);
        var sprintId = SprintAddress.Sprint(query.SprintId);

        var sprint = await sprints.FindAsync(teamId, sprintId, cancellationToken) ?? throw SprintAddress.SprintNotFound();

        if (sprint.State != nameof(SprintState.Active))
        {
            throw new ConflictException(SprintErrorCodes.NotActive);
        }

        return await sprints.ClosePreviewAsync(teamId, sprintId, cancellationToken);
    }
}
