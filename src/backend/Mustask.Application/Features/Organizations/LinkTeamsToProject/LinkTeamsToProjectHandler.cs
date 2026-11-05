using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.LinkTeamsToProject;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/teams</c> (feat-028 api-contract § 2.4; US-3 "a link from either
/// page", "several ends in one confirmation", "linking a linked pair", "only active ends are linked"): the project's page links
/// teams to the project. Each pair is linked <b>once</b> — a pair already linked keeps who made it and when (BR-OL-4) — and the
/// request is <b>all or nothing</b>: one entry that is not an active team of the organization, and nothing is linked. Linking moves no
/// item and names no default (BR-OL-7: a project that gets its first team has it as its default by inference).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.1, § 0.5): the second code (<c>organizations.team.link</c>; the route declared
/// <c>organizations.project.link</c>) <b>first of all</b>, before any id is looked at (D-7); the project (404); the team entries
/// (404 <c>team.not_found</c>, each of: malformed, unknown, foreign, deleted); an absent or empty list is a no-op 204 (D-14).
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 7.2, C-10, C-11, C-17): every team named <c>FOR SHARE</c> in <b>one statement ordered by id</b> (step 2,
/// the binder's mode, which does not wait for another binder or for the project), then one <c>INSERT … ON CONFLICT DO NOTHING</c> for
/// all the pairs. A deletion of one of the teams holds the team row <c>FOR NO KEY UPDATE</c>: it commits first (the team reads deleted
/// and the whole request is refused 404), or waits for this link and removes the link it wrote with the rest — no link to a deleted
/// team remains. The project is only <i>read</i> (ADR-0026 rule 8): a deactivation does not conflict with the insert's key share, so a
/// link made just before the deactivation stands, as the contract says. Two requests that link one unlinked pair at once leave one
/// link and no 5xx.
/// </para>
/// </remarks>
public sealed class LinkTeamsToProjectHandler(
    IProjectRepository projects,
    ITeamRepository teams,
    IProjectTeamRepository links,
    IOrganizationAccess access,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(LinkTeamsToProjectCommand command, CancellationToken cancellationToken)
    {
        // 0. The second code, first of all (D-7).
        if (!access.Holds(PermissionCatalog.OrganizationsTeamLink))
        {
            throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
        }

        // The unit of work returns a result; this use case has none (204), so it returns a flag nobody reads.
        _ = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.ProjectId, out var projectId)
                    || !await projects.IsReachableAsync(organizationId, projectId, ct))
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                // 2. The team entries: any one that is not even an id names no team (404). Duplicates collapse.
                var teamIds = new HashSet<Guid>();

                foreach (var raw in command.TeamIds ?? [])
                {
                    if (!AddressedId.TryParse(raw, out var teamId))
                    {
                        throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                    }

                    teamIds.Add(teamId);
                }

                if (teamIds.Count == 0)
                {
                    // Nothing to link and nothing to refuse (D-14).
                    return false;
                }

                // 3. Every team locked FOR SHARE in one statement ordered by id, then judged on a fresh read: all active teams of the
                // organization, or none is linked.
                var locked = await teams.LockManyForShareAsync(organizationId, teamIds, ct);

                if (locked.Count != teamIds.Count || locked.Any(team => !team.IsActive))
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                // 4. One statement for all the pairs; a pair already linked is left as it is.
                await links.LinkAsync(
                    organizationId,
                    teamIds.Select(id => (projectId, id)).ToList(),
                    currentUser.SubjectId,
                    clock.UtcNow,
                    ct);

                return true;
            },
            cancellationToken);
    }
}
