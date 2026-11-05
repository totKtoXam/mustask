using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Boards;
using Mustask.Domain.Identity;
using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.Boards;
using Mustask.Infrastructure.Tests.Organizations;
using Mustask.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Boards;

/// <summary>
/// feat-040 plan-backend PD-8, PD-9, PD-11 (ADR-0028 rule 8, BR-TS-7): the repository-level proofs of the team backlog's reorder
/// across projects. The lock takes the team <c>FOR SHARE</c> and the members <c>FOR KEY SHARE</c> in the active projects of the
/// organization and <b>no project row and no project–team link</b>; it spares what is no member (the other team's item, the
/// Removed state's, another level's, a deactivated project's); the locking read is the membership; the departed drop spans the
/// active projects and keeps a deactivated project's places; a key violation of a place is a stale reorder; an unlink-shaped
/// sequence cannot deadlock with it. These run at the repository, like <c>ProjectBacklogMemberLockTests</c>.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class TeamBacklogMemberLockTests(OrganizationsDatabaseFixture database)
{
    // The reorder holds the team and the members: a Delete or a save of a member (FOR UPDATE) waits, the team's deletion
    // (FOR NO KEY UPDATE) waits, and what is no member is free. A deactivation of a project and an unlink's lock of the link wait
    // for nothing — the reorder takes no project row and no link (PD-8).
    [Fact]
    public async Task LockAsync_HoldsTheTeamAndTheMembersOfEveryActiveProject_AndNoProjectRowNorLink()
    {
        var world = await TeamViewWorld.SeedAsync(database);

        await using var reorder = database.NewContext();
        await using var transaction = await reorder.Database.BeginTransactionAsync();

        Assert.True(await new BacklogRepository(reorder).LockAsync(world.OrganizationId, world.Scope, CancellationToken.None));

        foreach (var member in world.Members)
        {
            var refusal = await Assert.ThrowsAsync<PostgresException>(() => RowLockAsync(member));
            Assert.Equal(PostgresErrorCodes.LockNotAvailable, refusal.SqlState);
        }

        foreach (var nonMember in new[] { world.RemovedInFirst, world.TaskInFirst, world.StoryInDeactivated, world.StoryOfTheOtherTeam })
        {
            await RowLockAsync(nonMember);
        }

        var deletion = await Assert.ThrowsAsync<PostgresException>(() => LockAsync(
            "SELECT 1 FROM teams WHERE id = @team FOR NO KEY UPDATE", ("team", world.Team)));
        Assert.Equal(PostgresErrorCodes.LockNotAvailable, deletion.SqlState);

        foreach (var project in new[] { world.FirstProject, world.SecondProject, world.DeactivatedProject })
        {
            await LockAsync("SELECT 1 FROM projects WHERE id = @project FOR NO KEY UPDATE", ("project", project));
            await LockAsync(
                "SELECT 1 FROM project_teams WHERE project_id = @project AND team_id = @team FOR NO KEY UPDATE", ("project", project), ("team", world.Team));
        }

        await transaction.CommitAsync();

        await RowLockAsync(world.StoryInFirst);
    }

    // An unlink reassigns the items the team owns FOR UPDATE in id order; a reorder holds the members FOR KEY SHARE in id order: the
    // unlink waits for the reorder's commit and then completes — no deadlock, whichever the order (BR-TS-7, api-contract § 8.1).
    [Fact]
    public async Task LockAsync_AnUnlinkShapedSequence_WaitsForTheReorder_ThenCompletes_NoDeadlock()
    {
        var world = await TeamViewWorld.SeedAsync(database);

        await using var reorder = database.NewContext();
        await using var transaction = await reorder.Database.BeginTransactionAsync();
        Assert.True(await new BacklogRepository(reorder).LockAsync(world.OrganizationId, world.Scope, CancellationToken.None));

        var unlink = OwnedItemsForUpdateAsync(world.Team, TimeSpan.FromSeconds(20));
        await Task.Delay(500);
        Assert.False(unlink.IsCompleted, "The unlink-shaped lock did not wait for the reorder's member locks.");

        await transaction.CommitAsync();

        await unlink;
    }

    // The team is found by the organization of the address and its id: another organization's id is false, and nothing is locked.
    [Fact]
    public async Task LockAsync_ATeamOfAnotherOrganization_IsFalse_NothingIsLocked()
    {
        var world = await TeamViewWorld.SeedAsync(database);

        await using var reorder = database.NewContext();
        await using var transaction = await reorder.Database.BeginTransactionAsync();

        Assert.False(await new BacklogRepository(reorder).LockAsync(Guid.CreateVersion7(), world.Scope, CancellationToken.None));

        await RowLockAsync(world.StoryInFirst);
        await LockAsync("SELECT 1 FROM teams WHERE id = @team FOR NO KEY UPDATE", ("team", world.Team));
    }

    // A team deleted before the lock is not an active team: false, and the members are not locked (the 404 of the handler).
    [Fact]
    public async Task LockAsync_ADeletedTeam_IsFalse_NothingIsLocked()
    {
        var world = await TeamViewWorld.SeedAsync(database);
        await database.ExecuteAsync("UPDATE teams SET status = 'Deleted' WHERE id = @team", ("team", world.Team));

        await using var reorder = database.NewContext();
        await using var transaction = await reorder.Database.BeginTransactionAsync();

        Assert.False(await new BacklogRepository(reorder).LockAsync(world.OrganizationId, world.Scope, CancellationToken.None));

        await RowLockAsync(world.StoryInFirst);
    }

    // A team's reorder is of the whole backlog: a scope carrying a project filter (or a team filter) is a programming error.
    [Fact]
    public async Task LockAsync_AFilteredScope_IsRefused()
    {
        var world = await TeamViewWorld.SeedAsync(database);
        var byProject = world.Scope with { Filter = WorkItemFilter.OfProjects([world.FirstProject.ToString()]) };
        var byTeam = world.Scope with { Filter = WorkItemFilter.OfTeams([world.Team.ToString()]) };

        await using var reorder = database.NewContext();
        var repository = new BacklogRepository(reorder);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.LockAsync(world.OrganizationId, byProject, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.LockAsync(world.OrganizationId, byTeam, CancellationToken.None));
    }

    // The locking read is the membership: the team's live items of the level's types in the organization's active projects, with
    // their places, creation times and project keys, in id order, each locked until the commit; DeletedStateParityTests pins it equal
    // to the membership across the deleted states.
    [Fact]
    public async Task ReadEntriesAsync_IsTheMembership_WithPlacesCreationTimesAndKeys_AndLocksEachMember()
    {
        var world = await TeamViewWorld.SeedAsync(database);
        await database.ExecuteAsync(
            """
            INSERT INTO backlog_orders (team_id, backlog_level_id) VALUES (@team, @level);
            INSERT INTO backlog_places (work_item_id, team_id, backlog_level_id, position) VALUES (@item, @team, @level, 1)
            """,
            ("team", world.Team),
            ("level", world.Scope.LevelId),
            ("item", world.BugInSecond));

        await using var reorder = database.NewContext();
        await using var transaction = await reorder.Database.BeginTransactionAsync();

        var entries = await new BacklogRepository(reorder).ReadEntriesAsync(world.OrganizationId, world.Scope, CancellationToken.None);

        Assert.Equal(world.Members.ToHashSet(), entries.Select(entry => entry.ItemId).ToHashSet());
        Assert.Equal(1, entries.Single(entry => entry.ItemId == world.BugInSecond).Place);
        Assert.Null(entries.Single(entry => entry.ItemId == world.StoryInFirst).Place);
        Assert.Equal("SEC", entries.Single(entry => entry.ItemId == world.BugInSecond).ProjectKey);
        Assert.Equal("PRM", entries.Single(entry => entry.ItemId == world.StoryInFirst).ProjectKey);
        Assert.All(entries, entry => Assert.NotNull(entry.CreatedAt));

        var refusal = await Assert.ThrowsAsync<PostgresException>(() => RowLockAsync(world.StoryInFirst));
        Assert.Equal(PostgresErrorCodes.LockNotAvailable, refusal.SqlState);

        await using var membership = database.NewContext();
        var expected = await BacklogMembership.Members(membership, world.Scope, world.OrganizationId).Select(member => member.Item.Id).ToListAsync();
        Assert.Equal(expected.Order(), entries.Select(entry => entry.ItemId).Order());
    }

    // The tenant predicate: read under another organization the same scope reads nothing.
    [Fact]
    public async Task ReadEntriesAsync_UnderAnotherOrganization_ReadsNothing()
    {
        var world = await TeamViewWorld.SeedAsync(database);

        await using var reorder = database.NewContext();
        await using var transaction = await reorder.Database.BeginTransactionAsync();

        Assert.Empty(await new BacklogRepository(reorder).ReadEntriesAsync(Guid.CreateVersion7(), world.Scope, CancellationToken.None));
    }

    // PD-9, BR-TS-7: the departed drop is by predicate over the organization's active projects — the places of items that left the
    // backlog (the Removed one, the one of another level now) are dropped; the place of an item of a deactivated project is left
    // exactly as it is; the places of the members are rewritten densely 1..n.
    [Fact]
    public async Task SaveOrderAsync_DropsTheDepartedPlacesOfTheActiveProjects_KeepsADeactivatedProjectsPlace()
    {
        var world = await TeamViewWorld.SeedAsync(database);
        foreach (var (item, position) in new[] { (world.RemovedInFirst, 3), (world.StoryInDeactivated, 7) })
        {
            await PlaceAsync(world, item, position);
        }

        await using var reorder = database.NewContext();
        await using var transaction = await reorder.Database.BeginTransactionAsync();
        var repository = new BacklogRepository(reorder);
        Assert.True(await repository.LockAsync(world.OrganizationId, world.Scope, CancellationToken.None));

        await repository.SaveOrderAsync(world.OrganizationId, world.Scope, [world.BugInSecond, world.StoryInFirst], CancellationToken.None);
        await transaction.CommitAsync();

        Assert.Equal(
            new[] { $"{world.BugInSecond}:1", $"{world.StoryInFirst}:2", $"{world.StoryInDeactivated}:7" }.Order(),
            await PlacesAsync(world));
    }

    // PD-9: the places an item of an active project holds while it is still a member by owner, type and state are never "departed" —
    // the reorder that does not name it (a member added after the locking read cannot be: the lock holds) keeps no stale place.
    [Fact]
    public async Task SaveOrderAsync_AMemberNotNamedByTheOrder_KeepsItsPlace_BecauseItIsStillAMember()
    {
        var world = await TeamViewWorld.SeedAsync(database);
        await PlaceAsync(world, world.BugInSecond, 9);

        await using var reorder = database.NewContext();
        await using var transaction = await reorder.Database.BeginTransactionAsync();
        var repository = new BacklogRepository(reorder);
        Assert.True(await repository.LockAsync(world.OrganizationId, world.Scope, CancellationToken.None));

        await repository.SaveOrderAsync(world.OrganizationId, world.Scope, [world.StoryInFirst], CancellationToken.None);
        await transaction.CommitAsync();

        Assert.Equal(new[] { $"{world.BugInSecond}:9", $"{world.StoryInFirst}:1" }.Order(), await PlacesAsync(world));
    }

    // PD-11 (ADR-0028 rule 4): the backstops. An item gone from the table fails the place's key onto the item; an item owned by
    // another team fails the key onto the item's owner — each a stale reorder, never a server error.
    [Fact]
    public async Task SaveOrderAsync_AnItemThatLeftTheTable_OrOneOfAnotherTeam_IsAStaleReorder_NotAServerError()
    {
        var world = await TeamViewWorld.SeedAsync(database);

        foreach (var named in new[] { Guid.CreateVersion7(), world.StoryOfTheOtherTeam })
        {
            await using var reorder = database.NewContext();
            await using var transaction = await reorder.Database.BeginTransactionAsync();
            var repository = new BacklogRepository(reorder);
            Assert.True(await repository.LockAsync(world.OrganizationId, world.Scope, CancellationToken.None));

            var refused = await Assert.ThrowsAsync<ConflictException>(
                () => repository.SaveOrderAsync(world.OrganizationId, world.Scope, [world.StoryInFirst, named], CancellationToken.None));

            Assert.Equal(BoardErrorCodes.ReorderStale, refused.Code);
        }
    }

    // --- Arrangement -------------------------------------------------------------------------------------------------

    private async Task PlaceAsync(TeamViewWorld world, Guid item, int position)
    {
        await database.ExecuteAsync(
            "INSERT INTO backlog_orders (team_id, backlog_level_id) VALUES (@team, @level) ON CONFLICT (team_id, backlog_level_id) DO NOTHING",
            ("team", world.Team),
            ("level", world.Scope.LevelId));
        await database.ExecuteAsync(
            "INSERT INTO backlog_places (work_item_id, team_id, backlog_level_id, position) VALUES (@item, @team, @level, @position)",
            ("item", item),
            ("team", world.Team),
            ("level", world.Scope.LevelId),
            ("position", position));
    }

    /// <summary>The team places of the seed's items as <c>item:position</c>, ordered by text.</summary>
    private async Task<string[]> PlacesAsync(TeamViewWorld world) =>
        (await database.QueryAsync(
            "SELECT work_item_id::text || ':' || position::text FROM backlog_places WHERE team_id = @team ORDER BY 1", ("team", world.Team)))
        .Order()
        .ToArray();

    private async Task RowLockAsync(Guid itemId) =>
        await LockAsync("SELECT 1 FROM work_items WHERE id = @item FOR UPDATE", ("item", itemId));

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

    /// <summary>What an unlink's reassignment takes: every item the team owns <c>FOR UPDATE</c>, in id order; waits up to <paramref name="wait"/>.</summary>
    private async Task OwnedItemsForUpdateAsync(Guid team, TimeSpan wait)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var timeout = new NpgsqlCommand($"SET LOCAL lock_timeout = '{(int)wait.TotalMilliseconds}ms'", connection, transaction))
        {
            await timeout.ExecuteNonQueryAsync();
        }

        await using var command = new NpgsqlCommand("SELECT 1 FROM work_items WHERE owning_team_id = @team ORDER BY id FOR UPDATE", connection, transaction);
        command.Parameters.AddWithValue("team", team);
        await command.ExecuteNonQueryAsync();
        await transaction.RollbackAsync();
    }
}
