using Mustask.Application.Abstractions.Iterations;
using Mustask.Domain.Iterations;

namespace Mustask.Application.Tests.TestDoubles.Iterations;

/// <summary>
/// The sprint store for handlers whose tests own no sprints (mod-007): the open-sprint lock of a team's unlink and deletion
/// (ADR-0028 rule 7) and an item save's sprint check. It records the teams whose open sprints were locked; a sprint id names
/// nothing. Sprint writers are tested against the database (Mustask.Infrastructure.Tests, Mustask.Api.Tests).
/// </summary>
public sealed class FakeSprintRepository : ISprintRepository
{
    public List<Guid> LockedOpenTeams { get; } = [];

    public Task LockOpenAsync(Guid teamId, CancellationToken cancellationToken)
    {
        LockedOpenTeams.Add(teamId);
        return Task.CompletedTask;
    }

    public Task LockHierarchiesAsync(Guid teamId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<SprintFacts?> LockForShareAsync(Guid organizationId, Guid sprintId, CancellationToken cancellationToken) =>
        Task.FromResult<SprintFacts?>(null);

    public Task<Sprint?> LockForUpdateAsync(Guid organizationId, Guid teamId, Guid sprintId, CancellationToken cancellationToken) =>
        Task.FromResult<Sprint?>(null);

    public Task<(Sprint? Sprint, SprintFacts? Destination)> LockPairForUpdateAsync(
        Guid organizationId, Guid teamId, Guid sprintId, Guid? destinationId, CancellationToken cancellationToken) =>
        Task.FromResult<(Sprint?, SprintFacts?)>((null, null));

    public Task<int> TakeNextNumberAsync(Guid teamId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<bool> IsNameTakenAsync(Guid teamId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> HasActiveAsync(Guid teamId, Guid exceptId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public void Add(Sprint sprint) => throw new NotSupportedException();

    public void Remove(Sprint sprint) => throw new NotSupportedException();

    public Task<int> LockItemsAsync(Guid sprintId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task ClearItemsAsync(Guid sprintId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task WriteSnapshotAsync(Guid sprintId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task MoveUnfinishedAsync(Guid sprintId, Guid? destinationId, DateTimeOffset now, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task SaveAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}
