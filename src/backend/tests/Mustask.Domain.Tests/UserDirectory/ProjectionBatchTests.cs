using Mustask.Domain.UserDirectory;

namespace Mustask.Domain.Tests.UserDirectory;

/// <summary>mod-014 US-5: one batch's pushes, in moment order per id, counted once each (api-contract.md B.4).</summary>
public sealed class ProjectionBatchTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyDictionary<string, ProjectedUser> NothingHeld = new Dictionary<string, ProjectedUser>();

    private static UserPush State(string id, long moment, string lastName = "Смирнова", bool enabled = true) =>
        new(id, moment, id, "Анна", lastName, null, enabled, Deleted: false);

    [Fact]
    public void Apply_NewIds_AreAddedAndCounted()
    {
        var result = ProjectionBatch.Apply([State("a", 1), State("b", 2)], NothingHeld, Now, snapshotId: null);

        Assert.Equal(2, result.Added.Count);
        Assert.Equal(new FeedTally(2, 2, 0, 0, 0), result.Tally);
    }

    // US-5 "An older push arriving after a newer one is ignored" — within one request.
    [Fact]
    public void Apply_SameIdTwiceInOneRequest_TheNewestMomentWinsWhateverTheOrder()
    {
        var result = ProjectionBatch.Apply(
            [State("a", 20, lastName: "Кузнецова"), State("a", 10, lastName: "Смирнова")],
            NothingHeld,
            Now,
            snapshotId: null);

        var user = Assert.Single(result.Added);
        Assert.Equal("Анна Кузнецова", user.DisplayName);
        Assert.Equal(new FeedTally(2, 1, 1, 0, 0), result.Tally);
    }

    [Fact]
    public void Apply_DeletionAndStateInOneRequest_DeletionWins()
    {
        var deletion = new UserPush("a", 10, "a", "Анна", "Смирнова", null, Enabled: false, Deleted: true);

        var result = ProjectionBatch.Apply([State("a", 20), deletion], NothingHeld, Now, snapshotId: null);

        var user = Assert.Single(result.Added);
        Assert.Equal(ProjectedUserLifecycle.Deleted, user.Lifecycle);
        Assert.Equal(new FeedTally(2, 1, 0, 0, 1), result.Tally);
    }

    [Fact]
    public void Apply_HeldUser_IsUpdatedInPlaceNotAdded()
    {
        var held = ProjectedUser.Create(State("a", 1), Now);

        var result = ProjectionBatch.Apply(
            [State("a", 2, enabled: false)],
            new Dictionary<string, ProjectedUser> { ["a"] = held },
            Now,
            snapshotId: null);

        Assert.Empty(result.Added);
        Assert.False(held.IsActive);
        Assert.Equal(new FeedTally(1, 0, 1, 0, 0), result.Tally);
    }

    [Fact]
    public void Apply_WithASnapshot_StampsEveryCarriedUser()
    {
        var snapshot = Guid.NewGuid();
        var deletedHeld = ProjectedUser.Create(new UserPush("d", 1, "d", null, null, null, false, true), Now);
        var staleHeld = ProjectedUser.Create(State("s", 100), Now);

        var result = ProjectionBatch.Apply(
            [State("a", 5), State("d", 5), State("s", 5)],
            new Dictionary<string, ProjectedUser> { ["d"] = deletedHeld, ["s"] = staleHeld },
            Now,
            snapshot);

        Assert.Equal(snapshot, Assert.Single(result.Added).LastSnapshotId);
        Assert.Equal(snapshot, deletedHeld.LastSnapshotId);
        Assert.Equal(snapshot, staleHeld.LastSnapshotId);
        Assert.Equal(new FeedTally(3, 1, 0, 1, 1), result.Tally);
    }

    [Fact]
    public void Apply_WithoutASnapshot_StampsNothing()
    {
        var result = ProjectionBatch.Apply([State("a", 1)], NothingHeld, Now, snapshotId: null);

        Assert.Null(Assert.Single(result.Added).LastSnapshotId);
    }

    // Security pass S5: the ids of the deletions that were applied — a deletion is final (BR-UP-5), so a log line names them.
    // A deletion of a user already deleted changes nothing, a plain state is no deletion, an id twice counts once.
    [Fact]
    public void Apply_ReportsTheIdsWhoseDeletionWasApplied_AndNoOther()
    {
        UserPush Deletion(string id, long moment) => new(id, moment, id, "Анна", "Смирнова", null, Enabled: false, Deleted: true);
        var present = ProjectedUser.Create(State("present", 1), Now);
        var alreadyDeleted = ProjectedUser.Create(Deletion("already", 1), Now);

        var result = ProjectionBatch.Apply(
            [
                Deletion("present", 5),
                Deletion("already", 5),
                Deletion("new", 5),
                Deletion("new", 6),
                State("plain", 5),
            ],
            new Dictionary<string, ProjectedUser> { ["present"] = present, ["already"] = alreadyDeleted },
            Now,
            snapshotId: null);

        Assert.Equal(["present", "new"], result.Deleted);
    }

    [Fact]
    public void Apply_WithNoDeletion_ReportsNoDeletedIds()
    {
        var result = ProjectionBatch.Apply([State("a", 1), State("b", 2)], NothingHeld, Now, snapshotId: null);

        Assert.Empty(result.Deleted);
    }

    [Fact]
    public void FeedTally_Sum_AddsEveryCount()
    {
        var sum = FeedTally.Sum([new FeedTally(3, 1, 1, 1, 0), new FeedTally(2, 0, 0, 1, 1)]);

        Assert.Equal(new FeedTally(5, 1, 1, 2, 1), sum);
    }
}
