using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <inheritdoc cref="OrganizationReadModel"/>
internal sealed class TeamReadModel(MustaskDbContext context) : ITeamReadModel
{
    public async Task<IReadOnlyList<OrganizationTeamRow>> ListOrganizationTeamsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        // Statement 1: the organization's active teams in name order (Russian collation, letter case not deciding, then id).
        var teams = await context.ActiveTeamsOf(organizationId)
            .AsNoTracking()
            .OrderBy(team => EF.Functions.Collate(team.Name, OrganizationsCollation.Russian))
            .ThenBy(team => team.Id)
            .Select(team => new { team.Id, team.Name, team.Description })
            .ToListAsync(cancellationToken);

        if (teams.Count == 0)
        {
            return [];
        }

        // Statement 2: every link of those teams to an active project, in the projects' name order. Grouped in memory by team:
        // assembling the rows already read, not reading extra ones (backend.md, "Filtering happens before materialization").
        var links = await (
                from link in context.ProjectTeams.AsNoTracking()
                join project in context.ReachableProjects(organizationId) on link.ProjectId equals project.Id
                where link.OrganizationId == organizationId
                    && context.ActiveTeamsOf(organizationId).Any(team => team.Id == link.TeamId)
                orderby EF.Functions.Collate(project.Name, OrganizationsCollation.Russian), project.Id
                select new { link.TeamId, Project = new ProjectRef(project.Id, project.Name) })
            .ToListAsync(cancellationToken);

        var byTeam = links.ToLookup(link => link.TeamId, link => link.Project);

        return teams
            .Select(team => new OrganizationTeamRow(team.Id, team.Name, team.Description, byTeam[team.Id].ToList()))
            .ToList();
    }

    public Task<TeamRow?> FindAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        context.ActiveTeamsOf(organizationId)
            .AsNoTracking()
            .Where(team => team.Id == teamId)
            .Select(team => new TeamRow(team.Id, team.Name, team.Description))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<LinkedTeamRow>> ListLinkedAsync(
        Guid organizationId, Guid projectId, string userId, CancellationToken cancellationToken)
    {
        // mod-016 BR-M1-27: whether the acting user is in each team comes with the list, as one EXISTS per row in the same
        // statement — one query whatever the number of teams. mod-017 BR-OT-4, feat-028 BR-OL-7: so does the default mark (the
        // named setting and the count of the project's linked active teams, two subqueries of the one statement). The order is the
        // name in Russian collation, letter case not deciding, then the id; no team comes first.
        var rows = await context.LinkedTeams(organizationId, projectId)
            .AsNoTracking()
            .OrderBy(team => EF.Functions.Collate(team.Name, OrganizationsCollation.Russian))
            .ThenBy(team => team.Id)
            .Select(team => new StoredTeam(
                team.Id,
                team.Name,
                context.Projects.Any(project => project.Id == projectId && project.DefaultTeamId == team.Id),
                context.LinkedTeams(organizationId, projectId).Count(),
                context.TeamMemberships.Any(membership => membership.TeamId == team.Id && membership.UserId == userId)))
            .ToListAsync(cancellationToken);

        return rows.Select(ToRow).ToList();
    }

    public async Task<LinkedTeamRow?> FindLinkedAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken)
    {
        var row = await context.LinkedTeams(organizationId, projectId)
            .AsNoTracking()
            .Where(team => team.Id == teamId)
            .Select(team => new StoredTeam(
                team.Id,
                team.Name,
                context.Projects.Any(project => project.Id == projectId && project.DefaultTeamId == team.Id),
                context.LinkedTeams(organizationId, projectId).Count(),
                false))
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : ToRow(row);
    }

    public async Task<IReadOnlyList<TeamProjectRow>> ListProjectsOfTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        await context.LinkedProjects(organizationId, teamId)
            .AsNoTracking()
            .OrderBy(project => EF.Functions.Collate(project.Name, OrganizationsCollation.Russian))
            .ThenBy(project => project.Id)
            .Select(project => new TeamProjectRow(project.Id, project.Name, project.Key))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TeamLinkRow>> ListLinksWithProjectsAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        await (
                from link in context.ProjectTeams.AsNoTracking()
                join project in context.Projects on link.ProjectId equals project.Id
                where link.OrganizationId == organizationId && link.TeamId == teamId
                orderby EF.Functions.Collate(project.Name, OrganizationsCollation.Russian), project.Id
                select new TeamLinkRow(project.Id, project.Name, project.Status == ProjectStatus.Active))
            .ToListAsync(cancellationToken);

    private static LinkedTeamRow ToRow(StoredTeam team) => new(
        team.Id,
        team.Name,
        ProjectDefaultTeam.MarkOf(team.IsNamed, team.LinkedActiveTeams),
        team.IsMember);

    private sealed record StoredTeam(Guid Id, string Name, bool IsNamed, int LinkedActiveTeams, bool IsMember);
}
