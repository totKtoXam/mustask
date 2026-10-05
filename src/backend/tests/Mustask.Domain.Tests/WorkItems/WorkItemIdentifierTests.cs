using Mustask.Domain.WorkItems;

namespace Mustask.Domain.Tests.WorkItems;

/// <summary>The identifier's exact form (BR-M3-5, spec § The identifier and its number).</summary>
public sealed class WorkItemIdentifierTests
{
    // US-1 · "Each type gets its own type key": MSK-EPIC-1 … MSK-TASK-5, OPS-BUG-1; the number in decimal, no leading zero.
    [Theory]
    [InlineData("MSK", "EPIC", 1, "MSK-EPIC-1")]
    [InlineData("MSK", "TASK", 5, "MSK-TASK-5")]
    [InlineData("OPS", "BUG", 1, "OPS-BUG-1")]
    [InlineData("A1B2C3D4E5", "US", 2147483647, "A1B2C3D4E5-US-2147483647")]
    public void Format_AndParse_RoundTrip(string projectKey, string typeKey, int number, string expected)
    {
        Assert.Equal(expected, WorkItemIdentifier.Format(projectKey, typeKey, number));

        Assert.True(WorkItemIdentifier.TryParse(expected, out var parsed));
        Assert.Equal((projectKey, typeKey, number), (parsed.ProjectKey, parsed.TypeKey, parsed.Number));
        Assert.Equal(expected, parsed.ToString());
    }

    // US-3 · "An identifier that resolves to nothing": small letters, a leading zero; and every other departure from the
    // exact form — a zero, a number past int, a missing part, spaces, a trailing line feed, a project key of one letter.
    [Theory]
    [InlineData("msk-bug-4")]
    [InlineData("MSK-bug-4")]
    [InlineData("MSK-BUG-04")]
    [InlineData("MSK-BUG-0")]
    [InlineData("MSK-BUG--4")]
    [InlineData("MSK-BUG-2147483648")]
    [InlineData("MSK-BUG-99999999999")]
    [InlineData("MSK-BUG")]
    [InlineData("MSK-4")]
    [InlineData("MSK-BUG-4\n")]
    [InlineData(" MSK-BUG-4")]
    [InlineData("M-BUG-4")]
    [InlineData("1SK-BUG-4")]
    [InlineData("МСК-BUG-4")]
    [InlineData("MSK-BUG-٤")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_Malformed_IsFalse(string? value)
    {
        Assert.False(WorkItemIdentifier.TryParse(value, out _));
    }
}
