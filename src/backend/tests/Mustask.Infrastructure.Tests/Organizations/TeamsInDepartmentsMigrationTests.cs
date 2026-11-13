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
/// mod-021 US-2 step 1 (doc-016 BR-TD-19, BR-TD-20; ADR-0040): <c>TeamsInDepartments</c> puts every active team into one department and
/// colours every department, team and project, on populated data. The cast is the story's: organization «Ромашка» with the departments
/// «Логистика» and «Финансы», the projects A, B (in «Логистика»), C and the deactivated F (in «Финансы»), D, E and H at the top level, the
/// teams MARS, VENUS, JUPITER, MARS2, PLUTO and the deleted SATURN; organization «Другая» with «Склад», Z and ORION. Every test has a
/// database of its own on the fixture's server, migrated to the migration before this one.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class TeamsInDepartmentsMigrationTests(OrganizationsDatabaseFixture database) : IClassFixture<OrganizationsDatabaseFixture>
{
    private const string Migration = "_TeamsInDepartments";
    private const string Fallback = "Команды без департамента";
    private static readonly DateTimeOffset Epoch = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    private sealed record Legacy(string ConnectionString, Guid Romashka, Guid Other, Dictionary<string, Guid> Ids);

    // US-2 · Step 1, a team with one department link stands in that department; several links: the department holding most of its linked
    // projects (a deactivated one counts, a top-level one does not), a tie by department name; no link: the department of its projects.
    [Fact]
    public async Task Upgrade_PlacesEveryActiveTeam_ByTheRulesOfStepOne()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);

        await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal("Логистика", await DepartmentOfAsync(legacy, "MARS"));
        Assert.Equal("Логистика", await DepartmentOfAsync(legacy, "VENUS"));
        Assert.Equal("Логистика", await DepartmentOfAsync(legacy, "JUPITER"));
        Assert.Equal("Финансы", await DepartmentOfAsync(legacy, "MARS2"));
        Assert.Equal("Склад", await DepartmentOfAsync(legacy, "ORION"));
        Assert.Equal(Fallback, await DepartmentOfAsync(legacy, "PLUTO"));
    }

    // US-2 · "The rule that decided is logged": one row per placed team with the rule, one per created department, one per retired link.
    [Fact]
    public async Task Upgrade_LogsEveryPlacement_EveryCreatedDepartment_AndEveryDroppedLink()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);

        var notices = await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal(
            [
                "JUPITER|Логистика|tie_by_name",
                "MARS|Логистика|one_link",
                "MARS2|Финансы|most_projects",
                "PLUTO|Команды без департамента|fallback",
                "VENUS|Логистика|most_projects",
            ],
            await QueryAsync(
                legacy.ConnectionString,
                "SELECT concat_ws('|', team_name, department_name, rule) FROM team_place_upgrade_log WHERE kind = 'team_placed' AND organization_id = @o ORDER BY team_name",
                ("o", legacy.Romashka)));
        Assert.Equal(["ORION|Склад|most_projects"], await QueryAsync(
            legacy.ConnectionString,
            "SELECT concat_ws('|', team_name, department_name, rule) FROM team_place_upgrade_log WHERE kind = 'team_placed' AND organization_id = @o",
            ("o", legacy.Other)));
        Assert.Equal([Fallback], await QueryAsync(
            legacy.ConnectionString, "SELECT department_name FROM team_place_upgrade_log WHERE kind = 'department_created'"));

        // MARS 1 link, VENUS 2, JUPITER 2, MARS2 2: seven department-team links were retired, each named.
        Assert.Equal(["7"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM team_place_upgrade_log WHERE kind = 'link_dropped'"));
        Assert.Contains(notices, notice => notice.Contains("5 teams placed, 1 departments created", StringComparison.Ordinal));
    }

    // US-2 · "an organization with no such team gains no such department", and a deleted team gets no place.
    [Fact]
    public async Task Upgrade_CreatesTheFallbackOnlyWhereATeamNeedsIt_AndGivesADeletedTeamNoPlace()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);

        await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal(["1"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM areas WHERE type = 1 AND name = @n AND organization_id = @o", ("n", Fallback), ("o", legacy.Romashka)));
        Assert.Equal(["0"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM areas WHERE type = 1 AND name = @n AND organization_id = @o", ("n", Fallback), ("o", legacy.Other)));
        Assert.Equal(["NULL"], await QueryAsync(legacy.ConnectionString, "SELECT department_id FROM teams WHERE id = @t", ("t", legacy.Ids["SATURN"])));
        Assert.Equal(["0"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM team_place_upgrade_log WHERE team_name = 'SATURN'"));
    }

    // US-2 · "if «Ромашка» already has a department «Команды без департамента», PLUTO is placed in it and none is created".
    [Fact]
    public async Task Upgrade_ReusesAnExistingFallbackDepartment()
    {
        var legacy = await SeedLegacyAsync(existingFallback: true);

        await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal(["1"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM areas WHERE type = 1 AND name = @n AND organization_id = @o", ("n", Fallback), ("o", legacy.Romashka)));
        Assert.Equal(legacy.Ids["Fallback"].ToString(), Assert.Single(await QueryAsync(legacy.ConnectionString, "SELECT department_id FROM teams WHERE id = @t", ("t", legacy.Ids["PLUTO"]))));
        Assert.Equal(["0"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM team_place_upgrade_log WHERE kind = 'department_created'"));
    }

    // US-2 · nothing is lost: the team, its project links, its other table rows and the projects are all still there.
    [Fact]
    public async Task Upgrade_KeepsEveryTeamProjectAndProjectLink_AndDropsTheDepartmentLinkTable()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);

        await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal(["7|8|14"], await QueryAsync(legacy.ConnectionString, "SELECT concat_ws('|', (SELECT count(*) FROM teams), (SELECT count(*) FROM projects), (SELECT count(*) FROM project_teams))"));
        Assert.Equal([""], await QueryAsync(legacy.ConnectionString, "SELECT coalesce(to_regclass('area_teams')::text, '')"));
    }

    // BR-TD-20 · every department, team and project has a palette colour, chosen in its scope by the max-hue-distance rule: the
    // upgrade colours in the order of creation (a team by the time its id carries), and the SQL function equals the domain one.
    [Fact]
    public async Task Upgrade_ColoursEveryObject_ByTheDomainRule_InTheScopeOfEachKind()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);

        await MigrateAsync(legacy.ConnectionString, null);

        // The story's examples: a blue department gives its teams orange, magenta, green.
        Assert.Equal("blue", await ColorOfAsync(legacy, "areas", "Логистика"));
        Assert.Equal("orange", await ColorOfAsync(legacy, "areas", "Финансы"));
        Assert.Equal("magenta", await ColorOfAsync(legacy, "areas", Fallback));
        Assert.Equal(["orange", "magenta", "green"], await ColorsOfAsync(legacy, "teams", "MARS", "VENUS", "JUPITER"));
        Assert.Equal(["orange", "magenta"], await ColorsOfAsync(legacy, "projects", "A", "B"));

        // The rest follows the same function: a team of a department starts from the department's colour.
        Assert.Equal(ColorPalette.Choose(["orange"]), await ColorOfAsync(legacy, "teams", "MARS2"));
        Assert.Equal(ColorPalette.Choose(["magenta"]), await ColorOfAsync(legacy, "teams", "PLUTO"));
        Assert.Equal(ColorPalette.Choose(["blue"]), await ColorOfAsync(legacy, "teams", "ORION"));

        // The top-level projects are a scope of their own, with no department colour.
        Assert.Equal(["blue", "orange", "magenta"], await ColorsOfAsync(legacy, "projects", "D", "E", "H"));

        // C is first in «Финансы»; F, deactivated, is coloured by the rule against C but is not counted afterwards.
        Assert.Equal(ColorPalette.Choose(["orange"]), await ColorOfAsync(legacy, "projects", "C"));
        Assert.Equal(ColorPalette.Choose(["orange", ColorPalette.Choose(["orange"])]), await ColorOfAsync(legacy, "projects", "F"));

        Assert.Equal(["0"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM teams WHERE color IS NULL OR color = ''"));
        Assert.Equal(["0"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM areas WHERE type = 1 AND color IS NULL"));
        Assert.Equal(["0"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM areas WHERE type <> 1 AND color IS NOT NULL"));
    }

    // The upgrade runs once; migrating again is a no-op and changes no colour and no place.
    [Fact]
    public async Task Upgrade_RunAgain_ChangesNoColourAndNoPlace()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);
        await MigrateAsync(legacy.ConnectionString, null);
        var before = await FingerprintAsync(legacy.ConnectionString);

        await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal(before, await FingerprintAsync(legacy.ConnectionString));
    }

    // The rules after the upgrade: an active team needs a department of its own organization, and a colour of the palette.
    [Fact]
    public async Task Upgrade_LeavesTheConstraintsOfThePlace()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);
        await MigrateAsync(legacy.ConnectionString, null);

        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            legacy.ConnectionString,
            "INSERT INTO teams (id, organization_id, name, normalized_name, status, color) VALUES (gen_random_uuid(), @o, 'X', 'X', 'Active', 'blue')",
            ("o", legacy.Romashka)));
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            legacy.ConnectionString,
            "INSERT INTO teams (id, organization_id, name, normalized_name, status, color, department_id) VALUES (gen_random_uuid(), @o, 'X', 'X', 'Active', 'blue', @d)",
            ("o", legacy.Romashka),
            ("d", legacy.Ids["Склад"])));
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            legacy.ConnectionString,
            "INSERT INTO teams (id, organization_id, name, normalized_name, status, color, department_id) VALUES (gen_random_uuid(), @o, 'X', 'X', 'Active', 'chartreuse', @d)",
            ("o", legacy.Romashka),
            ("d", legacy.Ids["Логистика"])));
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            legacy.ConnectionString, "DELETE FROM areas WHERE id = @d", ("d", legacy.Ids["Логистика"])));
    }

    // All or nothing: a top-level project node with the fallback's name that PLUTO would collide with stops the whole migration,
    // and the schema stays as it was.
    [Fact]
    public async Task Upgrade_ThatCannotPlaceATeam_FailsAsAWhole_AndChangesNothing()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);
        await ExecuteAsync(
            legacy.ConnectionString,
            "UPDATE areas SET name = @n, normalized_name = upper(@n) WHERE project_id = @p AND type = 2",
            ("n", Fallback),
            ("p", legacy.Ids["D"]));

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => MigrateAsync(legacy.ConnectionString, null));

        Assert.Contains("mod-021", failure.ToString(), StringComparison.Ordinal);
        Assert.Equal(["area_teams"], await QueryAsync(legacy.ConnectionString, "SELECT to_regclass('area_teams')::text"));
        Assert.Equal(["0"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM information_schema.columns WHERE table_name = 'teams' AND column_name IN ('color', 'department_id')"));
        Assert.Equal(["7"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM area_teams"));
    }

    // Down: the previous shape returns with one link per placed team, and the new columns and the log are gone.
    [Fact]
    public async Task Down_RestoresTheLinkTable_WithOneLinkPerPlacedTeam()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);
        await MigrateAsync(legacy.ConnectionString, null);

        await MigrateAsync(legacy.ConnectionString, "previous");

        Assert.Equal(["6"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM area_teams"));
        Assert.Equal(["0"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM information_schema.columns WHERE table_name = 'teams' AND column_name IN ('color', 'department_id')"));
        Assert.Equal(["0"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM information_schema.columns WHERE table_name IN ('projects', 'areas') AND column_name = 'color'"));
        Assert.Equal([""], await QueryAsync(legacy.ConnectionString, "SELECT coalesce(to_regclass('team_place_upgrade_log')::text, '')"));
        Assert.Equal(["7"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM teams"));

        // And it goes up again on the data it left.
        await MigrateAsync(legacy.ConnectionString, null);
        Assert.Equal("Логистика", await DepartmentOfAsync(legacy, "MARS"));
    }

    // An organization with nothing in it migrates and logs nothing.
    [Fact]
    public async Task Upgrade_OfAnEmptyDatabase_Succeeds()
    {
        var connectionString = await NewLegacyDatabaseAsync();

        await MigrateAsync(connectionString, null);

        Assert.Equal(["0"], await QueryAsync(connectionString, "SELECT count(*) FROM team_place_upgrade_log"));
    }

    // --- The cast ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// Organization «Ромашка» and «Другая» in the shape the previous migration left, as the story's table gives them. A team's id is built
    /// so that its creation order is the order of the cast (the upgrade reads the order from the id's time prefix).
    /// </summary>
    private async Task<Legacy> SeedLegacyAsync(bool existingFallback)
    {
        var cs = await NewLegacyDatabaseAsync();
        var romashka = await CreateOrganizationAsync(cs, "Ромашка");
        var other = await CreateOrganizationAsync(cs, "Другая");
        var ids = new Dictionary<string, Guid>();

        var at = 0;
        ids["Логистика"] = await InsertDepartmentAsync(cs, romashka, "Логистика", at++);
        ids["Финансы"] = await InsertDepartmentAsync(cs, romashka, "Финансы", at++);
        ids["Склад"] = await InsertDepartmentAsync(cs, other, "Склад", at++);

        if (existingFallback)
        {
            ids["Fallback"] = await InsertDepartmentAsync(cs, romashka, Fallback, at++);
        }

        var inLogistics = ids["Логистика"];
        var inFinance = ids["Финансы"];

        foreach (var (name, parent, status, minute) in new (string, Guid?, string, int)[]
        {
            ("A", inLogistics, "Active", 1), ("B", inLogistics, "Active", 2), ("C", inFinance, "Active", 3), ("F", inFinance, "Deactivated", 4),
            ("D", null, "Active", 5), ("E", null, "Active", 6), ("H", null, "Active", 7),
        })
        {
            ids[name] = await InsertProjectAsync(cs, romashka, name, parent, status, minute);
        }

        ids["Z"] = await InsertProjectAsync(cs, other, "Z", ids["Склад"], "Active", 8);

        var n = 1;
        foreach (var name in new[] { "MARS", "VENUS", "JUPITER", "MARS2", "PLUTO" })
        {
            ids[name] = await InsertTeamAsync(cs, romashka, name, "Active", n++);
        }

        ids["SATURN"] = await InsertTeamAsync(cs, romashka, "SATURN", "Deleted", n++);
        ids["ORION"] = await InsertTeamAsync(cs, other, "ORION", "Active", n++);

        // Department links: MARS one; VENUS, JUPITER and MARS2 two; PLUTO none; SATURN, deleted earlier, none; ORION none.
        foreach (var (team, department) in new[]
        {
            ("MARS", "Логистика"),
            ("VENUS", "Логистика"), ("VENUS", "Финансы"),
            ("JUPITER", "Логистика"), ("JUPITER", "Финансы"),
            ("MARS2", "Логистика"), ("MARS2", "Финансы"),
        })
        {
            await ExecuteAsync(
                cs,
                "INSERT INTO area_teams (area_id, team_id, organization_id, linked_by, linked_at) VALUES (@a, @t, @o, 'legacy-0', @at)",
                ("a", ids[department]),
                ("t", ids[team]),
                ("o", romashka),
                ("at", Epoch));
        }

        // Project links: 14 in all (MARS: A B C D; VENUS: A B H; JUPITER: A C D; MARS2: F H; PLUTO: D; ORION: Z).
        foreach (var (team, project) in new[]
        {
            ("MARS", "A"), ("MARS", "B"), ("MARS", "C"), ("MARS", "D"),
            ("VENUS", "A"), ("VENUS", "B"), ("VENUS", "H"),
            ("JUPITER", "A"), ("JUPITER", "C"), ("JUPITER", "D"),
            ("MARS2", "F"), ("MARS2", "H"),
            ("PLUTO", "D"),
            ("ORION", "Z"),
        })
        {
            await ExecuteAsync(
                cs,
                "INSERT INTO project_teams (project_id, team_id, organization_id, linked_by, linked_at) VALUES (@p, @t, @o, 'legacy-0', @at)",
                ("p", ids[project]),
                ("t", ids[team]),
                ("o", team == "ORION" ? other : romashka),
                ("at", Epoch));
        }

        return new Legacy(cs, romashka, other, ids);
    }

    private static async Task<Guid> InsertDepartmentAsync(string cs, Guid organization, string name, int minute)
    {
        var id = Guid.CreateVersion7();

        await ExecuteAsync(
            cs,
            """
            INSERT INTO areas (id, organization_id, parent_id, type, project_id, name, normalized_name, inherits_access, created_by, created_at)
            VALUES (@id, @o, NULL, 1, NULL, @name, upper(@name), true, 'legacy-0', @at)
            """,
            ("id", id),
            ("o", organization),
            ("name", name),
            ("at", Epoch.AddMinutes(minute)));

        return id;
    }

    private static async Task<Guid> InsertProjectAsync(string cs, Guid organization, string name, Guid? department, string status, int minute)
    {
        var project = Guid.CreateVersion7();

        await ExecuteAsync(
            cs,
            """
            INSERT INTO projects (id, organization_id, name, normalized_name, key, status, created_at, created_by)
            VALUES (@p, @o, @name, upper(@name), @key, @status, @at, 'legacy-0');
            INSERT INTO areas (id, organization_id, parent_id, type, project_id, name, normalized_name, inherits_access, created_by, created_at)
            VALUES (gen_random_uuid(), @o, @dept, 2, @p, @name, upper(@name), true, 'legacy-0', @at)
            """,
            ("p", project),
            ("o", organization),
            ("name", name),
            ("key", name.Length == 1 ? name + "PR" : name),
            ("status", status),
            ("dept", (object?)department ?? DBNull.Value),
            ("at", Epoch.AddMinutes(minute)));

        return project;
    }

    /// <summary>A team whose id carries the time prefix <paramref name="order"/>: a later team has a later id, as a version 7 id does.</summary>
    private static async Task<Guid> InsertTeamAsync(string cs, Guid organization, string name, string status, int order)
    {
        var id = Guid.Parse($"{order:x8}-0000-7000-8000-000000000001");

        await ExecuteAsync(
            cs,
            "INSERT INTO teams (id, organization_id, name, normalized_name, status) VALUES (@id, @o, @name, upper(@name), @status)",
            ("id", id),
            ("o", organization),
            ("name", name),
            ("status", status));

        return id;
    }

    // --- Reading back ------------------------------------------------------------------------------------------------

    private static async Task<string> DepartmentOfAsync(Legacy legacy, string team) =>
        Assert.Single(await QueryAsync(
            legacy.ConnectionString,
            "SELECT a.name FROM teams t JOIN areas a ON a.id = t.department_id WHERE t.id = @t",
            ("t", legacy.Ids[team])));

    private static async Task<string> ColorOfAsync(Legacy legacy, string table, string name) =>
        Assert.Single(await QueryAsync(
            legacy.ConnectionString,
            table == "areas"
                ? "SELECT color FROM areas WHERE type = 1 AND name = @n AND organization_id = @o"
                : $"SELECT color FROM {table} WHERE name = @n AND organization_id = @o",
            ("n", name),
            ("o", name == "ORION" ? legacy.Other : legacy.Romashka)));

    private static async Task<string[]> ColorsOfAsync(Legacy legacy, string table, params string[] names)
    {
        var colours = new List<string>();

        foreach (var name in names)
        {
            colours.Add(await ColorOfAsync(legacy, table, name));
        }

        return [.. colours];
    }

    private static async Task<string> FingerprintAsync(string cs) =>
        Assert.Single(await QueryAsync(
            cs,
            """
            SELECT md5(
                coalesce((SELECT string_agg(concat_ws('|', id, color, department_id), ';' ORDER BY id) FROM teams), '') ||
                coalesce((SELECT string_agg(concat_ws('|', id, color), ';' ORDER BY id) FROM projects), '') ||
                coalesce((SELECT string_agg(concat_ws('|', id, color), ';' ORDER BY id) FROM areas), ''))
            """));

    // --- The databases -----------------------------------------------------------------------------------------------

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

    /// <summary>Migrates to the latest migration (<c>null</c>) or to the one before this migration, found by name; returns the notices of mod-021.</summary>
    private static async Task<List<string>> MigrateAsync(string connectionString, string? target)
    {
        await using var context = new MustaskDbContext(new DbContextOptionsBuilder<MustaskDbContext>().UseNpgsql(connectionString).Options);
        var notices = new List<string>();

        ((NpgsqlConnection)context.Database.GetDbConnection()).Notice += (_, e) =>
        {
            if (e.Notice.MessageText.StartsWith("mod-021:", StringComparison.Ordinal))
            {
                notices.Add(e.Notice.MessageText);
            }
        };

        var migrations = context.Database.GetMigrations().ToList();
        var id = target is null
            ? null
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
