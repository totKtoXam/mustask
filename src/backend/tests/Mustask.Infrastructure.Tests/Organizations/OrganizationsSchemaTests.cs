using Mustask.Domain.Organizations;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// plan-backend § 3: the storage-level backstops of M1's rules. Each case
/// builds exactly one violation with raw SQL, so exactly one guard can fire,
/// and the test names it. The domain offers no way to build them; that is the
/// point of a backstop. mod-017: a team is not the first and an area owns nothing, so the first-team, owner and default-area
/// guards are gone (retired by name in the commit: BR-M1-9 «a second first team», BR-M1-11 «a root with no owner», BR-M1-14 «a
/// second default area of one team» and «a default area owned by another team», «a deleted first team», «a team that owns
/// areas», and the owner cases of the cross-project theory — BR-OT-10, BR-OT-11); the default-team setting has its own.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class OrganizationsSchemaTests(OrganizationsDatabaseFixture database)
{
    private const string UniqueViolation = PostgresErrorCodes.UniqueViolation;
    private const string CheckViolation = PostgresErrorCodes.CheckViolation;
    private const string DefaultTeamKey = "fk_projects_project_teams_id_default_team_id";
    private const string ParentKey = "fk_areas_areas_organization_id_parent_id";

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
            "feat-028 BR-OL-3: an active team name in the organization",
            "INSERT INTO teams (id, organization_id, name, normalized_name, status) "
            + "SELECT gen_random_uuid(), organization_id, name, normalized_name, 'Active' FROM teams WHERE id = @second",
            "ix_teams_organization_id_normalized_name"
        },
        {
            "BR-M1-12: a sibling name",
            "INSERT INTO areas (id, organization_id, parent_id, name, normalized_name, inherits_access, created_by, created_at) "
            + "SELECT gen_random_uuid(), organization_id, parent_id, name, normalized_name, true, 'x', now() FROM areas WHERE id = @secondArea",
            "ix_areas_organization_id_parent_id_normalized_name"
        },
        {
            "BR-AW-4: a second top-level area of one name (top-level areas are siblings, nulls are not distinct)",
            "INSERT INTO areas (id, organization_id, parent_id, name, normalized_name, inherits_access, created_by, created_at) "
            + "SELECT gen_random_uuid(), organization_id, NULL, name, normalized_name, true, 'x', now() FROM areas WHERE id = @root",
            "ix_areas_organization_id_parent_id_normalized_name"
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
        { "an empty team description (the empty string is stored as NULL)", "UPDATE teams SET description = '' WHERE id = @second", "ck_teams_description_length" },
        { "a team description over 2 000 characters", "UPDATE teams SET description = repeat('я', 2001) WHERE id = @second", "ck_teams_description_length" },
        { "a project description over 2 000 characters", "UPDATE projects SET description = repeat('я', 2001) WHERE id = @project", "ck_projects_description_length" },
        { "an empty area name", "UPDATE areas SET name = '' WHERE id = @secondArea", "ck_areas_name_length" },
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
        { "a project with links and areas", "DELETE FROM projects WHERE id = @project", "fk_project_teams_projects_organization_id_project_id|fk_area_projects_projects_organization_id_project_id" },
        { "a team a project is linked to", "DELETE FROM teams WHERE id = @second", "fk_project_teams_teams_organization_id_team_id" },
        {
            "a link the project names as its default",
            "UPDATE projects SET default_team_id = @second WHERE id = @project; DELETE FROM project_teams WHERE project_id = @project AND team_id = @second",
            DefaultTeamKey
        },
        { "an area with children", "DELETE FROM areas WHERE id = @root", ParentKey },
        { "a project an area is linked to", "INSERT INTO area_projects (area_id, project_id, organization_id, linked_by, linked_at) VALUES (@root, @project, @org, 'x', now()); DELETE FROM project_teams WHERE project_id = @project; DELETE FROM projects WHERE id = @project", "fk_area_projects_projects_organization_id_project_id" },
    };

    // qa.md D-1 (BR-M1-22): an area's parent belongs to the area's own project, and a project's default team is a team of that
    // project (mod-017 BR-OT-4). @otherRoot and @otherTeam are of another organization, @siblingRoot is of another project in A's organization
    // and @siblingTeam is a team of A's organization that is NOT linked to the project (feat-028 BR-OL-6).
    public static TheoryData<string, string, string> CrossProjectReferences() => new()
    {
        { "an area under another organization's area", InsertAreaUnder("@otherRoot"), ParentKey },
        { "a project naming another organization's team as its default", NameDefault("@otherTeam"), DefaultTeamKey },
        { "a project naming a team of the organization that is not linked to it as its default", NameDefault("@siblingTeam"), DefaultTeamKey },
    };

    // The controls of the cases above: the same statements with the parent or the team of the row's own project.
    public static TheoryData<string, string> SameProjectReferences() => new()
    {
        { "an area under an area of its organization", InsertAreaUnder("@secondArea") },
        { "an area linked to a project of its organization", LinkAreaProject("@project") },
        { "a project naming its own team as its default", NameDefault("@second") },
        { "a project naming no default", NameDefault("NULL") },
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

        await database.ExecuteAsync("UPDATE teams SET status = 'Deleted' WHERE id = @second", parameters);
        await database.ExecuteAsync(
            "INSERT INTO teams (id, organization_id, name, normalized_name, status) "
            + "SELECT gen_random_uuid(), organization_id, name, normalized_name, 'Active' FROM teams WHERE id = @second",
            parameters);
    }

    // BR-M1-12: a child may carry the root's name, and the same name is free under another parent.
    [Fact]
    public async Task Insert_SameNameUnderAnotherParent_IsAllowed()
    {
        var parameters = await ParametersAsync();

        await database.ExecuteAsync(
            "INSERT INTO areas (id, organization_id, parent_id, name, normalized_name, inherits_access, created_by, created_at) "
            + "SELECT gen_random_uuid(), organization_id, @secondArea, name, normalized_name, true, 'x', now() FROM areas WHERE id = @root",
            parameters);
        await database.ExecuteAsync(
            "INSERT INTO areas (id, organization_id, parent_id, name, normalized_name, inherits_access, created_by, created_at) "
            + "SELECT gen_random_uuid(), organization_id, @root, name, normalized_name, true, 'x', now() FROM areas WHERE id = @root",
            parameters);
    }

    // mod-017 BR-OT-11: no team is the first, so a project's team goes like any other — the database holds no marker that
    // forbids it (the first-team check and index are gone).
    [Fact]
    public async Task Update_AnyTeamDeleted_IsAllowed()
    {
        var parameters = await ParametersAsync();

        await database.ExecuteAsync("UPDATE teams SET status = 'Deleted' WHERE id = @first", parameters);
    }

    // mod-017 BR-OT-10: the owner and default columns of an area, and the first-team marker of a team, are gone.
    [Fact]
    public async Task Schema_HasNoAreaOwnerNoDefaultOfTeamAndNoFirstTeamMarker()
    {
        var columns = await database.QueryAsync(
            "SELECT table_name || '.' || column_name FROM information_schema.columns "
            + "WHERE (table_name = 'areas' AND column_name IN ('owner_team_id', 'default_of_team_id')) "
            + "OR (table_name = 'teams' AND column_name = 'is_first_team')");

        Assert.Empty(columns);
    }

    // qa.md D-1 (BR-M1-22, ADR-0011): nothing in one project refers to a team or an area of another, so the database refuses
    // it on its own. The keys of areas to the parent area and of projects to their default team are composite, (project_id, …)
    // onto (project_id, id); keys on id alone accepted each of these rows. Rolled back, so the row outlives the test neither way.
    [Theory]
    [MemberData(nameof(CrossProjectReferences))]
    public async Task Write_RowReferringToAnotherProject_IsRefusedByItsCompositeKey(string what, string sql, string key)
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
    public async Task Write_RowReferringToItsOwnProject_IsAllowed(string what, string sql)
    {
        var parameters = await CrossProjectParametersAsync();

        var rows = await ExecuteAndRollBackAsync(sql, parameters);

        Assert.True(rows == 1, $"{what}: expected one row written, got {rows}");
    }

    private static string InsertAreaUnder(string parent) =>
        "INSERT INTO areas (id, organization_id, parent_id, name, normalized_name, inherits_access, created_by, created_at) "
        + $"VALUES (gen_random_uuid(), @org, {parent}, 'Чужая', 'ЧУЖАЯ', true, 'x', now())";

    private static string LinkAreaProject(string project, string area = "@root") =>
        $"INSERT INTO area_projects (area_id, project_id, organization_id, linked_by, linked_at) VALUES ({area}, {project}, @org, 'x', now())";

    private static string NameDefault(string team) => $"UPDATE projects SET default_team_id = {team} WHERE id = @project";

    /// <summary>
    /// Structure A (the row's), a plain area «Платформа» under its root, structure B of another organization, and a second
    /// project in A's organization, as qa.md D-1's steps build them.
    /// </summary>
    private async Task<(string Name, object Value)[]> CrossProjectParametersAsync()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        var unlinkedTeam = Team.Create(structure.Organization.Id, OrganizationsDatabaseFixture.Name("Не связана"));
        await using (var context = database.NewContext())
        {
            context.Teams.Add(unlinkedTeam);
            await context.SaveChangesAsync();
        }

        var root = OrganizationsDatabaseFixture.NewArea(structure.Organization.Id, null, "Корень");
        var secondArea = OrganizationsDatabaseFixture.NewArea(structure.Organization.Id, root.Id, "Платформа");
        var otherRoot = OrganizationsDatabaseFixture.NewArea(other.Organization.Id, null, "Чужой корень");
        await database.AddAreasAsync(root, secondArea, otherRoot);

        return
        [
            ("org", structure.Organization.Id),
            ("project", structure.Project.Id),
            ("second", structure.SecondTeam.Id),
            ("root", root.Id),
            ("secondArea", secondArea.Id),
            ("otherTeam", other.SecondTeam.Id),
            ("otherRoot", otherRoot.Id),
            ("otherProject", other.Project.Id),
            ("siblingTeam", unlinkedTeam.Id),
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
        var root = OrganizationsDatabaseFixture.NewArea(structure.Organization.Id, null, "Корень");
        var secondArea = OrganizationsDatabaseFixture.NewArea(structure.Organization.Id, root.Id, "Платформа");
        await database.AddAreasAsync(root, secondArea);

        // A process of its own for the organization-name duplicate, so only the name index can fire.
        await using var provider = database.BuildServices();
        var otherProcess = await Process.ProcessDatabaseFixture.SeedInUnitOfWorkAsync(provider, otherOrganization);

        return
        [
            ("org", structure.Organization.Id),
            ("process", structure.Organization.ProcessId.Value),
            ("otherProcess", otherProcess.Value),
            ("bareProcess", Guid.CreateVersion7()),
            ("project", structure.Project.Id),
            ("first", structure.FirstTeam.Id),
            ("second", structure.SecondTeam.Id),
            ("root", root.Id),
            ("secondArea", secondArea.Id),
        ];
    }
}
