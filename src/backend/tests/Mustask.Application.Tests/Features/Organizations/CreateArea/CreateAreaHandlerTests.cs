using Mustask.Application.Features.Organizations.CreateArea;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.CreateArea;

/// <summary>
/// mod-018 US-1 on fakes: what the creation of a department or a sub-area stores, the order of its answers (BR-AW-15) and its lock
/// order (ADR-0031, ADR-0028 rule 8 reworked).
/// </summary>
public sealed class CreateAreaHandlerTests
{
    private sealed class Fixture
    {
        public Fixture(OrganizationRole role = OrganizationRole.Admin)
        {
            Areas = new FakeAreaRepository(Store);
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization).Project;
            Node = Store.RootOf(Project);
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

        /// <summary>The project's node.</summary>
        public Area Node { get; }

        public Team Platform { get; }

        public FakeAreaRepository Areas { get; }

        public CreateAreaHandler Handler { get; }

        public CreateAreaCommand Command(
            string? name = "Отчёты",
            string? parentId = null,
            string? organizationId = null,
            string? description = null,
            IReadOnlyList<string?>? teamIds = null) =>
            new(organizationId ?? Organization.Id.ToString(), name, parentId, description, teamIds);

        public Task<CreateAreaResult> HandleAsync(CreateAreaCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-1 · a department at the top; a sub-area under a project's node, then under that sub-area; the path is the names joined.
    [Fact]
    public async Task Handle_DepartmentAndSubAreas_CreatesThemTypedWithTheirPaths()
    {
        var f = new Fixture();

        var department = await f.HandleAsync(f.Command(name: " Разработка ", description: "Всё про разработку"));
        var quarterly = await f.HandleAsync(f.Command(name: "Квартальные", parentId: f.Node.Id.ToString()));
        var deeper = await f.HandleAsync(f.Command(name: "Q1", parentId: quarterly.Response.Id.ToString()));

        Assert.Equal(new AreaResponse(department.Response.Id, null, "department", null, "Разработка", "Всё про разработку", "Разработка"), department.Response);
        Assert.Equal(("area", f.Project.Id, f.Node.Id, "Пример проекта / Квартальные"), (quarterly.Response.Type, quarterly.Response.ProjectId, quarterly.Response.ParentId, quarterly.Response.Path));
        Assert.Equal((f.Project.Id, "Пример проекта / Квартальные / Q1"), (deeper.Response.ProjectId, deeper.Response.Path));
        Assert.Equal(4, f.Store.Areas.Count);
    }

    // OQ-7 A · below a department only a project's node stands: a sub-area there is type_not_allowed, and nothing is stored.
    [Fact]
    public async Task Handle_UnderADepartment_IsTypeNotAllowed()
    {
        var f = new Fixture();
        var department = f.Store.AddDepartment(f.Organization, "Разработка");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(parentId: department.Id.ToString())));

        Assert.Equal(OrganizationErrorCodes.AreaTypeNotAllowed, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-4 · "A department is created with its teams at once": all stored in the same save (OQ-8 A: a department only).
    [Fact]
    public async Task Handle_DepartmentWithTeams_StoresTheLinksInTheSameSave()
    {
        var f = new Fixture();

        var created = await f.HandleAsync(f.Command(teamIds: [f.Platform.Id.ToString()]));

        Assert.Equal(created.Response.Id, Assert.Single(f.Store.AreaTeams).AreaId);
        Assert.Equal(f.Platform.Id, f.Store.AreaTeams.Single().TeamId);
        Assert.Equal(1, f.Store.SaveCount);
    }

    // OQ-8 A · teams named for a sub-area: link_type_not_allowed, nothing created.
    [Fact]
    public async Task Handle_SubAreaWithTeams_IsLinkTypeNotAllowed_NothingStored()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(
            f.Command(parentId: f.Node.Id.ToString(), teamIds: [f.Platform.Id.ToString()])));

        Assert.Equal(OrganizationErrorCodes.AreaLinkTypeNotAllowed, refusal.Code);
        Assert.Single(f.Store.Areas);
        Assert.Empty(f.Store.AreaTeams);
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
    public async Task Handle_InvalidNameAndUnknownTeam_AnswersTheNameFirst()
    {
        var f = new Fixture();

        await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(
            f.Command(name: "", teamIds: [Guid.CreateVersion7().ToString()])));
    }

