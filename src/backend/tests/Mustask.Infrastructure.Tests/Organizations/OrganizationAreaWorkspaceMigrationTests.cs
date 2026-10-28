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
/// mod-018 US-5 (the typed tree): <c>OrganizationAreaWorkspace</c> keeps <b>every</b> project's area tree as that project's node with its
/// sub-areas, types the nodes, gives them the organization and the author of their project, keeps every work item and its area, builds the
/// organization-level tables, fails naming the project when a tree is deeper than 10 levels, and rolls back. The legacy shape is written
/// with SQL: the current model cannot write it. Every test gets a database of its own on the fixture's server, migrated to the migration
/// before this one.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class OrganizationAreaWorkspaceMigrationTests(OrganizationsDatabaseFixture database) : IClassFixture<OrganizationsDatabaseFixture>
{
    private const string Migration = "_OrganizationAreaWorkspace";
    private static readonly DateTimeOffset Changed = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    // US-5 · "every project's tree stays": the five areas (a root with two children in one project, a root in another, a root of a
    // deactivated project) are typed — a root is the project's node (type 2), a child a sub-area (type 3) — take the organization and the
    // author of their project, and the items still stand on their areas.
    [Fact]
    public async Task Upgrade_KeepsEveryTree_AsTypedNodes_KeepsEveryItemOnItsArea()
    {
        var legacy = await NewLegacyDatabaseAsync();
        await SeedAsync(legacy);
        var before = await ItemsFingerprintAsync(legacy);

        await MigrateAsync(legacy, Migration);

        Assert.Equal(["5"], await QueryAsync(legacy, "SELECT count(*) FROM areas"));
        Assert.Equal(
            ["3|2"],
            await QueryAsync(legacy, "SELECT concat_ws('|', count(*) FILTER (WHERE type = 2), count(*) FILTER (WHERE type = 3)) FROM areas"));
        Assert.Equal(["0"], await QueryAsync(legacy, "SELECT count(*) FROM areas WHERE type = 2 AND parent_id IS NOT NULL"));
        Assert.Equal(
            ["5"],
            await QueryAsync(
                legacy,
                """
                SELECT count(*) FROM areas JOIN projects ON projects.id = areas.project_id
                WHERE areas.organization_id = projects.organization_id AND areas.created_by = projects.created_by AND areas.created_at = projects.created_at
                """));
        Assert.Equal(["4"], await QueryAsync(legacy, "SELECT count(*) FROM work_items WHERE area_id IS NOT NULL"));
        Assert.Equal(["4"], await QueryAsync(legacy, "SELECT count(*) FROM projects"));
        Assert.Equal(before, await ItemsFingerprintAsync(legacy));
    }

    // US-5 · "the tree is built afresh": after the upgrade a department is created and linked to a team; the shape check holds a
    // department to no project, and a project has one node.
    [Fact]
    public async Task Upgrade_ThenADepartmentIsCreatedAndLinked_TheShapeChecksHold()
    {
        var legacy = await NewLegacyDatabaseAsync();
        var seeded = await SeedAsync(legacy);
        await MigrateAsync(legacy, Migration);
        var area = Guid.CreateVersion7();
        var team = Guid.CreateVersion7();

        await ExecuteAsync(
            legacy,
            """
            INSERT INTO areas (id, organization_id, type, project_id, parent_id, name, normalized_name, created_by, created_at)
            VALUES (@area, @organization, 1, NULL, NULL, 'Платформа', 'ПЛАТФОРМА', 'test', now());
            INSERT INTO teams (id, organization_id, name, normalized_name, status) VALUES (@team, @organization, 'Команда', 'КОМАНДА', 'Active');
            INSERT INTO area_teams (area_id, team_id, organization_id, linked_by, linked_at)
            VALUES (@area, @team, @organization, 'test', now())
            """,
            ("area", area),
            ("team", team),
            ("organization", seeded.Organization));

        Assert.Equal(["1"], await QueryAsync(legacy, "SELECT count(*) FROM area_teams"));

        var departmentWithAProject = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            legacy,
            "INSERT INTO areas (id, organization_id, type, project_id, parent_id, name, normalized_name, created_by, created_at) VALUES (gen_random_uuid(), @organization, 1, @project, NULL, 'Б', 'Б', 'test', now())",
            ("organization", seeded.Organization),
            ("project", seeded.Main)));
        var twoNodesForOneProject = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            legacy,
            "INSERT INTO areas (id, organization_id, type, project_id, parent_id, name, normalized_name, created_by, created_at) VALUES (gen_random_uuid(), @organization, 2, @project, NULL, 'В', 'В', 'test', now())",
            ("organization", seeded.Organization),
            ("project", seeded.Main)));

        Assert.Equal("23514", departmentWithAProject.SqlState);
        Assert.Equal("23505", twoNodesForOneProject.SqlState);
    }

    // US-5 · "a link never crosses an organization": the composite keys refuse a team of another organization.
    [Fact]
    public async Task Upgrade_ALinkToATeamOfAnotherOrganization_IsRefusedByTheKey()
    {
        var legacy = await NewLegacyDatabaseAsync();
        var seeded = await SeedAsync(legacy);
        await MigrateAsync(legacy, Migration);
        var area = Guid.CreateVersion7();
        var team = Guid.CreateVersion7();
        await ExecuteAsync(
            legacy,
            """
            INSERT INTO areas (id, organization_id, type, name, normalized_name, created_by, created_at) VALUES (@area, @organization, 1, 'А', 'А', 'test', now());
            INSERT INTO teams (id, organization_id, name, normalized_name, status) VALUES (@team, @other, 'Команда', 'КОМАНДА', 'Active')
            """,
            ("area", area),
            ("team", team),
            ("organization", seeded.Organization),
            ("other", seeded.OtherOrganization));

        var refused = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            legacy,
            "INSERT INTO area_teams (area_id, team_id, organization_id, linked_by, linked_at) VALUES (@area, @team, @organization, 'test', now())",
            ("area", area),
            ("team", team),
            ("organization", seeded.Organization)));

        Assert.Equal("23503", refused.SqlState);
    }

    // OQ-13 A · a tree deeper than 10 levels stops the upgrade naming the project; nothing changes, and the shortened tree upgrades.
    [Fact]
    public async Task Upgrade_ATreeDeeperThanTen_FailsNamingTheProject_AndNothingChanges()
    {
        var legacy = await NewLegacyDatabaseAsync();
        var seeded = await SeedAsync(legacy);
        var deepest = seeded.MainRoot;

        for (var level = 2; level <= 10; level++)
        {
            deepest = await InsertAreaAsync(legacy, seeded.Main, deepest, $"Глубина {level}");
        }

        var eleventh = await InsertAreaAsync(legacy, seeded.Main, deepest, "Глубина 11");

        var refused = await Assert.ThrowsAnyAsync<Exception>(() => MigrateAsync(legacy, Migration));

        Assert.Contains("Пример (PRM)", refused.ToString());
        Assert.DoesNotContain("Второй (SEC)", refused.ToString());
        Assert.Equal(
            ["0"],
            await QueryAsync(legacy, "SELECT count(*) FROM information_schema.columns WHERE table_name = 'areas' AND column_name = 'type'"));

        await ExecuteAsync(legacy, "DELETE FROM areas WHERE id = @id", ("id", eleventh));
        await MigrateAsync(legacy, Migration);

        Assert.Equal(["14"], await QueryAsync(legacy, "SELECT count(*) FROM areas"));
    }

    // The rollback (Down): the departments and their links go, the project nodes become roots again, the old shape comes back and the
    // migration applies again.
    [Fact]
    public async Task Rollback_RestoresTheOldShape_DropsDepartments_AndAppliesAgain()
    {
        var legacy = await NewLegacyDatabaseAsync();
        var seeded = await SeedAsync(legacy);
        await MigrateAsync(legacy, Migration);
        var department = Guid.CreateVersion7();
        await ExecuteAsync(
            legacy,
            "INSERT INTO areas (id, organization_id, type, name, normalized_name, created_by, created_at) VALUES (@id, @organization, 1, 'Отдел', 'ОТДЕЛ', 'test', now())",
            ("id", department),
            ("organization", seeded.Organization));
        await ExecuteAsync(
            legacy,
            "UPDATE areas SET parent_id = @department WHERE project_id = @project AND parent_id IS NULL",
            ("department", department),
            ("project", seeded.Main));

        await MigrateAsync(legacy, "previous");

        Assert.Equal(
            ["5|false|false"],
            await QueryAsync(
                legacy,
                """
                SELECT concat_ws('|',
                    (SELECT count(*) FROM areas),
                    EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'areas' AND column_name = 'type')::text,
                    EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'area_teams')::text)
                """));
        Assert.Equal(["3"], await QueryAsync(legacy, "SELECT count(*) FROM areas WHERE parent_id IS NULL"));

        await MigrateAsync(legacy, Migration);

        Assert.Equal(["true"], await QueryAsync(legacy, "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'area_teams')::text"));
    }

    // US-5 · "little to convert": a database with no area upgrades and changes no row.
    [Fact]
    public async Task Upgrade_EmptyDatabase_Succeeds()
    {
        var legacy = await NewLegacyDatabaseAsync();

        await MigrateAsync(legacy, Migration);

        Assert.Equal(["0"], await QueryAsync(legacy, "SELECT count(*) FROM areas"));
    }

    // --- Arrangement -------------------------------------------------------------------------------------------------

    private sealed record Seeded(Guid Organization, Guid OtherOrganization, Guid Main, Guid MainRoot, Guid Other);

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

        return new Seeded(organization, otherOrganization, main, rootMain, foreign);
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
