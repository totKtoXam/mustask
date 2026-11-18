using Mustask.Application.Abstractions;
using Mustask.Application.Features.Process;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// mod-022 US-2 steps 2 and 3 (doc-016 BR-TD-19, BR-TD-20; ADR-0040): <c>ProjectsInDepartments</c> puts every top-level project into a
/// department and cuts the project–team links that join two departments, on populated data. The cast: organization «Ромашка» with the
/// departments «Логистика» and «Финансы»; teams MARS, VENUS (Логистика), JUPITER (Финансы) and the deleted SATURN; projects A and B (in
/// «Логистика»), C (in «Финансы»), and at the top level D, E, F (deactivated), H; organization «Другая» with «Склад», the team ORION and the
/// top-level project Z. Every test has a database of its own, migrated to the migration before this one.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class ProjectsInDepartmentsMigrationTests(OrganizationsDatabaseFixture database) : IClassFixture<OrganizationsDatabaseFixture>
{
    private const string Migration = "_ProjectsInDepartments";
    private const string Fallback = "Проекты без департамента";
    private static readonly DateTimeOffset Epoch = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    private sealed record Legacy(string ConnectionString, Guid Romashka, Guid Other, Dictionary<string, Guid> Ids);

    // US-2 · step 2: a top-level project takes the department holding most of its linked teams (a deactivated one too), a tie goes by
    // department name, no linked team goes to «Проекты без департамента»; a project already in a department stays.
    [Fact]
    public async Task Upgrade_PlacesEveryTopLevelProject_ByTheRulesOfStepTwo()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);

        await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal("Логистика", await DepartmentOfAsync(legacy, "D"));
        Assert.Equal("Логистика", await DepartmentOfAsync(legacy, "E"));
        Assert.Equal("Финансы", await DepartmentOfAsync(legacy, "F"));
        Assert.Equal(Fallback, await DepartmentOfAsync(legacy, "H"));
        Assert.Equal("Склад", await DepartmentOfAsync(legacy, "Z"));
        Assert.Equal("Логистика", await DepartmentOfAsync(legacy, "A"));
        Assert.Equal("Финансы", await DepartmentOfAsync(legacy, "C"));
        Assert.Equal(["0"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM areas WHERE type = 2 AND parent_id IS NULL"));
    }

    // US-2 · "the rule that decided is logged": one row per placed project with the rule, one per created department, one per cut link
    // with the number of items made «Без команды»; notices name each and sum up per organization.
    [Fact]
    public async Task Upgrade_LogsEveryPlacement_EveryCreatedDepartment_AndEveryCutLink()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);

        var notices = await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal(
            ["D|Логистика|most_teams", "E|Логистика|tie_by_name", "F|Финансы|most_teams", "H|Проекты без департамента|fallback"],
            await QueryAsync(
                legacy.ConnectionString,
                "SELECT concat_ws('|', project_name, department_name, rule) FROM project_place_upgrade_log WHERE kind = 'project_placed' AND organization_id = @o ORDER BY project_name",
                ("o", legacy.Romashka)));
        Assert.Equal(["Z|Склад|most_teams"], await QueryAsync(
            legacy.ConnectionString,
            "SELECT concat_ws('|', project_name, department_name, rule) FROM project_place_upgrade_log WHERE kind = 'project_placed' AND organization_id = @o",
            ("o", legacy.Other)));
        Assert.Equal([Fallback], await QueryAsync(
            legacy.ConnectionString, "SELECT department_name FROM project_place_upgrade_log WHERE kind = 'department_created'"));

        // JUPITER (Финансы) is across from A, D and E (all in «Логистика» now); D holds three of its items.
        Assert.Equal(
            ["A|JUPITER|0", "D|JUPITER|3", "E|JUPITER|0"],
            await QueryAsync(
                legacy.ConnectionString,
                "SELECT concat_ws('|', project_name, team_name, item_count) FROM project_place_upgrade_log WHERE kind = 'link_cut' ORDER BY project_name, team_name"));
        Assert.Contains(notices, notice => notice.Contains("1 departments created, 4 projects placed, 3 links cut, 3 items made without a team", StringComparison.Ordinal));
        Assert.Contains(notices, notice => notice.Contains("link cut: team JUPITER from project D (3 items", StringComparison.Ordinal));
    }

    // US-2 · "if «Ромашка» already has a department «Проекты без департамента», H is placed in it and none is created".
    [Fact]
    public async Task Upgrade_ReusesAnExistingFallbackDepartment()
    {
        var legacy = await SeedLegacyAsync(existingFallback: true);

        await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal(["1"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM areas WHERE type = 1 AND name = @n AND organization_id = @o", ("n", Fallback), ("o", legacy.Romashka)));
        Assert.Equal(legacy.Ids["Fallback"].ToString(), Assert.Single(await QueryAsync(legacy.ConnectionString, "SELECT parent_id FROM areas WHERE project_id = @p AND type = 2", ("p", legacy.Ids["H"]))));
        Assert.Equal(["0"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM project_place_upgrade_log WHERE kind = 'department_created'"));
    }

    // US-2 · an organization with no top-level project gains no such department; the created one is coloured by the BR-TD-20 rule.
    [Fact]
    public async Task Upgrade_CreatesTheFallbackOnlyWhereAProjectNeedsIt_AndColoursIt()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);

        await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal(["1"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM areas WHERE type = 1 AND name = @n AND organization_id = @o", ("n", Fallback), ("o", legacy.Romashka)));
        Assert.Equal(["0"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM areas WHERE type = 1 AND name = @n AND organization_id = @o", ("n", Fallback), ("o", legacy.Other)));

        // «Логистика» is blue and «Финансы» orange in the cast; the created department takes the colour the rule chooses against them.
        Assert.Equal(
            ColorPalette.Choose(["blue", "orange"]),
            Assert.Single(await QueryAsync(legacy.ConnectionString, "SELECT color FROM areas WHERE type = 1 AND name = @n AND organization_id = @o", ("n", Fallback), ("o", legacy.Romashka))));
    }

    // US-2 · step 3: a link whose ends stand in different departments is cut like an unlink to «Без команды»; links inside one
    // department stay; the project's default team is cleared if it named the team; the team's items lose owner, sprint and backlog place
    // in any state, and no item's «Изменён» moves.
    [Fact]
    public async Task Upgrade_CutsTheLinksAcrossDepartments_LikeAnUnlinkToNoTeam()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);
        var before = await QueryAsync(legacy.ConnectionString, "SELECT changed_at::text FROM work_items WHERE project_id = @p ORDER BY number", ("p", legacy.Ids["D"]));

        await MigrateAsync(legacy.ConnectionString, null);

        // Kept: MARS–A, VENUS–A... MARS and VENUS stand in «Логистика» with A, B, D, E; JUPITER with C, F. Cut: JUPITER with A, D, E.
        Assert.Equal(
            ["A|MARS", "A|VENUS", "B|MARS", "B|VENUS", "C|JUPITER", "D|MARS", "D|VENUS", "E|MARS", "F|JUPITER", "Z|ORION"],
            await QueryAsync(
                legacy.ConnectionString,
                "SELECT concat_ws('|', p.name, t.name) FROM project_teams l JOIN projects p ON p.id = l.project_id JOIN teams t ON t.id = l.team_id ORDER BY p.name, t.name"));
        Assert.Equal(["NULL"], await QueryAsync(legacy.ConnectionString, "SELECT default_team_id FROM projects WHERE id = @p", ("p", legacy.Ids["D"])));
        Assert.Equal(legacy.Ids["MARS"].ToString(), Assert.Single(await QueryAsync(legacy.ConnectionString, "SELECT default_team_id FROM projects WHERE id = @p", ("p", legacy.Ids["E"]))));

        // D's three items of JUPITER (one deleted, one in a sprint, one placed) are «Без команды»; MARS's item stays; E's item of MARS stays.
        Assert.Equal(["0|1"], await QueryAsync(
            legacy.ConnectionString,
            "SELECT concat_ws('|', count(*) FILTER (WHERE owning_team_id = @j), count(*) FILTER (WHERE owning_team_id = @m)) FROM work_items WHERE project_id = @p",
            ("p", legacy.Ids["D"]), ("j", legacy.Ids["JUPITER"]), ("m", legacy.Ids["MARS"])));
        Assert.Equal(["3"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM work_items WHERE project_id = @p AND owning_team_id IS NULL AND sprint_id IS NULL", ("p", legacy.Ids["D"])));
        Assert.Equal(["0"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM backlog_places WHERE team_id = @j", ("j", legacy.Ids["JUPITER"])));
        Assert.Equal(["1"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM backlog_places WHERE team_id = @m", ("m", legacy.Ids["MARS"])));
        Assert.Equal(before, await QueryAsync(legacy.ConnectionString, "SELECT changed_at::text FROM work_items WHERE project_id = @p ORDER BY number", ("p", legacy.Ids["D"])));
        Assert.Equal(["4"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM work_items WHERE project_id = @p", ("p", legacy.Ids["D"])));
    }

    // Nothing is lost: every project, team and the links that stand, the colours and the names of the projects.
    [Fact]
    public async Task Upgrade_KeepsEveryProjectAndTeam_AndTheColourOfEveryProject()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);
        var colours = await QueryAsync(legacy.ConnectionString, "SELECT concat_ws('|', name, color) FROM projects ORDER BY name");

        await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal(["8|5"], await QueryAsync(legacy.ConnectionString, "SELECT concat_ws('|', (SELECT count(*) FROM projects), (SELECT count(*) FROM teams))"));
        Assert.Equal(colours, await QueryAsync(legacy.ConnectionString, "SELECT concat_ws('|', name, color) FROM projects ORDER BY name"));
    }

    // The upgrade is idempotent: running its script again on the migrated database changes and logs nothing.
    [Fact]
    public async Task Upgrade_RunAgain_ChangesAndLogsNothing()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);
        await MigrateAsync(legacy.ConnectionString, null);
        var before = await FingerprintAsync(legacy.ConnectionString);

        await ExecuteAsync(legacy.ConnectionString, ProjectsInDepartmentsUpgrade.Script);

        Assert.Equal(before, await FingerprintAsync(legacy.ConnectionString));
    }

    // After the upgrade the database refuses a project node with no parent, and an organization-less cast migrates.
    [Fact]
    public async Task Upgrade_LeavesTheShapeCheck_ThatRefusesAProjectAtTheTop()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);
        await MigrateAsync(legacy.ConnectionString, null);

        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            legacy.ConnectionString, "UPDATE areas SET parent_id = NULL WHERE project_id = @p AND type = 2", ("p", legacy.Ids["A"])));
    }

    // The upgrade never aborts on a name clash: a top-level node that holds the fallback's name sends the fallback to the first free
    // «(N)» name, the projects are placed there, the log and the notice carry the chosen name, and a second run creates nothing.
    [Fact]
    public async Task Upgrade_WhenATopLevelNodeHoldsTheFallbackName_NamesItWithTheFirstFreeSuffix()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);
        await ExecuteAsync(
            legacy.ConnectionString,
            "UPDATE areas SET name = @n, normalized_name = upper(@n) WHERE project_id = @p AND type = 2",
            ("n", Fallback),
            ("p", legacy.Ids["H"]));

        var notices = await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal(Fallback + " (2)", await DepartmentOfAsync(legacy, "H"));
        Assert.Equal(["1"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM areas WHERE type = 1 AND name = @n AND organization_id = @o", ("n", Fallback + " (2)"), ("o", legacy.Romashka)));
        Assert.Equal([Fallback + " (2)"], await QueryAsync(legacy.ConnectionString, "SELECT department_name FROM project_place_upgrade_log WHERE kind = 'department_created'"));
        Assert.Contains(notices, n => n.Contains("department created: " + Fallback + " (2)", StringComparison.Ordinal));

        var before = await FingerprintAsync(legacy.ConnectionString);
        await ExecuteAsync(legacy.ConnectionString, ProjectsInDepartmentsUpgrade.Script);
        Assert.Equal(before, await FingerprintAsync(legacy.ConnectionString));
    }

    [Fact]
    public async Task Upgrade_WhenTheFirstSuffixIsTakenToo_NamesTheFallbackWithTheNextOne()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);
        await ExecuteAsync(
            legacy.ConnectionString,
            "UPDATE areas SET name = @n, normalized_name = upper(@n) WHERE project_id = @p AND type = 2",
            ("n", Fallback),
            ("p", legacy.Ids["H"]));
        await InsertProjectAsync(legacy.ConnectionString, legacy.Romashka, Fallback + " (2)", null, "Active", 50, "blue");

        await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal(Fallback + " (3)", await DepartmentOfAsync(legacy, "H"));
        Assert.Equal([Fallback + " (3)"], await QueryAsync(legacy.ConnectionString, "SELECT department_name FROM project_place_upgrade_log WHERE kind = 'department_created'"));

        var before = await FingerprintAsync(legacy.ConnectionString);
        await ExecuteAsync(legacy.ConnectionString, ProjectsInDepartmentsUpgrade.Script);
        Assert.Equal(before, await FingerprintAsync(legacy.ConnectionString));
    }

    // Down: the check relaxes and the log goes; the placement and the cuts stay (irreversible); it goes up again on what it left.
    [Fact]
    public async Task Down_RelaxesTheCheck_DropsTheLog_AndGoesUpAgain()
    {
        var legacy = await SeedLegacyAsync(existingFallback: false);
        await MigrateAsync(legacy.ConnectionString, null);

        await MigrateAsync(legacy.ConnectionString, "previous");

        Assert.Equal([""], await QueryAsync(legacy.ConnectionString, "SELECT coalesce(to_regclass('project_place_upgrade_log')::text, '')"));
        await ExecuteAsync(legacy.ConnectionString, "UPDATE areas SET parent_id = NULL WHERE project_id = @p AND type = 2", ("p", legacy.Ids["A"]));
        Assert.Equal(["8"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM projects"));
        Assert.Equal(["10"], await QueryAsync(legacy.ConnectionString, "SELECT count(*) FROM project_teams"));

        await MigrateAsync(legacy.ConnectionString, null);

        Assert.Equal("Логистика", await DepartmentOfAsync(legacy, "A"));
    }

    // An empty database and an organization with nothing to place migrate and log nothing.
    [Fact]
    public async Task Upgrade_OfAnEmptyDatabase_Succeeds()
    {
        var connectionString = await NewLegacyDatabaseAsync();

        await MigrateAsync(connectionString, null);

        Assert.Equal(["0"], await QueryAsync(connectionString, "SELECT count(*) FROM project_place_upgrade_log"));
    }

    // US-2 · a tie goes by department NAME, not by creation order: the department created first has the later name.
    [Fact]
    public async Task Upgrade_Tie_GoesByDepartmentName_NotByCreationOrder()
    {
        var cs = await NewLegacyDatabaseAsync();
        var organization = await CreateOrganizationAsync(cs, "Тай");
        var yabloko = await InsertDepartmentAsync(cs, organization, "Яблоко", 1, "blue");
        var audit = await InsertDepartmentAsync(cs, organization, "Аудит", 2, "orange");
        var teamY = await InsertTeamAsync(cs, organization, "TY", yabloko, 1, "blue");
        var teamA = await InsertTeamAsync(cs, organization, "TA", audit, 2, "orange");
        var project = await InsertProjectAsync(cs, organization, "PX", null, "Active", 3, "blue");
        await LinkAsync(cs, organization, project, teamY);
        await LinkAsync(cs, organization, project, teamA);

        await MigrateAsync(cs, null);

        Assert.Equal(["Аудит"], await QueryAsync(cs, "SELECT d.name FROM areas n JOIN areas d ON d.id = n.parent_id WHERE n.project_id = @p AND n.type = 2", ("p", project)));
        Assert.Equal(["TA"], await QueryAsync(cs, "SELECT t.name FROM project_teams l JOIN teams t ON t.id = l.team_id WHERE l.project_id = @p", ("p", project)));
    }

    // Scale: the cut of 5 000 items per organization completes (feat-028 NFR) and logs the number.
    [Fact]
    public async Task Upgrade_CutOfFiveThousandItems_Completes_AndCountsThem()
    {
        var cs = await NewLegacyDatabaseAsync();
        var organization = await CreateOrganizationAsync(cs, "Большая");
        var logistics = await InsertDepartmentAsync(cs, organization, "Логистика", 1, "blue");
        var finance = await InsertDepartmentAsync(cs, organization, "Финансы", 2, "orange");
        var mars = await InsertTeamAsync(cs, organization, "MARS", logistics, 1, "blue");
        var jupiter = await InsertTeamAsync(cs, organization, "JUPITER", finance, 2, "orange");
        var big = await InsertProjectAsync(cs, organization, "BIG", null, "Active", 3, "blue");
        await LinkAsync(cs, organization, big, mars);
        await LinkAsync(cs, organization, big, jupiter);
        await InsertItemsAsync(cs, big, jupiter, 5000);

        await MigrateAsync(cs, null);

        Assert.Equal(["5000"], await QueryAsync(cs, "SELECT item_count FROM project_place_upgrade_log WHERE kind = 'link_cut'"));
        Assert.Equal(["5000"], await QueryAsync(cs, "SELECT count(*) FROM work_items WHERE project_id = @p AND owning_team_id IS NULL", ("p", big)));
    }

    // --- The cast ----------------------------------------------------------------------------------------------------

    private async Task<Legacy> SeedLegacyAsync(bool existingFallback)
    {
        var cs = await NewLegacyDatabaseAsync();
        var romashka = await CreateOrganizationAsync(cs, "Ромашка");
        var other = await CreateOrganizationAsync(cs, "Другая");
        var ids = new Dictionary<string, Guid>();

        ids["Логистика"] = await InsertDepartmentAsync(cs, romashka, "Логистика", 0, "blue");
        ids["Финансы"] = await InsertDepartmentAsync(cs, romashka, "Финансы", 1, "orange");
        ids["Склад"] = await InsertDepartmentAsync(cs, other, "Склад", 2, "blue");

        if (existingFallback)
        {
            ids["Fallback"] = await InsertDepartmentAsync(cs, romashka, Fallback, 3, "green");
        }

        var n = 1;
        ids["MARS"] = await InsertTeamAsync(cs, romashka, "MARS", ids["Логистика"], n++, "orange");
        ids["VENUS"] = await InsertTeamAsync(cs, romashka, "VENUS", ids["Логистика"], n++, "magenta");
        ids["JUPITER"] = await InsertTeamAsync(cs, romashka, "JUPITER", ids["Финансы"], n++, "blue");
        ids["SATURN"] = await InsertTeamAsync(cs, romashka, "SATURN", null, n++, "blue", deleted: true);
        ids["ORION"] = await InsertTeamAsync(cs, other, "ORION", ids["Склад"], n++, "orange");

        var minute = 10;
        foreach (var (name, parent, status, colour) in new (string, string?, string, string)[]
        {
            ("A", "Логистика", "Active", "orange"), ("B", "Логистика", "Active", "magenta"), ("C", "Финансы", "Active", "blue"),
            ("D", null, "Active", "blue"), ("E", null, "Active", "orange"), ("F", null, "Deactivated", "green"), ("H", null, "Active", "purple"),
        })
        {
            ids[name] = await InsertProjectAsync(cs, romashka, name, parent is null ? null : ids[parent], status, minute++, colour);
        }

        ids["Z"] = await InsertProjectAsync(cs, other, "Z", null, "Active", minute, "blue");

        // 13 links: A: MARS, VENUS, JUPITER; B: MARS, VENUS; C: JUPITER; D: MARS, VENUS, JUPITER; E: MARS, JUPITER; F: JUPITER; Z: ORION.
        foreach (var (project, team) in new[]
        {
            ("A", "MARS"), ("A", "VENUS"), ("A", "JUPITER"), ("B", "MARS"), ("B", "VENUS"), ("C", "JUPITER"),
            ("D", "MARS"), ("D", "VENUS"), ("D", "JUPITER"), ("E", "MARS"), ("E", "JUPITER"), ("F", "JUPITER"), ("Z", "ORION"),
        })
        {
            await LinkAsync(cs, project == "Z" ? other : romashka, ids[project], ids[team]);
        }

        // E names MARS as its default team (it stays), D names JUPITER (cut, so cleared).
        await ExecuteAsync(cs, "UPDATE projects SET default_team_id = @t WHERE id = @p", ("t", ids["JUPITER"]), ("p", ids["D"]));
        await ExecuteAsync(cs, "UPDATE projects SET default_team_id = @t WHERE id = @p", ("t", ids["MARS"]), ("p", ids["E"]));

        // D holds: three items of JUPITER (one deleted, one in a sprint, one with a backlog place), one of MARS (placed on MARS's backlog).
        await InsertItemAsync(cs, ids["D"], 1, ids["JUPITER"], sprint: false, deleted: false, placeFor: ids["JUPITER"]);
        await InsertItemAsync(cs, ids["D"], 2, ids["JUPITER"], sprint: true, deleted: false, placeFor: null);
        await InsertItemAsync(cs, ids["D"], 3, ids["JUPITER"], sprint: false, deleted: true, placeFor: null);
        await InsertItemAsync(cs, ids["D"], 4, ids["MARS"], sprint: false, deleted: false, placeFor: ids["MARS"]);

        return new Legacy(cs, romashka, other, ids);
    }

    private static async Task<Guid> InsertDepartmentAsync(string cs, Guid organization, string name, int minute, string colour)
    {
        var id = Guid.CreateVersion7();

        await ExecuteAsync(
            cs,
            """
            INSERT INTO areas (id, organization_id, parent_id, type, project_id, name, normalized_name, inherits_access, created_by, created_at, color)
            VALUES (@id, @o, NULL, 1, NULL, @name, upper(@name), true, 'legacy-1', @at, @color)
            """,
            ("id", id), ("o", organization), ("name", name), ("at", Epoch.AddMinutes(minute)), ("color", colour));

        return id;
    }

    private static async Task<Guid> InsertProjectAsync(string cs, Guid organization, string name, Guid? department, string status, int minute, string colour)
    {
        var project = Guid.CreateVersion7();

        await ExecuteAsync(
            cs,
            """
            INSERT INTO projects (id, organization_id, name, normalized_name, key, status, created_at, created_by, color)
            VALUES (@p, @o, @name, upper(@name), @key, @status, @at, 'legacy-1', @color);
            INSERT INTO areas (id, organization_id, parent_id, type, project_id, name, normalized_name, inherits_access, created_by, created_at)
            VALUES (gen_random_uuid(), @o, @dept, 2, @p, @name, upper(@name), true, 'legacy-1', @at)
            """,
            ("p", project), ("o", organization), ("name", name), ("key", name.Length == 1 ? name + "PR" : name.Length > 10 ? "K" + minute : name), ("status", status),
            ("dept", (object?)department ?? DBNull.Value), ("at", Epoch.AddMinutes(minute)), ("color", colour));

        return project;
    }

    private static async Task<Guid> InsertTeamAsync(string cs, Guid organization, string name, Guid? department, int order, string colour, bool deleted = false)
    {
        var id = Guid.Parse($"{order:x8}-0000-7000-8000-000000000001");

        await ExecuteAsync(
            cs,
            "INSERT INTO teams (id, organization_id, name, normalized_name, status, color, department_id) VALUES (@id, @o, @name, upper(@name), @status, @color, @d)",
            ("id", id), ("o", organization), ("name", name), ("status", deleted ? "Deleted" : "Active"), ("color", colour), ("d", (object?)department ?? DBNull.Value));

        return id;
    }

    private static Task LinkAsync(string cs, Guid organization, Guid project, Guid team) =>
        ExecuteAsync(
            cs,
            "INSERT INTO project_teams (project_id, team_id, organization_id, linked_by, linked_at) VALUES (@p, @t, @o, 'legacy-1', @at) ON CONFLICT DO NOTHING",
            ("p", project), ("t", team), ("o", organization), ("at", Epoch));

    /// <summary>One work item of the team on the project, written with the foreign keys of the process tables switched off (the items' type and state are not the point).</summary>
    private static async Task InsertItemAsync(string cs, Guid project, int number, Guid team, bool sprint, bool deleted, Guid? placeFor)
    {
        var id = Guid.CreateVersion7();

        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(
            """
            SET LOCAL session_replication_role = replica;
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, owning_team_id, sprint_id,
                                    deleted_from_state_id, deleted_by, deleted_at)
            VALUES (@id, @p, @n, gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), 'Item', '', gen_random_uuid(), now(), 'legacy-1', @changed, @t, @sprint,
                    @deletedState, @deletedBy, @deletedAt);
            INSERT INTO backlog_places (work_item_id, backlog_level_id, position, team_id)
            SELECT @id, gen_random_uuid(), 1, @place WHERE @place IS NOT NULL;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("p", project);
        command.Parameters.AddWithValue("n", number);
        command.Parameters.AddWithValue("t", team);
        command.Parameters.AddWithValue("changed", Epoch.AddDays(number));
        command.Parameters.AddWithValue("sprint", sprint ? Guid.CreateVersion7() : DBNull.Value);
        command.Parameters.AddWithValue("deletedState", deleted ? Guid.CreateVersion7() : DBNull.Value);
        command.Parameters.AddWithValue("deletedBy", deleted ? "legacy-1" : DBNull.Value);
        command.Parameters.AddWithValue("deletedAt", deleted ? Epoch : DBNull.Value);
        command.Parameters.Add(new NpgsqlParameter("place", NpgsqlTypes.NpgsqlDbType.Uuid) { Value = (object?)placeFor ?? DBNull.Value });
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    private static async Task InsertItemsAsync(string cs, Guid project, Guid team, int count)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(
            """
            SET LOCAL session_replication_role = replica;
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, owning_team_id)
            SELECT gen_random_uuid(), @p, n, gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), 'Item ' || n, '', gen_random_uuid(), now(), 'legacy-1', now(), @t
            FROM generate_series(1, @count) AS n
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("p", project);
        command.Parameters.AddWithValue("t", team);
        command.Parameters.AddWithValue("count", count);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    // --- Reading back ------------------------------------------------------------------------------------------------

    private static async Task<string> DepartmentOfAsync(Legacy legacy, string project) =>
        Assert.Single(await QueryAsync(
            legacy.ConnectionString,
            "SELECT d.name FROM areas n JOIN areas d ON d.id = n.parent_id WHERE n.project_id = @p AND n.type = 2",
            ("p", legacy.Ids[project])));

    private static async Task<string> FingerprintAsync(string cs) =>
        Assert.Single(await QueryAsync(
            cs,
            """
            SELECT md5(
                coalesce((SELECT string_agg(concat_ws('|', id, parent_id, color), ';' ORDER BY id) FROM areas), '') ||
                coalesce((SELECT string_agg(concat_ws('|', project_id, team_id), ';' ORDER BY project_id, team_id) FROM project_teams), '') ||
                coalesce((SELECT string_agg(concat_ws('|', id, owning_team_id, sprint_id, changed_at), ';' ORDER BY id) FROM work_items), '') ||
                coalesce((SELECT string_agg(concat_ws('|', id, default_team_id, color), ';' ORDER BY id) FROM projects), '') ||
                (SELECT count(*) FROM project_place_upgrade_log)::text)
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

    /// <summary>Migrates to the latest migration (<c>null</c>) or to the one before this migration; returns the notices of mod-022.</summary>
    private static async Task<List<string>> MigrateAsync(string connectionString, string? target)
    {
        await using var context = new MustaskDbContext(new DbContextOptionsBuilder<MustaskDbContext>().UseNpgsql(connectionString).Options);
        var notices = new List<string>();

        ((NpgsqlConnection)context.Database.GetDbConnection()).Notice += (_, e) =>
        {
            if (e.Notice.MessageText.StartsWith("mod-022:", StringComparison.Ordinal))
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
