using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetProject;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}</c> (US-3
/// "The project settings": the page shows its name and its key read-only). A
/// straight projection over <see cref="IProjectReadModel"/>, which applies the
/// reachability specification of ADR-0026 rule 3 (plan-backend § 6.2): the
/// project is active, belongs to that organization, and the organization is
/// active.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3): the address from the
/// outermost level in, and nothing else. The route has no body, no rule about
/// the addressed object alone, and no write, so there is nothing to validate,
/// no lock to take (§ 8.2 lists none for a read) and no uniqueness to check.
/// </para>
/// <para>
/// <b>One answer for every way of not being reachable</b> (BR-M1-20,
/// BR-M1-22, ADR-0026 rule 5): a malformed id, an id that names nothing, the
/// id of a project of another organization, a deactivated project, and a
/// project under a deactivated organization all answer
/// <see cref="OrganizationErrorCodes.ProjectNotFound"/>. A deactivated project
/// is never revealed as such through an addressed request.
/// </para>
/// </remarks>
public sealed class GetProjectHandler(IProjectReadModel projects)
{
    public async Task<GetProjectResult> Handle(GetProjectQuery query, CancellationToken cancellationToken)
    {
        // 1. The address, from the outermost level in. The scoped check has answered for the
        // organization already; the handler parses it again only to name the container of the project it reads.
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.ProjectId, out var projectId)
            || await projects.FindReachableAsync(organizationId, projectId, cancellationToken) is not { } project)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        return new GetProjectResult(new ProjectResponse(project.Id, project.OrganizationId, project.Name, project.Key));
    }
}
