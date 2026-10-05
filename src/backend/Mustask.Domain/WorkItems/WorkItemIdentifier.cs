using System.Globalization;
using System.Text.RegularExpressions;

namespace Mustask.Domain.WorkItems;

/// <summary>
/// A work item's identifier <c>&lt;project key&gt;-&lt;type key&gt;-&lt;number&gt;</c>,
/// e.g. <c>MSK-BUG-42</c> (BR-M3-5, spec § The identifier and its number). It is
/// derived, never stored: the project key, the type key and the number never
/// change, so neither does it. It is well-formed only in that exact form — a
/// project key as <see cref="Organizations.ProjectKey"/> judges it, a type key of Latin
/// capitals and digits starting with a letter, and a number of 1 or more
/// without leading zeros that fits <see cref="int"/>. <c>msk-bug-42</c>,
/// <c>MSK-BUG-042</c> and <c>MSK-BUG-0</c> are malformed.
/// </summary>
public readonly partial record struct WorkItemIdentifier
{
    // \z, not $: in .NET "$" also matches before a final line feed. Project keys and type keys hold no hyphen, so the
    // two hyphens split the identifier unambiguously.
    [GeneratedRegex(@"^(?<project>[A-Z][A-Z0-9]{1,9})-(?<type>[A-Z][A-Z0-9]{0,9})-(?<number>[1-9][0-9]{0,9})\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex Pattern { get; }

    private WorkItemIdentifier(string projectKey, string typeKey, int number)
    {
        ProjectKey = projectKey;
        TypeKey = typeKey;
        Number = number;
    }

    public string ProjectKey { get; }

    public string TypeKey { get; }

    public int Number { get; }

    /// <summary>The identifier of item <paramref name="number"/> of a project keyed <paramref name="projectKey"/>, of a type keyed <paramref name="typeKey"/>.</summary>
    public static string Format(string projectKey, string typeKey, int number)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectKey);
        ArgumentException.ThrowIfNullOrEmpty(typeKey);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);

        return string.Create(CultureInfo.InvariantCulture, $"{projectKey}-{typeKey}-{number}");
    }

    /// <summary>Parses an identifier as it arrived in an address. Anything not in the exact form is <c>false</c>.</summary>
    public static bool TryParse(string? value, out WorkItemIdentifier identifier)
    {
        identifier = default;

        if (value is null)
        {
            return false;
        }

        var match = Pattern.Match(value);

        if (!match.Success
            || !int.TryParse(match.Groups["number"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        identifier = new WorkItemIdentifier(match.Groups["project"].Value, match.Groups["type"].Value, number);
        return true;
    }

    public override string ToString() => Format(ProjectKey, TypeKey, Number);
}
