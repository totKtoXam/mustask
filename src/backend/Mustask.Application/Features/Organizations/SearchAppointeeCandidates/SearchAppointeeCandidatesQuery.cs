namespace Mustask.Application.Features.Organizations.SearchAppointeeCandidates;

/// <summary><c>GET /api/organizations/rescue/appointee-candidates?text=</c> (feat-033 api-contract § 1.2).</summary>
/// <param name="Text">The typed text; <c>null</c> when the query string has none.</param>
public sealed record SearchAppointeeCandidatesQuery(string? Text);
