namespace Mustask.Application.Features.WorkItems.GetParentCandidates;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/parent-candidates?text=&amp;levelAbove=</c>
/// (feat-001 US-1, US-2, US-5, US-7; api-contract § 3.4). Every address value is
/// the untrusted string it arrived as.
/// </summary>
/// <param name="Text">The typed text; <c>null</c> when the query string has none.</param>
/// <param name="LevelAbove"><c>true</c>: only the level directly above the item's (the backlog's dialog, BR-H-14).</param>
public sealed record GetParentCandidatesQuery(string? OrganizationId, string? ProjectId, string? Identifier, string? Text, bool LevelAbove);
