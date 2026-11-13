using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.MoveTeam;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/teams/{teamId}/department</c> (doc-016 BR-TD-4, US-1): a team moves to another
/// department of its organization, only while it is linked to no project. A move changes nothing but the place: members, grants,
/// sprints, backlog order and the colour stay. Moving to the department it already stands in succeeds and changes nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b>: the organization; the team (404, unlocked read: unknown, foreign and deleted alike); the body (400,
/// <c>departmentId</c> required); the destination (404, 409 <c>type_not_allowed</c>); the team again under its lock; the same place
/// (200); the links (409 <c>team.has_links</c>); the save.
/// </para>
/// <para>
/// <b>Locks</b> (ADR-0040 § 1, ADR-0031 rule 6): the tree lock, then the destination <c>FOR SHARE</c> (tier 1: its presence is the
/// lock statement's own answer, so a destination deleted at the same moment is 404, never a server error — the deletion holds the
/// tree lock first and so does every other change of a place), then the team <c>FOR NO KEY UPDATE</c> (tier 2), and only then the
/// team's links are read: a link binds the team <c>FOR SHARE</c>, so it either committed before this lock and is seen, or waits for
/// this move. The source department needs no lock of its own: the tree lock already holds every other writer of a place.
/// </para>
/// <para>Authorization is the route's (<c>organizations.team.create</c>, the administrator's); mod-023 widens it to department managers.</para>
/// </remarks>
public sealed class MoveTeamHandler(
    IOrganizationReadModel organizations,
    ITeamReadModel teamReads,
    ITeamRepository teams,
    IProjectTeamRepository links,
    IAreaRepository areas,
    IUnitOfWork unitOfWork)
{
    public async Task<MoveTeamResult> Handle(MoveTeamCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address, outside in: the organization, the team (unlocked, for the 404 before any lock).
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId)
                    || await organizations.FindActiveAsync(organizationId, ct) is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.TeamId, out var teamId)
                    || await teamReads.FindAsync(organizationId, teamId, ct) is null)
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                // 2. The body: the destination is required.
                if (command.DepartmentId is null)
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>>
                        {
                            ["departmentId"] = [new FieldError(OrganizationErrorCodes.TeamDepartmentRequired)],
                        });
                }

                if (!AddressedId.TryParse(command.DepartmentId, out var destinationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
                }

                // 3. The locks, in rule 6's order: the tree, the destination, then the team.
                await areas.LockTreeAsync(organizationId, ct);

                var destination = await areas.LockForShareAsync(organizationId, destinationId, ct)
                    ?? throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);

                if (destination.Type != AreaType.Department)
                {
                    throw new ConflictException(OrganizationErrorCodes.AreaTypeNotAllowed);
                }

                // The team as the locks left it: a deletion that committed first makes it not found.
                var team = await teams.LockForNoKeyUpdateAsync(organizationId, teamId, ct);

                if (team is not { IsActive: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                // 4. The same place: nothing changes, links or not (BR-TD-4).
                if (team.DepartmentId == destinationId)
                {
                    return new MoveTeamResult(Respond(team, destination));
                }

                // 5. A linked team does not move (OQ-9 A): the links are read after the team's lock.
                if (await links.HasLinksAsync(organizationId, teamId, ct))
                {
                    throw new ConflictException(OrganizationErrorCodes.TeamHasLinks);
                }

                team.MoveTo(destinationId);
                await teams.SaveAsync(ct);

                return new MoveTeamResult(Respond(team, destination));
            },
            cancellationToken);

    private static TeamPlaceResponse Respond(Team team, Area department) =>
        new(team.Id, team.Name, team.Description, department.Id, department.Name, team.Color);
}
