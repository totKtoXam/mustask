using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The reads M1 offers other modules (api-contract § 7, feat-028 api-contract § 6). By-id reads return the object with its
/// status, reachable or not (ADR-0026 rule 4), and <b>take the organization</b>: an object of another organization answers
/// <c>null</c> (BR-OL-1, mod-017 S-2), so no caller compares organizations itself. Reachability is computed through
/// <see cref="Reachability"/>, and the project default team through <see cref="ProjectDefaultTeam"/> (mod-017 BR-OT-4) over the
/// project's <b>links</b>. An area is read with its path (mod-018). Each read runs a fixed number of
/// queries.
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

    public async Task<ProjectInfo?> FindProjectAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        var project = await context.Projects
            .AsNoTracking()
            .Where(candidate => candidate.Id == projectId && candidate.OrganizationId == organizationId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.OrganizationId,
                candidate.Name,
                candidate.Key,
                candidate.Status,
                candidate.CreatedBy,
                candidate.CreatedAt,
                // mod-017 BR-OT-4, feat-028 BR-OL-7: the default team in one statement with the project — the named team when it
                // is still linked and active, the count of the project's linked active teams, and one of them (read only when it
                // is the only one).
                NamedActiveTeamId = (
                        from link in context.ProjectTeams
                        join team in context.Teams on link.TeamId equals team.Id
                        where link.ProjectId == candidate.Id && team.Id == candidate.DefaultTeamId && team.Status == TeamStatus.Active
                        select (Guid?)team.Id)
                    .FirstOrDefault(),
                ActiveTeams = (
                        from link in context.ProjectTeams
                        join team in context.Teams on link.TeamId equals team.Id
                        where link.ProjectId == candidate.Id && team.Status == TeamStatus.Active
                        select team.Id)
                    .Count(),
                AnActiveTeamId = (
                        from link in context.ProjectTeams
                        join team in context.Teams on link.TeamId equals team.Id
                        where link.ProjectId == candidate.Id && team.Status == TeamStatus.Active
                        select (Guid?)team.Id)
                    .FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            return null;
        }

        var isReachable = await context.ReachableProjects(project.OrganizationId)
            .AnyAsync(candidate => candidate.Id == project.Id, cancellationToken);

        return new ProjectInfo(
            project.Id,
            project.OrganizationId,
            project.Name,
            project.Key,
            project.Status,
            isReachable,
            ProjectDefaultTeam.Resolve(project.NamedActiveTeamId, project.ActiveTeams, project.AnActiveTeamId),
            project.CreatedBy,
            project.CreatedAt);
    }

    public async Task<TeamInfo?> FindTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await StoredTeams(context.Teams.Where(candidate => candidate.Id == teamId && candidate.OrganizationId == organizationId))
            .SingleOrDefaultAsync(cancellationToken);

        return team is null ? null : ToInfo(team);
    }

    public async Task<ProjectTeamInfo?> FindProjectTeamAsync(
        Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await context.Teams
            .AsNoTracking()
            .Where(candidate => candidate.Id == teamId && candidate.OrganizationId == organizationId)
            .Select(candidate => new
            {
                Stored = new StoredTeam(
                    candidate.Id,
                    candidate.OrganizationId,
                    candidate.Name,
                    candidate.Status,
                    context.Organizations.Any(organization => organization.Id == candidate.OrganizationId
                        && organization.Status == OrganizationStatus.Active)),
                // Both predicates carry the organization: a link across organizations cannot exist, and a project id of
                // another organization finds nothing (BR-OL-1).
                IsLinked = context.ProjectTeams.Any(link =>
                    link.OrganizationId == organizationId && link.ProjectId == projectId && link.TeamId == candidate.Id),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return team is null ? null : new ProjectTeamInfo(ToInfo(team.Stored), team.IsLinked);
    }

    public async Task<IReadOnlyList<TeamInfo>> ListLinkedTeamsAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        var teams = await StoredTeams(
                from link in context.ProjectTeams
                join team in context.Teams on link.TeamId equals team.Id
                where link.OrganizationId == organizationId && link.ProjectId == projectId && team.Status == TeamStatus.Active
                orderby EF.Functions.Collate(team.Name, OrganizationsCollation.Russian), team.Id
                select team)
            .ToListAsync(cancellationToken);

        return teams.Select(ToInfo).ToList();
    }

    public async Task<IReadOnlyList<LinkedProjectInfo>> ListLinkedProjectsAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        await (
                from link in context.ProjectTeams.AsNoTracking()
                join project in context.ReachableProjects(organizationId) on link.ProjectId equals project.Id
                where link.OrganizationId == organizationId && link.TeamId == teamId
                orderby EF.Functions.Collate(project.Name, OrganizationsCollation.Russian), project.Id
                select new LinkedProjectInfo(project.Id, project.Name, project.Key, true))
            .ToListAsync(cancellationToken);

    public async Task<AreaInfo?> FindAreaAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken)
    {
        var tree = await LoadTreeAsync(organizationId, cancellationToken);

        return tree.Rows.TryGetValue(areaId, out var row) ? ToInfo(organizationId, row, tree.Tree) : null;
    }

    public async Task<IReadOnlyList<AreaInfo>> ListAreasOfProjectAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        var ids = await context.AreaProjects
            .AsNoTracking()
            .Where(link => link.OrganizationId == organizationId && link.ProjectId == projectId)
            .Select(link => link.AreaId)
            .ToListAsync(cancellationToken);

        return await InPreOrderAsync(organizationId, ids, cancellationToken);
    }

    public async Task<IReadOnlyList<AreaInfo>> ListAreasOfTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken)
    {
        var ids = await context.AreaTeams
            .AsNoTracking()
            .Where(link => link.OrganizationId == organizationId && link.TeamId == teamId)
            .Select(link => link.AreaId)
            .ToListAsync(cancellationToken);

        return await InPreOrderAsync(organizationId, ids, cancellationToken);
    }

    public async Task<IReadOnlyList<LinkedProjectInfo>> ListLinkedProjectsOfAreaAsync(
        Guid organizationId, Guid areaId, bool includeDescendants, CancellationToken cancellationToken)
    {
        // One statement: the subtree by a recursive CTE (the area alone when descendants are not asked for), then each active project
        // once through its links to any of them. Every step carries the organization (ADR-0028 rule 4).
        var subtree = includeDescendants;

        return await context.Database
            .SqlQuery<LinkedProjectRow>(
                $"""
                 WITH RECURSIVE subtree (id) AS (
                     SELECT a.id FROM areas a WHERE a.organization_id = {organizationId} AND a.id = {areaId}
                     UNION ALL
                     SELECT c.id FROM areas c JOIN subtree s ON c.parent_id = s.id
                     WHERE {subtree} AND c.organization_id = {organizationId}
                 )
                 SELECT p.id AS "Id", p.name AS "Name", p.key AS "Key", TRUE AS "IsReachable"
                 FROM projects p
                 JOIN organizations o ON o.id = p.organization_id
                 WHERE p.organization_id = {organizationId}
                   AND p.status = 'Active'
                   AND o.status = 'Active'
                   AND EXISTS (
                       SELECT 1 FROM area_projects ap
                       WHERE ap.organization_id = {organizationId} AND ap.project_id = p.id AND ap.area_id IN (SELECT id FROM subtree))
                 ORDER BY p.name COLLATE "ru-x-icu", p.id
                 """)
            .Select(row => new LinkedProjectInfo(row.Id, row.Name, row.Key, true))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<AreaInfo>> InPreOrderAsync(Guid organizationId, List<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var loaded = await LoadTreeAsync(organizationId, cancellationToken);
        var wanted = ids.ToHashSet();

        return loaded.Tree.PreOrder()
            .Where(node => wanted.Contains(node.Id))
            .Select(node => ToInfo(organizationId, loaded.Rows[node.Id], loaded.Tree))
            .ToList();
    }

    private async Task<(Dictionary<Guid, AreaRow> Rows, AreaTree Tree)> LoadTreeAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var rows = await context.Areas
            .AsNoTracking()
            .Where(area => area.OrganizationId == organizationId)
            .OrderBy(area => EF.Functions.Collate(area.Name, OrganizationsCollation.Russian))
            .ThenBy(area => area.Id)
            .Select(area => new AreaRow(area.Id, area.ParentId, area.Name, area.Description))
            .ToListAsync(cancellationToken);

        return (rows.ToDictionary(row => row.Id), new AreaTree(rows.Select(row => new AreaNode(row.Id, row.ParentId, row.Name))));
    }

    private static AreaInfo ToInfo(Guid organizationId, AreaRow row, AreaTree tree) =>
        new(row.Id, organizationId, row.ParentId, row.Name, row.Description, tree.PathOf(row.Id));

    private sealed class LinkedProjectRow
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;

        public string Key { get; set; } = null!;

        public bool IsReachable { get; set; }
    }

    private sealed record AreaRow(Guid Id, Guid? ParentId, string Name, string? Description);

    private IQueryable<StoredTeam> StoredTeams(IQueryable<Team> teams) =>
        teams
            .AsNoTracking()
            .Select(team => new StoredTeam(
                team.Id,
                team.OrganizationId,
                team.Name,
                team.Status,
                context.Organizations.Any(organization => organization.Id == team.OrganizationId
                    && organization.Status == OrganizationStatus.Active)));

    private static TeamInfo ToInfo(StoredTeam team) => new(
        team.Id,
        team.OrganizationId,
        team.Name,
        team.Status,
        team.IsOrganizationActive && team.Status == TeamStatus.Active);

    private sealed record StoredTeam(Guid Id, Guid OrganizationId, string Name, TeamStatus Status, bool IsOrganizationActive);
}
