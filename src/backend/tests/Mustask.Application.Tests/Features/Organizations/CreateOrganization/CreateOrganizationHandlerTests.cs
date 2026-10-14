using Mustask.Application.Features.Organizations.CreateOrganization;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.CreateOrganization;

/// <summary>
/// <c>CreateOrganization</c> on fakes (plan-backend § 10): what it stores, and the
/// order of its steps: the name, the pre-check, the seed before anything is tracked,
/// the save. The create right is the endpoint's, so no test here asks for it.
/// </summary>
public sealed class CreateOrganizationHandlerTests
{
    private const string Subject = "3f2b9c1e-6a4d-4e8b-9d27-0c5a1b7e4f60";

    private sealed class Fixture
    {
        public Fixture()
        {
            Seed = new FakeProcessSeed(Store);
            Clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 10, 2, 9, 30, 0, TimeSpan.Zero) };
            User = new FakeCurrentUser { DisplayName = "Иван Петров", SubjectId = Subject };
            Handler = new CreateOrganizationHandler(
                new FakeOrganizationRepository(Store),
                new FakeMembershipRepository(Store),
                Seed,
                Clock,
                User,
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public FakeProcessSeed Seed { get; }

        public FakeClock Clock { get; }

        public FakeCurrentUser User { get; }

        public CreateOrganizationHandler Handler { get; }

        public Task<CreateOrganizationResult> HandleAsync(string? name) =>
            Handler.Handle(new CreateOrganizationCommand(name), CancellationToken.None);
    }

    // US-1 · "An organization is created with its process": active, with the process the seed recorded for it, and who
    // created it and when (BR-M1-3, BR-M1-21). The creator is the token's subject, not the display name.
    [Fact]
    public async Task Handle_ValidName_CreatesAnActiveOrganizationWithItsSeededProcess_KeepsCreatorAndTime()
    {
        var f = new Fixture();

        var result = await f.HandleAsync("Пример организации");

        var stored = Assert.Single(f.Store.Organizations);
        Assert.Equal(new OrganizationResponse(stored.Id, "Пример организации"), result.Response);
        Assert.Equal(OrganizationStatus.Active, stored.Status);
        Assert.Equal(result.Response.Id, Assert.Single(f.Seed.SeededOrganizations));
        Assert.NotEqual(Guid.Empty, stored.ProcessId.Value);
        Assert.Equal(Subject, stored.CreatedBy);
        Assert.Equal(f.Clock.UtcNow, stored.CreatedAt);
    }

    // US-1 · "The name is trimmed and its length checked": «  Пример организации  » is stored as «Пример организации».
    [Fact]
    public async Task Handle_PaddedName_StoresTheTrimmedName()
    {
        var f = new Fixture();

        var result = await f.HandleAsync("  Пример организации  ");

        var stored = Assert.Single(f.Store.Organizations);
        Assert.Equal("Пример организации", result.Response.Name);
        Assert.Equal("Пример организации", stored.Name);
        Assert.Equal("ПРИМЕР ОРГАНИЗАЦИИ", stored.NormalizedName);
    }

    // US-1 · "The name is trimmed and its length checked": one and a hundred characters are the two ends of the rule.
    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task Handle_NameOfTheLimitLengths_Creates(int length)
    {
        var f = new Fixture();
        var name = new string('я', length);

        var result = await f.HandleAsync($" {name} ");

        Assert.Equal(name, result.Response.Name);
        Assert.Single(f.Store.Organizations);
    }

    // US-1 · "The name is trimmed and its length checked": an empty name, one of spaces only, and one over 100 characters
    // are refused with the length reason, and nothing is seeded or stored.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Handle_InvalidName_IsValidationFailedWithNameLength_NothingSeededOrStored(string? name)
    {
        var f = new Fixture();
        var submitted = name == "101" ? new string('я', 101) : name;

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(submitted));