    // US-1 · a parent that is malformed, unknown or of another organization is «Область не найдена».
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Handle_ParentNotAnAreaOfTheOrganization_IsAreaNotFound(string how)
    {
        var f = new Fixture();
        var foreign = f.Store.AddDepartment(f.Store.AddOrganization("Другая"), "Чужая");
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

    // OQ-6 A · a deactivated project's node is read-only: a sub-area below it is «Проект не найден».
    [Fact]
    public async Task Handle_UnderADeactivatedProject_IsProjectNotFound()
    {
        var f = new Fixture();
        f.Project.Deactivate();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(parentId: f.Node.Id.ToString())));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-4 · a team that is unknown or deleted is 404, and nothing is created.
    [Theory]
    [InlineData("unknown team")]
    [InlineData("deleted team")]
    public async Task Handle_TeamNotUsable_Is404_AndNothingIsStored(string how)
    {
        var f = new Fixture();
        var team = f.Store.AddTeam(f.Organization, "Вторая");
        team.Delete();
        var named = how == "unknown team" ? Guid.CreateVersion7() : team.Id;

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(teamIds: [named.ToString()])));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Single(f.Store.Areas);
        Assert.Empty(f.Store.AreaTeams);
    }

    // OQ-12 A · one name space per level: a department cannot take a project node's name at the top; per parent below.
    [Fact]
    public async Task Handle_SiblingNameAnyCase_IsAreaNameTaken_SameNameUnderAnotherParentIsFree()
    {
        var f = new Fixture();
        f.Store.AddArea(f.Node, "Квартальные");
        var other = f.Store.AddProject(f.Organization, "Другой", "OTH").Project;

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(
            f.Command(name: "квартальные ", parentId: f.Node.Id.ToString())));
        var elsewhere = await f.HandleAsync(f.Command(name: "Квартальные", parentId: f.Store.RootOf(other).Id.ToString()));
        var topLevel = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: " ПРИМЕР ПРОЕКТА")));

        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, refusal.Code);
        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, topLevel.Code);
        Assert.Equal(f.Store.RootOf(other).Id, elsewhere.Response.ParentId);
    }

    // US-1 · "The tree is at most 10 levels deep at creation".
    [Fact]
    public async Task Handle_UnderLevelTen_IsAreaDepthExceeded()
    {
        var f = new Fixture();
        var parent = f.Node;

        for (var level = 2; level <= 10; level++)
        {
            parent = f.Store.AddArea(parent, $"Уровень {level}");
        }

        var before = f.Store.Areas.Count;
        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(parentId: parent.Id.ToString())));

        Assert.Equal(OrganizationErrorCodes.AreaDepthExceeded, refusal.Code);
        Assert.Equal(before, f.Store.Areas.Count);
    }

    // US-6 / BR-AW-15 step 0: a role without area.create cannot make a department; a role without the link codes cannot name teams.
    // The second code answers first of all, so an unknown parent id learns nothing.
    [Fact]
    public async Task Handle_MemberDepartment_IsForbidden_BeforeAnyRead()
    {
        var f = new Fixture(OrganizationRole.Member);

        var refusal = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.AccessForbidden, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    [Fact]
    public async Task Handle_MemberUnderAParent_NeedsNoSecondCode_ButNamingTeamsIsForbidden()
    {
        var f = new Fixture(OrganizationRole.Member);

        var created = await f.HandleAsync(f.Command(name: "Квартальные", parentId: f.Node.Id.ToString()));
        var refusal = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(
            f.Command(name: "Годовые", parentId: f.Node.Id.ToString(), teamIds: [f.Platform.Id.ToString()])));

        Assert.Equal(f.Node.Id, created.Response.ParentId);
        Assert.Equal(OrganizationErrorCodes.AccessForbidden, refusal.Code);
        Assert.Empty(f.Store.AreaTeams);
    }

    // ADR-0031: the tree, then the parent, then the teams, then one save.
    [Fact]
    public async Task Handle_LockOrder_TreeParent_ThenOneSave()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command(name: "Квартальные", parentId: f.Node.Id.ToString()));

        Assert.Equal([$"lock area tree {f.Organization.Id}", $"lock area share {f.Node.Id}", "save"], f.Store.Calls);
    }

    [Fact]
    public async Task Handle_DepartmentWithTeams_LockOrder_TreeThenTeams()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command(teamIds: [f.Platform.Id.ToString()]));

        Assert.Equal([$"lock area tree {f.Organization.Id}", $"lock teams share {f.Platform.Id}", "save"], f.Store.Calls);
    }

    // ADR-0028 A-9: a parent seen by the unlocked read but absent from the lock statement's answer is 404, never a later read.
    [Fact]
    public async Task Handle_ParentDeletedBetweenTheReadAndTheLock_IsAreaNotFound()
    {
        var f = new Fixture();
        var sub = f.Store.AddArea(f.Node, "Квартальные");
        f.Areas.VanishedAfterTheLock.Add(sub.Id);

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(parentId: sub.Id.ToString())));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-1 · "Two creations of one sibling name at the same moment keep one": the loser's save fails on the sibling index.
    [Fact]
    public async Task Handle_LosesTheSiblingNameRace_AnswersAreaNameTaken_StoresNothing()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.AreaNameTaken));
        var before = f.Store.Areas.Count;

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: "Разработка")));

        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, refusal.Code);
        Assert.Equal(before, f.Store.Areas.Count);
    }
}
