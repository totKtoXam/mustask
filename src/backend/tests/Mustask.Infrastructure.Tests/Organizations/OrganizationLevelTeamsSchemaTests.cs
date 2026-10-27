using System.Reflection;
using Mustask.Infrastructure.Organizations;
using Mustask.Infrastructure.WorkItems;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// feat-028 plan-backend § 4.1, § 9.4 (ADR-0028 rule 12): the storage-level backstops of the organization-level teams that
/// <c>OrganizationsSchemaTests</c> and <c>OwningTeamSchemaTests</c> do not build. Each case writes exactly one violation with raw SQL, so
/// exactly one guard can fire, and the test names it: a link across organizations, a second row for one pair, a team membership across
/// organizations, a name that is free in another organization; and the names the code maps to error codes all exist in the database.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class OrganizationLevelTeamsSchemaTests(OrganizationsDatabaseFixture database)
{
    // BR-OL-1 · a link holds its organization twice, once for the project and once for the team: a project of one organization and a
    // team of another cannot be joined, whichever organization the row says.
    [Theory]
    [InlineData("the project's organization")]
    [InlineData("the team's organization")]
    public async Task Link_AcrossOrganizations_IsRefusedByTheCompositeKeys(string organizationOf)
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        var organization = organizationOf == "the project's organization" ? structure.Organization.Id : other.Organization.Id;

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "INSERT INTO project_teams (project_id, team_id, organization_id, linked_by, linked_at) VALUES (@project, @team, @organization, 'test', now())",
            ("project", structure.Project.Id),
            ("team", other.SecondTeam.Id),
            ("organization", organization)));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal(
            organizationOf == "the project's organization"
                ? "fk_project_teams_teams_organization_id_team_id"
                : "fk_project_teams_projects_organization_id_project_id",
            error.ConstraintName);
    }

    // BR-OL-4 · one row per pair: the primary key refuses a second link of the same project and team, and the repository's
    // ON CONFLICT DO NOTHING is what turns that into "linked once".
    [Fact]
    public async Task Link_OfALinkedPair_IsRefusedByThePrimaryKey_AndOnConflictDoNothingKeepsTheFirst()
    {
        var structure = await database.CreateStructureAsync();
        var parameters = new (string Name, object Value)[]
        {
            ("project", structure.Project.Id),
            ("team", structure.SecondTeam.Id),
            ("organization", structure.Organization.Id),
        };

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "INSERT INTO project_teams (project_id, team_id, organization_id, linked_by, linked_at) VALUES (@project, @team, @organization, 'second', now())",
            parameters));
        await database.ExecuteAsync(
            "INSERT INTO project_teams (project_id, team_id, organization_id, linked_by, linked_at) VALUES (@project, @team, @organization, 'second', now()) ON CONFLICT DO NOTHING",
            parameters);

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal(OrganizationsConstraints.ProjectTeamPrimaryKey, error.ConstraintName);
        Assert.Equal(
            [OrganizationsDatabaseFixture.Creator],
            await database.QueryAsync("SELECT linked_by FROM project_teams WHERE project_id = @project AND team_id = @team", parameters));
    }

    // BR-M1-10, BR-OL-5 · a team's member is a member of the team's own organization: a team of one organization cannot hold a person
    // whose membership is in another (the key onto the team carries the organization).
    [Fact]
    public async Task TeamMembership_OfATeamInAnotherOrganization_IsRefusedByTheTeamKey()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        var user = $"schema-{Guid.NewGuid():N}";
        await database.AddMemberAsync(structure.Organization.Id, user);

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "INSERT INTO team_memberships (team_id, user_id, organization_id) VALUES (@team, @user, @organization)",
            ("team", other.SecondTeam.Id),
            ("user", user),
            ("organization", structure.Organization.Id)));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal(OrganizationsConstraints.TeamMembershipTeamKey, error.ConstraintName);
    }

    // BR-OL-3 · a name is unique among an organization's active teams, not among the instance's: the same name is held in two organizations
    // at once (both structures hold «Платформа»), and a team linked to no project holds it all the same.
    [Fact]
    public async Task TeamName_IsUniquePerOrganization_HeldInTwoAtOnce_HeldByAnUnlinkedTeam()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        var parameters = new (string Name, object Value)[] { ("team", structure.SecondTeam.Id), ("organization", structure.Organization.Id) };
        await database.ExecuteAsync("DELETE FROM project_teams WHERE team_id = @team", parameters);

        var held = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "INSERT INTO teams (id, organization_id, name, normalized_name, status) SELECT gen_random_uuid(), organization_id, name, normalized_name, 'Active' FROM teams WHERE id = @team",
            parameters));

        Assert.Equal(OrganizationsConstraints.TeamName, held.ConstraintName);
        Assert.Equal(structure.SecondTeam.Name, other.SecondTeam.Name);
        Assert.Equal(
            ["2"],
            await database.QueryAsync(
                "SELECT count(*)::text FROM teams WHERE normalized_name = (SELECT normalized_name FROM teams WHERE id = @team) AND status = 'Active' AND organization_id IN (@organization, @otherOrganization)",
                ("team", structure.SecondTeam.Id),
                ("organization", structure.Organization.Id),
                ("otherOrganization", other.Organization.Id)));
    }

    // BR-OL-12 · a project's team is the organization's: the table that held a team's project is gone from the shape, and the new one
    // holds the pair.
    [Fact]
    public async Task Schema_TeamHasNoProject_TheLinkHasThePair_AndBothDescriptionsAreChecked()
    {
        var columns = await database.QueryAsync(
            "SELECT table_name || '.' || column_name FROM information_schema.columns "
            + "WHERE (table_name = 'teams' AND column_name IN ('project_id', 'organization_id', 'description')) "
            + "OR (table_name = 'project_teams') OR (table_name = 'projects' AND column_name = 'description')");

        Assert.Equal(
            [
                "project_teams.linked_at",
                "project_teams.linked_by",
                "project_teams.organization_id",
                "project_teams.project_id",
                "project_teams.team_id",
                "projects.description",
                "teams.description",
                "teams.organization_id",
            ],
            columns.Select(column => column).Order(StringComparer.Ordinal));
    }

    // ADR-0028 rule 12 · every constraint or index name the code maps to an error code exists in the database: a renamed constraint
    // would turn a 409 into a 500 with no test noticing.
    [Fact]
    public async Task EveryConstraintNameTheCodeMaps_ExistsInTheDatabase()
    {
        var mapped = new[] { typeof(OrganizationsConstraints), typeof(WorkItemsConstraints) }
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();
        var existing = await database.QueryAsync(
            "SELECT conname FROM pg_constraint UNION SELECT indexname FROM pg_indexes WHERE schemaname = 'public'");

        Assert.NotEmpty(mapped);
        Assert.All(mapped, name => Assert.Contains(name, existing));
    }
}
