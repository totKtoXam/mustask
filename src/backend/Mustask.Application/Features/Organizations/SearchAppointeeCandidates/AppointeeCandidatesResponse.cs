namespace Mustask.Application.Features.Organizations.SearchAppointeeCandidates;

/// <summary>The body of api-contract § 1.2: at most 20 users, and whether more matched. Its own copy of the member picker's shape.</summary>
public sealed record AppointeeCandidatesResponse(IReadOnlyList<AppointeeCandidateResponse> Users, bool MoreMatched);

/// <param name="Email">Empty when the user has none.</param>
public sealed record AppointeeCandidateResponse(string Id, string DisplayName, string Email);
