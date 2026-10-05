namespace Mustask.Application.Features.WorkItems.GetAssigneeCandidates;

/// <summary>The body of api-contract § 2.5: at most 20 users, and whether more matched.</summary>
public sealed record AssigneeCandidatesResponse(IReadOnlyList<AssigneeCandidateResponse> Users, bool MoreMatched);

/// <param name="Email">Empty when the user has none.</param>
public sealed record AssigneeCandidateResponse(string Id, string DisplayName, string Email);
