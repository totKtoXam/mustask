using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Features.Organizations.GetDeactivatedOrganizations;

/// <summary>
/// <c>GET /api/organizations/deactivated</c> (US-2 "A deactivated organization
/// is listed separately, with no purge"): the deactivated organizations in
/// display order, so the panel can offer «Восстановить» and nothing that
/// deletes (BR-M1-1, BR-M1-20).
/// </summary>
/// <remarks>
/// <para>
/// A straight projection over <see cref="IOrganizationReadModel"/>, with no
/// step of the order of answers (plan-backend § 5.3) to take: the route has no
/// address, no body and no organization-context filter, because it names no
/// organization. The one thing the handler says is which status it wants: the
/// intent of the read is named here, in one place, and the read model
/// narrows and orders in SQL (ADR-0026 rule 4, plan-backend § 6.4, § 6.5).
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
public sealed class GetDeactivatedOrganizationsHandler(IOrganizationReadModel organizations)
{
    public async Task<GetDeactivatedOrganizationsResult> Handle(GetDeactivatedOrganizationsQuery query, CancellationToken cancellationToken)
    {
        var rows = await organizations.ListAsync(OrganizationStatus.Deactivated, cancellationToken);

        return new GetDeactivatedOrganizationsResult(
            rows.Select(row => new OrganizationListItemResponse(row.Id, row.Name)).ToList());
    }
}
