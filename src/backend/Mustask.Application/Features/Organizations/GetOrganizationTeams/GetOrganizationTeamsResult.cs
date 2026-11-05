namespace Mustask.Application.Features.Organizations.GetOrganizationTeams;

/// <summary>The list as the server orders it; the client renders it in the order received. An organization with no team gives <c>[]</c> (BR-OL-13).</summary>
public sealed record GetOrganizationTeamsResult(IReadOnlyList<TeamListItemResponse> Items);

/// <summary>
/// <c>TeamListItem</c> of feat-028 api-contract § 1.2: a team with its active projects, by name; feat-040 (api-contract § 4.1)
/// adds <paramref name="IsMember"/>, whether the caller is in the team, for the switcher's team level.
/// </summary>
public sealed record TeamListItemResponse(Guid Id, string Name, string? Description, IReadOnlyList<TeamProjectResponse> Projects, bool IsMember);

/// <summary>A project named in a team's row.</summary>
public sealed record TeamProjectResponse(Guid Id, string Name);
