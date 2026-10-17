using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.DeleteArea;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}</c>
/// (US-5 "Only an empty leaf is deleted" and "A leaf deleted while a child is put
/// under it"). An area has no
/// status and no restore: the row is removed outright, and only when it is an
/// empty leaf (BR-M1-13). mod-017 (BR-OT-10): a team has no default area, so
/// that is no reason to refuse; moving, renaming or deleting an area never changes an item's team.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3, api-contract § 0.4), one
/// numbered step each below: the address from the outside in, ending at the
/// area; the rule about the addressed area alone (the root); no body, so no
/// values and no references; the domain rule, which is this use case's 409
/// (a node with children or work items); the save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 8.2, § 8.1): the area <c>FOR UPDATE</c>, and
/// nothing else, so the lock order of the module has nothing to order. The
/// lock is what closes the two races of a leaf deleted while something is put
/// under it. A creation and a move lock the leaf <c>FOR SHARE</c> as the parent.
/// Whoever comes second re-reads it as the first left it: a deletion that
/// went first leaves no row and the other answers
/// <see cref="OrganizationErrorCodes.AreaNotFound"/>, and a child that went
/// first is seen by the children check below, which runs only after the lock, as
/// a new statement under READ COMMITTED, and answers
/// <see cref="OrganizationErrorCodes.AreaNotEmpty"/>. The project is not locked:
/// a write beneath only reads its containers' status (ADR-0026 rule 8).
/// </para>
/// <para>
/// <b>Work items</b> (mod-003 US-6, BR-M3-24): an area that holds one, in any
/// state, is not empty either. <see cref="IAreaUsage"/> answers after the lock,
/// like the children check: a save that moves an item into the area locks it
/// <c>FOR SHARE</c>, so it serializes with this deletion on the same row.
/// </para>
/// </remarks>
public sealed class DeleteAreaHandler(
    IProjectRepository projects,
    IAreaRepository areas,
    IAreaUsage areaUsage,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(DeleteAreaCommand command, CancellationToken cancellationToken) =>
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

                // The lock comes before the load: an area deleted meanwhile is gone when the lock is granted, and the
                // load finds nothing (a malformed, unknown and another project's area all find nothing too, BR-M1-22).
                var area = await areas.LockForUpdateAsync(projectId, areaId, ct)
                    ?? throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);

                // 2. The rule about the addressed object alone: the root is never deleted (BR-M1-11). It comes before
                // the children, although the root has some.
                area.EnsureNotRoot();

                // 3. and 4. No body: no values to validate and no references to resolve.

                // 5. The domain rule, read after the lock: a node with children or work items is refused.
                var hasChildren = await areas.HasChildrenAsync(area.Id, ct);
                var holdsWorkItems = !hasChildren && await areaUsage.IsInUseAsync(projectId, area.Id, ct);

                area.EnsureDeletable(hasChildren, holdsWorkItems);

                // 6. Save. Nothing beneath the area can be added from here to the commit: the lock is held.
                areas.Remove(area);
                await areas.SaveAsync(ct);

                return true;
            },
            cancellationToken);
}
