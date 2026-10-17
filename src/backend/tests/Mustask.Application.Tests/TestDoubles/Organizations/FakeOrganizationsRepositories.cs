using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Tests.TestDoubles.Organizations;

/// <summary>In-memory <see cref="IOrganizationRepository"/> over <see cref="InMemoryOrganizations"/>.</summary>
public sealed class FakeOrganizationRepository(InMemoryOrganizations store) : IOrganizationRepository
{
    public Task<string?> FindNameHolderAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        Task.FromResult(store.Organizations
            .Where(organization => organization.NormalizedName == normalizedName && organization.Id != exceptId)
            .Select(organization => organization.Name)
            .FirstOrDefault());

    public Task<Organization?> LockAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        store.Record($"lock organization {organizationId}");

        return Task.FromResult(store.FindOrganization(organizationId));
    }

    public void Add(Organization organization) => store.Stage(organization);

    public Task SaveAsync(CancellationToken cancellationToken) => store.SaveAsync();
}

/// <summary>In-memory <see cref="IProjectRepository"/> over <see cref="InMemoryOrganizations"/>.</summary>
public sealed class FakeProjectRepository(InMemoryOrganizations store) : IProjectRepository
{
    public Task<ProjectStatus?> FindNameHolderAsync(Guid organizationId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        Task.FromResult(store.Projects
            .Where(project => project.OrganizationId == organizationId && project.NormalizedName == normalizedName && project.Id != exceptId)
            .Select(project => (ProjectStatus?)project.Status)
            .FirstOrDefault());

    public Task<bool> IsKeyTakenAsync(Guid organizationId, string key, CancellationToken cancellationToken) =>
        Task.FromResult(store.Projects.Any(project => project.OrganizationId == organizationId && project.Key == key));

    public Task<bool> IsReachableAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult(store.IsReachable(organizationId, projectId));

    public Task<Project?> LockAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        store.Record($"lock project {projectId}");

        return Task.FromResult(store.FindProject(organizationId, projectId));
    }

    public Task<int> CountReachableAsync(Guid organizationId, IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
        Task.FromResult(projectIds.Distinct().Count(projectId => store.IsReachable(organizationId, projectId)));

    public Task ClearDefaultTeamAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken)
    {
        store.Record($"clear default team {teamId} in {projectId}");

        foreach (var project in store.Projects.Where(project => project.OrganizationId == organizationId && project.Id == projectId && project.DefaultTeamId == teamId))
        {
            project.NameDefaultTeam(null);
        }

        return Task.CompletedTask;
    }

    public Task ClearDefaultTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken)
    {
        store.Record($"clear default team {teamId}");

        foreach (var project in store.Projects.Where(project => project.OrganizationId == organizationId && project.DefaultTeamId == teamId))
        {
            project.NameDefaultTeam(null);
        }

        return Task.CompletedTask;
    }

    public void Add(NewProject project)
    {
        store.Stage(project.Project);
        store.Stage(project.Root);
    }

    public Task SaveAsync(CancellationToken cancellationToken) => store.SaveAsync();
}

/// <summary>In-memory <see cref="ITeamRepository"/> over <see cref="InMemoryOrganizations"/>.</summary>
public sealed class FakeTeamRepository(InMemoryOrganizations store) : ITeamRepository
{
    public Task<Team?> LockForNoKeyUpdateAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        LockAsync("no-key-update", organizationId, teamId);

    public Task<Team?> LockForShareAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        LockAsync("share", organizationId, teamId);

    public Task<IReadOnlyList<Team>> LockManyForShareAsync(
        Guid organizationId, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken)
    {
        var teams = store.Teams
            .Where(team => team.OrganizationId == organizationId && teamIds.Contains(team.Id))
            .OrderBy(team => team.Id)
            .ToList();

        store.Record($"lock teams share {string.Join(",", teams.Select(team => team.Id))}");

        return Task.FromResult<IReadOnlyList<Team>>(teams);
    }

    public Task<bool> IsActiveNameTakenAsync(Guid organizationId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        Task.FromResult(store.Teams.Any(team =>
            team.OrganizationId == organizationId && team.IsActive && team.NormalizedName == normalizedName && team.Id != exceptId));

    public Task<string?> FindNameAsync(Guid teamId, CancellationToken cancellationToken) =>
        Task.FromResult(store.Teams.Where(team => team.Id == teamId).Select(team => team.Name).FirstOrDefault());

    public void Add(Team team) => store.Stage(team);

    public Task SaveAsync(CancellationToken cancellationToken) => store.SaveAsync();

    private Task<Team?> LockAsync(string mode, Guid organizationId, Guid teamId)
    {
        store.Record($"lock team {mode} {teamId}");

        return Task.FromResult(store.Teams.SingleOrDefault(team => team.Id == teamId && team.OrganizationId == organizationId));
    }
}

/// <summary>
/// In-memory <see cref="IProjectTeamRepository"/> over <see cref="InMemoryOrganizations"/>: every lock recorded in the store's calls
/// (<c>lock links {projectId} {teamIds}</c>, <c>lock link share {projectId} {teamId}</c>, <c>lock team links {teamId}</c>) so a
/// test can pin ADR-0028 rule 8's order.
/// </summary>
public sealed class FakeProjectTeamRepository(InMemoryOrganizations store) : IProjectTeamRepository
{
    public Task LockLinksAsync(Guid organizationId, Guid projectId, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken)
    {
        store.Record($"lock links {projectId} {string.Join(",", teamIds.Distinct().Order())}");

        return Task.CompletedTask;
    }

