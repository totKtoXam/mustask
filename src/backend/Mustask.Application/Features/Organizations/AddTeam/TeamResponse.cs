namespace Mustask.Application.Features.Organizations.AddTeam;

/// <summary><c>Team</c> of api-contract § 3, the one shape every team response uses. The slice's own copy.</summary>
/// <param name="IsFirstTeam">The team that owns the root: it cannot be deleted, and its default area is the root for good.</param>
/// <param name="DefaultArea">Always present.</param>
public sealed record TeamResponse(Guid Id, Guid ProjectId, string Name, bool IsFirstTeam, TeamDefaultAreaResponse DefaultArea);

/// <param name="Path">The names below the root joined by « / »; the root's own name for the root (DC-9).</param>
public sealed record TeamDefaultAreaResponse(Guid Id, string Path);
