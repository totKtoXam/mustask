using Mustask.Domain.Identity;

namespace Mustask.Application.Features.Organizations.GetGrants;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/{areas|projects|teams}/{objectId}/grants</c> (mod-019 api-contract § 2). The ids of the
/// address are the untrusted strings that arrived; the scope is the route's.
/// </summary>
public sealed record GetGrantsQuery(string? OrganizationId, ResourceScope Scope, string? ObjectId);
