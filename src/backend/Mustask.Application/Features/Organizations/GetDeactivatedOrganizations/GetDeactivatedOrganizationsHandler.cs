using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Features.Organizations.GetDeactivatedOrganizations;

/// <summary>
/// <c>GET /api/organizations/deactivated</c> (US-2 "A deactivated organization
/// is listed separately, with no purge"): the deactivated organizations in
/// display order, so the panel can offer «Восстановить» and nothing that
/// deletes (BR-M1-1, BR-M1-20) — narrowed by mod-016 to the ones the caller may
/// restore.
/// </summary>
/// <remarks>
/// <para>
/// A straight projection over <see cref="IOrganizationReadModel"/>, with no
/// step of the order of answers (plan-backend § 5.3) to take: the route has no
/// address and no body, because it names no organization. The one thing the
/// handler says is which status it wants and for which roles: the intent of the
/// read is named here, in one place, and the read model narrows and orders in
/// SQL (ADR-0026 rule 4, plan-backend § 6.4, § 6.5).
/// </para>
/// <para>
/// <b>Only where the caller may restore</b> (mod-016 BR-M1-35, D-13,
/// api-contract § 4.1 row 2): an organization is listed when the caller's role in
/// it holds <c>organizations.organization.deactivate</c> — the roles are derived
/// from the bundles (<see cref="OrganizationRoleBundles.RolesHolding"/>), not
/// hard-coded, so a bundle that changes changes the list. A member or a viewer of
/// a deactivated organization sees nothing of it, and the restore they cannot
/// do is a 404 for a non-member and a 403 for them.
/// </para>
/// <para>
/// An organization is in this list by its own status alone. A deactivated
/// project beneath an active organization does not put the organization here,
/// and the projects beneath a deactivated one are neither read nor changed
/// (ADR-0026 rule 2). The handler keeps the read model's order: re-sorting in
/// memory would put the client's collation in place of the database's
/// (api-contract § 0.6).
/// </para>
/// </remarks>
public sealed class GetDeactivatedOrganizationsHandler(IOrganizationReadModel organizations, ICurrentUser currentUser)
{
    public async Task<GetDeactivatedOrganizationsResult> Handle(GetDeactivatedOrganizationsQuery query, CancellationToken cancellationToken)
    {
        var restoringRoles = OrganizationRoleBundles.RolesHolding(PermissionCatalog.OrganizationsOrganizationDeactivate);

        var rows = await organizations.ListAsync(OrganizationStatus.Deactivated, currentUser.SubjectId, restoringRoles, cancellationToken);

        return new GetDeactivatedOrganizationsResult(
            rows.Select(row => new OrganizationListItemResponse(row.Id, row.Name)).ToList());
    }
}
