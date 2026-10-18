namespace Mustask.Application.Features.Organizations.GetOrganizationTeams;

/// <summary>The list as the server orders it; the client renders it in the order received. An organization with no team gives <c>[]</c> (BR-OL-13).</summary>
public sealed record GetOrganizationTeamsResult(IReadOnlyList<TeamListItemResponse> Items);

/// <summary><c>TeamListItem</c> of feat-028 api-contract § 1.2: a team with its active projects, by name.</summary>
public sealed record TeamListItemResponse(Guid Id, string Name, string? Description, IReadOnlyList<TeamProjectResponse> Projects);

/// <summary>A project named in a team's row.</summary>
public sealed record TeamProjectResponse(Guid Id, string Name);
