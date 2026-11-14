using Mustask.Application.Features.Organizations.MoveTeam;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.MoveTeam;

/// <summary>mod-021 US-3 on fakes: a team moves to another department while it is linked to no project (BR-TD-4), and the order of the answers and of the locks.</summary>
public sealed class MoveTeamHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Source = Store.AddDepartment(Organization, "Разработка");
            Destination = Store.AddDepartment(Organization, "Продажи", "orange");
            Mars = Store.AddTeam(Organization, "MARS", Source, "green");
            Areas = new FakeAreaRepository(Store);
            Handler = new MoveTeamHandler(
                new FakeOrganizationReadModel(Store),
                new FakeTeamReadModel(Store),
                new FakeTeamRepository(Store),
                new FakeProjectTeamRepository(Store),
                Areas,
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public Area Source { get; }

        public Area Destination { get; }

        public Team Mars { get; }

        public FakeAreaRepository Areas { get; }

        public MoveTeamHandler Handler { get; }

        public MoveTeamCommand Command(string? departmentId = "destination", string? teamId = null, string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), teamId ?? Mars.Id.ToString(), departmentId == "destination" ? Destination.Id.ToString() : departmentId);

        public Task<MoveTeamResult> HandleAsync(MoveTeamCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-3 · a team linked to no project moves; the colour stays and the answer names the new place.
    [Fact]
    public async Task Handle_UnlinkedTeam_MovesToTheDepartment_KeepsItsColour()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command());

        Assert.Equal(f.Destination.Id, f.Mars.DepartmentId);
        Assert.Equal("green", f.Mars.Color);
        Assert.Equal(new TeamPlaceResponse(f.Mars.Id, "MARS", null, f.Destination.Id, "Продажи", "green"), result.Response);
        Assert.Equal(1, f.Store.SaveCount);
    }

    // US-3 · "a linked team does not move": team.has_links, nothing changes.
    [Fact]
    public async Task Handle_LinkedTeam_IsTeamHasLinks_AndStaysWhereItIs()
    {
        var f = new Fixture();
        var project = f.Store.AddProject(f.Organization).Project;
        f.Store.Link(project, f.Mars);

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.TeamHasLinks, refusal.Code);
        Assert.Equal(f.Source.Id, f.Mars.DepartmentId);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-3 · the same department is a no-op even for a linked team: no save, no refusal.
    [Fact]
    public async Task Handle_TheSameDepartment_IsAnAnswerWithNoChange_EvenWhenLinked()
    {
        var f = new Fixture();
        f.Store.Link(f.Store.AddProject(f.Organization).Project, f.Mars);

        var result = await f.HandleAsync(f.Command(departmentId: f.Source.Id.ToString()));

        Assert.Equal(f.Source.Id, result.Response.DepartmentId);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-3 · the destination must be a department of this organization: absent, foreign, malformed are 404; a node of a project or a sub-area is 409.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("foreign")]
    public async Task Handle_DestinationNotOfTheOrganization_IsAreaNotFound(string how)
    {
        var f = new Fixture();
        var foreign = f.Store.AddDepartment(f.Store.AddOrganization("Чужая"), "Чужой");
        var id = how switch
        {
            "malformed" => "not-an-id",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => foreign.Id.ToString(),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(departmentId: id)));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Equal(f.Source.Id, f.Mars.DepartmentId);
    }

    [Fact]
    public async Task Handle_DestinationIsAProjectNodeOrASubArea_IsAreaTypeNotAllowed()
    {
        var f = new Fixture();
        var node = f.Store.RootOf(f.Store.AddProject(f.Organization, department: f.Source).Project);
        var subArea = f.Store.AddArea(node, "Подраздел");

        foreach (var id in new[] { node.Id, subArea.Id })
        {
            var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(departmentId: id.ToString())));
            Assert.Equal(OrganizationErrorCodes.AreaTypeNotAllowed, refusal.Code);
        }
    }

    // The race: the destination deleted between the unlocked reads and the lock is 404, never a server error.
    [Fact]
    public async Task Handle_DestinationDeletedAtTheLock_IsAreaNotFound()
    {
        var f = new Fixture();
        f.Areas.VanishedAfterTheLock.Add(f.Destination.Id);

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Equal(f.Source.Id, f.Mars.DepartmentId);
    }

    [Fact]
    public async Task Handle_WithoutADestination_IsValidationFailed_OnTheDepartmentField()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(departmentId: null)));

        Assert.Equal(OrganizationErrorCodes.TeamDepartmentRequired, refusal.Errors["departmentId"].Single().Code);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("deleted")]
    public async Task Handle_TeamNotActiveOfTheOrganization_IsTeamNotFound(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddTeam(f.Organization, "Удалённая", f.Source);
        other.Delete();
        var id = how switch
        {
            "malformed" => "not-an-id",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => other.Id.ToString(),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(teamId: id)));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    [Fact]
    public async Task Handle_UnknownOrganization_IsOrganizationNotFound()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(organizationId: Guid.CreateVersion7().ToString())));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }

    // ADR-0040 § 1 · the tree, then the destination FOR SHARE, then the team FOR NO KEY UPDATE, then one save.
    [Fact]
    public async Task Handle_LockOrder_TreeDestinationTeam_ThenOneSave()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Equal(
            [
                $"lock area tree {f.Organization.Id}",
                $"lock area share {f.Destination.Id}",
                $"lock team no-key-update {f.Mars.Id}",
                "save",
            ],
            f.Store.Calls);
    }
}
