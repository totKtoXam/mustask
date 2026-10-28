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
    // NFR Volumes: hundreds of nodes, ten levels deep, in one query.
    [Fact]
    public async Task AreaTree_GetRows_FiveHundredNodesTenLevels_OneQuery()
    {
        var structure = await database.CreateStructureAsync();
        var areas = new List<Area>();

        for (var branch = 0; branch < 50; branch++)
        {
            var parent = structure.Project.Root;

            for (var level = 2; level <= 10; level++)
            {
                var child = NewSubArea(parent, $"Узел {branch}-{level}");
                areas.Add(child);
                parent = child;
            }
        }

        await database.AddAreasAsync([.. areas]);

        var counter = new CommandCountingInterceptor();
        var rows = await ReadAsync<IAreaTreeReadModel, IReadOnlyList<AreaTreeRow>>(
            counter,
            readModel => readModel.GetRowsAsync(structure.Organization.Id, CancellationToken.None));

        Assert.Single(counter.Commands);
        Assert.Equal(451, rows.Count);
        Assert.Equal(10, new AreaTree(rows.Select(row => row.ToNode())).LevelOf(areas[^1].Id));
    }

    // A row is a node's place, name, type and project and nothing about a team: no join to the teams.
    [Fact]
    public async Task AreaTree_GetRows_ReturnsPlaceNameAndType_JoinsNoTeam()
    {
        var structure = await database.CreateStructureAsync();
        var api = NewSubArea(structure.Project.Root, "API");
        var department = NewDepartment(structure.Organization.Id, "Разработка");
        await database.AddAreasAsync(api, department);

        var counter = new CommandCountingInterceptor();
        var rows = await ReadAsync<IAreaTreeReadModel, IReadOnlyList<AreaTreeRow>>(
            counter,
            readModel => readModel.GetRowsAsync(structure.Organization.Id, CancellationToken.None));

        Assert.Equal(
            [
                (api.Id, (Guid?)structure.Project.Root.Id, "API", AreaType.Area),
                (structure.Project.Root.Id, null, "Пример проекта", AreaType.Project),
                (department.Id, null, "Разработка", AreaType.Department),
            ],
            rows.Select(row => (row.Id, row.ParentId, row.Name, row.Type)).OrderBy(row => row.Name, StringComparer.Ordinal));
        Assert.DoesNotContain(counter.Commands, command => command.Contains("teams", StringComparison.OrdinalIgnoreCase));
    }

    // OQ-6 A · a deactivated project's nodes stay in the rows, marked; another organization reads nothing of them.
    [Fact]
    public async Task AreaTree_GetRows_DeactivatedProjectIsMarked_ForeignOrganizationReadsNothing()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        await database.ExecuteAsync("UPDATE projects SET status = 'Deactivated' WHERE id = @id", ("id", structure.Project.Project.Id));

        var rows = await ReadAsync<IAreaTreeReadModel, IReadOnlyList<AreaTreeRow>>(
            null,
            readModel => readModel.GetRowsAsync(structure.Organization.Id, CancellationToken.None));
        var foreign = await ReadAsync<IAreaTreeReadModel, IReadOnlyList<AreaTreeRow>>(
            null,
            readModel => readModel.GetRowsAsync(Guid.CreateVersion7(), CancellationToken.None));

        var node = Assert.Single(rows);
        Assert.Equal((false, AreaType.Project), (node.ProjectActive, node.Type));
        Assert.Empty(foreign);
        Assert.Single(await ReadAsync<IAreaTreeReadModel, IReadOnlyList<AreaTreeRow>>(
            null,
            readModel => readModel.GetRowsAsync(other.Organization.Id, CancellationToken.None)));
    }

    // mod-017 US-4 · "The project's teams": by name in Russian collation, letter case not deciding, no team first (BR-OT-11), each
    // with its default mark (BR-OT-4), all in one statement; feat-028: over the project's LINKS. Renamed (D-16, BR-OL-6):
    // Teams_ListActive_ByName_WithTheDefaultMark_OneStatement -> Teams_ListLinked_ByName_WithTheDefaultMark_OneStatement.
    [Fact]
    public async Task Teams_ListLinked_ByName_WithTheDefaultMark_OneStatement()
    {
        var structure = await database.CreateStructureAsync(projectName: "Ядро");
        var (organizationId, projectId) = (structure.Organization.Id, structure.Project.Project.Id);
        var alpha = Team.Create(organizationId, Name("альфа"));
        var unlinked = Team.Create(organizationId, Name("Не связана"));
        await AddTeamAsync(alpha);
        await AddTeamAsync(unlinked);
        await LinkAsync(organizationId, projectId, alpha.Id);

        var counter = new CommandCountingInterceptor();
        var none = await ReadAsync<ITeamReadModel, IReadOnlyList<LinkedTeamRow>>(
            counter,
            readModel => readModel.ListLinkedAsync(organizationId, projectId, "nobody", CancellationToken.None));
        await database.ExecuteAsync("UPDATE projects SET default_team_id = @team WHERE id = @project", ("team", structure.SecondTeam.Id), ("project", projectId));
        var named = await ReadAsync<ITeamReadModel, IReadOnlyList<LinkedTeamRow>>(
            null,
            readModel => readModel.ListLinkedAsync(organizationId, projectId, "nobody", CancellationToken.None));

        Assert.Single(counter.Commands);
        Assert.Equal(["альфа", "Платформа", "Ядро"], none.Select(team => team.Name));
        Assert.All(none, team => Assert.Equal(DefaultTeamMark.None, team.Mark));
        Assert.Equal(
            [DefaultTeamMark.None, DefaultTeamMark.Named, DefaultTeamMark.None],
            named.Select(team => team.Mark));
    }

    // mod-017 BR-OT-4, feat-028 BR-OL-7 · a project's only linked active team carries «onlyTeam» when the settings name none.
    [Fact]
    public async Task Teams_TheOnlyLinkedTeam_IsMarkedOnlyTeam_UnlessTheSettingsNameAnother()
    {
        var structure = await database.CreateStructureAsync();
        var projectId = structure.Project.Project.Id;
        await database.ExecuteAsync("UPDATE teams SET status = 'Deleted' WHERE id = @second", ("second", structure.SecondTeam.Id));

        var only = await ReadAsync<ITeamReadModel, LinkedTeamRow?>(
            null,
            readModel => readModel.FindLinkedAsync(structure.Organization.Id, projectId, structure.FirstTeam.Id, CancellationToken.None));

        Assert.Equal(DefaultTeamMark.OnlyTeam, only!.Mark);
        Assert.False(only.IsMember);
    }

    // US-7 · "A deleted team answers not found": it leaves the list and the by-id read; a team that is not linked is not found at
    // the project's address either (BR-OL-11).
    [Fact]
    public async Task Teams_DeletedTeam_AndAnUnlinkedOne_AreLeftOut()
    {
        var structure = await database.CreateStructureAsync();
        var (organizationId, projectId) = (structure.Organization.Id, structure.Project.Project.Id);
        var unlinked = Team.Create(organizationId, Name("Не связана"));
        await AddTeamAsync(unlinked);
        await database.ExecuteAsync("UPDATE teams SET status = 'Deleted' WHERE id = @second", ("second", structure.SecondTeam.Id));

        var teams = await ReadAsync<ITeamReadModel, IReadOnlyList<LinkedTeamRow>>(
            null,
            readModel => readModel.ListLinkedAsync(organizationId, projectId, "nobody", CancellationToken.None));
        var deleted = await ReadAsync<ITeamReadModel, LinkedTeamRow?>(
            null,
            readModel => readModel.FindLinkedAsync(organizationId, projectId, structure.SecondTeam.Id, CancellationToken.None));
        var notLinked = await ReadAsync<ITeamReadModel, LinkedTeamRow?>(
            null,
            readModel => readModel.FindLinkedAsync(organizationId, projectId, unlinked.Id, CancellationToken.None));

        Assert.Equal([structure.FirstTeam.Id], teams.Select(team => team.Id));
        Assert.Null(deleted);
        Assert.Null(notLinked);
    }

    // feat-028 US-1 · the organization's list: active teams by name, each with its ACTIVE projects by name, in TWO statements whatever
    // the number of teams or projects; a deleted team, a deactivated project and another organization's team are left out.
    [Fact]
    public async Task OrganizationTeams_ByName_WithActiveProjects_TwoStatements()
    {
        var structure = await database.CreateStructureAsync();
        var organizationId = structure.Organization.Id;
        var second = await database.AddProjectAsync(organizationId, "Аналитика", "ANL");
        var old = await database.AddProjectAsync(organizationId, "Старый", "OLD");
        await database.ExecuteAsync("UPDATE projects SET status = 'Deactivated' WHERE id = @id", ("id", old.Project.Id));
        await LinkAsync(organizationId, second.Project.Id, structure.SecondTeam.Id);
        await LinkAsync(organizationId, old.Project.Id, structure.SecondTeam.Id);
        await database.CreateStructureAsync();
        await database.AddMemberAsync(organizationId, "u-max");
        await database.AddTeamMemberAsync(organizationId, structure.FirstTeam.Id, "u-max");

        var counter = new CommandCountingInterceptor();
        var teams = await ReadAsync<ITeamReadModel, IReadOnlyList<OrganizationTeamRow>>(
            counter, readModel => readModel.ListOrganizationTeamsAsync(organizationId, "u-max", CancellationToken.None));

        Assert.Equal(2, counter.Commands.Count);
        Assert.Equal(["Платформа", "Пример проекта"], teams.Select(team => team.Name));
        Assert.Equal(["Аналитика", "Пример проекта"], teams[0].Projects.Select(project => project.Name));
        Assert.Equal(["Пример проекта"], teams[1].Projects.Select(project => project.Name));

        // feat-040 api-contract § 4.1: the caller's own team is told in the same statement (the two stay two); nobody else's is.
        Assert.Equal([false, true], teams.Select(team => team.IsMember));
    }

    // feat-028 US-5 · the deletion's links: every project the team is linked to, any status, by name, in one statement.
    [Fact]
    public async Task LinksWithProjects_AnyStatus_ByName_OneStatement()
    {
        var structure = await database.CreateStructureAsync();
        var organizationId = structure.Organization.Id;
        var old = await database.AddProjectAsync(organizationId, "Архив", "ARC");
        await database.ExecuteAsync("UPDATE projects SET status = 'Deactivated' WHERE id = @id", ("id", old.Project.Id));
        await LinkAsync(organizationId, old.Project.Id, structure.FirstTeam.Id);

        var counter = new CommandCountingInterceptor();
        var links = await ReadAsync<ITeamReadModel, IReadOnlyList<TeamLinkRow>>(
            counter, readModel => readModel.ListLinksWithProjectsAsync(organizationId, structure.FirstTeam.Id, CancellationToken.None));

        Assert.Single(counter.Commands);
        Assert.Equal([("Архив", false), ("Пример проекта", true)], links.Select(link => (link.Name, link.IsActive)));
    }

    // feat-028 BR-OL-1 · a team read of another organization finds nothing.
    [Fact]
    public async Task TeamReads_OfAnotherOrganization_FindNothing()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();

        var found = await ReadAsync<ITeamReadModel, TeamRow?>(
            null, readModel => readModel.FindAsync(other.Organization.Id, structure.FirstTeam.Id, CancellationToken.None));
        var projects = await ReadAsync<ITeamReadModel, IReadOnlyList<TeamProjectRow>>(
            null, readModel => readModel.ListProjectsOfTeamAsync(other.Organization.Id, structure.FirstTeam.Id, CancellationToken.None));

        Assert.Null(found);
        Assert.Empty(projects);
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

    private async Task LinkAsync(Guid organizationId, Guid projectId, Guid teamId)
    {
        await using var context = database.NewContext();
        context.ProjectTeams.Add(ProjectTeam.Link(organizationId, projectId, teamId, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
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
