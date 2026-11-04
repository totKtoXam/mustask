using Mustask.Domain.UserDirectory;

namespace Mustask.Domain.Tests.UserDirectory;

/// <summary>tech-025 (ADR-0034): the projection is written by a sign-in — creation, update, legacy rows, derived values.</summary>
public sealed class ProjectedUserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Later = Now.AddHours(1);

    private const string Id = "7c9e6679-7425-40de-944b-e07fc1f90ae7";

    private static SignInProfile Profile(string lastName = "Смирнова", string? email = "anna.s@example.test", string username = "a.smirnova") =>
        new(Id, username, "Анна", lastName, email);

    [Fact]
    public void SignIn_FirstTime_IsPresentEnabledWithDerivedValues()
    {
        var user = ProjectedUser.SignIn(Profile(), Now);

        Assert.Equal(Id, user.Id);
        Assert.Equal("a.smirnova", user.Username);
        Assert.Equal(ProjectedUserLifecycle.Present, user.Lifecycle);
        Assert.True(user.Enabled);
        Assert.True(user.IsActive);
        Assert.Equal("Анна Смирнова", user.DisplayName);
        Assert.Equal("анна смирнова", user.SearchName);
        Assert.Equal("anna.s@example.test", user.SearchEmail);
        Assert.Equal(DirectoryCollation.SortKey("Анна Смирнова"), user.SortKey);
        Assert.Equal(DirectoryCollation.Version, user.SortKeyVersion);
        Assert.Equal(Now, user.ChangedAt);
        Assert.Equal(0, user.Moment);
        Assert.Null(user.LastSnapshotId);
    }

    [Fact]
    public void SignIn_NoNamesNoEmail_StoresEmptyAndShowsTheUsername()
    {
        var user = ProjectedUser.SignIn(new SignInProfile(Id, "qa.fixture", null, null, null), Now);

        Assert.Equal(string.Empty, user.FirstName);
        Assert.Equal(string.Empty, user.LastName);
        Assert.Equal(string.Empty, user.Email);
        Assert.Equal("qa.fixture", user.DisplayName);
    }

    // D-5 (qa.md, re-check 3): the derivation is total even for a text validation would refuse.
    [Fact]
    public void SignIn_AndRecordSignIn_WithANoncharacterAreRefusedByValidation_NeverByTheDerivation()
    {
        var withNoncharacter = new SignInProfile(Id, "a.smirnova", "Ан￾на", "Смирнова", "anna@example.test");

        Assert.Throws<Mustask.Shared.Exceptions.ValidationException>(() => ProjectedUser.SignIn(withNoncharacter, Now));

        var user = ProjectedUser.SignIn(Profile(), Now);

        Assert.Throws<Mustask.Shared.Exceptions.ValidationException>(() => user.RecordSignIn(withNoncharacter, Later));
        Assert.Equal("Анна Смирнова", user.DisplayName);
    }

    [Fact]
    public void RecordSignIn_SameClaims_IsUnchangedAndLeavesChangedAtAlone()
    {
        var user = ProjectedUser.SignIn(Profile(), Now);

        Assert.Equal(SignInOutcome.Unchanged, user.RecordSignIn(Profile(), Later));
        Assert.Equal(Now, user.ChangedAt);
        Assert.Equal("Анна Смирнова", user.DisplayName);
    }

    [Fact]
    public void RecordSignIn_ChangedName_IsUpdatedAndRederivesEverything()
    {
        var user = ProjectedUser.SignIn(Profile(), Now);

        Assert.Equal(SignInOutcome.Updated, user.RecordSignIn(Profile(lastName: "Кузнецова"), Later));

        Assert.Equal("Анна Кузнецова", user.DisplayName);
        Assert.Equal("анна кузнецова", user.SearchName);
        Assert.Equal(DirectoryCollation.SortKey("Анна Кузнецова"), user.SortKey);
        Assert.Equal(Later, user.ChangedAt);
    }

    [Fact]
    public void RecordSignIn_ChangedEmailOnly_IsUpdated()
    {
        var user = ProjectedUser.SignIn(Profile(), Now);

        Assert.Equal(SignInOutcome.Updated, user.RecordSignIn(Profile(email: "New.Address@Example.test"), Later));

        Assert.Equal("New.Address@Example.test", user.Email);
        Assert.Equal("new.address@example.test", user.SearchEmail);
    }

    [Fact]
    public void RecordSignIn_EmailDropped_StoresEmpty()
    {
        var user = ProjectedUser.SignIn(Profile(), Now);

        Assert.Equal(SignInOutcome.Updated, user.RecordSignIn(Profile(email: null), Later));

        Assert.Equal(string.Empty, user.Email);
        Assert.Equal(string.Empty, user.SearchEmail);
    }

    // A change of username alone moves the fallback display name.
    [Fact]
    public void RecordSignIn_UsernameRenameWithNoNames_MovesTheDisplayName()
    {
        var user = ProjectedUser.SignIn(new SignInProfile(Id, "qa.fixture", null, null, null), Now);

        Assert.Equal(SignInOutcome.Updated, user.RecordSignIn(new SignInProfile(Id, "qa.robot", null, null, null), Later));
        Assert.Equal("qa.robot", user.DisplayName);
    }

    [Fact]
    public void RecordSignIn_ForAnotherId_Throws()
    {
        var user = ProjectedUser.SignIn(Profile(), Now);

        Assert.Throws<ArgumentException>(() => user.RecordSignIn(Profile() with { Id = "someone-else" }, Later));
    }

    [Fact]
    public void RecordSignIn_InvalidClaims_ThrowAndChangeNothing()
    {
        var user = ProjectedUser.SignIn(Profile(), Now);

        Assert.Throws<Mustask.Shared.Exceptions.ValidationException>(() => user.RecordSignIn(Profile() with { Username = null }, Later));

        Assert.Equal("a.smirnova", user.Username);
        Assert.Equal(Now, user.ChangedAt);
    }

    // The rows the retired push channel wrote are still read; a sign-in is proof that Keycloak lets the person in.
    [Theory]
    [InlineData(ProjectedUserLifecycle.Absent)]
    [InlineData(ProjectedUserLifecycle.Deleted)]
    public void RecordSignIn_LegacyInactiveRow_BecomesPresentAndActive(ProjectedUserLifecycle legacy)
    {
        var user = ProjectedUser.SignIn(Profile(), Now);
        SetLegacyState(user, enabled: true, legacy);

        Assert.False(user.IsActive);
        Assert.Equal(SignInOutcome.Updated, user.RecordSignIn(Profile(), Later));

        Assert.Equal(ProjectedUserLifecycle.Present, user.Lifecycle);
        Assert.True(user.Enabled);
        Assert.True(user.IsActive);
        Assert.Equal(Later, user.ChangedAt);
    }

    [Fact]
    public void RecordSignIn_LegacyDisabledRow_BecomesEnabledAndActive()
    {
        var user = ProjectedUser.SignIn(Profile(), Now);
        SetLegacyState(user, enabled: false, ProjectedUserLifecycle.Present);

        Assert.Equal(SignInOutcome.Updated, user.RecordSignIn(Profile(), Later));

        Assert.True(user.IsActive);
    }

    [Fact]
    public void RecordSignIn_SortKeyOfAnotherCollationVersion_IsRecomputed()
    {
        var user = ProjectedUser.SignIn(Profile(), Now);
        typeof(ProjectedUser).GetProperty(nameof(ProjectedUser.SortKeyVersion))!.SetValue(user, "an-older-icu");
        typeof(ProjectedUser).GetProperty(nameof(ProjectedUser.SortKey))!.SetValue(user, new byte[] { 1, 2, 3 });

        Assert.Equal(SignInOutcome.Unchanged, user.RecordSignIn(Profile(), Later));

        Assert.Equal(DirectoryCollation.Version, user.SortKeyVersion);
        Assert.Equal(DirectoryCollation.SortKey("Анна Смирнова"), user.SortKey);
    }

    private static void SetLegacyState(ProjectedUser user, bool enabled, ProjectedUserLifecycle lifecycle)
    {
        typeof(ProjectedUser).GetProperty(nameof(ProjectedUser.Enabled))!.SetValue(user, enabled);
        typeof(ProjectedUser).GetProperty(nameof(ProjectedUser.Lifecycle))!.SetValue(user, lifecycle);
        typeof(ProjectedUser).GetProperty(nameof(ProjectedUser.IsActive))!.SetValue(user, false);
    }
}
