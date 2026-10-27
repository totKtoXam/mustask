using Mustask.Application.Features.Organizations.RenameOrganization;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.RenameOrganization;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/name</c> on fakes (US-2): what a
/// rename stores, which names it allows and refuses, and the order of its
/// answers (plan-backend § 5.3, api-contract § 0.4).
/// </summary>
public sealed class RenameOrganizationHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization("Бета");
            Handler = new RenameOrganizationHandler(new FakeOrganizationRepository(Store), new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public RenameOrganizationHandler Handler { get; }

        public RenameOrganizationCommand Command(string? name, string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), name);

        public Task<RenameOrganizationResult> HandleAsync(RenameOrganizationCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-2 · "The organization is renamed": the name is trimmed, stored and answered as stored.
    [Fact]
    public async Task Handle_FreeName_StoresTheTrimmedNameAndAnswersIt()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command("  Пример организации 2 "));

        Assert.Equal(new OrganizationResponse(f.Organization.Id, "Пример организации 2"), result.Response);
        Assert.Equal("Пример организации 2", f.Organization.Name);
        Assert.Equal("ПРИМЕР ОРГАНИЗАЦИИ 2", f.Organization.NormalizedName);
        Assert.True(f.Organization.IsActive);
    }

    // US-2 · "its projects, teams and areas are unchanged", and the process is the one it had.
    [Fact]
    public async Task Handle_Renames_LeavesProcessProjectsTeamsAndAreasUnchanged()
    {
        var f = new Fixture();
        var project = f.Store.AddProject(f.Organization);
        var platform = f.Store.AddLinkedTeam(f.Organization, project);
        f.Store.AddArea(f.Organization, "Отчёты");
        var processId = f.Organization.ProcessId;
        var before = Snapshot(f.Store);

        await f.HandleAsync(f.Command("Пример организации 2"));

        Assert.Equal(before, Snapshot(f.Store));
        Assert.Equal(processId, f.Organization.ProcessId);
    }

    // US-2 · "renaming it to «пример организации 2», its own name in another letter case, is allowed (BR-M1-1)":
    // a name never conflicts with the object's own current name, in the same case or another.
    [Theory]
    [InlineData("бета")]
    [InlineData("  БЕТА  ")]
    [InlineData("Бета")]
    public async Task Handle_OwnNameInAnotherCase_IsAllowedAndStoredAsSubmitted(string submitted)
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(submitted));

        Assert.Equal(submitted.Trim(), result.Response.Name);
        Assert.Equal(submitted.Trim(), f.Organization.Name);
        Assert.Equal("БЕТА", f.Organization.NormalizedName);
        Assert.Equal(1, f.Store.SaveCount);
    }

    // US-2 · "A rename to a taken ... name is refused" (BR-M1-1): another organization holds the name in any letter case and
    // with surrounding spaces, active or deactivated alike, and the answer names the holder's stored name, not the submitted one.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_NameOfAnotherOrganizationAnyCase_IsNameTakenWithTheStoredName_NotRenamed(bool holderDeactivated)
    {
        var f = new Fixture();
        var holder = f.Store.AddOrganization("Альфа");

        if (holderDeactivated)
        {
            holder.Deactivate();
        }

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command("  альфа ")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNameTaken, refusal.Code);
        Assert.Equal(ErrorParam.Text("Альфа"), refusal.Params["name"]);
        Assert.Equal("Бета", f.Organization.Name);
        Assert.Equal("Альфа", holder.Name);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-2 · "refused with «Название должно содержать от 1 до 100 символов»": a null, an empty, a spaces-only and a
    // 101-character name; the organization keeps its name.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Handle_InvalidName_IsValidationFailedWithNameLength_NotRenamed(string? name)
    {
        var f = new Fixture();
        var submitted = name == "101" ? new string('я', 101) : name;

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(submitted)));

        Assert.Equal(OrganizationErrorCodes.ValidationFailed, refusal.Code);
        Assert.Equal(["name"], refusal.Errors.Keys);
        Assert.Equal(OrganizationErrorCodes.NameLength, Assert.Single(refusal.Errors["name"]).Code);
        Assert.Equal("Бета", f.Organization.Name);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-2 · the length rule's upper edge: 100 characters after trimming are accepted.
    [Fact]
    public async Task Handle_NameOfOneHundredCharactersAfterTrimming_IsRenamed()
    {
        var f = new Fixture();
        var name = new string('я', 100);

        var result = await f.HandleAsync(f.Command($"  {name} "));

        Assert.Equal(name, result.Response.Name);
        Assert.Equal(name, f.Organization.Name);
    }

    // US-2 · "A deactivated organization takes no change" and "An unknown organization answers not found" (BR-M1-20): behind
    // the filter the id is well formed and active; if it is not, the address answers before the body is looked at.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("deactivated")]
    public async Task Handle_OrganizationNotActive_IsOrganizationNotFound_BeforeTheBody_NothingChanges(string how)
    {
        var f = new Fixture();
        var holder = f.Store.AddOrganization("Альфа");
        var organizationId = how switch
        {
            "malformed" => "not-an-id",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => f.Organization.Id.ToString(),
        };

        if (how == "deactivated")
        {
            f.Organization.Deactivate();
        }

        // The name is invalid, and it is also another organization's: the address answers first.
        var invalid = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command("", organizationId)));
        var taken = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(holder.Name, organizationId)));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, invalid.Code);
        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, taken.Code);
        Assert.Equal("Бета", f.Organization.Name);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // plan-backend § 8.2: the organization is locked once, before anything is decided, then one save; a malformed id
    // takes no lock at all.
    [Fact]
    public async Task Handle_LocksTheOrganizationFirst_ThenSavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command("Гамма"));
        Assert.Equal([$"lock organization {f.Organization.Id}", "save"], f.Store.Calls);

        f.Store.Calls.Clear();
        await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command("Дельта", "not-an-id")));
        Assert.Empty(f.Store.Calls);
    }

    // US-2 · "Two renames to one free name keep one": the loser's save fails on the name index, and the repository's
    // answer (the submitted, trimmed name, plan-backend § 6.3) reaches the client unchanged.
    [Fact]
    public async Task Handle_LosesTheNameRace_AnswersNameTakenFromTheSave()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new ConflictException(
            OrganizationErrorCodes.OrganizationNameTaken,
            new ErrorParams { ["name"] = ErrorParam.Text("Гамма") }));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(" Гамма ")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNameTaken, refusal.Code);
        Assert.Equal(ErrorParam.Text("Гамма"), refusal.Params["name"]);
    }

    /// <summary>Everything beneath the organization, as the rename must leave it: one line per fact, so two snapshots compare by value.</summary>
    private static List<string> Snapshot(InMemoryOrganizations store) =>
    [
        .. store.Projects.Select(project => $"project {project.Id} {project.Name} {project.Key} {project.Status}"),
        .. store.Teams.Select(team => $"team {team.Id} {team.Name} {team.Status}"),
        .. store.Areas.Select(area => $"area {area.Id} {area.ParentId} {area.Name}"),
    ];
}
