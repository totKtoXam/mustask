using Mustask.Application.Features.Organizations.GetArea;
using Mustask.Application.Features.Organizations.GetAreaDeletion;
using Mustask.Application.Features.Organizations.GetAreaTeams;
using Mustask.Application.Features.Organizations.GetProjectAreas;
using Mustask.Application.Features.Organizations.GetTeamAreas;
using Mustask.Application.Features.Organizations.SetAreaDescription;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.AreaLinks;

/// <summary>mod-018 US-1 / US-3 / US-4 on fakes: the area page, its description, the deletion dialog and the link lists of the pages.</summary>
public sealed class AreaReadHandlersTests
{
    private sealed class World
    {
        public World()
        {
            Organization = Store.AddOrganization();
            Team = Store.AddTeam(Organization);
            Department = Store.AddDepartment(Organization, "Разработка");
            Project = Store.AddProject(Organization, department: Department).Project;
            Node = Store.RootOf(Project);
            Quarterly = Store.AddArea(Node, "Квартальные");
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public Project Project { get; }

        public Team Team { get; }

        public Area Department { get; }

        public Area Node { get; }

        public Area Quarterly { get; }

        public string Org => Organization.Id.ToString();

        public FakeAreaTreeReadModel Reads => new(Store);

        public FakeAreaRepository Areas => new(Store);

        public FakeOrganizationStructure Structure => new(Store);

        public SetAreaDescriptionHandler Describe() =>
            new(new FakeOrganizationReadModel(Store), new FakeProjectRepository(Store), Areas, new FakeUnitOfWork());
    }

    private static CancellationToken Ct => CancellationToken.None;

    // US-3 · the page of a node: its type, its parent with the path, its direct children with their types, its own path.
    [Fact]
    public async Task GetArea_ShowsTheTypeTheParentThePathAndTheChildren()
    {
        var w = new World();
        var handler = new GetAreaHandler(w.Reads);

        var sub = (await handler.Handle(new GetAreaQuery(w.Org, w.Quarterly.Id.ToString()), Ct)).Response;
        var node = (await handler.Handle(new GetAreaQuery(w.Org, w.Node.Id.ToString()), Ct)).Response;
        var department = (await handler.Handle(new GetAreaQuery(w.Org, w.Department.Id.ToString()), Ct)).Response;

        Assert.Equal(("area", w.Project.Id, true, "Разработка / Пример проекта / Квартальные"), (sub.Type, sub.ProjectId, sub.ProjectActive, sub.Path));
        Assert.Equal(new AreaRefResponse(w.Node.Id, "Пример проекта", "Разработка / Пример проекта"), sub.Parent);
        Assert.Empty(sub.Children);
        Assert.Equal("project", node.Type);
        Assert.Equal(w.Quarterly.Id, Assert.Single(node.Children).Id);
        Assert.Equal("area", node.Children[0].Type);
        Assert.Equal(("department", null, null), (department.Type, department.ProjectId, department.ProjectActive));
        Assert.Null(department.Parent);
        Assert.Equal("project", Assert.Single(department.Children).Type);
    }

    // OQ-6 A · a deactivated project's node is shown and marked, not hidden.
    [Fact]
    public async Task GetArea_OfADeactivatedProject_IsMarkedInactive()
    {
        var w = new World();
        w.Project.Deactivate();

        var node = (await new GetAreaHandler(w.Reads).Handle(new GetAreaQuery(w.Org, w.Node.Id.ToString()), Ct)).Response;

        Assert.Equal(false, node.ProjectActive);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task GetArea_NotAnAreaOfTheOrganization_IsAreaNotFound(string how)
    {
        var w = new World();
        var foreign = w.Store.AddDepartment(w.Store.AddOrganization("Другая"), "Чужая");
        var id = how switch { "malformed" => "x", "unknown" => Guid.CreateVersion7().ToString(), _ => foreign.Id.ToString() };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => new GetAreaHandler(w.Reads).Handle(new GetAreaQuery(w.Org, id), Ct));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
    }

    // US-1 · "A description is set and cleared apart from the name": kept as typed, an empty one clears it.
    [Fact]
    public async Task SetAreaDescription_SetsAndClears_KeepsTheName()
    {
        var w = new World();
        var handler = w.Describe();

        var set = await handler.Handle(new SetAreaDescriptionCommand(w.Org, w.Department.Id.ToString(), " Всё про разработку\n"), Ct);
        var cleared = await handler.Handle(new SetAreaDescriptionCommand(w.Org, w.Department.Id.ToString(), ""), Ct);

        Assert.Equal(" Всё про разработку\n", set.Response.Description);
        Assert.Null(cleared.Response.Description);
        Assert.Equal("Разработка", cleared.Response.Name);
        Assert.Equal("department", cleared.Response.Type);
        Assert.Equal([$"lock area update {w.Department.Id}", "save", $"lock area update {w.Department.Id}", "save"], w.Store.Calls);
    }

    // OQ-10 A · a project's node may be described; OQ-6 A · not while its project is deactivated (404 project.not_found).
    [Fact]
    public async Task SetAreaDescription_ProjectNodeIsDescribable_ADeactivatedProjectsNodeIsNotFound()
    {
        var w = new World();

        var described = await w.Describe().Handle(new SetAreaDescriptionCommand(w.Org, w.Node.Id.ToString(), "О проекте"), Ct);
        w.Project.Deactivate();
        var refusal = await Assert.ThrowsAsync<NotFoundException>(() =>
            w.Describe().Handle(new SetAreaDescriptionCommand(w.Org, w.Quarterly.Id.ToString(), "Нельзя"), Ct));

        Assert.Equal("О проекте", described.Response.Description);
        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Null(w.Quarterly.Description);
    }