        Assert.Equal(OrganizationErrorCodes.ValidationFailed, refusal.Code);
        Assert.Equal(["name"], refusal.Errors.Keys);
        Assert.Equal(OrganizationErrorCodes.NameLength, Assert.Single(refusal.Errors["name"]).Code);
        Assert.Empty(f.Store.Organizations);
        Assert.Empty(f.Seed.SeededOrganizations);
        Assert.Empty(f.Store.Calls);
    }

    // US-1 · "The name is unique instance-wide, deactivated organizations included": a name differing only in letter case and
    // surrounding spaces is refused, the answer names the holder's stored name, and it is the same for an active and a
    // deactivated holder (BR-M1-1). The refusal comes before the seed.
    [Theory]
    [InlineData(OrganizationStatus.Active)]
    [InlineData(OrganizationStatus.Deactivated)]
    public async Task Handle_NameOfActiveOrDeactivated_IsNameTakenNamingTheStoredName_NothingSeeded(OrganizationStatus status)
    {
        var f = new Fixture();
        var holder = f.Store.AddOrganization("Пример организации");

        if (status == OrganizationStatus.Deactivated)
        {
            holder.Deactivate();
        }

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync("  пример ОРГАНИЗАЦИИ "));

        Assert.Equal(OrganizationErrorCodes.OrganizationNameTaken, refusal.Code);
        var parameter = Assert.Single(refusal.Params);
        Assert.Equal("name", parameter.Key);
        Assert.Equal(ErrorParam.Text("Пример организации"), parameter.Value);
        Assert.Same(holder, Assert.Single(f.Store.Organizations));
        Assert.Empty(f.Seed.SeededOrganizations);
        Assert.Empty(f.Store.Calls);
    }

    // US-1 · "A failed process seed leaves nothing behind": whatever the seed throws but a cancellation, the answer is
    // the create-failed 500 with the seed's exception inside, and nothing is stored (BR-M1-3, status.md D-t).
    [Theory]
    [InlineData("invalid operation")]
    [InlineData("argument")]
    [InlineData("other")]
    public async Task Handle_SeedFails_IsCreateFailedWithTheSeedsExceptionInside_NothingStored(string kind)
    {
        var f = new Fixture();
        Exception failure = kind switch
        {
            "invalid operation" => new InvalidOperationException("The seed broke an obligation."),
            "argument" => new ArgumentException("The identifier is not well formed."),
            _ => new TimeoutException("The database did not answer."),
        };
        f.Seed.Failure = failure;

        var refusal = await Assert.ThrowsAsync<OrganizationCreateFailedException>(() => f.HandleAsync("Пример организации"));

        Assert.Equal(OrganizationErrorCodes.OrganizationCreateFailed, refusal.Code);
        Assert.Equal(500, refusal.StatusCode);
        Assert.Same(failure, refusal.InnerException);
        Assert.Empty(refusal.Params);
        Assert.Empty(f.Store.Organizations);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // The seed's cancellation is the request's, not a failed seed: it is not turned into the create-failed answer.
    [Fact]
    public async Task Handle_SeedIsCancelled_LetsTheCancellationThrough_NothingStored()
    {
        var f = new Fixture();
        f.Seed.Failure = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => f.HandleAsync("Пример организации"));

        Assert.Empty(f.Store.Organizations);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-1 · "A failed process seed leaves nothing behind": "a retry with the same name succeeds once the seed works".
    [Fact]
    public async Task Handle_RetryAfterAFailedSeed_Creates()
    {
        var f = new Fixture();
        f.Seed.Failure = new InvalidOperationException("The seed is down.");

        await Assert.ThrowsAsync<OrganizationCreateFailedException>(() => f.HandleAsync("Пример организации"));
        f.Seed.Failure = null;
        var result = await f.HandleAsync("Пример организации");

        Assert.Equal("Пример организации", Assert.Single(f.Store.Organizations).Name);
        Assert.Equal(result.Response.Id, Assert.Single(f.Seed.SeededOrganizations));
    }

    // plan-backend § 10 steps 3 and 4 / mod-001's obligation 2: the seed is called first, with the id the organization is
    // then created with, and the one save follows it. No lock is taken: an insert is closed by the unique index (§ 8.2).
    [Fact]
    public async Task Handle_SeedsFirstWithTheOrganizationsId_ThenSavesOnce_TakesNoLock()
    {
        var f = new Fixture();

        var result = await f.HandleAsync("Пример организации");

        Assert.Equal([$"seed {result.Response.Id}", "save"], f.Store.Calls);
    }

    // US-1 · "Two simultaneous creations of one name produce one organization": the loser's save fails on the name index and
    // the repository's answer, naming the submitted name, reaches the client unchanged; nothing is stored for it. The
    // rollback of the seeded rows is the database's, and the Api test checks it.
    [Fact]
    public async Task Handle_LosesTheNameRace_AnswersNameTakenFromTheRepository_StoresNothing()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new ConflictException(
            OrganizationErrorCodes.OrganizationNameTaken,
            new ErrorParams { ["name"] = ErrorParam.Text("Пример организации") }));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(" Пример организации "));

        Assert.Equal(OrganizationErrorCodes.OrganizationNameTaken, refusal.Code);
        Assert.Equal(ErrorParam.Text("Пример организации"), refusal.Params["name"]);
        Assert.Empty(f.Store.Organizations);
    }

    // mod-016 US-6 / BR-M1-25 · "doc-006 US-2 scenario 1": the creator becomes the organization's administrator, added by
    // themselves at the organization's creation time, with no role change — and nobody else holds a membership.
    [Fact]
    public async Task Handle_Creates_TheCreatorBecomesTheOnlyMember_AsAdmin_AddedByThemselves()
    {
        var f = new Fixture();

        var result = await f.HandleAsync("Пример организации");

        var membership = Assert.Single(f.Store.Memberships);
        Assert.Equal(result.Response.Id, membership.OrganizationId);
        Assert.Equal(Subject, membership.UserId);
        Assert.Equal(OrganizationRole.Admin, membership.Role);
        Assert.Equal(Subject, membership.AddedBy);
        Assert.Equal(f.Clock.UtcNow, membership.AddedAt);
        Assert.Null(membership.RoleChangedBy);
        Assert.Null(membership.RoleChangedAt);
    }

    // mod-016 US-6 · "doc-006 US-2 scenario 5 — a failed process seed leaves nothing behind", no membership either: the seed,
    // a refused name and a lost race each leave neither the organization nor its founder.
    [Fact]
    public async Task Handle_SeedFails_NoMembershipIsStored()
    {
        var f = new Fixture();
        f.Seed.Failure = new InvalidOperationException("The seed is down.");

        await Assert.ThrowsAsync<OrganizationCreateFailedException>(() => f.HandleAsync("Пример организации"));

        Assert.Empty(f.Store.Organizations);
        Assert.Empty(f.Store.Memberships);
    }

    [Fact]
    public async Task Handle_NameTaken_NoMembershipIsStored()
    {
        var f = new Fixture();
        f.Store.AddOrganization("Пример организации");

        await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync("Пример организации"));

        Assert.Empty(f.Store.Memberships);
    }

    [Fact]
    public async Task Handle_LosesTheNameRace_NoMembershipIsStored()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.OrganizationNameTaken, new ErrorParams { ["name"] = ErrorParam.Text("x") }));

        await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync("Пример организации"));

        Assert.Empty(f.Store.Organizations);
        Assert.Empty(f.Store.Memberships);
    }

    // mod-016 US-3 · "A creator the projection does not know yet still gets the creation memberships" (BR-M1-25): the handler
    // never consults the user projection (it has no reference to it), so a creator it does not know is stored the same.
    [Fact]
    public async Task Handle_TheMembershipIsWrittenInTheSameSaveAsTheOrganization()
    {
        var f = new Fixture();

        await f.HandleAsync("Пример организации");

        Assert.Equal(1, f.Store.SaveCount);
        Assert.Single(f.Store.Organizations);
        Assert.Single(f.Store.Memberships);
    }

    // Two creations are two organizations: each is seeded under its own key.
    [Fact]
    public async Task Handle_TwoNames_SeedsEachUnderItsOwnId()
    {
        var f = new Fixture();

        var first = await f.HandleAsync("Альфа");
        var second = await f.HandleAsync("Бета");

        Assert.NotEqual(first.Response.Id, second.Response.Id);
        Assert.Equal([first.Response.Id, second.Response.Id], f.Seed.SeededOrganizations);
        Assert.Equal(2, f.Store.Organizations.Count);
    }
}
