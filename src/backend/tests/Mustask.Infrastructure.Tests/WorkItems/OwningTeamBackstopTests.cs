using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.Tests.Organizations;
using Mustask.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Infrastructure.Tests.WorkItems;

/// <summary>
/// mod-017 plan-backend D-15 and § 7.4: the foreign keys of the owning team and of the project's default team are the backstops
/// of the team locks. <see cref="OwningTeamSchemaTests"/> shows the database refusing the write; these cases drive the same
/// violation through the repositories' <c>SaveAsync</c> and assert what the caller meets: a 404 <c>organizations.team.not_found</c>
/// (never the raw <see cref="DbUpdateException"/> that would answer 500), and nothing stored. A race cannot reach the keys while
/// the locks hold, so each case writes the violation itself: a team that is not there, and a team of another project.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class OwningTeamBackstopTests(OrganizationsDatabaseFixture database)
{
    private static readonly DateTimeOffset Created = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    // US-1 · "a team the project does not have": an insert naming such a team is refused by the owner key, and the repository
    // answers the team's absence.
    [Theory]
    [InlineData(Foreign.UnknownTeam)]
    [InlineData(Foreign.TeamOfAnotherProject)]
    public async Task WorkItemRepository_SaveAsync_NewItemOwnedByATeamThatIsNotTheProjects_IsTeamNotFound_StoresNothing(Foreign kind)
    {
        var world = await SeedAsync();
        var owner = world.Team(kind);

        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWorkItemRepository>();

        repository.Add(WorkItem.Create(
            world.ProjectId, 1, world.Type, world.RootAreaId, "Элемент", Guid.CreateVersion7(), Created, "backstop-test", owner));

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => repository.SaveAsync(CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Equal(404, refusal.StatusCode);
        Assert.IsType<DbUpdateException>(refusal.InnerException);
        Assert.Equal(["0"], await database.QueryAsync("SELECT count(*)::text FROM work_items WHERE project_id = @project", ("project", world.ProjectId)));
    }

    // US-1 · "a team gone while the form was open": a save of a stored item to such a team is refused the same way, and the item
    // keeps its team and its «Изменён».
    [Theory]
    [InlineData(Foreign.UnknownTeam)]
    [InlineData(Foreign.TeamOfAnotherProject)]
    public async Task WorkItemRepository_SaveAsync_ItemMovedToATeamThatIsNotTheProjects_IsTeamNotFound_KeepsTheStoredTeam(Foreign kind)
    {
        var world = await SeedAsync();
        await AddItemAsync(world, owner: world.OwnTeam);
        var target = world.Team(kind);

        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWorkItemRepository>();

        var item = await repository.LockAsync(world.ProjectId, 1, CancellationToken.None);
        Assert.NotNull(item);
        Assert.True(item.Save(new WorkItemEdit(Team: new TeamEdit(target)), world.Type, Created.AddHours(1)));

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => repository.SaveAsync(CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Equal(
            [$"{world.OwnTeam}|true"],
            await database.QueryAsync(
                "SELECT concat_ws('|', owning_team_id::text, (changed_at = @created)::text) FROM work_items WHERE project_id = @project",
                ("project", world.ProjectId),
                ("created", Created)));
    }

    // US-3 · "a team the project does not have", for the default naming: the default key refuses a team that is not there or is
    // another project's, and the repository answers the team's absence (plan-backend D-15), not a 500.
    [Theory]
    [InlineData(Foreign.UnknownTeam)]
    [InlineData(Foreign.TeamOfAnotherProject)]
    public async Task ProjectRepository_SaveAsync_DefaultTeamThatIsNotTheProjects_IsTeamNotFound_SettingUnchanged(Foreign kind)
    {
        var world = await SeedAsync();
        var named = world.Team(kind);

        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();

        var project = await repository.LockAsync(world.OrganizationId, world.ProjectId, CancellationToken.None);
        Assert.NotNull(project);
        Assert.True(project.NameDefaultTeam(named));

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => repository.SaveAsync(CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Equal(404, refusal.StatusCode);
        Assert.IsType<DbUpdateException>(refusal.InnerException);
        Assert.Equal(["NULL"], await database.QueryAsync("SELECT coalesce(default_team_id::text, 'NULL') FROM projects WHERE id = @project", ("project", world.ProjectId)));
    }

    // The mapping refuses only the violation: naming a team of the project saves.
    [Fact]
    public async Task ProjectRepository_SaveAsync_DefaultTeamOfTheProject_IsSaved()
    {
        var world = await SeedAsync();

        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();

        var project = await repository.LockAsync(world.OrganizationId, world.ProjectId, CancellationToken.None);
        Assert.NotNull(project);
        Assert.True(project.NameDefaultTeam(world.OwnTeam));
        await repository.SaveAsync(CancellationToken.None);

        Assert.Equal([world.OwnTeam.ToString()], await database.QueryAsync("SELECT default_team_id::text FROM projects WHERE id = @project", ("project", world.ProjectId)));
    }

    // --- Arrangement -------------------------------------------------------------------------------------------------

    /// <summary>The team a write names though it is not the project's.</summary>
    public enum Foreign
    {
        UnknownTeam,
        TeamOfAnotherProject,
    }

    private sealed record World(
        Guid OrganizationId, Guid ProjectId, Guid RootAreaId, Guid OwnTeam, Guid OtherProjectTeam, WorkItemTypeStates Type)
    {
        public Guid Team(Foreign kind) => kind == Foreign.UnknownTeam ? Guid.CreateVersion7() : OtherProjectTeam;
    }

    /// <summary>A project with its teams and a story type, and another project of the organization with a team of its own.</summary>
    private async Task<World> SeedAsync()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.AddProjectAsync(structure.Organization.Id, "Другой проект", "OTH");

        await using var context = database.NewContext();
        var type = await context.WorkItemTypes.Include(candidate => candidate.States)
            .SingleAsync(candidate => candidate.ProcessId == structure.Organization.ProcessId && candidate.Key == "US");
        var states = new WorkItemTypeStates(
            type.Id, type.Key, type.Appearance, type.States.Select(state => new WorkItemTypeState(state.Id, state.Position, state.Category)).ToList());

        return new World(
            structure.Organization.Id, structure.Project.Project.Id, structure.Project.Root.Id, structure.SecondTeam.Id, other.Team.Id, states);
    }

    private async Task AddItemAsync(World world, Guid? owner)
    {
        await using var context = database.NewContext();

        context.WorkItems.Add(WorkItem.Create(
            world.ProjectId, 1, world.Type, world.RootAreaId, "Элемент", Guid.CreateVersion7(), Created, "backstop-test", owner));
        await context.SaveChangesAsync();
    }

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddDbContext<MustaskDbContext>(options => options.UseNpgsql(database.ConnectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddApplication();
        services.AddOrganizationsInfrastructure();
        services.AddWorkItemsInfrastructure();

        return services.BuildServiceProvider();
    }
}
