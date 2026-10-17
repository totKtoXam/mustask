using Mustask.Application.Features.Organizations.CreateProject;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.CreateProject;

/// <summary>
/// <c>CreateProject</c> on fakes (US-3): what it creates, the order of its
/// answers (plan-backend § 5.3, api-contract § 0.4, § 2.3) and the absence of
/// locks (§ 8.2). The races are the Api tests' (a real index is the referee);
/// here the loser's save is made to fail the way the repository reports it.
/// </summary>
public sealed class CreateProjectHandlerTests
{
    private const string Subject = "9f2d7c41-0000-4000-8000-0000000c4ea7";

    private sealed class Fixture
    {
        /// <summary>
        /// The organization with <see cref="Subject"/> as its administrator — who the scoped check lets reach this route — unless
        /// the test says the creator holds no membership (the window of a removal between the check and the flush).
        /// </summary>
        public Fixture(bool creatorIsAMember = true)
        {
            Organization = Store.AddOrganization();
            Clock.UtcNow = new DateTimeOffset(2026, 10, 2, 9, 30, 0, TimeSpan.Zero);
            CurrentUser.SubjectId = Subject;

            if (creatorIsAMember)
            {
                Store.AddMember(Organization, Subject, OrganizationRole.Admin);
            }

            Handler = new CreateProjectHandler(
                new FakeOrganizationReadModel(Store),
                new FakeTeamMembershipRepository(Store),
                new FakeProjectRepository(Store),
                Clock,
                CurrentUser,
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public FakeClock Clock { get; } = new();

        public FakeCurrentUser CurrentUser { get; } = new();

        public Organization Organization { get; }

        public CreateProjectHandler Handler { get; }

        public CreateProjectCommand Command(string? name = "Пример проекта", string? key = "PRM", string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), name, key);

        public Task<CreateProjectResult> HandleAsync(CreateProjectCommand command) => Handler.Handle(command, CancellationToken.None);

        /// <summary>The project, its team and its root as stored, found by the project's id.</summary>
        public (Project Project, Team Team, Area Root) StoredParts(Guid projectId) =>
            (
                Store.Projects.Single(project => project.Id == projectId),
                Store.Teams.Single(team => team.ProjectId == projectId),
                Store.Areas.Single(area => area.ProjectId == projectId));
    }

    // US-3 · "A project is created ready to use": active, with its name and key; one team and a root area, both named after
    // the project; the team an ordinary one and the root owned by nobody (mod-017 BR-OT-10, BR-OT-11); the creator and the time kept (BR-M1-21).
    [Fact]
    public async Task Handle_ValidNameAndKey_CreatesProjectWithOneOrdinaryTeamAndRoot_RecordsCreator()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(name: " Пример проекта "));

        var (project, team, root) = f.StoredParts(result.Response.Id);
        Assert.Equal(new ProjectResponse(project.Id, f.Organization.Id, "Пример проекта", "PRM"), result.Response);
        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Equal(f.Organization.Id, project.OrganizationId);
        Assert.Equal(Subject, project.CreatedBy);
        Assert.Equal(f.Clock.UtcNow, project.CreatedAt);

        Assert.Equal("Пример проекта", team.Name);
        Assert.True(team.IsActive);
        Assert.Null(project.DefaultTeamId);

