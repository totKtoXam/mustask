using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.DeleteArea;

/// <summary>The command of <c>DELETE /api/organizations/{organizationId}/areas/{areaId}</c>.</summary>
public sealed record DeleteAreaCommand(string? OrganizationId, string? AreaId);

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/areas/{areaId}</c> (mod-018 api-contract § 2, US-3, BR-AW-9): an area with no child
/// is deleted outright and its project and team links end with it (the database cascades them). An area with a child answers 409
/// <c>area.not_empty</c>; nothing else holds it back, since no item belongs to an area any more (BR-AW-2).
/// </summary>
/// <remarks>
/// <b>The order of answers</b>: the organization; the area (404); 409. <b>Locks</b>: the tree lock, then the area <c>FOR UPDATE</c>
/// (judged present by the lock statement's own answer), and the children are looked for after it — a creation under the area holds
/// the tree lock and the area <c>FOR SHARE</c>, so it either committed before this lock or waits for this deletion and finds the
/// parent gone.
/// </remarks>
public sealed class DeleteAreaHandler(IOrganizationReadModel organizations, IAreaRepository areas, IUnitOfWork unitOfWork)
{
    public async Task Handle(DeleteAreaCommand command, CancellationToken cancellationToken) =>
        _ = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId)
                    || await organizations.FindActiveAsync(organizationId, ct) is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.AreaId, out var areaId)
                    || await areas.FindAsync(organizationId, areaId, ct) is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                await areas.LockTreeAsync(organizationId, ct);

                var area = await areas.LockForUpdateAsync(organizationId, areaId, ct)
                    ?? throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);

                if (await areas.HasChildrenAsync(organizationId, area.Id, ct))
                {
                    throw new ConflictException(OrganizationErrorCodes.AreaNotEmpty);
                }

                areas.Remove(area);
                await areas.SaveAsync(ct);

                return true;
            },
            cancellationToken);
}
