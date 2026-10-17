using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Tests.TestDoubles.Organizations;

/// <summary>
/// In-memory <see cref="IOrganizationStructure"/> over <see cref="InMemoryOrganizations"/> (mod-017): the reads the work items and
/// the project default naming take of M1 — a team with its status and reachability, a project with its resolved default team.
/// Reachability is the specification of ADR-0026 rule 3, the same as <see cref="InMemoryOrganizations.IsReachable"/>.
/// </summary>
public sealed class FakeOrganizationStructure(InMemoryOrganizations store) : IOrganizationStructure
{
    public Task<OrganizationInfo?> FindOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(store.FindOrganization(organizationId) is { } organization
            ? new OrganizationInfo(organization.Id, organization.Name, organization.Status, organization.ProcessId, organization.CreatedBy, organization.CreatedAt)
            : null);

    public Task<ProjectInfo?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = store.Projects.SingleOrDefault(candidate => candidate.Id == projectId);

        if (project is null)
        {
            return Task.FromResult<ProjectInfo?>(null);
        }

        var active = store.Teams.Where(team => team.ProjectId == project.Id && team.IsActive).Select(team => team.Id).ToList();
        var root = store.Areas.Single(area => area.ProjectId == project.Id && area.IsRoot);

        return Task.FromResult<ProjectInfo?>(new ProjectInfo(
            project.Id,
            project.OrganizationId,
            project.Name,
            project.Key,
            project.Status,
            store.IsReachable(project.OrganizationId, project.Id),
            ProjectDefaultTeam.Resolve(project.DefaultTeamId, active),
            root.Id,
            project.CreatedBy,
            project.CreatedAt));
    }

    public Task<TeamInfo?> FindTeamAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var team = store.Teams.SingleOrDefault(candidate => candidate.Id == teamId);

        if (team is null)
        {
            return Task.FromResult<TeamInfo?>(null);
        }

        var project = store.Projects.Single(candidate => candidate.Id == team.ProjectId);

        return Task.FromResult<TeamInfo?>(new TeamInfo(
            team.Id,
            team.ProjectId,
            project.OrganizationId,
            team.Name,
            team.Status,
            team.IsActive && store.IsReachable(project.OrganizationId, project.Id)));
    }

    public async Task<IReadOnlyList<TeamInfo>> ListActiveTeamsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var teams = new List<TeamInfo>();

        foreach (var team in store.InDisplayOrder(store.Teams.Where(candidate => candidate.ProjectId == projectId && candidate.IsActive), team => team.Name, team => team.Id))
        {
            teams.Add((await FindTeamAsync(team.Id, cancellationToken))!);
        }

        return teams;
    }
}
