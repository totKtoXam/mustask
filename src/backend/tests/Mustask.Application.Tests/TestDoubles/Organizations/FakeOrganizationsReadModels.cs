using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Tests.TestDoubles.Organizations;

/// <summary>In-memory <see cref="IOrganizationReadModel"/> over <see cref="InMemoryOrganizations"/>.</summary>
public sealed class FakeOrganizationReadModel(InMemoryOrganizations store) : IOrganizationReadModel
{
    public Task<IReadOnlyList<OrganizationRow>> ListAsync(
        OrganizationStatus status, string userId, IReadOnlyCollection<OrganizationRole>? onlyRoles, CancellationToken cancellationToken)
    {
        var mine = store.Organizations.Where(organization =>
            organization.Status == status
            && store.Memberships.Any(membership =>
                membership.OrganizationId == organization.Id
                && membership.UserId == userId
                && (onlyRoles is null || onlyRoles.Contains(membership.Role))));

        return Task.FromResult<IReadOnlyList<OrganizationRow>>(store
            .InDisplayOrder(mine, organization => organization.Name, organization => organization.Id)
            .Select(organization => new OrganizationRow(organization.Id, organization.Name))
            .ToList());
    }

    public Task<OrganizationRow?> FindActiveAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(store.FindOrganization(organizationId) is { IsActive: true } organization
            ? new OrganizationRow(organization.Id, organization.Name)
            : null);
}

/// <summary>In-memory <see cref="IProjectReadModel"/> over <see cref="InMemoryOrganizations"/>.</summary>
public sealed class FakeProjectReadModel(InMemoryOrganizations store) : IProjectReadModel
{
    public Task<IReadOnlyList<ProjectRow>> ListAsync(Guid organizationId, ProjectStatus status, CancellationToken cancellationToken)
    {
        var projects = store.FindOrganization(organizationId) is { IsActive: true }
            ? store.Projects.Where(project => project.OrganizationId == organizationId && project.Status == status)
            : [];

        return Task.FromResult<IReadOnlyList<ProjectRow>>(store
            .InDisplayOrder(projects, project => project.Name, project => project.Id)
            .Select(Row)
            .ToList());
    }

    public Task<ProjectRow?> FindReachableAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult(store.IsReachable(organizationId, projectId) ? Row(store.FindProject(organizationId, projectId)!) : null);

    private static ProjectRow Row(Project project) => new(project.Id, project.OrganizationId, project.Name, project.Key, project.Description);
}

/// <summary>In-memory <see cref="ITeamReadModel"/> over <see cref="InMemoryOrganizations"/> (feat-028): teams of the organization and their links.</summary>
public sealed class FakeTeamReadModel(InMemoryOrganizations store) : ITeamReadModel
{
    public Task<IReadOnlyList<OrganizationTeamRow>> ListOrganizationTeamsAsync(Guid organizationId, string userId, CancellationToken cancellationToken)
    {
        var teams = ActiveTeams(organizationId);

        return Task.FromResult<IReadOnlyList<OrganizationTeamRow>>(store
            .InDisplayOrder(teams, team => team.Name, team => team.Id)
            .Select(team => new OrganizationTeamRow(
                team.Id,
                team.Name,
                team.Description,
                store.InDisplayOrder(ActiveProjectsOf(organizationId, team.Id), project => project.Name, project => project.Id)
                    .Select(project => new ProjectRef(project.Id, project.Name))
                    .ToList(),
                store.TeamMemberships.Any(membership => membership.TeamId == team.Id && membership.UserId == userId)))
            .ToList());
    }

    public Task<TeamRow?> FindAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        Task.FromResult(
            ActiveTeams(organizationId).SingleOrDefault(team => team.Id == teamId) is { } team
                ? new TeamRow(team.Id, team.Name, team.Description)
                : null);

    public Task<IReadOnlyList<LinkedTeamRow>> ListLinkedAsync(Guid organizationId, Guid projectId, string userId, CancellationToken cancellationToken)
    {
        var teams = LinkedActiveTeams(organizationId, projectId);

        return Task.FromResult<IReadOnlyList<LinkedTeamRow>>(store
            .InDisplayOrder(teams, team => team.Name, team => team.Id)
            .Select(team => Row(projectId, team) with
            {
                IsMember = store.TeamMemberships.Any(membership => membership.TeamId == team.Id && membership.UserId == userId),
            })
            .ToList());
    }

    public Task<LinkedTeamRow?> FindLinkedAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        Task.FromResult(
            LinkedActiveTeams(organizationId, projectId).SingleOrDefault(team => team.Id == teamId) is { } team
                ? Row(projectId, team)
                : null);

    public Task<IReadOnlyList<TeamProjectRow>> ListProjectsOfTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TeamProjectRow>>(store
            .InDisplayOrder(ActiveProjectsOf(organizationId, teamId), project => project.Name, project => project.Id)
            .Select(project => new TeamProjectRow(project.Id, project.Name, project.Key))
            .ToList());

    public Task<IReadOnlyList<TeamLinkRow>> ListLinksWithProjectsAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TeamLinkRow>>(store
            .InDisplayOrder(
                store.Links
                    .Where(link => link.OrganizationId == organizationId && link.TeamId == teamId)
                    .Select(link => store.Projects.Single(project => project.Id == link.ProjectId)),
                project => project.Name,
                project => project.Id)
            .Select(project => new TeamLinkRow(project.Id, project.Name, project.IsActive))
            .ToList());

    private IEnumerable<Team> ActiveTeams(Guid organizationId) =>
        store.FindOrganization(organizationId) is { IsActive: true }
            ? store.Teams.Where(team => team.OrganizationId == organizationId && team.IsActive)
            : [];

    private IEnumerable<Team> LinkedActiveTeams(Guid organizationId, Guid projectId) =>
        store.IsReachable(organizationId, projectId)
            ? ActiveTeams(organizationId).Where(team => store.IsLinked(projectId, team.Id))
            : [];

    private IEnumerable<Project> ActiveProjectsOf(Guid organizationId, Guid teamId) =>
        store.Links
            .Where(link => link.OrganizationId == organizationId && link.TeamId == teamId)
            .Select(link => link.ProjectId)
            .Where(projectId => store.IsReachable(organizationId, projectId))
            .Select(projectId => store.Projects.Single(project => project.Id == projectId));

    /// <summary>The row with its default mark, decided by the one definition of the default team (mod-017 BR-OT-4, feat-028 BR-OL-7).</summary>
    private LinkedTeamRow Row(Guid projectId, Team team) => new(
        team.Id,
        team.Name,
        ProjectDefaultTeam.MarkOf(
            store.Projects.Any(project => project.Id == projectId && project.DefaultTeamId == team.Id),
            LinkedActiveTeams(team.OrganizationId, projectId).Count()));
}
