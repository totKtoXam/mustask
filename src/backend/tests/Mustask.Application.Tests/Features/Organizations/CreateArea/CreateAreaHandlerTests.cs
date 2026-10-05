using Mustask.Application.Features.Organizations.CreateArea;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.CreateArea;

/// <summary>
/// The reference use case on fakes (plan-backend § 14.2): what it creates, and
/// above all the order of its answers (§ 5.3, api-contract § 0.4) and its lock
/// order (§ 8.2), which the lanes copy.
/// </summary>
public sealed class CreateAreaHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddTeam(Project);
            Handler = new CreateAreaHandler(
                new FakeProjectRepository(Store),
                new FakeTeamRepository(Store),
                new FakeAreaRepository(Store),
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public NewTeam Platform { get; }

        public CreateAreaHandler Handler { get; }

        public CreateAreaCommand Command(string? parentId = null, string? name = "Отчёты", string? ownerTeamId = null, string? projectId = null, string? organizationId = null) =>
            new(
                organizationId ?? Organization.Id.ToString(),
                projectId ?? Project.Project.Id.ToString(),
                parentId ?? Project.Root.Id.ToString(),
                name,
                ownerTeamId);

        public Task<CreateAreaResult> HandleAsync(CreateAreaCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-5 · "A child area is created under any node": «Отчёты» under the root, «Квартальные» under «Отчёты».
    [Fact]
    public async Task Handle_UnderRootAndUnderChild_CreatesTheChildren()
    {
        var f = new Fixture();

        var reports = await f.HandleAsync(f.Command(name: " Отчёты "));
        var quarterly = await f.HandleAsync(f.Command(parentId: reports.Response.Id.ToString(), name: "Квартальные"));

        Assert.Equal(new AreaResponse(reports.Response.Id, f.Project.Project.Id, f.Project.Root.Id, "Отчёты", null), reports.Response);
        Assert.Equal(reports.Response.Id, quarterly.Response.ParentId);
        Assert.Equal("Квартальные", quarterly.Response.Name);

        var tree = new AreaTree(f.Store.Areas.Select(area => new AreaNode(area.Id, area.ParentId, area.Name, area.OwnerTeamId, area.DefaultOfTeamId)));
        Assert.Equal("Отчёты / Квартальные", tree.PathOf(quarterly.Response.Id));
    }

    // US-6 · "A child area is created with or without an owner".
    [Fact]
    public async Task Handle_WithOwnerOrInheriting_StoresTheExplicitOwnerOrNone()
    {
        var f = new Fixture();

        var owned = await f.HandleAsync(f.Command(name: "Отчёты", ownerTeamId: f.Platform.Team.Id.ToString()));
        var inheriting = await f.HandleAsync(f.Command(name: "Архив"));

        Assert.Equal(f.Platform.Team.Id, owned.Response.OwnerTeamId);
        Assert.Null(inheriting.Response.OwnerTeamId);
        Assert.Null(f.Store.Areas.Single(area => area.Id == owned.Response.Id).DefaultOfTeamId);
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
    // the project level answers before the body is looked at.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deactivated")]
    [InlineData("under a deactivated organization")]
    public async Task Handle_ProjectNotReachable_IsProjectNotFound_BeforeTheBody(string how)
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

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(projectId: projectId, name: "", parentId: "x")));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    // US-5 · "Sibling names are unique ... an empty name, or one longer than 100 characters, is refused".
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Handle_InvalidName_IsValidationFailedWithNameLength_BeforeAnyReferenceOrLock(string? name)
    {
        var f = new Fixture();
        var submitted = name == "101" ? new string('я', 101) : name;

        // The owner and the parent are wrong too: the body's values answer first (api-contract § 0.4 step 3 before 4).
        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(
            f.Command(name: submitted, parentId: "x", ownerTeamId: "y")));

        Assert.Equal(OrganizationErrorCodes.ValidationFailed, refusal.Code);
        Assert.Equal(["name"], refusal.Errors.Keys);
        Assert.Equal(OrganizationErrorCodes.NameLength, Assert.Single(refusal.Errors["name"]).Code);
        Assert.Empty(f.Store.Calls);
    }

    // US-6 · "An area's owner is a team of the area's own project" (BR-M1-22) and US-7 · "A deleted team answers not found":
    // the owner answers before the parent (api-contract § 4.2).
    [Theory]
    [InlineData("malformed")]
    [InlineData("empty")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    [InlineData("deleted")]
    public async Task Handle_OwnerNotAnActiveTeamOfTheProject_IsTeamNotFound_BeforeTheParent(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Organization, "Второй проект", "SEC");
        var ownerTeamId = how switch
        {
            "malformed" => "{" + f.Platform.Team.Id + "}",
            "empty" => "",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another project" => other.FirstTeam.Id.ToString(),
            _ => f.Platform.Team.Id.ToString(),
        };

        if (how == "deleted")
        {
            f.Platform.Team.Delete();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(
            f.Command(ownerTeamId: ownerTeamId, parentId: Guid.CreateVersion7().ToString())));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.DoesNotContain(f.Store.Calls, call => call.StartsWith("lock area", StringComparison.Ordinal));
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-5 · "A node never leaves its project" (BR-M1-22): a parent that is null, malformed, unknown or of another project.
    [Theory]
    [InlineData("null")]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    public async Task Handle_ParentNotAnAreaOfTheProject_IsAreaNotFound(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Organization, "Второй проект", "SEC");
        var command = f.Command() with
        {
            ParentId = how switch
            {
                "null" => null,
                "malformed" => "00000000-0000-0000-0000-000000000000",
                "unknown" => Guid.CreateVersion7().ToString(),
                _ => other.Root.Id.ToString(),
            },
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(command));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
        Assert.DoesNotContain(f.Store.Areas, area => area.ProjectId == other.Project.Id && !area.IsRoot);
    }

    // US-5 · "Sibling names are unique, compared ignoring case and spaces"; «Квартальные» is still free under another parent.
    [Fact]
    public async Task Handle_SiblingNameAnyCase_IsAreaNameTaken_SameNameUnderAnotherParentIsFree()
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Project.Root, "Отчёты");
        f.Store.AddArea(reports, "Квартальные");
        var archive = f.Store.AddArea(f.Project.Root, "Архив");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(
            f.Command(parentId: reports.Id.ToString(), name: "квартальные ")));
        var elsewhere = await f.HandleAsync(f.Command(parentId: archive.Id.ToString(), name: "Квартальные"));

        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, refusal.Code);
        Assert.Single(f.Store.Areas, area => area.ParentId == reports.Id);
        Assert.Equal(archive.Id, elsewhere.Response.ParentId);
    }

    // plan-backend § 8.2: the owner team FOR SHARE, then the parent FOR SHARE, then one save; with no owner, no team lock.
    [Fact]
    public async Task Handle_LocksTheOwnerThenTheParent_ThenSavesOnce()
    {
        var f = new Fixture();
        var teamId = f.Platform.Team.Id;
        var rootId = f.Project.Root.Id;

        await f.HandleAsync(f.Command(ownerTeamId: teamId.ToString()));
        Assert.Equal([$"lock team share {teamId}", $"lock area share {rootId}", "save"], f.Store.Calls);

        f.Store.Calls.Clear();
        await f.HandleAsync(f.Command(name: "Архив"));
        Assert.Equal([$"lock area share {rootId}", "save"], f.Store.Calls);
    }

    // US-5 · "Two creations of one sibling name at the same moment keep one": the loser's save fails on the sibling index,
    // and the repository's answer reaches the client unchanged.
    [Fact]
    public async Task Handle_LosesTheSiblingNameRace_AnswersAreaNameTaken_StoresNothing()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.AreaNameTaken));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: "Квартальные")));

        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, refusal.Code);
        Assert.DoesNotContain(f.Store.Areas, area => area.Name == "Квартальные");
    }
}
