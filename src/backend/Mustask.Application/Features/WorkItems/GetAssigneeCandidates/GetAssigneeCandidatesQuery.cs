namespace Mustask.Application.Features.WorkItems.GetAssigneeCandidates;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/workitems/assignee-candidates?text=</c>
/// (US-4, api-contract § 2.5).
/// </summary>
/// <param name="Text">The typed text; <c>null</c> when the query string has none.</param>
public sealed record GetAssigneeCandidatesQuery(string? OrganizationId, string? Text);
