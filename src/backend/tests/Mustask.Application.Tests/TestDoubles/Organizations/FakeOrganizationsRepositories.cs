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

    public void Add(NewProject project)
    {
        store.Stage(project.Project);
        store.Stage(project.FirstTeam);
        store.Stage(project.Root);
    }

    public Task SaveAsync(CancellationToken cancellationToken) => store.SaveAsync();
}

/// <summary>In-memory <see cref="ITeamRepository"/> over <see cref="InMemoryOrganizations"/>.</summary>
public sealed class FakeTeamRepository(InMemoryOrganizations store) : ITeamRepository
{
    public Task<Team?> LockForUpdateAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        LockAsync("update", projectId, teamId);

    public Task<Team?> LockForNoKeyUpdateAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        LockAsync("no-key-update", projectId, teamId);

    public Task<Team?> LockForShareAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        LockAsync("share", projectId, teamId);

    public Task<Team> GetFirstTeamAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult(store.Teams.Single(team => team.ProjectId == projectId && team.IsFirstTeam));

    public Task<bool> IsActiveNameTakenAsync(Guid projectId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        Task.FromResult(store.Teams.Any(team =>
            team.ProjectId == projectId && team.IsActive && team.NormalizedName == normalizedName && team.Id != exceptId));

    public Task<string?> FindNameAsync(Guid teamId, CancellationToken cancellationToken) =>
        Task.FromResult(store.Teams.Where(team => team.Id == teamId).Select(team => team.Name).FirstOrDefault());

    public Task ReassignExplicitAreasAsync(Guid fromTeamId, Guid toTeamId, CancellationToken cancellationToken)
    {
        store.Record($"reassign {fromTeamId} {toTeamId}");

        foreach (var area in store.Areas.Where(area => area.OwnerTeamId == fromTeamId))
        {
            area.ClearDefault();
            area.SetOwner(toTeamId, defaultTeamName: null);
        }

        return Task.CompletedTask;
    }

    public void Add(NewTeam team)
    {
        store.Stage(team.Team);
        store.Stage(team.Area);
    }

    public Task SaveAsync(CancellationToken cancellationToken) => store.SaveAsync();

    private Task<Team?> LockAsync(string mode, Guid projectId, Guid teamId)
    {
        store.Record($"lock team {mode} {teamId}");

        return Task.FromResult(store.Teams.SingleOrDefault(team => team.Id == teamId && team.ProjectId == projectId));
    }
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

    public Task<IReadOnlyList<Area>> LockManyForUpdateAsync(Guid projectId, IReadOnlyCollection<Guid> areaIds, CancellationToken cancellationToken)
    {
        var areas = store.Areas
            .Where(area => area.ProjectId == projectId && areaIds.Contains(area.Id))
            .OrderBy(area => area.Id)
            .ToList();

        foreach (var area in areas)
        {
            store.Record($"lock area update {area.Id}");
        }

        return Task.FromResult<IReadOnlyList<Area>>(areas);
    }

    public Task<Area> LockRootAsync(Guid projectId, bool forUpdate, CancellationToken cancellationToken)
    {
        store.Record($"lock root {(forUpdate ? "update" : "share")} {projectId}");

        return Task.FromResult(store.Areas.Single(area => area.ProjectId == projectId && area.IsRoot));
    }

    public Task<Area?> LockDefaultOfAsync(Guid teamId, CancellationToken cancellationToken)
    {
        store.Record($"lock default {teamId}");

        return Task.FromResult(store.Areas.SingleOrDefault(area => area.DefaultOfTeamId == teamId));
    }

    public Task<bool> HasChildrenAsync(Guid areaId, CancellationToken cancellationToken) =>
        Task.FromResult(store.Areas.Any(area => area.ParentId == areaId));

    public Task<bool> IsSiblingNameTakenAsync(Guid parentId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        Task.FromResult(store.Areas.Any(area => area.ParentId == parentId && area.NormalizedName == normalizedName && area.Id != exceptId));

    public Task<IReadOnlyList<AreaNode>> GetNodesAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AreaNode>>(store
            .InDisplayOrder(store.Areas.Where(area => area.ProjectId == projectId), area => area.Name, area => area.Id)
            .Select(area => new AreaNode(area.Id, area.ParentId, area.Name, area.OwnerTeamId, area.DefaultOfTeamId))
            .ToList());

    public void Add(Area area) => store.Stage(area);

    public void Remove(Area area) => store.Unstore(area);

    public Task SaveAsync(CancellationToken cancellationToken) => store.SaveAsync();

    private Area? Find(Guid projectId, Guid areaId) =>
        store.Areas.SingleOrDefault(area => area.Id == areaId && area.ProjectId == projectId);
}
