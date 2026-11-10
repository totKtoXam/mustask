namespace Mustask.Application.Features.WorkItems.GetAssigneeCandidates;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/workitems/assignee-candidates?text=&amp;projectId=</c>
/// (US-4, api-contract § 2.5).
/// </summary>
/// <param name="Text">The typed text; <c>null</c> when the query string has none.</param>
/// <param name="ProjectId">The project the picker serves (mod-019 OQ-11): the code is judged on it; absent, on the organization role alone.</param>
public sealed record GetAssigneeCandidatesQuery(string? OrganizationId, string? Text, string? ProjectId = null);
