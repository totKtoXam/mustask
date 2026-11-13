using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure.Tests.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static Mustask.Infrastructure.Tests.Organizations.OrganizationsDatabaseFixture;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// feat-043 US-2, US-8 (BR-NV-9, BR-NV-13, BR-NV-17, NFR Volumes) · the navigation reads against a real PostgreSQL: what each
/// returns, in which order, and the pinned number of statements whatever the volume — seven for «Обзор», four for the selectors'
/// offers, at five projects and at two hundred.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class NavigationReadModelTests(OrganizationsDatabaseFixture database)
{
    private const int OverviewStatements = 7;
    private const int OffersStatements = 4;

    // US-2 "Обзор": departments only in the count (OQ-2 A), active projects and teams once each; the lists are cut to five by name.
    [Fact]
    public async Task Overview_CountsDepartmentsProjectsAndTeamsOnce_ListsFirstFiveByName_SevenStatements()
    {
        var world = await SeedAsync(projects: 8, teams: 7, departments: 6);

        var counter = new CommandCountingInterceptor();
        var rows = await ReadAsync(counter, world.Organization, "u-olga", (model, caller) => model.ReadOverviewAsync(caller, CancellationToken.None));

        Assert.Equal(OverviewStatements, counter.Commands.Count);
        Assert.Equal((7, 9, 9), (rows.DepartmentCount, rows.ProjectCount, rows.TeamCount));
        Assert.Equal(NavigationLimits.OverviewRows, rows.Departments.Count);
        Assert.Equal(NavigationLimits.OverviewRows, rows.Projects.Count);
        Assert.Equal(NavigationLimits.OverviewRows, rows.Teams.Count);
        Assert.Equal(["Департамент 000", "Департамент 001", "Департамент 002", "Департамент 003", "Департамент 004"], rows.Departments.Select(row => row.Name));
        Assert.Equal(["PRM", "P000", "P001", "P002", "P003"], rows.Projects.Select(row => row.Key));
    }

    // US-2 · "the team block follows her teams" (BR-NV-10): her own teams are all listed, others' are not.
    [Fact]
    public async Task Overview_MyTeams_AreTheCallersAll_NotTheFirstFive()
    {
        var world = await SeedAsync(projects: 1, teams: 8, departments: 0);
        await database.AddMemberAsync(world.Organization, "u-olga");

        foreach (var team in world.Teams.Skip(1).Take(7))
        {
            await database.AddTeamMemberAsync(world.Organization, team, "u-olga");
        }

        var rows = await ReadAsync(null, world.Organization, "u-olga", (model, caller) => model.ReadOverviewAsync(caller, CancellationToken.None));
        var none = await ReadAsync(null, world.Organization, "u-vera", (model, caller) => model.ReadOverviewAsync(caller, CancellationToken.None));

        Assert.Equal(7, rows.MyTeams.Count);
        Assert.All(rows.MyTeams, team => Assert.True(team.IsMember));
        Assert.Empty(none.MyTeams);
        Assert.Equal(NavigationLimits.OverviewRows, rows.Teams.Count);
    }

    // BR-NV-13 · a deactivated project is in no count, row or link; another organization's rows never appear.
    [Fact]
    public async Task Reads_LeaveOutDeactivatedProjectsAndOtherOrganizations()
    {
        var structure = await database.CreateStructureAsync();
        var organizationId = structure.Organization.Id;
        var old = await database.AddProjectAsync(organizationId, "Старый", "OLD");
        await database.ExecuteAsync("UPDATE projects SET status = 'Deactivated' WHERE id = @id", ("id", old.Project.Id));
        await database.LinkAsync(organizationId, old.Project.Id, structure.SecondTeam.Id);
        await database.CreateStructureAsync();

        var overview = await ReadAsync(null, organizationId, "u-x", (model, caller) => model.ReadOverviewAsync(caller, CancellationToken.None));
        var offers = await ReadAsync(null, organizationId, "u-x", (model, caller) => model.ReadSelectorOffersAsync(caller, CancellationToken.None));

        Assert.Equal((1, 1, 2), (overview.DepartmentCount, overview.ProjectCount, overview.TeamCount));
        Assert.Equal(["Пример проекта"], offers.Projects.Select(project => project.Name));
        Assert.Equal(2, offers.Projects[0].TeamIds.Count);
    }

    // US-8 / BR-NV-17 · the offers carry the project's place and the links the panel judges the fit from.
    [Fact]
    public async Task SelectorOffers_CarryPlacesAndLinks_FiveStatements()
    {
        var structure = await database.CreateStructureAsync();
        var organizationId = structure.Organization.Id;
        var logistics = NewDepartment(organizationId, "Логистика");
        var finance = NewDepartment(organizationId, "Финансы");
        await database.AddAreasAsync(logistics, finance);
        await PlaceAsync(structure.Project.Root.Id, logistics.Id);
        await database.AddProjectAsync(organizationId, "Верхний", "TOP");

        var counter = new CommandCountingInterceptor();
        var offers = await ReadAsync(counter, organizationId, "u-x", (model, caller) => model.ReadSelectorOffersAsync(caller, CancellationToken.None));

        Assert.Equal(OffersStatements, counter.Commands.Count);
        Assert.Equal(["Логистика", "Отдел структуры", "Финансы"], offers.Departments.Select(department => department.Name));
        var placed = offers.Projects.Single(project => project.Key == "PRM");
        Assert.Equal((logistics.Id, "Логистика"), (placed.DepartmentId, placed.DepartmentName));
        Assert.Equivalent(new[] { structure.FirstTeam.Id, structure.SecondTeam.Id }, placed.TeamIds);
        var unplaced = offers.Projects.Single(project => project.Key == "TOP");
        Assert.Null(unplaced.DepartmentId);
        Assert.Empty(unplaced.TeamIds);
        // mod-021: a team has one place, the department it stands in, and the colour it was given.
        Assert.All(offers.Teams, team => Assert.Equal(structure.Department.Id, team.DepartmentId));
        Assert.Equal("magenta", offers.Teams.Single(team => team.Id == structure.SecondTeam.Id).Color);
        Assert.Equal("blue", placed.Color);
    }

    // NFR Volumes · the statement count does not grow with the objects: the same at 5 projects and at 200 projects, 200 teams, 20 departments.
    [Theory]
    [InlineData(5, 5, 2)]
    [InlineData(200, 200, 20)]
    public async Task BothReads_StatementCountIsPinned_AtFiveAndAtTwoHundredObjects(int projects, int teams, int departments)
    {
        var world = await SeedAsync(projects, teams, departments);

        var overviewCounter = new CommandCountingInterceptor();
        var overview = await ReadAsync(overviewCounter, world.Organization, "u-x", (model, caller) => model.ReadOverviewAsync(caller, CancellationToken.None));
        var offersCounter = new CommandCountingInterceptor();
        var offers = await ReadAsync(offersCounter, world.Organization, "u-x", (model, caller) => model.ReadSelectorOffersAsync(caller, CancellationToken.None));

        Assert.Equal(OverviewStatements, overviewCounter.Commands.Count);
        Assert.Equal(OffersStatements, offersCounter.Commands.Count);
        Assert.Equal(projects + 1, overview.ProjectCount);
        Assert.Equal(projects + 1, offers.Projects.Count);
        Assert.Equal(teams + 2, offers.Teams.Count);
        Assert.Equal(departments + 1, offers.Departments.Count);
    }

    private async Task<World> SeedAsync(int projects, int teams, int departments)
    {
        var structure = await database.CreateStructureAsync();
        var organizationId = structure.Organization.Id;
        await using var context = database.NewContext();

        for (var p = 0; p < projects; p++)
        {
            var project = Project.Create(organizationId, Name($"Проект {p:D3}"), $"P{p:D3}", Now, Creator, "blue");
            context.Projects.Add(project.Project);
            context.Areas.Add(project.Root);
            context.ProjectTeams.Add(ProjectTeam.Link(organizationId, project.Project.Id, structure.FirstTeam.Id, Creator, Now));
        }

        var teamIds = new List<Guid> { structure.FirstTeam.Id, structure.SecondTeam.Id };

        for (var t = 0; t < teams; t++)
        {
            var team = Team.Create(organizationId, structure.Department.Id, "blue", Name($"Команда {t:D3}"));
            context.Teams.Add(team);
            teamIds.Add(team.Id);
        }

        for (var d = 0; d < departments; d++)
        {
            var department = NewDepartment(organizationId, $"Департамент {d:D3}");
            context.Areas.Add(department);
        }

        await context.SaveChangesAsync();

        return new World(organizationId, teamIds);
    }

    private async Task PlaceAsync(Guid nodeId, Guid departmentId)
    {
        await using var context = database.NewContext();
        var node = await context.Areas.SingleAsync(area => area.Id == nodeId);
        context.Entry(node).Property(nameof(Area.ParentId)).CurrentValue = departmentId;
        await context.SaveChangesAsync();
    }

    private async Task<TResult> ReadAsync<TResult>(
        IInterceptor? interceptor, Guid organizationId, string userId, Func<INavigationReadModel, CallerAccess, Task<TResult>> read)
    {
        await using var provider = interceptor is null ? database.BuildServices() : database.BuildServices(interceptor);
        await using var scope = provider.CreateAsyncScope();

        return await read(scope.ServiceProvider.GetRequiredService<INavigationReadModel>(), new CallerAccess(organizationId, userId, OrganizationRole.Member));
    }

    private sealed record World(Guid Organization, IReadOnlyList<Guid> Teams);
}
