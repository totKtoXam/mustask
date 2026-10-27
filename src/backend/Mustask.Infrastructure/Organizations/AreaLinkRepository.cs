using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>The write side of the area links (mod-018 BR-AW-6): one statement per operation; see <see cref="IAreaLinkRepository"/>.</summary>
internal sealed class AreaLinkRepository(MustaskDbContext context) : IAreaLinkRepository
{
    public Task<int> LinkProjectsAsync(
        Guid organizationId,
        IReadOnlyCollection<(Guid AreaId, Guid ProjectId)> pairs,
        string linkedBy,
        DateTimeOffset linkedAt,
        CancellationToken cancellationToken)
    {
        if (pairs.Count == 0)
        {
            return Task.FromResult(0);
        }

        var ordered = pairs.Distinct().OrderBy(pair => pair.AreaId).ThenBy(pair => pair.ProjectId).ToArray();
        var areaIds = ordered.Select(pair => pair.AreaId).ToArray();
        var projectIds = ordered.Select(pair => pair.ProjectId).ToArray();

        return context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO area_projects (organization_id, area_id, project_id, linked_by, linked_at)
             SELECT {organizationId}, p.area_id, p.project_id, {linkedBy}, {linkedAt}
             FROM unnest({areaIds}, {projectIds}) AS p (area_id, project_id)
             ON CONFLICT (area_id, project_id) DO NOTHING
             """,
            cancellationToken);
    }

    public Task<int> LinkTeamsAsync(
        Guid organizationId,
        IReadOnlyCollection<(Guid AreaId, Guid TeamId)> pairs,
        string linkedBy,
        DateTimeOffset linkedAt,
        CancellationToken cancellationToken)
    {
        if (pairs.Count == 0)
        {
            return Task.FromResult(0);
        }

        var ordered = pairs.Distinct().OrderBy(pair => pair.AreaId).ThenBy(pair => pair.TeamId).ToArray();
        var areaIds = ordered.Select(pair => pair.AreaId).ToArray();
        var teamIds = ordered.Select(pair => pair.TeamId).ToArray();

        return context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO area_teams (organization_id, area_id, team_id, linked_by, linked_at)
             SELECT {organizationId}, p.area_id, p.team_id, {linkedBy}, {linkedAt}
             FROM unnest({areaIds}, {teamIds}) AS p (area_id, team_id)
             ON CONFLICT (area_id, team_id) DO NOTHING
             """,
            cancellationToken);
    }

    public async Task RemoveProjectLinkAsync(Guid organizationId, Guid areaId, Guid projectId, CancellationToken cancellationToken) =>
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM area_projects WHERE organization_id = {organizationId} AND area_id = {areaId} AND project_id = {projectId}",
            cancellationToken);

    public async Task RemoveTeamLinkAsync(Guid organizationId, Guid areaId, Guid teamId, CancellationToken cancellationToken) =>
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM area_teams WHERE organization_id = {organizationId} AND area_id = {areaId} AND team_id = {teamId}",
            cancellationToken);

    public async Task RemoveTeamLinksAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM area_teams WHERE organization_id = {organizationId} AND team_id = {teamId}",
            cancellationToken);

    public void Add(AreaProject link) => context.AreaProjects.Add(link);

    public void Add(AreaTeam link) => context.AreaTeams.Add(link);

    public Task SaveAsync(CancellationToken cancellationToken) => OrganizationsSaveChanges.SaveAsync(context, cancellationToken);
}
