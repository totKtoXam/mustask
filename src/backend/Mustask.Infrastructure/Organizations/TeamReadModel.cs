using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <inheritdoc cref="OrganizationReadModel"/>
internal sealed class TeamReadModel(MustaskDbContext context) : ITeamReadModel
{
    public async Task<IReadOnlyList<TeamRow>> ListActiveAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        var rows = await Project(context.ReachableTeams(organizationId, projectId)
                .AsNoTracking()
                .OrderByDescending(team => team.IsFirstTeam)
                .ThenBy(team => EF.Functions.Collate(team.Name, OrganizationsCollation.Russian))
                .ThenBy(team => team.Id))
            .ToListAsync(cancellationToken);

        return rows.Select(ToRow).ToList();
    }

    public async Task<TeamRow?> FindReachableAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken)
    {
        var row = await Project(context.ReachableTeams(organizationId, projectId)
                .AsNoTracking()
                .Where(team => team.Id == teamId))
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : ToRow(row);
    }

    private IQueryable<StoredTeam> Project(IQueryable<Team> teams) =>
        teams.Select(team => new StoredTeam(
            team.Id,
            team.ProjectId,
            team.Name,
            team.IsFirstTeam,
            context.Areas.Where(area => area.DefaultOfTeamId == team.Id).Select(area => (Guid?)area.Id).FirstOrDefault()));

    private static TeamRow ToRow(StoredTeam team) => new(
        team.Id,
        team.ProjectId,
        team.Name,
        team.IsFirstTeam,
        team.DefaultAreaId ?? throw new InvalidOperationException($"The active team {team.Id} has no default area (BR-M1-14)."));

    private sealed record StoredTeam(Guid Id, Guid ProjectId, string Name, bool IsFirstTeam, Guid? DefaultAreaId);
}
