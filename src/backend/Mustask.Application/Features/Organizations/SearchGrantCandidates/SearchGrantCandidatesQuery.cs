using Mustask.Domain.Identity;

namespace Mustask.Application.Features.Organizations.SearchGrantCandidates;

/// <summary><c>GET /api/organizations/{organizationId}/{areas|projects|teams}/{objectId}/grant-candidates?text=</c> (mod-019 api-contract § 2).</summary>
public sealed record SearchGrantCandidatesQuery(string? OrganizationId, ResourceScope Scope, string? ObjectId, string? Text);
