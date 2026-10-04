using Mustask.Domain.UserDirectory;

namespace Mustask.Domain.Tests.UserDirectory;

/// <summary>mod-014 BR-UP-3: the display name.</summary>
public sealed class DisplayNameRuleTests
{
    [Theory]
    [InlineData("Анна", "Смирнова", "a.smirnova", "Анна Смирнова")]
    [InlineData("  Анна ", " Смирнова  ", "a.smirnova", "Анна Смирнова")]
    [InlineData("Анна", null, "a.smirnova", "Анна")]
    [InlineData(null, "Смирнова", "a.smirnova", "Смирнова")]
    [InlineData(" ", "", "a.smirnova", "a.smirnova")]
    public void Compose_JoinsTrimmedNamesWithOneSpace(string? first, string? last, string username, string expected)
    {
        Assert.Equal(expected, DisplayNameRule.Compose(first, last, username));
    }

    // US-1 "A user with no first or last name is shown by username".
    [Fact]
    public void Compose_NoNames_IsTheUsername()
    {
        Assert.Equal("qa.fixture", DisplayNameRule.Compose(null, null, "qa.fixture"));
    }
}
