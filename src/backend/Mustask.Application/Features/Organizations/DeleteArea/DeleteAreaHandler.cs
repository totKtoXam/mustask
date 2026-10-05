using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.DeleteArea;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}</c>
/// (US-5 "Only an empty leaf is deleted" and "A leaf deleted while a child is put
/// under it", US-6 "A team's default area stays with its team"). An area has no
/// status and no restore: the row is removed outright, and only when it is an
/// empty leaf that is no team's default area (BR-M1-13, BR-M1-14).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3, api-contract § 0.4), one
/// numbered step each below: the address from the outside in, ending at the
/// area; the rule about the addressed area alone (the root); no body, so no
/// values and no references; the domain rules, which are this use case's 409s
/// (a node with children, then a default area); the save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 8.2, § 8.1): the area <c>FOR UPDATE</c>, and
/// nothing else, so the lock order of the module has nothing to order. The
/// lock is what closes the two races of a leaf deleted while something is put
/// under it. A creation and a move lock the leaf <c>FOR SHARE</c> as the parent,
/// and a default change locks it <c>FOR UPDATE</c>, so the two serialize on the
/// row. Whoever comes second re-reads it as the first left it: a deletion that
/// went first leaves no row and the other answers
/// <see cref="OrganizationErrorCodes.AreaNotFound"/>, and a child that went
/// first is seen by the children check below, which runs only after the lock, as
/// a new statement under READ COMMITTED, and answers
/// <see cref="OrganizationErrorCodes.AreaNotEmpty"/>. The project is not locked:
/// a write beneath only reads its containers' status (ADR-0026 rule 8).
/// </para>
/// </remarks>
public sealed class DeleteAreaHandler(
    IProjectRepository projects,
    ITeamRepository teams,
    IAreaRepository areas,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(DeleteAreaCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address, from the outermost level in. The organization-context filter has answered for the
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
                // the children and the default, although the root has both.
                area.EnsureNotRoot();

                // 3. and 4. No body: no values to validate and no references to resolve.

                // 5. The domain rules, read after the lock: a node with children is refused first, then a team's
                // default area, whose refusal names the team (US-6).
                var hasChildren = await areas.HasChildrenAsync(area.Id, ct);
                var defaultTeamName = area.DefaultOfTeamId is { } defaultTeamId
                    ? await teams.FindNameAsync(defaultTeamId, ct)
                    : null;

                area.EnsureDeletable(hasChildren, defaultTeamName);

                // 6. Save. Nothing beneath the area can be added from here to the commit: the lock is held.
                areas.Remove(area);
                await areas.SaveAsync(ct);

                return true;
            },
            cancellationToken);
}
