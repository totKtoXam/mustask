using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetProjects;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects</c> (US-3 "the
/// organization's project list", US-8 "the switcher lists the active ...
/// projects ... and not the deactivated project"): the active projects of one
/// active organization, in display order.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3): the route's only address
/// level is the organization, so the only refusal is
/// <see cref="OrganizationErrorCodes.OrganizationNotFound"/>. The
/// organization-context filter is the boundary and has answered already; the
/// handler checks the level once more, through the same reachability
/// specification (<see cref="IOrganizationReadModel.FindActiveAsync"/>), so a
/// call that does not pass the filter can never be told apart from a
/// deactivated organization by an empty list (api-contract § 2, ADR-0026
/// rule 5). The two reads are two statements, with no transaction and no lock:
/// an organization deactivated between them answers an empty list, because a
/// read takes no lock and the deactivation writes one row (ADR-0026 rule 8).
/// </para>
/// <para>
/// <b>Order.</b> The read model orders in SQL, by name in Russian collation
/// then by id (plan-backend § 6.5); the handler keeps the order it received,
/// because the client renders the list in that order (api-contract § 0.6).
/// Deactivated projects, and projects of other organizations, never reach
/// this handler: the read model applies the reachability and the status
/// (plan-backend § 6.2).
/// </para>
/// </remarks>
public sealed class GetProjectsHandler(IOrganizationReadModel organizations, IProjectReadModel projects)
{
    public async Task<GetProjectsResult> Handle(GetProjectsQuery query, CancellationToken cancellationToken)
    {
        // 1. The address: the organization, malformed, unknown or not active (BR-M1-20).
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId)
            || await organizations.FindActiveAsync(organizationId, cancellationToken) is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        // 2-6. Nothing else to refuse: a read has no body and no rule about the addressed object.
        var rows = await projects.ListAsync(organizationId, ProjectStatus.Active, cancellationToken);

        return new GetProjectsResult(rows
            .Select(row => new ProjectListItemResponse(row.Id, row.Name, row.Key))
            .ToList());
    }
}
