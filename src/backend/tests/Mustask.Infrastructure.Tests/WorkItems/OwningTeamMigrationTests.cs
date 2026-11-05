using Mustask.Infrastructure.Tests.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Mustask.Infrastructure.Tests.WorkItems;

/// <summary>
/// mod-017 BR-OT-14 (doc-012 § Existing data, OQ-4 A): <c>AddWorkItemOwningTeam</c> gives every item the team that owned its
/// area, resolved through the tree, and moves nothing else: «Изменён» stays, no project names a default team, and the areas keep
/// their names, parents and items. The legacy shape is written with SQL: the current model no longer has the area owner, so it
/// cannot write it. The database is this class's own: the tests roll migrations back.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class OwningTeamMigrationTests(OrganizationsDatabaseFixture database) : IClassFixture<OrganizationsDatabaseFixture>
{
    private const string FirstMigration = "_AddWorkItemOwningTeam";
    private static readonly DateTimeOffset Changed = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    // US-6 · "the upgrade keeps every item on its area's team": the root's owner, an inherited owner, an explicit owner
    // deeper down, a team's area with its own children, an item with nothing owned up the tree but the root; «Изменён» does
    // not move; no project names a default; the areas keep their names, parents and items.
    [Fact]
    public async Task Upgrade_MapsEveryItemToTheTeamThatOwnedItsArea()
    {
        await RollBackAsync();
        var world = await SeedLegacyAsync();
        var areasBefore = await AreasFingerprintAsync();

        var messages = await MigrateToAsync(FirstMigration);

        Assert.Equal(
            [
                "Корень|Пример проекта|t",
                "Область команды|Платформа|t",
                "Потомок команды|Платформа|t",
                "Явный владелец ниже|Пример проекта|t",
                "Наследует корень|Пример проекта|t",
            ],
            await ItemTeamsAsync(world.Project));
        Assert.Equal(["Корень|Архив|t", "Корень архива|Архив|t"], await ItemTeamsAsync(world.Deactivated));
        Assert.Equal(areasBefore, await AreasFingerprintAsync());
        Assert.Equal(["0"], await database.QueryAsync("SELECT count(*)::text FROM projects WHERE default_team_id IS NOT NULL"));
        Assert.Single(messages.Notices);
    }

    // US-6 · "a deactivated project and a deleted team are upgraded": the deactivated project's items are mapped like any
    // other; an item whose area chain ends at a deleted team has no team, and none is owned by a deleted team.
    [Fact]
    public async Task Upgrade_DeactivatedProjectAndDeletedTeam_NoItemOwnedByADeletedTeam()
    {
        await RollBackAsync();
        var world = await SeedLegacyAsync();

        await MigrateToAsync(FirstMigration);

        Assert.Equal(["Корень удалённого|Пример удалённого|t", "Область удалённой команды|-|t"], await ItemTeamsAsync(world.WithDeletedTeam));
        Assert.Equal(
            ["0"],
            await database.QueryAsync(
                "SELECT count(*)::text FROM work_items AS item JOIN teams AS team ON team.id = item.owning_team_id WHERE team.status = 'Deleted'"));
    }

    // US-6 · "the upgrade logs the counts": the migration reports the items it mapped and the items left without a team.
    [Fact]
    public async Task Upgrade_LogsTheCountsOfItemsMappedAndUnowned()
    {
        await RollBackAsync();
        await SeedLegacyAsync();
        var items = long.Parse((await database.QueryAsync("SELECT count(*)::text FROM work_items")).Single());

        var messages = await MigrateToAsync(FirstMigration);

        var unowned = long.Parse((await database.QueryAsync("SELECT count(*)::text FROM work_items WHERE owning_team_id IS NULL")).Single());
        Assert.True(unowned >= 1);
        Assert.Equal(
            $"mod-017: {items - unowned} work items took the team that owned their area; {unowned} work items have no team",
            Assert.Single(messages.Notices));
    }

    // The rollback drops exactly the two columns and the keys of the migration; the items and projects stay, and the
    // migration applies again to the same result.
    [Fact]
    public async Task Rollback_DropsTheColumnsAndKeys_AndTheMigrationAppliesAgain()
    {
        await RollBackAsync();
        var world = await SeedLegacyAsync();
        await MigrateToAsync(FirstMigration);
        var before = await ItemTeamsAsync(world.Project);

        await RollBackAsync();

        Assert.False(await ColumnExistsAsync("work_items", "owning_team_id"));
        Assert.False(await ColumnExistsAsync("projects", "default_team_id"));
        Assert.Equal(["5"], await database.QueryAsync("SELECT count(*)::text FROM work_items WHERE project_id = @project", ("project", world.Project)));

        await MigrateToAsync(FirstMigration);

        Assert.Equal(before, await ItemTeamsAsync(world.Project));
    }

    // --- Arrangement -------------------------------------------------------------------------------------------------

    private sealed record World(Guid Project, Guid Deactivated, Guid WithDeletedTeam);

    /// <summary>
    /// «Пример проекта» (first team, «Платформа» with its area, the area's child, an area under it owned again by the first
    /// team, a root child owning nothing), «Архив» (deactivated, one item in its root and one in a child) and «Пример
    /// удалённого» (a deleted team that still owns an area, the shape no code of the line writes but a database can hold).
    /// </summary>
    private async Task<World> SeedLegacyAsync()
    {
        var main = await database.CreateLegacyStructureAsync(projectName: "Пример проекта", key: "PRM");
        var archive = await database.AddLegacyProjectAsync(main.OrganizationId, "Архив", "ARC");
        var removed = await database.AddLegacyProjectAsync(main.OrganizationId, "Пример удалённого", "DEL");

        await database.ExecuteAsync("UPDATE projects SET status = 'Deactivated' WHERE id = @project", ("project", archive.ProjectId));

        var teamAreaChild = Guid.CreateVersion7();
        var explicitOwner = Guid.CreateVersion7();
        var inheritsRoot = Guid.CreateVersion7();
        var archiveChild = Guid.CreateVersion7();
        var deletedTeam = Guid.CreateVersion7();
        var deletedTeamArea = Guid.CreateVersion7();

        // The tree: root (F) > «Платформа» (T, default) > «Потомок» (inherits T), > «Явный» (owned again by F); root > «Наследует».
        await database.ExecuteAsync(
            """
            INSERT INTO areas (id, project_id, parent_id, name, normalized_name, owner_team_id, default_of_team_id) VALUES
                (@teamAreaChild, @project, @teamArea, 'Потомок', 'ПОТОМОК', NULL, NULL),
                (@explicitOwner, @project, @teamArea, 'Явный', 'ЯВНЫЙ', @first, NULL),
                (@inheritsRoot, @project, @root, 'Наследует', 'НАСЛЕДУЕТ', NULL, NULL),
                (@archiveChild, @archive, @archiveRoot, 'Дочерняя', 'ДОЧЕРНЯЯ', NULL, NULL);
            INSERT INTO teams (id, project_id, name, normalized_name, status, is_first_team)
            VALUES (@deletedTeam, @removed, 'Ушедшая', 'УШЕДШАЯ', 'Deleted', false);
            INSERT INTO areas (id, project_id, parent_id, name, normalized_name, owner_team_id, default_of_team_id)
            VALUES (@deletedTeamArea, @removed, @removedRoot, 'Область ушедшей', 'ОБЛАСТЬ УШЕДШЕЙ', @deletedTeam, NULL)
            """,
            ("teamAreaChild", teamAreaChild),
            ("explicitOwner", explicitOwner),
            ("inheritsRoot", inheritsRoot),
            ("archiveChild", archiveChild),
            ("deletedTeam", deletedTeam),
            ("deletedTeamArea", deletedTeamArea),
            ("project", main.ProjectId),
            ("teamArea", main.SecondAreaId),
            ("first", main.FirstTeamId),
            ("root", main.RootAreaId),
            ("archive", archive.ProjectId),
            ("archiveRoot", archive.RootAreaId),
            ("removed", removed.ProjectId),
            ("removedRoot", removed.RootAreaId));

        var type = await StoryTypeAsync(main.OrganizationId);

        await InsertItemAsync(main.ProjectId, 1, type, main.RootAreaId, "Корень");
        await InsertItemAsync(main.ProjectId, 2, type, main.SecondAreaId, "Область команды");
        await InsertItemAsync(main.ProjectId, 3, type, teamAreaChild, "Потомок команды");
        await InsertItemAsync(main.ProjectId, 4, type, explicitOwner, "Явный владелец ниже");
        await InsertItemAsync(main.ProjectId, 5, type, inheritsRoot, "Наследует корень");
        await InsertItemAsync(archive.ProjectId, 1, type, archive.RootAreaId, "Корень");
        await InsertItemAsync(archive.ProjectId, 2, type, archiveChild, "Корень архива");
        await InsertItemAsync(removed.ProjectId, 1, type, removed.RootAreaId, "Корень удалённого");
        await InsertItemAsync(removed.ProjectId, 2, type, deletedTeamArea, "Область удалённой команды");

        return new World(main.ProjectId, archive.ProjectId, removed.ProjectId);
    }

    private sealed record ItemType(Guid TypeId, Guid StateId);

    private async Task<ItemType> StoryTypeAsync(Guid organizationId)
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

        return new ItemType(Guid.Parse(row[0]), Guid.Parse(row[1]));
    }

    private Task InsertItemAsync(Guid project, int number, ItemType type, Guid area, string title) =>
        database.ExecuteAsync(
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at)
            VALUES (gen_random_uuid(), @project, @number, @type, @state, @area, @title, '', gen_random_uuid(), @changed, 'migration-test', @changed)
            """,
            ("project", project),
            ("number", number),
            ("type", type.TypeId),
            ("state", type.StateId),
            ("area", area),
            ("title", title),
            ("changed", Changed));

    /// <summary>The project's items as "title|team name or -|changed_at kept", in number order.</summary>
    private Task<List<string>> ItemTeamsAsync(Guid project) =>
        database.QueryAsync(
            """
            SELECT concat_ws('|', item.title, coalesce(team.name, '-'), CASE WHEN item.changed_at = @changed THEN 't' ELSE 'f' END)
            FROM work_items AS item
            LEFT JOIN teams AS team ON team.id = item.owning_team_id
            WHERE item.project_id = @project
            ORDER BY item.number
            """,
            ("project", project),
            ("changed", Changed));

    private async Task<string> AreasFingerprintAsync() =>
        Assert.Single(await database.QueryAsync(
            "SELECT md5(coalesce(string_agg(concat_ws('|', id, project_id, parent_id, name, normalized_name), ';' ORDER BY id), '')) FROM areas"));

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

    /// <summary>Migrates up to the migration with that name, on a connection whose notices of mod-017 are collected.</summary>
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

    private async Task<bool> ColumnExistsAsync(string table, string column) =>
        (await database.QueryAsync(
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @table AND column_name = @column)::text",
            ("table", table),
            ("column", column))).Single() == "true";
}
