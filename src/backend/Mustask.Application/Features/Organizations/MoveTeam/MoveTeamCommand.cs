namespace Mustask.Application.Features.Organizations.MoveTeam;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/teams/{teamId}/department</c> (doc-016 BR-TD-4, team part). Ids are the untrusted
/// strings of the address and the body; a malformed one is the not-found code of its level, never a 400.
/// </summary>
/// <param name="DepartmentId">The destination, as sent: required.</param>
public sealed record MoveTeamCommand(string? OrganizationId, string? TeamId, string? DepartmentId);

/// <summary>The team with its place (the contract's <c>TeamPlace</c>).</summary>
public sealed record TeamPlaceResponse(Guid Id, string Name, string? Description, Guid DepartmentId, string DepartmentName, string Color);

/// <summary>The handler's outcome: the body of the answer.</summary>
public sealed record MoveTeamResult(TeamPlaceResponse Response);
