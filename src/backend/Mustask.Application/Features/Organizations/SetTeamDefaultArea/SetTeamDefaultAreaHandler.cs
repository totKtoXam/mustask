using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.SetTeamDefaultArea;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/default-area</c>
/// (US-4 "A team's default area is chosen among the areas it owns explicitly",
/// "The first team's default is the root, for good"; US-6 "A re-own or a
/// delete, and a default change, at the same moment"). It follows the order of
/// answers, the locks and the race handling of the reference use case
/// <c>CreateArea</c> (plan-backend § 14.2).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3, api-contract § 0.4), one
/// numbered step each below: the address from the outside in, down to the team;
/// the rule about the addressed team alone (the first team's default is fixed);
/// the values in the body (none: <c>areaId</c> is a reference); the reference in
/// the body, the area; the domain rule and the save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 8.2): the team <c>FOR NO KEY UPDATE</c> (a
/// team being deleted holds it <c>FOR UPDATE</c>, so the two serialize and the
/// re-check of <c>Active</c> after the lock sees the outcome, ADR-0026 rule 8;
/// two default changes of one team serialize on it too), then the new area and
/// the team's current default <c>FOR UPDATE</c> in ascending id order, in one
/// statement. The current default is read from the committed rows only after the
/// team lock is held, and nothing but this very operation can move a team's
/// default mark while it is held, so the id read is the id locked. A re-own or a
/// delete of the new area locks the same row <c>FOR UPDATE</c>: whichever comes
/// second sees the other's outcome after its own lock and refuses with its own
/// code (<c>area.is_team_default</c> from the re-own and the delete,
/// <c>team.default_area_not_owned</c> or <c>area.not_found</c> from here).
/// </para>
/// <para>
/// <b>Two flushes.</b> The default area lives on the area row, and its unique
/// index is checked row by row, so one statement that moved the mark would
/// collide with itself: the old default is cleared and flushed, and only then is
/// the new one marked and flushed (<see cref="TeamDefaultArea.RequiresChange"/>).
/// </para>
/// </remarks>
public sealed class SetTeamDefaultAreaHandler(
    IProjectRepository projects,
    ITeamRepository teams,
    IAreaRepository areas,
    IUnitOfWork unitOfWork)
{
    public async Task<SetTeamDefaultAreaResult> Handle(SetTeamDefaultAreaCommand command, CancellationToken cancellationToken) =>
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

                // The team: locked FOR NO KEY UPDATE and re-checked after the lock, so a team deleted meanwhile
                // answers not found (US-7, ADR-0026 rule 8). A team of another project is no team of this one (BR-M1-22).
                if (!AddressedId.TryParse(command.TeamId, out var teamId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                var team = await teams.LockForNoKeyUpdateAsync(projectId, teamId, ct);

                if (team is not { IsActive: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                // 2. A rule about the addressed team alone: the first team's default is the root, for good (BR-M1-14).
                // It answers before the body is looked at (api-contract § 0.4 step 2 before 4).
                if (team.IsFirstTeam)
                {
                    throw new ConflictException(OrganizationErrorCodes.TeamFirstTeamDefaultFixed);
                }

                // 3. Values in the body: none, the body holds one reference.

                // 4. The reference in the body: an area of this project. A reference that is null, malformed, unknown or
                // of another project gets area.not_found (BR-M1-22). The committed rows answer that without a lock and
                // name the current default, whose id the area locks need.
                if (!AddressedId.TryParse(command.AreaId, out var areaId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                var nodes = await areas.GetNodesAsync(projectId, ct);

                if (nodes.All(node => node.Id != areaId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                // Every active team has exactly one default area, and the team lock keeps it where it is.
                var currentId = nodes.Single(node => node.DefaultOfTeamId == team.Id).Id;

                // Both areas, FOR UPDATE, in ascending id order; the rows come back as their last committer left them.
                // An area deleted since the read above is gone from the answer, and that is its not-found.
                var locked = await areas.LockManyForUpdateAsync(projectId, [areaId, currentId], ct);
                var next = locked.SingleOrDefault(area => area.Id == areaId)
                    ?? throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                var current = locked.Single(area => area.Id == currentId);

                // 5. The domain rule: an area the team owns explicitly (409 default_area_not_owned, also for an area
                // re-owned after the read above). Setting the current default again changes nothing.
                if (TeamDefaultArea.RequiresChange(team, current, next))
                {
                    // Two flushes, the old mark cleared before the new one is set (see the remarks). No uniqueness
                    // pre-check and no mapped race: the team lock is the only writer of this team's mark.
                    current.ClearDefault();
                    await areas.SaveAsync(ct);

                    next.MarkDefaultOf(team);
                    await areas.SaveAsync(ct);
                }

                // The path is read after the locks, from the rows as they stand in this transaction.
                var tree = new AreaTree(await areas.GetNodesAsync(projectId, ct));

                return new SetTeamDefaultAreaResult(
                    new TeamResponse(
                        team.Id,
                        team.ProjectId,
                        team.Name,
                        team.IsFirstTeam,
                        new TeamDefaultAreaResponse(next.Id, tree.PathOf(next.Id))));
            },
            cancellationToken);
}
