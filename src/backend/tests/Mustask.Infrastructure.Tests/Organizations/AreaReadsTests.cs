using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Features.Organizations;
using Mustask.Application.Features.Organizations.GetArea;
using Mustask.Application.Features.Organizations.GetAreaDeletion;
using Mustask.Application.Features.Organizations.GetAreaTeams;
using Mustask.Application.Features.Organizations.GetAreaTree;
using Mustask.Application.Features.Organizations.GetProjectAreas;
using Mustask.Application.Features.Organizations.GetTeamAreas;
using Mustask.Domain.Organizations;
using Xunit.Abstractions;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// mod-018, the typed tree: the structure contract's area reads (US-6, ADR-0031) and the statement counts of every read (spec § NFR Query
/// shape) against a real PostgreSQL. The reads answer nothing for an id of another organization, and take the same number of statements for
/// a small organization as for a large one.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class AreaReadsTests(OrganizationsDatabaseFixture database, ITestOutputHelper output)
{
    private const int TreeStatements = 1;
    private const int PageStatements = 1;
    private const int LinkedListStatements = 2;
    private const int DeletionStatements = 2;
    private const int ProjectAreasStatements = 3;
    private const int TeamAreasStatements = 3;

    private static Guid Org(SeededStructure structure) => structure.Organization.Id;

    private async Task<T> WithStructureAsync<T>(Func<IOrganizationStructure, Task<T>> read)
    {
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();

        return await read(scope.ServiceProvider.GetRequiredService<IOrganizationStructure>());
    }

    private async Task LinkAsync(Guid organization, Area department, Team team)
    {
        await using var context = database.NewContext();

        context.Set<AreaTeam>().Add(AreaTeam.Link(organization, department.Id, team.Id, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
        await context.SaveChangesAsync();
    }

    // US-6 · "The structure contract reads areas inside the organization": a foreign area and the departments of a foreign team read as
    // nothing; the organization's own read with their type, project and path.
    [Fact]
    public async Task StructureContract_AForeignIdReadsAsNothing_TheOwnReadsWithTypeAndPath()
    {
        var mine = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        var department = OrganizationsDatabaseFixture.NewDepartment(Org(mine), "Разработка");
        var sub = OrganizationsDatabaseFixture.NewSubArea(mine.Project.Root, "Отчёты");
        var foreign = OrganizationsDatabaseFixture.NewDepartment(Org(other), "Разработка");
        await database.AddAreasAsync(department, sub, foreign);
        await LinkAsync(Org(mine), department, mine.FirstTeam);
        await LinkAsync(Org(other), foreign, other.FirstTeam);

        Assert.Null(await WithStructureAsync(s => s.FindAreaAsync(Org(mine), foreign.Id, default)));
        Assert.Empty(await WithStructureAsync(s => s.ListAreasOfTeamAsync(Org(mine), other.FirstTeam.Id, default)));
        Assert.Null(await WithStructureAsync(s => s.FindAreaAsync(Org(other), department.Id, default)));

        var found = await WithStructureAsync(s => s.FindAreaAsync(Org(mine), sub.Id, default));
        Assert.Equal((AreaType.Area, mine.Project.Project.Id, "Пример проекта / Отчёты"), (found!.Type, found.ProjectId, found.Path));
        var listed = Assert.Single(await WithStructureAsync(s => s.ListAreasOfTeamAsync(Org(mine), mine.FirstTeam.Id, default)));
        Assert.Equal((department.Id, AreaType.Department), (listed.Id, listed.Type));
    }

    // NFR Query shape · every read takes the same number of statements for a small organization and for a large one.
    [Fact]
    public async Task Reads_TheSameNumberOfStatements_ForASmallOrganizationAsForALargeOne()
    {
        var small = await SeedAsync(projects: 1, depth: 3, linked: 3);
        var large = await SeedAsync(projects: 40, depth: 10, linked: 120);

        var mismatches = new List<string>();

        foreach (var (name, expected, run) in Reads())
        {
            var smallCount = await CountAsync(small, run);
            var largeCount = await CountAsync(large, run);
            output.WriteLine($"{name}: {smallCount} / {largeCount}");

            Assert.True(smallCount == largeCount, $"{name}: {smallCount} statements for the small organization, {largeCount} for the large one.");
            if (expected != largeCount) { mismatches.Add($"{name}: pinned {expected}, got {largeCount}"); }
        }

        Assert.True(mismatches.Count == 0, string.Join("; ", mismatches));
    }

    private static IEnumerable<(string Name, int Expected, Func<IServiceProvider, Seeded, Task> Run)> Reads()
    {
        yield return ("tree", TreeStatements, (p, s) => p.GetRequiredService<GetAreaTreeHandler>().Handle(new GetAreaTreeQuery(s.Organization.ToString()), default));
        yield return ("page", PageStatements, (p, s) => p.GetRequiredService<GetAreaHandler>().Handle(new GetAreaQuery(s.Organization.ToString(), s.Hub.ToString()), default));
        yield return ("teams", LinkedListStatements, (p, s) => p.GetRequiredService<GetAreaTeamsHandler>().Handle(new GetAreaTeamsQuery(s.Organization.ToString(), s.Hub.ToString()), default));
        yield return ("deletion", DeletionStatements, (p, s) => p.GetRequiredService<GetAreaDeletionHandler>().Handle(new GetAreaDeletionQuery(s.Organization.ToString(), s.Hub.ToString()), default));
        yield return ("project areas", ProjectAreasStatements, (p, s) => p.GetRequiredService<GetProjectAreasHandler>().Handle(new GetProjectAreasQuery(s.Organization.ToString(), s.Project.ToString()), default));
        yield return ("team areas", TeamAreasStatements, (p, s) => p.GetRequiredService<GetTeamAreasHandler>().Handle(new GetTeamAreasQuery(s.Organization.ToString(), s.Team.ToString()), default));
    }

    private sealed record Seeded(Guid Organization, Guid Hub, Guid Project, Guid Team);

    /// <summary>
    /// <paramref name="projects"/> projects, each with a chain of sub-areas down to <paramref name="depth"/> levels, and <paramref name="linked"/>
    /// teams linked to a department (the hub); the first team is linked to the hub too.
    /// </summary>
    private async Task<Seeded> SeedAsync(int projects, int depth, int linked)
    {
        var structure = await database.CreateStructureAsync();
        var organization = Org(structure);
        var hub = OrganizationsDatabaseFixture.NewDepartment(organization, "Центр");
        var areas = new List<Area> { hub };
        var parent = structure.Project.Root;

        for (var level = 2; level <= depth; level++)
        {
            parent = OrganizationsDatabaseFixture.NewSubArea(parent, $"Уровень {level}");
            areas.Add(parent);
        }

        await database.AddAreasAsync([.. areas]);

        for (var index = 1; index < projects; index++)
        {
            await database.AddProjectAsync(organization, OrganizationsDatabaseFixture.UniqueName("Проект"), $"Q{index:D3}");
        }

        await using var context = database.NewContext();

        for (var index = 0; index < linked; index++)
        {
            var team = Team.Create(organization, OrganizationsDatabaseFixture.Name($"Команда {index:D3}"));
            context.Teams.Add(team);
            context.Set<AreaTeam>().Add(AreaTeam.Link(organization, hub.Id, team.Id, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
        }

        context.Set<AreaTeam>().Add(AreaTeam.Link(organization, hub.Id, structure.FirstTeam.Id, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
        await context.SaveChangesAsync();

        return new Seeded(organization, hub.Id, structure.Project.Project.Id, structure.FirstTeam.Id);
    }

    private async Task<int> CountAsync(Seeded seeded, Func<IServiceProvider, Seeded, Task> run)
    {
        var counter = new SqlLogInterceptor();
        await using var provider = database.BuildServices(counter);
        await using var scope = provider.CreateAsyncScope();

        await run(scope.ServiceProvider, seeded);

        return counter.Commands.Count;
    }
}
