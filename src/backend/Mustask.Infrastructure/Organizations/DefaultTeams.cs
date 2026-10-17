using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The project default team (mod-017 BR-OT-4, feat-028 BR-OL-7) read as it stands, in one statement: the named team when it is
/// still linked and active, the number of the project's linked active teams, and one of them. <see cref="ProjectDefaultTeam"/>
/// decides — the team the settings name, else the only linked active team, else none — so the structure seam, the creation's
/// lock and the team list cannot disagree.
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
                NamedActive = (
                        from link in context.ProjectTeams
                        join team in context.Teams on link.TeamId equals team.Id
                        where link.ProjectId == project.Id && team.Id == project.DefaultTeamId && team.Status == TeamStatus.Active
                        select (Guid?)team.Id)
                    .FirstOrDefault(),
                ActiveTeams = (
                        from link in context.ProjectTeams
                        join team in context.Teams on link.TeamId equals team.Id
                        where link.ProjectId == project.Id && team.Status == TeamStatus.Active
                        select team.Id)
                    .Count(),
                AnActive = (
                        from link in context.ProjectTeams
                        join team in context.Teams on link.TeamId equals team.Id
                        where link.ProjectId == project.Id && team.Status == TeamStatus.Active
                        select (Guid?)team.Id)
                    .FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : ProjectDefaultTeam.Resolve(row.NamedActive, row.ActiveTeams, row.AnActive);
    }
}
