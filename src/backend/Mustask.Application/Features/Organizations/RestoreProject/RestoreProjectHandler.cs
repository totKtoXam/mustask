using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.RestoreProject;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/restore</c>
/// (US-3 "A deactivated project is restored exactly as it was", api-contract
/// § 2.7). The one request that addresses a deactivated project directly.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3), one numbered step each
/// below: the address from the outside in, where the organization must be
/// active (ADR-0026 rule 5: a restore still needs every container above the
/// object reachable) and the project may be of either status; then nothing,
/// because the request has no body and no rule about the addressed object
/// alone; then the save.
/// </para>
/// <para>
/// <b>Exact by construction.</b> The restore writes the one row of the project
/// (ADR-0026 rule 2): its name, key, teams, default areas and area tree were
/// never touched by the deactivation, so none of them is read or written here.
/// Restoring an active project changes nothing and answers 200 with the same
/// shape (a no-op, not a refusal).
/// </para>
/// <para>
/// <b>No uniqueness pre-check.</b> A project's name and key stay reserved in
/// its organization whatever its status (BR-M1-1, ADR-0026 rule 6), so no
/// other project can have taken them while this one was deactivated, and the
/// restore can neither conflict nor race on a unique index.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 8.2): the project <c>FOR NO KEY UPDATE</c>,
/// taken before the status is read, so the status the handler sees is the one
/// the last writer left and a concurrent deactivation or restore of the same
/// project is serialized with this one. The organization is not locked: its
/// status is only read, and its deactivation writes one row and reads nothing
/// beneath, so the two are always serializable (ADR-0026 rule 8). The
/// organization-context filter has answered for the organization already; the
/// handler reads it again so that it stays right when called without the filter.
/// </para>
/// </remarks>
public sealed class RestoreProjectHandler(
    IOrganizationReadModel organizations,
    IProjectRepository projects,
    IUnitOfWork unitOfWork)
{
    public async Task<RestoreProjectResult> Handle(RestoreProjectCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address, from the outermost level in. The organization must be active, even though the project
                // that is addressed may be deactivated: a project of a deactivated organization answers the
                // organization's not-found code (ADR-0026 rule 5).
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId)
                    || await organizations.FindActiveAsync(organizationId, ct) is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                // The project is locked, whatever its status, so the status read next cannot change under this request.
                // A project of another organization is not found by this address (BR-M1-22).
                var project = AddressedId.TryParse(command.ProjectId, out var projectId)
                    ? await projects.LockAsync(organizationId, projectId, ct)
                    : null;

                if (project is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                // 2 to 5. Nothing: no body, no rule about the addressed object alone, no uniqueness to check.

                // 6. Save. An active project is already as it should be: nothing is written (US-3 restores a
                // deactivated one; api-contract § 2.7 answers 200 for an active one).
                if (!project.IsActive)
                {
                    project.Restore();
                    await projects.SaveAsync(ct);
                }

                return new RestoreProjectResult(new ProjectResponse(project.Id, project.OrganizationId, project.Name, project.Key));
            },
            cancellationToken);
}
