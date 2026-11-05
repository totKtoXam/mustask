using Mustask.Application.Features.WorkItems.RestoreWorkItem;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.WorkItems.RestoreWorkItem;

/// <summary>
/// mod-005 US-4 · the restore handler's own business (api-contract § 0.4, § 2.2): a sent <c>stateId</c> is an untrusted string
/// that maps to nothing (absent), the id, or an id that names nothing; the order of the 409s is not_deleted and then
/// state_not_allowed, whatever the string; the clock reaches the domain. The restore's rules are the domain's
/// (<c>WorkItemDeletionTests</c>).
/// </summary>
public sealed class RestoreWorkItemHandlerTests
{
    private readonly WorkItemWorld _world = new();
    private readonly FakeClock _clock = new() { UtcNow = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero) };

    private (RestoreWorkItemHandler Handler, FakeWorkItemRepository Repository) Build()
    {
        var repository = new FakeWorkItemRepository(_world);
        var handler = new RestoreWorkItemHandler(
            _world.Structure, _world.Processes, repository, new FakeWorkItemReadModel(_world), _clock, new FakeUnitOfWork());

        return (handler, repository);
    }

    private RestoreWorkItemCommand Command(string? stateId = null, string identifier = "MSK-US-12") =>
        new(_world.OrganizationId.ToString(), _world.ProjectId.ToString(), identifier, stateId);

    // US-4 · "The proposal is the held state": no stateId restores the state held at deletion, with the clock's time.
    [Fact]
    public async Task Handle_NoStateId_RestoresTheHeldState_WithTheClock_LockingTheItemAlone()
    {
        var (handler, repository) = Build();
        var item = _world.LiveStory();
        item.Save(new WorkItemEdit(StateId: _world.ActiveId), _world.StoryType, _clock.UtcNow);
        item.Delete(_world.StoryType, "deleter-sub", _clock.UtcNow);
        repository.Locked = item;
        _clock.UtcNow = _clock.UtcNow.AddHours(3);

        await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(_world.ActiveId, item.StateId);
        Assert.Null(item.DeletedFromStateId);
        Assert.Equal(_clock.UtcNow, item.ChangedAt);
        Assert.Equal(["lock-item 12", "save"], _world.Calls);
    }

    // US-4 · "The choice is limited to the item's type": a chosen state of the type is applied.
    [Fact]
    public async Task Handle_AChosenState_IsApplied()
    {
        var (handler, repository) = Build();
        var item = _world.DeletedStory();
        repository.Locked = item;

        await handler.Handle(Command(_world.ActiveId.ToString()), CancellationToken.None);

        Assert.Equal(_world.ActiveId, item.StateId);
    }

    // US-4 · "Restoring something not deleted is refused, even naming «Удалена» or a foreign state": not_deleted first, whatever
    // the string, and nothing is applied or flushed.
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Handle_ALiveItem_Is409NotDeleted_BeforeStateNotAllowed(string? stateId)
    {
        var (handler, repository) = Build();
        var item = _world.LiveStory();
        repository.Locked = item;

        var refused = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(Command(stateId), CancellationToken.None));

        Assert.Equal(WorkItemErrorCodes.WorkItemNotDeleted, refused.Code);
        Assert.Equal(0, repository.SaveCount);
        Assert.Equal(_world.NewId, item.StateId);
    }

    // US-4 · a string that is not an id, the zero id, an unknown id and the type's «Удалена» all name no allowed state — but only
    // for an item that is deleted.
    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("11111111-2222-3333-4444-555555555555")]
    [InlineData("")]
    public async Task Handle_ADeletedItem_AStateIdThatNamesNoAllowedState_Is409StateNotAllowed_Unchanged(string stateId)
    {
        var (handler, repository) = Build();
        var item = _world.DeletedStory();
        repository.Locked = item;

        var refused = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(Command(stateId), CancellationToken.None));

        Assert.Equal(WorkItemErrorCodes.StateNotAllowed, refused.Code);
        Assert.Equal(0, repository.SaveCount);
        Assert.True(item.IsDeleted(_world.StoryType));
        Assert.NotNull(item.DeletedFromStateId);
    }

    [Fact]
    public async Task Handle_TheTypesRemovedState_Is409StateNotAllowed()
    {
        var (handler, repository) = Build();
        repository.Locked = _world.DeletedStory();

        var refused = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(Command(_world.RemovedId.ToString()), CancellationToken.None));

        Assert.Equal(WorkItemErrorCodes.StateNotAllowed, refused.Code);
    }

    // api-contract § 0.4 · the path's not-found comes first.
    [Fact]
    public async Task Handle_NoRow_Is404_BeforeAnyRefusal()
    {
        var (handler, _) = Build();

        var refused = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(Command("not-a-guid"), CancellationToken.None));

        Assert.Equal(WorkItemErrorCodes.WorkItemNotFound, refused.Code);
    }
}
