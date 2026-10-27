using Mustask.Application.Features.Organizations.CreateArea;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.CreateArea;

/// <summary>
/// mod-018 US-1 / US-2 on fakes: what the creation stores, the order of its answers (BR-AW-15) and its lock order (ADR-0028 rule 8).
/// </summary>
public sealed class CreateAreaHandlerTests
{
    private sealed class Fixture
    {
        public Fixture(OrganizationRole role = OrganizationRole.Admin)
        {
            Areas = new FakeAreaRepository(Store);
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddTeam(Organization);
            Handler = new CreateAreaHandler(
                new FakeOrganizationReadModel(Store),
                new FakeProjectRepository(Store),
                new FakeTeamRepository(Store),
                Areas,
                new FakeAreaLinkRepository(Store),
                new FakeOrganizationAccess(role),
                new FakeCurrentUser(),
                new FakeClock(),
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public Project Project { get; }

        public Team Platform { get; }

        public FakeAreaRepository Areas { get; }

        public CreateAreaHandler Handler { get; }

        public CreateAreaCommand Command(
            string? name = "Отчёты",
            string? parentId = null,
            string? organizationId = null,
            string? description = null,
            IReadOnlyList<string?>? projectIds = null,
            IReadOnlyList<string?>? teamIds = null) =>
            new(organizationId ?? Organization.Id.ToString(), name, parentId, description, projectIds, teamIds);

        public Task<CreateAreaResult> HandleAsync(CreateAreaCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-1 · "The administrator builds the example tree": «Отчёты» at the top, «Квартальные» under it, the path is the names joined.
    [Fact]
    public async Task Handle_TopLevelAndChild_CreatesBothWithTheirPaths()
    {
        var f = new Fixture();

        var reports = await f.HandleAsync(f.Command(name: " Отчёты ", description: "Всё про отчёты"));
        var quarterly = await f.HandleAsync(f.Command(name: "Квартальные", parentId: reports.Response.Id.ToString()));

        Assert.Equal(new AreaResponse(reports.Response.Id, null, "Отчёты", "Всё про отчёты", "Отчёты"), reports.Response);
        Assert.Equal(reports.Response.Id, quarterly.Response.ParentId);
        Assert.Equal("Отчёты / Квартальные", quarterly.Response.Path);
        Assert.Equal(2, f.Store.Areas.Count);
    }

    // US-2 · "An area is created with its projects and teams at once": all stored, nothing granted.
    [Fact]
    public async Task Handle_WithProjectsAndTeams_StoresTheLinksInTheSameSave()
    {
        var f = new Fixture();

        var created = await f.HandleAsync(f.Command(
            projectIds: [f.Project.Id.ToString()], teamIds: [f.Platform.Id.ToString()]));

        Assert.Equal(created.Response.Id, Assert.Single(f.Store.AreaProjects).AreaId);
        Assert.Equal(f.Project.Id, f.Store.AreaProjects.Single().ProjectId);
        Assert.Equal(f.Platform.Id, f.Store.AreaTeams.Single().TeamId);
        Assert.Equal(1, f.Store.SaveCount);
    }

    // BR-AW-15 step 1: behind the filter the organization is well formed; an unknown or deactivated one is 404.
    [Theory]
    [InlineData("abc")]
    [InlineData("unknown")]
    [InlineData("deactivated")]
    public async Task Handle_OrganizationNotFound_Is404_BeforeTheBody(string how)
    {
        var f = new Fixture();

        if (how == "deactivated")
        {
            f.Organization.Deactivate();
        }

        var id = how switch
        {
            "abc" => "abc",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => f.Organization.Id.ToString(),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(name: "", organizationId: id)));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    // US-1 · "A name is required, at most 100 characters; a description at most 2 000": every reason at once, before any lock.
    [Fact]
    public async Task Handle_InvalidNameAndDescription_IsOneValidationFailed_BeforeAnyLock()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(
            f.Command(name: "   ", description: new string('я', 2001))));

        Assert.Equal(OrganizationErrorCodes.ValidationFailed, refusal.Code);
        Assert.Equal(["name", "description"], refusal.Errors.Keys.Order(StringComparer.Ordinal).Reverse());
        Assert.Empty(f.Store.Calls);
    }

    // BR-AW-15 step 3 before 4: the body's values answer before the other ids of the body are judged.
    [Fact]
    public async Task Handle_InvalidNameAndUnknownProject_AnswersTheNameFirst()
    {
        var f = new Fixture();

        await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(
            f.Command(name: "", projectIds: [Guid.CreateVersion7().ToString()])));
    }

