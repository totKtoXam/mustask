using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Features.Organizations.GetOrganizations;

/// <summary>
/// <c>GET /api/organizations</c> (US-1 "every signed-in user sees every
/// organization", US-8 "lists in order, deactivated ... left out"): the active
/// organizations in display order.
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
/// <b>No narrowing by user</b> (BR-M1-19, api-contract § 0.1): until mod-016 the
/// list holds every active organization for any signed-in user, whoever created
/// it and whether or not they hold the create right. The handler therefore
/// takes no current user. The order is the read model's (by name in Russian
/// collation, then by id, api-contract § 0.6) and the handler does not reorder
/// it: the client renders the list as received.
/// </para>
/// <para>
/// No unit of work and no lock: a read, one query (plan-backend § 6.4).
/// </para>
/// </remarks>
public sealed class GetOrganizationsHandler(IOrganizationReadModel organizations)
{
    public async Task<GetOrganizationsResult> Handle(GetOrganizationsQuery query, CancellationToken cancellationToken)
    {
        var rows = await organizations.ListAsync(OrganizationStatus.Active, cancellationToken);

        var items = rows
            .Select(row => new OrganizationListItemResponse(row.Id, row.Name))
            .ToList();

        return new GetOrganizationsResult(items);
    }
}
