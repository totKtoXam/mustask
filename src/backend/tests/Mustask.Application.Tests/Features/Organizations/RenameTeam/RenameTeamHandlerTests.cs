using Mustask.Application.Features.Organizations.RenameTeam;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.RenameTeam;

/// <summary>
/// <c>RenameTeam</c> on fakes (feat-028 plan-backend § 7.2; US-1 "rename and description", BR-OL-3): what a rename changes and leaves,
/// the order of its answers (api-contract § 0.5) and its lock. Rewritten over the organization (mod-002 BR-M1-1, mod-017 BR-OT-11;
/// D-16): the answer carries no mark and no project, names are unique per organization.
/// </summary>
public sealed class RenameTeamHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Platform = Store.AddTeam(Organization, "Платформа");
            Handler = new RenameTeamHandler(new FakeTeamRepository(Store), new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public Team Platform { get; }

        public RenameTeamHandler Handler { get; }

        public RenameTeamCommand Command(string? name = "Платформа данных", string? teamId = null, string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), teamId ?? Platform.Id.ToString(), name);

        public Task<RenameTeamResult> HandleAsync(RenameTeamCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-1 · "rename": «Платформа» becomes «Платформа данных», trimmed; the answer is the team with its description.
    [Fact]
    public async Task Handle_FreeName_RenamesTheTeam_AnswersTheTeam()
    {
        var f = new Fixture();
        Assert.True(EntityDescription.TryCreate("О команде", out var description));
        f.Platform.Describe(description);

        var result = await f.HandleAsync(f.Command(name: "  Платформа данных "));

        Assert.Equal(new TeamResponse(f.Platform.Id, "Платформа данных", "О команде"), result.Response);
        Assert.Equal("ПЛАТФОРМА ДАННЫХ", f.Platform.NormalizedName);
    }

    // BR-M1-1 · "a name never conflicts with the object's own current name": «ПЛАТФОРМА» renames «Платформа».
    [Fact]
    public async Task Handle_OwnNameInAnotherCase_IsAllowed()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(name: "ПЛАТФОРМА"));

        Assert.Equal("ПЛАТФОРМА", result.Response.Name);
    }

    // BR-OL-3 · the name of another active team of the organization, in any letter case, is taken; nothing changes.
    [Fact]
    public async Task Handle_NameOfAnotherActiveTeamAnyCase_IsTeamNameTaken_NothingChanges()
    {
        var f = new Fixture();
        f.Store.AddTeam(f.Organization, "Аналитика");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: " аналитика ")));

        Assert.Equal(OrganizationErrorCodes.TeamNameTaken, refusal.Code);
        Assert.Equal("Платформа", f.Platform.Name);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // BR-OL-3 · another organization's name and a deleted team's name are free.
    [Fact]
    public async Task Handle_NameInAnotherOrganization_OrOfADeletedTeam_IsFree()
    {
        var f = new Fixture();
        f.Store.AddTeam(f.Store.AddOrganization("Другая"), "Аналитика");
        f.Store.AddTeam(f.Organization, "Ядро").Delete();

        var first = await f.HandleAsync(f.Command(name: "Аналитика"));
        var second = await f.HandleAsync(f.Command(name: "Ядро"));

        Assert.Equal("Аналитика", first.Response.Name);
        Assert.Equal("Ядро", second.Response.Name);
    }

    // BR-M1-1 · an empty name, spaces only and 101 characters are refused with the name reason; 100 characters are accepted.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_InvalidName_IsValidationFailedWithNameLength_BeforeTheUniquenessCheck(string? name)
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(name: name)));

        Assert.Equal(OrganizationErrorCodes.NameLength, Assert.Single(refusal.Errors["name"]).Code);
    }

    [Fact]
    public async Task Handle_NameOfOneHundredCharactersAfterTrimming_IsAccepted()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(name: " " + new string('я', 100) + " "));

        Assert.Equal(100, result.Response.Name.Length);
    }

    // US-8 · a malformed organization, and a team that is unknown, deleted, of another organization or malformed: by level.
    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(organizationId: "abc")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("deleted")]
    [InlineData("of another organization")]
    public async Task Handle_TeamNotAnActiveTeamOfTheOrganization_IsTeamNotFound_BeforeTheBody(string how)
    {
        var f = new Fixture();
        var foreign = f.Store.AddTeam(f.Store.AddOrganization("Другая организация"), "Чужая");
        var id = how switch
        {
            "malformed" => "not-a-team",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => foreign.Id.ToString(),
            _ => f.Platform.Id.ToString(),
        };

        if (how == "deleted")
        {
            f.Platform.Delete();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(name: " ", teamId: id)));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Equal("Чужая", foreign.Name);
    }

    // plan-backend § 7.2 · the team FOR NO KEY UPDATE, then one save; no project, link or area is locked.
    [Fact]
    public async Task Handle_LocksTheTeamForNoKeyUpdate_ThenSavesOnce_LocksNothingElse()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Equal([$"lock team no-key-update {f.Platform.Id}", "save"], f.Store.Calls);
    }

    [Fact]
    public async Task Handle_LosesTheNameRace_AnswersTeamNameTaken()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.TeamNameTaken));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.TeamNameTaken, refusal.Code);
    }
}
