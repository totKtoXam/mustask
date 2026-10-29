using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Tests.TestDoubles.Organizations;

/// <summary>
/// In-memory <see cref="IAreaRepository"/> over <see cref="InMemoryOrganizations"/>: every lock recorded (<c>lock area tree {org}</c>,
/// <c>lock area update|share {id}</c>, <c>lock areas share {ids}</c>, <c>lock project node {id}</c>) so a test can pin the lock order
/// of ADR-0031 (ADR-0028 rule 8 reworked). As the real lock statements do, a lock answers the rows it locked:
/// <see cref="VanishedAfterTheLock"/> names areas the store holds but a lock statement does not return.
/// </summary>
public sealed class FakeAreaRepository(InMemoryOrganizations store) : IAreaRepository
{
    /// <summary>Areas that exist in the store but are gone by the time a lock statement runs (a concurrent delete).</summary>
    public HashSet<Guid> VanishedAfterTheLock { get; } = [];

    public Task LockTreeAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        store.Record($"lock area tree {organizationId}");

        return Task.CompletedTask;
    }

    public Task<Area?> FindAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(organizationId, areaId));

    public Task<Area?> LockForUpdateAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken)
    {
        store.Record($"lock area update {areaId}");

        return Task.FromResult(VanishedAfterTheLock.Contains(areaId) ? null : Find(organizationId, areaId));
    }

    public Task<Area?> LockForShareAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken)
    {
        store.Record($"lock area share {areaId}");

        return Task.FromResult(VanishedAfterTheLock.Contains(areaId) ? null : Find(organizationId, areaId));
    }

    public Task<IReadOnlyDictionary<Guid, AreaType>> LockManyForShareAsync(
        Guid organizationId, IReadOnlyCollection<Guid> areaIds, CancellationToken cancellationToken)
    {
        var locked = areaIds
            .Distinct()
            .Where(areaId => !VanishedAfterTheLock.Contains(areaId) && Find(organizationId, areaId) is not null)
            .ToDictionary(areaId => areaId, areaId => Find(organizationId, areaId)!.Type);

        store.Record($"lock areas share {string.Join(",", areaIds.Distinct().Order())}");

        return Task.FromResult<IReadOnlyDictionary<Guid, AreaType>>(locked);
    }

    public Task<Area?> LockProjectNodeAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        store.Record($"lock project node {projectId}");

        var node = store.Areas.SingleOrDefault(area =>
            area.OrganizationId == organizationId && area.ProjectId == projectId && area.Type == AreaType.Project);

        return Task.FromResult(node is not null && VanishedAfterTheLock.Contains(node.Id) ? null : node);
    }

    public Task<bool> HasChildrenAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken) =>
        Task.FromResult(store.Areas.Any(area => area.OrganizationId == organizationId && area.ParentId == areaId));

    public Task<bool> IsSiblingNameTakenAsync(
        Guid organizationId, Guid? parentId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        Task.FromResult(store.Areas.Any(area =>
            area.OrganizationId == organizationId && area.ParentId == parentId && area.NormalizedName == normalizedName && area.Id != exceptId));

    public Task<IReadOnlyList<AreaNode>> GetNodesAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AreaNode>>(store
            .InDisplayOrder(store.Areas.Where(area => area.OrganizationId == organizationId), area => area.Name, area => area.Id)
            .Select(area => new AreaNode(area.Id, area.ParentId, area.Name, area.Type, area.ProjectId))
            .ToList());

    public void Add(Area area) => store.Stage(area);

    public void Remove(Area area) => store.Unstore(area);

    public Task SaveAsync(CancellationToken cancellationToken) => store.SaveAsync();

    private Area? Find(Guid organizationId, Guid areaId) =>
        store.Areas.SingleOrDefault(area => area.Id == areaId && area.OrganizationId == organizationId);
}

/// <summary>In-memory <see cref="IAreaLinkRepository"/> over <see cref="InMemoryOrganizations"/>; every call is recorded in the store calls.</summary>
public sealed class FakeAreaLinkRepository(InMemoryOrganizations store) : IAreaLinkRepository
{
    public Task<int> LinkTeamsAsync(
        Guid organizationId,
        IReadOnlyCollection<(Guid AreaId, Guid TeamId)> pairs,
        string linkedBy,
        DateTimeOffset linkedAt,
        CancellationToken cancellationToken)
    {
        store.Record($"link area teams {pairs.Count}");

        var made = 0;

        foreach (var (areaId, teamId) in pairs.Distinct().Where(pair => !store.AreaTeams.Any(link => link.AreaId == pair.AreaId && link.TeamId == pair.TeamId)))
        {
            store.AreaTeams.Add(AreaTeam.Link(organizationId, areaId, teamId, linkedBy, linkedAt));
            made++;
        }

        return Task.FromResult(made);
    }

    public Task RemoveTeamLinkAsync(Guid organizationId, Guid areaId, Guid teamId, CancellationToken cancellationToken)
    {
        store.Record($"remove area team link {areaId} {teamId}");
        store.AreaTeams.RemoveAll(link => link.AreaId == areaId && link.TeamId == teamId);

        return Task.CompletedTask;
    }

    public Task RemoveTeamLinksAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken)
    {
        store.Record($"remove area team links {teamId}");
        store.AreaTeams.RemoveAll(link => link.TeamId == teamId);

        return Task.CompletedTask;
    }

    public void Add(AreaTeam link) => store.Stage(link);

    public Task SaveAsync(CancellationToken cancellationToken) => store.SaveAsync();
}

/// <summary>In-memory <see cref="IAreaTreeReadModel"/> over <see cref="InMemoryOrganizations"/> (mod-018).</summary>
public sealed class FakeAreaTreeReadModel(InMemoryOrganizations store) : IAreaTreeReadModel
{
    public Task<IReadOnlyList<AreaTreeRow>> GetRowsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AreaTreeRow>>(store
            .InDisplayOrder(store.Areas.Where(area => area.OrganizationId == organizationId), area => area.Name, area => area.Id)
            .Select(area => new AreaTreeRow(
                area.Id,
                area.ParentId,
                area.Name,
                area.Description,
                area.Type,
                area.ProjectId,
                area.ProjectId is { } projectId ? store.Projects.Single(project => project.Id == projectId).IsActive : null,
                area.ProjectId is { } keyOf ? store.Projects.Single(project => project.Id == keyOf).Key : null))
            .ToList());

    public Task<IReadOnlyList<AreaTeamRow>> ListTeamsAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken)
    {
        var teams = store.AreaTeams
            .Where(link => link.OrganizationId == organizationId && link.AreaId == areaId)
            .Select(link => store.Teams.Single(team => team.Id == link.TeamId))
            .Where(team => team.IsActive);

        return Task.FromResult<IReadOnlyList<AreaTeamRow>>(store
            .InDisplayOrder(teams, team => team.Name, team => team.Id)
            .Select(team => new AreaTeamRow(team.Id, team.Name))
            .ToList());
    }

    public Task<IReadOnlyList<Guid>> ListAreaIdsOfTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(store.AreaTeams
            .Where(link => link.OrganizationId == organizationId && link.TeamId == teamId)
            .Select(link => link.AreaId)
            .ToList());
}
