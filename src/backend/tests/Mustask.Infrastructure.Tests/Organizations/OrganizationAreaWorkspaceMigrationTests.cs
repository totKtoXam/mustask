using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Application.Abstractions;
using Mustask.Application.Features.Process;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// mod-018 US-5 (OQ-1 A): <c>OrganizationAreaWorkspace</c> drops <b>every</b> per-project area and the work item's area column with them,
/// keeps every other row, logs what it dropped, and leaves a database on which an organization's area tree and its links can be built.
/// The legacy shape is written with SQL: the current model cannot write it. Every test gets a database of its own on the fixture's
/// server, migrated to the migration before this one, so the counts it logs are exact. These tests need Docker and were written where
/// Docker was not running: they are the first thing the QA session runs.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class OrganizationAreaWorkspaceMigrationTests(OrganizationsDatabaseFixture database) : IClassFixture<OrganizationsDatabaseFixture>
{
    private const string Migration = "_OrganizationAreaWorkspace";
    private static readonly DateTimeOffset Changed = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    // US-5 · "every area is dropped": the five areas (a root with two children in one project, a root in another, a root of a
    // deactivated project) are gone, and the items that pointed at them are still there, one row each, with their other fields.
    [Fact]
    public async Task Upgrade_DropsEveryArea_KeepsEveryItemAndEveryProject()
    {
        var legacy = await NewLegacyDatabaseAsync();
        var seeded = await SeedAsync(legacy);
        var before = await ItemsFingerprintAsync(legacy);

        var notices = await MigrateAsync(legacy, Migration);

        Assert.Equal(["0"], await QueryAsync(legacy, "SELECT count(*) FROM areas"));
        Assert.Equal(["4"], await QueryAsync(legacy, "SELECT count(*) FROM work_items"));
        Assert.Equal(["3"], await QueryAsync(legacy, "SELECT count(*) FROM projects"));
        Assert.Equal(before, await ItemsFingerprintAsync(legacy));
        Assert.Equal(
            ["0"],
            await QueryAsync(legacy, "SELECT count(*) FROM information_schema.columns WHERE table_name = 'work_items' AND column_name = 'area_id'"));
        Assert.Equal("mod-018: 5 areas dropped (3 of them project roots) and 4 work items whose area it cleared", Assert.Single(notices));
        _ = seeded;
    }

    // US-5 · "the tree is built afresh": after the upgrade an organization-level area is created, and linked to a project and a team;
    // the old names are free again.
    [Fact]
    public async Task Upgrade_ThenAnOrganizationAreaIsCreatedAndLinked_ThroughTheKeysOfTheNewShape()
    {
        var legacy = await NewLegacyDatabaseAsync();
        var seeded = await SeedAsync(legacy);
        await MigrateAsync(legacy, Migration);
        var area = Guid.CreateVersion7();
        var team = Guid.CreateVersion7();

        await ExecuteAsync(
            legacy,
            """
            INSERT INTO areas (id, organization_id, parent_id, name, normalized_name, inherits_access, created_by, created_at)
            VALUES (@area, @organization, NULL, 'Платформа', 'ПЛАТФОРМА', true, 'test', now());
            INSERT INTO area_projects (area_id, project_id, organization_id, linked_by, linked_at)
            VALUES (@area, @project, @organization, 'test', now());
            INSERT INTO teams (id, organization_id, name, normalized_name, status) VALUES (@team, @organization, 'Команда', 'КОМАНДА', 'Active');
            INSERT INTO area_teams (area_id, team_id, organization_id, linked_by, linked_at)
            VALUES (@area, @team, @organization, 'test', now())
            """,
            ("area", area),
            ("team", team),
            ("organization", seeded.Organization),
            ("project", seeded.Main));

        Assert.Equal(["1|1"], await QueryAsync(legacy, "SELECT concat_ws('|', (SELECT count(*) FROM area_projects), (SELECT count(*) FROM area_teams))"));
    }

    // US-5 · "a link never crosses an organization": the composite keys refuse a project of another organization.
    [Fact]
    public async Task Upgrade_ALinkToAProjectOfAnotherOrganization_IsRefusedByTheKey()
    {
        var legacy = await NewLegacyDatabaseAsync();
        var seeded = await SeedAsync(legacy);
        await MigrateAsync(legacy, Migration);
        var area = Guid.CreateVersion7();
        await ExecuteAsync(
            legacy,
            "INSERT INTO areas (id, organization_id, parent_id, name, normalized_name, inherits_access, created_by, created_at) VALUES (@area, @organization, NULL, 'А', 'А', true, 'test', now())",
            ("area", area),
            ("organization", seeded.Organization));

        var refused = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            legacy,
            "INSERT INTO area_projects (area_id, project_id, organization_id, linked_by, linked_at) VALUES (@area, @project, @organization, 'test', now())",
            ("area", area),
            ("project", seeded.Other),
            ("organization", seeded.Organization)));

        Assert.Equal("23503", refused.SqlState);
    }

    // The rollback: the old shape comes back (a project on the area, the item's column), the tables of the new shape are gone, and the
    // migration applies again.
    [Fact]
    public async Task Rollback_RestoresTheOldShape_AndAppliesAgain()
    {
        var legacy = await NewLegacyDatabaseAsync();
        await SeedAsync(legacy);
        await MigrateAsync(legacy, Migration);

        await MigrateAsync(legacy, "previous");

        Assert.Equal(
            ["true|true|false"],
            await QueryAsync(
                legacy,
                """
                SELECT concat_ws('|',
                    EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'areas' AND column_name = 'project_id')::text,
                    EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'work_items' AND column_name = 'area_id')::text,
                    EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'area_projects')::text)
                """));

        await MigrateAsync(legacy, Migration);

        Assert.Equal(["true"], await QueryAsync(legacy, "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'area_projects')::text"));
    }

    // US-5 · "little to drop": a database with no area logs zeros and changes no row.
    [Fact]
    public async Task Upgrade_EmptyDatabase_LogsZeros()
    {
        var legacy = await NewLegacyDatabaseAsync();

        var notices = await MigrateAsync(legacy, Migration);

        Assert.Equal("mod-018: 0 areas dropped (0 of them project roots) and 0 work items whose area it cleared", Assert.Single(notices));
    }

    // --- Arrangement -------------------------------------------------------------------------------------------------

    private sealed record Seeded(Guid Organization, Guid Main, Guid Other);

    /// <summary>
    /// The legacy data: one organization with two projects (a root with two children in the first, a root in the second), a deactivated
    /// third project with a root, and four items — three in areas of the first project (the root and a child), one in the second.
    /// </summary>
    private async Task<Seeded> SeedAsync(string cs)
    {
        Guid organization;
        var main = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var archive = Guid.CreateVersion7();

        organization = await CreateOrganizationAsync(cs, "Организация А");
        var otherOrganization = await CreateOrganizationAsync(cs, "Организация Б");
        await InsertProjectAsync(cs, organization, main, "Пример", "PRM", "Active");
        await InsertProjectAsync(cs, organization, second, "Второй", "SEC", "Active");
        await InsertProjectAsync(cs, organization, archive, "Архив", "ARC", "Deactivated");
        var foreign = Guid.CreateVersion7();
        await InsertProjectAsync(cs, otherOrganization, foreign, "Чужой", "FRN", "Active");

        var rootMain = await InsertAreaAsync(cs, main, null, "Пример");
        var childOne = await InsertAreaAsync(cs, main, rootMain, "API");
        await InsertAreaAsync(cs, main, rootMain, "Web");
        var rootSecond = await InsertAreaAsync(cs, second, null, "Второй");
        await InsertAreaAsync(cs, archive, null, "Архив");
        var type = (await QueryAsync(
            cs,
            """
            SELECT concat_ws('|', type.id::text, state.id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS state ON state.work_item_type_id = type.id AND state.position = 1
            WHERE process.organization_id = @organization AND type.key = 'US'
            """,
            ("organization", organization))).Single();
        var parts = type.Split('|');
            await InsertItemAsync(cs, main, 1, Guid.Parse(parts[0]), Guid.Parse(parts[1]), rootMain);
            await InsertItemAsync(cs, main, 2, Guid.Parse(parts[0]), Guid.Parse(parts[1]), childOne);
            await InsertItemAsync(cs, main, 3, Guid.Parse(parts[0]), Guid.Parse(parts[1]), childOne);
            await InsertItemAsync(cs, second, 1, Guid.Parse(parts[0]), Guid.Parse(parts[1]), rootSecond);

        return new Seeded(organization, main, foreign);
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

    private static async Task<Guid> InsertAreaAsync(string cs, Guid projectId, Guid? parentId, string name)
    {
        var id = Guid.CreateVersion7();

        await ExecuteAsync(
            cs,
            "INSERT INTO areas (id, project_id, parent_id, name, normalized_name) VALUES (@id, @project, @parent, @name, upper(@name))",
            ("id", id),
            ("project", projectId),
            ("parent", (object?)parentId ?? DBNull.Value),
            ("name", name));

        return id;
    }

    private static Task InsertItemAsync(string cs, Guid project, int number, Guid type, Guid state, Guid area) =>
        ExecuteAsync(
            cs,
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at)
            VALUES (gen_random_uuid(), @project, @number, @type, @state, @area, 'Элемент', '', gen_random_uuid(), @changed, 'migration-test', @changed)
            """,
            ("project", project),
            ("number", number),
            ("type", type),
            ("state", state),
            ("area", area),
            ("changed", Changed));

    /// <summary>An item's project, number, title, state and «Изменён» as one hash: the area column is the one thing that goes.</summary>
    private static async Task<string> ItemsFingerprintAsync(string cs) =>
        Assert.Single(await QueryAsync(
            cs,
            "SELECT md5(coalesce(string_agg(concat_ws('|', id, project_id, number, title, state_id, changed_at), ';' ORDER BY id), '')) FROM work_items"));

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

    /// <summary>Migrates to this migration (<see cref="Migration"/>) or to the one before it (<c>"previous"</c>, found by name), and returns its notices.</summary>
    private static async Task<List<string>> MigrateAsync(string connectionString, string target)
    {
        await using var context = new MustaskDbContext(new DbContextOptionsBuilder<MustaskDbContext>().UseNpgsql(connectionString).Options);
        var notices = new List<string>();

        ((NpgsqlConnection)context.Database.GetDbConnection()).Notice += (_, e) =>
        {
            if (e.Notice.MessageText.StartsWith("mod-018:", StringComparison.Ordinal))
            {
                notices.Add(e.Notice.MessageText);
            }
        };

        var migrations = context.Database.GetMigrations().ToList();
        var id = target == "previous"
            ? migrations.TakeWhile(candidate => !candidate.EndsWith(Migration, StringComparison.Ordinal)).Last()
            : migrations.Single(candidate => candidate.EndsWith(Migration, StringComparison.Ordinal));

        await context.GetService<IMigrator>().MigrateAsync(id);

        return notices;
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
            rows.Add(Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return rows;
    }
}
