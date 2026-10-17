using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <inheritdoc cref="OrganizationReadModel"/>
internal sealed class TeamReadModel(MustaskDbContext context) : ITeamReadModel
{
    public async Task<IReadOnlyList<TeamRow>> ListActiveAsync(Guid organizationId, Guid projectId, string userId, CancellationToken cancellationToken)
    {
        // mod-016 BR-M1-27: whether the acting user is in each team comes with the list, as one EXISTS per row in the same
        // statement — one query whatever the number of teams. mod-017 BR-OT-4: so does the default mark (the named setting
        // and the count of the project's active teams, two subqueries of the one statement). BR-OT-11: no team is first,
        // the order is the name in Russian collation, letter case not deciding, then the id.
        var rows = await context.ReachableTeams(organizationId, projectId)
            .AsNoTracking()
            .OrderBy(team => EF.Functions.Collate(team.Name, OrganizationsCollation.Russian))
            .ThenBy(team => team.Id)
            .Select(team => new StoredTeam(
                team.Id,
                team.ProjectId,
                team.Name,
                context.Projects.Any(project => project.Id == team.ProjectId && project.DefaultTeamId == team.Id),
                context.Teams.Count(other => other.ProjectId == team.ProjectId && other.Status == TeamStatus.Active),
                context.TeamMemberships.Any(membership => membership.TeamId == team.Id && membership.UserId == userId)))
            .ToListAsync(cancellationToken);

        return rows.Select(ToRow).ToList();
    }

    public async Task<TeamRow?> FindReachableAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken)
    {
        var row = await context.ReachableTeams(organizationId, projectId)
            .AsNoTracking()
            .Where(team => team.Id == teamId)
            .Select(team => new StoredTeam(
                team.Id,
                team.ProjectId,
                team.Name,
                context.Projects.Any(project => project.Id == team.ProjectId && project.DefaultTeamId == team.Id),
                context.Teams.Count(other => other.ProjectId == team.ProjectId && other.Status == TeamStatus.Active),
                false))
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : ToRow(row);
    }

    private static TeamRow ToRow(StoredTeam team) => new(
        team.Id,
        team.ProjectId,
        team.Name,
        ProjectDefaultTeam.MarkOf(team.IsNamed, team.ActiveTeams),
        team.IsMember);

    private sealed record StoredTeam(Guid Id, Guid ProjectId, string Name, bool IsNamed, int ActiveTeams, bool IsMember);
}
