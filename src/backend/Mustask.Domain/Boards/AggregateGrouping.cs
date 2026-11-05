namespace Mustask.Domain.Boards;

/// <summary>The three aggregate views of feat-048 (BR-AV-1): the organization's backlog, board and work-item list.</summary>
public enum AggregateView
{
    Backlogs,
    Boards,
    WorkItems,
}

/// <summary>
/// How an aggregate view is cut into groups (feat-048 BR-AV-1..2). There is no grouping by organization: every view is inside one
/// organization. <see cref="None"/> is one group, the whole stream.
/// </summary>
public enum AggregateGrouping
{
    None,
    Department,
    Project,
    Team,
}

/// <summary>The wire names of <see cref="AggregateGrouping"/> and the groupings a level offers (BR-AV-2).</summary>
public static class AggregateGroupings
{
    public static string ToWireName(this AggregateGrouping grouping) => grouping switch
    {
        AggregateGrouping.None => "none",
        AggregateGrouping.Department => "department",
        AggregateGrouping.Project => "project",
        AggregateGrouping.Team => "team",
        _ => throw new ArgumentOutOfRangeException(nameof(grouping), grouping, "Unknown grouping."),
    };

    /// <summary>The grouping of a wire name: exactly <c>none</c>, <c>department</c>, <c>project</c> or <c>team</c>, lower case.</summary>
    public static bool TryParse(string? value, out AggregateGrouping grouping)
    {
        AggregateGrouping? parsed = value switch
        {
            "none" => AggregateGrouping.None,
            "department" => AggregateGrouping.Department,
            "project" => AggregateGrouping.Project,
            "team" => AggregateGrouping.Team,
            _ => null,
        };

        grouping = parsed.GetValueOrDefault();

        return parsed is not null;
    }

    /// <summary>
    /// The groupings a level offers (BR-AV-2): only those that can give more than one group. A dimension a selector fixes is not
    /// offered: a project and a team both chosen leave only <c>none</c>; a project, <c>none</c> and <c>team</c>; a team,
    /// <c>none</c> and <c>project</c>; a department (no project, no team), <c>none</c>, <c>project</c> and <c>team</c>; nothing
    /// chosen, all four.
    /// </summary>
    public static bool Offers(bool projectChosen, bool teamChosen, bool departmentChosen, AggregateGrouping grouping) =>
        grouping == AggregateGrouping.None
        || (projectChosen, teamChosen, departmentChosen) switch
        {
            (true, true, _) => false,
            (true, false, _) => grouping == AggregateGrouping.Team,
            (false, true, _) => grouping == AggregateGrouping.Project,
            (false, false, true) => grouping is AggregateGrouping.Project or AggregateGrouping.Team,
            (false, false, false) => true,
        };
}
