using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Process;
using Mustask.Domain.Boards;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.Boards;
using Mustask.Infrastructure.Process;
using Mustask.Infrastructure.Tests.Organizations;
using Mustask.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Boards;

/// <summary>
/// mod-005 (ADR-0030 rule 6, "the finding"; plan-backend § 7.5): the project backlog's reorder locks its members as the team
/// reorder does, so a Delete or a purge of a member either waits for the reorder or is read as a non-member by its locking
/// read — the place's key onto the item can no longer fail with a server error. These are the repository's own proofs:
/// the lock is taken and conflicts with the row lock a Delete takes (<c>FOR UPDATE</c>), it spares what is no member, the
/// locking read is the membership, and the key's violation is mapped to a stale reorder.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class ProjectBacklogMemberLockTests(OrganizationsDatabaseFixture database)
{
    // A reorder that holds the members blocks the row lock a Delete takes on a member, and spares a row that is no member:
    // an item in «Удалена», and an item of another level.
    [Fact]
    public async Task LockAsync_HoldsTheMembersKeyShared_ADeleteOfAMemberWaits_ANonMemberDoesNot()
    {
        var world = await SeedAsync();

        await using var reorder = database.NewContext();
        await using var transaction = await reorder.Database.BeginTransactionAsync();

        Assert.True(await new ProjectBacklogRepository(reorder).LockAsync(world.OrganizationId, world.Scope, CancellationToken.None));

        // What a Delete takes first (IWorkItemRepository.LockAsync): the row FOR UPDATE.
        foreach (var member in new[] { world.StoryA, world.BugB })
        {
            var refusal = await Assert.ThrowsAsync<PostgresException>(() => RowLockAsync(member));

            Assert.Equal(PostgresErrorCodes.LockNotAvailable, refusal.SqlState);
        }

        await RowLockAsync(world.AlreadyRemoved);
        await RowLockAsync(world.OfAnotherLevel);

        // The project is held FOR SHARE: a deactivation (FOR NO KEY UPDATE) of it waits too.
        var deactivation = await Assert.ThrowsAsync<PostgresException>(() => LockAsync(
            "SELECT 1 FROM projects WHERE id = @project FOR NO KEY UPDATE", ("project", world.ProjectId)));
        Assert.Equal(PostgresErrorCodes.LockNotAvailable, deactivation.SqlState);

        await transaction.CommitAsync();

        // Released with the commit.
        await RowLockAsync(world.StoryA);
    }

    // The locks are not for a project that is no longer reachable: the answer is false and no member is locked.
    [Fact]
    public async Task LockAsync_AProjectOfAnotherOrganization_IsFalse_NothingIsLocked()
    {
        var world = await SeedAsync();

        await using var reorder = database.NewContext();
        await using var transaction = await reorder.Database.BeginTransactionAsync();

        Assert.False(await new ProjectBacklogRepository(reorder).LockAsync(Guid.CreateVersion7(), world.Scope, CancellationToken.None));

        await RowLockAsync(world.StoryA);
    }

    // A project backlog's reorder is of the whole backlog: a filtered scope is a programming error, not a narrower lock.
    [Fact]
    public async Task LockAsync_AScopeWithATeamFilter_IsRefused()
    {
        var world = await SeedAsync();
        var filtered = world.Scope with { Filter = WorkItemFilterOf(Guid.CreateVersion7()) };

        await using var reorder = database.NewContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ProjectBacklogRepository(reorder).LockAsync(world.OrganizationId, filtered, CancellationToken.None));
    }

    // The locking read is the membership: the live items of the level's types, with their places, in id order, and each
    // locked until the commit.
    [Fact]
    public async Task ReadEntriesAsync_IsTheMembership_WithPlacesAndCreationTimes_AndLocksEachMember()
    {
        var world = await SeedAsync();
        await database.ExecuteAsync(
            """
            INSERT INTO project_backlog_orders (project_id, backlog_level_id) VALUES (@project, @level);
            INSERT INTO project_backlog_places (work_item_id, project_id, backlog_level_id, position) VALUES (@item, @project, @level, 1)
            """,
            ("project", world.ProjectId),
            ("level", world.Scope.LevelId),
            ("item", world.BugB));

        await using var reorder = database.NewContext();
        await using var transaction = await reorder.Database.BeginTransactionAsync();

        var entries = await new ProjectBacklogRepository(reorder).ReadEntriesAsync(world.Scope, CancellationToken.None);

        Assert.Equal(new[] { world.StoryA, world.BugB }.ToHashSet(), entries.Select(entry => entry.ItemId).ToHashSet());
        Assert.Equal(1, entries.Single(entry => entry.ItemId == world.BugB).Place);
        Assert.Null(entries.Single(entry => entry.ItemId == world.StoryA).Place);
        Assert.All(entries, entry => Assert.NotNull(entry.CreatedAt));

        var refusal = await Assert.ThrowsAsync<PostgresException>(() => RowLockAsync(world.StoryA));
        Assert.Equal(PostgresErrorCodes.LockNotAvailable, refusal.SqlState);

        await using var membership = database.NewContext();
        var expected = await ProjectBacklogMembership.Members(membership, world.Scope).Select(member => member.Item.Id).ToListAsync();
        Assert.Equal(expected.Order(), entries.Select(entry => entry.ItemId).Order());
    }

    // The backstop: an item that is gone from the table when the place is written fails the place's key, which is a stale reorder.
    [Fact]
    public async Task SaveOrderAsync_AnItemThatLeftTheTable_IsAStaleReorder_NotAServerError()
    {
        var world = await SeedAsync();

        await using var reorder = database.NewContext();
        await using var transaction = await reorder.Database.BeginTransactionAsync();
        var repository = new ProjectBacklogRepository(reorder);
        Assert.True(await repository.LockAsync(world.OrganizationId, world.Scope, CancellationToken.None));

        var refused = await Assert.ThrowsAsync<ConflictException>(() => repository.SaveOrderAsync(world.Scope, [world.StoryA, Guid.CreateVersion7()], CancellationToken.None));

        Assert.Equal(BoardErrorCodes.ReorderStale, refused.Code);
    }

    // --- Arrangement -------------------------------------------------------------------------------------------------

    private static WorkItemFilter WorkItemFilterOf(Guid team) => new(true, [team], false);

    private async Task RowLockAsync(Guid itemId)
    {
        await LockAsync("SELECT 1 FROM work_items WHERE id = @item FOR UPDATE", ("item", itemId));
    }

    /// <summary>One statement in a transaction of its own that waits at most 300 ms for a lock, then fails with 55P03.</summary>
    private async Task LockAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var timeout = new NpgsqlCommand("SET LOCAL lock_timeout = '300ms'", connection, transaction))
        {
            await timeout.ExecuteNonQueryAsync();
        }

        await using var command = new NpgsqlCommand(sql, connection, transaction);

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
        await transaction.RollbackAsync();
    }

    private sealed record World(Guid OrganizationId, Guid ProjectId, ProjectBacklogScope Scope, Guid StoryA, Guid BugB, Guid AlreadyRemoved, Guid OfAnotherLevel);

    /// <summary>
    /// A project with two items of the «Истории» level in their first state (the members), one story in «Удалена» and one
    /// task (the non-members), and the project backlog's scope for the level.
    /// </summary>
    private async Task<World> SeedAsync()
    {
        var structure = await database.CreateStructureAsync();
        var organization = structure.Organization.Id;
        var project = structure.Project.Project.Id;

        await using var context = database.NewContext();
        var process = await new ProcessDefinitionReadModel(context).FindByOrganizationAsync(organization, CancellationToken.None)
            ?? throw new InvalidOperationException("The organization has a process.");
        var story = process.WorkItemTypes.Single(type => type.Key == "US");
        var bug = process.WorkItemTypes.Single(type => type.Key == "BUG");
        var task = process.WorkItemTypes.Single(type => type.Key == "TASK");
        Assert.Equal(story.BacklogLevelId, bug.BacklogLevelId);

        var ids = new[] { Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7() };
        var rows = new[]
        {
            (ids[0], 1, story, story.States.Single(state => state.Position == 1).Id),
            (ids[1], 2, bug, bug.States.Single(state => state.Position == 1).Id),
            (ids[2], 3, story, story.States.Single(state => state.Category == StateCategory.Removed).Id),
            (ids[3], 4, task, task.States.Single(state => state.Position == 1).Id),
        };

        foreach (var (id, number, type, state) in rows)
        {
            await database.ExecuteAsync(
                """
                INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, owning_team_id, title, description, creation_request_id, created_at, created_by, changed_at)
                VALUES (@id, @project, @number, @type, @state, @area, @team, 'Элемент', '', gen_random_uuid(), now() + @number * interval '1 second', 'lock-test', now())
                """,
                ("id", id),
                ("project", project),
                ("number", number),
                ("type", type.Id),
                ("state", state),
                ("area", structure.Project.Root.Id),
                ("team", structure.Project.Team.Id));
        }

        var scope = ProjectBacklogScope.Of(project, story.BacklogLevelId, process.WorkItemTypes.Where(type => type.BacklogLevelId == story.BacklogLevelId));

        return new World(organization, project, scope, ids[0], ids[1], ids[2], ids[3]);
    }
}
