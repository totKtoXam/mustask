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
        // statement — one query whatever the number of teams.
        var rows = await context.ReachableTeams(organizationId, projectId)
            .AsNoTracking()
            .OrderByDescending(team => team.IsFirstTeam)
            .ThenBy(team => EF.Functions.Collate(team.Name, OrganizationsCollation.Russian))
            .ThenBy(team => team.Id)
            .Select(team => new StoredTeam(
                team.Id,
                team.ProjectId,
                team.Name,
                team.IsFirstTeam,
                context.Areas.Where(area => area.DefaultOfTeamId == team.Id).Select(area => (Guid?)area.Id).FirstOrDefault(),
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
                team.IsFirstTeam,
                context.Areas.Where(area => area.DefaultOfTeamId == team.Id).Select(area => (Guid?)area.Id).FirstOrDefault(),
                false))
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : ToRow(row);
    }

    private static TeamRow ToRow(StoredTeam team) => new(
        team.Id,
        team.ProjectId,
        team.Name,
        team.IsFirstTeam,
        team.DefaultAreaId ?? throw new InvalidOperationException($"The active team {team.Id} has no default area (BR-M1-14)."),
        team.IsMember);

    private sealed record StoredTeam(Guid Id, Guid ProjectId, string Name, bool IsFirstTeam, Guid? DefaultAreaId, bool IsMember);
}
