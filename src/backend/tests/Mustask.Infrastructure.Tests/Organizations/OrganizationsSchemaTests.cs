using Mustask.Domain.Organizations;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// plan-backend § 3: the storage-level backstops of M1's rules. Each case
/// builds exactly one violation with raw SQL, so exactly one guard can fire,
/// and the test names it. The domain offers no way to build them; that is the
/// point of a backstop.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class OrganizationsSchemaTests(OrganizationsDatabaseFixture database)
{
    private const string UniqueViolation = PostgresErrorCodes.UniqueViolation;
    private const string CheckViolation = PostgresErrorCodes.CheckViolation;
    private const string OwnerKey = "fk_areas_teams_project_id_owner_team_id";
    private const string DefaultKey = "fk_areas_teams_project_id_default_of_team_id";
    private const string ParentKey = "fk_areas_areas_project_id_parent_id";

    // @org, @project, @first, @second, @root, @secondArea, @process are bound from a fresh structure.
    public static TheoryData<string, string, string> UniqueIndexViolations() => new()
    {
        {
            "BR-M1-1: an organization name in another case, the holder deactivated or not",
            "INSERT INTO organizations (id, name, normalized_name, status, process_id, created_at, created_by) "
            + "SELECT gen_random_uuid(), lower(name), normalized_name, 'Deactivated', @otherProcess, now(), 'x' FROM organizations WHERE id = @org",
            "ix_organizations_normalized_name"
        },
        {
            "BR-M1-3: two organizations on one process",
            "INSERT INTO organizations (id, name, normalized_name, status, process_id, created_at, created_by) "
            + "VALUES (gen_random_uuid(), 'Иная', 'ИНАЯ ' || gen_random_uuid(), 'Active', @process, now(), 'x')",
            "ix_organizations_process_id"
        },
        {
            "BR-M1-1: a project name in the organization, all statuses",
            "INSERT INTO projects (id, organization_id, name, normalized_name, key, status, created_at, created_by) "
            + "SELECT gen_random_uuid(), organization_id, name, normalized_name, 'ZZZ', 'Deactivated', now(), 'x' FROM projects WHERE id = @project",
            "ix_projects_organization_id_normalized_name"
        },
        {
            "BR-M1-1: a project key in the organization, all statuses",
            "INSERT INTO projects (id, organization_id, name, normalized_name, key, status, created_at, created_by) "
            + "SELECT gen_random_uuid(), organization_id, 'Иной', 'ИНОЙ', key, 'Deactivated', now(), 'x' FROM projects WHERE id = @project",
            "ix_projects_organization_id_key"
        },
        {
            "BR-M1-1: an active team name in the project",
            "INSERT INTO teams (id, project_id, name, normalized_name, status, is_first_team) "
            + "SELECT gen_random_uuid(), project_id, name, normalized_name, 'Active', false FROM teams WHERE id = @second",
            "ix_teams_project_id_normalized_name"
        },
        {
            "BR-M1-9: a second first team",
            "INSERT INTO teams (id, project_id, name, normalized_name, status, is_first_team) "
            + "VALUES (gen_random_uuid(), @project, 'Иная', 'ИНАЯ', 'Active', true)",
            "ix_teams_project_id_first_team"
        },
        {
            "BR-M1-12: a sibling name",
            "INSERT INTO areas (id, project_id, parent_id, name, normalized_name, owner_team_id, default_of_team_id) "
            + "SELECT gen_random_uuid(), project_id, parent_id, name, normalized_name, NULL, NULL FROM areas WHERE id = @secondArea",
            "ix_areas_parent_id_normalized_name"
        },
        {
            "BR-M1-11: a second root",
            "INSERT INTO areas (id, project_id, parent_id, name, normalized_name, owner_team_id, default_of_team_id) "
            + "VALUES (gen_random_uuid(), @project, NULL, 'Второй корень', 'ВТОРОЙ КОРЕНЬ', @first, NULL)",
            "ix_areas_project_id_root"
        },
        {
            "BR-M1-14: a second default area of one team",
            "INSERT INTO areas (id, project_id, parent_id, name, normalized_name, owner_team_id, default_of_team_id) "
            + "VALUES (gen_random_uuid(), @project, @root, 'Второе', 'ВТОРОЕ', @second, @second)",
            "ix_areas_default_of_team_id"
        },
    };

    public static TheoryData<string, string, string> CheckViolations() => new()
    {
        { "an unknown organization status", "UPDATE organizations SET status = 'Deleted' WHERE id = @org", "ck_organizations_status" },
        { "an empty organization name", "UPDATE organizations SET name = '' WHERE id = @org", "ck_organizations_name_length" },
        { "an unknown project status", "UPDATE projects SET status = 'Deleted' WHERE id = @project", "ck_projects_status" },
        { "an empty project name", "UPDATE projects SET name = '' WHERE id = @project", "ck_projects_name_length" },
        { "a lower-case key", "UPDATE projects SET key = 'prm' WHERE id = @project", "ck_projects_key_format" },
        { "a one-character key", "UPDATE projects SET key = 'P' WHERE id = @project", "ck_projects_key_format" },
        { "an unknown team status", "UPDATE teams SET status = 'Deactivated' WHERE id = @second", "ck_teams_status" },
        { "an empty team name", "UPDATE teams SET name = '' WHERE id = @second", "ck_teams_name_length" },
        { "a deleted first team", "UPDATE teams SET status = 'Deleted' WHERE id = @first", "ck_teams_first_team_active" },
        { "an empty area name", "UPDATE areas SET name = '' WHERE id = @secondArea", "ck_areas_name_length" },
        { "a default area owned by another team", "UPDATE areas SET owner_team_id = @first WHERE id = @secondArea", "ck_areas_default_owned" },
        { "a root with no owner", "UPDATE areas SET owner_team_id = NULL, default_of_team_id = NULL WHERE id = @root", "ck_areas_root_owned" },
    };

    // The expected key, or either of two when the row is referenced from two tables.
    public static TheoryData<string, string, string> RestrictedDeletes() => new()
    {
        {
            "a process an organization holds",
            "INSERT INTO processes (id, organization_id) VALUES (@bareProcess, gen_random_uuid()); "
            + "INSERT INTO organizations (id, name, normalized_name, status, process_id, created_at, created_by) "
            + "VALUES (gen_random_uuid(), 'Голая', 'ГОЛАЯ ' || @bareProcess::text, 'Active', @bareProcess, now(), 'x'); "
            + "DELETE FROM processes WHERE id = @bareProcess",
            "fk_organizations_processes_process_id"
        },
        { "an organization with projects", "DELETE FROM organizations WHERE id = @org", "fk_projects_organizations_organization_id" },
        { "a project with teams and areas", "DELETE FROM projects WHERE id = @project", "fk_teams_projects_project_id|fk_areas_projects_project_id" },
        { "a team that owns areas", "DELETE FROM teams WHERE id = @second", $"{OwnerKey}|{DefaultKey}" },
        { "an area with children", "DELETE FROM areas WHERE id = @root", ParentKey },
    };

    // qa.md D-1 (BR-M1-22): an area's owner and parent belong to the area's own project. @leaf is a child of A's root with
    // no owner; @otherTeam and @otherRoot are of another organization's project, @siblingTeam and @siblingRoot of another
    // project in A's organization. The default key needs no case of its own: ck_areas_default_owned makes it the owner.
    public static TheoryData<string, string, string> CrossProjectReferences() => new()
    {
        { "an area owned by another organization's team", InsertAreaUnder("@root", "@otherTeam"), OwnerKey },
        { "an area under another organization's area", InsertAreaUnder("@otherRoot", "NULL"), ParentKey },
        { "an area re-owned to another organization's team", ReownLeafTo("@otherTeam"), OwnerKey },
        { "an area owned by another project's team in the organization", InsertAreaUnder("@root", "@siblingTeam"), OwnerKey },
        { "an area under another project's area in the organization", InsertAreaUnder("@siblingRoot", "NULL"), ParentKey },
    };

    // The controls of the cases above: the same statements with the team or the parent of the area's own project.
    public static TheoryData<string, string> SameProjectReferences() => new()
    {
        { "an area owned by its project's team", InsertAreaUnder("@root", "@second") },
        { "an area under its project's area", InsertAreaUnder("@secondArea", "NULL") },
        { "an area re-owned to its project's team", ReownLeafTo("@second") },
    };

    [Theory]
    [MemberData(nameof(UniqueIndexViolations))]
    public async Task Write_Duplicate_IsRefusedByItsNamedUniqueIndex(string what, string sql, string index)
    {
        var parameters = await ParametersAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(sql, parameters));

        Assert.True(error.SqlState == UniqueViolation, $"{what}: expected a unique violation, got {error.SqlState} {error.MessageText}");
        Assert.Equal(index, error.ConstraintName);
    }

    [Theory]
    [MemberData(nameof(CheckViolations))]
    public async Task Write_BadRow_IsRefusedByItsNamedCheck(string what, string sql, string check)
    {
        var parameters = await ParametersAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(sql, parameters));

        Assert.True(error.SqlState == CheckViolation, $"{what}: expected a check violation, got {error.SqlState} {error.MessageText}");
        Assert.Equal(check, error.ConstraintName);
    }

    // doc-010 D-13 (nothing is purged) and plan-backend § 3: every key is Restrict.
    [Theory]
    [MemberData(nameof(RestrictedDeletes))]
    public async Task Delete_ReferencedRow_IsRestrictedByItsKey(string what, string sql, string keys)
    {
        var parameters = await ParametersAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(sql, parameters));

        Assert.True(
            error.SqlState is PostgresErrorCodes.RestrictViolation or PostgresErrorCodes.ForeignKeyViolation,
            $"{what}: expected the key to refuse the delete, got {error.SqlState} {error.MessageText}");
        Assert.Contains(error.ConstraintName, keys.Split('|'));
    }

    // BR-M1-1 (ADR-0026 rule 6): a deleted team's name is free again; the partial index covers active teams only.
    [Fact]
    public async Task Insert_NameOfADeletedTeam_IsAllowed()
    {
        var parameters = await ParametersAsync();

        await database.ExecuteAsync("UPDATE areas SET owner_team_id = @first, default_of_team_id = NULL WHERE owner_team_id = @second", parameters);
        await database.ExecuteAsync("UPDATE teams SET status = 'Deleted' WHERE id = @second", parameters);
        await database.ExecuteAsync(
            "INSERT INTO teams (id, project_id, name, normalized_name, status, is_first_team) "
            + "SELECT gen_random_uuid(), project_id, name, normalized_name, 'Active', false FROM teams WHERE id = @second",
            parameters);
    }

    // BR-M1-12: a child may carry the root's name, and the same name is free under another parent.
    [Fact]
    public async Task Insert_SameNameUnderAnotherParent_IsAllowed()
    {
        var parameters = await ParametersAsync();

        await database.ExecuteAsync(
            "INSERT INTO areas (id, project_id, parent_id, name, normalized_name, owner_team_id, default_of_team_id) "
            + "SELECT gen_random_uuid(), project_id, @secondArea, name, normalized_name, NULL, NULL FROM areas WHERE id = @root",
            parameters);
        await database.ExecuteAsync(
            "INSERT INTO areas (id, project_id, parent_id, name, normalized_name, owner_team_id, default_of_team_id) "
            + "SELECT gen_random_uuid(), project_id, @root, name, normalized_name, NULL, NULL FROM areas WHERE id = @root",
            parameters);
    }

    // qa.md D-1 (BR-M1-22, ADR-0011): nothing in one project refers to a team or an area of another, so the database refuses
    // it on its own. The keys of areas to teams and to the parent area are composite, (project_id, …) onto (project_id, id);
    // keys on id alone accepted each of these rows. Rolled back, so the row outlives the test neither way.
    [Theory]
    [MemberData(nameof(CrossProjectReferences))]
    public async Task Write_AreaReferringToAnotherProject_IsRefusedByItsCompositeKey(string what, string sql, string key)
    {
        var parameters = await CrossProjectParametersAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAndRollBackAsync(sql, parameters));

        Assert.True(
            error.SqlState == PostgresErrorCodes.ForeignKeyViolation,
            $"{what}: expected a foreign key violation, got {error.SqlState} {error.MessageText}");
        Assert.Equal(key, error.ConstraintName);
    }

    [Theory]
    [MemberData(nameof(SameProjectReferences))]
    public async Task Write_AreaReferringToItsOwnProject_IsAllowed(string what, string sql)
    {
        var parameters = await CrossProjectParametersAsync();

        var rows = await ExecuteAndRollBackAsync(sql, parameters);

        Assert.True(rows == 1, $"{what}: expected one row written, got {rows}");
    }

    private static string InsertAreaUnder(string parent, string owner) =>
        "INSERT INTO areas (id, project_id, parent_id, name, normalized_name, owner_team_id, default_of_team_id) "
        + $"VALUES (gen_random_uuid(), @project, {parent}, 'Чужая', 'ЧУЖАЯ', {owner}, NULL)";

    private static string ReownLeafTo(string owner) => $"UPDATE areas SET owner_team_id = {owner} WHERE id = @leaf";

    /// <summary>
    /// Structure A (the area's), a leaf of A's root, structure B of another organization, and a second project in A's
    /// organization, as qa.md D-1's steps build them.
    /// </summary>
    private async Task<(string Name, object Value)[]> CrossProjectParametersAsync()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        var sibling = await database.AddProjectAsync(structure.Organization.Id, "Соседний проект", "SIB");
        var leaf = Area.CreateChild(structure.Project.Root, OrganizationsDatabaseFixture.Name("Лист"), ownerTeam: null);
        await database.AddAreasAsync(leaf);

        return
        [
            ("project", structure.Project.Project.Id),
            ("second", structure.SecondTeam.Team.Id),
            ("root", structure.Project.Root.Id),
            ("secondArea", structure.SecondTeam.Area.Id),
            ("leaf", leaf.Id),
            ("otherTeam", other.SecondTeam.Team.Id),
            ("otherRoot", other.Project.Root.Id),
            ("siblingTeam", sibling.FirstTeam.Id),
            ("siblingRoot", sibling.Root.Id),
        ];
    }

    /// <summary>Runs <paramref name="sql"/> in a transaction of its own and rolls it back; returns the rows it wrote.</summary>
    private async Task<int> ExecuteAndRollBackAsync(string sql, (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(sql, connection, transaction);

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var rows = await command.ExecuteNonQueryAsync();
        await transaction.RollbackAsync();

        return rows;
    }

    private async Task<(string Name, object Value)[]> ParametersAsync()
    {
        var structure = await database.CreateStructureAsync();
        var otherOrganization = Guid.CreateVersion7();

        // A process of its own for the organization-name duplicate, so only the name index can fire.
        await using var provider = database.BuildServices();
        var otherProcess = await Process.ProcessDatabaseFixture.SeedInUnitOfWorkAsync(provider, otherOrganization);

        return
        [
            ("org", structure.Organization.Id),
            ("process", structure.Organization.ProcessId.Value),
            ("otherProcess", otherProcess.Value),
            ("bareProcess", Guid.CreateVersion7()),
            ("project", structure.Project.Project.Id),
            ("first", structure.Project.FirstTeam.Id),
            ("second", structure.SecondTeam.Team.Id),
            ("root", structure.Project.Root.Id),
            ("secondArea", structure.SecondTeam.Area.Id),
        ];
    }
}
