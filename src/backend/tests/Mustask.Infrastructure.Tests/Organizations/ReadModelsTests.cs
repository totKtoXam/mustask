using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure.Tests.Process;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static Mustask.Infrastructure.Tests.Organizations.OrganizationsDatabaseFixture;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// The four read models against a real PostgreSQL: what each returns, in which
/// order, and with a fixed number of queries whatever the size (NFR Volumes,
/// docs/conventions/backend.md "EF Core").
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class ReadModelsTests(OrganizationsDatabaseFixture database)
{
    // NFR Volumes: hundreds of areas, ten levels deep, in two queries: the project check and the nodes.
    [Fact]
    public async Task AreaTree_GetNodes_FiveHundredNodesTenLevels_TwoQueries()
    {
        var structure = await database.CreateStructureAsync();
        var areas = new List<Area>();

        for (var branch = 0; branch < 50; branch++)
        {
            var parent = structure.Project.Root;

            for (var level = 1; level <= 10; level++)
            {
                var child = Area.CreateChild(parent, Name($"Узел {branch}-{level}"));
                areas.Add(child);
                parent = child;
            }
        }

        await database.AddAreasAsync([.. areas]);

        var counter = new CommandCountingInterceptor();
        var nodes = await ReadAsync<IAreaTreeReadModel, IReadOnlyList<AreaTreeRow>?>(
            counter,
            readModel => readModel.GetNodesAsync(structure.Organization.Id, structure.Project.Project.Id, CancellationToken.None));

        Assert.Equal(2, counter.Commands.Count);
        Assert.Equal(501, nodes!.Count);
        Assert.Equal(10, new AreaTree(nodes.Select(row => row.ToNode())).DepthOf(areas[^1].Id));
    }

    // mod-017 api-contract § 1.6 (BR-OT-10): a row is a node's place and name and nothing about a team — no join to the teams.
    // Replaces AreaTree_GetNodes_CarriesTheNamesOfTheTeamsEachNodeRefersTo (retired, BR-OT-10).
    [Fact]
    public async Task AreaTree_GetNodes_ReturnsPlaceAndName_JoinsNoTeam()
    {
        var structure = await database.CreateStructureAsync();
        var api = Area.CreateChild(structure.Project.Root, Name("API"));
        await database.AddAreasAsync(api);

        var counter = new CommandCountingInterceptor();
        var nodes = await ReadAsync<IAreaTreeReadModel, IReadOnlyList<AreaTreeRow>?>(
            counter,
            readModel => readModel.GetNodesAsync(structure.Organization.Id, structure.Project.Project.Id, CancellationToken.None));

        Assert.Equal(
            [(api.Id, (Guid?)structure.Project.Root.Id, "API"), (structure.Project.Root.Id, null, "Пример проекта")],
            nodes!.Select(row => (row.Id, row.ParentId, row.Name)).OrderBy(row => row.Name, StringComparer.Ordinal));
        Assert.DoesNotContain(counter.Commands, command => command.Contains("teams", StringComparison.OrdinalIgnoreCase));
    }

    // BR-M1-20, BR-M1-22: the tree of an unreachable project, or of a project of another organization, is not served.
    [Fact]
    public async Task AreaTree_GetNodes_UnreachableOrForeignProject_IsNull()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();

        var foreign = await ReadAsync<IAreaTreeReadModel, IReadOnlyList<AreaTreeRow>?>(
            null,
            readModel => readModel.GetNodesAsync(other.Organization.Id, structure.Project.Project.Id, CancellationToken.None));
        await database.ExecuteAsync("UPDATE projects SET status = 'Deactivated' WHERE id = @id", ("id", structure.Project.Project.Id));
        var deactivated = await ReadAsync<IAreaTreeReadModel, IReadOnlyList<AreaTreeRow>?>(
            null,
            readModel => readModel.GetNodesAsync(structure.Organization.Id, structure.Project.Project.Id, CancellationToken.None));

        Assert.Null(foreign);
        Assert.Null(deactivated);
    }

    // mod-017 US-4 · "The project's teams": by name in Russian collation, letter case not deciding, no team first (BR-OT-11), each
    // with its default mark (BR-OT-4), all in one statement. Replaces
    // Teams_ListActive_FirstTeamFirstThenRussianOrder_WithDefaultAreas (D-16, BR-OT-11).
    [Fact]
    public async Task Teams_ListActive_ByName_WithTheDefaultMark_OneStatement()
    {
        var structure = await database.CreateStructureAsync(projectName: "Ядро");
        var projectId = structure.Project.Project.Id;
        var alpha = Team.Create(projectId, Name("альфа"));
        await AddTeamAsync(alpha);

        var counter = new CommandCountingInterceptor();
        var none = await ReadAsync<ITeamReadModel, IReadOnlyList<TeamRow>>(
            counter,
            readModel => readModel.ListActiveAsync(structure.Organization.Id, projectId, "nobody", CancellationToken.None));
        await database.ExecuteAsync("UPDATE projects SET default_team_id = @team WHERE id = @project", ("team", structure.SecondTeam.Id), ("project", projectId));
        var named = await ReadAsync<ITeamReadModel, IReadOnlyList<TeamRow>>(
            null,
            readModel => readModel.ListActiveAsync(structure.Organization.Id, projectId, "nobody", CancellationToken.None));

        Assert.Single(counter.Commands);
        Assert.Equal(["альфа", "Платформа", "Ядро"], none.Select(team => team.Name));
        Assert.All(none, team => Assert.Equal(DefaultTeamMark.None, team.Mark));
        Assert.Equal(
            [DefaultTeamMark.None, DefaultTeamMark.Named, DefaultTeamMark.None],
            named.Select(team => team.Mark));
    }

    // mod-017 BR-OT-4 · a project's only active team carries «onlyTeam» when the settings name none.
    [Fact]
    public async Task Teams_TheOnlyActiveTeam_IsMarkedOnlyTeam_UnlessTheSettingsNameAnother()
    {
        var structure = await database.CreateStructureAsync();
        var projectId = structure.Project.Project.Id;
        await database.ExecuteAsync("UPDATE teams SET status = 'Deleted' WHERE id = @second", ("second", structure.SecondTeam.Id));

        var only = await ReadAsync<ITeamReadModel, TeamRow?>(
            null,
            readModel => readModel.FindReachableAsync(structure.Organization.Id, projectId, structure.Project.Team.Id, CancellationToken.None));

        Assert.Equal(DefaultTeamMark.OnlyTeam, only!.Mark);
        Assert.False(only.IsMember);
    }

    // US-7 · "A deleted team answers not found": it leaves the list and the by-id read.
    [Fact]
    public async Task Teams_DeletedTeam_IsLeftOut()
    {
        var structure = await database.CreateStructureAsync();
        var projectId = structure.Project.Project.Id;
        await database.ExecuteAsync("UPDATE teams SET status = 'Deleted' WHERE id = @second", ("second", structure.SecondTeam.Id));

        var teams = await ReadAsync<ITeamReadModel, IReadOnlyList<TeamRow>>(
            null,
            readModel => readModel.ListActiveAsync(structure.Organization.Id, projectId, "nobody", CancellationToken.None));
        var deleted = await ReadAsync<ITeamReadModel, TeamRow?>(
            null,
            readModel => readModel.FindReachableAsync(structure.Organization.Id, projectId, structure.SecondTeam.Id, CancellationToken.None));

        Assert.Equal([structure.Project.Team.Id], teams.Select(team => team.Id));
        Assert.Null(deleted);
    }

    // US-2 · "listed separately": one status per list; the active one by id only while active. mod-016 BR-M1-7: the lists hold
    // the organizations the user is a member of, and nothing of the others.
    [Fact]
    public async Task Organizations_ListAndFind_FollowTheStatus()
    {
        var user = "u-list-" + Guid.CreateVersion7().ToString("N");
        var active = await database.CreateStructureAsync();
        var deactivated = await database.CreateStructureAsync();
        await database.ExecuteAsync("UPDATE organizations SET status = 'Deactivated' WHERE id = @id", ("id", deactivated.Organization.Id));
        await database.AddMemberAsync(active.Organization.Id, user, OrganizationRole.Viewer);
        await database.AddMemberAsync(deactivated.Organization.Id, user, OrganizationRole.Admin);

        var activeList = await ReadAsync<IOrganizationReadModel, IReadOnlyList<OrganizationRow>>(
            null, readModel => readModel.ListAsync(OrganizationStatus.Active, user, null, CancellationToken.None));
        var deactivatedList = await ReadAsync<IOrganizationReadModel, IReadOnlyList<OrganizationRow>>(
            null, readModel => readModel.ListAsync(OrganizationStatus.Deactivated, user, null, CancellationToken.None));
        var foundDeactivated = await ReadAsync<IOrganizationReadModel, OrganizationRow?>(
            null, readModel => readModel.FindActiveAsync(deactivated.Organization.Id, CancellationToken.None));

        Assert.Equal([active.Organization.Id], activeList.Select(row => row.Id));
        Assert.Equal([deactivated.Organization.Id], deactivatedList.Select(row => row.Id));
        Assert.Null(foundDeactivated);
    }

    // mod-016 US-2 · "The organization list holds only the user's organizations" and "Deactivated lists and restore are the
    // administrators'" (BR-M1-7, BR-M1-35): the membership narrows the list in the one statement — a user of no organization, a
    // user of another, and a role outside `onlyRoles` see nothing; the organization's other members do not change it.
    [Fact]
    public async Task Organizations_List_IsNarrowedByTheCallersMembershipAndRole()
    {
        var admin = "u-admin-" + Guid.CreateVersion7().ToString("N");
        var member = "u-member-" + Guid.CreateVersion7().ToString("N");
        var outsider = "u-outsider-" + Guid.CreateVersion7().ToString("N");
        var organization = await database.CreateStructureAsync();
        await database.ExecuteAsync("UPDATE organizations SET status = 'Deactivated' WHERE id = @id", ("id", organization.Organization.Id));
        await database.AddMemberAsync(organization.Organization.Id, admin, OrganizationRole.Admin);
        await database.AddMemberAsync(organization.Organization.Id, member, OrganizationRole.Member);
        IReadOnlyCollection<OrganizationRole> restoring = [OrganizationRole.Admin];

        async Task<IReadOnlyList<Guid>> ListedAsync(string user, IReadOnlyCollection<OrganizationRole>? roles) =>
            (await ReadAsync<IOrganizationReadModel, IReadOnlyList<OrganizationRow>>(
                null, readModel => readModel.ListAsync(OrganizationStatus.Deactivated, user, roles, CancellationToken.None)))
            .Select(row => row.Id).ToList();

        Assert.Equal([organization.Organization.Id], await ListedAsync(admin, restoring));
        Assert.Equal([organization.Organization.Id], await ListedAsync(member, null));
        Assert.Empty(await ListedAsync(member, restoring));
        Assert.Empty(await ListedAsync(outsider, null));
        Assert.Empty(await ListedAsync(outsider, restoring));
    }

    // US-3 · "deactivated" and BR-M1-22: a project is found only reachable and only under its own organization.
    [Fact]
    public async Task Projects_FindReachable_OnlyActiveAndOnlyInItsOrganization()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        var projectId = structure.Project.Project.Id;

        var found = await ReadAsync<IProjectReadModel, ProjectRow?>(
            null, readModel => readModel.FindReachableAsync(structure.Organization.Id, projectId, CancellationToken.None));
        var foreign = await ReadAsync<IProjectReadModel, ProjectRow?>(
            null, readModel => readModel.FindReachableAsync(other.Organization.Id, projectId, CancellationToken.None));

        Assert.Equal(new ProjectRow(projectId, structure.Organization.Id, "Пример проекта", "PRM"), found);
        Assert.Null(foreign);
    }

    private async Task AddTeamAsync(Team team)
    {
        await using var context = database.NewContext();
        context.Teams.Add(team);
        await context.SaveChangesAsync();
    }

    private async Task<TResult> ReadAsync<TReadModel, TResult>(IInterceptor? interceptor, Func<TReadModel, Task<TResult>> read)
        where TReadModel : notnull
    {
        await using var provider = interceptor is null ? database.BuildServices() : database.BuildServices(interceptor);
        await using var scope = provider.CreateAsyncScope();

        return await read(scope.ServiceProvider.GetRequiredService<TReadModel>());
    }
}
