using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Tests.TestDoubles.Organizations;

/// <summary>
/// In-memory <see cref="IOrganizationStructure"/> over <see cref="InMemoryOrganizations"/> (mod-017, feat-028, mod-018): the reads the
/// work items, the boards, the areas and the project default naming take of M1 — a team with its status, reachability and link, a
/// project with its resolved default team, an area with its path. Every by-id read takes the organization and answers <c>null</c> for
/// an object of another one, as the real one does (BR-OL-1). Reachability is the specification of ADR-0026 rule 3, the same as
/// <see cref="InMemoryOrganizations.IsReachable"/>.
/// </summary>
public sealed class FakeOrganizationStructure(InMemoryOrganizations store) : IOrganizationStructure
{
    public Task<OrganizationInfo?> FindOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(store.FindOrganization(organizationId) is { } organization
            ? new OrganizationInfo(organization.Id, organization.Name, organization.Status, organization.ProcessId, organization.CreatedBy, organization.CreatedAt)
            : null);

    public Task<ProjectInfo?> FindProjectAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        var project = store.Projects.SingleOrDefault(candidate => candidate.Id == projectId && candidate.OrganizationId == organizationId);

        if (project is null)
        {
            return Task.FromResult<ProjectInfo?>(null);
        }

        var active = store.Links
            .Where(link => link.ProjectId == project.Id)
            .Select(link => store.Teams.Single(team => team.Id == link.TeamId))
            .Where(team => team.IsActive)
            .Select(team => team.Id)
            .ToList();

        return Task.FromResult<ProjectInfo?>(new ProjectInfo(
            project.Id,
            project.OrganizationId,
            project.Name,
            project.Key,
            project.Status,
            store.IsReachable(project.OrganizationId, project.Id),
            ProjectDefaultTeam.Resolve(project.DefaultTeamId, active),
            project.CreatedBy,
            project.CreatedAt));
    }

    public Task<TeamInfo?> FindTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken)
    {
        var team = store.Teams.SingleOrDefault(candidate => candidate.Id == teamId && candidate.OrganizationId == organizationId);

        return Task.FromResult(team is null ? null : Info(team));
    }

    public async Task<ProjectTeamInfo?> FindProjectTeamAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        await FindTeamAsync(organizationId, teamId, cancellationToken) is { } team
            ? new ProjectTeamInfo(team, store.Links.Any(link => link.OrganizationId == organizationId && link.ProjectId == projectId && link.TeamId == teamId))
            : null;

    public Task<IReadOnlyList<TeamInfo>> ListLinkedTeamsAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        var teams = store.Links
            .Where(link => link.OrganizationId == organizationId && link.ProjectId == projectId)
            .Select(link => store.Teams.Single(team => team.Id == link.TeamId))
            .Where(team => team.IsActive);

        return Task.FromResult<IReadOnlyList<TeamInfo>>(store.InDisplayOrder(teams, team => team.Name, team => team.Id).Select(Info).ToList());
    }

    public Task<IReadOnlyList<LinkedProjectInfo>> ListLinkedProjectsAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken)
    {
        var projects = store.Links
            .Where(link => link.OrganizationId == organizationId && link.TeamId == teamId)
            .Select(link => store.Projects.Single(project => project.Id == link.ProjectId))
            .Where(project => store.IsReachable(organizationId, project.Id));

        return Task.FromResult<IReadOnlyList<LinkedProjectInfo>>(store
            .InDisplayOrder(projects, project => project.Name, project => project.Id)
            .Select(project => new LinkedProjectInfo(project.Id, project.Name, project.Key, true))
            .ToList());
    }

    public Task<AreaInfo?> FindAreaAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken)
    {
        var area = store.Areas.SingleOrDefault(candidate => candidate.Id == areaId && candidate.OrganizationId == organizationId);

        return Task.FromResult(area is null ? null : InfoOf(area));
    }

    public Task<IReadOnlyList<AreaInfo>> ListAreasOfProjectAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AreaInfo>>(Ordered(store.AreaProjects
            .Where(link => link.OrganizationId == organizationId && link.ProjectId == projectId)
            .Select(link => link.AreaId)
            .ToList()));

    public Task<IReadOnlyList<AreaInfo>> ListAreasOfTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AreaInfo>>(Ordered(store.AreaTeams
            .Where(link => link.OrganizationId == organizationId && link.TeamId == teamId)
            .Select(link => link.AreaId)
            .ToList()));

    public Task<IReadOnlyList<LinkedProjectInfo>> ListLinkedProjectsOfAreaAsync(
        Guid organizationId, Guid areaId, bool includeDescendants, CancellationToken cancellationToken)
    {
        var tree = TreeOf(organizationId);

        if (!tree.Contains(areaId))
        {
            return Task.FromResult<IReadOnlyList<LinkedProjectInfo>>([]);
        }

        var projects = store.AreaProjects
            .Where(link => link.OrganizationId == organizationId
                && (link.AreaId == areaId || (includeDescendants && tree.IsSelfOrDescendant(areaId, link.AreaId))))
            .Select(link => link.ProjectId)
            .Distinct()
            .Select(projectId => store.Projects.Single(project => project.Id == projectId));

        return Task.FromResult<IReadOnlyList<LinkedProjectInfo>>(store
            .InDisplayOrder(projects, project => project.Name, project => project.Id)
            .Select(project => new LinkedProjectInfo(project.Id, project.Name, project.Key, store.IsReachable(organizationId, project.Id)))
            .ToList());
    }

    private AreaTree TreeOf(Guid organizationId) =>
        new(store.Areas.Where(area => area.OrganizationId == organizationId).Select(area => new AreaNode(area.Id, area.ParentId, area.Name)));

    private List<AreaInfo> Ordered(IReadOnlyCollection<Guid> areaIds) =>
        store.InDisplayOrder(store.Areas.Where(area => areaIds.Contains(area.Id)), area => area.Name, area => area.Id)
            .Select(InfoOf)
            .ToList();

    private AreaInfo InfoOf(Area area) =>
        new(area.Id, area.OrganizationId, area.ParentId, area.Name, area.Description, TreeOf(area.OrganizationId).PathOf(area.Id));

    private TeamInfo Info(Team team) => new(
        team.Id,
        team.OrganizationId,
        team.Name,
        team.Status,
        team.IsActive && store.FindOrganization(team.OrganizationId) is { IsActive: true });
}
