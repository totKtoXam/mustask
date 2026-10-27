using Mustask.Application.Features.WorkItems.DeleteWorkItem;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.WorkItems.DeleteWorkItem;

/// <summary>
/// mod-005 US-1 · the delete handler's own business (api-contract § 0.4, § 2.1): the order of answers — the path's not-found
/// before the 409 — and the actor and the clock reaching the domain. The deletion's rules are the domain's
/// (<c>WorkItemDeletionTests</c>); the lock's effect is the race tests'.
/// </summary>
public sealed class DeleteWorkItemHandlerTests
{
    private readonly WorkItemWorld _world = new();
    private readonly FakeClock _clock = new() { UtcNow = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero) };
    private readonly FakeCurrentUser _user = new() { SubjectId = "member-sub" };

    private (DeleteWorkItemHandler Handler, FakeWorkItemRepository Repository) Build()
    {
        var repository = new FakeWorkItemRepository(_world);
        var handler = new DeleteWorkItemHandler(
            _world.Structure, _world.Processes, repository, new FakeWorkItemReadModel(_world), _user, _clock, new FakeUnitOfWork());

        return (handler, repository);
    }

    private DeleteWorkItemCommand Command(string identifier = "MSK-US-12") =>
        new(_world.OrganizationId.ToString(), _world.ProjectId.ToString(), identifier);

    // US-1 · "Confirming moves the item into «Удалена»": the actor is the token's subject and the time the clock's, the one row is
    // locked and flushed once, and nothing else is locked (ADR-0030 rule 4).
    [Fact]
    public async Task Handle_LiveItem_DeletesIt_WithTheActorAndTheClock_LockingTheItemAlone()
    {
        var (handler, repository) = Build();
        var item = _world.LiveStory();
        repository.Locked = item;

        await handler.Handle(Command(), CancellationToken.None);

        Assert.True(item.IsDeleted(_world.StoryType));
        Assert.Equal(("member-sub", _clock.UtcNow, _clock.UtcNow), (item.DeletedBy, item.DeletedAt, item.ChangedAt));
        Assert.Equal(["lock-item 12", "save"], _world.Calls);
    }

    // US-1 · "Deleting something already deleted": the domain refuses under the lock, nothing is flushed.
    [Fact]
    public async Task Handle_AlreadyDeleted_Is409Deleted_NothingFlushed()
    {
        var (handler, repository) = Build();
        var item = _world.DeletedStory();
        repository.Locked = item;
        var recorded = (item.DeletedBy, item.DeletedAt);

        var refused = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(Command(), CancellationToken.None));

        Assert.Equal(WorkItemErrorCodes.WorkItemDeleted, refused.Code);
        Assert.Equal(0, repository.SaveCount);
        Assert.Equal(recorded, (item.DeletedBy, item.DeletedAt));
    }

    // api-contract § 0.4 · the path's not-found comes before the 409: no row, a malformed identifier, another project's key, a
    // type key that is not the item's, or an unknown project — each its level's code, and the item is not read.
    [Fact]
    public async Task Handle_PathNotFound_BeforeTheRefusal()
    {
        var (handler, repository) = Build();

        // No row for the number: the lock finds nothing.
        var noRow = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(Command("MSK-US-99"), CancellationToken.None));
        Assert.Equal(WorkItemErrorCodes.WorkItemNotFound, noRow.Code);

        repository.Locked = _world.DeletedStory();

        foreach (var identifier in new[] { "garbage", "XYZ-US-12", "MSK-US-0" })
        {
            var refused = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(Command(identifier), CancellationToken.None));
            Assert.Equal(WorkItemErrorCodes.WorkItemNotFound, refused.Code);
        }

        var wrongType = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(Command("MSK-BUG-12"), CancellationToken.None));
        Assert.Equal(WorkItemErrorCodes.WorkItemNotFound, wrongType.Code);

        var project = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new DeleteWorkItemCommand(_world.OrganizationId.ToString(), Guid.NewGuid().ToString(), "MSK-US-12"), CancellationToken.None));
        Assert.Equal("organizations.project.not_found", project.Code);
        Assert.Equal(0, repository.SaveCount);
    }
}
