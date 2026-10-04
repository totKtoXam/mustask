using Mustask.Application.Abstractions.UserDirectory;

namespace Mustask.Application.Tests.Abstractions.UserDirectory;

/// <summary>mod-014 US-1 / BR-UP-15: the order offered to consumers for merging.</summary>
public sealed class DirectoryUserOrderTests
{
    private static DirectoryUser User(string id, string displayName) => new(id, displayName, string.Empty, true);

    // US-1 "The order puts Cyrillic before Latin and ignores case and ё".
    [Fact]
    public void Comparer_OrdersTheScenarioNames()
    {
        string[] expected = ["анна Абрамова", "Егор Лось", "Ёлкин Пётр", "Юлия Щукина", "Alex Brown", "Zoe Adams"];

        var sorted = expected.Reverse()
            .Select((name, index) => User(index.ToString(System.Globalization.CultureInfo.InvariantCulture), name))
            .Order(DirectoryUserOrder.Comparer)
            .Select(user => user.DisplayName)
            .ToArray();

        Assert.Equal(expected, sorted);
    }

    // US-1 "Equal display names are ordered by id, the same way every time".
    [Fact]
    public void Comparer_EqualNames_ByIdInCodePointOrder()
    {
        var users = new[] { User("b", "Анна Смирнова"), User("A", "анна смирнова"), User("a", "Анна Смирнова") };

        var sorted = users.Order(DirectoryUserOrder.Comparer).Select(user => user.Id).ToArray();

        Assert.Equal(["A", "a", "b"], sorted);
    }

    [Fact]
    public void Comparer_Ids_AreComparedByCodePointNotUtf16Unit()
    {
        // U+FF5E (one UTF-16 unit, 0xFF5E) is below U+1F600 (a surrogate pair,
        // 0xD83D…) in code points and in UTF-8 bytes, but above it in UTF-16 units.
        var low = User("～", "Same");
        var high = User("\U0001F600", "Same");

        Assert.True(DirectoryUserOrder.Comparer.Compare(low, high) < 0);
    }

    [Fact]
    public void Comparer_IsTotal()
    {
        var a = User("a", "Анна");

        Assert.Equal(0, DirectoryUserOrder.Comparer.Compare(a, a with { }));
    }
}
