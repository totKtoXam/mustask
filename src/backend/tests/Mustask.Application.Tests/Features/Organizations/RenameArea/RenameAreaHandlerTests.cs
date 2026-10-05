using Mustask.Application.Features.Organizations.RenameArea;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.RenameArea;

/// <summary>
/// <c>RenameArea</c> on fakes (lane L-22): what a rename changes and keeps, and
/// the order of its answers (plan-backend § 5.3, api-contract § 0.4, § 4.3) and
/// its lock (§ 8.2), copied from the reference use case <c>CreateArea</c>.
/// </summary>
public sealed class RenameAreaHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddTeam(Project);
            Reports = Store.AddArea(Project.Root, "Отчёты");
            Quarterly = Store.AddArea(Reports, "Квартальные", Platform.Team);
            Handler = new RenameAreaHandler(
                new FakeProjectRepository(Store),
                new FakeAreaRepository(Store),
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public NewTeam Platform { get; }

        /// <summary>A child of the root.</summary>
        public Area Reports { get; }

        /// <summary>A child of <see cref="Reports"/>, owned explicitly by <see cref="Platform"/>: the area the tests rename.</summary>
        public Area Quarterly { get; }

        public RenameAreaHandler Handler { get; }

        public RenameAreaCommand Command(string? name = "Годовые", string? areaId = null, string? projectId = null, string? organizationId = null) =>
            new(
                organizationId ?? Organization.Id.ToString(),
                projectId ?? Project.Project.Id.ToString(),
                areaId ?? Quarterly.Id.ToString(),
                name);

        public Task<RenameAreaResult> HandleAsync(RenameAreaCommand command) => Handler.Handle(command, CancellationToken.None);

        public AreaTree Tree() =>
            new(Store.Areas
                .Where(area => area.ProjectId == Project.Project.Id)
                .Select(area => new AreaNode(area.Id, area.ParentId, area.Name, area.OwnerTeamId, area.DefaultOfTeamId)));
    }

    // US-5 · "A child area is renamed": «Отчёты / Квартальные» with children of its own becomes «Отчёты / Годовые»,
    // with its children and its owner unchanged.
    [Fact]
    public async Task Handle_ChildWithChildren_RenamesIt_KeepsChildrenAndOwner()
    {
        var f = new Fixture();
        var firstQuarter = f.Store.AddArea(f.Quarterly, "Первый квартал");
        var secondQuarter = f.Store.AddArea(f.Quarterly, "Второй квартал", f.Platform.Team);

        var result = await f.HandleAsync(f.Command(name: " Годовые "));

        Assert.Equal(new AreaResponse(f.Quarterly.Id, f.Project.Project.Id, f.Reports.Id, "Годовые", f.Platform.Team.Id), result.Response);

        var tree = f.Tree();
        Assert.Equal("Отчёты / Годовые", tree.PathOf(f.Quarterly.Id));
        Assert.Equal(f.Reports.Id, f.Quarterly.ParentId);
        Assert.Equal(
            ["Второй квартал", "Первый квартал"],
            tree.ChildrenOf(f.Quarterly.Id).Select(node => node.Name).Order(StringComparer.Ordinal));
        Assert.Equal(f.Platform.Team.Id, f.Quarterly.OwnerTeamId);
        Assert.Equal(f.Platform.Team.Id, secondQuarter.OwnerTeamId);
        Assert.Null(firstQuarter.OwnerTeamId);
        Assert.Equal(new ResolvedOwner(f.Platform.Team.Id, true), tree.ResolveOwner(firstQuarter.Id));
        Assert.Null(f.Quarterly.DefaultOfTeamId);
    }

    // US-5 · "Sibling names are unique, compared ignoring case and spaces": the area's own name, in any case, is not
    // "taken by a sibling", so a change of case (or no change at all) is a rename like any other.
    [Theory]
    [InlineData("Квартальные")]
    [InlineData("  КВАРТАЛЬНЫЕ ")]
    public async Task Handle_OwnNameInAnotherCaseOrUnchanged_IsAllowed(string name)
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(name: name));

        Assert.Equal(name.Trim(), result.Response.Name);
        Assert.Equal(name.Trim(), f.Quarterly.Name);
        Assert.Equal(1, f.Store.SaveCount);
    }

    // BR-M1-20: behind the filter the organization id is well formed; if it were not, the outermost level answers.
    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(organizationId: "abc", name: "")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }

    // US-3 · "A project id names no project of the organization" and "a project is deactivated" (BR-M1-20, BR-M1-22):
    // the project level answers before the area and the body are looked at.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deactivated")]
    [InlineData("under a deactivated organization")]
    public async Task Handle_ProjectNotReachable_IsProjectNotFound_BeforeTheAreaAndTheBody(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Store.AddOrganization("Другая организация"), "Чужой", "OTH");
        var projectId = how switch
        {
            "malformed" => "not-an-id",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => other.Project.Id.ToString(),
            _ => f.Project.Project.Id.ToString(),
        };

        if (how == "deactivated")
        {
            f.Project.Project.Deactivate();
        }

        if (how == "under a deactivated organization")
        {
            f.Organization.Deactivate();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(projectId: projectId, name: "", areaId: "x")));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
        Assert.Equal("Квартальные", f.Quarterly.Name);
    }

    // BR-M1-22 · an area id that is null, malformed, unknown, or of another project (its root included) answers
    // area.not_found, before the body is looked at.
    [Theory]
    [InlineData("null")]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    [InlineData("root of another project")]
    public async Task Handle_AreaNotOfTheProject_IsAreaNotFound_BeforeTheBody(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Organization, "Второй проект", "SEC");
        var otherChild = f.Store.AddArea(other.Root, "Чужая область");
        var command = f.Command(name: "") with
        {
            AreaId = how switch
            {
                "null" => null,
                "malformed" => "00000000-0000-0000-0000-000000000000",
                "unknown" => Guid.CreateVersion7().ToString(),
                "of another project" => otherChild.Id.ToString(),
                _ => other.Root.Id.ToString(),
            },
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(command));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
        Assert.Equal("Чужая область", otherChild.Name);
        Assert.Equal("Второй проект", other.Root.Name);
    }

    // US-5 · "The root comes with the project and follows it": a direct rename of the root is refused and changes nothing
    // (BR-M1-11). The rule about the addressed area answers before the body's values (api-contract § 0.4 step 2 before 3).
    [Theory]
    [InlineData("Новое название")]
    [InlineData("")]
    public async Task Handle_Root_IsRootIsFixed_BeforeTheBody(string name)
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: name, areaId: f.Project.Root.Id.ToString())));

        Assert.Equal(OrganizationErrorCodes.AreaRootIsFixed, refusal.Code);
        Assert.Equal("Пример проекта", f.Project.Root.Name);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-5 · "an empty name, or one longer than 100 characters, is refused with «Название должно содержать от 1 до 100
    // символов»": one reason on `name`, and nothing is saved.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Handle_InvalidName_IsValidationFailedWithNameLength_NothingSaved(string? name)
    {
        var f = new Fixture();
        var submitted = name == "101" ? new string('я', 101) : name;

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(name: submitted)));

        Assert.Equal(OrganizationErrorCodes.ValidationFailed, refusal.Code);
        Assert.Equal(["name"], refusal.Errors.Keys);
        Assert.Equal(OrganizationErrorCodes.NameLength, Assert.Single(refusal.Errors["name"]).Code);
        Assert.Equal("Квартальные", f.Quarterly.Name);
        Assert.Equal([$"lock area update {f.Quarterly.Id}"], f.Store.Calls);
    }

    // US-5 · "Sibling names are unique, compared ignoring case and spaces ... or a child of «Отчёты» is renamed to it".
    [Fact]
    public async Task Handle_ToSiblingNameAnyCase_IsAreaNameTaken_NameUnchanged()
    {
        var f = new Fixture();
        f.Store.AddArea(f.Reports, "Годовые");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: " годовые ")));

        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, refusal.Code);
        Assert.Equal("Квартальные", f.Quarterly.Name);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-5 · "«Квартальные» is still free under another parent": a sibling of another parent, and the root, are not siblings.
    [Fact]
    public async Task Handle_ToNameHeldUnderAnotherParentOrByTheRoot_IsAllowed()
    {
        var f = new Fixture();
        var archive = f.Store.AddArea(f.Project.Root, "Архив");
        f.Store.AddArea(archive, "Месячные");

        var elsewhere = await f.HandleAsync(f.Command(name: "месячные"));
        var rootsName = await f.HandleAsync(f.Command(name: "Пример проекта"));

        Assert.Equal("месячные", elsewhere.Response.Name);
        Assert.Equal("Пример проекта", rootsName.Response.Name);
        Assert.Equal("Пример проекта", f.Quarterly.Name);
        Assert.Equal(f.Reports.Id, rootsName.Response.ParentId);
    }

    // plan-backend § 8.2: the area FOR UPDATE, nothing else, then one save. The project and the team are not locked.
    [Fact]
    public async Task Handle_LocksTheAreaForUpdate_ThenSavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Equal([$"lock area update {f.Quarterly.Id}", "save"], f.Store.Calls);
    }

    // US-5 · the sibling-name race: the loser's save fails on the sibling index, and the repository's answer reaches the
    // client unchanged (plan-backend § 6.3).
    [Fact]
    public async Task Handle_LosesTheSiblingNameRace_AnswersAreaNameTaken()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.AreaNameTaken));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: "Годовые")));

        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, refusal.Code);
        Assert.Equal(1, f.Store.SaveCount);
    }
}
