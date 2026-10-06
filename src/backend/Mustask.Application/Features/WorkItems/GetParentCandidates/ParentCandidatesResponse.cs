using Mustask.Application.Abstractions.WorkItems;

namespace Mustask.Application.Features.WorkItems.GetParentCandidates;

/// <summary>The body of api-contract § 3.4: at most 20 candidates, most recently changed first, and whether more matched.</summary>
public sealed record ParentCandidatesResponse(IReadOnlyList<WorkItemRefView> Items, bool MoreMatched);
