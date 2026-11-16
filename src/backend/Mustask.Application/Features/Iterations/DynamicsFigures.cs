using Mustask.Domain.Iterations;

namespace Mustask.Application.Features.Iterations;

/// <summary>
/// Min, average and max over a series of sprints of the four measures of the outcome pages (BR-15, BR-16): closed items and
/// closed story points as counts, and their percents of what was held. Display-ready; the exact values never leave the server.
/// </summary>
public sealed record DynamicsFigures(
    Figure ClosedItems, Figure ClosedItemsPercent, Figure ClosedStoryPoints, Figure ClosedStoryPointsPercent)
{
    /// <summary>
    /// A held-0 sprint is in the count figures as 0 and in no percent (the percent is <c>null</c>); with no sprint every figure is
    /// empty.
    /// </summary>
    public static DynamicsFigures Of(IReadOnlyCollection<SprintCounts> series) =>
        new(
            SprintFigures.OfCounts(series.Select(counts => (decimal)counts.ClosedItems).ToList()),
            SprintFigures.OfPercents(series.Select(counts => counts.ClosedItemsPercent)),
            SprintFigures.OfCounts(series.Select(counts => counts.ClosedStoryPoints).ToList()),
            SprintFigures.OfPercents(series.Select(counts => counts.ClosedStoryPointsPercent)));
}

/// <summary>One sprint's figures, display-ready: the percents are whole numbers, <c>null</c> when nothing was held.</summary>
public sealed record SprintFiguresRow(
    int Items,
    decimal StoryPoints,
    int ClosedItems,
    decimal? ClosedItemsPercent,
    decimal ClosedStoryPoints,
    decimal? ClosedStoryPointsPercent)
{
    public static SprintFiguresRow Of(SprintCounts counts) =>
        new(
            counts.Items,
            counts.StoryPoints,
            counts.ClosedItems,
            SprintFigures.RoundPercent(counts.ClosedItemsPercent),
            counts.ClosedStoryPoints,
            SprintFigures.RoundPercent(counts.ClosedStoryPointsPercent));
}
