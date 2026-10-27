using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Features.Organizations;
using Mustask.Application.Features.Organizations.GetArea;
using Mustask.Application.Features.Organizations.GetAreaDeletion;
using Mustask.Application.Features.Organizations.GetAreaProjects;
using Mustask.Application.Features.Organizations.GetAreaTeams;
using Mustask.Application.Features.Organizations.GetAreaTree;
using Mustask.Application.Features.Organizations.GetProjectAreas;
using Mustask.Application.Features.Organizations.GetTeamAreas;
using Mustask.Domain.Organizations;
using Xunit.Abstractions;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// mod-018, QA: the structure contract's area reads (US-6, ADR-0028 rules 4 and 8) and the statement counts of every new read
/// (spec § NFR Query shape) against a real PostgreSQL. The reads answer nothing for an id of another organization — the contract's own
/// test fails if one does not, no caller carries the check — and take the same number of statements for 5 areas as for 400.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class AreaReadsTests(OrganizationsDatabaseFixture database, ITestOutputHelper output)
{
    private const int TreeStatements = 1;
    private const int PageStatements = 1;
    private const int LinkedListStatements = 2;
    private const int DeletionStatements = 3;
    private const int ProjectAreasStatements = 4;
    private const int TeamAreasStatements = 3;

    private static Guid Org(SeededStructure structure) => structure.Organization.Id;

    private async Task<T> WithStructureAsync<T>(Func<IOrganizationStructure, Task<T>> read)
    {
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();

        return await read(scope.ServiceProvider.GetRequiredService<IOrganizationStructure>());
    }

    private async Task LinkAsync(Guid organization, Area area, Project? project = null, Team? team = null)
    {
        await using var context = database.NewContext();

        if (project is not null)
        {
            context.Set<AreaProject>().Add(AreaProject.Link(organization, area.Id, project.Id, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
        }

        if (team is not null)
        {
            context.Set<AreaTeam>().Add(AreaTeam.Link(organization, area.Id, team.Id, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
        }

        await context.SaveChangesAsync();
    }

    // US-6 · "The structure contract reads areas inside the organization": a foreign area, the projects linked to it and the areas of a foreign
    // project or team read as nothing; the organization's own read as before.
    [Fact]
    public async Task StructureContract_AForeignIdReadsAsNothing_TheOwnReadsAsBefore()
    {
        var mine = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        var logistics = OrganizationsDatabaseFixture.NewArea(Org(mine), null, "Логистика");
        var storage = OrganizationsDatabaseFixture.NewArea(Org(mine), logistics.Id, "Складские системы");
        var foreign = OrganizationsDatabaseFixture.NewArea(Org(other), null, "Логистика");
        await database.AddAreasAsync(logistics, storage, foreign);
        await LinkAsync(Org(mine), logistics, mine.Project, mine.FirstTeam);
        await LinkAsync(Org(other), foreign, other.Project, other.FirstTeam);

        Assert.Null(await WithStructureAsync(s => s.FindAreaAsync(Org(mine), foreign.Id, default)));
        Assert.Empty(await WithStructureAsync(s => s.ListLinkedProjectsOfAreaAsync(Org(mine), foreign.Id, true, default)));
        Assert.Empty(await WithStructureAsync(s => s.ListAreasOfProjectAsync(Org(mine), other.Project.Id, default)));
        Assert.Empty(await WithStructureAsync(s => s.ListAreasOfTeamAsync(Org(mine), other.FirstTeam.Id, default)));
        Assert.Null(await WithStructureAsync(s => s.FindAreaAsync(Org(other), logistics.Id, default)));

        var found = await WithStructureAsync(s => s.FindAreaAsync(Org(mine), storage.Id, default));
        Assert.Equal(("Логистика / Складские системы", logistics.Id), (found!.Path, found.ParentId));
        Assert.Equal(logistics.Id, Assert.Single(await WithStructureAsync(s => s.ListAreasOfProjectAsync(Org(mine), mine.Project.Id, default))).Id);
        Assert.Equal(logistics.Id, Assert.Single(await WithStructureAsync(s => s.ListAreasOfTeamAsync(Org(mine), mine.FirstTeam.Id, default))).Id);
    }

    // US-6 · the area-to-projects resolution: Alpha once for «Логистика» with its descendants however many of the areas link it, a deactivated
    // project left out, and with no descendants only the area's own links.
    [Fact]
    public async Task ListLinkedProjectsOfArea_EachProjectOnce_DeactivatedLeftOut_DescendantsOnRequest()
    {
        var structure = await database.CreateStructureAsync();
        var organization = Org(structure);
        var gamma = await database.AddProjectAsync(organization, OrganizationsDatabaseFixture.UniqueName("Гамма"), "GAM");
        var logistics = OrganizationsDatabaseFixture.NewArea(organization, null, "Логистика");
        var storage = OrganizationsDatabaseFixture.NewArea(organization, logistics.Id, "Складские системы");
        var deep = OrganizationsDatabaseFixture.NewArea(organization, storage.Id, "Глубже");
        await database.AddAreasAsync(logistics, storage, deep);
        await LinkAsync(organization, logistics, structure.Project);
        await LinkAsync(organization, storage, structure.Project);
        await LinkAsync(organization, storage, gamma);
        await LinkAsync(organization, deep, gamma);
        await database.ExecuteAsync("UPDATE projects SET status = 'Deactivated' WHERE id = @id", ("id", gamma.Id));

        var own = await WithStructureAsync(s => s.ListLinkedProjectsOfAreaAsync(organization, logistics.Id, false, default));
        var withDescendants = await WithStructureAsync(s => s.ListLinkedProjectsOfAreaAsync(organization, logistics.Id, true, default));
        var storageOnly = await WithStructureAsync(s => s.ListLinkedProjectsOfAreaAsync(organization, storage.Id, false, default));

        Assert.Equal(structure.Project.Id, Assert.Single(own).Id);
        Assert.Equal(structure.Project.Id, Assert.Single(withDescendants).Id);
        Assert.Equal(structure.Project.Id, Assert.Single(storageOnly).Id);

        await database.ExecuteAsync("UPDATE projects SET status = 'Active' WHERE id = @id", ("id", gamma.Id));
        var restored = await WithStructureAsync(s => s.ListLinkedProjectsOfAreaAsync(organization, logistics.Id, true, default));
        Assert.Equal(2, restored.Count);
        Assert.Single(restored, project => project.Id == gamma.Id);
    }

    // NFR Query shape · every new read takes the same number of statements for a small organization and for a large one.
    [Fact]
    public async Task NewReads_TheSameNumberOfStatements_For5AreasAs400()
    {
        var small = await SeedAsync(chains: 1, depth: 5, linked: 3);
        var large = await SeedAsync(chains: 40, depth: 10, linked: 120);

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
        yield return ("projects", LinkedListStatements, (p, s) => p.GetRequiredService<GetAreaProjectsHandler>().Handle(new GetAreaProjectsQuery(s.Organization.ToString(), s.Hub.ToString()), default));
        yield return ("teams", LinkedListStatements, (p, s) => p.GetRequiredService<GetAreaTeamsHandler>().Handle(new GetAreaTeamsQuery(s.Organization.ToString(), s.Hub.ToString()), default));
        yield return ("deletion", DeletionStatements, (p, s) => p.GetRequiredService<GetAreaDeletionHandler>().Handle(new GetAreaDeletionQuery(s.Organization.ToString(), s.Leaf.ToString()), default));
        yield return ("project areas", ProjectAreasStatements, (p, s) => p.GetRequiredService<GetProjectAreasHandler>().Handle(new GetProjectAreasQuery(s.Organization.ToString(), s.Project.ToString()), default));
        yield return ("team areas", TeamAreasStatements, (p, s) => p.GetRequiredService<GetTeamAreasHandler>().Handle(new GetTeamAreasQuery(s.Organization.ToString(), s.Team.ToString()), default));
    }

    private sealed record Seeded(Guid Organization, Guid Hub, Guid Leaf, Guid Project, Guid Team);

    /// <summary>
    /// <paramref name="chains"/> chains of <paramref name="depth"/> levels, <paramref name="linked"/> projects and as many teams all linked
    /// to the first area (the hub) and to a leaf, and the first project and team linked to every chain's top.
    /// </summary>
    private async Task<Seeded> SeedAsync(int chains, int depth, int linked)
    {
        var structure = await database.CreateStructureAsync();
        var organization = Org(structure);
        var areas = new List<Area>();
        Area? hub = null;
        Area? leaf = null;

        for (var chain = 0; chain < chains; chain++)
        {
            Guid? parent = null;

            for (var level = 1; level <= depth; level++)
            {
                var area = OrganizationsDatabaseFixture.NewArea(organization, parent, $"Ветка {chain:D3} уровень {level}");
                areas.Add(area);
                parent = area.Id;
                hub ??= area;
                leaf = area;
            }
        }

        await database.AddAreasAsync([.. areas]);

        await using var context = database.NewContext();

        for (var index = 0; index < linked; index++)
        {
            var project = Project.Create(organization, OrganizationsDatabaseFixture.Name($"Проект {index:D3}"), $"Q{index:D3}", OrganizationsDatabaseFixture.Now, OrganizationsDatabaseFixture.Creator);
            var team = Team.Create(organization, OrganizationsDatabaseFixture.Name($"Команда {index:D3}"));
            context.Projects.Add(project);
            context.Teams.Add(team);
            context.Set<AreaProject>().Add(AreaProject.Link(organization, hub!.Id, project.Id, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
            context.Set<AreaProject>().Add(AreaProject.Link(organization, leaf!.Id, project.Id, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
            context.Set<AreaTeam>().Add(AreaTeam.Link(organization, hub.Id, team.Id, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
            context.Set<AreaTeam>().Add(AreaTeam.Link(organization, leaf.Id, team.Id, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
        }

        // The first project and team are the ones the project's and the team's reads are asked about: linked to the top of every chain.
        foreach (var top in areas.Where(area => area.ParentId is null))
        {
            context.Set<AreaProject>().Add(AreaProject.Link(organization, top.Id, structure.Project.Id, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
            context.Set<AreaTeam>().Add(AreaTeam.Link(organization, top.Id, structure.FirstTeam.Id, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
        }

        await context.SaveChangesAsync();

        return new Seeded(organization, hub!.Id, leaf!.Id, structure.Project.Id, structure.FirstTeam.Id);
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
