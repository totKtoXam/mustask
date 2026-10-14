using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Features.Organizations.GetOrganizations;

/// <summary>
/// <c>GET /api/organizations</c> (US-1 "every signed-in user sees every
/// organization" — narrowed by mod-016 to the user's own, US-8 "lists in order,
/// deactivated ... left out"): the active organizations the user is a member of,
/// in display order.
/// </summary>
/// <remarks>
/// <para>
/// A straight projection over <see cref="IOrganizationReadModel"/>, as
/// <c>GetLanguages</c> is over its read model. The list asks for the
/// <see cref="OrganizationStatus.Active"/> status by name (ADR-0026 rule 4: no
/// global query filter, every read says what it wants), so a deactivated
/// organization is not here; <c>GetDeactivatedOrganizations</c> is its own use
/// case. An organization is the top of the containment chain, so there is no
/// reachability to apply (ADR-0026 rule 3).
/// </para>
/// <para>
/// <b>Narrowed by membership</b> (mod-016 BR-M1-7, api-contract § 4.1 row 1): the
/// list holds the active organizations in which the caller has a membership, in
/// any role. The route names no organization, so the scoped check does not run
/// on it; the narrowing is part of the read, and the caller is the token's
/// subject. A user with no membership gets an empty list — not an error and not
/// a 404 — and an instance administrator or an organization creator without a
/// membership sees nothing either (api-contract § 0.1). The order is the read
/// model's (by name in Russian collation, then by id, api-contract § 0.6) and the
/// handler does not reorder it: the client renders the list as received.
/// </para>
/// <para>
/// No unit of work and no lock: a read, one query (plan-backend § 6.4).
/// </para>
/// </remarks>
public sealed class GetOrganizationsHandler(IOrganizationReadModel organizations, ICurrentUser currentUser)
{
    public async Task<GetOrganizationsResult> Handle(GetOrganizationsQuery query, CancellationToken cancellationToken)
    {
        var rows = await organizations.ListAsync(OrganizationStatus.Active, currentUser.SubjectId, onlyRoles: null, cancellationToken);

        var items = rows
            .Select(row => new OrganizationListItemResponse(row.Id, row.Name))
            .ToList();

        return new GetOrganizationsResult(items);
    }
}
