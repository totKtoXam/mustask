namespace Mustask.Application.Features.Organizations.GetTeamProjects;

/// <summary>The team's active projects as the server orders them (by name, then id); <c>[]</c> when there are none.</summary>
public sealed record GetTeamProjectsResult(IReadOnlyList<TeamProjectResponse> Items);

/// <summary><c>TeamProject</c> of feat-028 api-contract § 1.2.</summary>
public sealed record TeamProjectResponse(Guid Id, string Name, string Key);
