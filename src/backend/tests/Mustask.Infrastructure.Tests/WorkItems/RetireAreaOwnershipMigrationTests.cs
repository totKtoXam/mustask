using Mustask.Infrastructure.Tests.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Mustask.Infrastructure.Tests.WorkItems;

/// <summary>
/// mod-017 BR-OT-14 and the data drop (doc-012 § Existing data, OQ-4 A): <c>RetireAreaOwnership</c> deletes exactly the stale team
/// places — the rows whose team is not the item's owning team after <c>AddWorkItemOwningTeam</c> — and the three markers
/// (<c>areas.owner_team_id</c>, <c>areas.default_of_team_id</c>, <c>teams.is_first_team</c>), and logs both counts. No item, area,
/// team, project, project place or other row goes. It adds the place key: a team place names the item's current owning team.
/// The legacy shape is written with SQL (the current model cannot write it); the database is this class's own: the tests roll
/// migrations back.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class RetireAreaOwnershipMigrationTests(OrganizationsDatabaseFixture database) : IClassFixture<OrganizationsDatabaseFixture>
{
    private const string FirstMigration = "_AddWorkItemOwningTeam";
    private const string SecondMigration = "_RetireAreaOwnership";
    private const string PlaceKey = "fk_backlog_places_work_items_owner";

    // US-6 · "the upgrade drops what was already unread": a place that names the item's mapped owner stays, one that names
    // another team goes; no item, area, team or project goes with it. Two items of the team's area, one of the root.
    [Fact]
    public async Task Upgrade_DeletesTheStaleTeamPlaces_KeepsThoseNamingTheMappedOwner_DeletesNothingElse()
    {
        await RollBackAsync();
        var world = await SeedLegacyAsync();
        await MigrateToAsync(FirstMigration);
        var before = await CountsAsync();
        Assert.Equal(3, (await PlacesOfAsync(world.RootItem, world.StaleItem, world.TeamItemKept)).Count);

        await MigrateToAsync(SecondMigration);

        Assert.Equal(
            [$"{world.RootItem}|{world.FirstTeam}", $"{world.TeamItemKept}|{world.SecondTeam}"],
            await PlacesOfAsync(world.RootItem, world.StaleItem, world.TeamItemKept));
        Assert.Equal(before, await CountsAsync());
    }

    // US-6 · "the upgrade logs the counts": the stale places deleted and the places kept; the area owners, the default areas and
    // the first-team markers dropped. The expected numbers are read from the database just before the step, so rows other tests
    // left in this class's database count too.
    [Fact]
    public async Task Upgrade_LogsTheStalePlacesDeleted_AndTheMarkersDropped()
    {
        await RollBackAsync();
        await SeedLegacyAsync();
        await MigrateToAsync(FirstMigration);
        var stale = await ScalarAsync(
            "SELECT count(*) FROM backlog_places AS place JOIN work_items AS item ON item.id = place.work_item_id WHERE place.team_id IS DISTINCT FROM item.owning_team_id");
        var places = await ScalarAsync("SELECT count(*) FROM backlog_places");
        var owners = await ScalarAsync("SELECT count(*) FROM areas WHERE owner_team_id IS NOT NULL");
        var defaults = await ScalarAsync("SELECT count(*) FROM areas WHERE default_of_team_id IS NOT NULL");
        var firsts = await ScalarAsync("SELECT count(*) FROM teams WHERE is_first_team");

        var messages = await MigrateToAsync(SecondMigration);

        Assert.True(stale >= 1);
        Assert.Equal(
            [
                $"mod-017: {stale} stale team places deleted; {places - stale} team places kept",
                $"mod-017: {owners} area owners, {defaults} default areas and {firsts} first-team markers dropped",
            ],
            messages.Notices);
    }

    // The three markers and their keys, indexes and checks are gone; the model has no pending change.
    [Fact]
    public async Task Upgrade_DropsTheThreeMarkers_WithTheirKeysIndexesAndChecks()
    {
        await RollBackAsync();
        await SeedLegacyAsync();

        await MigrateToAsync(SecondMigration);

        Assert.False(await ColumnExistsAsync("areas", "owner_team_id"));
        Assert.False(await ColumnExistsAsync("areas", "default_of_team_id"));
        Assert.False(await ColumnExistsAsync("teams", "is_first_team"));
        Assert.Equal(
            ["0"],
            await database.QueryAsync(
                "SELECT count(*)::text FROM pg_constraint WHERE conname IN ('fk_areas_teams_project_id_owner_team_id', 'fk_areas_teams_project_id_default_of_team_id', 'ck_areas_default_owned', 'ck_areas_root_owned', 'ck_teams_first_team_active')"));
        Assert.Equal(
            ["0"],
            await database.QueryAsync(
                "SELECT count(*)::text FROM pg_indexes WHERE indexname IN ('ix_areas_default_of_team_id', 'ix_areas_project_id_default_of_team_id', 'ix_areas_project_id_owner_team_id', 'ix_teams_project_id_first_team')"));

        await using var context = database.NewContext();
        await context.Database.MigrateAsync();
        Assert.False(context.Database.HasPendingModelChanges(), "The model has changes no migration carries.");
    }

    // ADR-0028 rule 5: after the step a team place names the item's current owning team. A place of an item with no owner, a place
    // naming another team, and a change of the owner under a place are refused by the key; a place naming the owner is allowed.
    [Fact]
    public async Task PlaceKey_RefusesAPlaceOfAnItemWithNoOwnerOrOfAnotherTeam_AndAChangeOfOwnerUnderAPlace()
    {
        await RollBackAsync();
        var world = await SeedLegacyAsync();
        await MigrateToAsync(SecondMigration);
        await database.ExecuteAsync("DELETE FROM backlog_places WHERE work_item_id = @item", ("item", world.RootItem));
        const string InsertPlace = "INSERT INTO backlog_places (work_item_id, team_id, backlog_level_id, position) VALUES (@item, @team, @level, 9)";

        await database.ExecuteAsync("UPDATE work_items SET owning_team_id = NULL WHERE id = @item", ("item", world.RootItem));
        var noOwner = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            InsertPlace, ("item", world.RootItem), ("team", world.FirstTeam), ("level", world.Level)));

        await database.ExecuteAsync("UPDATE work_items SET owning_team_id = @team WHERE id = @item", ("item", world.RootItem), ("team", world.FirstTeam));
        var anotherTeam = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            InsertPlace, ("item", world.RootItem), ("team", world.SecondTeam), ("level", world.Level)));

        await database.ExecuteAsync(InsertPlace, ("item", world.RootItem), ("team", world.FirstTeam), ("level", world.Level));
        var reowned = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "UPDATE work_items SET owning_team_id = @team WHERE id = @item", ("item", world.RootItem), ("team", world.SecondTeam)));

        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, PlaceKey), (noOwner.SqlState, noOwner.ConstraintName));
        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, PlaceKey), (anotherTeam.SqlState, anotherTeam.ConstraintName));
        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, PlaceKey), (reowned.SqlState, reowned.ConstraintName));
    }

    // The rollback restores the schema and what the old checks require — the earliest active team of each project is its first
    // team and owns the root and its default — and drops the place key; it does not recreate the dropped data. The migration
    // applies again to the same result.
    [Fact]
    public async Task Rollback_RestoresTheMarkersAndTheChecks_DropsThePlaceKey_AndTheStepAppliesAgain()
    {
        await RollBackAsync();
        var world = await SeedLegacyAsync();
        await MigrateToAsync(SecondMigration);
        var projects = await ScalarAsync("SELECT count(*) FROM projects");

        await MigrateToAsync(FirstMigration);

        Assert.True(await ColumnExistsAsync("areas", "owner_team_id"));
        Assert.True(await ColumnExistsAsync("areas", "default_of_team_id"));
        Assert.True(await ColumnExistsAsync("teams", "is_first_team"));
        Assert.Equal(["0"], await database.QueryAsync("SELECT count(*)::text FROM pg_constraint WHERE conname = @key", ("key", PlaceKey)));
        Assert.Equal(projects, await ScalarAsync("SELECT count(*) FROM teams WHERE is_first_team"));
        Assert.Equal(
            ["0"],
            await database.QueryAsync("SELECT count(*)::text FROM areas WHERE parent_id IS NULL AND (owner_team_id IS NULL OR default_of_team_id <> owner_team_id)"));
        Assert.Equal(
            [world.FirstTeam.ToString()],
            await database.QueryAsync("SELECT owner_team_id::text FROM areas WHERE id = @root", ("root", world.RootArea)));

        await MigrateToAsync(SecondMigration);

        Assert.False(await ColumnExistsAsync("areas", "owner_team_id"));
        Assert.Equal(["1"], await database.QueryAsync("SELECT count(*)::text FROM pg_constraint WHERE conname = @key", ("key", PlaceKey)));
    }

    // --- Arrangement -------------------------------------------------------------------------------------------------

    private sealed record World(
        Guid FirstTeam, Guid SecondTeam, Guid RootArea, Guid Level, Guid RootItem, Guid StaleItem, Guid TeamItemKept);

    /// <summary>What the step must not touch (the places are judged by their own assertions): rows of the other tables.</summary>
    private sealed record Counts(long Items, long Areas, long Teams, long Projects, long ProjectPlaces, long Orders, long Memberships);

    /// <summary>
    /// A project in the legacy shape: a first team owning the root, a second team with its own area, an item in the root (its team
    /// place names the first team), an item in the team's area whose place names the first team (stale after the mapping) and one
    /// whose place names the second (kept).
    /// </summary>
    private async Task<World> SeedLegacyAsync()
    {
        var main = await database.CreateLegacyStructureAsync();
        var type = await StoryTypeAsync(main.OrganizationId);
        var (rootItem, staleItem, keptItem) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        await InsertItemAsync(rootItem, main.ProjectId, 1, type, main.RootAreaId);
        await InsertItemAsync(staleItem, main.ProjectId, 2, type, main.SecondAreaId);
        await InsertItemAsync(keptItem, main.ProjectId, 3, type, main.SecondAreaId);
        await database.ExecuteAsync(
            """
            INSERT INTO backlog_orders (team_id, backlog_level_id) VALUES (@first, @level), (@second, @level) ON CONFLICT DO NOTHING;
            INSERT INTO backlog_places (work_item_id, team_id, backlog_level_id, position) VALUES
                (@rootItem, @first, @level, 1),
                (@staleItem, @first, @level, 2),
                (@keptItem, @second, @level, 1)
            """,
            ("first", main.FirstTeamId),
            ("second", main.SecondTeamId),
            ("level", type.LevelId),
            ("rootItem", rootItem),
            ("staleItem", staleItem),
            ("keptItem", keptItem));

        return new World(main.FirstTeamId, main.SecondTeamId, main.RootAreaId, type.LevelId, rootItem, staleItem, keptItem);
    }

    private sealed record ItemType(Guid TypeId, Guid StateId, Guid LevelId);

    private async Task<ItemType> StoryTypeAsync(Guid organizationId)
    {
        var row = (await database.QueryAsync(
            """
            SELECT concat_ws('|', type.id::text, state.id::text, type.backlog_level_id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS state ON state.work_item_type_id = type.id AND state.position = 1
            WHERE process.organization_id = @organization AND type.key = 'US'
            """,
            ("organization", organizationId))).Single().Split('|');

        return new ItemType(Guid.Parse(row[0]), Guid.Parse(row[1]), Guid.Parse(row[2]));
    }

    private Task InsertItemAsync(Guid id, Guid project, int number, ItemType type, Guid area) =>
        database.ExecuteAsync(
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at)
            VALUES (@id, @project, @number, @type, @state, @area, 'Элемент', '', gen_random_uuid(), now(), 'migration-test', now())
            """,
            ("id", id),
            ("project", project),
            ("number", number),
            ("type", type.TypeId),
            ("state", type.StateId),
            ("area", area));

    /// <summary>The places of those items as "item|team", in the order of the items given.</summary>
    private async Task<List<string>> PlacesOfAsync(params Guid[] items)
    {
        var rows = await database.QueryAsync(
            "SELECT concat_ws('|', work_item_id, team_id) FROM backlog_places WHERE work_item_id = ANY(@items)", ("items", items));

        return items.SelectMany(item => rows.Where(row => row.StartsWith(item.ToString(), StringComparison.Ordinal))).ToList();
    }

    private async Task<Counts> CountsAsync() =>
        new(
            await ScalarAsync("SELECT count(*) FROM work_items"),
            await ScalarAsync("SELECT count(*) FROM areas"),
            await ScalarAsync("SELECT count(*) FROM teams"),
            await ScalarAsync("SELECT count(*) FROM projects"),
            await ScalarAsync("SELECT count(*) FROM project_backlog_places"),
            await ScalarAsync("SELECT count(*) FROM backlog_orders"),
            await ScalarAsync("SELECT count(*) FROM team_memberships"));

    private async Task<long> ScalarAsync(string sql) => long.Parse(Assert.Single(await database.QueryAsync(sql)));

    private async Task<bool> ColumnExistsAsync(string table, string column) =>
        (await database.QueryAsync(
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @table AND column_name = @column)::text",
            ("table", table),
            ("column", column))).Single() == "true";

    // --- The migrator ------------------------------------------------------------------------------------------------

    private sealed record Messages(List<string> Notices);

    /// <summary>Migrates to the migration before AddWorkItemOwningTeam, found by name, never hard-coded.</summary>
    private async Task RollBackAsync()
    {
        await using var context = database.NewContext();

        var previous = context.Database.GetMigrations()
            .TakeWhile(id => !id.EndsWith(FirstMigration, StringComparison.Ordinal))
            .Last();

        await context.GetService<IMigrator>().MigrateAsync(previous);
    }

    /// <summary>Migrates to the migration with that name, on a connection whose notices of mod-017 are collected.</summary>
    private async Task<Messages> MigrateToAsync(string suffix)
    {
        await using var context = database.NewContext();
        var messages = new Messages([]);

        ((NpgsqlConnection)context.Database.GetDbConnection()).Notice += (_, e) =>
        {
            if (e.Notice.MessageText.StartsWith("mod-017:", StringComparison.Ordinal))
            {
                messages.Notices.Add(e.Notice.MessageText);
            }
        };

        var target = context.Database.GetMigrations().Single(id => id.EndsWith(suffix, StringComparison.Ordinal));

        await context.GetService<IMigrator>().MigrateAsync(target);

        return messages;
    }
}
