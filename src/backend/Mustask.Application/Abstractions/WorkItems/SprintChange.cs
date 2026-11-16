namespace Mustask.Application.Abstractions.WorkItems;

/// <summary>
/// The sprint a save sends (mod-007 api-contract § 3), in a wrapper because a flat <c>null</c> would read as «not changed». The
/// command carries <c>null</c> for «not sent». <paramref name="SprintId"/> is the untrusted string as it arrived, or <c>null</c> for
/// «Без спринта»; a string that is not a canonical id names no sprint (404).
/// </summary>
public sealed record SprintChange(string? SprintId);
