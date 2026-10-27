using Mustask.Application.Features.Organizations.AddTeam;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.AddTeam;

/// <summary>
/// AddTeam on fakes (feat-028 plan-backend § 7.2, § 9.1; US-1): what it creates — a team of the organization, with its optional
/// description, people and links, in one flush —, the order of its answers (api-contract § 0.5) and that it locks nothing (§ 7).
/// Rewritten from the project-nested form (mod-017 BR-OT-10, BR-OT-11, mod-002 BR-M1-1; D-16).
/// </summary>
public sealed class AddTeamHandlerTests
{
    private sealed class Fixture
    {
        public Fixture(OrganizationRole role = OrganizationRole.Admin)
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Clock.UtcNow = new DateTimeOffset(2026, 10, 17, 3, 0, 0, TimeSpan.Zero);
            User.SubjectId = "admin-anna";
            Directory.Add("u-1", "Иван", "ivan@example.test", true);
            Directory.Add("u-2", "Мария", "maria@example.test", true);
            Store.AddMember(Organization, "u-1");
            Store.AddMember(Organization, "u-2");
            Handler = new AddTeamHandler(
                new FakeProjectRepository(Store),
                new FakeTeamRepository(Store),
                new FakeProjectTeamRepository(Store),
                new FakeAreaRepository(Store),
                new FakeAreaLinkRepository(Store),
                new FakeTeamMembershipRepository(Store),
                new FakeMemberReadModel(Store, Directory),
                Directory,
                new FakeOrganizationAccess(role),
                User,
                Clock,
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public FakeUserDirectory Directory { get; } = new();

        public FakeCurrentUser User { get; } = new();

        public FakeClock Clock { get; } = new();

        public Organization Organization { get; }

        public Project Project { get; }

        public AddTeamHandler Handler { get; }

        public AddTeamCommand Command(
            string? name = "MARS",
            string? description = null,
            IReadOnlyList<string?>? memberIds = null,
            IReadOnlyList<string?>? projectIds = null,
            string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), name, description, memberIds, projectIds);

        public Task<AddTeamResult> HandleAsync(AddTeamCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-1 · "created with a name only" (BR-OL-2): a team of the organization with no link, no member and no description; the
    // caller joins nothing.
    [Fact]
    public async Task Handle_NameOnly_CreatesTeamAlone_NoLinkNoMembership()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(name: " MARS "));

        var team = Assert.Single(f.Store.Teams);
        Assert.Equal(team.Id, result.Response.Id);
        Assert.Equal("MARS", result.Response.Name);
        Assert.Null(result.Response.Description);
        Assert.Equal(f.Organization.Id, team.OrganizationId);
        Assert.Empty(f.Store.Links);
        Assert.Empty(f.Store.TeamMemberships);
    }