        Assert.Equal("Пример проекта", root.Name);
        Assert.True(root.IsRoot);
    }

    // US-3 · "A project is created ready to use": nothing but the project, one team and one root is written, in one save, and
    // no lock is taken (plan-backend § 8.2: inserts only, closed by the unique indexes).
    [Fact]
    public async Task Handle_Creates_OneProjectOneTeamOneArea_OneSave_NoLock()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Single(f.Store.Projects);
        Assert.Single(f.Store.Teams);
        Assert.Single(f.Store.Areas);
        Assert.Equal(["save"], f.Store.Calls);
    }

    // US-3 · "A project is created ready to use": the key is kept exactly as sent, and the stored name is the trimmed one.
    [Fact]
    public async Task Handle_KeepsTheKeyAsSent_AndStoresTheTrimmedName()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(name: "  Платформа данных\t", key: "P2"));

        Assert.Equal("Платформа данных", result.Response.Name);
        Assert.Equal("P2", result.Response.Key);
        Assert.Equal("Платформа данных", f.Store.Projects.Single().Name);
    }

    // BR-M1-20, ADR-0026 rule 5: behind the filter the organization is active and well formed; if it were not, the outermost
    // level answers first, whatever the body holds (the body is wrong too).
    [Theory]
    [InlineData("malformed")]
    [InlineData("empty")]
    [InlineData("unknown")]
    [InlineData("deactivated")]
    public async Task Handle_OrganizationNotActive_IsOrganizationNotFound_BeforeTheBody_NothingStored(string how)
    {
        var f = new Fixture();
        var organizationId = how switch
        {
            "malformed" => "not-an-id",
            "empty" => "",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => f.Organization.Id.ToString(),
        };

        if (how == "deactivated")
        {
            f.Organization.Deactivate();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(name: "", key: "prm", organizationId: organizationId)));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Empty(f.Store.Projects);
        Assert.Empty(f.Store.Calls);
    }

    // BR-M1-20: an address with no organization value at all is the same not-found as a malformed one.
    [Fact]
    public async Task Handle_OrganizationNullId_IsOrganizationNotFound()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(new CreateProjectCommand(null, "Пример проекта", "PRM")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Empty(f.Store.Projects);
    }

    // US-3 · "The name is unique in the organization ... an empty name, or one longer than 100 characters, is refused with
    // «Название должно содержать от 1 до 100 символов»": the name field only, before any pre-check or save.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Handle_InvalidName_IsValidationFailedWithNameLength_BeforeAnyPreCheck(string? name)
    {
        var f = new Fixture();
        f.Store.AddProject(f.Organization, "Пример проекта", "PRM");
        var submitted = name == "101" ? new string('я', 101) : name;

        // The key is taken too: the body's values answer before the uniqueness checks (§ 0.4 step 3 before step 5).
        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(name: submitted, key: "PRM")));

        Assert.Equal(OrganizationErrorCodes.ValidationFailed, refusal.Code);
        Assert.Equal(["name"], refusal.Errors.Keys);
        Assert.Equal(OrganizationErrorCodes.NameLength, Assert.Single(refusal.Errors["name"]).Code);
        Assert.Single(f.Store.Projects);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-3 · "The name is unique ...": the limit is 100 code units after trimming, so exactly 100 is a name.
    [Fact]
    public async Task Handle_NameOfExactlyOneHundredCharacters_IsAccepted()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(name: " " + new string('я', 100) + " "));

        Assert.Equal(100, result.Response.Name.Length);
    }

    // US-3 · "The key is required and well-formed": an empty key is refused with the «required» reason, and each malformed key
    // with the «invalid» reason; no project is created. «РМК» is typed in Cyrillic; " PRM " and "PRM\n" are kept as sent.
    [Theory]
    [InlineData(null, OrganizationErrorCodes.ProjectKeyRequired)]
    [InlineData("", OrganizationErrorCodes.ProjectKeyRequired)]
    [InlineData("prm", OrganizationErrorCodes.ProjectKeyInvalid)]
    [InlineData("Prm", OrganizationErrorCodes.ProjectKeyInvalid)]
    [InlineData("1PRM", OrganizationErrorCodes.ProjectKeyInvalid)]
    [InlineData("P", OrganizationErrorCodes.ProjectKeyInvalid)]
    [InlineData("PROJECTKEY01", OrganizationErrorCodes.ProjectKeyInvalid)]
    [InlineData("РМК", OrganizationErrorCodes.ProjectKeyInvalid)]
    [InlineData(" PRM ", OrganizationErrorCodes.ProjectKeyInvalid)]
    [InlineData("PRM\n", OrganizationErrorCodes.ProjectKeyInvalid)]
    [InlineData("PR-M", OrganizationErrorCodes.ProjectKeyInvalid)]
    [InlineData("   ", OrganizationErrorCodes.ProjectKeyInvalid)]
    public async Task Handle_KeyMissingOrMalformed_IsValidationFailedWithTheKeyReason_CreatesNothing(string? key, string reason)
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(key: key)));

        Assert.Equal(OrganizationErrorCodes.ValidationFailed, refusal.Code);
        Assert.Equal(["key"], refusal.Errors.Keys);
        Assert.Equal(reason, Assert.Single(refusal.Errors["key"]).Code);
        Assert.Empty(f.Store.Projects);
        Assert.Empty(f.Store.Teams);
        Assert.Empty(f.Store.Areas);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-3 · "The key is required and well-formed": two and ten characters are the bounds, both accepted.
    [Theory]
    [InlineData("P2")]
    [InlineData("PROJECTKEY")]
    [InlineData("A012345678")]
    public async Task Handle_KeyAtTheBounds_IsAccepted(string key)
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(key: key));

        Assert.Equal(key, result.Response.Key);
    }

    // api-contract § 0.5: every field reason at once, so a bad name and a bad key come back together.
    [Fact]
    public async Task Handle_NameAndKeyBothBad_IsOneValidationFailedWithBothReasons()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(name: "   ", key: "prm")));

        Assert.Equal(OrganizationErrorCodes.ValidationFailed, refusal.Code);
        Assert.Equal(["key", "name"], refusal.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(OrganizationErrorCodes.NameLength, Assert.Single(refusal.Errors["name"]).Code);
        Assert.Equal(OrganizationErrorCodes.ProjectKeyInvalid, Assert.Single(refusal.Errors["key"]).Code);
        Assert.Empty(f.Store.Projects);
    }

    // US-3 · "The name is unique in the organization": «пример проекта » against the active «Пример проекта» is refused with
    // the plain code, whatever the case and the surrounding spaces; the existing project is untouched.
    [Fact]
    public async Task Handle_NameOfAnActiveProjectAnyCase_IsProjectNameTaken_StoresNothing()
    {
        var f = new Fixture();
        f.Store.AddProject(f.Organization, "Пример проекта", "PRM");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: "пример проекта ", key: "OTH")));

        Assert.Equal(OrganizationErrorCodes.ProjectNameTaken, refusal.Code);
        Assert.Equal(0, refusal.Params.Count);
        Assert.Single(f.Store.Projects);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-3 · "A deactivated project's name stays reserved": the same name against a deactivated project is refused with the
    // «deactivated» code (BR-M1-1).
    [Fact]
    public async Task Handle_NameOfADeactivatedProject_IsProjectNameTakenByDeactivated_StoresNothing()
    {
        var f = new Fixture();
        f.Store.AddProject(f.Organization, "Пример проекта", "PRM").Project.Deactivate();

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: "ПРИМЕР ПРОЕКТА", key: "OTH")));

        Assert.Equal(OrganizationErrorCodes.ProjectNameTakenByDeactivated, refusal.Code);
        Assert.Single(f.Store.Projects);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-3 · "The name is unique in the organization ... the name is still free in «Бета»" (BR-M1-1): another organization's
    // project, active or deactivated, holds nothing against this one.
    [Fact]
    public async Task Handle_NameAndKeyOfAnotherOrganizationsProjects_AreFree()
    {
        var f = new Fixture();
        var other = f.Store.AddOrganization("Другая организация");
        f.Store.AddProject(other, "Пример проекта", "PRM");
        f.Store.AddProject(other, "Архивный", "OLD").Project.Deactivate();

        var created = await f.HandleAsync(f.Command(name: "Пример проекта", key: "PRM"));
        var second = await f.HandleAsync(f.Command(name: "Архивный", key: "OLD"));

        Assert.Equal(f.Organization.Id, created.Response.OrganizationId);
        Assert.Equal(f.Organization.Id, second.Response.OrganizationId);
        Assert.Equal(2, f.Store.Projects.Count(project => project.OrganizationId == f.Organization.Id));
    }

    // US-3 · "The key is unique in the organization, deactivated projects included": `PRM` held by an active or a deactivated
    // project is refused with the key code and the key as its parameter.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_KeyOfAProjectOfAnyStatus_IsProjectKeyTaken_WithTheKeyParam(bool deactivated)
    {
        var f = new Fixture();
        var holder = f.Store.AddProject(f.Organization, "Пример проекта", "PRM");

        if (deactivated)
        {
            holder.Project.Deactivate();
        }

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: "Другой проект", key: "PRM")));

        Assert.Equal(OrganizationErrorCodes.ProjectKeyTaken, refusal.Code);
        Assert.Equal(ErrorParam.Text("PRM"), refusal.Params["key"]);
        Assert.Equal(1, refusal.Params.Count);
        Assert.Single(f.Store.Projects);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // api-contract § 2.3: "When both the name and the key are taken, the name's code is answered."
    [Fact]
    public async Task Handle_NameAndKeyBothTaken_AnswersTheNamesCode()
    {
        var f = new Fixture();
        f.Store.AddProject(f.Organization, "Пример проекта", "PRM");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: "Пример проекта", key: "PRM")));

        Assert.Equal(OrganizationErrorCodes.ProjectNameTaken, refusal.Code);
    }

    // US-3 · "Two projects with one key at the same moment": the loser's insert fails on the key index, and the repository's
    // answer (the key code, the key as its parameter) reaches the client unchanged; nothing of the loser is stored.
    [Fact]
    public async Task Handle_LosesTheKeyRace_AnswersProjectKeyTaken_StoresNothing()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.ProjectKeyTaken, new ErrorParams { ["key"] = ErrorParam.Text("PRM") }));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.ProjectKeyTaken, refusal.Code);
        Assert.Equal(ErrorParam.Text("PRM"), refusal.Params["key"]);
        Assert.Empty(f.Store.Projects);
        Assert.Empty(f.Store.Teams);
        Assert.Empty(f.Store.Areas);
    }

    // US-3 · "The name is unique in the organization": a concurrent creation took the name first, so the loser answers the
    // plain name code, as the pre-check does for an active holder (api-contract § 2.3).
    [Fact]
    public async Task Handle_LosesTheNameRace_AnswersProjectNameTaken_StoresNothing()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.ProjectNameTaken));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.ProjectNameTaken, refusal.Code);
        Assert.Empty(f.Store.Projects);
    }

    // mod-016 US-3 · "The project's creator is its first team's first member" (BR-M1-25, doc-006 BR-M1-9): the creating
    // administrator is the only member of the first team, whatever the user projection says of them (the handler holds none).
    [Fact]
    public async Task Handle_CreatorIsAMember_TheyAreTheOnlyMemberOfTheProjectsTeam()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command());

        var firstTeam = f.StoredParts(result.Response.Id).Team;
        var membership = Assert.Single(f.Store.TeamMemberships);
        Assert.Equal((firstTeam.Id, f.Organization.Id, Subject), (membership.TeamId, membership.OrganizationId, membership.UserId));
        Assert.Equal(1, f.Store.SaveCount);
    }

    // BE-CON-5 / plan-backend § 3.6 row 12 · the creator's membership ended between the scoped check and the flush: the route's
    // scoped check admits only an administrator, so a creator with no membership here is exactly that window. The handler adds the
    // first team's membership without looking, the composite key refuses it, and the whole creation rolls back with the
    // organization's not-found — no project is created for a user who is no longer a member.
    [Fact]
    public async Task Handle_CreatorIsNotAMemberAnyMore_IsOrganizationNotFound_StoresNothing()
    {
        var f = new Fixture(creatorIsAMember: false);

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Empty(f.Store.Projects);
        Assert.Empty(f.Store.Teams);
        Assert.Empty(f.Store.TeamMemberships);
    }

    // mod-016 US-3 · only the new project's team gains the creator; a later team of the project starts empty.
    [Fact]
    public async Task Handle_OnlyTheNewProjectsTeamGainsTheCreator()
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Organization, "Другой проект", "OTH");

        var result = await f.HandleAsync(f.Command());

        var membership = Assert.Single(f.Store.TeamMemberships);
        Assert.Equal(f.StoredParts(result.Response.Id).Team.Id, membership.TeamId);
        Assert.NotEqual(other.Team.Id, membership.TeamId);
    }

    // mod-016 plan-backend § 3.6 row 12 · the creator's membership ended mid-request: the database refuses the team membership
    // through the composite key, the whole creation rolls back, and the answer is the organization's not-found.
    [Fact]
    public async Task Handle_CreatorsMembershipEndedMidRequest_IsOrganizationNotFound_StoresNothing()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.TeamMemberNotInOrganization));

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Empty(f.Store.Projects);
        Assert.Empty(f.Store.TeamMemberships);
    }

    // BR-M1-21: the creator is whoever the current user is at the time; two creations by two users keep two creators.
    [Fact]
    public async Task Handle_RecordsTheCurrentUsersSubject_ForEachCreation()
    {
        var f = new Fixture();

        var first = await f.HandleAsync(f.Command(name: "Первый", key: "FST"));
        f.CurrentUser.SubjectId = "c3a1b2d4-0000-4000-8000-0000000000aa";
        f.Store.AddMember(f.Organization, f.CurrentUser.SubjectId, OrganizationRole.Admin);
        var second = await f.HandleAsync(f.Command(name: "Второй", key: "SND"));

        Assert.Equal(Subject, f.StoredParts(first.Response.Id).Project.CreatedBy);
        Assert.Equal("c3a1b2d4-0000-4000-8000-0000000000aa", f.StoredParts(second.Response.Id).Project.CreatedBy);
    }
}
