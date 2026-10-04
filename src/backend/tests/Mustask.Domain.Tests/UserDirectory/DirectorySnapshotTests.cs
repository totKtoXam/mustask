using Mustask.Domain.UserDirectory;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Tests.UserDirectory;

/// <summary>mod-014 US-4: a snapshot's lifecycle and its proof of completeness (ADR-0025 § 5).</summary>
public sealed class DirectorySnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Begin_FirstSnapshot_IsOpen()
    {
        var snapshot = DirectorySnapshot.Begin(Guid.NewGuid(), 100, newestBegun: null, Now);

        Assert.Equal(DirectorySnapshotState.Open, snapshot.State);
        Assert.Equal(100, snapshot.EnumerationStart);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(99)]
    public void Begin_NotNewerThanTheNewestBegun_Conflicts(long enumerationStart)
    {
        var ex = Assert.Throws<ConflictException>(() => DirectorySnapshot.Begin(Guid.NewGuid(), enumerationStart, newestBegun: 100, Now));

        Assert.Equal(UserDirectoryErrorCodes.SnapshotConflict, ex.Code);
    }

    [Fact]
    public void EnsureBegunWith_AnotherStart_Conflicts()
    {
        var snapshot = DirectorySnapshot.Begin(Guid.NewGuid(), 100, null, Now);

        snapshot.EnsureBegunWith(100);
        Assert.Throws<ConflictException>(() => snapshot.EnsureBegunWith(101));
    }

    [Fact]
    public void Supersede_OpenSnapshot_IsNotOpenAnymore()
    {
        var snapshot = DirectorySnapshot.Begin(Guid.NewGuid(), 100, null, Now);

        snapshot.Supersede();

        Assert.Equal(DirectorySnapshotState.Superseded, snapshot.State);
        var ex = Assert.Throws<ConflictException>(snapshot.EnsureOpen);
        Assert.Equal(UserDirectoryErrorCodes.SnapshotNotOpen, ex.Code);
    }

    [Fact]
    public void EnsureCompletable_AllPagesAndEntries_Passes()
    {
        var snapshot = DirectorySnapshot.Begin(Guid.NewGuid(), 100, null, Now);

        snapshot.EnsureCompletable(pageCount: 2, entryCount: 600, appliedPageNumbers: [1, 0], stampedUsers: 600);
    }

    [Fact]
    public void EnsureCompletable_EmptyRealm_Passes()
    {
        var snapshot = DirectorySnapshot.Begin(Guid.NewGuid(), 100, null, Now);

        snapshot.EnsureCompletable(pageCount: 0, entryCount: 0, appliedPageNumbers: [], stampedUsers: 0);
    }

    // US-4 "An interrupted snapshot marks nobody absent".
    [Theory]
    [InlineData(2, 600, new[] { 0 }, 500)]       // a page missing
    [InlineData(2, 600, new[] { 0, 2 }, 600)]    // a page out of range
    [InlineData(1, 500, new[] { 0, 1 }, 500)]    // a page beyond the declared count
    [InlineData(2, 600, new[] { 0, 1 }, 599)]    // an entry missing
    public void EnsureCompletable_SomethingMissing_IsIncomplete(int pageCount, int entryCount, int[] pages, int stamped)
    {
        var snapshot = DirectorySnapshot.Begin(Guid.NewGuid(), 100, null, Now);

        var ex = Assert.Throws<ConflictException>(() => snapshot.EnsureCompletable(pageCount, entryCount, pages, stamped));

        Assert.Equal(UserDirectoryErrorCodes.SnapshotIncomplete, ex.Code);
        Assert.Equal(entryCount, (decimal)ex.Params["expected"].Value);
        Assert.Equal(stamped, (decimal)ex.Params["received"].Value);
        Assert.Equal(DirectorySnapshotState.Open, snapshot.State);
    }

    [Fact]
    public void EnsureCompletable_Superseded_IsNotOpen()
    {
        var snapshot = DirectorySnapshot.Begin(Guid.NewGuid(), 100, null, Now);
        snapshot.Supersede();

        var ex = Assert.Throws<ConflictException>(() => snapshot.EnsureCompletable(0, 0, [], 0));

        Assert.Equal(UserDirectoryErrorCodes.SnapshotNotOpen, ex.Code);
    }

    [Fact]
    public void Complete_StoresTheCounts()
    {
        var snapshot = DirectorySnapshot.Begin(Guid.NewGuid(), 100, null, Now);

        snapshot.Complete(1, 3, new FeedTally(3, 1, 1, 1, 0), markedAbsent: 2, Now);

        Assert.Equal(DirectorySnapshotState.Completed, snapshot.State);
        Assert.Equal(new FeedTally(3, 1, 1, 1, 0), snapshot.Tally);
        Assert.Equal(2, snapshot.MarkedAbsent);
        Assert.Equal(1, snapshot.PageCount);
        Assert.Equal(3, snapshot.EntryCount);
        Assert.Equal(Now, snapshot.CompletedAt);
    }

    [Fact]
    public void Supersede_Completed_StaysCompleted()
    {
        var snapshot = DirectorySnapshot.Begin(Guid.NewGuid(), 100, null, Now);
        snapshot.Complete(0, 0, FeedTally.Empty, 0, Now);

        snapshot.Supersede();

        Assert.Equal(DirectorySnapshotState.Completed, snapshot.State);
    }

    [Fact]
    public void Page_Record_KeepsItsTally()
    {
        var page = DirectorySnapshotPage.Record(Guid.NewGuid(), 3, new FeedTally(2, 1, 0, 1, 0), Now);

        Assert.Equal(3, page.PageNumber);
        Assert.Equal(new FeedTally(2, 1, 0, 1, 0), page.Tally);
    }
}
