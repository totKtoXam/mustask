namespace Mustask.Application.Features.Organizations.SearchMemberCandidates;

/// <summary>The body of api-contract § 1.3: at most 20 users, and whether more matched. The shape of the assignee picker.</summary>
public sealed record MemberCandidatesResponse(IReadOnlyList<MemberCandidateResponse> Users, bool MoreMatched);

/// <param name="Email">Empty when the user has none.</param>
public sealed record MemberCandidateResponse(string Id, string DisplayName, string Email);
