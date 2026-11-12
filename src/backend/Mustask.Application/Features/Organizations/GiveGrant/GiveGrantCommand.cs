using Mustask.Domain.Identity;

namespace Mustask.Application.Features.Organizations.GiveGrant;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/{areas|projects|teams}/{objectId}/grants</c> (mod-019 api-contract § 2). The ids of
/// the address and the fields of the body are the untrusted strings that arrived.
/// </summary>
public sealed record GiveGrantCommand(string? OrganizationId, ResourceScope Scope, string? ObjectId, string? UserId, string? Role);