    // US-1 · "created with its optional parts in one operation": the description as typed, the people, and the links recording the
    // caller and the time — one flush.
    [Fact]
    public async Task Handle_DescriptionMembersAndProjects_AreOneOperation_LinksRecordTheCallerAndTime()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(
            description: "  Платформа\nи инструменты ",
            memberIds: ["u-1", "u-2", "u-1"],
            projectIds: [f.Project.Id.ToString()]));

        var team = Assert.Single(f.Store.Teams);
        Assert.Equal("  Платформа\nи инструменты ", result.Response.Description);
        Assert.Equal(["u-1", "u-2"], f.Store.TeamMemberships.Where(m => m.TeamId == team.Id).Select(m => m.UserId).Order());
        var link = Assert.Single(f.Store.Links);
        Assert.Equal((f.Project.Id, team.Id, "admin-anna", f.Clock.UtcNow), (link.ProjectId, link.TeamId, link.LinkedBy, link.LinkedAt));
        Assert.Equal(1, f.Store.SaveCount);
    }

    // US-1 · "names are unique per organization": the name of an active team in any letter case is taken; creates nothing.
    [Theory]
    [InlineData("MARS")]
    [InlineData("mars")]
    [InlineData("  Mars ")]
    public async Task Handle_NameOfAnActiveTeamAnyCase_IsTeamNameTaken_CreatesNothing(string name)
    {
        var f = new Fixture();
        f.Store.AddTeam(f.Organization, "MARS");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: name)));

        Assert.Equal(OrganizationErrorCodes.TeamNameTaken, refusal.Code);
        Assert.Single(f.Store.Teams);
    }

    // US-1 · "names are unique per organization": another organization's name and a deleted team's name are free.
    [Fact]
    public async Task Handle_NameInAnotherOrganization_OrOfADeletedTeam_IsFree()
    {
        var f = new Fixture();
        var other = f.Store.AddOrganization("Другая");
        f.Store.AddTeam(other, "MARS");
        f.Store.AddTeam(f.Organization, "VENUS").Delete();

        await f.HandleAsync(f.Command(name: "MARS"));
        await f.HandleAsync(f.Command(name: "VENUS"));

        Assert.Equal(4, f.Store.Teams.Count);
    }

    // US-1 · "a choice that cannot be made": a blank name and a description over 2 000 characters, every reason at once, before
    // anything is read.
    [Fact]
    public async Task Handle_InvalidNameAndDescription_IsValidationFailedWithEveryReason()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ValidationException>(
            () => f.HandleAsync(f.Command(name: "  ", description: new string('я', 2001))));

        Assert.Equal(OrganizationErrorCodes.ValidationFailed, refusal.Code);
        Assert.Equal(OrganizationErrorCodes.NameLength, refusal.Errors["name"].Single().Code);
        Assert.Equal(OrganizationErrorCodes.DescriptionLength, refusal.Errors["description"].Single().Code);
        Assert.Empty(f.Store.Teams);
    }

    // US-1 · "a choice that cannot be made": a project that is malformed, unknown, of another organization or deactivated is
    // project.not_found, and nothing is created — the name stays free.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("foreign")]
    [InlineData("deactivated")]
    public async Task Handle_ProjectNotActiveOfTheOrganization_IsProjectNotFound_CreatesNothing(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddOrganization("Другая");
        var foreign = f.Store.AddProject(other, "Чужой", "OTH");
        var deactivated = f.Store.AddProject(f.Organization, "Старый", "OLD");
        deactivated.Deactivate();
        var id = how switch
        {
            "malformed" => "not-an-id",
            "unknown" => Guid.CreateVersion7().ToString(),
            "foreign" => foreign.Id.ToString(),
            _ => deactivated.Id.ToString(),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(
            () => f.HandleAsync(f.Command(projectIds: [f.Project.Id.ToString(), id])));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Empty(f.Store.Teams);
        Assert.Empty(f.Store.Links);
    }

    // US-1 · "a choice that cannot be made": a person of another organization, an unknown one and a disabled one are
    // member_not_in_organization; nothing is created.
    [Theory]
    [InlineData("other-organization")]
    [InlineData("unknown")]
    [InlineData("disabled")]
    [InlineData("blank")]
    public async Task Handle_MemberNotAnActiveMemberOfTheOrganization_IsMemberNotInOrganization_CreatesNothing(string how)
    {
        var f = new Fixture();
        f.Directory.Add("outsider", "Олег", "oleg@example.test", true);
        f.Directory.Add("gone", "Ушедший", "gone@example.test", false);
        f.Store.AddMember(f.Organization, "gone");
        var id = how switch
        {
            "other-organization" => "outsider",
            "unknown" => "nobody",
            "disabled" => "gone",
            _ => " ",
        };

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(memberIds: ["u-1", id])));

        Assert.Equal(OrganizationErrorCodes.TeamMemberNotInOrganization, refusal.Code);
        Assert.Empty(f.Store.Teams);
        Assert.Empty(f.Store.TeamMemberships);
    }

    // BR-OL-14 · the 409s of a create come member first, then name.
    [Fact]
    public async Task Handle_BadMemberAndTakenName_AnswersTheMemberFirst()
    {
        var f = new Fixture();
        f.Store.AddTeam(f.Organization, "MARS");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(memberIds: ["nobody"])));

        Assert.Equal(OrganizationErrorCodes.TeamMemberNotInOrganization, refusal.Code);
    }

    // US-8 · D-7 "a link joins two ends": projectIds needs team.link and project.link, memberIds needs team.write — asked before
    // any id is looked at. A role holding none of them learns nothing.
    [Fact]
    public async Task Handle_SecondCodesMissing_Is403_BeforeAnyIdIsLooked_At()
    {
        var f = new Fixture(OrganizationRole.Member);

        var withProjects = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(f.Command(projectIds: ["not-an-id"])));
        var withMembers = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(f.Command(memberIds: ["nobody"])));

        Assert.Equal(OrganizationErrorCodes.AccessForbidden, withProjects.Code);
        Assert.Equal(OrganizationErrorCodes.AccessForbidden, withMembers.Code);
        Assert.Empty(f.Store.Teams);
    }

    // plan-backend § 7.2 · the team is new and nothing binds to it: no lock, one flush.
    [Fact]
    public async Task Handle_LocksNothing_SavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command(projectIds: [f.Project.Id.ToString()]));

        Assert.Equal(["save"], f.Store.Calls);
    }

    // US-1 · "two creations of one name": the loser of the race hears the pre-check's 409 and stores nothing.
    [Fact]
    public async Task Handle_LosesTheTeamNameRace_AnswersTeamNameTaken_StoresNothing()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.TeamNameTaken));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.TeamNameTaken, refusal.Code);
        Assert.Empty(f.Store.Teams);
    }

    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(organizationId: "not-an-id")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }
}
