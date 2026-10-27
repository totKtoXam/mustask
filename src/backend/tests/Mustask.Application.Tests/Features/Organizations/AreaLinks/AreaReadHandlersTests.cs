using Mustask.Application.Features.Organizations.GetArea;
using Mustask.Application.Features.Organizations.GetAreaDeletion;
using Mustask.Application.Features.Organizations.GetAreaProjects;
using Mustask.Application.Features.Organizations.GetAreaTeams;
using Mustask.Application.Features.Organizations.GetProjectAreas;
using Mustask.Application.Features.Organizations.GetTeamAreas;
using Mustask.Application.Features.Organizations.SetAreaDescription;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.AreaLinks;

/// <summary>mod-018 US-1 / US-3 / US-4 on fakes: the area page, its description, the deletion dialog and the link lists of the three pages.</summary>
public sealed class AreaReadHandlersTests
{
    private sealed class World
    {
        public World()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Team = Store.AddTeam(Organization);
            Reports = Store.AddArea(Organization, "Отчёты");
            Quarterly = Store.AddArea(Organization, "Квартальные", Reports);
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public Project Project { get; }

        public Team Team { get; }

        public Area Reports { get; }

        public Area Quarterly { get; }

        public string Org => Organization.Id.ToString();

        public FakeAreaTreeReadModel Reads => new(Store);

        public FakeAreaRepository Areas => new(Store);

        public FakeOrganizationStructure Structure => new(Store);
    }

    private static CancellationToken Ct => CancellationToken.None;

    // US-1 · the page of an area: its parent with the path, its direct children, its own path.
    [Fact]
    public async Task GetArea_ShowsTheParentThePathAndTheChildren()
    {
        var w = new World();

        var child = (await new GetAreaHandler(w.Reads).Handle(new GetAreaQuery(w.Org, w.Quarterly.Id.ToString()), Ct)).Response;
        var parent = (await new GetAreaHandler(w.Reads).Handle(new GetAreaQuery(w.Org, w.Reports.Id.ToString()), Ct)).Response;

        Assert.Equal("Отчёты / Квартальные", child.Path);
        Assert.Equal(new AreaRefResponse(w.Reports.Id, "Отчёты", "Отчёты"), child.Parent);
        Assert.Empty(child.Children);
        Assert.Null(parent.Parent);
        Assert.Equal(w.Quarterly.Id, Assert.Single(parent.Children).Id);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task GetArea_NotAnAreaOfTheOrganization_IsAreaNotFound(string how)
    {
        var w = new World();
        var foreign = w.Store.AddArea(w.Store.AddOrganization("Другая"), "Чужая");
        var id = how switch { "malformed" => "x", "unknown" => Guid.CreateVersion7().ToString(), _ => foreign.Id.ToString() };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => new GetAreaHandler(w.Reads).Handle(new GetAreaQuery(w.Org, id), Ct));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
    }

    // US-1 · "A description is set and cleared apart from the name": kept as typed, an empty one clears it.
    [Fact]
    public async Task SetAreaDescription_SetsAndClears_KeepsTheName()
    {
        var w = new World();
        var handler = new SetAreaDescriptionHandler(new FakeOrganizationReadModel(w.Store), w.Areas, new FakeUnitOfWork());

        var set = await handler.Handle(new SetAreaDescriptionCommand(w.Org, w.Reports.Id.ToString(), " Всё про отчёты\n"), Ct);
        var cleared = await handler.Handle(new SetAreaDescriptionCommand(w.Org, w.Reports.Id.ToString(), ""), Ct);

        Assert.Equal(" Всё про отчёты\n", set.Response.Description);
        Assert.Null(cleared.Response.Description);
        Assert.Equal("Отчёты", cleared.Response.Name);
        Assert.Equal([$"lock area update {w.Reports.Id}", "save", $"lock area update {w.Reports.Id}", "save"], w.Store.Calls);
    }