    [Fact]
    public async Task SetAreaDescription_TooLong_IsValidationFailed_UnknownAreaIsNotFound()
    {
        var w = new World();
        var handler = w.Describe();

        var invalid = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new SetAreaDescriptionCommand(w.Org, w.Department.Id.ToString(), new string('я', 2001)), Ct));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new SetAreaDescriptionCommand(w.Org, Guid.CreateVersion7().ToString(), new string('я', 2001)), Ct));

        Assert.Equal(["description"], invalid.Errors.Keys);
        Assert.Equal(OrganizationErrorCodes.AreaNotFound, missing.Code);
    }

    // US-2 · "The deletion dialog lists what the deletion ends": the team links of a department; the others have none.
    [Fact]
    public async Task GetAreaDeletion_ListsTheTeamLinksItWouldEnd_OnlyADepartmentHasAny()
    {
        var w = new World();
        w.Store.LinkArea(w.Department, w.Team);
        var handler = new GetAreaDeletionHandler(w.Areas, w.Reads);

        var department = await handler.Handle(new GetAreaDeletionQuery(w.Org, w.Department.Id.ToString()), Ct);
        var sub = await handler.Handle(new GetAreaDeletionQuery(w.Org, w.Quarterly.Id.ToString()), Ct);

        Assert.Equal("Платформа", Assert.Single(department.Teams).Name);
        Assert.Empty(sub.Teams);
    }

    [Fact]
    public async Task GetAreaDeletion_UnknownArea_IsAreaNotFound()
    {
        var w = new World();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetAreaDeletionHandler(w.Areas, w.Reads).Handle(new GetAreaDeletionQuery(w.Org, Guid.CreateVersion7().ToString()), Ct));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
    }

    // US-4 · the department's own page lists its active teams (a deleted team's link is gone with it).
    [Fact]
    public async Task GetAreaTeams_ListsOnlyTheActiveOnes_UnknownAreaIsAreaNotFound()
    {
        var w = new World();
        var gone = w.Store.AddTeam(w.Organization, "Ушла");
        w.Store.LinkArea(w.Department, w.Team);
        w.Store.LinkArea(w.Department, gone);
        gone.Delete();
        var handler = new GetAreaTeamsHandler(w.Areas, w.Reads);

        var teams = await handler.Handle(new GetAreaTeamsQuery(w.Org, w.Department.Id.ToString()), Ct);
        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetAreaTeamsQuery(w.Org, Guid.CreateVersion7().ToString()), Ct));

        Assert.Equal(new AreaTeamResponse(w.Team.Id, "Платформа"), Assert.Single(teams.Items));
        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
    }

    // US-3 · a project's page lists its own subtree: the node first, the sub-areas below, paths from the top.
    [Fact]
    public async Task GetProjectAreas_ListsTheNodeAndItsSubAreas_WithDepthAndPath()
    {
        var w = new World();
        var other = w.Store.AddProject(w.Organization, "Другой", "OTH").Project;
        w.Store.AddArea(w.Store.RootOf(other), "Чужая");

        var result = await new GetProjectAreasHandler(w.Structure, w.Reads).Handle(new GetProjectAreasQuery(w.Org, w.Project.Id.ToString()), Ct);

        Assert.Equal(["Пример проекта", "Квартальные"], result.Items.Select(item => item.Name));
        Assert.Equal(["project", "area"], result.Items.Select(item => item.Type));
        Assert.Equal([2, 3], result.Items.Select(item => item.Depth));
        Assert.Equal("Разработка / Пример проекта / Квартальные", result.Items[1].Path);
        Assert.All(result.Items, item => Assert.Equal(w.Project.Id, item.ProjectId));
    }

    // ADR-0026 rule 5 · a deactivated project is a 404 on its own route; its node stays in the organization's tree.
    [Fact]
    public async Task GetProjectAreas_DeactivatedOrUnknownProject_IsProjectNotFound()
    {
        var w = new World();
        w.Project.Deactivate();
        var handler = new GetProjectAreasHandler(w.Structure, w.Reads);

        var deactivated = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetProjectAreasQuery(w.Org, w.Project.Id.ToString()), Ct));
        var unknown = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetProjectAreasQuery(w.Org, Guid.CreateVersion7().ToString()), Ct));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, deactivated.Code);
        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, unknown.Code);
    }

    // US-4 · a team's page lists its departments with the path.
    [Fact]
    public async Task GetTeamAreas_ListsTheDepartmentsWithTheirPaths_ADeletedTeamIsNotFound()
    {
        var w = new World();
        w.Store.LinkArea(w.Department, w.Team);
        var handler = new GetTeamAreasHandler(w.Structure);

        var team = await handler.Handle(new GetTeamAreasQuery(w.Org, w.Team.Id.ToString()), Ct);
        w.Team.Delete();
        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetTeamAreasQuery(w.Org, w.Team.Id.ToString()), Ct));

        Assert.Equal(new TeamAreaResponse(w.Department.Id, "Разработка", "Разработка"), Assert.Single(team.Items));
        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }
}
