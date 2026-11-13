namespace Mustask.Domain.Iterations;

/// <summary>
/// The date range of an outcome page (BR-15): both ends inclusive, both or neither, the end not before the start; anything
/// unusable falls back to the default, the 90 days before the sprint's start through the day before it.
/// </summary>
public readonly record struct SprintRange(DateOnly From, DateOnly To)
{
    public static SprintRange Default(DateOnly sprintStart) =>
        new(sprintStart.AddDays(-SprintLimits.DefaultRangeDays), sprintStart.AddDays(-1));

    /// <summary>The range sent, or the default when a date is missing, malformed or the pair reversed.</summary>
    public static SprintRange Resolve(string? from, string? to, DateOnly sprintStart) =>
        DateOnly.TryParseExact(from, "yyyy-MM-dd", out var f)
        && DateOnly.TryParseExact(to, "yyyy-MM-dd", out var t)
        && t >= f
            ? new SprintRange(f, t)
            : Default(sprintStart);
}
