using Mustask.Domain.Identity;

namespace Mustask.Application.Features.Organizations.RevokeGrant;

/// <summary><c>DELETE /api/organizations/{organizationId}/{areas|projects|teams}/{objectId}/grants/{userId}</c> (mod-019 api-contract § 2).</summary>
public sealed record RevokeGrantCommand(string? OrganizationId, ResourceScope Scope, string? ObjectId, string? UserId);
