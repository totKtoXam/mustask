using Mustask.Application.Abstractions.Iterations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Iterations;

namespace Mustask.Application.Features.Iterations.GetSprint;

/// <summary>
/// The header of a Closed sprint (US-5). A sprint that is not Closed has no such page and is not found. The closer's name is the
/// directory's current one, or none for a person the directory does not know.
/// </summary>
public sealed class GetSprintHandler(IOrganizationStructure structure, ISprintReadModel sprints, IUserDirectory directory)
{
    public async Task<GetSprintResponse> Handle(GetSprintQuery query, CancellationToken cancellationToken)
    {
        var organizationId = SprintAddress.Organization(query.OrganizationId);
        var teamId = await SprintAddress.TeamAsync(structure, organizationId, query.TeamId, cancellationToken);
        var sprintId = SprintAddress.Sprint(query.SprintId);

        if (await sprints.FindAsync(teamId, sprintId, cancellationToken) is not { State: nameof(SprintState.Closed) } sprint)
        {
            throw SprintAddress.SprintNotFound();
        }

        string? closedByName = null;

        if (sprint.ClosedBy is { } closedBy
            && (await directory.LookupAsync([closedBy], cancellationToken)).Known.TryGetValue(closedBy, out var user))
        {
            closedByName = user.DisplayName;
        }

        var (items, storyPoints) = await sprints.DoneTotalsAsync(sprintId, cancellationToken);

        return new GetSprintResponse(
            new SprintResponse(
                sprint.Id, sprint.TeamId, sprint.Number, sprint.Name, sprint.State, sprint.StartDate, sprint.EndDate,
                sprint.CreatedAt, sprint.CreatedBy, sprint.ClosedAt, sprint.ClosedBy),
            closedByName,
            items,
            storyPoints);
    }
}
