using Mustask.Application.Features.Boards;
using Mustask.Application.Features.WorkItems;

namespace Mustask.Application.Tests.TestDoubles.Organizations;

/// <summary>
/// In-memory <see cref="IWorkItemTeamOwnership"/> (mod-017, feat-028): the number of items each team owns in each project, and every
/// call recorded in the organizations store's <see cref="InMemoryOrganizations.Calls"/> beside the team's own and the links' locks,
/// so a test can pin the order of ADR-0028 rule 8 (<c>lock owned</c>, <c>reassign owned</c>). A reassignment moves the counts to the
/// destination, as the database does.
/// </summary>
public sealed class FakeWorkItemTeamOwnership(InMemoryOrganizations store) : IWorkItemTeamOwnership
{
    /// <summary>The items each team owns in each project, in any state.</summary>
    public Dictionary<(Guid ProjectId, Guid TeamId), int> Owned { get; } = [];

    /// <summary>The items that passed to nobody («Без команды») by a reassignment.</summary>
    public int Unowned { get; private set; }

    /// <summary>The time the last reassignment showed.</summary>
    public DateTimeOffset? ReassignedAt { get; private set; }

    /// <summary>The destination of the last reassignment (<c>null</c> for «Без команды»).</summary>
    public Guid? LastDestination { get; private set; }

    public Task<int> CountOwnedAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        Task.FromResult(Owned.GetValueOrDefault((projectId, teamId)));

    public Task<IReadOnlyList<ProjectCount>> CountOwnedByProjectAsync(
        Guid teamId, IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectCount>>(
            projectIds.Where(projectId => Owned.GetValueOrDefault((projectId, teamId)) > 0)
                .Select(projectId => new ProjectCount(projectId, Owned[(projectId, teamId)]))
                .ToList());

    public Task LockOwnedAsync(IReadOnlyCollection<Guid> projectIds, Guid teamId, CancellationToken cancellationToken)
    {
        store.Record($"lock owned {teamId}");

        return Task.CompletedTask;
    }

    public Task<int> ReassignOwnedAsync(
        IReadOnlyCollection<Guid> projectIds, Guid teamId, Guid? destinationTeamId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        store.Record($"reassign owned {teamId} to {destinationTeamId?.ToString() ?? "none"}");
        ReassignedAt = now;
        LastDestination = destinationTeamId;

        var moved = 0;

        foreach (var projectId in projectIds)
        {
            if (!Owned.Remove((projectId, teamId), out var count))
            {
                continue;
            }

            moved += count;

            if (destinationTeamId is { } destination)
            {
                Owned[(projectId, destination)] = Owned.GetValueOrDefault((projectId, destination)) + count;
            }
            else
            {
                Unowned += count;
            }
        }

        return Task.FromResult(moved);
    }
}

/// <summary>
/// In-memory <see cref="ITeamBacklogPlaces"/> (mod-017, feat-028): every drop recorded in the store's calls
/// (<c>drop item places</c>, <c>drop team places</c>, <c>drop team places in</c>).
/// </summary>
public sealed class FakeTeamBacklogPlaces(InMemoryOrganizations store) : ITeamBacklogPlaces
{
    public Task DropForItemAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        store.Record($"drop item places {workItemId}");

        return Task.CompletedTask;
    }

    public Task DropForTeamAsync(Guid teamId, CancellationToken cancellationToken)
    {
        store.Record($"drop team places {teamId}");

        return Task.CompletedTask;
    }

    public Task DropForTeamInProjectAsync(Guid teamId, Guid projectId, CancellationToken cancellationToken)
    {
        store.Record($"drop team places in {projectId} {teamId}");

        return Task.CompletedTask;
    }
}
