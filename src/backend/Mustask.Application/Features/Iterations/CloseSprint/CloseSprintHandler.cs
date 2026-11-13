using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Iterations;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Iterations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Iterations.CloseSprint;

/// <summary>
/// Closes the Active sprint (US-5, BR-6, BR-14) in one transaction: the snapshot of everything the sprint holds outside «Удалена»,
/// then the move of the unfinished items to the destination, then the state. Three set-based statements whatever the number of
/// items (NFR: 5 000), taken after the locks in the order the team's unlink takes them: the team <c>FOR SHARE</c>, the sprint
/// <c>FOR UPDATE</c>, the destination <c>FOR SHARE</c> (a removal or a start of it waits for the close), the projects' hierarchy
/// locks, the items by id.
/// </summary>
public sealed class CloseSprintHandler(
    IOrganizationStructure structure,
    ITeamRepository teams,
    ISprintRepository sprints,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<SprintResponse> Handle(CloseSprintCommand command, CancellationToken cancellationToken)
    {
        var organizationId = SprintAddress.Organization(command.OrganizationId);
        var teamId = await SprintAddress.TeamAsync(structure, organizationId, command.TeamId, cancellationToken);
        var sprintId = SprintAddress.Sprint(command.SprintId);

        // A destination that is not an id names nothing, as one that never existed does.
        Guid? destinationId = null;

        if (command.DestinationSprintId is not null)
        {
            destinationId = AddressedId.TryParse(command.DestinationSprintId, out var parsed) ? parsed : throw SprintAddress.SprintNotFound();
        }

        return await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                await SprintAddress.LockTeamAsync(teams, organizationId, teamId, ct);

                // ADR-0028 rule 8: the hierarchy locks (step 5), then the sprint and its destination together (step 6), then the
                // items (step 7).
                await sprints.LockHierarchiesAsync(teamId, ct);

                var (sprint, facts) = await sprints.LockPairForUpdateAsync(organizationId, teamId, sprintId, destinationId, ct);

                if (sprint is null || (destinationId is not null && facts is null))
                {
                    throw SprintAddress.SprintNotFound();
                }

                var now = clock.UtcNow;

                sprint.Close(now, currentUser.SubjectId);

                if (facts is not null && (facts.TeamId != teamId || facts.State != SprintState.Planned))
                {
                    throw new ConflictException(SprintErrorCodes.CloseDestinationInvalid);
                }

                _ = await sprints.LockItemsAsync(sprint.Id, ct);

                await sprints.WriteSnapshotAsync(sprint.Id, ct);
                await sprints.MoveUnfinishedAsync(sprint.Id, destinationId, now, ct);
                await sprints.SaveAsync(ct);

                return SprintResponse.From(sprint);
            },
            cancellationToken);
    }
}
