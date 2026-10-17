using Mustask.Infrastructure.Tests.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace Mustask.Infrastructure.Tests.WorkItems;

/// <summary>
/// mod-017 (ADR-0028 rules 5 and 9): the storage-level backstops of the owning team. Each case builds exactly one violation
/// with raw SQL, so exactly one guard can fire, and the test names it. The code never builds them; that is the point of a
/// backstop.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class OwningTeamSchemaTests(OrganizationsDatabaseFixture database)
{
    private const string OwnerKey = "fk_work_items_teams_project_id_owning_team_id";
    private const string DefaultKey = "fk_projects_teams_id_default_team_id";

    // US-1 · "a team the project does not have": the database refuses an owning team of another project, the organization's own
    // other project included.
    [Fact]
    public async Task Item_OwnedByATeamOfAnotherProject_IsRefusedByTheOwnerKey()
    {
        var world = await SeedAsync();

        var refusal = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "UPDATE work_items SET owning_team_id = @team WHERE id = @item", ("team", world.OtherProjectTeam), ("item", world.Item)));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, refusal.SqlState);
        Assert.Equal(OwnerKey, refusal.ConstraintName);
    }

    [Fact]
    public async Task Item_OwnedByATeamOfItsProject_OrByNone_IsAccepted()
    {
        var world = await SeedAsync();

        await database.ExecuteAsync("UPDATE work_items SET owning_team_id = @team WHERE id = @item", ("team", world.Team), ("item", world.Item));
        Assert.Equal([world.Team.ToString()], await database.QueryAsync("SELECT owning_team_id::text FROM work_items WHERE id = @item", ("item", world.Item)));

        await database.ExecuteAsync("UPDATE work_items SET owning_team_id = NULL WHERE id = @item", ("item", world.Item));
        Assert.Equal(["NULL"], await database.QueryAsync("SELECT owning_team_id::text FROM work_items WHERE id = @item", ("item", world.Item)));
    }

    // A team row is never removed from under the items it owns; a deletion releases them first (ADR-0028 rule 7).
    [Fact]
    public async Task Team_ThatOwnsAnItem_CannotBeRemoved()
    {
        var world = await SeedAsync();
        var bare = Guid.CreateVersion7();
        await database.ExecuteAsync(
            "INSERT INTO teams (id, project_id, name, normalized_name, status, is_first_team) VALUES (@team, @project, 'Без области', 'БЕЗ ОБЛАСТИ', 'Active', false)",
            ("team", bare),
            ("project", world.Project));
        await database.ExecuteAsync("UPDATE work_items SET owning_team_id = @team WHERE id = @item", ("team", bare), ("item", world.Item));

        var refusal = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "DELETE FROM teams WHERE id = @team", ("team", bare)));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, refusal.SqlState);
        Assert.Equal(OwnerKey, refusal.ConstraintName);
    }

    // US-3 · "a team the project does not have", for the default naming: another project's team is refused by the key.
    [Fact]
    public async Task Project_DefaultTeam_OfAnotherProject_IsRefusedByTheDefaultKey()
    {
        var world = await SeedAsync();

        var refusal = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "UPDATE projects SET default_team_id = @team WHERE id = @project", ("team", world.OtherProjectTeam), ("project", world.Project)));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, refusal.SqlState);
        Assert.Equal(DefaultKey, refusal.ConstraintName);
    }

    [Fact]
    public async Task Project_DefaultTeam_OfItsOwnProject_IsAcceptedAndClears()
    {
        var world = await SeedAsync();

        await database.ExecuteAsync("UPDATE projects SET default_team_id = @team WHERE id = @project", ("team", world.Team), ("project", world.Project));
        Assert.Equal([world.Team.ToString()], await database.QueryAsync("SELECT default_team_id::text FROM projects WHERE id = @project", ("project", world.Project)));

        await database.ExecuteAsync("UPDATE projects SET default_team_id = NULL WHERE id = @project", ("project", world.Project));
        Assert.Equal(["NULL"], await database.QueryAsync("SELECT default_team_id::text FROM projects WHERE id = @project", ("project", world.Project)));
    }

    // The composite key (id, default_team_id) is built by EF with the first column the primary key: the runtime path of naming
    // and clearing the setting through the model works (plan-backend § 4.1, D-11's fallback is unneeded).
    [Fact]
    public async Task Project_NameAndClearDefaultTeam_RoundTripsThroughEf()
    {
        var world = await SeedAsync();

        await using (var context = database.NewContext())
        {
            var project = await context.Projects.SingleAsync(candidate => candidate.Id == world.Project);
            Assert.True(project.NameDefaultTeam(world.Team));
            await context.SaveChangesAsync();
        }

        await using (var context = database.NewContext())
        {
            var project = await context.Projects.AsNoTracking().SingleAsync(candidate => candidate.Id == world.Project);
            Assert.Equal(world.Team, project.DefaultTeamId);
        }

        await using (var context = database.NewContext())
        {
            var project = await context.Projects.SingleAsync(candidate => candidate.Id == world.Project);
            Assert.True(project.NameDefaultTeam(null));
            await context.SaveChangesAsync();
        }

        Assert.Equal(["NULL"], await database.QueryAsync("SELECT default_team_id::text FROM projects WHERE id = @project", ("project", world.Project)));
    }

    [Fact]
    public async Task Indexes_TheUniqueTargetOfThePlaceKey_AndTheTeamViewIndex_Exist()
    {
        await SeedAsync();

        Assert.Equal(
            ["true"],
            await database.QueryAsync(
                "SELECT i.indisunique::text FROM pg_class AS c JOIN pg_index AS i ON i.indexrelid = c.oid WHERE c.relname = 'ix_work_items_id_owning_team_id'"));
        Assert.Equal(
            ["project_id,owning_team_id,type_id"],
            await database.QueryAsync(
                """
                SELECT string_agg(a.attname, ',' ORDER BY k.ordinality)
                FROM pg_class AS c
                JOIN pg_index AS i ON i.indexrelid = c.oid
                CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY AS k(attnum, ordinality)
                JOIN pg_attribute AS a ON a.attrelid = i.indrelid AND a.attnum = k.attnum
                WHERE c.relname = 'ix_work_items_project_id_owning_team_id_type_id'
                """));
    }

    // The model and the migrations agree: nothing is left for a later migration to add.
    [Fact]
    public void Model_HasNoPendingChanges_AfterTheMigrations()
    {
        using var context = database.NewContext();

        Assert.False(context.Database.HasPendingModelChanges(), "The model has changes no migration carries.");
    }

    // --- Arrangement -------------------------------------------------------------------------------------------------

    private sealed record World(Guid Project, Guid Team, Guid OtherProjectTeam, Guid Item);

    /// <summary>A project with a second team and one story, and another project of the organization with its own team.</summary>
    private async Task<World> SeedAsync()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.AddProjectAsync(structure.Organization.Id, "Другой проект", "OTH");
        var project = structure.Project.Project.Id;

        var row = (await database.QueryAsync(
            """
            SELECT concat_ws('|', type.id::text, state.id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS state ON state.work_item_type_id = type.id AND state.position = 1
            WHERE process.organization_id = @organization AND type.key = 'US'
            """,
            ("organization", structure.Organization.Id))).Single().Split('|');
        var item = Guid.CreateVersion7();

        await database.ExecuteAsync(
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at)
            VALUES (@item, @project, 1, @type, @state, @area, 'История', '', gen_random_uuid(), now(), 'schema-test', now())
            """,
            ("item", item),
            ("project", project),
            ("type", Guid.Parse(row[0])),
            ("state", Guid.Parse(row[1])),
            ("area", structure.Project.Root.Id));

        return new World(project, structure.SecondTeam.Team.Id, other.FirstTeam.Id, item);
    }
}
