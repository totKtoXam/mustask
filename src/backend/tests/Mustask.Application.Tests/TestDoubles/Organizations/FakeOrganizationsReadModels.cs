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

    private static ProjectRow Row(Project project) => new(project.Id, project.OrganizationId, project.Name, project.Key);
}

/// <summary>In-memory <see cref="ITeamReadModel"/> over <see cref="InMemoryOrganizations"/>.</summary>
public sealed class FakeTeamReadModel(InMemoryOrganizations store) : ITeamReadModel
{
    public Task<IReadOnlyList<TeamRow>> ListActiveAsync(Guid organizationId, Guid projectId, string userId, CancellationToken cancellationToken)
    {
        var teams = store.IsReachable(organizationId, projectId)
            ? store.Teams.Where(team => team.ProjectId == projectId && team.IsActive)
            : [];

        return Task.FromResult<IReadOnlyList<TeamRow>>(store
            .InDisplayOrder(teams, team => team.Name, team => team.Id)
            .Select(team => Row(team) with
            {
                IsMember = store.TeamMemberships.Any(membership => membership.TeamId == team.Id && membership.UserId == userId),
            })
            .ToList());
    }

    public Task<TeamRow?> FindReachableAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        Task.FromResult(
            store.IsReachable(organizationId, projectId)
            && store.Teams.SingleOrDefault(team => team.Id == teamId && team.ProjectId == projectId) is { IsActive: true } team
                ? Row(team)
                : null);

    /// <summary>The row with its default mark, decided by the one definition of the default team (mod-017 BR-OT-4).</summary>
    private TeamRow Row(Team team) => new(
        team.Id,
        team.ProjectId,
        team.Name,
        ProjectDefaultTeam.MarkOf(
            store.Projects.Any(project => project.Id == team.ProjectId && project.DefaultTeamId == team.Id),
            store.Teams.Count(other => other.ProjectId == team.ProjectId && other.IsActive)));
}

/// <summary>In-memory <see cref="IAreaTreeReadModel"/> over <see cref="InMemoryOrganizations"/>.</summary>
public sealed class FakeAreaTreeReadModel(InMemoryOrganizations store) : IAreaTreeReadModel
{
    public Task<IReadOnlyList<AreaTreeRow>?> GetNodesAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        if (!store.IsReachable(organizationId, projectId))
        {
            return Task.FromResult<IReadOnlyList<AreaTreeRow>?>(null);
        }

        return Task.FromResult<IReadOnlyList<AreaTreeRow>?>(store
            .InDisplayOrder(store.Areas.Where(area => area.ProjectId == projectId), area => area.Name, area => area.Id)
            .Select(area => new AreaTreeRow(area.Id, area.ParentId, area.Name))
            .ToList());
    }
}
