using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetDeactivatedProjects;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/deactivated</c> (US-3
/// "A deactivated project is restored exactly as it was": the organization's
/// deactivated projects, the list the restore is made from; BR-M1-24).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3): the address is the only
/// thing a read has to answer for. The organization-context filter has answered
/// for it already; the handler re-checks it, because the filter is the boundary
/// and not the only check (§ 7.3), and an organization that is malformed,
/// unknown or not active is <see cref="OrganizationErrorCodes.OrganizationNotFound"/>,
/// never an empty list and never "deactivated" (BR-M1-20, ADR-0026 rule 5).
/// </para>
/// <para>
/// <b>What it reads</b>: the projects of the status <see cref="ProjectStatus.Deactivated"/>
/// of that active organization, through the reachability specification the read
/// model applies (ADR-0026 rule 4). A project of another organization is never
/// listed (BR-M1-22). The read model orders them; the handler keeps its order
/// (api-contract § 0.6). It writes nothing and takes no lock: a read needs no
/// transaction.
/// </para>
/// </remarks>
public sealed class GetDeactivatedProjectsHandler(IOrganizationReadModel organizations, IProjectReadModel projects)
{
    public async Task<GetDeactivatedProjectsResult> Handle(GetDeactivatedProjectsQuery query, CancellationToken cancellationToken)
    {
        // The address: the organization, malformed, unknown or not active, answers its own not-found code.
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId)
            || await organizations.FindActiveAsync(organizationId, cancellationToken) is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        var rows = await projects.ListAsync(organizationId, ProjectStatus.Deactivated, cancellationToken);

        return new GetDeactivatedProjectsResult(
            rows.Select(row => new ProjectListItemResponse(row.Id, row.Name, row.Key)).ToList());
    }
}
