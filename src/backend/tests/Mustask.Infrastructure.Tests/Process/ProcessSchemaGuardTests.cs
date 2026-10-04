using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Process;

/// <summary>
/// plan-backend.md § 2, "Indexes" and "Foreign keys": the storage-level guards
/// behind "no type, key, token, state or backlog level repeats within a process"
/// (US-1, BR-M2-9), "nothing is ever deleted" and "a type sits on a level of its
/// own process" (BR-M2-1). Each duplicate case copies a row of the
/// seeded process and changes every unique column but one, so exactly one guard
/// can fire and the test names it. Raw SQL is used on purpose: the domain model
/// offers no way to build the violation.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(ProcessDatabaseCollection.Name)]
public sealed class ProcessSchemaGuardTests(ProcessDatabaseFixture database)
{
    private const string UniqueViolation = "23505";
    private const string ForeignKeyViolation = PostgresErrorCodes.ForeignKeyViolation;
    // ON DELETE RESTRICT raises restrict_violation, a sibling of the NO ACTION foreign_key_violation (23503).
    private const string RestrictViolation = PostgresErrorCodes.RestrictViolation;

    private const string CopyLevel =
        "INSERT INTO backlog_levels (id, process_id, name, rank, kind) "
        + "SELECT gen_random_uuid(), process_id, {name}, {rank}, kind FROM backlog_levels WHERE process_id = @process AND name = 'Эпики'";

    private const string CopyType =
        "INSERT INTO work_item_types (id, process_id, backlog_level_id, key, name, appearance, position) "
        + "SELECT gen_random_uuid(), process_id, backlog_level_id, {key}, {name}, {appearance}, {position} "
        + "FROM work_item_types WHERE process_id = @process AND key = 'EPIC'";

    private const string CopyState =
        "INSERT INTO state_definitions (id, work_item_type_id, name, category, position) "
        + "SELECT gen_random_uuid(), work_item_type_id, {name}, category, {position} FROM state_definitions "
        + "WHERE work_item_type_id = (SELECT id FROM work_item_types WHERE process_id = @process AND key = 'EPIC') AND position = 1";

    // The unchanged column of each copy is the one the guard is about.
    public static TheoryData<string, string, string> Violations() => new()
    {
        { "a second process for one organization", "INSERT INTO processes (id, organization_id) SELECT gen_random_uuid(), organization_id FROM processes WHERE id = @process", "ux_processes_organization_id" },
        { "two levels with one rank", CopyLevel.Replace("{name}", "'Иной'").Replace("{rank}", "rank"), "ux_backlog_levels_process_id_rank" },
        { "two levels with one name", CopyLevel.Replace("{name}", "name").Replace("{rank}", "99"), "ux_backlog_levels_process_id_name" },
        { "two types with one key", CopyType.Replace("{key}", "key").Replace("{name}", "'Иной'").Replace("{appearance}", "'zzz'").Replace("{position}", "99"), "ux_work_item_types_process_id_key" },
        { "two types with one name", CopyType.Replace("{key}", "'ZZZ'").Replace("{name}", "name").Replace("{appearance}", "'zzz'").Replace("{position}", "99"), "ux_work_item_types_process_id_name" },
        { "two types with one appearance token", CopyType.Replace("{key}", "'ZZZ'").Replace("{name}", "'Иной'").Replace("{appearance}", "appearance").Replace("{position}", "99"), "ux_work_item_types_process_id_appearance" },
        { "two types with one position", CopyType.Replace("{key}", "'ZZZ'").Replace("{name}", "'Иной'").Replace("{appearance}", "'zzz'").Replace("{position}", "position"), "ux_work_item_types_process_id_position" },
        { "two states with one position in a type", CopyState.Replace("{name}", "'Иная'").Replace("{position}", "position"), "ux_state_definitions_work_item_type_id_position" },
        { "two states with one name in a type", CopyState.Replace("{name}", "name").Replace("{position}", "99"), "ux_state_definitions_work_item_type_id_name" },
    };

    // US-1 · "No two types, states or backlog levels share an identity ... within a process": the storage refuses the duplicate.
    [Theory]
    [MemberData(nameof(Violations))]
    public async Task Insert_DuplicateWithinAProcess_IsRejectedByItsUniqueIndex(string what, string sql, string constraint)
    {
        var processId = await SeededProcessAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(sql, processId));

