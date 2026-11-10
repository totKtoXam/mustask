using Mustask.Domain.Identity;

namespace Mustask.Application.Features.Organizations.ChangeGrantRole;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/{areas|projects|teams}/{objectId}/grants/{userId}</c> (mod-019 api-contract § 2).
/// <paramref name="UserId"/> is the path's grantee.
/// </summary>
public sealed record ChangeGrantRoleCommand(string? OrganizationId, ResourceScope Scope, string? ObjectId, string? UserId, string? Role);
