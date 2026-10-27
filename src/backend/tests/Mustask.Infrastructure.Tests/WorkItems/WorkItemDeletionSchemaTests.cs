using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.Tests.Organizations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mustask.Infrastructure.Tests.WorkItems;

/// <summary>
/// mod-005 (ADR-0030 rule 1): the storage-level backstops of the deletion's marks. Each case builds exactly one violation with
/// raw SQL, so exactly one guard can fire, and the test names it; the aggregate never builds them, which is the point of a
/// backstop.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class WorkItemDeletionSchemaTests(OrganizationsDatabaseFixture database)
{
    private const string MarksCheck = "ck_work_items_deletion_marks";
    private const string HeldStateKey = "fk_work_items_state_definitions_deleted_from_state_id";

    // The marks are all-or-none: one or two of the three are refused, none or all three are accepted.
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public async Task Marks_OneOrTwoOfThree_AreRefusedByTheCheck(bool state, bool by, bool at)
    {
        var world = await SeedAsync();

        var refusal = await Assert.ThrowsAsync<PostgresException>(() => SetMarksAsync(world, state, by, at));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refusal.SqlState);
        Assert.Equal(MarksCheck, refusal.ConstraintName);
    }

    [Fact]
    public async Task Marks_NoneOrAllThree_AreAccepted()
    {
        var world = await SeedAsync();

        await SetMarksAsync(world, state: true, by: true, at: true);
        Assert.Equal([$"{world.FirstState}|deleter-sub"], await database.QueryAsync("SELECT concat_ws('|', deleted_from_state_id, deleted_by) FROM work_items WHERE id = @item", ("item", world.Item)));

        await database.ExecuteAsync(
            "UPDATE work_items SET deleted_from_state_id = NULL, deleted_by = NULL, deleted_at = NULL WHERE id = @item", ("item", world.Item));
        Assert.Equal(["NULL"], await database.QueryAsync("SELECT deleted_by FROM work_items WHERE id = @item", ("item", world.Item)));
    }

    // A held state is a state: the key refuses an id that is no state definition (the database's half of doc-001 BR-14 (b)).
    [Fact]
    public async Task HeldState_ThatIsNoState_IsRefusedByTheKey()
    {
        var world = await SeedAsync();

        var refusal = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "UPDATE work_items SET deleted_from_state_id = @state, deleted_by = 'x', deleted_at = now() WHERE id = @item",
            ("state", Guid.CreateVersion7()),
            ("item", world.Item)));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, refusal.SqlState);
        Assert.Equal(HeldStateKey, refusal.ConstraintName);
    }

    // The key is Restrict: a state definition that a deleted item remembers cannot be removed from under it.
    [Fact]
    public async Task StateDefinition_ThatAnItemRemembers_CannotBeRemoved()
    {
        var world = await SeedAsync();
        await SetMarksAsync(world, state: true, by: true, at: true);

        // Move the item out of the state first, so only the memory holds it: the other key (state_id) is not what refuses.
        await database.ExecuteAsync("UPDATE work_items SET state_id = @removed WHERE id = @item", ("removed", world.RemovedState), ("item", world.Item));

        var refusal = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "DELETE FROM state_definitions WHERE id = @state", ("state", world.FirstState)));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, refusal.SqlState);
        Assert.Equal(HeldStateKey, refusal.ConstraintName);
    }

    // plan-backend D-P6: the index is partial, over the few deleted rows only.
    [Fact]
    public async Task Index_OnTheHeldState_IsPartial()
    {
        await SeedAsync();

        var definition = (await database.QueryAsync(
            "SELECT indexdef FROM pg_indexes WHERE tablename = 'work_items' AND indexname = 'ix_work_items_deleted_from_state_id'")).Single();

        Assert.Contains("(deleted_from_state_id)", definition, StringComparison.Ordinal);
        Assert.Contains("WHERE (deleted_from_state_id IS NOT NULL)", definition, StringComparison.Ordinal);

        // No second, conventional index was added for the new key.
        Assert.Equal(
            ["1"],
            await database.QueryAsync(
                "SELECT count(*)::text FROM pg_indexes WHERE tablename = 'work_items' AND indexdef LIKE '%(deleted_from_state_id)%'"));
    }

    // The deleter is a user id of the column's width, as the creator is: 255 fit, 256 do not.
    [Fact]
    public async Task DeletedBy_Takes255Characters_AndRefuses256()
    {
        var world = await SeedAsync();

        await database.ExecuteAsync(
            "UPDATE work_items SET deleted_from_state_id = @state, deleted_by = @by, deleted_at = now() WHERE id = @item",
            ("state", world.FirstState),
            ("by", new string('u', WorkItemLimits.UserIdMaxLength)),
            ("item", world.Item));

        var refusal = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "UPDATE work_items SET deleted_by = @by WHERE id = @item", ("by", new string('u', WorkItemLimits.UserIdMaxLength + 1)), ("item", world.Item)));

        Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, refusal.SqlState);
    }

    // The marks round-trip through EF: Delete writes the three columns with the state, Restore clears them.
    [Fact]
    public async Task Marks_RoundTripThroughEf_DeleteThenRestore()
    {
        var world = await SeedAsync();
        var deletedAt = new DateTimeOffset(2026, 10, 18, 9, 14, 0, TimeSpan.Zero);

        await using (var context = database.NewContext())
        {
            var item = await context.WorkItems.SingleAsync(candidate => candidate.Id == world.Item);
            item.Delete(await TypeStatesAsync(context, item.TypeId), "deleter-sub", deletedAt);
            await context.SaveChangesAsync();
        }

        await using (var context = database.NewContext())
        {
            var item = await context.WorkItems.AsNoTracking().SingleAsync(candidate => candidate.Id == world.Item);

            Assert.Equal(world.RemovedState, item.StateId);
            Assert.Equal(world.FirstState, item.DeletedFromStateId);
            Assert.Equal("deleter-sub", item.DeletedBy);
            Assert.Equal(deletedAt, item.DeletedAt);
        }

        await using (var context = database.NewContext())
        {
            var item = await context.WorkItems.SingleAsync(candidate => candidate.Id == world.Item);
            item.Restore(await TypeStatesAsync(context, item.TypeId), null, deletedAt.AddHours(1));
            await context.SaveChangesAsync();
        }

        Assert.Equal(
            [$"{world.FirstState}|NULL|NULL|NULL"],
            await database.QueryAsync(
                "SELECT concat_ws('|', state_id, coalesce(deleted_from_state_id::text, 'NULL'), coalesce(deleted_by, 'NULL'), coalesce(deleted_at::text, 'NULL')) FROM work_items WHERE id = @item",
                ("item", world.Item)));
    }

    // The model and the migrations agree: nothing is left for a later migration to add.
    [Fact]
    public void Model_HasNoPendingChanges_AfterTheMigrations()
    {
        using var context = database.NewContext();

        Assert.False(context.Database.HasPendingModelChanges(), "The model has changes no migration carries.");
    }

    // --- Arrangement -------------------------------------------------------------------------------------------------

    private sealed record World(Guid Item, Guid FirstState, Guid RemovedState);

    private Task SetMarksAsync(World world, bool state, bool by, bool at) =>
        database.ExecuteAsync(
            """
            UPDATE work_items
            SET deleted_from_state_id = CASE WHEN @state THEN @held ELSE NULL END,
                deleted_by = CASE WHEN @by THEN 'deleter-sub' ELSE NULL END,
                deleted_at = CASE WHEN @at THEN now() ELSE NULL END
            WHERE id = @item
            """,
            ("state", state),
            ("by", by),
            ("at", at),
            ("held", world.FirstState),
            ("item", world.Item));

    private static async Task<WorkItemTypeStates> TypeStatesAsync(Mustask.Infrastructure.MustaskDbContext context, Guid typeId)
    {
        var type = await context.WorkItemTypes.Include(candidate => candidate.States).AsNoTracking().SingleAsync(candidate => candidate.Id == typeId);

        return new WorkItemTypeStates(
            type.Id,
            type.Key,
            type.Appearance,
            type.States.Select(state => new WorkItemTypeState(state.Id, state.Position, state.Category)).ToList());
    }

    /// <summary>A project with one story in its first state; the story's first and removed states are returned.</summary>
    private async Task<World> SeedAsync()
    {
        var structure = await database.CreateStructureAsync();
        var project = structure.Project.Id;

        var row = (await database.QueryAsync(
            """
            SELECT concat_ws('|', type.id::text, first.id::text, removed.id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS first ON first.work_item_type_id = type.id AND first.position = 1
            JOIN state_definitions AS removed ON removed.work_item_type_id = type.id AND removed.category = 'Removed'
            WHERE process.organization_id = @organization AND type.key = 'US'
            """,
            ("organization", structure.Organization.Id))).Single().Split('|');
        var item = Guid.CreateVersion7();

        await database.ExecuteAsync(
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, title, description, creation_request_id, created_at, created_by, changed_at)
            VALUES (@item, @project, 1, @type, @state, 'История', '', gen_random_uuid(), now(), 'schema-test', now())
            """,
            ("item", item),
            ("project", project),
            ("type", Guid.Parse(row[0])),
            ("state", Guid.Parse(row[1])));

        return new World(item, Guid.Parse(row[1]), Guid.Parse(row[2]));
    }
}
