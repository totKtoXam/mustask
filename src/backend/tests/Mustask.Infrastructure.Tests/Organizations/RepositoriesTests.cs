using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Mustask.Infrastructure.Tests.Organizations.OrganizationsDatabaseFixture;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// The write side's reads and locks (plan-backend § 5.1, § 7): a lock returns the row only inside the container it names
/// (BR-M1-22), the pre-checks compare normalized names, and the deletion's lock takes every active team of the project in id
/// order (mod-017 D-3, D-4).
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
        var projectId = structure.Project.Project.Id;
        var otherProjectId = other.Project.Project.Id;
        var reports = Area.CreateChild(structure.Project.Root, Name("Отчёты"));
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
                await teams.LockForNoKeyUpdateAsync(projectId, structure.SecondTeam.Id, ct) is not null,
                await teams.LockForNoKeyUpdateAsync(otherProjectId, structure.SecondTeam.Id, ct) is null,
                await teams.LockForShareAsync(otherProjectId, structure.SecondTeam.Id, ct) is null,
                await areas.LockForUpdateAsync(projectId, reports.Id, ct) is not null,
                await areas.LockForShareAsync(otherProjectId, reports.Id, ct) is null,
                await areas.FindAsync(otherProjectId, reports.Id, ct) is null,
                (await areas.LockRootAsync(projectId, forUpdate: true, ct)).Id == structure.Project.Root.Id,
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
        var projectId = structure.Project.Project.Id;
        var platformArea = Area.CreateChild(structure.Project.Root, Name("Платформа"));
        await database.AddAreasAsync(platformArea);

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
                await teams.IsActiveNameTakenAsync(projectId, "ПЛАТФОРМА", null, ct),
                await teams.IsActiveNameTakenAsync(projectId, "ПЛАТФОРМА", structure.SecondTeam.Id, ct),
                await teams.FindNameAsync(structure.SecondTeam.Id, ct),
                await areas.IsSiblingNameTakenAsync(structure.Project.Root.Id, "ПЛАТФОРМА", null, ct),
                await areas.IsSiblingNameTakenAsync(structure.Project.Root.Id, "ПЛАТФОРМА", platformArea.Id, ct),
                await areas.HasChildrenAsync(structure.Project.Root.Id, ct),
                await areas.HasChildrenAsync(platformArea.Id, ct),
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

    // mod-017 BR-OT-12, D-3, D-4 · the deletion's lock: every ACTIVE team of the project, and only of that project, tracked, in
    // ascending id — one fixed order for every caller.
    [Fact]
    public async Task LockProjectTeams_ReturnsTheActiveTeamsOfTheProjectOnly_InAscendingId()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        var projectId = structure.Project.Project.Id;
        var third = Team.Create(projectId, Name("Аналитика"));
        var deleted = Team.Create(projectId, Name("Ядро"));
        await using (var context = database.NewContext())
        {
            context.Teams.AddRange(third, deleted);
            await context.SaveChangesAsync();
        }

        await database.ExecuteAsync("UPDATE teams SET status = 'Deleted' WHERE id = @id", ("id", deleted.Id));

        var locked = await InUnitOfWorkAsync((scope, ct) =>
            scope.ServiceProvider.GetRequiredService<ITeamRepository>().LockProjectTeamsAsync(projectId, ct));

        Assert.Equal(
            new[] { structure.Project.Team.Id, structure.SecondTeam.Id, third.Id }.Order(),
            locked.Select(team => team.Id));
        Assert.DoesNotContain(locked, team => team.Id == other.Project.Team.Id);
    }

    // mod-017 BR-OT-4 · the mark the rename answers: «named» for the team the settings name, «onlyTeam» for the only active team,
    // else none — the same definition as the list's.
    [Fact]
    public async Task GetMark_NamedOnlyTeamOrNone()
    {
        var structure = await database.CreateStructureAsync();
        var projectId = structure.Project.Project.Id;

        async Task<DefaultTeamMark> MarkAsync(Guid teamId) =>
            await InUnitOfWorkAsync(async (scope, ct) =>
            {
                var teams = scope.ServiceProvider.GetRequiredService<ITeamRepository>();
                var team = (await teams.LockForShareAsync(projectId, teamId, ct))!;

                return await teams.GetMarkAsync(team, ct);
            });

        var severalNoneNamed = await MarkAsync(structure.SecondTeam.Id);
        await database.ExecuteAsync("UPDATE projects SET default_team_id = @team WHERE id = @project", ("team", structure.SecondTeam.Id), ("project", projectId));
        var named = await MarkAsync(structure.SecondTeam.Id);
        var notNamed = await MarkAsync(structure.Project.Team.Id);
        await database.ExecuteAsync("UPDATE projects SET default_team_id = NULL WHERE id = @project", ("project", projectId));
        await database.ExecuteAsync("UPDATE teams SET status = 'Deleted' WHERE id = @id", ("id", structure.SecondTeam.Id));
        var only = await MarkAsync(structure.Project.Team.Id);

        Assert.Equal(DefaultTeamMark.None, severalNoneNamed);
        Assert.Equal(DefaultTeamMark.Named, named);
        Assert.Equal(DefaultTeamMark.None, notNamed);
        Assert.Equal(DefaultTeamMark.OnlyTeam, only);
    }

    // mod-017 BR-OT-12 · the deletion's step: one statement clears the setting if it names the team, and only then.
    [Fact]
    public async Task ClearDefaultTeam_ClearsOnlyASettingThatNamesThatTeam()
    {
        var structure = await database.CreateStructureAsync();
        var projectId = structure.Project.Project.Id;
        await database.ExecuteAsync("UPDATE projects SET default_team_id = @team WHERE id = @project", ("team", structure.SecondTeam.Id), ("project", projectId));

        await InUnitOfWorkAsync(async (scope, ct) =>
        {
            await scope.ServiceProvider.GetRequiredService<IProjectRepository>().ClearDefaultTeamAsync(projectId, structure.Project.Team.Id, ct);

            return true;
        });
        var kept = await database.QueryAsync("SELECT default_team_id::text FROM projects WHERE id = @project", ("project", projectId));

        await InUnitOfWorkAsync(async (scope, ct) =>
        {
            await scope.ServiceProvider.GetRequiredService<IProjectRepository>().ClearDefaultTeamAsync(projectId, structure.SecondTeam.Id, ct);

            return true;
        });
        var cleared = await database.QueryAsync("SELECT default_team_id::text FROM projects WHERE id = @project", ("project", projectId));

        Assert.Equal([structure.SecondTeam.Id.ToString()], kept);
        Assert.Equal(["NULL"], cleared);
    }

    // The input of AreaTree: every node of the project, siblings in display order.
    [Fact]
    public async Task GetNodes_ReturnsEveryAreaOfTheProject_SiblingsInDisplayOrder()
    {
        var structure = await database.CreateStructureAsync();
        var alpha = Area.CreateChild(structure.Project.Root, Name("альфа"));
        var platform = Area.CreateChild(structure.Project.Root, Name("Платформа"));
        await database.AddAreasAsync(alpha, platform);

        var nodes = await InUnitOfWorkAsync((scope, ct) =>
            scope.ServiceProvider.GetRequiredService<IAreaRepository>().GetNodesAsync(structure.Project.Project.Id, ct));

        var tree = new AreaTree(nodes);
        Assert.Equal(["альфа", "Платформа"], tree.ChildrenOf(structure.Project.Root.Id).Select(node => node.Name));
    }

    private async Task<T> InUnitOfWorkAsync<T>(Func<IServiceScope, CancellationToken, Task<T>> work)
    {
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        return await unitOfWork.ExecuteInTransactionAsync(ct => work(scope, ct), CancellationToken.None);
    }
}
