using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.SetAreaDescription;

/// <summary>The command of <c>PUT /api/organizations/{organizationId}/areas/{areaId}/description</c>.</summary>
public sealed record SetAreaDescriptionCommand(string? OrganizationId, string? AreaId, string? Description);

/// <summary>The area as every area write returns it (mod-018 api-contract § 1, <c>Area</c>).</summary>
public sealed record AreaResponse(Guid Id, Guid? ParentId, string Name, string? Description, string Path);

/// <summary>The handler's outcome: the body of the answer.</summary>
public sealed record SetAreaDescriptionResult(AreaResponse Response);

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/areas/{areaId}/description</c> (mod-018 api-contract § 2, US-3, BR-AW-4): the area's
/// description, kept exactly as sent (at most 2 000 characters); empty or <c>null</c> clears it. <b>Order</b>: the organization; the
/// area (404); the value (400). <b>Locks</b>: the area <c>FOR UPDATE</c> only.
/// </summary>
public sealed class SetAreaDescriptionHandler(IOrganizationReadModel organizations, IAreaRepository areas, IUnitOfWork unitOfWork)
{
    public async Task<SetAreaDescriptionResult> Handle(SetAreaDescriptionCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
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

                if (!EntityDescription.TryCreate(command.Description, out var description))
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>> { ["description"] = [EntityDescription.LengthReason] });
                }

                area.Describe(description);
                await areas.SaveAsync(ct);

                var tree = new AreaTree(await areas.GetNodesAsync(organizationId, ct));

                return new SetAreaDescriptionResult(
                    new AreaResponse(area.Id, area.ParentId, area.Name, area.Description, tree.PathOf(area.Id)));
            },
            cancellationToken);
}