    public Task<bool> LockLinkForShareAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken)
    {
        store.Record($"lock link share {projectId} {teamId}");

        return Task.FromResult(store.IsLinked(projectId, teamId));
    }

    public Task<IReadOnlyList<Guid>> LockTeamLinksAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken)
    {
        store.Record($"lock team links {teamId}");

        return Task.FromResult<IReadOnlyList<Guid>>(
            store.Links.Where(link => link.OrganizationId == organizationId && link.TeamId == teamId).Select(link => link.ProjectId).Order().ToList());
    }

    public Task<IReadOnlySet<Guid>> FindLinkedAsync(
        Guid organizationId, Guid projectId, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(
            store.Links.Where(link => link.ProjectId == projectId && teamIds.Contains(link.TeamId)).Select(link => link.TeamId).ToHashSet());

    public Task<int> LinkAsync(
        Guid organizationId,
        IReadOnlyCollection<(Guid ProjectId, Guid TeamId)> pairs,
        string linkedBy,
        DateTimeOffset linkedAt,
        CancellationToken cancellationToken)
    {
        store.Record($"link {pairs.Count}");

        var made = 0;

        foreach (var (projectId, teamId) in pairs.Distinct().Where(pair => !store.IsLinked(pair.ProjectId, pair.TeamId)))
        {
            store.Links.Add(ProjectTeam.Link(organizationId, projectId, teamId, linkedBy, linkedAt));
            made++;
        }

        return Task.FromResult(made);
    }

    public Task RemoveLinkAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken)
    {
        store.Record($"remove link {projectId} {teamId}");
        store.Links.RemoveAll(link => link.ProjectId == projectId && link.TeamId == teamId);

        return Task.CompletedTask;
    }

    public Task RemoveTeamLinksAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken)
    {
        store.Record($"remove team links {teamId}");
        store.Links.RemoveAll(link => link.TeamId == teamId);

        return Task.CompletedTask;
    }

    public void Add(ProjectTeam link) => store.Stage(link);

    public Task SaveAsync(CancellationToken cancellationToken) => store.SaveAsync();
}

/// <summary>In-memory <see cref="IAreaRepository"/> over <see cref="InMemoryOrganizations"/>.</summary>
public sealed class FakeAreaRepository(InMemoryOrganizations store) : IAreaRepository
{
    public Task<Area?> FindAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(projectId, areaId));

    public Task<Area?> LockForUpdateAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken)
    {
        store.Record($"lock area update {areaId}");

        return Task.FromResult(Find(projectId, areaId));
    }

    public Task<Area?> LockForShareAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken)
    {
        store.Record($"lock area share {areaId}");

        return Task.FromResult(Find(projectId, areaId));
    }

    public Task<Area> LockRootAsync(Guid projectId, bool forUpdate, CancellationToken cancellationToken)
    {
        store.Record($"lock root {(forUpdate ? "update" : "share")} {projectId}");

        return Task.FromResult(store.Areas.Single(area => area.ProjectId == projectId && area.IsRoot));
    }

    public Task<bool> HasChildrenAsync(Guid areaId, CancellationToken cancellationToken) =>
        Task.FromResult(store.Areas.Any(area => area.ParentId == areaId));

    public Task<bool> IsSiblingNameTakenAsync(Guid parentId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        Task.FromResult(store.Areas.Any(area => area.ParentId == parentId && area.NormalizedName == normalizedName && area.Id != exceptId));

    public Task<IReadOnlyList<AreaNode>> GetNodesAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AreaNode>>(store
            .InDisplayOrder(store.Areas.Where(area => area.ProjectId == projectId), area => area.Name, area => area.Id)
            .Select(area => new AreaNode(area.Id, area.ParentId, area.Name))
            .ToList());

    public void Add(Area area) => store.Stage(area);

    public void Remove(Area area) => store.Unstore(area);

    public Task SaveAsync(CancellationToken cancellationToken) => store.SaveAsync();

    private Area? Find(Guid projectId, Guid areaId) =>
        store.Areas.SingleOrDefault(area => area.Id == areaId && area.ProjectId == projectId);
}
