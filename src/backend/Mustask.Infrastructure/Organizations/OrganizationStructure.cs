using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The reads M1 offers other modules (api-contract § 7). By-id reads return the
/// object with its status, reachable or not (ADR-0026 rule 4); reachability is
/// computed through <see cref="Reachability"/>, and the project default team through
/// <see cref="ProjectDefaultTeam"/> (mod-017 BR-OT-4). An area has no owner any more, so there is no read of one (BR-OT-10).
/// Each read runs a fixed number of queries.
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

    public async Task<ProjectInfo?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await context.Projects
            .AsNoTracking()
            .Where(candidate => candidate.Id == projectId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.OrganizationId,
                candidate.Name,
                candidate.Key,
                candidate.Status,
                candidate.CreatedBy,
                candidate.CreatedAt,
                // mod-017 BR-OT-4: the default team in one statement with the project — the named team when it is active, the
                // count of the project's active teams, and one of them (read only when it is the only one).
                NamedActiveTeamId = context.Teams
                    .Where(team => team.Id == candidate.DefaultTeamId && team.ProjectId == candidate.Id && team.Status == TeamStatus.Active)
                    .Select(team => (Guid?)team.Id)
                    .FirstOrDefault(),
                ActiveTeams = context.Teams.Count(team => team.ProjectId == candidate.Id && team.Status == TeamStatus.Active),
                AnActiveTeamId = context.Teams
                    .Where(team => team.ProjectId == candidate.Id && team.Status == TeamStatus.Active)
                    .Select(team => (Guid?)team.Id)
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

        var isReachable = await IsProjectReachableAsync(project.OrganizationId, project.Id, cancellationToken);

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

    public async Task<TeamInfo?> FindTeamAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var team = await Stored(context.Teams.Where(candidate => candidate.Id == teamId)).SingleOrDefaultAsync(cancellationToken);

        if (team is null)
        {
            return null;
        }

        var isReachable = await context.ReachableTeams(team.OrganizationId, team.ProjectId)
            .AnyAsync(candidate => candidate.Id == teamId, cancellationToken);

        return ToInfo(team, isReachable);
    }

    public async Task<IReadOnlyList<TeamInfo>> ListActiveTeamsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var organizationId = await context.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => (Guid?)project.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);

        if (organizationId is null)
        {
            return [];
        }

        var teams = await Stored(context.Teams
                .Where(team => team.ProjectId == projectId && team.Status == TeamStatus.Active)
                .OrderBy(team => EF.Functions.Collate(team.Name, OrganizationsCollation.Russian))
                .ThenBy(team => team.Id))
            .ToListAsync(cancellationToken);

        var isReachable = await IsProjectReachableAsync(organizationId.Value, projectId, cancellationToken);

        return teams.Select(team => ToInfo(team, isReachable)).ToList();
    }

    private Task<bool> IsProjectReachableAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        context.ReachableProjects(organizationId).AnyAsync(project => project.Id == projectId, cancellationToken);

    private IQueryable<StoredTeam> Stored(IQueryable<Team> teams) =>
        teams
            .AsNoTracking()
            .Select(team => new StoredTeam(
                team.Id,
                team.ProjectId,
                context.Projects.Where(project => project.Id == team.ProjectId).Select(project => project.OrganizationId).First(),
                team.Name,
                team.Status));

    private static TeamInfo ToInfo(StoredTeam team, bool isReachable) => new(
        team.Id,
        team.ProjectId,
        team.OrganizationId,
        team.Name,
        team.Status,
        isReachable && team.Status == TeamStatus.Active);

    private sealed record StoredTeam(
        Guid Id,
        Guid ProjectId,
        Guid OrganizationId,
        string Name,
        TeamStatus Status);
}
