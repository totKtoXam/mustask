using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The reads M1 offers other modules (api-contract § 7, feat-028 api-contract § 6). By-id reads return the object with its
/// status, reachable or not (ADR-0026 rule 4), and <b>take the organization</b>: an object of another organization answers
/// <c>null</c> (BR-OL-1, mod-017 S-2), so no caller compares organizations itself. Reachability is computed through
/// <see cref="Reachability"/>, and the project default team through <see cref="ProjectDefaultTeam"/> (mod-017 BR-OT-4) over the
/// project's <b>links</b>. An area has no owner any more, so there is no read of one (BR-OT-10). Each read runs a fixed number of
/// queries.
/// </summary>
internal sealed class OrganizationStructure(MustaskDbContext context) : IOrganizationStructure
{
    public Task<OrganizationInfo?> FindOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        context.Organizations
            .AsNoTracking()
            .Where(organization => organization.Id == organizationId)
            .Select(organization => new OrganizationInfo(
                organization.Id,
                organization.Name,
                organization.Status,
                organization.ProcessId,
                organization.CreatedBy,
                organization.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ProjectInfo?> FindProjectAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        var project = await context.Projects
            .AsNoTracking()
            .Where(candidate => candidate.Id == projectId && candidate.OrganizationId == organizationId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.OrganizationId,
                candidate.Name,
                candidate.Key,
                candidate.Status,
                candidate.CreatedBy,
                candidate.CreatedAt,
                // mod-017 BR-OT-4, feat-028 BR-OL-7: the default team in one statement with the project — the named team when it
                // is still linked and active, the count of the project's linked active teams, and one of them (read only when it
                // is the only one).
                NamedActiveTeamId = (
                        from link in context.ProjectTeams
                        join team in context.Teams on link.TeamId equals team.Id
                        where link.ProjectId == candidate.Id && team.Id == candidate.DefaultTeamId && team.Status == TeamStatus.Active
                        select (Guid?)team.Id)
                    .FirstOrDefault(),
                ActiveTeams = (
                        from link in context.ProjectTeams
                        join team in context.Teams on link.TeamId equals team.Id
                        where link.ProjectId == candidate.Id && team.Status == TeamStatus.Active
                        select team.Id)
                    .Count(),
                AnActiveTeamId = (
                        from link in context.ProjectTeams
                        join team in context.Teams on link.TeamId equals team.Id
                        where link.ProjectId == candidate.Id && team.Status == TeamStatus.Active
                        select (Guid?)team.Id)
                    .FirstOrDefault(),
                RootAreaId = context.Areas
                    .Where(area => area.ProjectId == candidate.Id && area.ParentId == null)
                    .Select(area => area.Id)
                    .First(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            return null;
        }

        var isReachable = await context.ReachableProjects(project.OrganizationId)
            .AnyAsync(candidate => candidate.Id == project.Id, cancellationToken);

        return new ProjectInfo(
            project.Id,
            project.OrganizationId,
            project.Name,
            project.Key,
            project.Status,
            isReachable,
            ProjectDefaultTeam.Resolve(project.NamedActiveTeamId, project.ActiveTeams, project.AnActiveTeamId),
            project.RootAreaId,
            project.CreatedBy,
            project.CreatedAt);
    }

    public async Task<TeamInfo?> FindTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await StoredTeams(context.Teams.Where(candidate => candidate.Id == teamId && candidate.OrganizationId == organizationId))
            .SingleOrDefaultAsync(cancellationToken);

        return team is null ? null : ToInfo(team);
    }

    public async Task<ProjectTeamInfo?> FindProjectTeamAsync(
        Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await context.Teams
            .AsNoTracking()
            .Where(candidate => candidate.Id == teamId && candidate.OrganizationId == organizationId)
            .Select(candidate => new
            {
                Stored = new StoredTeam(
                    candidate.Id,
                    candidate.OrganizationId,
                    candidate.Name,
                    candidate.Status,
                    context.Organizations.Any(organization => organization.Id == candidate.OrganizationId
                        && organization.Status == OrganizationStatus.Active)),
                // Both predicates carry the organization: a link across organizations cannot exist, and a project id of
                // another organization finds nothing (BR-OL-1).
                IsLinked = context.ProjectTeams.Any(link =>
                    link.OrganizationId == organizationId && link.ProjectId == projectId && link.TeamId == candidate.Id),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return team is null ? null : new ProjectTeamInfo(ToInfo(team.Stored), team.IsLinked);
    }

    public async Task<IReadOnlyList<TeamInfo>> ListLinkedTeamsAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        var teams = await StoredTeams(
                from link in context.ProjectTeams
                join team in context.Teams on link.TeamId equals team.Id
                where link.OrganizationId == organizationId && link.ProjectId == projectId && team.Status == TeamStatus.Active
                orderby EF.Functions.Collate(team.Name, OrganizationsCollation.Russian), team.Id
                select team)
            .ToListAsync(cancellationToken);

        return teams.Select(ToInfo).ToList();
    }

    public async Task<IReadOnlyList<LinkedProjectInfo>> ListLinkedProjectsAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        await (
                from link in context.ProjectTeams.AsNoTracking()
                join project in context.ReachableProjects(organizationId) on link.ProjectId equals project.Id
                where link.OrganizationId == organizationId && link.TeamId == teamId
                orderby EF.Functions.Collate(project.Name, OrganizationsCollation.Russian), project.Id
                select new LinkedProjectInfo(project.Id, project.Name, project.Key, true))
            .ToListAsync(cancellationToken);

    private IQueryable<StoredTeam> StoredTeams(IQueryable<Team> teams) =>
        teams
            .AsNoTracking()
            .Select(team => new StoredTeam(
                team.Id,
                team.OrganizationId,
                team.Name,
                team.Status,
                context.Organizations.Any(organization => organization.Id == team.OrganizationId
                    && organization.Status == OrganizationStatus.Active)));

    private static TeamInfo ToInfo(StoredTeam team) => new(
        team.Id,
        team.OrganizationId,
        team.Name,
        team.Status,
        team.IsOrganizationActive && team.Status == TeamStatus.Active);

    private sealed record StoredTeam(Guid Id, Guid OrganizationId, string Name, TeamStatus Status, bool IsOrganizationActive);
}
