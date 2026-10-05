using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.CreateProject;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects</c> (US-3 "A project is
/// created ready to use", "The key is required and well-formed", "The key is
/// unique in the organization", "The name is unique in the organization", "A
/// deactivated project's name stays reserved").
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3, api-contract § 0.4): the
/// organization of the address; no rule about the addressed object alone (a
/// create has none); the values of the body, <c>name</c> and <c>key</c> with
/// every reason at once; no references in the body; the uniqueness pre-checks,
/// the name before the key (api-contract § 2.3: "when both the name and the key
/// are taken, the name's code is answered"); the save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 8.2): none. The write only reads the status of
/// its container, the organization (ADR-0026 rule 8), and inserts. The insert
/// takes <c>FOR KEY SHARE</c> on the organization row through the foreign key,
/// which a deactivation (<c>FOR NO KEY UPDATE</c>) does not conflict with, so a
/// project created while its organization is deactivated is simply a project
/// created just before. The unique indexes close the races (ADR-0011 echelon 1):
/// the name and key pre-checks are two round-trips, and a concurrent creation in
/// between fails the insert on its index, which the repository answers with the
/// pre-check's code (plan-backend § 6.3).
/// </para>
/// </remarks>
public sealed class CreateProjectHandler(
    IOrganizationReadModel organizations,
    IProjectRepository projects,
    IClock clock,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<CreateProjectResult> Handle(CreateProjectCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address. The organization-context filter has answered for it already; the handler checks again,
                // because the filter is the boundary and not the only check (plan-backend § 7.3), and a project must never
                // be written into an organization that is not active (ADR-0026 rule 5, BR-M1-20).
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId)
                    || await organizations.FindActiveAsync(organizationId, ct) is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                // 2. Rules about the addressed object alone: none, a create addresses only its container.

                // 3. Values in the body: one ValidationException with every field reason at once. The key is judged exactly
                // as sent: not trimmed and not upper-cased (BR-M1-1).
                var errors = new Dictionary<string, IReadOnlyList<FieldError>>();

                if (!EntityName.TryCreate(command.Name, out var name))
                {
                    errors["name"] = [EntityName.LengthReason];
                }

                if (ProjectKey.Validate(command.Key) is { } keyReason)
                {
                    errors["key"] = [new FieldError(keyReason)];
                }

                if (errors.Count > 0)
                {
                    throw new ValidationException(OrganizationErrorCodes.ValidationFailed, errors);
                }

                // 4. References in the body: none.

                // 5. The uniqueness pre-checks (BR-M1-1): the name among projects of any status, ignoring case and
                // surrounding spaces, then the key among projects of any status. The name answers first.
                var nameHolder = await projects.FindNameHolderAsync(organizationId, name.Normalized, exceptId: null, ct);

                if (nameHolder is { } holderStatus)
                {
                    throw new ConflictException(
                        holderStatus == ProjectStatus.Deactivated
                            ? OrganizationErrorCodes.ProjectNameTakenByDeactivated
                            : OrganizationErrorCodes.ProjectNameTaken);
                }

                // Validate has refused a null key, so the key is present and well formed here.
                var key = command.Key!;

                if (await projects.IsKeyTakenAsync(organizationId, key, ct))
                {
                    throw new ConflictException(
                        OrganizationErrorCodes.ProjectKeyTaken,
                        new ErrorParams { ["key"] = ErrorParam.Text(key) });
                }

                // The project, its first team and its root, all named after the project, the root owned by the first team
                // and its default (BR-M1-9, BR-M1-11, BR-M1-14). The creator is the token's subject (BR-M1-21).
                var created = Project.Create(organizationId, name, key, clock.UtcNow, currentUser.SubjectId);
                projects.Add(created);

                // 6. Save. A concurrent creation of the same name or key that committed first fails this insert on its unique
                // index, and the repository answers with the pre-check's code.
                await projects.SaveAsync(ct);

                var project = created.Project;

                return new CreateProjectResult(new ProjectResponse(project.Id, project.OrganizationId, project.Name, project.Key));
            },
            cancellationToken);
}
