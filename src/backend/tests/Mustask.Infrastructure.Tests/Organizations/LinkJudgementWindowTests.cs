using System.Data.Common;
using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations.SetDefaultTeam;
using Mustask.Application.Features.Organizations.UnlinkTeam;
using Mustask.Application.Features.WorkItems.CreateWorkItem;
using Mustask.Application.Features.WorkItems.SaveWorkItem;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure.Process;
using Mustask.Infrastructure.UserDirectory;
using Mustask.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// feat-028 review-code CR-4 and review-security S-1, the window between a link lock and a later read of the same link, reached
/// <b>deterministically</b>. A request's own statements run one after another under READ COMMITTED, each with a snapshot of its own;
/// a commit gate holds a request at its commit and so cannot reach a point in the middle of one, but an interceptor on the context
/// can: it runs a step of another transaction right after the lock statement has returned and before the request's next statement.
/// Each case commits a link in that gap — the case QA's race test (<c>TeamQaEndpointTests</c>) reached by luck and CI caught once —
/// and asserts what the request must do with a link it does not hold: judge it not linked (409 for a team named by an item write or a
/// default naming, a no-op for the unlinked pair, no default for a creation), never bind to it.
/// </summary>
/// <remarks>
/// The destructive form is the unlink: it holds a link <c>FOR NO KEY UPDATE</c> and finally <c>DELETE</c>s it, and that
/// <c>DELETE</c> waits for any transaction that bound an item or a setting to the link (a key share) and then fails with
/// <c>23001</c> (RESTRICT) when the binder commits — an unmapped error, a 500. The unlink case below opens such a binder in the gap
/// and commits it a second later; before the fix the unlink waited for it and failed, after it the unlink ends before the binder does.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class LinkJudgementWindowTests(OrganizationsDatabaseFixture database)
{
    private const string User = "window-test-user";
    private static readonly DateTimeOffset Created = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    // CR-4 · a team named by a creation whose link commits after the lock: the lock held nothing, so the team is not linked (409
    // team.not_in_project); no item is written. (Before the fix the fresh read judged it linked and the item was written owned by a
    // link nobody held.)
    [Fact]
    public async Task Create_ForATeamLinkedAfterTheLock_IsNotInProject_AndWritesNothing()
    {
        var world = await BareProjectAsync();
        var gap = new AfterLock("FOR SHARE", () => database.LinkAsync(world.OrganizationId, world.ProjectId, world.Team));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => RunAsync(
            gap,
            provider => provider.GetRequiredService<CreateWorkItemHandler>().Handle(
                new CreateWorkItemCommand(
                    world.OrganizationId.ToString(), world.ProjectId.ToString(), Guid.CreateVersion7().ToString(), world.TypeId.ToString(), "Для команды",
                    Team: new TeamChange(world.Team.ToString())),
                CancellationToken.None)));

        Assert.True(gap.Fired);
        Assert.Equal(OrganizationErrorCodes.TeamNotInProject, refusal.Code);
        Assert.Equal(["0"], await database.QueryAsync("SELECT count(*)::text FROM work_items WHERE project_id = @project", ("project", world.ProjectId)));
        Assert.Equal(["1"], await database.QueryAsync("SELECT count(*)::text FROM project_teams WHERE project_id = @project", ("project", world.ProjectId)));
    }

    // CR-4 · the first team of a team-less project is its default (BR-OL-7), but only a team whose link the creation holds can be: a
    // link committed after the lock is not held, so the item is «Без команды». With the link committed before the lock the same
    // creation is owned by the team — the case is the control that the gap, and not the setup, makes the difference.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Create_WithTheDefault_OwnsTheOnlyTeamOnlyWhenItsLinkWasThereAtTheLock(bool linkCommitsInTheGap, bool ownedByTheTeam)
    {
        var world = await BareProjectAsync();
        var gap = new AfterLock("FOR SHARE", () => linkCommitsInTheGap ? database.LinkAsync(world.OrganizationId, world.ProjectId, world.Team) : Task.CompletedTask);

        if (!linkCommitsInTheGap)
        {
            await database.LinkAsync(world.OrganizationId, world.ProjectId, world.Team);
        }

        await RunAsync(
            gap,
            provider => provider.GetRequiredService<CreateWorkItemHandler>().Handle(
                new CreateWorkItemCommand(
                    world.OrganizationId.ToString(), world.ProjectId.ToString(), Guid.CreateVersion7().ToString(), world.TypeId.ToString(), "По умолчанию"),
                CancellationToken.None));

        Assert.Equal(
            [ownedByTheTeam ? world.Team.ToString() : "NULL"],
            await database.QueryAsync(
                "SELECT coalesce(owning_team_id::text, 'NULL') FROM work_items WHERE project_id = @project", ("project", world.ProjectId)));
        Assert.Equal(["1"], await database.QueryAsync("SELECT count(*)::text FROM project_teams WHERE project_id = @project", ("project", world.ProjectId)));
    }

    // S-1 · a save to a team whose link commits after the lock is 409 team.not_in_project, and the item keeps its team and its time.
    [Fact]
    public async Task Save_ToATeamLinkedAfterTheLock_IsNotInProject_AndKeepsTheStoredTeam()
    {
        var world = await LinkedProjectAsync();
        var gap = new AfterLock("FOR SHARE", () => database.LinkAsync(world.OrganizationId, world.ProjectId, world.Team));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => RunAsync(
            gap,
            provider => provider.GetRequiredService<SaveWorkItemHandler>().Handle(
                new SaveWorkItemCommand(
                    world.OrganizationId.ToString(), world.ProjectId.ToString(), world.Identifier, Team: new TeamChange(world.Team.ToString())),
                CancellationToken.None)));

        Assert.True(gap.Fired);
        Assert.Equal(OrganizationErrorCodes.TeamNotInProject, refusal.Code);
        Assert.Equal(
            [$"{world.OwnTeam}|true"],
            await database.QueryAsync(
                "SELECT concat_ws('|', owning_team_id::text, (changed_at = @created)::text) FROM work_items WHERE project_id = @project",
                ("project", world.ProjectId),
                ("created", Created)));
    }

    // S-1 · a default naming of a team whose link commits after the lock is 409 team.not_in_project, and the setting stays empty.
    [Fact]
    public async Task NameDefault_ForATeamLinkedAfterTheLock_IsNotInProject_AndNamesNothing()
    {
        var world = await LinkedProjectAsync();
        var gap = new AfterLock("FOR SHARE", () => database.LinkAsync(world.OrganizationId, world.ProjectId, world.Team));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => RunAsync(
            gap,
            provider => provider.GetRequiredService<SetDefaultTeamHandler>().Handle(
                new SetDefaultTeamCommand(world.OrganizationId.ToString(), world.ProjectId.ToString(), world.Team.ToString()),
                CancellationToken.None)));

        Assert.True(gap.Fired);
        Assert.Equal(OrganizationErrorCodes.TeamNotInProject, refusal.Code);
        Assert.Equal(["NULL"], await database.QueryAsync("SELECT coalesce(default_team_id::text, 'NULL') FROM projects WHERE id = @project", ("project", world.ProjectId)));
    }

    // S-1 · the unlink of a team whose link commits after the lock statement — and which an item write binds to at once, a key share
    // the unlink's NO KEY UPDATE admits: the unlink holds nothing of that link, so it is the unlink of an unlinked pair, a no-op
    // (BR-OL-4), and it ends without touching the link. Before the fix it judged the pair linked on a later read, passed the
    // binder's uncommitted item by, waited at its final DELETE for the binder and failed with 23001 when the binder committed.
    [Fact]
    public async Task Unlink_OfATeamLinkedAfterTheLock_IsANoOp_AndNeverWaitsForABinderOfThatLink()
    {
        var world = await LinkedProjectAsync();
        Task binderCommitted = Task.CompletedTask;
        var gap = new AfterLock(
            "FOR NO KEY UPDATE",
            async () =>
            {
                await database.LinkAsync(world.OrganizationId, world.ProjectId, world.Team);
                binderCommitted = await BindAnItemAsync(world, world.Team, number: 2, commitAfter: TimeSpan.FromSeconds(1));
            });

        await RunAsync(
            gap,
            provider => provider.GetRequiredService<UnlinkTeamHandler>().Handle(
                new UnlinkTeamCommand(
                    world.OrganizationId.ToString(), world.ProjectId.ToString(), world.Team.ToString(), new UnlinkDestination(null)),
                CancellationToken.None));
        await binderCommitted;

        Assert.True(gap.Fired);
        Assert.Equal(
            ["1|1"],
            await database.QueryAsync(
                """
                SELECT concat_ws('|',
                    (SELECT count(*) FROM project_teams WHERE project_id = @project AND team_id = @team),
                    (SELECT count(*) FROM work_items WHERE project_id = @project AND owning_team_id = @team))
                """,
                ("project", world.ProjectId),
                ("team", world.Team)));
    }

    // S-1 · the same unlink with a destination whose link commits after the lock statement: the destination is not linked as far as the
    // request goes, so it is refused with 409 team.not_in_project and nothing moves (the team keeps its item and its link).
    [Fact]
    public async Task Unlink_ToADestinationLinkedAfterTheLock_IsNotInProject_AndMovesNothing()
    {
        var world = await LinkedProjectAsync();
        var gap = new AfterLock("FOR NO KEY UPDATE", () => database.LinkAsync(world.OrganizationId, world.ProjectId, world.Team));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => RunAsync(
            gap,
            provider => provider.GetRequiredService<UnlinkTeamHandler>().Handle(
                new UnlinkTeamCommand(
                    world.OrganizationId.ToString(), world.ProjectId.ToString(), world.OwnTeam.ToString(), new UnlinkDestination(world.Team.ToString())),
                CancellationToken.None)));

        Assert.True(gap.Fired);
        Assert.Equal(OrganizationErrorCodes.TeamNotInProject, refusal.Code);
        Assert.Equal(
            [$"{world.OwnTeam}|1|1"],
            await database.QueryAsync(
                """
                SELECT concat_ws('|',
                    (SELECT owning_team_id::text FROM work_items WHERE project_id = @project AND number = 1),
                    (SELECT count(*) FROM project_teams WHERE project_id = @project AND team_id = @own),
                    (SELECT count(*) FROM project_teams WHERE project_id = @project AND team_id = @team))
                """,
                ("project", world.ProjectId),
                ("own", world.OwnTeam),
                ("team", world.Team)));
    }

    // --- Arrangement -------------------------------------------------------------------------------------------------

    /// <summary>
    /// A world of one project. <see cref="Team"/> is a team of the organization that is <b>not linked</b> to the project at first;
    /// <see cref="OwnTeam"/> is linked (or <see cref="Guid.Empty"/> for a team-less project); <see cref="Identifier"/> addresses the
    /// project's first item, owned by <see cref="OwnTeam"/>, when there is one.
    /// </summary>
    private sealed record World(
        Guid OrganizationId, Guid ProjectId, Guid RootAreaId, Guid TypeId, Guid StateId, Guid Team, Guid OwnTeam, string Identifier);

    /// <summary>A project with no team at all, and an unlinked team of its organization.</summary>
    private async Task<World> BareProjectAsync()
    {
        var structure = await database.CreateStructureAsync();
        var organization = structure.Organization.Id;
        var project = await database.AddProjectAsync(organization, "Без команд", "NOT");
        var team = await database.AddTeamAsync(organization, OrganizationsDatabaseFixture.UniqueName("Первая"));
        var (type, state) = await StoryTypeAsync(organization);

        return new World(organization, project.Project.Id, project.Root.Id, type, state, team.Id, Guid.Empty, string.Empty);
    }

    /// <summary>A project with one linked team that owns its first item, and an unlinked team of the organization.</summary>
    private async Task<World> LinkedProjectAsync()
    {
        var structure = await database.CreateStructureAsync();
        var organization = structure.Organization.Id;
        var team = await database.AddTeamAsync(organization, OrganizationsDatabaseFixture.UniqueName("Отдельная"));
        var (type, state) = await StoryTypeAsync(organization);
        var project = structure.Project.Project.Id;
        var root = structure.Project.Root.Id;

        await database.ExecuteAsync(
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, owning_team_id)
            VALUES (gen_random_uuid(), @project, 1, @type, @state, @area, 'Первый', '', gen_random_uuid(), @created, @user, @created, @own)
            """,
            ("project", project),
            ("type", type),
            ("state", state),
            ("area", root),
            ("created", Created),
            ("user", User),
            ("own", structure.FirstTeam.Id));

        var key = (await database.QueryAsync("SELECT key FROM projects WHERE id = @project", ("project", project))).Single();
        var typeKey = (await database.QueryAsync("SELECT key FROM work_item_types WHERE id = @type", ("type", type))).Single();

        return new World(organization, project, root, type, state, team.Id, structure.FirstTeam.Id, $"{key}-{typeKey}-1");
    }

    private async Task<(Guid Type, Guid State)> StoryTypeAsync(Guid organizationId)
    {
        var row = (await database.QueryAsync(
            """
            SELECT concat_ws('|', type.id::text, state.id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS state ON state.work_item_type_id = type.id AND state.position = 1
            WHERE process.organization_id = @organization AND type.key = 'US'
            """,
            ("organization", organizationId))).Single().Split('|');

        return (Guid.Parse(row[0]), Guid.Parse(row[1]));
    }

    /// <summary>
    /// A transaction of another request that binds a new item of the project to <paramref name="team"/> and stays open (the insert
    /// takes a key share of the link), committed by the returned task after <paramref name="commitAfter"/> — never later than that,
    /// so a request that waits for it fails or ends, it does not hang.
    /// </summary>
    private async Task<Task> BindAnItemAsync(World world, Guid team, int number, TimeSpan commitAfter)
    {
        var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();

        await using (var command = new NpgsqlCommand(
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, owning_team_id)
            VALUES (gen_random_uuid(), @project, @number, @type, @state, @area, 'Связанный', '', gen_random_uuid(), @created, @user, @created, @team)
            """,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue("project", world.ProjectId);
            command.Parameters.AddWithValue("number", number);
            command.Parameters.AddWithValue("type", world.TypeId);
            command.Parameters.AddWithValue("state", world.StateId);
            command.Parameters.AddWithValue("area", world.RootAreaId);
            command.Parameters.AddWithValue("created", Created);
            command.Parameters.AddWithValue("user", User);
            command.Parameters.AddWithValue("team", team);
            await command.ExecuteNonQueryAsync();
        }

        return Task.Run(async () =>
        {
            await Task.Delay(commitAfter);
            await transaction.CommitAsync();
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        });
    }

    private async Task RunAsync(IInterceptor interceptor, Func<IServiceProvider, Task> run)
    {
        await using var provider = BuildServices(interceptor);
        await using var scope = provider.CreateAsyncScope();

        await run(scope.ServiceProvider);
    }

    private ServiceProvider BuildServices(IInterceptor interceptor)
    {
        var services = new ServiceCollection();

        services.AddDbContext<MustaskDbContext>(options => options.UseNpgsql(database.ConnectionString).AddInterceptors(interceptor));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserDirectory, UserDirectoryReadModel>();
        services.AddScoped<IProcessDefinitionReadModel, ProcessDefinitionReadModel>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ICurrentUser>(new FixedUser());
        services.AddScoped<IOrganizationAccess, AdministratorAccess>();
        services.AddApplication();
        services.AddOrganizationsInfrastructure();
        services.AddWorkItemsInfrastructure();
        services.AddBoardsInfrastructure();

        return services.BuildServiceProvider();
    }

    private sealed class FixedUser : ICurrentUser
    {
        public string DisplayName => "Window Test User";

        public string SubjectId => User;
    }

    /// <summary>The caller as an administrator of the organization: the handlers ask for their second codes.</summary>
    private sealed class AdministratorAccess : IOrganizationAccess
    {
        public OrganizationRole Role => OrganizationRole.Admin;

        public bool Holds(string code) => OrganizationRoleBundles.Holds(Role, code);
    }

    /// <summary>
    /// Runs <c>inTheGap</c> once, right after the first statement on <c>project_teams</c> that takes the lock <c>lockMode</c> has
    /// returned and before the request sends its next statement: a link committed by another transaction after the lock statement's
    /// snapshot and before the request's next read.
    /// </summary>
    private sealed class AfterLock(string lockMode, Func<Task> inTheGap) : DbCommandInterceptor
    {
        public bool Fired { get; private set; }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!Fired
                && command.CommandText.Contains("FROM project_teams", StringComparison.Ordinal)
                && command.CommandText.Contains(lockMode, StringComparison.Ordinal))
            {
                Fired = true;
                await inTheGap();
            }

            return result;
        }
    }
}
