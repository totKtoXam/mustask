namespace Mustask.Application.Features.Iterations.GetSprintParticipants;

/// <summary>
/// The results of a Closed sprint by participant (US-10). <paramref name="Total"/> is the «Итого» row, the same on every page;
/// <paramref name="TotalRows"/> counts the participants, «Без исполнителя» included.
/// </summary>
public sealed record GetSprintParticipantsResponse(
    string Level,
    DateOnly From,
    DateOnly To,
    int RangeSprints,
    IReadOnlyList<ParticipantResponse> Rows,
    ParticipantResponse Total,
    int TotalRows);

/// <summary>
/// One participant: <c>UserId</c> and <c>DisplayName</c> are <c>null</c> on «Без исполнителя» and on the total; a person the
/// directory does not know has an id and no name; <c>IsMember</c> is whether the person is a member of the team now. The figures
/// are of the sprint itself, <c>ComparedSprints</c> of <c>RangeSprints</c> is «k из N»: the range sprints in which the person held
/// items at the level, over which <c>Dynamics</c> runs.
/// </summary>
public sealed record ParticipantResponse(
    string? UserId,
    string? DisplayName,
    bool IsMember,
    int Items,
    decimal StoryPoints,
    decimal ClosedStoryPoints,
    decimal? ClosedStoryPointsPercent,
    int ClosedItems,
    decimal? ClosedItemsPercent,
    int ComparedSprints,
    DynamicsFigures Dynamics);
