using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.WorkItems.PurgeWorkItem;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.WorkItems.PurgeWorkItem;

/// <summary>
/// mod-005 US-6 · the purge's handler and the step contract, with fake steps, a fake repository and a unit of work that runs the
/// action (ADR-0030 rule 5 and 7): the order of the calls is the contract — pre-flights outside the transaction, then the
/// hierarchy lock, the item and its children, the steps, the team place, the children's link, the removal, and only then the
/// log line. The effect of the locks is the integration tests' (<c>DeleteRestorePurgeRaceTests</c>).
/// </summary>
public sealed class PurgeWorkItemHandlerTests
{
    private readonly WorkItemWorld _world = new();
    private readonly FakeClock _clock = new() { UtcNow = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero) };
    private readonly FakeCurrentUser _user = new() { SubjectId = "admin-sub" };

    /// <summary>A transaction that records where it begins and where it commits, around the action.</summary>
    private sealed class RecordingUnitOfWork(WorkItemWorld world) : Mustask.Application.Abstractions.IUnitOfWork
    {
        public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken cancellationToken)
        {
            world.Calls.Add("begin");
            var result = await action(cancellationToken);
            world.Calls.Add("commit");

            return result;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private (PurgeWorkItemHandler Handler, FakeWorkItemRepository Repository, FakeWorkItemReadModel ReadModel, ListLogger<PurgeWorkItemHandler> Logger) Build(
        params IWorkItemPurgeStep[] steps)
    {
        var repository = new FakeWorkItemRepository(_world);
        var readModel = new FakeWorkItemReadModel(_world);
        var logger = _world.Logger<PurgeWorkItemHandler>();
        var handler = new PurgeWorkItemHandler(
            _world.Structure,
            _world.Processes,
            repository,
            readModel,
            new WorldTeamPlaces(_world),
            steps,
            _user,
            _clock,
            new RecordingUnitOfWork(_world),
            logger);

        return (handler, repository, readModel, logger);
    }

    private PurgeWorkItemCommand Command(string identifier = "MSK-US-12") =>
        new(_world.OrganizationId.ToString(), _world.ProjectId.ToString(), identifier);

    /// <summary>A deleted story: the head the unlocked read answers and the row the locks find.</summary>
    private (WorkItem Item, FakeWorkItemRepository Repository, PurgeWorkItemHandler Handler, RecordingPurgeStep[] Steps) DeletedWithSteps(int stepCount)
    {
        var item = _world.DeletedStory();
        var steps = Enumerable.Range(1, stepCount).Select(index => new RecordingPurgeStep(_world, $"step-{index}")).ToArray();
        var (handler, repository, readModel, _) = Build(steps);

        repository.Locked = item;
        readModel.Head = new WorkItemHead(item.Id, item.TypeId, item.StateId);

        return (item, repository, handler, steps);
    }

    // US-6 · "The step contract end to end": pre-flights in registration order before the transaction begins, the steps inside it,
    // in registration order, and every step receives the children's ids with the identifier and the actor, never a title.
    [Fact]
    public async Task Handle_RunsPreflightsBeforeTheTransaction_ThenStepsInsideIt_InRegistrationOrder_WithTheChildIds()
    {
        var (item, repository, handler, steps) = DeletedWithSteps(2);
        var children = new[] { Guid.CreateVersion7(), Guid.CreateVersion7() };
        repository.ChildIds = children;

        await handler.Handle(Command(), CancellationToken.None);

        var calls = _world.Calls;
        Assert.True(calls.IndexOf("preflight step-1") < calls.IndexOf("preflight step-2"));
        Assert.True(calls.IndexOf("preflight step-2") < calls.IndexOf("begin"));
        Assert.True(calls.IndexOf("begin") < calls.IndexOf("step step-1"));
        Assert.True(calls.IndexOf("step step-1") < calls.IndexOf("step step-2"));
        Assert.True(calls.IndexOf("step step-2") < calls.IndexOf("remove"));

        foreach (var step in steps)
        {
            Assert.Equal(children, step.SeenPurge!.ChildIds);
            Assert.Equal(("MSK-US-12", "admin-sub", item.Id, _world.ProjectId, _world.OrganizationId), (
                step.SeenPurge.Request.Identifier,
                step.SeenPurge.Request.ActorId,
                step.SeenPurge.Request.WorkItemId,
                step.SeenPurge.Request.ProjectId,
                step.SeenPurge.Request.OrganizationId));
            Assert.Equal(_clock.UtcNow, step.SeenPurge.Request.At);
            Assert.Equal(step.SeenRequest, step.SeenPurge.Request);
        }
    }

    // US-6 · "The step contract end to end": a pre-flight that refuses is the purge's refusal; nothing is locked or removed.
    [Fact]
    public async Task Handle_ThePreflightRefuses_NothingIsLockedOrRemoved()
    {
        var (_, repository, handler, steps) = DeletedWithSteps(2);
        steps[0].PreflightFailure = new ConflictException("storage.unreachable");

        var refused = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(Command(), CancellationToken.None));

        Assert.Equal("storage.unreachable", refused.Code);
        Assert.DoesNotContain("begin", _world.Calls);
        Assert.DoesNotContain("lock-hierarchy", _world.Calls);
        Assert.DoesNotContain("preflight step-2", _world.Calls);
        Assert.False(repository.Removed);
        Assert.Equal(0, repository.SaveCount);
    }

    // US-6 · "Purge_RolledBack_LogsNothing": a step that fails leaves the removal uncalled and the log without its line.
    [Fact]
    public async Task Handle_AStepFails_NoRemovalIsCalled_NoLogLine()
    {
        var (_, repository, handler, steps) = DeletedWithSteps(2);
        steps[1].PurgeFailure = new InvalidOperationException("a step failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(Command(), CancellationToken.None));

        Assert.False(repository.Removed);
        Assert.Equal(0, repository.SaveCount);
        Assert.DoesNotContain("commit", _world.Calls);
        Assert.DoesNotContain(_world.Calls, call => call.StartsWith("log:", StringComparison.Ordinal));
    }

    // ADR-0030 rule 5 · the order of the calls: the hierarchy first, then the item and its children, then the steps drop their
    // data, the item's team place goes, the children let go, and the item is removed and flushed.
    [Fact]
    public async Task Handle_LocksTheHierarchyFirst_ThenTheItemAndChildren_ThenStepsDropTheTeamPlaceReleaseChildrenRemove()
    {
        var (_, _, handler, _) = DeletedWithSteps(1);

        await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(
            [
                "read-head 12",
                "preflight step-1",
                "begin",
                "lock-hierarchy",
                "lock-item-and-children",
                "find-locked",
                "step step-1",
                "drop-team-place",
                $"release-children {_clock.UtcNow:O}",
                "remove",
                "save",
                "commit",
                _world.Calls[^1],
            ],
            _world.Calls);
        Assert.StartsWith("log: ", _world.Calls[^1], StringComparison.Ordinal);
    }

    // US-6 · "Only a deleted item can be purged": a live head is refused before any pre-flight or lock.
    [Fact]
    public async Task Handle_LiveHead_Is409NotDeleted_BeforeAnyPreflight()
    {
        var live = _world.LiveStory();
        var step = new RecordingPurgeStep(_world, "step-1");
        var (handler, repository, readModel, _) = Build(step);
        repository.Locked = live;
        readModel.Head = new WorkItemHead(live.Id, live.TypeId, live.StateId);

        var refused = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(Command(), CancellationToken.None));

        Assert.Equal(WorkItemErrorCodes.WorkItemNotDeleted, refused.Code);
        Assert.Equal(["read-head 12"], _world.Calls);
        Assert.False(repository.Removed);
    }

    // US-6 · "AfterAPurge_…Answer404NotFound": no row for the address, or none under the locks, is 404 and nothing is removed.
    [Fact]
    public async Task Handle_NoHead_Is404()
    {
        var (handler, _, _, _) = Build();

        var refused = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(Command(), CancellationToken.None));

        Assert.Equal(WorkItemErrorCodes.WorkItemNotFound, refused.Code);
        Assert.DoesNotContain("begin", _world.Calls);
    }

    [Fact]
    public async Task Handle_NoRowUnderTheLock_Is404()
    {
        var (_, repository, handler, _) = DeletedWithSteps(1);
        repository.ChildIds = null;

        var refused = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(Command(), CancellationToken.None));

        Assert.Equal(WorkItemErrorCodes.WorkItemNotFound, refused.Code);
        Assert.DoesNotContain("step step-1", _world.Calls);
        Assert.False(repository.Removed);
    }

    // Restore ∥ Purge: the restore got there first — the head said deleted, the row under the locks is live.
    [Fact]
    public async Task Handle_ARestoreGotThere_Is409NotDeleted_NothingRemoved()
    {
        var (item, repository, handler, _) = DeletedWithSteps(1);
        item.Restore(_world.StoryType, null, _clock.UtcNow);
        repository.Locked = item;

        var refused = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(Command(), CancellationToken.None));

        Assert.Equal(WorkItemErrorCodes.WorkItemNotDeleted, refused.Code);
        Assert.DoesNotContain("step step-1", _world.Calls);
        Assert.DoesNotContain("drop-team-place", _world.Calls);
        Assert.False(repository.Removed);
    }

    [Fact]
    public async Task Handle_NoStepsRegistered_IsFine()
    {
        var (item, repository, handler, _) = DeletedWithSteps(0);

        await handler.Handle(Command(), CancellationToken.None);

        Assert.True(repository.Removed);
        Assert.Equal(1, repository.SaveCount);
        Assert.NotEqual(Guid.Empty, item.Id);
    }

    // US-6 · "A purge leaves no trace … one log line without the title": once, after the commit, with the user, the organization,
    // the project and the identifier.
    [Fact]
    public async Task Handle_LogsOnceAfterTheCommit_WithoutTheTitle()
    {
        var item = _world.DeletedStory();
        var (handler, repository, readModel, logger) = Build();
        repository.Locked = item;
        readModel.Head = new WorkItemHead(item.Id, item.TypeId, item.StateId);

        await handler.Handle(Command(), CancellationToken.None);

        var line = Assert.Single(logger.Messages);
        Assert.Contains("MSK-US-12", line, StringComparison.Ordinal);
        Assert.Contains("admin-sub", line, StringComparison.Ordinal);
        Assert.Contains(_world.ProjectId.ToString(), line, StringComparison.Ordinal);
        Assert.Contains(_world.OrganizationId.ToString(), line, StringComparison.Ordinal);
        Assert.DoesNotContain(item.Title, line, StringComparison.Ordinal);
        Assert.True(_world.Calls.IndexOf("commit") < _world.Calls.FindIndex(call => call.StartsWith("log:", StringComparison.Ordinal)));
    }

    // Common: the address is resolved from the outside in before anything is read of the item.
    [Fact]
    public async Task Handle_UnknownProjectMalformedIdentifierOrAnotherProjectsKey_Is404_BeforeTheItemIsRead()
    {
        var (handler, _, _, _) = Build();

        var project = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new PurgeWorkItemCommand(_world.OrganizationId.ToString(), Guid.NewGuid().ToString(), "MSK-US-12"), CancellationToken.None));
        Assert.Equal("organizations.project.not_found", project.Code);

        foreach (var identifier in new[] { "garbage", "XYZ-US-1", "MSK-US-0" })
        {
            var refused = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(Command(identifier), CancellationToken.None));
            Assert.Equal(WorkItemErrorCodes.WorkItemNotFound, refused.Code);
        }

        Assert.Empty(_world.Calls);
    }
}
