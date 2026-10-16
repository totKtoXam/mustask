using Mustask.Domain.Process;

namespace Mustask.Domain.Tests.Process;

/// <summary>
/// plan-backend.md § 2 "Closed sets are enums in Domain, stored and served as
/// strings": one name map per enum, round-trippable, and complete.
/// </summary>
public sealed class WireNamesTests
{
    // US-2 · "Categories are shown by their Russian labels": the five labels of doc-010 D-5, exhaustive over the enum.
    [Fact]
    public void StateCategoryLabels_EachCategory_HasItsDoc010Label()
    {
        var labels = Enum.GetValues<StateCategory>().ToDictionary(category => category, StateCategoryLabels.Of);

        Assert.Equal(
            new Dictionary<StateCategory, string>
            {
                [StateCategory.Proposed] = "Новые",
                [StateCategory.InProgress] = "В работе",
                [StateCategory.Resolved] = "Решённые",
                [StateCategory.Completed] = "Завершённые",
                [StateCategory.Removed] = "Удалённые",
            },
            labels);
    }

    [Fact]
    public void StateCategoryLabels_UnknownCategory_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StateCategoryLabels.Of((StateCategory)99));
    }

    // The category code on the wire is the member name (plan-backend.md § 1, "category Proposed/InProgress/Resolved/Completed/Removed").
    [Fact]
    public void StateCategory_Members_AreTheDoc007Codes()
    {
        Assert.Equal(
            ["Proposed", "InProgress", "Resolved", "Completed", "Removed"],
            Enum.GetNames<StateCategory>());
    }

    [Fact]
    public void AppearanceTokenNames_EachToken_RoundTripsAndIsLowercase()
    {
        foreach (var token in Enum.GetValues<AppearanceToken>())
        {
            var name = token.ToWireName();

            Assert.Equal(name.ToLowerInvariant(), name);
            Assert.Equal(token, AppearanceTokenNames.FromWireName(name));
        }

        Assert.Equal(
            ["epic", "feature", "story", "task", "bug", "testcase"],
            Enum.GetValues<AppearanceToken>().Select(token => token.ToWireName()));
    }

    [Fact]
    public void AppearanceTokenNames_UnknownName_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AppearanceTokenNames.FromWireName("Epic"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ((AppearanceToken)99).ToWireName());
    }

    [Fact]
    public void BacklogLevelKindNames_EachKind_RoundTripsAndIsLowercase()
    {
        foreach (var kind in Enum.GetValues<BacklogLevelKind>())
        {
            var name = kind.ToWireName();

            Assert.Equal(name.ToLowerInvariant(), name);
            Assert.Equal(kind, BacklogLevelKindNames.FromWireName(name));
        }

        Assert.Equal(
            ["portfolio", "requirement", "task"],
            Enum.GetValues<BacklogLevelKind>().Select(kind => kind.ToWireName()));
    }

    [Fact]
    public void BacklogLevelKindNames_UnknownName_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BacklogLevelKindNames.FromWireName("Portfolio"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ((BacklogLevelKind)99).ToWireName());
    }
}
