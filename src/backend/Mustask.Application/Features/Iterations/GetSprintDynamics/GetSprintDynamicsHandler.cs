using Mustask.Application.Abstractions.Iterations;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Iterations;

namespace Mustask.Application.Features.Iterations.GetSprintDynamics;

/// <summary>
/// The dynamics page of a Closed sprint (US-9, BR-15): its figures at the level beside those of the team's other Closed sprints
/// whose end date lies in the range, at most 20, the latest first. A fixed number of statements: the sprint, the range's count and
/// sprints, and one grouped read of the snapshot for all of them.
/// </summary>
public sealed class GetSprintDynamicsHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    ISprintReadModel sprints)
{
    public async Task<GetSprintDynamicsResponse> Handle(GetSprintDynamicsQuery query, CancellationToken cancellationToken)
    {
        var scope = await OutcomeScope.ResolveAsync(
            structure, processes, sprints, query.OrganizationId, query.TeamId, query.SprintId, query.Level, query.From, query.To,
            cancellationToken);

        var ids = scope.RangeSprints.Sprints.Select(sprint => sprint.Id).Append(scope.Sprint.Id).ToList();
        var counts = await sprints.CountsBySprintAsync(ids, scope.LevelId, cancellationToken);

        SprintCounts CountsOf(Guid id) => counts.TryGetValue(id, out var found) ? found : default;

        DynamicsRow RowOf(Guid id, int number, string name, DateOnly endDate)
        {
            var row = SprintFiguresRow.Of(CountsOf(id));

            return new DynamicsRow(
                id, number, name, endDate, row.Items, row.StoryPoints, row.ClosedItems, row.ClosedItemsPercent,
                row.ClosedStoryPoints, row.ClosedStoryPointsPercent);
        }

        var rows = scope.RangeSprints.Sprints
            .Select(sprint => RowOf(sprint.Id, sprint.Number, sprint.Name, sprint.EndDate))
            .ToList();

        return new GetSprintDynamicsResponse(
            scope.LevelName,
            scope.Range.From,
            scope.Range.To,
            scope.RangeSprints.Total,
            rows.Count,
            scope.RangeSprints.Total > rows.Count,
            RowOf(scope.Sprint.Id, scope.Sprint.Number, scope.Sprint.Name, scope.Sprint.EndDate ?? scope.Range.To),
            rows,
            DynamicsFigures.Of(scope.RangeSprints.Sprints.Select(sprint => CountsOf(sprint.Id)).ToList()));
    }
}
