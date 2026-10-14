using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.SetAreaOwner;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}/owner</c>
/// (US-6 "An area is re-owned", "An explicit owner is cleared", "A team's default
/// area stays with its team", "The root's owner is fixed", "An area's owner is a
/// team of the area's own project"). It copies the reference use case
/// <c>CreateArea</c> (plan-backend § 14.2): its order of answers, its locks and
/// its handling of a team that is deleted meanwhile.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3, api-contract § 0.4), one
/// numbered step each below: the address from the outside in, down to the area;
/// the rule about the addressed area alone (the root is fixed); the values in
/// the body (none: the owner is a reference, not a value); the reference in the
/// body, in lock order; the domain rules; the save. So a request that names an
/// unknown area and a deleted team gets <c>area.not_found</c>, and one that
/// names the root and a deleted team gets <c>root_is_fixed</c>.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 8.2): the handler addresses an area but binds a
/// team, so it reads the area without a lock for its not-found answer, locks the
/// new owner <c>FOR SHARE</c> (a team being deleted holds it <c>FOR UPDATE</c>,
/// so the two serialize and the re-check of <c>Active</c> after the lock sees the
/// outcome, ADR-0026 rule 8), then locks the area <c>FOR UPDATE</c> and reads it
/// again, because the lock waited for any other writer of that row (a default
/// change, a delete) and what was read before it may be stale. Team before area
/// is the module's lock order. The project is not locked: a write beneath only
/// reads its containers' status (ADR-0026 rule 8).
/// </para>
/// </remarks>
public sealed class SetAreaOwnerHandler(
    IProjectRepository projects,
    ITeamRepository teams,
    IAreaRepository areas,
    IUnitOfWork unitOfWork)
{
    public async Task<SetAreaOwnerResult> Handle(SetAreaOwnerCommand command, CancellationToken cancellationToken) =>
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

                // The area is read without a lock, only to answer for the address before anything else is locked.
                if (!AddressedId.TryParse(command.AreaId, out var areaId)
                    || await areas.FindAsync(projectId, areaId, ct) is not { } addressed)
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                // 2. The rule about the addressed area alone: the root keeps its owner for good (BR-M1-11). The root
                // never changes, so the unlocked read is enough to decide it.
                addressed.EnsureNotRoot();

                // 3. Values in the body: none. The owner is a reference to a team, answered in step 4.

                // 4. The reference in the body, then the area, in lock order (team before area, § 8.2).
                var ownerTeamId = await LockOwnerAsync(projectId, command.OwnerTeamId, ct);

                // The area locked and read again: it may have been deleted, re-owned or made a default while this
                // request waited for the team's lock, and only the locked row is the truth.
                var area = await areas.LockForUpdateAsync(projectId, areaId, ct)
                    ?? throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);

                // 5. Domain rules. The refusal of a default area names its team, so the name is read for it; the
                // domain decides whether the owner changes at all, and a request that repeats the current owner is
                // accepted (api-contract § 4.5) even for a default area.
                var defaultTeamName = area.DefaultOfTeamId is { } defaultTeamId
                    ? await teams.FindNameAsync(defaultTeamId, ct)
                    : null;

                // 6. Save, only when something changed. There is no unique-index race to map: no index covers the
                // owner, and a default area never changes owner, so the default's own unique index is untouched.
                if (area.SetOwner(ownerTeamId, defaultTeamName))
                {
                    await areas.SaveAsync(ct);
                }

                return new SetAreaOwnerResult(
                    new AreaResponse(area.Id, area.ProjectId, area.ParentId!.Value, area.Name, area.OwnerTeamId));
            },
            cancellationToken);

    /// <summary>
    /// <c>null</c> or absent: the explicit owner is cleared and the area inherits
    /// (US-6). Otherwise an active team of this project, locked <c>FOR SHARE</c>
    /// and re-checked after the lock: a team deleted meanwhile answers
    /// <see cref="OrganizationErrorCodes.TeamNotFound"/> (US-7, ADR-0026 rule 8).
    /// </summary>
    private async Task<Guid?> LockOwnerAsync(Guid projectId, string? ownerTeamId, CancellationToken cancellationToken)
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
            ? owner.Id
            : throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
    }
}
