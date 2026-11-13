using Mustask.Domain.Iterations;

namespace Mustask.Domain.Tests.Iterations;

/// <summary>The arithmetic of the outcome pages (mod-007 BR-15, BR-16): exact values, rounded once at the end, half up.</summary>
public sealed class SprintFiguresTests
{
    // BR-15 · a percent is whole, half up (away from zero), and none when nothing was held.
    [Theory]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]
    [InlineData(1, 8, 13)]
    [InlineData(5, 8, 63)]
    [InlineData(1, 2, 50)]
    [InlineData(10, 10, 100)]
    [InlineData(0, 7, 0)]
    public void Percent_IsWholeAndHalfUp(int closed, int held, int expected)
    {
        var counts = new SprintCounts(held, held, closed, closed);

        Assert.Equal(expected, SprintFigures.RoundPercent(counts.ClosedItemsPercent));
        Assert.Equal(expected, SprintFigures.RoundPercent(counts.ClosedStoryPointsPercent));
    }

    [Fact]
    public void Percent_OfNothingHeld_IsNone_NeverNaN()
    {
        var counts = new SprintCounts(0, 0m, 0, 0m);

        Assert.Null(counts.ClosedItemsPercent);
        Assert.Null(counts.ClosedStoryPointsPercent);
        Assert.Null(SprintFigures.RoundPercent(null));
    }

    // BR-16 · the average of counts has one decimal, half up, over the exact values.
    [Fact]
    public void Counts_AverageOfOneDecimal_ExtremesAsTheyAre()
    {
        var figure = SprintFigures.OfCounts([1m, 2m, 2m]);

        Assert.Equal(1m, figure.Min);
        Assert.Equal(1.7m, figure.Average);
        Assert.Equal(2m, figure.Max);
    }

    [Fact]
    public void Counts_AHalfRoundsUp()
    {
        Assert.Equal(0.3m, SprintFigures.OfCounts([0.25m]).Average);
        Assert.Equal(1.5m, SprintFigures.OfCounts([1m, 2m]).Average);
    }

    [Fact]
    public void Counts_OfNoSprint_AreEmpty()
    {
        Assert.Equal(Figure.Empty, SprintFigures.OfCounts([]));
        Assert.Equal(Figure.Empty, SprintFigures.OfPercents([]));
        Assert.Equal(Figure.Empty, SprintFigures.OfPercents([null, null]));
    }

    // BR-16 · percents are averaged exact and rounded once: 1/8, 1/8, 1/8 and 5/8 average to exactly 25, where the rounded 13, 13,
    // 13 and 63 would give 25.5 and round to 26.
    [Fact]
    public void Percents_AreAveragedExactAndRoundedOnce()
    {
        var series = new[] { 1m / 8, 1m / 8, 1m / 8, 5m / 8 }.Select(share => (decimal?)(share * 100m));

        var figure = SprintFigures.OfPercents(series);

        Assert.Equal(13m, figure.Min);
        Assert.Equal(25m, figure.Average);
        Assert.Equal(63m, figure.Max);
    }

    [Fact]
    public void Percents_ASprintHoldingNothing_IsLeftOut()
    {
        var figure = SprintFigures.OfPercents([50m, null, 100m]);

        Assert.Equal(50m, figure.Min);
        Assert.Equal(75m, figure.Average);
        Assert.Equal(100m, figure.Max);
    }

    // BR-15 · the range: both ends or the default (the 90 days before the sprint's start through the day before it).
    [Fact]
    public void Range_Default_IsNinetyDaysBeforeTheStart()
    {
        var start = new DateOnly(2026, 11, 2);

        var range = SprintRange.Default(start);

        Assert.Equal(new DateOnly(2026, 8, 4), range.From);
        Assert.Equal(new DateOnly(2026, 11, 1), range.To);
    }

    [Fact]
    public void Range_Resolve_TakesAValidPair()
    {
        var range = SprintRange.Resolve("2026-09-01", "2026-09-30", new DateOnly(2026, 11, 2));

        Assert.Equal(new SprintRange(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)), range);
        Assert.Equal(new SprintRange(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1)), SprintRange.Resolve("2026-09-01", "2026-09-01", default));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("2026-09-01", null)]
    [InlineData(null, "2026-09-30")]
    [InlineData("2026-09-30", "2026-09-01")]
    [InlineData("01.09.2026", "30.09.2026")]
    [InlineData("2026-13-01", "2026-13-02")]
    [InlineData("abc", "def")]
    public void Range_Resolve_AnythingUnusable_IsTheDefault(string? from, string? to)
    {
        var start = new DateOnly(2026, 11, 2);

        Assert.Equal(SprintRange.Default(start), SprintRange.Resolve(from, to, start));
    }
}
