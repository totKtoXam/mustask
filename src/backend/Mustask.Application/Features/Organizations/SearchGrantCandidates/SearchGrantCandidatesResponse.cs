using Mustask.Application.Abstractions.Organizations;

namespace Mustask.Application.Features.Organizations.SearchGrantCandidates;

/// <summary><c>GrantCandidates</c> of api-contract § 1: <c>UserRef</c>s by the people-order, at most 20, and whether more matched.</summary>
public sealed record SearchGrantCandidatesResponse(IReadOnlyList<UserRefView> Items, bool MoreMatched);
