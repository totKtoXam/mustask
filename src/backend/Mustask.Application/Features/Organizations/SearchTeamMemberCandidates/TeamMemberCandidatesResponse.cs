namespace Mustask.Application.Features.Organizations.SearchTeamMemberCandidates;

/// <summary>The body of api-contract § 2.2 (the shape of § 1.3): at most 20 users, and whether more matched.</summary>
public sealed record TeamMemberCandidatesResponse(IReadOnlyList<TeamMemberCandidateResponse> Users, bool MoreMatched);

/// <param name="Email">Empty when the user has none.</param>
public sealed record TeamMemberCandidateResponse(string Id, string DisplayName, string Email);
