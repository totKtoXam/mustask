using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Features.UserDirectory.RecordSignIn;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Domain.UserDirectory;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.UserDirectory.RecordSignIn;

/// <summary>tech-025 (ADR-0034): the sign-in upsert of the user projection.</summary>
public sealed class RecordSignInHandlerTests
{
    private const string Id = "7c9e6679-7425-40de-944b-e07fc1f90ae7";

    private readonly FakeProjectedUserStore _store = new();
    private readonly FakeClock _clock = new();
    private readonly ListLogger<RecordSignInHandler> _logger = new([]);

    private RecordSignInHandler Handler(IUnitOfWork? unitOfWork = null) =>
        new(_store, unitOfWork ?? new FakeUnitOfWork(), _clock, _logger);

    private static SignInProfile Profile(string lastName = "Смирнова", string? email = "anna.s@example.test") =>
        new(Id, "a.smirnova", "Анна", lastName, email);

    [Fact]
    public async Task Handle_FirstSignIn_CreatesTheRow()
    {
        var result = await Handler().Handle(new RecordSignInCommand(Profile()), CancellationToken.None);

        Assert.Equal(SignInOutcome.Added, result.Outcome);

        var user = Assert.Single(_store.Users);
        Assert.Equal(Id, user.Id);
        Assert.Equal("Анна Смирнова", user.DisplayName);
        Assert.Equal("anna.s@example.test", user.Email);
        Assert.True(user.IsActive);
        Assert.Equal(_clock.UtcNow, user.ChangedAt);
    }

    [Fact]
    public async Task Handle_SecondSignInWithChangedNames_UpdatesTheSameRow()
    {
        await Handler().Handle(new RecordSignInCommand(Profile()), CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddDays(1);

        var result = await Handler().Handle(new RecordSignInCommand(Profile(lastName: "Кузнецова")), CancellationToken.None);

        Assert.Equal(SignInOutcome.Updated, result.Outcome);

        var user = Assert.Single(_store.Users);
        Assert.Equal("Анна Кузнецова", user.DisplayName);
        Assert.Equal(_clock.UtcNow, user.ChangedAt);
    }

    [Fact]
    public async Task Handle_SecondSignInWithTheSameClaims_ChangesNothing()
    {
        await Handler().Handle(new RecordSignInCommand(Profile()), CancellationToken.None);
        var firstChange = _store.Users.Single().ChangedAt;
        _clock.UtcNow = _clock.UtcNow.AddDays(1);

        var result = await Handler().Handle(new RecordSignInCommand(Profile()), CancellationToken.None);

        Assert.Equal(SignInOutcome.Unchanged, result.Outcome);
        Assert.Single(_store.Users);
        Assert.Equal(firstChange, _store.Users.Single().ChangedAt);
    }

    [Fact]
    public async Task Handle_TwoFirstSignInsOfOnePersonAtOnce_LeaveOneRow()
    {
        var serial = new SerialUnitOfWork();

        var results = await Task.WhenAll(
            Handler(serial).Handle(new RecordSignInCommand(Profile()), CancellationToken.None),
            Handler(serial).Handle(new RecordSignInCommand(Profile(lastName: "Кузнецова")), CancellationToken.None));

        Assert.Single(_store.Users);
        Assert.Equal(1, results.Count(result => result.Outcome == SignInOutcome.Added));
        Assert.Equal(1, results.Count(result => result.Outcome == SignInOutcome.Updated));
    }

    [Fact]
    public async Task Handle_TwoDifferentPeopleAtOnce_LeaveTwoRows()
    {
        var serial = new SerialUnitOfWork();

        await Task.WhenAll(
            Handler(serial).Handle(new RecordSignInCommand(Profile()), CancellationToken.None),
            Handler(serial).Handle(new RecordSignInCommand(Profile() with { Id = "another-person" }), CancellationToken.None));

        Assert.Equal(2, _store.Users.Count);
    }

    [Fact]
    public async Task Handle_LegacyInactiveRow_BecomesActiveAgain()
    {
        _store.Seed(ProjectedUser.SignIn(Profile(), _clock.UtcNow), enabled: false);

        var result = await Handler().Handle(new RecordSignInCommand(Profile()), CancellationToken.None);

        Assert.Equal(SignInOutcome.Updated, result.Outcome);
        Assert.True(Assert.Single(_store.Users).IsActive);
    }

    [Theory]
    [InlineData(null, "a.smirnova")]
    [InlineData("", "a.smirnova")]
    [InlineData(Id, null)]
    [InlineData(Id, "")]
    public async Task Handle_MissingRequiredClaim_IsRefusedAndStoresNothing(string? id, string? username)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Handler().Handle(new RecordSignInCommand(new SignInProfile(id, username, "Анна", null, null)), CancellationToken.None));

