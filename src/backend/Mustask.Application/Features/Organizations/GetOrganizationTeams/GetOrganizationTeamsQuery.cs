namespace Mustask.Application.Features.Organizations.GetOrganizationTeams;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams</c> (feat-028 api-contract § 2.1: the organization's active teams, by name).
/// The id is the untrusted string of the address: a malformed one gets the organization's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
public sealed record GetOrganizationTeamsQuery(string? OrganizationId);
