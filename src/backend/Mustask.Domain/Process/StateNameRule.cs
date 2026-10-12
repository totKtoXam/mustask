namespace Mustask.Domain.Process;

/// <summary>
/// One name, one meaning (bug-004 D-3): a state name has one category on every
/// type of a process. «Разрешена» is the same thing wherever it stands, so a
/// board that heads its columns by state names (mod-006 BR-M4-2) never puts
/// two meanings under one header, and a count or a filter by category agrees
/// with the name a user reads.
/// </summary>
/// <remarks>
/// The rule is stated once, here, and every place in the domain that can bring a
/// state into a process asks it: <see cref="ProcessBlueprint.EnsureValid"/> (the
/// seed's input) and <see cref="ProcessDefinition.EnsureStateNamesHaveOneCategory"/>
/// (the built aggregate, which any later write path that creates or renames a
/// state must call before it saves). A write that goes round the aggregate (raw
/// SQL, <c>ExecuteUpdate</c>) does not ask it; the architecture rule
/// <c>ProcessStateWritesRules</c> keeps such writes out of the code outside the
/// process sub-slice. Names are compared as stored, ordinally; a write path that
/// normalizes names compares the normalized form.
/// </remarks>
public static class StateNameRule
{
    /// <summary>The names that stand in more than one category among <paramref name="states"/>, in the order they first appear.</summary>
    public static IReadOnlyList<string> Breaches(IEnumerable<(string Name, StateCategory Category)> states)
    {
        ArgumentNullException.ThrowIfNull(states);

        return states
            .GroupBy(state => state.Name, StringComparer.Ordinal)
            .Where(sameName => sameName.Select(state => state.Category).Distinct().Count() > 1)
            .Select(sameName => sameName.Key)
            .ToList();
    }

    /// <exception cref="InvalidOperationException">A name stands in more than one category.</exception>
    public static void Require(IEnumerable<(string Name, StateCategory Category)> states)
    {
        var breaches = Breaches(states);

        if (breaches.Count > 0)
        {
            throw new InvalidOperationException(
                $"A state name must have one category on every type of a process; in more than one: {string.Join(", ", breaches.Select(name => $"«{name}»"))}.");
        }
    }
}
