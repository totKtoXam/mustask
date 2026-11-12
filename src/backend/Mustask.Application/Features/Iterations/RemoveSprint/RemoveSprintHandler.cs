using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Iterations;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Iterations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Iterations.RemoveSprint;

/// <summary>
/// Removes a Planned sprint that holds no item outside «Удалена» (US-2, BR-8). An item of a deactivated project counts as held.
/// The sprint's items are locked first (the hierarchy locks, then the items by id), so a save that sets the sprint meanwhile is
/// either seen here or waits for the removal and finds the sprint gone. The «Удалена» items lose their pointer; the number stays
/// issued.
/// </summary>
public sealed class RemoveSprintHandler(
    IOrganizationStructure structure,
    ITeamRepository teams,
    ISprintRepository sprints,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(RemoveSprintCommand command, CancellationToken cancellationToken)
    {
        var organizationId = SprintAddress.Organization(command.OrganizationId);
        var teamId = await SprintAddress.TeamAsync(structure, organizationId, command.TeamId, cancellationToken);
        var sprintId = SprintAddress.Sprint(command.SprintId);

        _ = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                await SprintAddress.LockTeamAsync(teams, organizationId, teamId, ct);

                var sprint = await sprints.LockForUpdateAsync(organizationId, teamId, sprintId, ct)
                    ?? throw SprintAddress.SprintNotFound();

                if (sprint.State != SprintState.Planned)
                {
                    throw new ConflictException(SprintErrorCodes.NotRemovable);
                }

                if (await sprints.LockItemsAsync(sprint.Id, ct) > 0)
                {
                    throw new ConflictException(SprintErrorCodes.NotRemovable);
                }

                await sprints.ClearItemsAsync(sprint.Id, ct);
                sprints.Remove(sprint);
                await sprints.SaveAsync(ct);

                return true;
            },
            cancellationToken);
    }
}
