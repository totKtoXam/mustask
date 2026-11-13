using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.Tests.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Mustask.Infrastructure.Tests.WorkItems;

/// <summary>
/// feat-018 US-4 · "Existing rows are unchanged by the release": <c>AddWorkItemDescriptionFormat</c> adds the format
/// column with a default, rewrites no row, widens the stored bound and keeps the plain one in the database; its down
/// step is refused, with a message that names the cause, once a description exceeds 100 000 characters. The database
/// is this class's own: the test rolls a migration back.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class AddWorkItemDescriptionFormatMigrationTests(OrganizationsDatabaseFixture database) : IClassFixture<OrganizationsDatabaseFixture>
{
    private static readonly DateTimeOffset Created = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    private const string Markup = "<b>жирный</b> & <script>alert(1)</script>\r\n\tстрока";

    // US-4 · "Existing rows are unchanged by the release" and "a description of exactly 100 000 plain code units still
    // reads back whole": rows seeded at the previous migration come back byte for byte and plain.
    [Fact]
    public async Task Migrator_AddFormat_KeepsExistingDescriptionsByteForByte_AllPlain()
    {
        var projectId = await SeedItemsAsync();

        await RollBackAsync();
        Assert.False(await ColumnExistsAsync(), "Rolling back must drop work_items.description_format.");

        await using (var context = database.NewContext())
        {
            await context.Database.MigrateAsync();
        }

        // length|md5 of the value, and the format.
        var rows = await RowsAsync(projectId);

        Assert.Equal(
            [
                $"{WorkItemLimits.DescriptionMaxLength}|{Md5(new string('ж', WorkItemLimits.DescriptionMaxLength))}|Plain",
                $"{Markup.Length}|{Md5(Markup)}|Plain",
                "0|" + Md5(string.Empty) + "|Plain",
            ],
            rows);
    }

    // The database backstops the domain: a description over the physical ceiling, a plain one over its own bound, an
    // empty html one and an unknown format are refused by their named check.
    [Fact]
    public async Task Checks_RefuseWhatTheDomainNeverWrites()
    {
        var projectId = await SeedItemsAsync();
        var number = 1;

        await AssertCheckAsync("ck_work_items_description_length", projectId, number, "SET description = repeat('a', 1000001), description_format = 'Html'");
        await AssertCheckAsync("ck_work_items_plain_description_length", projectId, number, "SET description = repeat('a', 100001), description_format = 'Plain'");
        await AssertCheckAsync("ck_work_items_empty_description_plain", projectId, number, "SET description = '', description_format = 'Html'");
        await AssertCheckAsync("ck_work_items_description_format", projectId, number, "SET description_format = 'Markdown'");

        // The ceiling itself is accepted for html, in characters (not UTF-16 units).
        await database.ExecuteAsync(
            "UPDATE work_items SET description = repeat('😀', 1000000), description_format = 'Html' WHERE project_id = @project AND number = 1",
            ("project", projectId));
        await database.ExecuteAsync(
            "UPDATE work_items SET description = @plain, description_format = 'Plain' WHERE project_id = @project AND number = 1",
            ("plain", "a"),
            ("project", projectId));
    }

    // NFR Rollback: "its down migration fails once any row exceeds 100 000" — named, and nothing dropped by the refusal.
    [Fact]
    public async Task Migrator_RollBack_IsRefusedWithAMessage_WhenARowExceeds100000_AndSucceedsOtherwise()
    {
        var projectId = await SeedItemsAsync();

        await database.ExecuteAsync(
            "UPDATE work_items SET description = repeat('a', 100001), description_format = 'Html' WHERE project_id = @project AND number = 2",
            ("project", projectId));

        var refused = await Assert.ThrowsAnyAsync<Exception>(RollBackAsync);
        Assert.Contains("Cannot roll back AddWorkItemDescriptionFormat", refused.ToString(), StringComparison.Ordinal);
        Assert.True(await ColumnExistsAsync(), "A refused rollback must leave the column in place.");

        // An html row that fits is kept as it was, as a string in a plain field.
        await database.ExecuteAsync(
            "UPDATE work_items SET description = '<p>a</p>', description_format = 'Html' WHERE project_id = @project AND number = 2",
            ("project", projectId));

        await RollBackAsync();
        Assert.False(await ColumnExistsAsync());

        await using (var context = database.NewContext())
        {
            await context.Database.MigrateAsync();
        }

        Assert.True(await ColumnExistsAsync());
    }

    private async Task<Guid> SeedItemsAsync()
    {
        var structure = await database.CreateStructureAsync();
        var projectId = structure.Project.Project.Id;

        await using (var context = database.NewContext())
        {
            var processId = structure.Organization.ProcessId;
            var type = (await context.WorkItemTypes.Include(item => item.States).Where(item => item.ProcessId == processId).ToListAsync())
                .OrderBy(item => item.Position)
                .Last();

            var states = new WorkItemTypeStates(
                type.Id, type.Key, type.Appearance, type.States.Select(state => new WorkItemTypeState(state.Id, state.Position, state.Category)).ToList());

            for (var number = 1; number <= 3; number++)
            {
                context.WorkItems.Add(WorkItem.Create(
                    projectId, number, states, structure.Project.Root.Id, $"Элемент {number}", Guid.CreateVersion7(), Created, "migration-test"));
            }

            await context.SaveChangesAsync();
        }

        await database.ExecuteAsync(
            "UPDATE work_items SET description = repeat('ж', 100000) WHERE project_id = @project AND number = 1", ("project", projectId));
        await database.ExecuteAsync(
            "UPDATE work_items SET description = @markup WHERE project_id = @project AND number = 2", ("markup", Markup), ("project", projectId));

        return projectId;
    }

    private async Task AssertCheckAsync(string constraint, Guid projectId, int number, string set)
    {
        var failure = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            $"UPDATE work_items {set} WHERE project_id = @project AND number = {number}", ("project", projectId)));

        Assert.Equal(constraint, failure.ConstraintName);
    }

    /// <summary>Migrates to the migration before AddWorkItemDescriptionFormat, found by name, never hard-coded.</summary>
    private async Task RollBackAsync()
    {
        await using var context = database.NewContext();

        var previous = context.Database.GetMigrations()
            .TakeWhile(id => !id.EndsWith("_AddWorkItemDescriptionFormat", StringComparison.Ordinal))
            .Last();

        await context.GetService<IMigrator>().MigrateAsync(previous);
    }

    private async Task<List<string>> RowsAsync(Guid projectId)
    {
        const string Sql =
            """
            SELECT char_length(description)::text || '|' || md5(description) || '|' || description_format
            FROM work_items WHERE project_id = @project ORDER BY number
            """;

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(Sql, connection);
        command.Parameters.AddWithValue("project", projectId);

        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    private async Task<bool> ColumnExistsAsync()
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'work_items' AND column_name = 'description_format')",
            connection);

        return await command.ExecuteScalarAsync() is true;
    }

    private static string Md5(string text) =>
        Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
