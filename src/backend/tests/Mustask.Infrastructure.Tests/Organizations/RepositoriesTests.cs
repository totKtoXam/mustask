using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Mustask.Infrastructure.Tests.Organizations.OrganizationsDatabaseFixture;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// The write side's reads and locks (plan-backend § 5.1, § 7): a lock returns the row only inside the container it names
/// (BR-M1-22), the pre-checks compare normalized names, and the links' locks take a pair's links in team-id order and a team's
/// links in project-id order (feat-028 plan-backend D-3, D-4, § 7.4).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class RepositoriesTests(OrganizationsDatabaseFixture database)
{
    // BR-M1-22: a lock by (container, id) finds nothing in another container. Rewritten for mod-017 (D-16 keeps the name): the
    // team has no area and no first-team read, so the team locks are the team's own and the area locks go on a plain area.
    [Fact]
    public async Task Locks_FindTheRowOnlyInItsOwnContainer()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        var projectId = structure.Project.Id;
        var reports = NewArea(structure.Organization.Id, null, "Отчёты");
        await database.AddAreasAsync(reports);

        var result = await InUnitOfWorkAsync(async (scope, ct) =>
        {
            var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var teams = scope.ServiceProvider.GetRequiredService<ITeamRepository>();
            var areas = scope.ServiceProvider.GetRequiredService<IAreaRepository>();
            var organizations = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();

            return new[]
            {
                await organizations.LockAsync(structure.Organization.Id, ct) is not null,
                await organizations.LockAsync(Guid.CreateVersion7(), ct) is null,
                await projects.LockAsync(structure.Organization.Id, projectId, ct) is not null,
                await projects.LockAsync(other.Organization.Id, projectId, ct) is null,
                await teams.LockForNoKeyUpdateAsync(structure.Organization.Id, structure.SecondTeam.Id, ct) is not null,
                await teams.LockForNoKeyUpdateAsync(other.Organization.Id, structure.SecondTeam.Id, ct) is null,
                await teams.LockForShareAsync(other.Organization.Id, structure.SecondTeam.Id, ct) is null,
                await areas.LockForUpdateAsync(structure.Organization.Id, reports.Id, ct) is not null,
                await areas.LockForShareAsync(other.Organization.Id, reports.Id, ct) is null,
                await areas.FindAsync(other.Organization.Id, reports.Id, ct) is null,
                await areas.FindAsync(structure.Organization.Id, reports.Id, ct) is not null,
            };
        });

        Assert.All(result, value => Assert.True(value));
    }

    // BR-M1-1, BR-M1-12: the pre-checks compare normalized names and skip the object itself.
    [Fact]
    public async Task PreChecks_CompareNormalizedNames_AndSkipTheObjectItself()
    {
        var structure = await database.CreateStructureAsync();
        var organizationId = structure.Organization.Id;
        var projectId = structure.Project.Id;
        var topArea = NewArea(organizationId, null, "Корень");
        var platformArea = NewArea(organizationId, topArea.Id, "Платформа");
        await database.AddAreasAsync(topArea, platformArea);

        var result = await InUnitOfWorkAsync(async (scope, ct) =>
        {
            var organizations = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();
            var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var teams = scope.ServiceProvider.GetRequiredService<ITeamRepository>();
            var areas = scope.ServiceProvider.GetRequiredService<IAreaRepository>();
            var normalized = EntityName.Normalize(structure.Organization.Name);

            return new object?[]
            {
                await organizations.FindNameHolderAsync(normalized, null, ct),
                await organizations.FindNameHolderAsync(normalized, organizationId, ct),
                await projects.FindNameHolderAsync(organizationId, "ПРИМЕР ПРОЕКТА", null, ct),
                await projects.FindNameHolderAsync(organizationId, "ПРИМЕР ПРОЕКТА", projectId, ct),
                await projects.IsKeyTakenAsync(organizationId, "PRM", ct),
                await projects.IsKeyTakenAsync(organizationId, "prm", ct),
                await projects.IsReachableAsync(organizationId, projectId, ct),
                await teams.IsActiveNameTakenAsync(organizationId, "ПЛАТФОРМА", null, ct),
                await teams.IsActiveNameTakenAsync(organizationId, "ПЛАТФОРМА", structure.SecondTeam.Id, ct),
                await teams.FindNameAsync(structure.SecondTeam.Id, ct),
                await areas.IsSiblingNameTakenAsync(organizationId, topArea.Id, "ПЛАТФОРМА", null, ct),
                await areas.IsSiblingNameTakenAsync(organizationId, topArea.Id, "ПЛАТФОРМА", platformArea.Id, ct),
                await areas.HasChildrenAsync(organizationId, topArea.Id, ct),
                await areas.HasChildrenAsync(organizationId, platformArea.Id, ct),
            };
        });

        Assert.Equal(
            new object?[]
            {
                structure.Organization.Name, null,
                ProjectStatus.Active, null,
                true, false,
                true,
                true, false,
                "Платформа",
                true, false,
                true, false,
            },
            result);
    }

    // feat-028 plan-backend D-3, § 7.4 · the unlink's lock: the links of one project to the named teams, FOR NO KEY UPDATE, in ONE
    // statement ordered by team id — the same SQL whichever team an unlink names first. Replaces
    // LockProjectTeams_ReturnsTheActiveTeamsOfTheProjectOnly_InAscendingId (retired with the last-team guard, BR-OL-10).
    [Fact]
    public async Task LockLinks_OfAPair_InAscendingTeamId()
    {
        var structure = await database.CreateStructureAsync();
        var (organizationId, projectId) = (structure.Organization.Id, structure.Project.Id);
        var log = new SqlLogInterceptor();

        await InUnitOfWorkAsync(async (scope, ct) =>
        {
            var links = scope.ServiceProvider.GetRequiredService<IProjectTeamRepository>();
            await links.LockLinksAsync(organizationId, projectId, [structure.SecondTeam.Id, structure.FirstTeam.Id], ct);
            await links.LockLinksAsync(organizationId, projectId, [structure.FirstTeam.Id, structure.SecondTeam.Id], ct);

            return true;
        }, log);

        var statements = log.Commands.Where(sql => sql.Contains("FROM project_teams", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, statements.Count);
        Assert.All(statements, sql => Assert.Contains("ORDER BY team_id", sql, StringComparison.Ordinal));
        Assert.All(statements, sql => Assert.Contains("FOR NO KEY UPDATE", sql, StringComparison.Ordinal));
    }

    // feat-028 review-security S-1 · a link lock returns the rows it locked, and the caller judges "linked" by them alone: the pair's
    // lock answers the teams whose links it holds (an unlinked team is not among them), and the binder's lock is ONE statement per link
    // — no second read of the same rows, which under READ COMMITTED could see a link committed after the lock and never held.
    [Fact]
    public async Task LinkLocks_AnswerWhatTheirOwnStatementLocked_InOneStatementEach()
    {
        var structure = await database.CreateStructureAsync();
        var (organizationId, projectId) = (structure.Organization.Id, structure.Project.Id);
        var unlinked = await database.AddTeamAsync(organizationId, UniqueName("Не связана"));
        var log = new SqlLogInterceptor();

        var answers = await InUnitOfWorkAsync(async (scope, ct) =>
        {
            var links = scope.ServiceProvider.GetRequiredService<IProjectTeamRepository>();
            var pair = await links.LockLinksAsync(organizationId, projectId, [unlinked.Id, structure.SecondTeam.Id], ct);
            var held = await links.LockLinkForShareAsync(organizationId, projectId, structure.FirstTeam.Id, ct);
            var notHeld = await links.LockLinkForShareAsync(organizationId, projectId, unlinked.Id, ct);

            return (pair, held, notHeld);
        }, log);

        Assert.Equal([structure.SecondTeam.Id], answers.pair);
        Assert.True(answers.held);
        Assert.False(answers.notHeld);
        var statements = log.Commands.Where(sql => sql.Contains("FROM project_teams", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, statements.Count);
        Assert.Equal(1, statements.Count(sql => sql.Contains("FOR NO KEY UPDATE", StringComparison.Ordinal)));
        Assert.Equal(2, statements.Count(sql => sql.Contains("FOR SHARE", StringComparison.Ordinal)));
    }

    // feat-028 plan-backend § 7.3 C-12 · the deletion's lock: ALL of one team's links, by project id, and the project ids they name;
    // another team's links are not read.
    [Fact]
    public async Task LockTeamLinks_ReturnsTheTeamsLinksOnly_InAscendingProjectId()
    {
        var structure = await database.CreateStructureAsync();
        var organizationId = structure.Organization.Id;
        var second = await database.AddProjectAsync(organizationId, "Аналитика", "ANL");
        await using (var context = database.NewContext())
        {
            context.ProjectTeams.Add(ProjectTeam.Link(organizationId, second.Id, structure.FirstTeam.Id, Creator, Now));
            await context.SaveChangesAsync();
        }

        var projects = await InUnitOfWorkAsync((scope, ct) =>
            scope.ServiceProvider.GetRequiredService<IProjectTeamRepository>().LockTeamLinksAsync(organizationId, structure.FirstTeam.Id, ct));
        var other = await InUnitOfWorkAsync((scope, ct) =>
            scope.ServiceProvider.GetRequiredService<IProjectTeamRepository>().LockTeamLinksAsync(organizationId, structure.SecondTeam.Id, ct));

        Assert.Equal(new[] { structure.Project.Id, second.Id }.Order(), projects);
        Assert.Equal([structure.Project.Id], other);
    }

    // feat-028 BR-OL-4 · the link statement: a pair already linked keeps its row, who linked it and when; a new pair is added with the
    // caller and the time — one statement for every pair.
    [Fact]
    public async Task Link_AlreadyLinkedPair_KeepsItsRow_NewPairsAreAdded()
    {
        var structure = await database.CreateStructureAsync();
        var organizationId = structure.Organization.Id;
        var second = await database.AddProjectAsync(organizationId, "Аналитика", "ANL");
        var later = Now.AddDays(3);

        var made = await InUnitOfWorkAsync((scope, ct) =>
            scope.ServiceProvider.GetRequiredService<IProjectTeamRepository>().LinkAsync(
                organizationId,
                [(structure.Project.Id, structure.FirstTeam.Id), (second.Id, structure.FirstTeam.Id)],
                "someone-else",
                later,
                ct));
        var rows = await database.QueryAsync(
            "SELECT linked_by || '|' || linked_at::text FROM project_teams WHERE team_id = @team ORDER BY project_id",
            ("team", structure.FirstTeam.Id));

        Assert.Equal(1, made);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.StartsWith(Creator + "|", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.StartsWith("someone-else|", StringComparison.Ordinal));
    }

    // feat-028 BR-OL-1 · every link statement carries the organization: a foreign organization finds, links and removes nothing.
    [Fact]
    public async Task LinkStatements_OfAnotherOrganization_ChangeNothing()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();

        var found = await InUnitOfWorkAsync(async (scope, ct) =>
        {
            var links = scope.ServiceProvider.GetRequiredService<IProjectTeamRepository>();
            await links.RemoveLinkAsync(other.Organization.Id, structure.Project.Id, structure.FirstTeam.Id, ct);
            await links.RemoveTeamLinksAsync(other.Organization.Id, structure.SecondTeam.Id, ct);

            return (
                await links.FindLinkedAsync(other.Organization.Id, structure.Project.Id, [structure.FirstTeam.Id], ct),
                await links.LockTeamLinksAsync(other.Organization.Id, structure.FirstTeam.Id, ct),
                await links.LockLinkForShareAsync(other.Organization.Id, structure.Project.Id, structure.FirstTeam.Id, ct));
        });
        var remaining = await database.QueryAsync(
            "SELECT count(*)::text FROM project_teams WHERE project_id = @project", ("project", structure.Project.Id));

        Assert.Empty(found.Item1);
        Assert.Empty(found.Item2);
        Assert.False(found.Item3);
        Assert.Equal(["2"], remaining);
    }

    // feat-028 BR-OL-3 · the unique name index is per organization: the same name in another organization is free.
    [Fact]
    public async Task ClearDefaultTeam_ByOrganization_ClearsEverySettingThatNamedTheTeam()
    {
        var structure = await database.CreateStructureAsync();
        var organizationId = structure.Organization.Id;
        var second = await database.AddProjectAsync(organizationId, "Аналитика", "ANL");
        await using (var context = database.NewContext())
        {
            context.ProjectTeams.Add(ProjectTeam.Link(organizationId, second.Id, structure.FirstTeam.Id, Creator, Now));
            await context.SaveChangesAsync();
        }

        await database.ExecuteAsync(
            "UPDATE projects SET default_team_id = @team WHERE id IN (@a, @b)",
            ("team", structure.FirstTeam.Id), ("a", structure.Project.Id), ("b", second.Id));

        await InUnitOfWorkAsync(async (scope, ct) =>
        {
            await scope.ServiceProvider.GetRequiredService<IProjectRepository>().ClearDefaultTeamAsync(organizationId, structure.FirstTeam.Id, ct);

            return true;
        });
        var settings = await database.QueryAsync(
            "SELECT count(*)::text FROM projects WHERE organization_id = @org AND default_team_id IS NOT NULL", ("org", organizationId));

        Assert.Equal(["0"], settings);
    }

    // mod-017 BR-OT-12 · the unlink's step: one statement clears the setting of ONE project if it names the team, and only then.
    [Fact]
    public async Task ClearDefaultTeam_ClearsOnlyASettingThatNamesThatTeam()
    {
        var structure = await database.CreateStructureAsync();
        var (organizationId, projectId) = (structure.Organization.Id, structure.Project.Id);
        await database.ExecuteAsync("UPDATE projects SET default_team_id = @team WHERE id = @project", ("team", structure.SecondTeam.Id), ("project", projectId));

        await InUnitOfWorkAsync(async (scope, ct) =>
        {
            await scope.ServiceProvider.GetRequiredService<IProjectRepository>().ClearDefaultTeamAsync(organizationId, projectId, structure.FirstTeam.Id, ct);

            return true;
        });
        var kept = await database.QueryAsync("SELECT default_team_id::text FROM projects WHERE id = @project", ("project", projectId));

        await InUnitOfWorkAsync(async (scope, ct) =>
        {
            await scope.ServiceProvider.GetRequiredService<IProjectRepository>().ClearDefaultTeamAsync(organizationId, projectId, structure.SecondTeam.Id, ct);

            return true;
        });
        var cleared = await database.QueryAsync("SELECT default_team_id::text FROM projects WHERE id = @project", ("project", projectId));

        Assert.Equal([structure.SecondTeam.Id.ToString()], kept);
        Assert.Equal(["NULL"], cleared);
    }

    // The input of AreaTree: every node of the organization.
    [Fact]
    public async Task GetNodes_ReturnsEveryAreaOfTheOrganization()
    {
        var structure = await database.CreateStructureAsync();
        var top = NewArea(structure.Organization.Id, null, "Корень");
        var alpha = NewArea(structure.Organization.Id, top.Id, "альфа");
        var platform = NewArea(structure.Organization.Id, top.Id, "Платформа");
        await database.AddAreasAsync(top, alpha, platform);

        var nodes = await InUnitOfWorkAsync((scope, ct) =>
            scope.ServiceProvider.GetRequiredService<IAreaRepository>().GetNodesAsync(structure.Organization.Id, ct));

        var tree = new AreaTree(nodes);
        Assert.Equal(["альфа", "Платформа"], tree.ChildrenOf(top.Id).Select(node => node.Name).Order(StringComparer.Ordinal));
    }

    private async Task<T> InUnitOfWorkAsync<T>(Func<IServiceScope, CancellationToken, Task<T>> work, params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors)
    {
        await using var provider = database.BuildServices(interceptors);
        await using var scope = provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        return await unitOfWork.ExecuteInTransactionAsync(ct => work(scope, ct), CancellationToken.None);
    }
}
