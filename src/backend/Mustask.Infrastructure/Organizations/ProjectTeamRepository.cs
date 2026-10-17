using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <inheritdoc cref="OrganizationRepository"/>
/// <remarks>
/// The links (feat-028 BR-OL-4) are ADR-0028 rule 8's step 3. Every multi-row lock is <b>one statement ordered before it locks</b>
/// (<c>ORDER BY … FOR …</c> orders first), in (project id, team id) order — see <see cref="IProjectTeamRepository"/>. Every statement
/// carries the organization, so an id of another organization locks and changes nothing (BR-OL-1). The set statements are
/// parameterized with arrays (<c>= ANY(@ids)</c>, <c>unnest</c>), so their number does not grow with the number of ids.
/// </remarks>
internal sealed class ProjectTeamRepository(MustaskDbContext context) : IProjectTeamRepository
{
    public async Task LockLinksAsync(Guid organizationId, Guid projectId, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken)
    {
        if (teamIds.Count == 0)
        {
            return;
        }

        var ids = teamIds.Distinct().ToArray();

        // One statement ordered by team id: the pair of an unlink (the unlinked team and its destination) is taken in one order
        // whichever side an unlink names first (plan-backend § 7.3 C-6).
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             SELECT 1
             FROM project_teams
             WHERE organization_id = {organizationId} AND project_id = {projectId} AND team_id = ANY({ids})
             ORDER BY team_id
             FOR NO KEY UPDATE
             """,
            cancellationToken);
    }

    public async Task<bool> LockLinkForShareAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             SELECT 1
             FROM project_teams
             WHERE organization_id = {organizationId} AND project_id = {projectId} AND team_id = {teamId}
             FOR SHARE
             """,
            cancellationToken);

        // A new statement under READ COMMITTED: a link an unlink or a deletion removed while the lock waited is gone.
        return await context.ProjectTeams.AnyAsync(
            link => link.OrganizationId == organizationId && link.ProjectId == projectId && link.TeamId == teamId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> LockTeamLinksAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken)
    {
        // One statement ordered by project id: the deletion's order, the mirror of the unlink's (by team id) within one project.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             SELECT 1
             FROM project_teams
             WHERE organization_id = {organizationId} AND team_id = {teamId}
             ORDER BY project_id
             FOR NO KEY UPDATE
             """,
            cancellationToken);

        // A new statement: the links as the previous lock holder left them, now held by this transaction.
        return await context.ProjectTeams
            .AsNoTracking()
            .Where(link => link.OrganizationId == organizationId && link.TeamId == teamId)
            .OrderBy(link => link.ProjectId)
            .Select(link => link.ProjectId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> FindLinkedAsync(
        Guid organizationId, Guid projectId, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken)
    {
        if (teamIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ids = teamIds.Distinct().ToArray();

        var linked = await context.ProjectTeams
            .AsNoTracking()
            .Where(link => link.OrganizationId == organizationId && link.ProjectId == projectId && ids.Contains(link.TeamId))
            .Select(link => link.TeamId)
            .ToListAsync(cancellationToken);

        return linked.ToHashSet();
    }

    public async Task<int> LinkAsync(
        Guid organizationId,
        IReadOnlyCollection<(Guid ProjectId, Guid TeamId)> pairs,
        string linkedBy,
        DateTimeOffset linkedAt,
        CancellationToken cancellationToken)
    {
        var distinct = pairs.Distinct().ToArray();

        if (distinct.Length == 0)
        {
            return 0;
        }

        var projectIds = distinct.Select(pair => pair.ProjectId).ToArray();
        var teamIds = distinct.Select(pair => pair.TeamId).ToArray();

        // One statement whatever the number of pairs; a pair already linked keeps its row, who made it and when (BR-OL-4), and two
        // requests that link one unlinked pair end with one row: the loser waits on the winner's uncommitted key, then does nothing.
        return await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO project_teams (organization_id, project_id, team_id, linked_by, linked_at)
             SELECT {organizationId}, pair.project_id, pair.team_id, {linkedBy}, {linkedAt}
             FROM unnest({projectIds}, {teamIds}) AS pair(project_id, team_id)
             ORDER BY pair.project_id, pair.team_id
             ON CONFLICT (project_id, team_id) DO NOTHING
             """,
            cancellationToken);
    }

    public Task RemoveLinkAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM project_teams WHERE organization_id = {organizationId} AND project_id = {projectId} AND team_id = {teamId}",
            cancellationToken);

    public Task RemoveTeamLinksAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM project_teams WHERE organization_id = {organizationId} AND team_id = {teamId}",
            cancellationToken);

    public void Add(ProjectTeam link) => context.ProjectTeams.Add(link);

    public Task SaveAsync(CancellationToken cancellationToken) => OrganizationsSaveChanges.SaveAsync(context, cancellationToken);
}
