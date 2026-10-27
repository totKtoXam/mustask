using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.RenameArea;

/// <summary>The command of <c>PUT /api/organizations/{organizationId}/areas/{areaId}/name</c>.</summary>
public sealed record RenameAreaCommand(string? OrganizationId, string? AreaId, string? Name);

/// <summary>The area as every area write returns it (mod-018 api-contract § 1, <c>Area</c>).</summary>
public sealed record AreaResponse(Guid Id, Guid? ParentId, string Name, string? Description, string Path);

/// <summary>The handler's outcome: the body of the answer.</summary>
public sealed record RenameAreaResult(AreaResponse Response);

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/areas/{areaId}/name</c> (mod-018 api-contract § 2, US-3): the area's name, unique among
/// its siblings ignoring case and surrounding spaces. A new name equal to the area's own is fine (the case may change). The paths of
/// the whole subtree change with it; the answer carries the area's own.
/// </summary>
/// <remarks>
/// <b>The order of answers</b>: the organization; the area (404); the name (400); the sibling name (409). <b>Locks</b>: the area
/// <c>FOR UPDATE</c> only — a rename changes no shape, so it does not take the tree lock; a concurrent move of the area waits for it
/// and a concurrent creation of the same name under the same parent is caught by the sibling index.
/// </remarks>
public sealed class RenameAreaHandler(IOrganizationReadModel organizations, IAreaRepository areas, IUnitOfWork unitOfWork)
{
    public async Task<RenameAreaResult> Handle(RenameAreaCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address, outside in. The lock answers whether the area is there.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId)
                    || await organizations.FindActiveAsync(organizationId, ct) is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                var area = AddressedId.TryParse(command.AreaId, out var areaId)
                    ? await areas.LockForUpdateAsync(organizationId, areaId, ct)
                    : null;

                if (area is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                // 3. The value in the body.
                if (!EntityName.TryCreate(command.Name, out var name))
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>> { ["name"] = [EntityName.LengthReason] });
                }

                // 5. The sibling name, never against the area's own current name.
                if (await areas.IsSiblingNameTakenAsync(organizationId, area.ParentId, name.Normalized, area.Id, ct))
                {
                    throw new ConflictException(OrganizationErrorCodes.AreaNameTaken);
                }

                area.Rename(name);
                await areas.SaveAsync(ct);

                var tree = new AreaTree(await areas.GetNodesAsync(organizationId, ct));

                return new RenameAreaResult(new AreaResponse(area.Id, area.ParentId, area.Name, area.Description, tree.PathOf(area.Id)));
            },
            cancellationToken);
}
