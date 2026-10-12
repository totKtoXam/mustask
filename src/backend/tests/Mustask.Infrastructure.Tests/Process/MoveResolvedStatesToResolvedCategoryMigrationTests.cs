using Mustask.Infrastructure.Migrations;
using Mustask.Infrastructure.Tests.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Process;

/// <summary>
/// bug-004 D-3 · "«Разрешена» means one thing on every type": <c>MoveResolvedStatesToResolvedCategory</c> moves the
/// processes that already exist. It touches only the rows the old seed wrote (the third state of an Эпик, Фича or
/// Пользовательская история, «Разрешена», InProgress), leaves a state a person changed as it is and names it in a
/// warning, changes nothing when applied again, and rolls back to the old seed's shape. The database is this class's
/// own: the test rolls a migration back.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class MoveResolvedStatesToResolvedCategoryMigrationTests(OrganizationsDatabaseFixture database)
    : IClassFixture<OrganizationsDatabaseFixture>
{
    private const string MigrationName = "_MoveResolvedStatesToResolvedCategory";

    // Organization A is the old seed as it stood; B had its Эпик's «Разрешена» renamed «Проверена» by hand; C had its
    // Пользовательская история's «Разрешена» put in «Завершённые» (Completed) by hand.
    [Fact]
    public async Task Migrator_MoveResolved_MovesOnlyRowsStillAsSeeded_ReportsTheRest_IsIdempotent_AndRollsBack()
    {
        var a = (await database.CreateStructureAsync()).Organization.Id;
        var b = (await database.CreateStructureAsync()).Organization.Id;
        var c = (await database.CreateStructureAsync()).Organization.Id;

        // The database as it was before the migration: the old seed's rows, then the two hand edits.
        await RollBackAsync();
        await database.ExecuteAsync(
            "UPDATE state_definitions AS state SET name = 'Проверена' FROM work_item_types AS kind, processes AS process "
            + "WHERE kind.id = state.work_item_type_id AND process.id = kind.process_id AND process.organization_id = @organization "
            + "AND kind.key = 'EPIC' AND state.name = 'Разрешена'",
            ("organization", b));
        await database.ExecuteAsync(
            "UPDATE state_definitions AS state SET category = 'Completed' FROM work_item_types AS kind, processes AS process "
            + "WHERE kind.id = state.work_item_type_id AND process.id = kind.process_id AND process.organization_id = @organization "
            + "AND kind.key = 'US' AND state.name = 'Разрешена'",
            ("organization", c));

        Assert.Equal(["EPIC Разрешена InProgress", "FEAT Разрешена InProgress", "US Разрешена InProgress", "BUG Разрешена Resolved"], await ThirdStatesAsync(a));
        Assert.Equal(["EPIC Проверена InProgress", "FEAT Разрешена InProgress", "US Разрешена InProgress", "BUG Разрешена Resolved"], await ThirdStatesAsync(b));
        Assert.Equal(["EPIC Разрешена InProgress", "FEAT Разрешена InProgress", "US Разрешена Completed", "BUG Разрешена Resolved"], await ThirdStatesAsync(c));
        var before = await AllStatesAsync();

        // Up: A's three, B's two and C's two rows move; nothing else does.
        var up = await MigrateAsync();

        Assert.Equal(["EPIC Разрешена Resolved", "FEAT Разрешена Resolved", "US Разрешена Resolved", "BUG Разрешена Resolved"], await ThirdStatesAsync(a));
        Assert.Equal(["EPIC Проверена InProgress", "FEAT Разрешена Resolved", "US Разрешена Resolved", "BUG Разрешена Resolved"], await ThirdStatesAsync(b));
        Assert.Equal(["EPIC Разрешена Resolved", "FEAT Разрешена Resolved", "US Разрешена Completed", "BUG Разрешена Resolved"], await ThirdStatesAsync(c));

        var afterUp = await AllStatesAsync();
        Assert.Equal(
            before.Keys.Order(),
            afterUp.Keys.Order());
        Assert.Equal(
            new[]
            {
                $"{a} EPIC", $"{a} FEAT", $"{a} US",
                $"{b} FEAT", $"{b} US",
                $"{c} EPIC", $"{c} FEAT",
            }.Order(),
            afterUp.Where(state => before[state.Key].Category != state.Value.Category).Select(state => $"{state.Value.Organization} {state.Value.Key}").Order());
        Assert.All(
            afterUp.Where(state => before[state.Key].Category != state.Value.Category),
            state => Assert.Equal(("InProgress", "Resolved"), (before[state.Key].Category, state.Value.Category)));

        // Reported, not guessed: C's «Разрешена» now stands in two categories and is named; A and B are clean.
        Assert.Contains("7 state(s) moved", Assert.Single(up.Notices));
        var warning = Assert.Single(up.Warnings);
        Assert.Contains(c.ToString(), warning, StringComparison.Ordinal);
        Assert.Contains("\"Разрешена\"", warning, StringComparison.Ordinal);
        Assert.Contains("Completed, Resolved", warning, StringComparison.Ordinal);
        Assert.DoesNotContain(a.ToString(), warning, StringComparison.Ordinal);
        Assert.DoesNotContain(b.ToString(), warning, StringComparison.Ordinal);

        // The warning is in the server log, which `dotnet ef database update` does not print (review R1-1). What a person
        // sees is the runbook's check, run afterwards: it names C and only C. B's rename breaks no rule and is named nowhere.
        Assert.Equal([$"{c} Разрешена Completed, Resolved"], await RunbookCheckAsync());

        // Idempotent: the migration's own SQL run once more moves nothing and touches no row.
        var again = await ApplyUpSqlAgainAsync();

        Assert.Contains("0 state(s) moved", Assert.Single(again.Notices));
        Assert.Equal(afterUp, await AllStatesAsync());

        // Down: the same predicate, mirrored. A is the old seed again; the two hand edits stay as they were.
        await RollBackAsync();

        Assert.Equal(["EPIC Разрешена InProgress", "FEAT Разрешена InProgress", "US Разрешена InProgress", "BUG Разрешена Resolved"], await ThirdStatesAsync(a));
        Assert.Equal(["EPIC Проверена InProgress", "FEAT Разрешена InProgress", "US Разрешена InProgress", "BUG Разрешена Resolved"], await ThirdStatesAsync(b));
        Assert.Equal(["EPIC Разрешена InProgress", "FEAT Разрешена InProgress", "US Разрешена Completed", "BUG Разрешена Resolved"], await ThirdStatesAsync(c));
        Assert.Equal(before, await AllStatesAsync());

        // And Up again, as a second deploy would: the same result as the first.
        await MigrateAsync();

        Assert.Equal(afterUp, await AllStatesAsync());

        // Once per database (review R1-3): a checkout built before the migration, sharing the database, creates
        // organization D after it ran, with the old seed's shape. `database update` has nothing pending and leaves it;
        // the check names D beside C; the runbook's move, run by hand, puts D right and the check is back to C alone.
        var d = (await database.CreateStructureAsync()).Organization.Id;
        await database.ExecuteAsync(
            "UPDATE state_definitions AS state SET category = 'InProgress' FROM work_item_types AS kind, processes AS process "
            + "WHERE kind.id = state.work_item_type_id AND process.id = kind.process_id AND process.organization_id = @organization "
            + "AND kind.key IN ('EPIC', 'FEAT', 'US') AND state.name = 'Разрешена'",
            ("organization", d));

        await MigrateAsync();

        Assert.Equal(["EPIC Разрешена InProgress", "FEAT Разрешена InProgress", "US Разрешена InProgress", "BUG Разрешена Resolved"], await ThirdStatesAsync(d));
        Assert.Equal(
            new[] { $"{c} Разрешена Completed, Resolved", $"{d} Разрешена InProgress, Resolved" }.Order(StringComparer.Ordinal),
            (await RunbookCheckAsync()).Order(StringComparer.Ordinal));

        await database.ExecuteAsync(MigrationRunbook.MoveSql);

        Assert.Equal(["EPIC Разрешена Resolved", "FEAT Разрешена Resolved", "US Разрешена Resolved", "BUG Разрешена Resolved"], await ThirdStatesAsync(d));
        Assert.Equal([$"{c} Разрешена Completed, Resolved"], await RunbookCheckAsync());
    }

    private sealed record Messages(List<string> Notices, List<string> Warnings);

    private sealed record StateRow(Guid Organization, string Key, string Category);

    /// <summary>Applies every pending migration on a connection whose notices and warnings are collected.</summary>
    private async Task<Messages> MigrateAsync()
    {
        await using var context = database.NewContext();
        var messages = Collect(context);

        await context.Database.MigrateAsync();

        return messages;
    }

    /// <summary>Migrates to the migration before this one, found by name, never hard-coded.</summary>
    private async Task RollBackAsync()
    {
        await using var context = database.NewContext();

        var previous = context.Database.GetMigrations()
            .TakeWhile(id => !id.EndsWith(MigrationName, StringComparison.Ordinal))
            .Last();

        await context.GetService<IMigrator>().MigrateAsync(previous);
    }

    /// <summary>The migration's own <c>Up</c> operations, generated as SQL and run once more on the database as it stands.</summary>
    private async Task<Messages> ApplyUpSqlAgainAsync()
    {
        await using var context = database.NewContext();
        var messages = Collect(context);
        var commands = context.GetService<IMigrationsSqlGenerator>().Generate(new MoveResolvedStatesToResolvedCategory().UpOperations, context.Model);

        foreach (var command in commands)
        {
            await context.Database.ExecuteSqlRawAsync(command.CommandText);
        }

        return messages;
    }

    private static Messages Collect(DbContext context)
    {
        var messages = new Messages([], []);

        // EF's own "relation __EFMigrationsHistory already exists, skipping" notice is not the migration's: only its lines count.
        ((NpgsqlConnection)context.Database.GetDbConnection()).Notice += (_, e) =>
        {
            if (!e.Notice.MessageText.StartsWith("bug-004:", StringComparison.Ordinal))
            {
                return;
            }

            if (e.Notice.Severity == "WARNING")
            {
                messages.Warnings.Add(e.Notice.MessageText);
            }
            else if (e.Notice.Severity == "NOTICE")
            {
                messages.Notices.Add(e.Notice.MessageText);
            }
        };

        return messages;
    }

    /// <summary>The runbook's check, run on the database as it stands: one "organization name categories" line per breach.</summary>
    private async Task<List<string>> RunbookCheckAsync()
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(MigrationRunbook.CheckSql, connection);

        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add($"{reader.GetGuid(0)} {reader.GetString(1)} {reader.GetString(2)}");
        }

        return rows;
    }

    /// <summary>The third state of an Эпик, Фича, Пользовательская история and Баг of the organization's process, as "KEY name Category".</summary>
    private async Task<List<string>> ThirdStatesAsync(Guid organizationId)
    {
        const string Sql =
            """
            SELECT concat_ws(' ', kind.key, state.name, state.category)
            FROM state_definitions AS state
            JOIN work_item_types AS kind ON kind.id = state.work_item_type_id
            JOIN processes AS process ON process.id = kind.process_id
            WHERE process.organization_id = @organization AND state.position = 3 AND kind.key IN ('EPIC', 'FEAT', 'US', 'BUG')
            ORDER BY kind.position
            """;

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(Sql, connection);
        command.Parameters.AddWithValue("organization", organizationId);

        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    /// <summary>Every state of every process: its id to its organization, type key and category.</summary>
    private async Task<Dictionary<Guid, StateRow>> AllStatesAsync()
    {
        const string Sql =
            """
            SELECT state.id, process.organization_id, kind.key, state.category
            FROM state_definitions AS state
            JOIN work_item_types AS kind ON kind.id = state.work_item_type_id
            JOIN processes AS process ON process.id = kind.process_id
            """;

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(Sql, connection);

        var rows = new Dictionary<Guid, StateRow>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows[reader.GetGuid(0)] = new StateRow(reader.GetGuid(1), reader.GetString(2), reader.GetString(3));
        }

        return rows;
    }
}
