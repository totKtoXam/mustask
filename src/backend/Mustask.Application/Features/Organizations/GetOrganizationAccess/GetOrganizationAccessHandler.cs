using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;

namespace Mustask.Application.Features.Organizations.GetOrganizationAccess;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/access</c> (api-contract § 1.1; US-4 "Mustask.Api serves the role and the
/// codes of one organization"): the caller's role in the organization of the address and exactly its bundle of
/// organization codes, sorted. The panel reads it on an organization switch, on window refocus, after the caller's own
/// role change and after a 403 or 404 (BR-M1-31).
/// </summary>
/// <remarks>
/// <b>No query of its own.</b> The scoped check already read the caller's membership from the database for this very
/// request (ADR-0027 rule 3), so a role change applies on the next call with no cache anywhere (BR-M1-6); the handler
/// reads it from <see cref="IOrganizationAccess"/>. A non-member never reaches it: the check answered 404 first. The
/// instance codes are not in the answer; they stay in the token.
/// </remarks>
public sealed class GetOrganizationAccessHandler(IOrganizationAccess access)
{
    public Task<OrganizationAccessResponse> Handle(GetOrganizationAccessQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var role = access.Role;
        var permissions = OrganizationRoleBundles.CodesOf(role).Order(StringComparer.Ordinal).ToList();

        return Task.FromResult(new OrganizationAccessResponse(OrganizationRoles.ToCode(role), permissions));
    }
}
