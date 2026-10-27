using Mustask.Application.Abstractions;
using Mustask.Application.Features.Process;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// feat-028 BR-OL-13, US-7 (doc-012 § Existing data, OQ-1 C): <c>OrganizationLevelTeams</c> drops <b>every</b> team — of any status, of any
/// organization — with their memberships, every team place and team order row and every default-team setting, makes every owned work item
/// «Без команды» without moving its «Изменён», and keeps everything else; nothing is remapped, because a team of the old shape belongs to
/// one project and the names are never compared. The legacy shape is written with SQL: the current model cannot write it. Every test gets a
/// database of its own on the fixture's server, migrated to the migration before this one, so the counts it logs are exact.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class OrganizationLevelTeamsMigrationTests(OrganizationsDatabaseFixture database) : IClassFixture<OrganizationsDatabaseFixture>
{
    private const string Migration = "_OrganizationLevelTeams";
    private static readonly DateTimeOffset Changed = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    // US-7 · "every team is dropped with what hangs on it": the seven teams (Platform, Reports and a deleted Old of the first project,
    // "platform", Core and Ops of the second, Platform of another organization), their two memberships, three places, two order rows and
    // the one default setting are gone; nothing is left that names a team.
    [Fact]
    public async Task Upgrade_DropsEveryTeam_Memberships_Places_Orders_AndEveryDefaultSetting()
    {
        var legacy = await SeedLegacyAsync();

        await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal(
            ["0|0|0|0|0|0"],
            await QueryAsync(
                legacy.ConnectionString,
                """
                SELECT concat_ws('|',
                    (SELECT count(*) FROM teams),
                    (SELECT count(*) FROM team_memberships),
                    (SELECT count(*) FROM backlog_places),
                    (SELECT count(*) FROM backlog_orders),
                    (SELECT count(*) FROM projects WHERE default_team_id IS NOT NULL),
                    (SELECT count(*) FROM work_items WHERE owning_team_id IS NOT NULL))
                """));
    }

    // US-7 · "every owned item becomes «Без команды», nothing else moves": all seven items stay with their project, number, title, area,
    // state, assignee and «Изменён»; the five that were owned have no team, the two that were not are as they were.
    [Fact]
    public async Task Upgrade_ItemsLoseTheirOwner_KeepEverythingElse_ChangedAtDoesNotMove()
    {
        var legacy = await SeedLegacyAsync();
        var before = await ItemsFingerprintAsync(legacy.ConnectionString);

        await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal(before, await ItemsFingerprintAsync(legacy.ConnectionString));
        Assert.Equal(
            ["7|0|7"],
            await QueryAsync(
                legacy.ConnectionString,
                "SELECT concat_ws('|', count(*), count(*) FILTER (WHERE owning_team_id IS NOT NULL), count(*) FILTER (WHERE changed_at = @changed)) FROM work_items",
                ("changed", Changed)));
    }

    // US-7 · "a deactivated project is upgraded like any other": its owned item is without a team too, and a restore brings back a
    // project whose items have none.
    [Fact]
    public async Task Upgrade_RestoredDeactivatedProjectsItemIsAlsoWithoutATeam()
    {
        var legacy = await SeedLegacyAsync();

        await MigrateAsync(legacy.ConnectionString, null);
        await ExecuteAsync(legacy.ConnectionString, "UPDATE projects SET status = 'Active' WHERE id = @project", ("project", legacy.Archive));

        Assert.Equal(
            ["Ядро-элемент|-|t", "Без команды|-|t"],
            await QueryAsync(
                legacy.ConnectionString,
                """
                SELECT concat_ws('|', title, coalesce(owning_team_id::text, '-'), CASE WHEN changed_at = @changed THEN 't' ELSE 'f' END)
                FROM work_items WHERE project_id = @project ORDER BY number
                """,
                ("project", legacy.Archive),
                ("changed", Changed)));
    }

    // US-7 · "nothing but teams is dropped": the organizations, their memberships, the projects, the areas, the process and the items
    // are as many as before.
    [Fact]
    public async Task Upgrade_DropsNothingElse_CountsOfTheOtherTablesAreAsBefore()
    {
        var legacy = await SeedLegacyAsync();
        var before = await OtherTablesAsync(legacy.ConnectionString);

        await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal(before, await OtherTablesAsync(legacy.ConnectionString));
        Assert.Equal(["2|3|2|0"], await QueryAsync(legacy.ConnectionString, "SELECT concat_ws('|', (SELECT count(*) FROM organizations), (SELECT count(*) FROM projects), (SELECT count(*) FROM organization_memberships), (SELECT count(*) FROM teams)) "));
    }

    // US-7 · "the counts are logged": one notice, the counts of each kind dropped and the owned items made without a team.
    [Fact]
    public async Task Upgrade_LogsTheCounts_PerKind()
    {
        var legacy = await SeedLegacyAsync();

        var notices = await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal(
            "feat-028: 7 teams, 2 team memberships, 3 team places, 2 team order rows and 1 default-team settings dropped; 5 work items made without a team",
            Assert.Single(notices));
    }

    // US-7 · "little to drop": an organization with a project and no team logs zeros; an empty database changes no row and logs zeros.
    [Fact]
    public async Task Upgrade_OrganizationWithAProjectAndNoTeam_LogsZeros_ChangesNothing()
    {
        var connectionString = await NewLegacyDatabaseAsync();
        var organization = await CreateOrganizationAsync(connectionString, "Без команд");
        var project = Guid.CreateVersion7();
        await InsertProjectAsync(connectionString, organization, project, "Проект", "PRJ", "Active");
        var before = await OtherTablesAsync(connectionString);

        var notices = await MigrateAsync(connectionString, null);

        Assert.Equal(
            "feat-028: 0 teams, 0 team memberships, 0 team places, 0 team order rows and 0 default-team settings dropped; 0 work items made without a team",
            Assert.Single(notices));
        Assert.Equal(before, await OtherTablesAsync(connectionString));
    }

    [Fact]
    public async Task Upgrade_EmptyDatabase_ChangesNoRow_LogsZeros()
    {
        var connectionString = await NewLegacyDatabaseAsync();

        var notices = await MigrateAsync(connectionString, null);

        Assert.Equal(
            "feat-028: 0 teams, 0 team memberships, 0 team places, 0 team order rows and 0 default-team settings dropped; 0 work items made without a team",
            Assert.Single(notices));
        Assert.Equal(["0|0|0|0"], await QueryAsync(connectionString, "SELECT concat_ws('|', (SELECT count(*) FROM organizations), (SELECT count(*) FROM projects), (SELECT count(*) FROM teams), (SELECT count(*) FROM project_teams))"));
    }

    // US-7 · "an administrator rebuilds the structure": after the upgrade the dropped names are free and a team of the organization can be
    // created, linked to a project and given to an item — through the keys of the new shape.
    [Fact]
    public async Task Upgrade_ThenTheNamesAreFree_ATeamIsLinkedAndOwnsAnItem()
    {
        var legacy = await SeedLegacyAsync();
        await MigrateAsync(legacy.ConnectionString, null);
        var team = Guid.CreateVersion7();

        await ExecuteAsync(
            legacy.ConnectionString,
            """
            INSERT INTO teams (id, organization_id, name, normalized_name, status) VALUES (@team, @organization, 'Platform', 'PLATFORM', 'Active');
            INSERT INTO project_teams (project_id, team_id, organization_id, linked_by, linked_at) VALUES (@project, @team, @organization, 'test', now());
            UPDATE work_items SET owning_team_id = @team WHERE project_id = @project AND number = 1;
            UPDATE projects SET default_team_id = @team WHERE id = @project
            """,
            ("team", team),
            ("organization", legacy.OrganizationA),
            ("project", legacy.Main));

        Assert.Equal(["1"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM work_items WHERE owning_team_id = @team", ("team", team)));
    }

    // The rollback past this migration with a post-upgrade team, link, member and place present: the Down empties them again and restores the
    // old shape — a project column on the team, no link table, every project given one team named after it — and the migration applies
    // again, dropping those teams.
    [Fact]
    public async Task Rollback_WithPostUpgradeTeamLinkMemberAndPlace_RestoresTheOldShape_AndAppliesAgain()
    {
        var legacy = await SeedLegacyAsync();
        await MigrateAsync(legacy.ConnectionString, null);
        await SeedPostUpgradeAsync(legacy);

        await MigrateAsync(legacy.ConnectionString, "previous");

        Assert.Equal(
            ["true|false|3|0|0|0|0|0"],
            await QueryAsync(
                legacy.ConnectionString,
                """
                SELECT concat_ws('|',
                    EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'teams' AND column_name = 'project_id')::text,
                    EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'project_teams')::text,
                    (SELECT count(*) FROM teams),
                    (SELECT count(*) FROM team_memberships),
                    (SELECT count(*) FROM backlog_places),
                    (SELECT count(*) FROM backlog_orders),
                    (SELECT count(*) FROM projects WHERE default_team_id IS NOT NULL),
                    (SELECT count(*) FROM work_items WHERE owning_team_id IS NOT NULL))
                """));
        Assert.Equal(
            ["Архив", "Другой", "Пример проекта"],
            (await QueryAsync(legacy.ConnectionString, "SELECT name FROM teams")).Order(StringComparer.Ordinal));

        var notices = await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal(
            "feat-028: 3 teams, 0 team memberships, 0 team places, 0 team order rows and 0 default-team settings dropped; 0 work items made without a team",
            Assert.Single(notices));
        Assert.Equal(["0"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM teams"));
    }

    // --- Arrangement -------------------------------------------------------------------------------------------------

    private sealed record Legacy(
        string ConnectionString, Guid OrganizationA, Guid OrganizationB, Guid Main, Guid Archive, Guid Other);

    /// <summary>
    /// The story's legacy data (US-7), in the shape the migrations before this one left. Organization A has «Пример проекта» (Platform,
    /// Reports and a deleted Old; Platform is the project's default) and the deactivated «Архив» ("platform", Core, Ops); organization B has
    /// «Другой» with its own Platform. Two team memberships, three places on two orders, and seven items — five owned (three in the first
    /// project, one in the archive, one in the other organization), two not.
    /// </summary>
    private async Task<Legacy> SeedLegacyAsync()
    {
        var cs = await NewLegacyDatabaseAsync();
        var organizationA = await CreateOrganizationAsync(cs, "Организация А");
        var organizationB = await CreateOrganizationAsync(cs, "Организация Б");
        var main = Guid.CreateVersion7();
        var archive = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();

        await InsertProjectAsync(cs, organizationA, main, "Пример проекта", "PRM", "Active");
        await InsertProjectAsync(cs, organizationA, archive, "Архив", "ARC", "Deactivated");
        await InsertProjectAsync(cs, organizationB, other, "Другой", "OTH", "Active");

        var platform = await InsertTeamAsync(cs, main, "Platform", "Active");
        var reports = await InsertTeamAsync(cs, main, "Reports", "Active");
        await InsertTeamAsync(cs, main, "Old", "Deleted");
        await InsertTeamAsync(cs, archive, "platform", "Active");
        var core = await InsertTeamAsync(cs, archive, "Core", "Active");
        await InsertTeamAsync(cs, archive, "Ops", "Active");
        var platformOther = await InsertTeamAsync(cs, other, "Platform", "Active");

        await ExecuteAsync(
            cs,
            """
            INSERT INTO organization_memberships (organization_id, user_id, role, added_by, added_at) VALUES
                (@organization, 'legacy-1', 'member', 'legacy-0', @changed),
                (@organization, 'legacy-2', 'member', 'legacy-0', @changed);
            INSERT INTO team_memberships (team_id, user_id, organization_id) VALUES
                (@platform, 'legacy-1', @organization),
                (@core, 'legacy-2', @organization)
            """,
            ("organization", organizationA),
            ("platform", platform),
            ("core", core),
            ("changed", Changed));

        var type = await StoryTypeAsync(cs, organizationA);
        var typeB = await StoryTypeAsync(cs, organizationB);

        var rootMain = await InsertRootAsync(cs, main, "Пример проекта");
        var rootArchive = await InsertRootAsync(cs, archive, "Архив");
        var rootOther = await InsertRootAsync(cs, other, "Другой");

        await InsertItemAsync(cs, main, 1, type, rootMain, "Платформа-1", platform);
        await InsertItemAsync(cs, main, 2, type, rootMain, "Платформа-2", platform);
        await InsertItemAsync(cs, main, 3, type, rootMain, "Отчёты-1", reports);
        await InsertItemAsync(cs, main, 4, type, rootMain, "Без команды", null);
        await InsertItemAsync(cs, archive, 1, type, rootArchive, "Ядро-элемент", core);
        await InsertItemAsync(cs, archive, 2, type, rootArchive, "Без команды", null);
        await InsertItemAsync(cs, other, 1, typeB, rootOther, "Чужая платформа", platformOther);

        await ExecuteAsync(
            cs,
            """
            INSERT INTO backlog_orders (team_id, backlog_level_id) VALUES (@platform, @level), (@reports, @level);
            INSERT INTO backlog_places (work_item_id, team_id, backlog_level_id, position)
            SELECT id, @platform, @level, number FROM work_items WHERE project_id = @main AND number IN (1, 2);
            INSERT INTO backlog_places (work_item_id, team_id, backlog_level_id, position)
            SELECT id, @reports, @level, 1 FROM work_items WHERE project_id = @main AND number = 3;
            UPDATE projects SET default_team_id = @platform WHERE id = @main
            """,
            ("platform", platform),
            ("reports", reports),
            ("level", type.LevelId),
            ("main", main));

        return new Legacy(cs, organizationA, organizationB, main, archive, other);
    }

    /// <summary>Data a stand holds after the upgrade: a team of the organization, linked, with a member, an owned item, a place and a setting.</summary>
    private async Task SeedPostUpgradeAsync(Legacy legacy)
    {
        var team = Guid.CreateVersion7();
        var type = await StoryTypeAsync(legacy.ConnectionString, legacy.OrganizationA);

        await ExecuteAsync(
            legacy.ConnectionString,
            """
            INSERT INTO teams (id, organization_id, name, normalized_name, status) VALUES (@team, @organization, 'Новая', 'НОВАЯ', 'Active');
            INSERT INTO project_teams (project_id, team_id, organization_id, linked_by, linked_at) VALUES (@project, @team, @organization, 'test', now());
            INSERT INTO team_memberships (team_id, user_id, organization_id) VALUES (@team, 'legacy-1', @organization);
            UPDATE work_items SET owning_team_id = @team WHERE project_id = @project AND number IN (1, 2);
            INSERT INTO backlog_orders (team_id, backlog_level_id) VALUES (@team, @level);
            INSERT INTO backlog_places (work_item_id, team_id, backlog_level_id, position)
            SELECT id, @team, @level, number FROM work_items WHERE project_id = @project AND number = 1;
            UPDATE projects SET default_team_id = @team WHERE id = @project
            """,
            ("team", team),
            ("organization", legacy.OrganizationA),
            ("project", legacy.Main),
            ("level", type.LevelId));
    }

    private sealed record StoryType(Guid TypeId, Guid StateId, Guid LevelId);

    private static async Task<StoryType> StoryTypeAsync(string cs, Guid organizationId)
    {
        var row = (await QueryAsync(
            cs,
            """
            SELECT concat_ws('|', type.id::text, state.id::text, type.backlog_level_id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS state ON state.work_item_type_id = type.id AND state.position = 1
            WHERE process.organization_id = @organization AND type.key = 'US'
            """,
            ("organization", organizationId))).Single().Split('|');

        return new StoryType(Guid.Parse(row[0]), Guid.Parse(row[1]), Guid.Parse(row[2]));
    }

    private static Task InsertProjectAsync(string cs, Guid organizationId, Guid projectId, string name, string key, string status) =>
        ExecuteAsync(
            cs,
            """
            INSERT INTO projects (id, organization_id, name, normalized_name, key, status, created_at, created_by)
            VALUES (@project, @organization, @name, upper(@name), @key, @status, @changed, 'legacy-0')
            """,
            ("project", projectId),
            ("organization", organizationId),
            ("name", name),
            ("key", key),
            ("status", status),
            ("changed", Changed));

    private static async Task<Guid> InsertTeamAsync(string cs, Guid projectId, string name, string status)
    {
        var id = Guid.CreateVersion7();

        await ExecuteAsync(
            cs,
            "INSERT INTO teams (id, project_id, name, normalized_name, status) VALUES (@id, @project, @name, upper(@name), @status)",
            ("id", id),
            ("project", projectId),
            ("name", name),
            ("status", status));

        return id;
    }

    private static async Task<Guid> InsertRootAsync(string cs, Guid projectId, string name)
    {
        var id = Guid.CreateVersion7();

        await ExecuteAsync(
            cs,
            "INSERT INTO areas (id, project_id, parent_id, name, normalized_name) VALUES (@id, @project, NULL, @name, upper(@name))",
            ("id", id),
            ("project", projectId),
            ("name", name));

        return id;
    }

    private static Task InsertItemAsync(string cs, Guid project, int number, StoryType type, Guid area, string title, Guid? team) =>
        ExecuteAsync(
            cs,
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, owning_team_id)
            VALUES (gen_random_uuid(), @project, @number, @type, @state, @area, @title, '', gen_random_uuid(), @changed, 'migration-test', @changed, @team)
            """,
            ("project", project),
            ("number", number),
            ("type", type.TypeId),
            ("state", type.StateId),
            ("area", area),
            ("title", title),
            ("changed", Changed),
            ("team", (object?)team ?? DBNull.Value));

    /// <summary>An item's project, number, title, area, state, assignee and «Изменён» as one hash, whatever its team.</summary>
    private static async Task<string> ItemsFingerprintAsync(string cs) =>
        Assert.Single(await QueryAsync(
            cs,
            "SELECT md5(coalesce(string_agg(concat_ws('|', id, project_id, number, title, area_id, state_id, assignee_id, changed_at), ';' ORDER BY id), '')) FROM work_items"));

    /// <summary>The counts of everything that is not a team, as one line.</summary>
    private static async Task<string> OtherTablesAsync(string cs) =>
        Assert.Single(await QueryAsync(
            cs,
            """
            SELECT concat_ws('|',
                (SELECT count(*) FROM organizations),
                (SELECT count(*) FROM organization_memberships),
                (SELECT count(*) FROM projects),
                (SELECT count(*) FROM areas),
                (SELECT count(*) FROM work_items),
                (SELECT count(*) FROM processes),
                (SELECT count(*) FROM work_item_types),
                (SELECT count(*) FROM project_backlog_orders),
                (SELECT count(*) FROM project_backlog_places))
            """));

    // --- The databases -----------------------------------------------------------------------------------------------

    /// <summary>A new database on the fixture's server, migrated to the migration before this one.</summary>
    private async Task<string> NewLegacyDatabaseAsync()
    {
        var name = $"legacy_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(database.ConnectionString) { Database = name, Pooling = false }.ConnectionString;

        await MigrateAsync(connectionString, "previous");

        return connectionString;
    }

    /// <summary>
    /// Migrates to this migration (<c>null</c>; later migrations reshape the tables this test reads) or to the one before this migration (<c>"previous"</c>, found by name, never hard-coded),
    /// and returns the notices of feat-028 the migration raised.
    /// </summary>
    private static async Task<List<string>> MigrateAsync(string connectionString, string? target)
    {
        await using var context = new MustaskDbContext(new DbContextOptionsBuilder<MustaskDbContext>().UseNpgsql(connectionString).Options);
        var notices = new List<string>();

        ((NpgsqlConnection)context.Database.GetDbConnection()).Notice += (_, e) =>
        {
            if (e.Notice.MessageText.StartsWith("feat-028:", StringComparison.Ordinal))
            {
                notices.Add(e.Notice.MessageText);
            }
        };

        var migrations = context.Database.GetMigrations().ToList();
        var id = target is null
            ? migrations.Single(candidate => candidate.EndsWith(Migration, StringComparison.Ordinal))
            : migrations.TakeWhile(candidate => !candidate.EndsWith(Migration, StringComparison.Ordinal)).Last();

        await context.GetService<IMigrator>().MigrateAsync(id);

        return notices;
    }

    private async Task<Guid> CreateOrganizationAsync(string connectionString, string name)
    {
        await using var provider = database.BuildServices(connectionString);
        await using var scope = provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var seed = scope.ServiceProvider.GetRequiredService<IProcessSeed>();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var organizationId = Guid.CreateVersion7();

        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var processId = await seed.SeedAsync(organizationId, ct);

                context.Organizations.Add(
                    Organization.Create(
                        organizationId, OrganizationsDatabaseFixture.Name(OrganizationsDatabaseFixture.UniqueName(name)), processId, OrganizationsDatabaseFixture.Now, OrganizationsDatabaseFixture.Creator));
                await context.SaveChangesAsync(ct);

                return true;
            },
            CancellationToken.None);

        return organizationId;
    }

    private static async Task ExecuteAsync(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<string>> QueryAsync(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add(reader.IsDBNull(0) ? "NULL" : Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!);
        }

        return rows;
    }
}
