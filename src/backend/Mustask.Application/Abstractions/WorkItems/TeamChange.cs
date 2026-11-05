namespace Mustask.Application.Abstractions.WorkItems;

/// <summary>
/// The owning team a create or a save sends (mod-017 api-contract § 0.2), in a wrapper because a flat <c>null</c> would read as
/// «not changed» (mod-003 § 2.4). The command carries <c>null</c> for «not sent». <paramref name="TeamId"/> is the untrusted
/// string as it arrived, or <c>null</c> for «Без команды»; a string that is not a canonical id names no team (404).
/// </summary>
public sealed record TeamChange(string? TeamId);
