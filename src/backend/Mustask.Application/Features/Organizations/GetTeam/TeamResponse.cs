namespace Mustask.Application.Features.Organizations.GetTeam;

/// <summary><c>Team</c> of feat-028 api-contract § 1.1, the one shape every single-team response uses. The slice's own copy.</summary>
/// <param name="Description">The description as typed, or <c>null</c> when there is none (BR-OL-3).</param>
public sealed record TeamResponse(Guid Id, string Name, string? Description);