    [Fact]
    public async Task SetAreaDescription_TooLong_IsValidationFailed_UnknownAreaIsNotFound()
    {
        var w = new World();
        var handler = new SetAreaDescriptionHandler(new FakeOrganizationReadModel(w.Store), w.Areas, new FakeUnitOfWork());

        var invalid = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new SetAreaDescriptionCommand(w.Org, w.Reports.Id.ToString(), new string('я', 2001)), Ct));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new SetAreaDescriptionCommand(w.Org, Guid.CreateVersion7().ToString(), new string('я', 2001)), Ct));

        Assert.Equal(["description"], invalid.Errors.Keys);
        Assert.Equal(OrganizationErrorCodes.AreaNotFound, missing.Code);
    }

    // US-2 · "The deletion dialog lists what the deletion ends": every project link (the deactivated ones too) and every team link.
    [Fact]
    public async Task GetAreaDeletion_ListsEveryLinkItWouldEnd()
    {
        var w = new World();
        var old = w.Store.AddProject(w.Organization, "Старый", "OLD");
        w.Store.LinkArea(w.Quarterly, w.Project);
        w.Store.LinkArea(w.Quarterly, old);
        w.Store.LinkArea(w.Quarterly, w.Team);
        old.Deactivate();

        var result = await new GetAreaDeletionHandler(w.Areas, w.Reads).Handle(new GetAreaDeletionQuery(w.Org, w.Quarterly.Id.ToString()), Ct);

        Assert.Equal(["Пример проекта", "Старый"], result.Projects.Select(project => project.Name));
        Assert.Equal([true, false], result.Projects.Select(project => project.Active));
        Assert.Equal("Платформа", Assert.Single(result.Teams).Name);
    }

    [Fact]
    public async Task GetAreaDeletion_UnknownArea_IsAreaNotFound()
    {
        var w = new World();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetAreaDeletionHandler(w.Areas, w.Reads).Handle(new GetAreaDeletionQuery(w.Org, Guid.CreateVersion7().ToString()), Ct));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
    }

    // US-3 · the area's own page lists the active projects and the active teams (BR-AW-10: a deactivated project is inert).
    [Fact]
    public async Task GetAreaProjectsAndTeams_ListOnlyTheActiveOnes()
    {
        var w = new World();
        var old = w.Store.AddProject(w.Organization, "Старый", "OLD");
        var gone = w.Store.AddTeam(w.Organization, "Ушла");
        w.Store.LinkArea(w.Reports, w.Project);
        w.Store.LinkArea(w.Reports, old);
        w.Store.LinkArea(w.Reports, w.Team);
        w.Store.LinkArea(w.Reports, gone);
        old.Deactivate();
        gone.Delete();

        var projects = await new GetAreaProjectsHandler(w.Areas, w.Reads).Handle(new GetAreaProjectsQuery(w.Org, w.Reports.Id.ToString()), Ct);
        var teams = await new GetAreaTeamsHandler(w.Areas, w.Reads).Handle(new GetAreaTeamsQuery(w.Org, w.Reports.Id.ToString()), Ct);

        Assert.Equal(new AreaProjectResponse(w.Project.Id, "Пример проекта", "PRM"), Assert.Single(projects.Items));
        Assert.Equal(new AreaTeamResponse(w.Team.Id, "Платформа"), Assert.Single(teams.Items));
    }

    [Fact]
    public async Task GetAreaProjectsAndTeams_UnknownArea_IsAreaNotFound()
    {
        var w = new World();
        var id = Guid.CreateVersion7().ToString();

        var projects = await Assert.ThrowsAsync<NotFoundException>(() => new GetAreaProjectsHandler(w.Areas, w.Reads).Handle(new GetAreaProjectsQuery(w.Org, id), Ct));
        var teams = await Assert.ThrowsAsync<NotFoundException>(() => new GetAreaTeamsHandler(w.Areas, w.Reads).Handle(new GetAreaTeamsQuery(w.Org, id), Ct));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, projects.Code);
        Assert.Equal(OrganizationErrorCodes.AreaNotFound, teams.Code);
    }

    // US-3 / US-4 · a project's page and a team's page list their areas with the path.
    [Fact]
    public async Task GetProjectAreasAndTeamAreas_ListTheAreasWithTheirPaths()
    {
        var w = new World();
        w.Store.LinkArea(w.Quarterly, w.Project);
        w.Store.LinkArea(w.Quarterly, w.Team);

        var project = await new GetProjectAreasHandler(w.Structure).Handle(new GetProjectAreasQuery(w.Org, w.Project.Id.ToString()), Ct);
        var team = await new GetTeamAreasHandler(w.Structure).Handle(new GetTeamAreasQuery(w.Org, w.Team.Id.ToString()), Ct);

        Assert.Equal(new ProjectAreaResponse(w.Quarterly.Id, "Квартальные", "Отчёты / Квартальные"), Assert.Single(project.Items));
        Assert.Equal(new TeamAreaResponse(w.Quarterly.Id, "Квартальные", "Отчёты / Квартальные"), Assert.Single(team.Items));
    }

    [Fact]
    public async Task GetProjectAreasAndTeamAreas_DeactivatedProjectOrDeletedTeam_AreNotFound()
    {
        var w = new World();
        w.Project.Deactivate();
        w.Team.Delete();

        var project = await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetProjectAreasHandler(w.Structure).Handle(new GetProjectAreasQuery(w.Org, w.Project.Id.ToString()), Ct));
        var team = await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetTeamAreasHandler(w.Structure).Handle(new GetTeamAreasQuery(w.Org, w.Team.Id.ToString()), Ct));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, project.Code);
        Assert.Equal(OrganizationErrorCodes.TeamNotFound, team.Code);
    }
}
