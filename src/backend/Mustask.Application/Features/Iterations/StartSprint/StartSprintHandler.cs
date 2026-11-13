using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Iterations;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Iterations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Iterations.StartSprint;

/// <summary>
/// Starts a Planned sprint that has both dates (US-2, BR-3, BR-4). The team has one Active sprint at most: the check is read under
/// the team's lock, and the unique index on Active sprints decides two starts that race.
/// </summary>
public sealed class StartSprintHandler(
    IOrganizationStructure structure,
    ITeamRepository teams,
    ISprintRepository sprints,
    IUnitOfWork unitOfWork)
{
    public async Task<SprintResponse> Handle(StartSprintCommand command, CancellationToken cancellationToken)
    {
        var organizationId = SprintAddress.Organization(command.OrganizationId);
        var teamId = await SprintAddress.TeamAsync(structure, organizationId, command.TeamId, cancellationToken);
        var sprintId = SprintAddress.Sprint(command.SprintId);

        return await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                await SprintAddress.LockTeamAsync(teams, organizationId, teamId, ct);

                var sprint = await sprints.LockForUpdateAsync(organizationId, teamId, sprintId, ct)
                    ?? throw SprintAddress.SprintNotFound();

                sprint.Start();

                if (await sprints.HasActiveAsync(teamId, sprint.Id, ct))
                {
                    throw new ConflictException(SprintErrorCodes.ActiveExists);
                }

                await sprints.SaveAsync(ct);

                return SprintResponse.From(sprint);
            },
            cancellationToken);
    }
}
