using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Boards;
using Mustask.Application.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.UnlinkTeam;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/unlink</c> (feat-028 api-contract § 2.4, SC-09,
/// BR-OL-8; US-4): a team is taken off a project, and in <b>one operation</b> every item of this project that the team owns, in any
/// state, passes to the destination — another team linked to the project, or nobody — its places on the unlinked team's backlog are
/// dropped so each stands unplaced below the placed items of the destination's, the project's default-team setting is cleared if it
/// named the team, and the link is removed. The reference use case of the line (plan-backend D-15): every other slice copies its
/// patterns — the second code, the locks in one order, a fresh read after them, the set-based reassignment through the module
/// contracts.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.5, BR-OL-14): the second code (<c>organizations.team.link</c>; the route declared
/// <c>organizations.project.link</c>) <b>first of all</b>, before any id is looked at (D-7); the project (404); the team of the
/// address — an active team of the organization, a deleted, unknown or foreign one is <c>team.not_found</c>, one not linked to this
/// project is <i>not</i> a not-found here; the <c>destination</c> wrapper (400 <c>destination_required</c>, D-8); the destination, an
/// active team of the organization (404); then, under the locks, the 204 of an unlinked pair and the 409 of a destination that is the
/// team itself or is not linked.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 7.2, D-3, D-5; ADR-0028 rule 8): the links of the project to the unlinked team and to the destination
/// <c>FOR NO KEY UPDATE</c> in <b>one statement ordered by team id</b> — step 3, so two unlinks that name each other take the pair in
/// one order — and <b>no team row</b>: the status of both teams is judged on a fresh read <i>after</i> the links are held, which gives
/// the right answer whichever of an unlink and a deletion came first. Whether a team is <b>linked</b> is the answer of the lock
/// statement itself, which returns the rows it locked, and of nothing read later (review-security S-1): a link committed after that
/// statement is held by nobody, and an item or a setting that binds to it meanwhile takes a key share this unlink's
/// <c>NO KEY UPDATE</c> admits and its final <c>DELETE</c> then fails on. Such a pair is unlinked as far as this request goes — the
/// unlinked team makes the request a no-op (204), a destination makes it 409 <c>team.not_in_project</c> — as if the request had
/// run before that link. Then the project row for the setting (after the links, <b>before the items</b>: mod-005 merge, below), then
/// the owned items <c>FOR UPDATE</c> (step 5). A save, a
/// creation or a default naming that names the team holds the link <c>FOR SHARE</c> — and judges it linked by the same kind of
/// answer — so it commits before the unlink reads the items it owns (and is reassigned with the rest) or finds the link gone (409).
/// </para>
/// <para>
/// <b>The operation</b>, in this order because of the keys and the locks: the setting is cleared first, before any item is locked —
/// a project backlog's reorder holds the project row <c>FOR SHARE</c> and then its members (mod-005, ADR-0030 rule 6), and a project
/// row asked for <i>after</i> the items would close a cycle with it (mod-005 plan-backend § 14); the places of the team on this
/// project's items are dropped <i>before</i>
/// the reassignment (the place key onto the item's owner is checked at the end of the <c>UPDATE</c>); the items change owner in one
/// <c>UPDATE</c> and show the operation's time as «Изменён»; the setting is cleared before the link goes (the setting's key onto the
/// link is <c>Restrict</c>); the link is removed last. The sprint pointers of the unlinked team are <b>owed by
/// <c>mod-007-iterations</c></b>, which adds its step between the link locks and the owned-item lock (rule 8 step 4). All the
/// statements run in the request's transaction: a failure at any step leaves the link, the items, their places and the setting as
/// they were.
/// </para>
/// </remarks>
public sealed class UnlinkTeamHandler(
    IOrganizationStructure structure,
    IProjectRepository projects,
    IProjectTeamRepository links,
    IWorkItemTeamOwnership ownership,
    ITeamBacklogPlaces places,
    IOrganizationAccess access,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(UnlinkTeamCommand command, CancellationToken cancellationToken)
    {
        // 0. The second code, first of all (D-7): a role without it learns nothing about any id, and nothing changes.
        if (!access.Holds(PermissionCatalog.OrganizationsTeamLink))
        {
            throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
        }

        // The unit of work returns a result; this use case has none (204), so it returns a flag nobody reads.
        _ = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address, from the outermost level in. The project must be reachable; the team of the address must be an
                // active team of the organization (a team not linked to the project is judged under the locks, not here).
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.ProjectId, out var projectId)
                    || !await projects.IsReachableAsync(organizationId, projectId, ct))
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                if (!AddressedId.TryParse(command.TeamId, out var teamId)
                    || await structure.FindTeamAsync(organizationId, teamId, ct) is not { Status: TeamStatus.Active })
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                // 2. The destination wrapper (D-8): its teamId key must be present, a string for a team or null for «Без команды».
                if (command.Destination is not { } destination)
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>>
                        {
                            ["destination"] = [new FieldError(OrganizationErrorCodes.TeamDestinationRequired)],
                        });
                }

                // 3. The destination, if a team: an active team of the organization (404). Whether it is linked to the project
                // is judged under the locks.
                Guid? destinationId = null;

                if (destination.TeamId is not null)
                {
                    if (!AddressedId.TryParse(destination.TeamId, out var parsed)
                        || await structure.FindTeamAsync(organizationId, parsed, ct) is not { Status: TeamStatus.Active })
                    {
                        throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                    }

                    destinationId = parsed;
                }

                // 4. The links of the pair, FOR NO KEY UPDATE, in one statement ordered by team id. The statement returns the links
                // it holds, and a team is "linked" for this unlink if and only if its link is among them (S-1): a link committed
                // after the statement is held by nobody, and an item or a setting that binds to it meanwhile would fail this unlink's
                // final DELETE on the owner key.
                var held = await links.LockLinksAsync(organizationId, projectId, destinationId is { } id ? [teamId, id] : [teamId], ct);

                // 5. Fresh reads after the locks (D-5): the unlinked team's status, then the destination's.
                var unlinked = await structure.FindProjectTeamAsync(organizationId, projectId, teamId, ct);

                if (unlinked is not { Team.Status: TeamStatus.Active })
                {
                    // Deleted while the links were awaited: the deletion committed first, and the address answers as a deleted team.
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                TeamFacts? destinationFacts = null;

                if (destinationId is { } destinationTeamId)
                {
                    var found = await structure.FindProjectTeamAsync(organizationId, projectId, destinationTeamId, ct);

                    // A destination deleted while the links were awaited is 404, whatever the pair is: its status is judged first.
                    if (found is not { Team.Status: TeamStatus.Active })
                    {
                        throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                    }

                    destinationFacts = new TeamFacts(found.Team.Id, found.Team.OrganizationId, found.Team.Status, held.Contains(found.Team.Id));
                }

                if (!held.Contains(teamId))
                {
                    // Unlinking an unlinked pair succeeds and changes nothing, whatever team the destination names (BR-OL-4). A pair
                    // linked only after the lock statement is unlinked as far as this request goes: it ran before that link.
                    return false;
                }

                // The destination is the team itself, or an active team not linked to the project: 409, and nothing has moved.
                if (destinationFacts is not null)
                {
                    ProjectTeams.EnsureDestination(destinationFacts, teamId);
                }

                // 6. The operation, in rule 8's order and the keys' order (the class remarks). The setting is cleared FIRST, before the
                // items are locked: a project backlog's reorder holds the project row FOR SHARE and then its members, so a request
                // that took the items FOR UPDATE and then asked for the project row would meet it in a cycle (mod-005 merge,
                // plan-backend § 14 "Merge with feat-028"). Nothing can name the team as the default meanwhile: the link is held.
                await projects.ClearDefaultTeamAsync(organizationId, projectId, teamId, ct);
                await ownership.LockOwnedAsync([projectId], teamId, ct);
                await places.DropForTeamInProjectAsync(teamId, projectId, ct);
                await ownership.ReassignOwnedAsync([projectId], teamId, destinationId, clock.UtcNow, ct);
                await links.RemoveLinkAsync(organizationId, projectId, teamId, ct);

                return true;
            },
            cancellationToken);
    }
}
