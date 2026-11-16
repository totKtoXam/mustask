using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Iterations;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Iterations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Iterations.UpdateSprint;

/// <summary>
/// Renames and dates a sprint (US-1, BR-4, BR-7). Order: the address, the sprint (under its lock), the body (400), the rules of the
/// state (409), the name (409). A Closed sprint takes only the name.
/// </summary>
public sealed class UpdateSprintHandler(
    IOrganizationStructure structure,
    ITeamRepository teams,
    ISprintRepository sprints,
    IUnitOfWork unitOfWork)
{
    public async Task<SprintResponse> Handle(UpdateSprintCommand command, CancellationToken cancellationToken)
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

                var (name, dates) = SprintAddress.Body(command.Name, command.StartDate, command.EndDate, nameOptional: false);

                sprint.Update(name!.Value, dates);

                if (await sprints.IsNameTakenAsync(teamId, name.Value.Normalized, sprint.Id, ct))
                {
                    throw new ConflictException(SprintErrorCodes.NameTaken);
                }

                await sprints.SaveAsync(ct);

                return SprintResponse.From(sprint);
            },
            cancellationToken);
    }
}
