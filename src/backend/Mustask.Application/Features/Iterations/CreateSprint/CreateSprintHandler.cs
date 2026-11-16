using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Iterations;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Iterations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Iterations.CreateSprint;

/// <summary>
/// Creates a Planned sprint of a team (US-1, BR-1, BR-7). The answers run in the contract's order: the address, the body (400),
/// then, under the team's lock, the number and the name (409). The number is taken under the team's counter row, after the check
/// of the body and before the name's: a refusal rolls the issue back with the transaction, so no number is lost to it.
/// </summary>
public sealed class CreateSprintHandler(
    IOrganizationStructure structure,
    ITeamRepository teams,
    ISprintRepository sprints,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<SprintResponse> Handle(CreateSprintCommand command, CancellationToken cancellationToken)
    {
        var organizationId = SprintAddress.Organization(command.OrganizationId);
        var teamId = await SprintAddress.TeamAsync(structure, organizationId, command.TeamId, cancellationToken);
        var (name, dates) = SprintAddress.Body(command.Name, command.StartDate, command.EndDate, nameOptional: true);

        return await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                await SprintAddress.LockTeamAsync(teams, organizationId, teamId, ct);

                var number = await sprints.TakeNextNumberAsync(teamId, ct);
                var sprintName = name ?? SprintName.Default(number);

                if (await sprints.IsNameTakenAsync(teamId, sprintName.Normalized, null, ct))
                {
                    throw new ConflictException(SprintErrorCodes.NameTaken);
                }

                var sprint = Sprint.Create(organizationId, teamId, number, sprintName, dates, clock.UtcNow, currentUser.SubjectId);

                sprints.Add(sprint);
                await sprints.SaveAsync(ct);

                return SprintResponse.From(sprint);
            },
            cancellationToken);
    }
}
