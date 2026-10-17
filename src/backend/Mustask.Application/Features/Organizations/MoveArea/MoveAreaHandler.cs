using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.MoveArea;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}/parent</c>
/// (US-5 "A node moves with its whole subtree", "A move needs a free name at its
/// destination", "A node is never moved under itself or its descendant", "A node
/// never leaves its project"; mod-017 BR-OT-10: a move changes no item's team, as an area
/// owns none). Follows the reference use case <c>CreateArea</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3, api-contract § 0.4), one
/// numbered step each below: the address from the outside in, ending at the moved
/// area; the rule about that area alone (the root is never moved); the values in
/// the body (none: the body holds a reference only); the reference in the body,
/// <c>parentId</c>, which is an area of this project; the domain rule and the
/// uniqueness pre-check, in that order; the save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 8.2): the project row <c>FOR NO KEY UPDATE</c>
/// first, so that moves in one project run one at a time and the cycle check
/// below reads parent links no other move can change. Then the moved area
/// <c>FOR UPDATE</c> and the new parent <c>FOR SHARE</c>, in ascending id order
/// (a leaf being deleted holds its row <c>FOR UPDATE</c>, so no area is ever left
/// under a deleted parent, BR-M1-13). The refusals that need no lock (the address
/// and the root) are answered before any is taken, from an unlocked read of the
/// area.
/// </para>
/// <para>
/// <b>What a move changes</b> is one column of one row: the subtree goes with it
/// because every descendant keeps its own parent; no owner is stored or resolved, and
/// no item changes its team (mod-017 BR-OT-10, <see cref="AreaTree"/>).
/// </para>
/// </remarks>
public sealed class MoveAreaHandler(
    IProjectRepository projects,
    IAreaRepository areas,
    IUnitOfWork unitOfWork)
{
    public async Task<MoveAreaResult> Handle(MoveAreaCommand command, CancellationToken cancellationToken) =>
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

                // The area, unlocked: for the not-found answer and the rule below, before any lock is taken.
                if (!AddressedId.TryParse(command.AreaId, out var areaId)
                    || await areas.FindAsync(projectId, areaId, ct) is not { } addressed)
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                // 2. The rule about the addressed area alone: the root is never moved (BR-M1-11).
                addressed.EnsureNotRoot();

                // 3. Values in the body: none, the body holds a reference only.

                // 4. The reference in the body: the new parent, an area of this project (BR-M1-22). A parent that is
                // null, malformed, unknown or of another project gets area.not_found.
                if (!AddressedId.TryParse(command.ParentId, out var parentId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                // Locks, in order (plan-backend § 8.2). Only the lock matters here: the project was reachable a moment
                // ago, and a deactivation that commits meanwhile is serializable either way (ADR-0026 rule 8).
                await projects.LockAsync(organizationId, projectId, ct);
                var (area, parent) = await LockAreaAndParentAsync(projectId, areaId, parentId, ct);

                // 5. The domain rule, then the uniqueness pre-check (BR-M1-12). The tree is read after the locks, so
                // it holds every move that committed before this one took the project row.
                var tree = new AreaTree(await areas.GetNodesAsync(projectId, ct));

                if (!area.MoveUnder(parent, tree))
                {
                    // Already under that parent: nothing changes (api-contract § 4.4).
                    return Result(area, parent);
                }

                if (await areas.IsSiblingNameTakenAsync(parent.Id, area.NormalizedName, area.Id, ct))
                {
                    throw new ConflictException(OrganizationErrorCodes.AreaNameTaken);
                }

                // 6. Save. A sibling of the same name committed under the new parent after the pre-check fails this
                // update on the sibling index, and the repository answers it with the pre-check's code (plan-backend § 6.3).
                await areas.SaveAsync(ct);

                return Result(area, parent);
            },
            cancellationToken);

    /// <summary>
    /// The moved area <c>FOR UPDATE</c> and its new parent <c>FOR SHARE</c>, locked in ascending id order, each loaded
    /// again as the lock holder left it. Either gone since the unlocked read (a leaf deleted at the same moment) is an
    /// <see cref="OrganizationErrorCodes.AreaNotFound"/>, as a delete that committed first is (BR-M1-13). The area as
    /// its own parent is one row: it is locked once, and the domain refuses the move.
    /// </summary>
    private async Task<(Area Area, Area Parent)> LockAreaAndParentAsync(
        Guid projectId, Guid areaId, Guid parentId, CancellationToken cancellationToken)
    {
        Area? area;
        Area? parent;

        if (parentId == areaId)
        {
            area = await areas.LockForUpdateAsync(projectId, areaId, cancellationToken);
            parent = area;
        }
        else if (areaId.CompareTo(parentId) < 0)
        {
            area = await areas.LockForUpdateAsync(projectId, areaId, cancellationToken);
            parent = await areas.LockForShareAsync(projectId, parentId, cancellationToken);
        }
        else
        {
            parent = await areas.LockForShareAsync(projectId, parentId, cancellationToken);
            area = await areas.LockForUpdateAsync(projectId, areaId, cancellationToken);
        }

        return area is null || parent is null
            ? throw new NotFoundException(OrganizationErrorCodes.AreaNotFound)
            : (area, parent);
    }

    private static MoveAreaResult Result(Area area, Area parent) =>
        new(new AreaResponse(area.Id, area.ProjectId, parent.Id, area.Name));
}
