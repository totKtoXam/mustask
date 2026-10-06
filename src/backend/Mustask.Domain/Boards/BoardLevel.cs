using Mustask.Domain.Process;

namespace Mustask.Domain.Boards;

/// <summary>
/// The three levels a team has a backlog and a board for in this cut (spec
/// § Model, BR-M4-1; the «Задачи» level has neither, OQ-3). The address segment
/// is the wire name (<see cref="BoardLevelNames"/>).
/// </summary>
public enum BoardLevel
{
    Epics,
    Features,
    Stories,
}

/// <summary>A backlog level of the organization's process as the mapping needs it: its id, rank and kind (mod-001 BR-M2-4).</summary>
public sealed record ProcessLevel(Guid Id, int Rank, BacklogLevelKind Kind);

/// <summary>The one name map of <see cref="BoardLevel"/>, and its mapping onto the levels of a process.</summary>
public static class BoardLevelNames
{
    public static string ToWireName(this BoardLevel level) => level switch
    {
        BoardLevel.Epics => "epics",
        BoardLevel.Features => "features",
        BoardLevel.Stories => "stories",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown board level."),
    };

    /// <summary>The level of an address segment: exactly one of the three wire names, lower case; anything else is none.</summary>
    public static bool TryParse(string? segment, out BoardLevel level)
    {
        BoardLevel? parsed = segment switch
        {
            "epics" => BoardLevel.Epics,
            "features" => BoardLevel.Features,
            "stories" => BoardLevel.Stories,
            _ => null,
        };

        level = parsed.GetValueOrDefault();

        return parsed is not null;
    }

    /// <summary>
    /// The level of <paramref name="levels"/> that <paramref name="level"/> names
    /// (api-contract § 0.2), by kind and rank and never by name: <c>stories</c>
    /// is the requirement level (the highest-ranked one, should a process have
    /// several), <c>features</c> the lowest-ranked portfolio level, <c>epics</c>
    /// the portfolio level directly above it. <c>null</c> when the process has no
    /// such level.
    /// </summary>
    public static ProcessLevel? Resolve(this BoardLevel level, IEnumerable<ProcessLevel> levels)
    {
        var list = levels.ToList();
        var portfolio = list.Where(candidate => candidate.Kind == BacklogLevelKind.Portfolio).OrderBy(candidate => candidate.Rank).ToList();

        return level switch
        {
            BoardLevel.Stories => list.Where(candidate => candidate.Kind == BacklogLevelKind.Requirement).MaxBy(candidate => candidate.Rank),
            BoardLevel.Features => portfolio.ElementAtOrDefault(0),
            BoardLevel.Epics => portfolio.ElementAtOrDefault(1),
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown board level."),
        };
    }

    /// <summary>
    /// The level whose backlog nests beneath <paramref name="level"/>'s rows
    /// (feat-001 BR-H-10): <c>features</c> beneath <c>epics</c>, <c>stories</c>
    /// beneath <c>features</c>; nothing beneath <c>stories</c>, the «Задачи»
    /// level having no backlog in this cut (OQ-5).
    /// </summary>
    public static BoardLevel? Below(this BoardLevel level) => level switch
    {
        BoardLevel.Epics => BoardLevel.Features,
        BoardLevel.Features => BoardLevel.Stories,
        BoardLevel.Stories => null,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown board level."),
    };
}
