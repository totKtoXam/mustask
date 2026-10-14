using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.CreateArea;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/areas</c>
/// (US-5 "A child area is created under any node", US-6 "created with or
/// without an owner"). The reference use case of the module (plan-backend
/// § 14.2): the lanes copy its order of answers, its locks and its race
/// handling.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3, api-contract § 0.4), one
/// numbered step each below: the address from the outside in; rules about the
/// addressed object alone (none here: the address ends at the project); the
/// values in the body, every reason at once; the references in the body, in
/// lock order; the uniqueness pre-check; the save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 8.2): the owner team <c>FOR SHARE</c> (a team
/// being deleted holds it <c>FOR UPDATE</c>, so the two serialize and the
/// re-check of <c>Active</c> after the lock sees the outcome, ADR-0026 rule 8),
/// then the parent <c>FOR SHARE</c> (a leaf being deleted holds it
/// <c>FOR UPDATE</c>, so no child is ever left under a deleted parent,
/// BR-M1-13). The project is not locked: a write beneath only reads its
/// containers' status (ADR-0026 rule 8).
/// </para>
/// </remarks>
public sealed class CreateAreaHandler(
    IProjectRepository projects,
    ITeamRepository teams,
    IAreaRepository areas,
    IUnitOfWork unitOfWork)
{
    public async Task<CreateAreaResult> Handle(CreateAreaCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address, from the outermost level in. The scoped check has answered for the
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

                // 2. Rules about the addressed object alone: none, the address ends at the project.

                // 3. Values in the body: one ValidationException with every field reason.
                if (!EntityName.TryCreate(command.Name, out var name))
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>> { ["name"] = [EntityName.LengthReason] });
                }

                // 4. References in the body, in lock order: the owner team, then the parent area. A reference that is
                // malformed, unknown, not active or of another project gets its level's not-found code (BR-M1-22).
                var owner = await LockOwnerAsync(projectId, command.OwnerTeamId, ct);

                if (!AddressedId.TryParse(command.ParentId, out var parentId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                var parent = await areas.LockForShareAsync(projectId, parentId, ct)
                    ?? throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);

                // 5. The uniqueness pre-check (BR-M1-12): names compare ignoring case and surrounding spaces.
                if (await areas.IsSiblingNameTakenAsync(parent.Id, name.Normalized, exceptId: null, ct))
                {
                    throw new ConflictException(OrganizationErrorCodes.AreaNameTaken);
                }

                var area = Area.CreateChild(parent, name, owner);
                areas.Add(area);

                // 6. Save. A concurrent creation of the same sibling name that committed first fails this insert on
                // the sibling index, and the repository answers it with the pre-check's code (plan-backend § 6.3).
                await areas.SaveAsync(ct);

                return new CreateAreaResult(new AreaResponse(area.Id, area.ProjectId, parent.Id, area.Name, area.OwnerTeamId));
            },
            cancellationToken);

    /// <summary>
    /// <c>null</c> or absent: the area inherits (US-6). Otherwise an active team of
    /// this project, locked <c>FOR SHARE</c> and re-checked after the lock: a team
    /// deleted meanwhile answers <see cref="OrganizationErrorCodes.TeamNotFound"/>
    /// (US-7, ADR-0026 rule 8).
    /// </summary>
    private async Task<Team?> LockOwnerAsync(Guid projectId, string? ownerTeamId, CancellationToken cancellationToken)
    {
        if (ownerTeamId is null)
        {
            return null;
        }

        if (!AddressedId.TryParse(ownerTeamId, out var teamId))
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        var owner = await teams.LockForShareAsync(projectId, teamId, cancellationToken);

        return owner is { IsActive: true }
            ? owner
            : throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
    }
}
