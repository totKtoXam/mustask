using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.RenameProject;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/name</c>
/// (US-3 "Renaming a project renames its root area and keeps its key", "The
/// name is unique in the organization"). Written after the reference use case
/// <c>CreateArea</c> (plan-backend § 14.2).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3, api-contract § 0.4): the
/// address from the outside in (the organization, then the project, which must
/// be reachable); rules about the addressed object alone (none: a project has
/// no fixed state to rename); the name, with its one reason; references in the
/// body (none); the uniqueness pre-check, whose code tells an active holder from
/// a deactivated one; the save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 8.2): the project <c>FOR NO KEY UPDATE</c>, then
/// its root area <c>FOR UPDATE</c>, because the root takes the new name with the
/// project (BR-M1-11). <c>FOR NO KEY UPDATE</c> and not <c>FOR UPDATE</c> on the
/// project, so that a team or an area inserted beneath it, which takes
/// <c>FOR KEY SHARE</c> on the project row through its foreign key, is not
/// deadlocked against a rename that waits for the root (§ 8.2). The project's
/// status is read again after the lock: a deactivation that committed meanwhile
/// holds the same lock, so this rename then answers the not-found code
/// (ADR-0026 rule 8).
/// </para>
/// <para>
/// <b>Races</b> (§ 8.1): two renames, or a rename and a creation, to one name
/// are closed by the unique index on the normalized name. The loser's save is
/// answered by the repository with the pre-check's <c>name_taken</c> code
/// (§ 6.3).
/// </para>
/// </remarks>
public sealed class RenameProjectHandler(
    IProjectRepository projects,
    IAreaRepository areas,
    IUnitOfWork unitOfWork)
{
    public async Task<RenameProjectResult> Handle(RenameProjectCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address, from the outermost level in. The organization-context filter has answered for the
                // organization already; the handler parses it again only to name its container in the next read.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.ProjectId, out var projectId)
                    || !await projects.IsReachableAsync(organizationId, projectId, ct))
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                // 2. Rules about the addressed object alone: none.

                // 3. Values in the body: one ValidationException with every field reason (only the name here).
                if (!EntityName.TryCreate(command.Name, out var name))
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>> { ["name"] = [EntityName.LengthReason] });
                }

                // 4. References in the body: none.

                // Locks, in order: the project, then its root. The status is read again after the project lock.
                var project = await projects.LockAsync(organizationId, projectId, ct);

                if (project is not { IsActive: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                // 5. The uniqueness pre-check (BR-M1-1): names compare ignoring case and surrounding spaces, among projects
                // of any status, and never against the project's own current name. A deactivated holder keeps its name
                // reserved and has a code of its own.
                var holder = await projects.FindNameHolderAsync(organizationId, name.Normalized, exceptId: project.Id, ct);

                if (holder is { } holderStatus)
                {
                    throw new ConflictException(
                        holderStatus == ProjectStatus.Active
                            ? OrganizationErrorCodes.ProjectNameTaken
                            : OrganizationErrorCodes.ProjectNameTakenByDeactivated);
                }

                var root = await areas.LockRootAsync(projectId, forUpdate: true, ct);
                project.Rename(name, root);

                // 6. Save. A concurrent creation or rename to the same name that committed first fails this update on
                // the name index, and the repository answers it with the pre-check's code (plan-backend § 6.3).
                await projects.SaveAsync(ct);

                return new RenameProjectResult(new ProjectResponse(project.Id, project.OrganizationId, project.Name, project.Key));
            },
            cancellationToken);
}
