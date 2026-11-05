using Mustask.Domain.Boards;

namespace Mustask.Domain.Tests.Boards;

/// <summary>The groupings a level offers and their wire names (feat-048 BR-AV-2, BR-AV-3).</summary>
public sealed class AggregateGroupingTests
{
    [Theory]
    [InlineData("none", AggregateGrouping.None)]
    [InlineData("department", AggregateGrouping.Department)]
    [InlineData("project", AggregateGrouping.Project)]
    [InlineData("team", AggregateGrouping.Team)]
    public void TryParse_AcceptsTheFourWireNames_AndRoundTrips(string name, AggregateGrouping expected)
    {
        Assert.True(AggregateGroupings.TryParse(name, out var grouping));
        Assert.Equal(expected, grouping);
        Assert.Equal(name, grouping.ToWireName());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Team")]
    [InlineData("organization")]
    [InlineData(null)]
    public void TryParse_RefusesAnythingElse(string? name) => Assert.False(AggregateGroupings.TryParse(name, out _));

    [Theory]
    [InlineData(false, false, false, AggregateGrouping.Department, true)]
    [InlineData(false, false, false, AggregateGrouping.Project, true)]
    [InlineData(false, false, false, AggregateGrouping.Team, true)]
    [InlineData(true, false, false, AggregateGrouping.Team, true)]
    [InlineData(true, false, false, AggregateGrouping.Project, false)]
    [InlineData(true, false, false, AggregateGrouping.Department, false)]
    [InlineData(false, true, false, AggregateGrouping.Project, true)]
    [InlineData(false, true, false, AggregateGrouping.Team, false)]
    [InlineData(false, false, true, AggregateGrouping.Project, true)]
    [InlineData(false, false, true, AggregateGrouping.Team, true)]
    [InlineData(false, false, true, AggregateGrouping.Department, false)]
    [InlineData(true, true, false, AggregateGrouping.Project, false)]
    [InlineData(true, true, false, AggregateGrouping.Team, false)]
    public void Offers_OnlyTheGroupingsASelectorLeavesOpen(bool project, bool team, bool department, AggregateGrouping grouping, bool offered) =>
        Assert.Equal(offered, AggregateGroupings.Offers(project, team, department, grouping));

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    public void Offers_AlwaysOffersNone(bool project, bool team, bool department) =>
        Assert.True(AggregateGroupings.Offers(project, team, department, AggregateGrouping.None));
}
