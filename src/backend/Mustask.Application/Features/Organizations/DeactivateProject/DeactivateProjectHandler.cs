using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.DeactivateProject;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/deactivate</c>
/// (US-3 "A project is deactivated"). The project leaves the active list and
/// joins the deactivated one, everything under it answers
/// <c>project.not_found</c> until it is restored, and nothing is deleted
/// (BR-M1-20, BR-M1-24).
/// </summary>
/// <remarks>
/// <para>
/// <b>One row.</b> The deactivation writes the project's own status and reads
/// nothing beneath it: its teams, areas and the rest of its tree keep their
/// rows and their statuses, and become unreachable through the project's
/// status alone (ADR-0026 rules 2 and 3). A write beneath only reads the
/// project's status, so the deactivation needs no lock on anything but the
/// project's own row (ADR-0026 rule 8).
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 8.2): the project <c>FOR NO KEY UPDATE</c>,
/// which serializes it against a rename, another deactivation and a restore of
/// the same project, and lets the inserts of a team or an area through (their
/// foreign key check takes <c>FOR KEY SHARE</c>). The status is re-checked
/// after the lock: a deactivation that committed first leaves the project
/// deactivated, and the loser answers <c>project.not_found</c>, as the
/// contract has it for an already deactivated project (api-contract § 2.6).
/// </para>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3): the address is the only
/// step here. There is no rule about the project alone, no body, and no
/// reference in a body.
/// </para>
/// </remarks>
public sealed class DeactivateProjectHandler(IProjectRepository projects, IUnitOfWork unitOfWork)
{
    public async Task Handle(DeactivateProjectCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address, from the outermost level in. The scoped check has answered for the
                // organization already; the handler parses it again only to name its container in the next read.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                // Malformed, unknown, of another organization, deactivated already, or under a deactivated
                // organization: one answer, and no lock is taken for an object that is not reachable.
                if (!AddressedId.TryParse(command.ProjectId, out var projectId)
                    || !await projects.IsReachableAsync(organizationId, projectId, ct))
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                // 2-5. No rule about the project alone, no values and no references in a body, nothing to pre-check.
                // The lock comes before the load, and the status is re-checked on what the lock holder left: a
                // concurrent deactivation that committed first answers not found, like any deactivated project.
                var project = await projects.LockAsync(organizationId, projectId, ct);

                if (project is not { IsActive: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                project.Deactivate();

                // 6. Save: one row, the project's own.
                await projects.SaveAsync(ct);

                return true;
            },
            cancellationToken);
}
