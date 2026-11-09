using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.SetInheritsAccess;

/// <summary>The command of <c>PUT /api/organizations/{organizationId}/areas/{areaId}/inherits-access</c>; <c>Value</c> is nullable so a missing one is a validation answer.</summary>
public sealed record SetInheritsAccessCommand(string? OrganizationId, string? AreaId, bool? Value);

/// <summary>The answer: the node's value now.</summary>
public sealed record SetInheritsAccessResponse(bool InheritsAccess);

/// <summary>
/// <c>PUT …/areas/{areaId}/inherits-access</c> (mod-019 api-contract § 3, US-2, BR-SA-6, BR-SA-7): turns «Наследовать доступ» of a
/// project node or a sub-area on or off. The route declared <c>organizations.area.write</c> on the node; the second code is on the
/// node's <b>parent</b> — <c>area.write</c> on it, or <c>area.create</c> for a top-level project node — and is decided on the <b>locked
/// row</b> (BR-SA-13): a concurrent move changes the answer and never leaves a stale pass.
/// </summary>
/// <remarks>
/// <b>The order of answers</b>: the organization; the node (404, unlocked, for its parent); the parent's code (403); a department
/// (409 <c>type_not_allowed</c>); the value (400); then the locks — the organization's tree, then the node <c>FOR UPDATE</c> — and the
/// parent's code and the node's own again on the locked row; a switch to the value it has is a 200 that changes nothing. The change takes effect on the
/// next request: nothing is cached (BR-SA-8).
/// </remarks>
public sealed class SetInheritsAccessHandler(
    IOrganizationReadModel organizations,
    IProjectRepository projects,
    IAreaRepository areas,
    IOrganizationAccess access,
    IUnitOfWork unitOfWork)
{
    public async Task<SetInheritsAccessResponse> Handle(SetInheritsAccessCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
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

                if (!await HoldsOnParentAsync(addressed.ParentId, ct))
                {
                    throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
                }

                if (addressed.Type == AreaType.Department)
                {
                    throw new ConflictException(OrganizationErrorCodes.AreaTypeNotAllowed);
                }

                if (command.Value is not { } value)
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>> { ["value"] = [new FieldError(OrganizationErrorCodes.ValidationFailed)] });
                }

                await areas.LockTreeAsync(organizationId, ct);
                var area = await areas.LockForUpdateAsync(organizationId, areaId, ct)
                    ?? throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);

                // The parent as the lock holds it (a move that committed meanwhile, BR-SA-13) and the node's own code: an ancestor switched off under the route's check no longer
                // passes access down to the node (review C-8).
                if (!await HoldsOnParentAsync(area.ParentId, ct)
                    || !await access.HoldsOnAsync(PermissionCatalog.OrganizationsAreaWrite, areaId, ct))
                {
                    throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
                }

                await AreaRequests.EnsureProjectActiveAsync(projects, organizationId, area, ct);

                if (area.SetInheritsAccess(value))
                {
                    await areas.SaveAsync(ct);
                }

                return new SetInheritsAccessResponse(area.InheritsAccess);
            },
            cancellationToken);

    private async Task<bool> HoldsOnParentAsync(Guid? parentId, CancellationToken cancellationToken) =>
        parentId is { } parent
            ? await access.HoldsOnAsync(PermissionCatalog.OrganizationsAreaWrite, parent, cancellationToken)
            : access.Holds(PermissionCatalog.OrganizationsAreaCreate);
}
