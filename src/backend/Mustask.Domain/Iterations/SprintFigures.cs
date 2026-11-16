namespace Mustask.Domain.Iterations;

/// <summary>What one sprint (or one person in it) held and closed at one level, from the close snapshot (BR-14).</summary>
public readonly record struct SprintCounts(int Items, decimal StoryPoints, int ClosedItems, decimal ClosedStoryPoints)
{
    /// <summary>Closed items over held items, exact, or <c>null</c> when none were held (BR-15: «—»).</summary>
    public decimal? ClosedItemsPercent => Items == 0 ? null : ClosedItems * 100m / Items;

    /// <summary>Closed story points over held ones, exact, or <c>null</c> when none were held.</summary>
    public decimal? ClosedStoryPointsPercent => StoryPoints == 0 ? null : ClosedStoryPoints * 100m / StoryPoints;
}

/// <summary>Minimum, average and maximum of a series, already rounded for display; every member is <c>null</c> for an empty one.</summary>
public readonly record struct Figure(decimal? Min, decimal? Average, decimal? Max)
{
    public static Figure Empty => new(null, null, null);
}

/// <summary>
/// The arithmetic of BR-15 and BR-16, in one place. Percents and averages are computed on exact values and rounded once, at the
/// end, half up (away from zero): a percent to a whole percent, an average of counts to one decimal. Nothing here divides by
/// zero, and no figure is ever NaN or infinite.
/// </summary>
public static class SprintFigures
{
    /// <summary>A percent rounded for display: a whole percent, half up.</summary>
    public static decimal? RoundPercent(decimal? exact) => exact is { } value ? Math.Round(value, 0, MidpointRounding.AwayFromZero) : null;

    /// <summary>An average of counts rounded for display: one decimal, half up.</summary>
    public static decimal? RoundAverage(decimal? exact) => exact is { } value ? Math.Round(value, 1, MidpointRounding.AwayFromZero) : null;

    /// <summary>Min, average and max of counts: the average to one decimal, the extremes as they are.</summary>
    public static Figure OfCounts(IReadOnlyCollection<decimal> values) =>
        values.Count == 0
            ? Figure.Empty
            : new Figure(values.Min(), RoundAverage(values.Average()), values.Max());

    /// <summary>
    /// Min, average and max of percents, over the exact values and whole percents for display. A <c>null</c> percent (nothing
    /// held) is left out; with none left the figure is empty.
    /// </summary>
    public static Figure OfPercents(IEnumerable<decimal?> exactPercents)
    {
        var values = exactPercents.OfType<decimal>().ToList();

        return values.Count == 0
            ? Figure.Empty
            : new Figure(RoundPercent(values.Min()), RoundPercent(values.Average()), RoundPercent(values.Max()));
    }
}
