namespace Mustask.Application.Features.Organizations.GetTeams;

/// <summary><c>Team</c> of api-contract § 3, the one shape every team response uses. The slice's own copy.</summary>
/// <param name="IsFirstTeam">The team that owns the root: it cannot be deleted, and its default area is the root for good.</param>
/// <param name="DefaultArea">Always present.</param>
/// <param name="IsMember">
/// The <b>caller</b> is in this team (mod-016 api-contract § 3.3): the panel opens the project on the first team of the
/// list's order with this flag set, else on the first team. Only the list carries it.
/// </param>
public sealed record TeamResponse(Guid Id, Guid ProjectId, string Name, bool IsFirstTeam, TeamDefaultAreaResponse DefaultArea, bool IsMember = false);

/// <param name="Path">The names below the root joined by « / »; the root's own name for the root (DC-9).</param>
public sealed record TeamDefaultAreaResponse(Guid Id, string Path);
