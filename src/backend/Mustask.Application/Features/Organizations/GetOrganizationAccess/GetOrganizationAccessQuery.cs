namespace Mustask.Application.Features.Organizations.GetOrganizationAccess;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/access</c> (api-contract § 1.1, BR-M1-31). It carries no input: the
/// organization is the one the scoped check resolved for the request.
/// </summary>
public sealed record GetOrganizationAccessQuery;