        Assert.Equal(UserDirectoryErrorCodes.ValidationFailed, ex.Code);
        Assert.Empty(_store.Users);
    }

    [Fact]
    public async Task Handle_OversizedClaim_IsRefusedAndKeepsTheStoredRow()
    {
        await Handler().Handle(new RecordSignInCommand(Profile()), CancellationToken.None);

        var oversized = Profile(email: new string('e', UserDirectoryLimits.FieldMaxLength + 1));
        var ex = await Assert.ThrowsAsync<ValidationException>(() => Handler().Handle(new RecordSignInCommand(oversized), CancellationToken.None));

        Assert.Equal(UserDirectoryErrorCodes.ValueTooLong, Assert.Single(ex.Errors[SignInProfileValidation.EmailField]).Code);
        Assert.Equal("anna.s@example.test", Assert.Single(_store.Users).Email);
    }

    [Theory]
    [InlineData("Ан\0на")]
    [InlineData("Ан￾на")]
    public async Task Handle_OddCharacters_AreRefusedAsInvalid(string name)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Handler().Handle(new RecordSignInCommand(new SignInProfile(Id, "a.smirnova", name, null, null)), CancellationToken.None));

        Assert.Equal(UserDirectoryErrorCodes.ValueInvalid, Assert.Single(ex.Errors[SignInProfileValidation.FirstNameField]).Code);
        Assert.Empty(_store.Users);
    }

    [Fact]
    public async Task Handle_Logs_TheUserIdOnly_NeverANameOrAnEmail()
    {
        await Handler().Handle(new RecordSignInCommand(Profile()), CancellationToken.None);

        var logged = string.Join('\n', _logger.Messages);

        Assert.Contains(Id, logged, StringComparison.Ordinal);
        Assert.DoesNotContain("Смирнова", logged, StringComparison.Ordinal);
        Assert.DoesNotContain("anna.s@example.test", logged, StringComparison.Ordinal);
        Assert.DoesNotContain("a.smirnova", logged, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_NullCommand_Throws() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Handler().Handle(null!, CancellationToken.None));

    /// <summary>In-memory projection; a user is "locked" by the unit of work running one transaction at a time.</summary>
    private sealed class FakeProjectedUserStore : IProjectedUserStore
    {
        private readonly Dictionary<string, ProjectedUser> _users = new(StringComparer.Ordinal);

        public IReadOnlyCollection<ProjectedUser> Users => _users.Values;

        public Task<ProjectedUser?> LockAndLoadAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(_users.GetValueOrDefault(id));

        public void Add(ProjectedUser user) => _users.Add(user.Id, user);

        public void Seed(ProjectedUser user, bool enabled)
        {
            typeof(ProjectedUser).GetProperty(nameof(ProjectedUser.Enabled))!.SetValue(user, enabled);
            typeof(ProjectedUser).GetProperty(nameof(ProjectedUser.IsActive))!.SetValue(user, enabled);
            _users.Add(user.Id, user);
        }
    }

    /// <summary>Transactions that run one after another, as the per-user advisory lock makes them for one person.</summary>
    private sealed class SerialUnitOfWork : IUnitOfWork
    {
        private readonly SemaphoreSlim _gate = new(1, 1);

        public async Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);

            try
            {
                await Task.Yield();

                return await action(cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
