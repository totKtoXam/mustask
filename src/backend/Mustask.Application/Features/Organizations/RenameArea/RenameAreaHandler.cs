using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.RenameArea;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}/name</c>
/// (US-5 "A child area is renamed" and "Sibling names are unique"). The root is
/// renamed only with its project (BR-M1-11), so it is refused here; a child keeps
/// its children (mod-017: an area has no owner).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3, api-contract § 4.3), one
/// numbered step each below: the address from the outside in, down to the area;
/// the rule about the addressed area alone (the root is fixed); the values in the
/// body; the references in the body (none here); the sibling-name pre-check; the
/// save. So a request for the root with an empty name answers
/// <c>area.root_is_fixed</c>, and one for an unknown area answers
/// <c>area.not_found</c> whatever the name.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 8.2): the area <c>FOR UPDATE</c>, and nothing
/// else. The project is not locked: a write beneath only reads its containers'
/// status (ADR-0026 rule 8). The rename does not touch the parent link, so the
/// foreign key is not re-checked and the update waits for nobody but another
/// writer of the same row.
/// </para>
/// </remarks>
public sealed class RenameAreaHandler(
    IProjectRepository projects,
    IAreaRepository areas,
    IUnitOfWork unitOfWork)
{
    public async Task<RenameAreaResult> Handle(RenameAreaCommand command, CancellationToken cancellationToken) =>
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

                if (!AddressedId.TryParse(command.AreaId, out var areaId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                // The area of this project, locked and loaded as the lock holder left it: an area deleted meanwhile
                // is gone here, and answers as an unknown one.
                var area = await areas.LockForUpdateAsync(projectId, areaId, ct)
                    ?? throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);

                // 2. Rules about the addressed area alone: the root is renamed only with its project (BR-M1-11).
                area.EnsureNotRoot();

                // 3. Values in the body: one ValidationException with every field reason.
                if (!EntityName.TryCreate(command.Name, out var name))
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>> { ["name"] = [EntityName.LengthReason] });
                }

                // 4. References in the body: none, the name is the only field.

                // 5. The uniqueness pre-check (BR-M1-12): names compare ignoring case and surrounding spaces, among the
                // children of the same parent. The area itself is left out, so its own name in another case is free.
                // A child always has a parent: the root was refused in step 2.
                var parentId = area.ParentId!.Value;

                if (await areas.IsSiblingNameTakenAsync(parentId, name.Normalized, exceptId: area.Id, ct))
                {
                    throw new ConflictException(OrganizationErrorCodes.AreaNameTaken);
                }

                area.Rename(name);

                // 6. Save. A concurrent creation or rename to the same sibling name that committed first fails this update
                // on the sibling index, and the repository answers it with the pre-check's code (plan-backend § 6.3).
                await areas.SaveAsync(ct);

                return new RenameAreaResult(new AreaResponse(area.Id, area.ProjectId, parentId, area.Name));
            },
            cancellationToken);
}
