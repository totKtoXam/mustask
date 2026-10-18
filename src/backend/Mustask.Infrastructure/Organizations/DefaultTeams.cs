using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The project default team (mod-017 BR-OT-4, feat-028 BR-OL-7) read as it stands, in one statement, <b>over the links the caller
/// holds</b>: the named team when it is among them, still linked and active, the number of the held links' active teams, and one of
/// them. <see cref="ProjectDefaultTeam"/> decides — the team the settings name, else the only linked active team, else none — so the
/// creation's lock and the structure seam cannot disagree.
/// </summary>
internal static class DefaultTeams
{
    /// <summary>
    /// The default team of the project among <paramref name="heldTeamIds"/>, or <c>null</c> when it has none (or the project does not
    /// exist). Only a team whose link the caller holds <c>FOR SHARE</c> can come out (feat-028 review-code CR-4): a link committed
    /// after the lock is not among the held ones, so a creation never takes as its owner a team an unlink or a deletion can still
    /// remove without waiting for it. The creation is then ordered before that link, as if it had run a moment earlier.
    /// </summary>
    public static async Task<Guid?> ResolveAsync(
        MustaskDbContext context, Guid projectId, IReadOnlySet<Guid> heldTeamIds, CancellationToken cancellationToken)
    {
        if (heldTeamIds.Count == 0)
        {
            return null;
        }

        var held = heldTeamIds.ToArray();

        var row = await context.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new
            {
                NamedActive = (
                        from link in context.ProjectTeams
                        join team in context.Teams on link.TeamId equals team.Id
                        where link.ProjectId == project.Id && held.Contains(link.TeamId)
                            && team.Id == project.DefaultTeamId && team.Status == TeamStatus.Active
                        select (Guid?)team.Id)
                    .FirstOrDefault(),
                ActiveTeams = (
                        from link in context.ProjectTeams
                        join team in context.Teams on link.TeamId equals team.Id
                        where link.ProjectId == project.Id && held.Contains(link.TeamId) && team.Status == TeamStatus.Active
                        select team.Id)
                    .Count(),
                AnActive = (
                        from link in context.ProjectTeams
                        join team in context.Teams on link.TeamId equals team.Id
                        where link.ProjectId == project.Id && held.Contains(link.TeamId) && team.Status == TeamStatus.Active
                        select (Guid?)team.Id)
                    .FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : ProjectDefaultTeam.Resolve(row.NamedActive, row.ActiveTeams, row.AnActive);
    }
}
