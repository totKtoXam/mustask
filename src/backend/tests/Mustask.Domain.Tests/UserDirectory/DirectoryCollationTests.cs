using Mustask.Domain.UserDirectory;

namespace Mustask.Domain.Tests.UserDirectory;

/// <summary>mod-014 US-1, BR-UP-15: the order of display names, and that the stored key agrees with it.</summary>
public sealed class DirectoryCollationTests
{
    /// <summary>US-1 "The order puts Cyrillic before Latin and ignores case and ё" — the scenario's six names, in its order.</summary>
    private static readonly string[] ScenarioOrder =
    [
        "анна Абрамова",
        "Егор Лось",
        "Ёлкин Пётр",
        "Юлия Щукина",
        "Alex Brown",
        "Zoe Adams",
    ];

    [Fact]
    public void Compare_OrdersTheScenarioNames()
    {
        var shuffled = ScenarioOrder.Reverse().ToArray();

        var sorted = shuffled.Order(Comparer<string>.Create(DirectoryCollation.Compare)).ToArray();

        Assert.Equal(ScenarioOrder, sorted);
    }

    [Fact]
    public void SortKey_BytewiseOrderIsTheSameOrder()
    {
        var shuffled = ScenarioOrder.Reverse().ToArray();

        var sorted = shuffled
            .OrderBy(name => DirectoryCollation.SortKey(name), Comparer<byte[]>.Create(CompareBytes))
            .ToArray();

        Assert.Equal(ScenarioOrder, sorted);
    }

    [Theory]
    [InlineData("ё", "е")]
    [InlineData("Ёлкин", "елкин")]
    [InlineData("АННА", "анна")]
    public void Compare_IgnoresCaseAndYo(string left, string right)
    {
        Assert.Equal(0, DirectoryCollation.Compare(left, right));
        Assert.Equal(DirectoryCollation.SortKey(left), DirectoryCollation.SortKey(right));
    }

    // BR-UP-15: spaces, punctuation and symbols before digits, digits before Cyrillic, Cyrillic before Latin.
    [Theory]
    [InlineData("-a", "1")]
    [InlineData(" a", "1")]
    [InlineData("1", "а")]
    [InlineData("я", "a")]
    [InlineData("а", "б")]
    [InlineData("a", "b")]
    public void Compare_RanksClassesInTheContractsOrder(string first, string second)
    {
        Assert.True(DirectoryCollation.Compare(first, second) < 0, $"'{first}' should sort before '{second}'.");
        Assert.True(CompareBytes(DirectoryCollation.SortKey(first), DirectoryCollation.SortKey(second)) < 0);
    }

    [Fact]
    public void EnsureSupported_PassesOnThisRuntime()
    {
        DirectoryCollation.EnsureSupported();
    }

    [Fact]
    public void Version_NamesTheCultureAndOptions()
    {
        Assert.StartsWith("ru/", DirectoryCollation.Version, StringComparison.Ordinal);
        Assert.True(DirectoryCollation.Version.Length <= 64, "The version must fit its column.");
    }

    /// <summary>PostgreSQL's <c>bytea</c> order: unsigned bytes, then length.</summary>
    internal static int CompareBytes(byte[]? left, byte[]? right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return left.AsSpan().SequenceCompareTo(right);
    }
}
