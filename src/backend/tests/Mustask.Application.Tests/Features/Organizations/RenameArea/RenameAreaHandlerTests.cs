using Mustask.Application.Features.Organizations.RenameArea;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.RenameArea;

/// <summary>mod-018 US-2 on fakes: the rename, the order of its answers (BR-AW-15) and its lock (ADR-0028 rule 8).</summary>
public sealed class RenameAreaHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Reports = Store.AddArea(Organization, "Отчёты");
            Quarterly = Store.AddArea(Organization, "Квартальные", Reports);
            Areas = new FakeAreaRepository(Store);
            Handler = new RenameAreaHandler(new FakeOrganizationReadModel(Store), Areas, new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public Area Reports { get; }

        public Area Quarterly { get; }

        public FakeAreaRepository Areas { get; }

        public RenameAreaHandler Handler { get; }

        public Task<RenameAreaResult> HandleAsync(Area area, string? name, string? organizationId = null) =>
            Handler.Handle(new RenameAreaCommand(organizationId ?? Organization.Id.ToString(), area.Id.ToString(), name), CancellationToken.None);
    }

    // US-2 · "A rename shows everywhere at once": the new name and the new path of the area.
    [Fact]
    public async Task Handle_Renames_AndAnswersTheNewPath()
    {
        var f = new Fixture();

        var renamed = await f.HandleAsync(f.Quarterly, " Годовые ");

        Assert.Equal("Годовые", renamed.Response.Name);
        Assert.Equal("Отчёты / Годовые", renamed.Response.Path);
        Assert.Equal(f.Reports.Id, renamed.Response.ParentId);
        Assert.Equal(1, f.Store.SaveCount);
    }

    // US-2 · the area's own name, in another case, is not a clash.
    [Fact]
    public async Task Handle_OwnNameInAnotherCase_IsNotAClash()
    {
        var f = new Fixture();

        var renamed = await f.HandleAsync(f.Reports, "ОТЧЁТЫ");

        Assert.Equal("ОТЧЁТЫ", renamed.Response.Name);
    }

    // US-2 · sibling names are unique ignoring case and spaces.
    [Fact]
    public async Task Handle_SiblingName_IsAreaNameTaken()
    {
        var f = new Fixture();
        f.Store.AddArea(f.Organization, "Архив");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Reports, " архив "));

        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, refusal.Code);
        Assert.Equal("Отчёты", f.Reports.Name);
        Assert.Equal(0, f.Store.SaveCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Handle_InvalidName_IsValidationFailedWithNameLength(string? name)
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Reports, name == "101" ? new string('я', 101) : name));

        Assert.Equal(["name"], refusal.Errors.Keys);
        Assert.Equal(OrganizationErrorCodes.NameLength, Assert.Single(refusal.Errors["name"]).Code);
    }

    // BR-AW-15: the address answers before the body.
    [Fact]
    public async Task Handle_Address_Answers404OutsideIn_BeforeTheBody()
    {
        var f = new Fixture();
        var foreign = f.Store.AddArea(f.Store.AddOrganization("Другая"), "Чужая");

        var org = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Reports, "", "abc"));
        var area = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(foreign, ""));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, org.Code);
        Assert.Equal(OrganizationErrorCodes.AreaNotFound, area.Code);
    }

    // ADR-0028 rule 8: a rename takes the area row alone, no tree lock (it moves nothing).
    [Fact]
    public async Task Handle_LocksTheAreaOnly_ThenSavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Quarterly, "Годовые");

        Assert.Equal([$"lock area update {f.Quarterly.Id}", "save"], f.Store.Calls);
    }

    // US-2 · two renames to one sibling name at the same moment keep one: the loser's save fails on the sibling index.
    [Fact]
    public async Task Handle_LosesTheSiblingNameRace_AnswersAreaNameTaken()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.AreaNameTaken));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Quarterly, "Годовые"));

        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, refusal.Code);
    }
}
