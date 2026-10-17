using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The project default team (mod-017 BR-OT-4) read as it stands, in one statement: the named team when it is active, the
/// number of the project's active teams, and one of them. <see cref="ProjectDefaultTeam"/> decides — the team the settings
/// name, else the only active team, else none — so the structure seam, the creation's lock and the team list cannot disagree.
/// </summary>
internal static class DefaultTeams
{
    /// <summary>The default team of the project now, or <c>null</c> when it has none (or the project does not exist).</summary>
    public static async Task<Guid?> ResolveAsync(MustaskDbContext context, Guid projectId, CancellationToken cancellationToken)
    {
        var row = await context.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new
            {
                NamedActive = context.Teams
                    .Where(team => team.Id == project.DefaultTeamId && team.ProjectId == project.Id && team.Status == TeamStatus.Active)
                    .Select(team => (Guid?)team.Id)
                    .FirstOrDefault(),
                ActiveTeams = context.Teams.Count(team => team.ProjectId == project.Id && team.Status == TeamStatus.Active),
                AnActive = context.Teams
                    .Where(team => team.ProjectId == project.Id && team.Status == TeamStatus.Active)
                    .Select(team => (Guid?)team.Id)
                    .FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : ProjectDefaultTeam.Resolve(row.NamedActive, row.ActiveTeams, row.AnActive);
    }
}
