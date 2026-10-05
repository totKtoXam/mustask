using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Mustask.Infrastructure.Tests.Organizations.OrganizationsDatabaseFixture;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// The write side's reads and locks (plan-backend § 5.1, § 8): a lock returns
/// the row only inside the container it names (BR-M1-22), the pre-checks
/// compare normalized names, and the deletion's set-based re-own moves every
/// explicit area at once (BR-M1-18).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class RepositoriesTests(OrganizationsDatabaseFixture database)
{
    // BR-M1-22: a lock by (container, id) finds nothing in another container.
    [Fact]
    public async Task Locks_FindTheRowOnlyInItsOwnContainer()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        var projectId = structure.Project.Project.Id;
        var otherProjectId = other.Project.Project.Id;

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
                await teams.LockForUpdateAsync(projectId, structure.SecondTeam.Team.Id, ct) is not null,
                await teams.LockForNoKeyUpdateAsync(otherProjectId, structure.SecondTeam.Team.Id, ct) is null,
                await teams.LockForShareAsync(otherProjectId, structure.SecondTeam.Team.Id, ct) is null,
                await areas.LockForUpdateAsync(projectId, structure.SecondTeam.Area.Id, ct) is not null,
                await areas.LockForShareAsync(otherProjectId, structure.SecondTeam.Area.Id, ct) is null,
                await areas.FindAsync(otherProjectId, structure.SecondTeam.Area.Id, ct) is null,
                (await areas.LockRootAsync(projectId, forUpdate: true, ct)).Id == structure.Project.Root.Id,
                (await areas.LockDefaultOfAsync(structure.SecondTeam.Team.Id, ct))?.Id == structure.SecondTeam.Area.Id,
                (await teams.GetFirstTeamAsync(projectId, ct)).Id == structure.Project.FirstTeam.Id,
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
                await teams.IsActiveNameTakenAsync(projectId, "ПЛАТФОРМА", structure.SecondTeam.Team.Id, ct),
                await teams.FindNameAsync(structure.SecondTeam.Team.Id, ct),
                await areas.IsSiblingNameTakenAsync(structure.Project.Root.Id, "ПЛАТФОРМА", null, ct),
                await areas.IsSiblingNameTakenAsync(structure.Project.Root.Id, "ПЛАТФОРМА", structure.SecondTeam.Area.Id, ct),
                await areas.HasChildrenAsync(structure.Project.Root.Id, ct),
                await areas.HasChildrenAsync(structure.SecondTeam.Area.Id, ct),
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

    // US-7 · "deleted, areas pass to the first team" (BR-M1-18): every explicit area, its default mark cleared.
    [Fact]
    public async Task ReassignExplicitAreas_MovesEveryExplicitAreaAndClearsTheDefault()
    {
        var structure = await database.CreateStructureAsync();
        var owned = Area.CreateChild(structure.Project.Root, Name("Отчёты"), structure.SecondTeam.Team);
        var inherited = Area.CreateChild(structure.SecondTeam.Area, Name("API"), ownerTeam: null);
        await database.AddAreasAsync(owned, inherited);

        await InUnitOfWorkAsync(async (scope, ct) =>
        {
            await scope.ServiceProvider.GetRequiredService<ITeamRepository>()
                .ReassignExplicitAreasAsync(structure.SecondTeam.Team.Id, structure.Project.FirstTeam.Id, ct);

            return true;
        });

        await using var context = database.NewContext();
        var areas = await context.Areas
            .AsNoTracking()
            .Where(area => area.Id == owned.Id || area.Id == inherited.Id || area.Id == structure.SecondTeam.Area.Id)
            .ToDictionaryAsync(area => area.Id);

        Assert.Equal(structure.Project.FirstTeam.Id, areas[owned.Id].OwnerTeamId);
        Assert.Equal(structure.Project.FirstTeam.Id, areas[structure.SecondTeam.Area.Id].OwnerTeamId);
        Assert.Null(areas[structure.SecondTeam.Area.Id].DefaultOfTeamId);
        Assert.Null(areas[inherited.Id].OwnerTeamId);
    }

    // plan-backend § 8.2: several areas are locked in one fixed order, ascending id.
    [Fact]
    public async Task LockManyForUpdate_ReturnsTheAreasOfTheProjectInAscendingId()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        var ids = new[] { structure.SecondTeam.Area.Id, structure.Project.Root.Id, other.Project.Root.Id };

        var locked = await InUnitOfWorkAsync((scope, ct) =>
            scope.ServiceProvider.GetRequiredService<IAreaRepository>().LockManyForUpdateAsync(structure.Project.Project.Id, ids, ct));

        Assert.Equal(
            new[] { structure.SecondTeam.Area.Id, structure.Project.Root.Id }.Order(),
            locked.Select(area => area.Id));
    }

    // The input of AreaTree: every node of the project, siblings in display order.
    [Fact]
    public async Task GetNodes_ReturnsEveryAreaOfTheProject_SiblingsInDisplayOrder()
    {
        var structure = await database.CreateStructureAsync();
        var alpha = Area.CreateChild(structure.Project.Root, Name("альфа"), ownerTeam: null);
        await database.AddAreasAsync(alpha);

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
