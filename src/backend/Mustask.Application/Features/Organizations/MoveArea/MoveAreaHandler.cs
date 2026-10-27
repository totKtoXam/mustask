using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.MoveArea;

/// <summary>The command of <c>PUT /api/organizations/{organizationId}/areas/{areaId}/parent</c>. A <c>null</c> parent is the top level.</summary>
public sealed record MoveAreaCommand(string? OrganizationId, string? AreaId, string? ParentId);

/// <summary>The area as every area write returns it (mod-018 api-contract § 1, <c>Area</c>).</summary>
public sealed record AreaResponse(Guid Id, Guid? ParentId, string Name, string? Description, string Path);

/// <summary>The handler's outcome: the body of the answer.</summary>
public sealed record MoveAreaResult(AreaResponse Response);

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/areas/{areaId}/parent</c> (mod-018 api-contract § 2, US-3, BR-AW-3, BR-AW-4): the
/// area, with all its subtree, goes under another area or to the top level. The parent it already has is a success that changes
/// nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b>: the second code (<c>area.create</c>) first when the destination is the top level, and again once
/// the area is read when its current parent is; the organization; the area (404); the destination (404); then 409 — into its own
/// subtree, the depth, the sibling name.
/// </para>
/// <para>
/// <b>Locks</b> (ADR-0028 rule 8): the tree lock, then the area <c>FOR UPDATE</c> and the destination <c>FOR SHARE</c> in id order,
/// each judged present by its lock statement's own answer; the tree is read after them, so it holds every move and creation that
/// committed before this one's locks.
/// </para>
/// </remarks>
public sealed class MoveAreaHandler(
    IOrganizationReadModel organizations,
    IAreaRepository areas,
    IOrganizationAccess access,
    IUnitOfWork unitOfWork)
{
    public async Task<MoveAreaResult> Handle(MoveAreaCommand command, CancellationToken cancellationToken)
    {
        // 0. The second code, first of all, when the destination is the top level.
        if (command.ParentId is null && !access.Holds(PermissionCatalog.OrganizationsAreaCreate))
        {
            throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
        }

        return await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address, outside in: the organization, the area (unlocked, for the 404 and the current parent).
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId)
                    || await organizations.FindActiveAsync(organizationId, ct) is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.AreaId, out var areaId)
                    || await areas.FindAsync(organizationId, areaId, ct) is not { } addressed)
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                if (addressed.IsTopLevel && !access.Holds(PermissionCatalog.OrganizationsAreaCreate))
                {
                    throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
                }

                // 4. The destination in the body: an area of this organization (404), unlocked here.
                Guid? parentId = null;

                if (command.ParentId is not null)
                {
                    if (!AddressedId.TryParse(command.ParentId, out var destinationId)
                        || await areas.FindAsync(organizationId, destinationId, ct) is null)
                    {
                        throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                    }

                    parentId = destinationId;
                }

                // The locks, in rule 8's order: the tree, then the area rows by id.
                await areas.LockTreeAsync(organizationId, ct);
                var area = await LockAreaAndDestinationAsync(organizationId, areaId, parentId, ct);

                // 5. The domain rules, on the tree the locks hold: into its own subtree, the depth; then the sibling name.
                var tree = new AreaTree(await areas.GetNodesAsync(organizationId, ct));

                if (area.MoveUnder(parentId, tree))
                {
                    if (await areas.IsSiblingNameTakenAsync(organizationId, parentId, area.NormalizedName, area.Id, ct))
                    {
                        throw new ConflictException(OrganizationErrorCodes.AreaNameTaken);
                    }

                    // 6. A sibling of the same name committed under the new parent after the pre-check fails the update on the
                    // sibling index and answers with the pre-check's code.
                    await areas.SaveAsync(ct);

                    tree = new AreaTree(await areas.GetNodesAsync(organizationId, ct));
                }

                return new MoveAreaResult(new AreaResponse(area.Id, area.ParentId, area.Name, area.Description, tree.PathOf(area.Id)));
            },
            cancellationToken);
    }

    private async Task<Area> LockAreaAndDestinationAsync(
        Guid organizationId, Guid areaId, Guid? destinationId, CancellationToken cancellationToken)
    {
        Area? area;
        Area? destination = null;

        if (destinationId is not { } destinationGuid || destinationGuid == areaId)
        {
            area = await areas.LockForUpdateAsync(organizationId, areaId, cancellationToken);
        }
        else if (areaId.CompareTo(destinationGuid) < 0)
        {
            area = await areas.LockForUpdateAsync(organizationId, areaId, cancellationToken);
            destination = await areas.LockForShareAsync(organizationId, destinationGuid, cancellationToken);

            if (destination is null)
            {
                throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
            }
        }
        else
        {
            destination = await areas.LockForShareAsync(organizationId, destinationGuid, cancellationToken);
            area = await areas.LockForUpdateAsync(organizationId, areaId, cancellationToken);

            if (destination is null)
            {
                throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
            }
        }

        return area ?? throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
    }
}
