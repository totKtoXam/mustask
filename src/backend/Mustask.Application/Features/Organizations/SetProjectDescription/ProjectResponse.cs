namespace Mustask.Application.Features.Organizations.SetProjectDescription;

/// <summary><c>Project</c> of feat-028 api-contract § 1.4 (mod-002 api-contract § 2.3 plus the description). The slice's own copy of the shape.</summary>
/// <param name="Key">2 to 10 Latin capital letters and numerals; it never changes.</param>
/// <param name="Description">The description as typed, or <c>null</c> when there is none (feat-028 BR-OL-3).</param>
public sealed record ProjectResponse(Guid Id, Guid OrganizationId, string Name, string Key, string? Description);
