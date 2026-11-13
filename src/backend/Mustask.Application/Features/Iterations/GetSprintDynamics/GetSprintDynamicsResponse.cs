namespace Mustask.Application.Features.Iterations.GetSprintDynamics;

/// <summary>The dynamics of a Closed sprint against the team's other Closed sprints in the range (api-contract § 2.9).</summary>
public sealed record GetSprintDynamicsResponse(
    string Level,
    DateOnly From,
    DateOnly To,
    int RangeSprints,
    int ShownSprints,
    bool Truncated,
    DynamicsRow Current,
    IReadOnlyList<DynamicsRow> Sprints,
    DynamicsFigures Summary);

public sealed record DynamicsRow(
    Guid Id,
    int Number,
    string Name,
    DateOnly EndDate,
    int Items,
    decimal StoryPoints,
    int ClosedItems,
    decimal? ClosedItemsPercent,
    decimal ClosedStoryPoints,
    decimal? ClosedStoryPointsPercent);
