using Mustask.Application.Features.Boards;
using Mustask.Application.Features.WorkItems;

namespace Mustask.Application.Tests.TestDoubles.Organizations;

/// <summary>
/// In-memory <see cref="IWorkItemTeamOwnership"/> (mod-017): the number of items each team owns, and every call recorded in the
/// organizations store's <see cref="InMemoryOrganizations.Calls"/> beside the team's own locks, so a test can pin the order of
/// ADR-0028 rule 8 (<c>lock owned</c>, <c>release owned</c>).
/// </summary>
public sealed class FakeWorkItemTeamOwnership(InMemoryOrganizations store) : IWorkItemTeamOwnership
{
    /// <summary>The items each team owns, in any state.</summary>
    public Dictionary<Guid, int> Owned { get; } = [];

    /// <summary>The time the last release showed.</summary>
    public DateTimeOffset? ReleasedAt { get; private set; }

    public Task<int> CountOwnedAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        Task.FromResult(Owned.GetValueOrDefault(teamId));

    public Task LockOwnedAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken)
    {
        store.Record($"lock owned {teamId}");

        return Task.CompletedTask;
    }

    public Task<int> ReleaseOwnedAsync(Guid projectId, Guid teamId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        store.Record($"release owned {teamId}");
        ReleasedAt = now;

        return Task.FromResult(Owned.Remove(teamId, out var count) ? count : 0);
    }
}

/// <summary>In-memory <see cref="ITeamBacklogPlaces"/> (mod-017): every drop recorded in the store's calls (<c>drop item places</c>, <c>drop team places</c>).</summary>
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
}
