using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.LinkProjectsToTeam;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/teams/{teamId}/projects</c> (feat-028 api-contract § 2.2; US-3 "a link from either
/// page", "several ends in one confirmation", "only active ends are linked"): the team's page links projects to the team. Each pair
/// is linked <b>once</b> and the request is <b>all or nothing</b>: one entry that is not an active project of the organization
/// (malformed, unknown, foreign or deactivated alike), and nothing is linked. Linking moves no item and sets no default.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.1, § 0.5): the second code (<c>organizations.project.link</c>; the route declared
/// <c>organizations.team.link</c>) <b>first of all</b>, before any id is looked at (D-7); the team (404); the project entries (404
/// <c>project.not_found</c>); an absent or empty list is a no-op 204 (D-14).
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 7.2, C-10): the team <c>FOR SHARE</c>, re-checked as active (step 2, the binder's mode), then the
/// projects judged in one statement and one <c>INSERT … ON CONFLICT DO NOTHING</c> for all the pairs. A deletion of the team holds
/// the team row <c>FOR NO KEY UPDATE</c>, so the two serialize: the deletion first, this request reads the team deleted (404) and
/// links nothing; the link first, the deletion removes the link it wrote. The projects are only read (ADR-0026 rule 8).
/// </para>
/// </remarks>
public sealed class LinkProjectsToTeamHandler(
    IProjectRepository projects,
    ITeamRepository teams,
    IProjectTeamRepository links,
    IOrganizationAccess access,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(LinkProjectsToTeamCommand command, CancellationToken cancellationToken)
    {
        // 0. The second code, first of all (D-7).
        if (!access.Holds(PermissionCatalog.OrganizationsProjectLink))
        {
            throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
        }

        // The unit of work returns a result; this use case has none (204), so it returns a flag nobody reads.
        _ = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address: the team, locked as a binder and re-checked as active.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                var team = AddressedId.TryParse(command.TeamId, out var teamId)
                    ? await teams.LockForShareAsync(organizationId, teamId, ct)
                    : null;

                if (team is not { IsActive: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                // 2. The project entries: any one that is not even an id names no project (404). Duplicates collapse.
                var projectIds = new HashSet<Guid>();

                foreach (var raw in command.ProjectIds ?? [])
                {
                    if (!AddressedId.TryParse(raw, out var projectId))
                    {
                        throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                    }

                    projectIds.Add(projectId);
                }

                if (projectIds.Count == 0)
                {
                    // Nothing to link and nothing to refuse (D-14).
                    return false;
                }

                // 3. All active projects of the organization, in one statement, or none is linked.
                if (await projects.CountReachableAsync(organizationId, projectIds, ct) != projectIds.Count)
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                // 4. One statement for all the pairs; a pair already linked is left as it is.
                await links.LinkAsync(
                    organizationId,
                    projectIds.Select(id => (id, team.Id)).ToList(),
                    currentUser.SubjectId,
                    clock.UtcNow,
                    ct);

                return true;
            },
            cancellationToken);
    }
}