        Assert.True(error.SqlState == UniqueViolation, $"{what}: expected a unique violation, got {error.SqlState}.");
        Assert.Equal(constraint, error.ConstraintName);
    }

    // Spec § "Operation completeness": nothing is ever deleted, so a cascade must not exist. Each delete is refused.
    [Theory]
    [InlineData("DELETE FROM backlog_levels WHERE process_id = @process AND name = 'Эпики'")]
    [InlineData("DELETE FROM work_item_types WHERE process_id = @process AND key = 'EPIC'")]
    [InlineData("DELETE FROM processes WHERE id = @process")]
    public async Task Delete_ParentOfExistingRows_IsRefusedByARestrictForeignKey(string sql)
    {
        var processId = await SeededProcessAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(sql, processId));

        Assert.Equal(RestrictViolation, error.SqlState);
    }

    // BR-M2-9: the same label may repeat across types, because the state indexes are per type, not per process.
    // The insert is rolled back (QA pass 1, qa.md D-1): committed, its two extra states would break the 1 : 4 : 5 : 24
    // row ratio that ProcessSeedTests asserts over the whole shared database, whenever this class ran first.
    [Fact]
    public async Task Insert_SameStateNameInAnotherType_IsAllowed()
    {
        var processId = await SeededProcessAsync();

        var rows = await ExecuteAndRollBackAsync(
            "INSERT INTO state_definitions (id, work_item_type_id, name, category, position) "
            + "SELECT gen_random_uuid(), t.id, 'Дополнительная', 'InProgress', 99 FROM work_item_types t WHERE t.process_id = @process AND t.key IN ('EPIC', 'FEAT')",
            processId);

        Assert.Equal(2, rows);
    }

    // The control of the case below: the same statement with the level taken from the type's own process is accepted.
    // BR-M2-1: a type's level belongs to the type's process. Rolled back, like every insert of this class that is not refused.
    [Fact]
    public async Task Insert_TypeOnABacklogLevelOfItsOwnProcess_IsAllowed()
    {
        var processId = await SeededProcessAsync();

        var rows = await ExecuteAndRollBackAsync(InsertTypeOnLevelOf("@process"), processId);

        Assert.Equal(1, rows);
    }

    // qa.md D-2 (BR-M2-1, ADR-0011): two processes never share a backlog level, so the database refuses a type of one
    // process placed on a level of another. The foreign key of work_item_types targets the composite key
    // (process_id, id) of backlog_levels; a foreign key on backlog_levels (id) alone accepted this row. Rolled back, so
    // the row outlives the test neither when the guard works nor when it does not (QA pass 1, D-1).
    [Fact]
    public async Task Insert_TypeOnABacklogLevelOfAnotherProcess_IsRejectedByTheCompositeForeignKey()
    {
        var processId = await SeededProcessAsync();
        var otherProcessId = await SeededProcessAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAndRollBackAsync(InsertTypeOnLevelOf("@other"), processId, otherProcessId));

        Assert.Equal(ForeignKeyViolation, error.SqlState);
        Assert.Equal("fk_work_item_types_backlog_levels_process_id_backlog_level_id", error.ConstraintName);
    }

    /// <summary>A new type of <c>@process</c> on the level «Эпики» of the process that <paramref name="levelOwner"/> names (<c>@process</c> or <c>@other</c>).</summary>
    private static string InsertTypeOnLevelOf(string levelOwner) =>
        "INSERT INTO work_item_types (id, process_id, backlog_level_id, key, name, appearance, position) "
        + $"SELECT gen_random_uuid(), @process, (SELECT id FROM backlog_levels WHERE process_id = {levelOwner} AND name = 'Эпики'), 'QAX', 'Чужой', 'qax', 99";

    private async Task<Guid> SeededProcessAsync()
    {
        var organizationId = Guid.NewGuid();
        await using var provider = database.BuildServices();
        var id = await ProcessDatabaseFixture.SeedInUnitOfWorkAsync(provider, organizationId);

        return id.Value;
    }

    private async Task<int> ExecuteAsync(string sql, Guid processId)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("process", processId);

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>Runs <paramref name="sql"/> in a transaction of its own and rolls it back, so the shared database keeps only complete processes.</summary>
    private async Task<int> ExecuteAndRollBackAsync(string sql, Guid processId, Guid? otherProcessId = null)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("process", processId);

        if (otherProcessId is { } other)
        {
            command.Parameters.AddWithValue("other", other);
        }

        var rows = await command.ExecuteNonQueryAsync();
        await transaction.RollbackAsync();

        return rows;
    }
}
