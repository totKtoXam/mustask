using Mustask.Domain.UserDirectory;

namespace Mustask.Domain.Tests.UserDirectory;

/// <summary>mod-014: the effects table of api-contract.md B.5 (US-3, US-4, US-5 at the domain level).</summary>
public sealed class ProjectedUserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private const string Id = "7c9e6679-7425-40de-944b-e07fc1f90ae7";

    private static UserPush State(long moment, string lastName = "Смирнова", bool enabled = true, string? email = "anna.s@example.test") =>
        new(Id, moment, "a.smirnova", "Анна", lastName, email, enabled, Deleted: false);

    private static UserPush Deletion(long moment, string lastName = "Смирнова") =>
        new(Id, moment, "a.smirnova", "Анна", lastName, "anna.s@example.test", Enabled: false, Deleted: true);

    [Fact]
    public void Create_State_IsPresentWithDerivedValues()
    {
        var user = ProjectedUser.Create(State(10), Now);

        Assert.Equal(ProjectedUserLifecycle.Present, user.Lifecycle);
        Assert.True(user.IsActive);
        Assert.Equal("Анна Смирнова", user.DisplayName);
        Assert.Equal("анна смирнова", user.SearchName);
        Assert.Equal("anna.s@example.test", user.SearchEmail);
        Assert.Equal(DirectoryCollation.SortKey("Анна Смирнова"), user.SortKey);
        Assert.Equal(DirectoryCollation.Version, user.SortKeyVersion);
        Assert.Equal(10, user.Moment);
    }

    // D-5 (qa.md, re-check 3): the derivation is total even for a text validation would refuse, so a noncharacter that
    // reached the domain some other way (a snapshot's own read, a future caller) is never a 500: .NET's NFC throws on
    // U+FFFE, which Create and Apply used to pass through.
    [Fact]
    public void Create_AndApply_WithANoncharacterInTheNameOrTheEmail_DeriveWithTheReplacementCharacter()
    {
        var created = ProjectedUser.Create(
            new UserPush(Id, 10, "a.smirnova", "Ан￾на", "Смир﷐нова", "anna￿@example.test", Enabled: true, Deleted: false),
            Now);

        Assert.Equal("ан�на смир�нова", created.SearchName);
        Assert.Equal("anna�@example.test", created.SearchEmail);
        Assert.Equal(DirectoryCollation.SortKey("Ан￾на Смир﷐нова"), created.SortKey);

        var applied = ProjectedUser.Create(State(10), Now);

        Assert.Equal(
            PushOutcome.Changed,
            applied.Apply(new UserPush(Id, 20, "a.smirnova", "Анна", "Смирнова", "e￾@example.test", Enabled: true, Deleted: false), Now));
        Assert.Equal("e�@example.test", applied.SearchEmail);
    }

    // US-2 "A user created already disabled is inactive from the start".
    [Fact]
    public void Create_Disabled_IsInactive()
    {
        var user = ProjectedUser.Create(State(10, enabled: false), Now);

        Assert.False(user.IsActive);
        Assert.Equal(ProjectedUserLifecycle.Present, user.Lifecycle);
    }

    [Fact]
    public void Create_Deletion_IsHeldDeletedWithTheCarriedName()
    {
        var user = ProjectedUser.Create(Deletion(10), Now);

        Assert.Equal(ProjectedUserLifecycle.Deleted, user.Lifecycle);
        Assert.False(user.IsActive);
        Assert.Equal("Анна Смирнова", user.DisplayName);
    }

    [Fact]
    public void Create_NoEmail_StoresEmpty()
    {
        var user = ProjectedUser.Create(State(10, email: null), Now);

        Assert.Equal(string.Empty, user.Email);
    }

    // US-5 "The same push applied twice changes nothing".
    [Fact]
    public void Apply_SamePushTwice_IsUnchanged()
    {
        var user = ProjectedUser.Create(State(10), Now);

        Assert.Equal(PushOutcome.Unchanged, user.Apply(State(10), Now));
        Assert.Equal("Анна Смирнова", user.DisplayName);
    }

    // US-5 "An older push arriving after a newer one is ignored".
    [Fact]
    public void Apply_OlderAfterNewer_IsIgnored()
    {
        var user = ProjectedUser.Create(State(20, lastName: "Кузнецова"), Now);

        Assert.Equal(PushOutcome.Unchanged, user.Apply(State(10, lastName: "Смирнова"), Now));
        Assert.Equal("Анна Кузнецова", user.DisplayName);
        Assert.Equal(20, user.Moment);
    }

    // US-5 "An older 'enabled' after a newer 'disabled' leaves the user inactive".
    [Fact]
    public void Apply_OlderEnabledAfterNewerDisabled_StaysInactive()
    {
        var user = ProjectedUser.Create(State(20, enabled: false), Now);

        user.Apply(State(10, enabled: true), Now);

        Assert.False(user.IsActive);
    }

    // US-2 "A change of name is followed".
    [Fact]
    public void Apply_NewerWithAnotherName_IsChanged()
    {
        var user = ProjectedUser.Create(State(10), Now);

        Assert.Equal(PushOutcome.Changed, user.Apply(State(20, lastName: "Кузнецова"), Now));
        Assert.Equal("Анна Кузнецова", user.DisplayName);
        Assert.Equal("анна кузнецова", user.SearchName);
        Assert.Equal(DirectoryCollation.SortKey("Анна Кузнецова"), user.SortKey);
    }

    [Fact]
    public void Apply_NewerWithTheSameValues_IsUnchangedButAdvancesTheMoment()
    {
        var user = ProjectedUser.Create(State(10), Now);

        Assert.Equal(PushOutcome.Unchanged, user.Apply(State(20), Now));
        Assert.Equal(20, user.Moment);

        // The advanced moment is what keeps a delayed push from between 10
        // and 20 from reverting the user (ADR-0025 § 6).
        Assert.Equal(PushOutcome.Unchanged, user.Apply(State(15, lastName: "Старая"), Now));
        Assert.Equal("Анна Смирнова", user.DisplayName);
    }

    // US-3 "A disabled account becomes inactive" and "enabled again becomes active".
    [Fact]
    public void Apply_DisableThenEnable_FollowsTheStatus()
    {
        var user = ProjectedUser.Create(State(10), Now);

        Assert.Equal(PushOutcome.Changed, user.Apply(State(20, enabled: false), Now));
        Assert.False(user.IsActive);
        Assert.Equal("Анна Смирнова", user.DisplayName);

        Assert.Equal(PushOutcome.Changed, user.Apply(State(30, enabled: true), Now));
        Assert.True(user.IsActive);
    }

    // US-3 "A deleted account becomes inactive for good and keeps its name".
    [Fact]
    public void Apply_Deletion_IsFinalAndKeepsTheName()
    {
        var user = ProjectedUser.Create(State(10), Now);

        Assert.Equal(PushOutcome.Changed, user.Apply(Deletion(20), Now));

        Assert.Equal(ProjectedUserLifecycle.Deleted, user.Lifecycle);
        Assert.False(user.IsActive);
        Assert.Equal("Анна Смирнова", user.DisplayName);
        Assert.Equal("anna.s@example.test", user.Email);
    }

    // US-3 "A reported deletion is final": a later enabled push changes nothing.
    [Fact]
    public void Apply_StateAfterDeletion_IsHeldAsDeleted()
    {
        var user = ProjectedUser.Create(State(10), Now);
        user.Apply(Deletion(20), Now);

        Assert.Equal(PushOutcome.HeldAsDeleted, user.Apply(State(30, enabled: true), Now));
        Assert.False(user.IsActive);
        Assert.Equal(ProjectedUserLifecycle.Deleted, user.Lifecycle);
    }

    [Fact]
    public void Apply_DeletionTwice_IsUnchanged()
    {
        var user = ProjectedUser.Create(Deletion(10), Now);

        Assert.Equal(PushOutcome.Unchanged, user.Apply(Deletion(20), Now));
    }

    // US-5 "A deletion applied concurrently with an enabled push wins" — either order.
    [Fact]
    public void Apply_OlderDeletionAfterNewerState_StillDeletesAndKeepsTheNewerName()
    {
        var user = ProjectedUser.Create(State(20, lastName: "Кузнецова"), Now);

        Assert.Equal(PushOutcome.Changed, user.Apply(Deletion(10, lastName: "Смирнова"), Now));

        Assert.Equal(ProjectedUserLifecycle.Deleted, user.Lifecycle);
        Assert.Equal("Анна Кузнецова", user.DisplayName);
    }

    [Fact]
    public void Apply_NewerDeletion_TakesTheCarriedName()
    {
        var user = ProjectedUser.Create(State(10, lastName: "Смирнова"), Now);

        user.Apply(Deletion(20, lastName: "Кузнецова"), Now);

        Assert.Equal("Анна Кузнецова", user.DisplayName);
    }

    [Fact]
    public void Apply_PushForAnotherId_Throws()
    {
        var user = ProjectedUser.Create(State(10), Now);

        Assert.Throws<ArgumentException>(() => user.Apply(State(20) with { Id = "other" }, Now));
    }

    // US-4 "A deletion whose push was lost is caught": absent as of the start, name kept.
    [Fact]
    public void MarkAbsent_PresentAndOlderThanTheStart_BecomesAbsent()
    {
        var user = ProjectedUser.Create(State(10), Now);

        Assert.True(user.MarkAbsent(100, Now));

        Assert.Equal(ProjectedUserLifecycle.Absent, user.Lifecycle);
        Assert.False(user.IsActive);
        Assert.Equal(100, user.Moment);
        Assert.Equal("Анна Смирнова", user.DisplayName);
    }

    // US-4 "A user created during a reconciliation stays active".
    [Fact]
    public void MarkAbsent_StateNewerThanTheStart_IsUntouched()
    {
        var user = ProjectedUser.Create(State(150), Now);

        Assert.False(user.MarkAbsent(100, Now));
        Assert.True(user.IsActive);
    }

    [Fact]
    public void MarkAbsent_Deleted_IsNeverTurnedIntoAnAbsence()
    {
        var user = ProjectedUser.Create(Deletion(10), Now);

        Assert.False(user.MarkAbsent(100, Now));
        Assert.Equal(ProjectedUserLifecycle.Deleted, user.Lifecycle);
    }

    [Fact]
    public void MarkAbsent_AlreadyAbsent_IsNotCountedAgain()
    {
        var user = ProjectedUser.Create(State(10), Now);
        user.MarkAbsent(100, Now);

        Assert.False(user.MarkAbsent(200, Now));
        Assert.Equal(100, user.Moment);
    }

    // US-4 "An absence yields to a newer push".
    [Fact]
    public void Apply_NewerPushAfterAbsence_IsPresentAgain()
    {
        var user = ProjectedUser.Create(State(10), Now);
        user.MarkAbsent(100, Now);

        Assert.Equal(PushOutcome.Changed, user.Apply(State(150), Now));
        Assert.True(user.IsActive);
        Assert.Equal(ProjectedUserLifecycle.Present, user.Lifecycle);
    }

    // US-4 "An absence yields to a newer push ... a push for a moment earlier than that enumeration start changes nothing".
    [Fact]
    public void Apply_OlderPushAfterAbsence_IsIgnored()
    {
        var user = ProjectedUser.Create(State(10), Now);
        user.MarkAbsent(100, Now);

        Assert.Equal(PushOutcome.Unchanged, user.Apply(State(50), Now));
        Assert.False(user.IsActive);
        Assert.Equal(ProjectedUserLifecycle.Absent, user.Lifecycle);
    }

    // US-3 "A reported deletion is final ... after a complete reconciliation".
    [Fact]
    public void DeletedUser_SurvivesAReconciliationAndALaterPush()
    {
        var user = ProjectedUser.Create(State(10), Now);
        user.Apply(Deletion(20), Now);

        Assert.False(user.MarkAbsent(100, Now));
        Assert.Equal(PushOutcome.HeldAsDeleted, user.Apply(State(200), Now));
        Assert.False(user.IsActive);
    }

    // US-2 "A change of username alone moves the fallback display name".
    [Fact]
    public void Apply_UsernameRenameWithNoNames_MovesTheDisplayName()
    {
        var user = ProjectedUser.Create(new UserPush(Id, 10, "qa.fixture", null, null, null, true, false), Now);

        Assert.Equal(PushOutcome.Changed, user.Apply(new UserPush(Id, 20, "qa.robot", null, null, null, true, false), Now));
        Assert.Equal("qa.robot", user.DisplayName);
    }

    [Fact]
    public void StampSnapshot_RemembersTheSnapshot()
    {
        var user = ProjectedUser.Create(State(10), Now);
        var snapshot = Guid.NewGuid();

        user.StampSnapshot(snapshot);

        Assert.Equal(snapshot, user.LastSnapshotId);
    }

    // ADR-0025 § 9 (code review F5): a key of the current collation is not stale — nothing to recompute, nothing
    // changed. The stale case needs a row written under another ICU version, so it is tested through the database
    // (ReconciliationTests.Reconcile_RecomputesStaleSortKeys_OfDeletedAndAbsentUsersToo).
    [Fact]
    public void RefreshSortKey_KeyOfTheCurrentCollation_ChangesNothing()
    {
        var user = ProjectedUser.Create(State(10), Now);
        user.Apply(Deletion(20), Now);
        var key = user.SortKey;

        Assert.False(user.RefreshSortKey());
        Assert.Same(key, user.SortKey);
        Assert.Equal(ProjectedUserLifecycle.Deleted, user.Lifecycle);
        Assert.Equal(20, user.Moment);
    }
}