    // US-1 · a parent that is malformed, unknown or of another organization is «Область не найдена».
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Handle_ParentNotAnAreaOfTheOrganization_IsAreaNotFound(string how)
    {
        var f = new Fixture();
        var foreign = f.Store.AddArea(f.Store.AddOrganization("Другая"), "Чужая");
        var parent = how switch
        {
            "malformed" => "00000000-0000-0000-0000-000000000000",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => foreign.Id.ToString(),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(parentId: parent)));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-2 · a project or a team that is unknown, of another organization or deactivated is 404, and nothing is created.
    [Theory]
    [InlineData("unknown project")]
    [InlineData("deactivated project")]
    [InlineData("unknown team")]
    [InlineData("deactivated team")]
    public async Task Handle_OtherEndNotUsable_Is404_AndNothingIsStored(string how)
    {
        var f = new Fixture();
        var project = f.Store.AddProject(f.Organization, "Второй", "SEC");
        var team = f.Store.AddTeam(f.Organization, "Вторая");
        project.Deactivate();
        team.Delete();

        var command = how switch
        {
            "unknown project" => f.Command(projectIds: [Guid.CreateVersion7().ToString()]),
            "deactivated project" => f.Command(projectIds: [project.Id.ToString()]),
            "unknown team" => f.Command(teamIds: [Guid.CreateVersion7().ToString()]),
            _ => f.Command(teamIds: [team.Id.ToString()]),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(command));

        Assert.Equal(how.EndsWith("project", StringComparison.Ordinal) ? OrganizationErrorCodes.ProjectNotFound : OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Empty(f.Store.Areas);
        Assert.Empty(f.Store.AreaProjects);
        Assert.Empty(f.Store.AreaTeams);
    }

    // US-1 · sibling names are unique ignoring case and spaces, per parent (BR-AW-3); «Квартальные» is free under another parent.
    [Fact]
    public async Task Handle_SiblingNameAnyCase_IsAreaNameTaken_SameNameUnderAnotherParentIsFree()
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Organization, "Отчёты");
        f.Store.AddArea(f.Organization, "Квартальные", reports);
        var archive = f.Store.AddArea(f.Organization, "Архив");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(
            f.Command(name: "квартальные ", parentId: reports.Id.ToString())));
        var elsewhere = await f.HandleAsync(f.Command(name: "Квартальные", parentId: archive.Id.ToString()));
        var topLevel = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: " ОТЧЁТЫ")));

        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, refusal.Code);
        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, topLevel.Code);
        Assert.Equal(archive.Id, elsewhere.Response.ParentId);
    }

    // US-1 · "The tree is at most 10 levels deep at creation".
    [Fact]
    public async Task Handle_UnderLevelTen_IsAreaDepthExceeded()
    {
        var f = new Fixture();
        Area? parent = null;

        for (var level = 1; level <= 10; level++)
        {
            parent = f.Store.AddArea(f.Organization, $"Уровень {level}", parent);
        }

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(parentId: parent!.Id.ToString())));

        Assert.Equal(OrganizationErrorCodes.AreaDepthExceeded, refusal.Code);
        Assert.Equal(10, f.Store.Areas.Count);
    }

    // US-6 / BR-AW-15 step 0: a role without area.create cannot make a top-level area; a role without the link codes cannot name ends.
    // The second code answers first of all, so an unknown parent id learns nothing.
    [Fact]
    public async Task Handle_MemberTopLevel_IsForbidden_BeforeAnyRead()
    {
        var f = new Fixture(OrganizationRole.Member);

        var refusal = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.AccessForbidden, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    [Fact]
    public async Task Handle_MemberUnderAParent_NeedsNoSecondCode_ButNamingEndsIsForbidden()
    {
        var f = new Fixture(OrganizationRole.Member);
        var parent = f.Store.AddArea(f.Organization, "Отчёты");

        var created = await f.HandleAsync(f.Command(name: "Квартальные", parentId: parent.Id.ToString()));
        var refusal = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(
            f.Command(name: "Годовые", parentId: parent.Id.ToString(), projectIds: [f.Project.Id.ToString()])));

        Assert.Equal(parent.Id, created.Response.ParentId);
        Assert.Equal(OrganizationErrorCodes.AccessForbidden, refusal.Code);
        Assert.Empty(f.Store.AreaProjects);
    }

    // ADR-0028 rule 8: the tree, then the parent, then the teams, then one save; the project is checked unlocked.
    [Fact]
    public async Task Handle_LockOrder_TreeParentTeams_ThenOneSave()
    {
        var f = new Fixture();
        var parent = f.Store.AddArea(f.Organization, "Отчёты");

        await f.HandleAsync(f.Command(name: "Квартальные", parentId: parent.Id.ToString(), teamIds: [f.Platform.Id.ToString()]));

        Assert.Equal(
            [$"lock area tree {f.Organization.Id}", $"lock area share {parent.Id}", $"lock teams share {f.Platform.Id}", "save"],
            f.Store.Calls);
    }

    // ADR-0028 A-9: a parent seen by the unlocked read but absent from the lock statement's answer is 404, never a later read.
    [Fact]
    public async Task Handle_ParentDeletedBetweenTheReadAndTheLock_IsAreaNotFound()
    {
        var f = new Fixture();
        var parent = f.Store.AddArea(f.Organization, "Отчёты");
        f.Areas.VanishedAfterTheLock.Add(parent.Id);

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(parentId: parent.Id.ToString())));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-1 · "Two creations of one sibling name at the same moment keep one": the loser's save fails on the sibling index.
    [Fact]
    public async Task Handle_LosesTheSiblingNameRace_AnswersAreaNameTaken_StoresNothing()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.AreaNameTaken));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: "Квартальные")));

        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, refusal.Code);
        Assert.Empty(f.Store.Areas);
    }
}
