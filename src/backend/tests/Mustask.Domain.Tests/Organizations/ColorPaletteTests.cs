using Mustask.Domain.Organizations;

namespace Mustask.Domain.Tests.Organizations;

// doc-016 BR-TD-20 · the choice rule is a pure function of the colours in use in the scope.
public sealed class ColorPaletteTests
{
    [Fact]
    public void Palette_HasTwelveDistinctKeysInTheFixedOrder()
    {
        var keys = ColorPalette.Entries.Select(entry => entry.Key).ToList();

        Assert.Equal(
            ["blue", "orange", "green", "purple", "red", "teal", "amber", "pink", "indigo", "lime", "magenta", "cyan"],
            keys);
        Assert.Equal(12, keys.Distinct().Count());
    }

    [Fact]
    public void Choose_WithNothingInUse_TakesTheFirstEntry()
    {
        Assert.Equal("blue", ColorPalette.Choose([]));
    }

    // US-1: the first department is blue, the next is the farthest from blue (orange), the third the farthest from both (magenta).
    [Fact]
    public void Choose_TakesTheEntryFarthestFromTheOnesInUse()
    {
        Assert.Equal("orange", ColorPalette.Choose(["blue"]));
        Assert.Equal("magenta", ColorPalette.Choose(["blue", "orange"]));
    }

    // US-1: teams of a blue department take the colours around it, the department's own colour counting as used.
    [Fact]
    public void Choose_ForTeamsOfABlueDepartment_GivesOrangeMagentaGreen()
    {
        var inUse = new List<string> { "blue" };

        foreach (var expected in new[] { "orange", "magenta", "green" })
        {
            var chosen = ColorPalette.Choose(inUse);
            Assert.Equal(expected, chosen);
            inUse.Add(chosen);
        }
    }

    [Fact]
    public void Choose_IsDeterministic_AndIgnoresTheOrderOfTheInput()
    {
        Assert.Equal(ColorPalette.Choose(["red", "teal", "lime"]), ColorPalette.Choose(["lime", "red", "teal"]));
    }

    [Fact]
    public void Choose_IgnoresAForeignKey()
    {
        Assert.Equal("blue", ColorPalette.Choose(["chartreuse"]));
        Assert.Equal("orange", ColorPalette.Choose(["blue", "chartreuse"]));
    }

    // The tie: with one colour in use that makes two unused entries equally far, the earlier entry in the palette wins.
    [Fact]
    public void Choose_OnATieOfDistance_TakesTheEarlierPaletteEntry()
    {
        // Whatever the input, the winner must not have an earlier unused entry at the same (largest) distance.
        foreach (var used in ColorPalette.Entries)
        {
            var chosen = ColorPalette.Choose([used.Key]);
            var hue = ColorPalette.Entries.Single(entry => entry.Key == chosen).Hue;
            var best = ColorPalette.Distance(used.Hue, hue);

            var earlier = ColorPalette.Entries
                .TakeWhile(entry => entry.Key != chosen)
                .Where(entry => entry.Key != used.Key);

            Assert.All(earlier, entry => Assert.True(ColorPalette.Distance(used.Hue, entry.Hue) < best));
        }
    }

    // All twelve in use: the one used by fewest, ties by palette order.
    [Fact]
    public void Choose_WhenEveryEntryIsInUse_TakesTheOneUsedByFewest()
    {
        var all = ColorPalette.Entries.Select(entry => entry.Key).ToList();

        Assert.Equal("blue", ColorPalette.Choose(all));

        var twiceExceptGreen = all.Concat(all.Where(key => key != "green")).ToList();

        Assert.Equal("green", ColorPalette.Choose(twiceExceptGreen));
    }

    [Fact]
    public void Choose_WhenEveryEntryIsInUse_AndTheFewestTie_TakesTheEarlierEntry()
    {
        var all = ColorPalette.Entries.Select(entry => entry.Key).ToList();
        var twiceExceptTealAndPink = all.Concat(all.Where(key => key is not ("teal" or "pink"))).ToList();

        Assert.Equal("teal", ColorPalette.Choose(twiceExceptTealAndPink));
    }

    [Theory]
    [InlineData(10, 350, 20)]
    [InlineData(350, 10, 20)]
    [InlineData(0, 180, 180)]
    [InlineData(206, 206, 0)]
    [InlineData(24, 359, 25)]
    public void Distance_IsTheShortestWayRoundTheCircle(int first, int second, int expected)
    {
        Assert.Equal(expected, ColorPalette.Distance(first, second));
    }

    [Fact]
    public void IsKey_AcceptsOnlyThePaletteKeys()
    {
        Assert.True(ColorPalette.IsKey("blue"));
        Assert.False(ColorPalette.IsKey("Blue"));
        Assert.False(ColorPalette.IsKey(null));
        Assert.False(ColorPalette.IsKey(""));
    }
}
