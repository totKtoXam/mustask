using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Features.Boards.GetBacklog;
using Mustask.Application.Features.Boards.GetBoard;
using Mustask.Application.Features.Organizations.DeleteTeam;
using Mustask.Domain.Identity;
using Mustask.Infrastructure.Boards;
using Mustask.Infrastructure.Process;
using Mustask.Infrastructure.Tests.Organizations;
using Mustask.Infrastructure.UserDirectory;
using Mustask.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Infrastructure.Tests.Boards;

/// <summary>
/// feat-040 BR-TS-7 and tech-011 m2 (plan-backend PD-10, § 7): a read that meets the team's deletion is never a server error and
/// never an empty view — it answers «Команда не найдена.» or the view as it stood. The handlers read the team, then the rows, then
/// the team again; here the real read model is wrapped so that the team is deleted (by the real <see cref="DeleteTeamHandler"/>, in
/// a scope of its own, committed) <b>between</b> the team check and the rows' read, and <b>after</b> the rows' read: both end in
/// the not-found, for the backlog and for the board. (The two plain orders — a read before and a read after a committed deletion —
/// are the endpoint tests'.)
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class TeamViewDeletionRaceTests(OrganizationsDatabaseFixture database)
{
    [Theory]
    [InlineData("backlog", "before the rows are read")]
    [InlineData("backlog", "after the rows are read")]
    [InlineData("board", "before the rows are read")]
    [InlineData("board", "after the rows are read")]
    public async Task Read_WhenTheTeamIsDeletedMeanwhile_AnswersTeamNotFound_NeverAnEmptyViewNorAServerError(string surface, string when)
    {
        var world = await TeamViewWorld.SeedAsync(database);
        await using var provider = BuildServices(deleteBeforeTheRead: when.StartsWith("before", StringComparison.Ordinal), world);
        await using var scope = provider.CreateAsyncScope();

        var read = surface == "backlog"
            ? (Func<Task>)(() => scope.ServiceProvider.GetRequiredService<GetBacklogHandler>()
                .Handle(new GetBacklogQuery(world.OrganizationId.ToString(), world.Team.ToString(), "stories"), CancellationToken.None))
            : () => scope.ServiceProvider.GetRequiredService<GetBoardHandler>()
                .Handle(new GetBoardQuery(world.OrganizationId.ToString(), world.Team.ToString(), "stories"), CancellationToken.None);

        var refusal = await Assert.ThrowsAsync<NotFoundException>(read);

        Assert.Equal("organizations.team.not_found", refusal.Code);
        Assert.Equal(["Deleted"], await database.QueryAsync("SELECT status FROM teams WHERE id = @team", ("team", world.Team)));
    }

    // Without the deletion the same handlers answer the view: the decorator changes nothing else.
    [Fact]
    public async Task Read_WithNoDeletion_AnswersTheView()
    {
        var world = await TeamViewWorld.SeedAsync(database);
        await using var provider = BuildServices(deleteBeforeTheRead: null, world);
        await using var scope = provider.CreateAsyncScope();

        var backlog = await scope.ServiceProvider.GetRequiredService<GetBacklogHandler>()
            .Handle(new GetBacklogQuery(world.OrganizationId.ToString(), world.Team.ToString(), "stories"), CancellationToken.None);

        Assert.Equal(world.Members.ToHashSet(), backlog.Items.Select(row => row.Id).ToHashSet());
        Assert.Equal(2, backlog.Projects.Count);
    }

    private ServiceProvider BuildServices(bool? deleteBeforeTheRead, TeamViewWorld world)
    {
        var services = new ServiceCollection();

        services.AddDbContext<MustaskDbContext>(options => options.UseNpgsql(database.ConnectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserDirectory, UserDirectoryReadModel>();
        services.AddScoped<IProcessDefinitionReadModel, ProcessDefinitionReadModel>();
        services.AddSingleton<ICurrentUser>(new FixedUser());
        services.AddScoped<IOrganizationAccess, AdministratorAccess>();
        services.AddApplication();
        services.AddOrganizationsInfrastructure();
        services.AddWorkItemsInfrastructure();
        services.AddBoardsInfrastructure();

        if (deleteBeforeTheRead is { } before)
        {
            // The real read model, wrapped: the deletion runs in a provider of its own, so it commits whatever the read's context holds.
            services.AddScoped<IBacklogReadModel>(provider => new DeletingReadModel(
                ActivatorUtilities.CreateInstance<BacklogReadModel>(provider),
                before,
                () => DeleteTheTeamAsync(world)));
        }

        return services.BuildServiceProvider();
    }

    private async Task DeleteTheTeamAsync(TeamViewWorld world)
    {
        var services = new ServiceCollection();

        services.AddDbContext<MustaskDbContext>(options => options.UseNpgsql(database.ConnectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<ICurrentUser>(new FixedUser());
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IOrganizationAccess, AdministratorAccess>();
        services.AddApplication();
        services.AddOrganizationsInfrastructure();
        services.AddScoped<IUserDirectory, UserDirectoryReadModel>();
        services.AddScoped<IProcessDefinitionReadModel, ProcessDefinitionReadModel>();
        services.AddWorkItemsInfrastructure();
        services.AddBoardsInfrastructure();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DeleteTeamHandler>()
            .Handle(new DeleteTeamCommand(world.OrganizationId.ToString(), world.Team.ToString()), CancellationToken.None);
    }

    private sealed class FixedUser : ICurrentUser
    {
        public string DisplayName => "Race User";

        public string SubjectId => "race-user-1";
    }

    private sealed class AdministratorAccess : IOrganizationAccess
    {
        public OrganizationRole Role => OrganizationRole.Admin;

        public bool Holds(string code) => OrganizationRoleBundles.Holds(Role, code);
    }

    /// <summary>
    /// <see cref="IBacklogReadModel"/> that runs <paramref name="deleteTheTeam"/> either right before the team's rows are read or
    /// right after; every other read is the inner one.
    /// </summary>
    private sealed class DeletingReadModel(IBacklogReadModel inner, bool before, Func<Task> deleteTheTeam) : IBacklogReadModel
    {
        public async Task<IReadOnlyList<BacklogItemRow>> ReadTeamAsync(TeamBacklogScope scope, CallerAccess caller, CancellationToken cancellationToken)
        {
            if (before)
            {
                await deleteTheTeam();
            }

            var rows = await inner.ReadTeamAsync(scope, caller, cancellationToken);

            if (!before)
            {
                await deleteTheTeam();
            }

            return rows;
        }

        public async Task<IReadOnlyList<BacklogItemRow>> ReadTeamTreeAsync(TeamBacklogTreeScope scope, CallerAccess caller, CancellationToken cancellationToken)
        {
            if (before)
            {
                await deleteTheTeam();
            }

            var rows = await inner.ReadTeamTreeAsync(scope, caller, cancellationToken);

            if (!before)
            {
                await deleteTheTeam();
            }

            return rows;
        }

        public Task<IReadOnlyList<BacklogItemRow>> ReadProjectAsync(ProjectBacklogScope scope, CancellationToken cancellationToken) =>
            inner.ReadProjectAsync(scope, cancellationToken);

        public Task<IReadOnlyList<BacklogItemRow>> ReadProjectTreeAsync(ProjectBacklogTreeScope scope, CancellationToken cancellationToken) =>
            inner.ReadProjectTreeAsync(scope, cancellationToken);

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<ChecklistChildView>>> ReadChecklistsAsync(
            Guid organizationId, IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken) =>
            inner.ReadChecklistsAsync(organizationId, parentIds, cancellationToken);

        public Task<IReadOnlyDictionary<Guid, Mustask.Application.Abstractions.WorkItems.WorkItemRefView>> FindLiveParentsAsync(
            Guid organizationId, IReadOnlyCollection<Guid> workItemIds, CancellationToken cancellationToken) =>
            inner.FindLiveParentsAsync(organizationId, workItemIds, cancellationToken);
    }
}
