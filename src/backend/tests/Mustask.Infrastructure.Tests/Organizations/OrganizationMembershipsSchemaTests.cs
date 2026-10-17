using Mustask.Domain.Organizations;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// mod-016 plan-backend § 3.3: the storage-level backstops of the membership rules. Each case builds exactly one violation with
/// raw SQL, so exactly one guard can fire, and the test names it: one membership per user per organization (BR-M1-5), a role of
/// the three, the pair of «role changed» columns, the foreign keys, a team member who is an organization member (BR-M1-10) and
/// the cascade that ends a removed member's team memberships (BR-M1-36).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class OrganizationMembershipsSchemaTests(OrganizationsDatabaseFixture database)
{
    private const string Insert =
        "INSERT INTO organization_memberships (organization_id, user_id, role, added_by, added_at) VALUES (@org, @user, @role, 'admin-anna', now())";

    private static (string Name, object Value)[] Parameters(SeededStructure structure, string user = "u-schema", string role = "member") =>
    [
        ("org", structure.Organization.Id),
        ("user", user),
        ("role", role),
        ("team", structure.SecondTeam.Id),
        ("first", structure.Project.Team.Id),
    ];

    // BR-M1-5: one membership per user per organization — the named primary key.
    [Fact]
    public async Task Insert_ASecondMembershipOfOneUser_IsRefusedByTheNamedPrimaryKey()
    {
        var structure = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-schema");

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(Insert, Parameters(structure)));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("pk_organization_memberships", error.ConstraintName);
    }

    // The same user may belong to any number of organizations.
    [Fact]
    public async Task Insert_TheSameUserInAnotherOrganization_IsAllowed()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-schema");

        await database.ExecuteAsync(Insert, Parameters(other));
    }

    // ADR-0027 rule 2 / api-contract § 1.4: the role is exactly one of the three.
    [Theory]
    [InlineData("owner")]
    [InlineData("Admin")]
    [InlineData("")]
    public async Task Insert_ARoleOutsideTheThree_IsRefusedByItsNamedCheck(string role)
    {
        var structure = await database.CreateStructureAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(Insert, Parameters(structure, role: role)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_organization_memberships_role", error.ConstraintName);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("member")]
    [InlineData("viewer")]
    public async Task Insert_EachOfTheThreeRoles_IsAllowed(string role)
    {
        var structure = await database.CreateStructureAsync();

        await database.ExecuteAsync(Insert, Parameters(structure, role: role));
    }

    // BR-M1-16: who changed the role and when are both set or both null.
    [Theory]
    [InlineData("UPDATE organization_memberships SET role_changed_by = 'admin-anna' WHERE organization_id = @org AND user_id = @user")]
    [InlineData("UPDATE organization_memberships SET role_changed_at = now() WHERE organization_id = @org AND user_id = @user")]
    public async Task Update_OneOfTheTwoRoleChangedColumns_IsRefusedByItsNamedCheck(string sql)
    {
        var structure = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-schema");

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(sql, Parameters(structure)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_organization_memberships_role_change", error.ConstraintName);
    }

    [Fact]
    public async Task Update_BothRoleChangedColumns_IsAllowed()
    {
        var structure = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-schema");

        await database.ExecuteAsync(
            "UPDATE organization_memberships SET role = 'viewer', role_changed_by = 'admin-anna', role_changed_at = now() "
            + "WHERE organization_id = @org AND user_id = @user",
            Parameters(structure));
    }

    // plan-backend § 3.3: a membership belongs to an organization that exists; organizations are never purged (Restrict).
    [Fact]
    public async Task Insert_AnUnknownOrganization_IsRefusedByItsForeignKey()
    {
        var structure = await database.CreateStructureAsync();
        var parameters = Parameters(structure).Select(pair => pair.Name == "org" ? ("org", (object)Guid.CreateVersion7()) : pair).ToArray();

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(Insert, parameters));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal("fk_organization_memberships_organizations_organization_id", error.ConstraintName);
    }

    [Fact]
    public async Task Delete_AnOrganizationThatHasMembers_IsRestrictedByTheMembershipsKey()
    {
        var process = Guid.CreateVersion7();
        var organization = Guid.CreateVersion7();

        // A bare organization (no project), so that the memberships' key is the only one in the way.
        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "INSERT INTO processes (id, organization_id) VALUES (@process, @org); "
            + "INSERT INTO organizations (id, name, normalized_name, status, process_id, created_at, created_by) "
            + "VALUES (@org, 'Голая', 'ГОЛАЯ ' || @org::text, 'Active', @process, now(), 'x'); "
            + "INSERT INTO organization_memberships (organization_id, user_id, role, added_by, added_at) "
            + "VALUES (@org, 'u-schema', 'admin', 'u-schema', now()); "
            + "DELETE FROM organizations WHERE id = @org",
            [("process", process), ("org", organization)]));

        Assert.True(error.SqlState is PostgresErrorCodes.RestrictViolation or PostgresErrorCodes.ForeignKeyViolation);
        Assert.Equal("fk_organization_memberships_organizations_organization_id", error.ConstraintName);
    }

    // BR-M1-5 / BR-M1-17: the user id has no foreign key to the user projection — a membership outlives the account and a
    // creator the projection does not know yet must be storable (BR-M1-25).
    [Fact]
    public async Task Insert_AUserTheProjectionDoesNotKnow_IsAllowed()
    {
        var structure = await database.CreateStructureAsync();

        await database.ExecuteAsync(Insert, Parameters(structure, user: "never-projected-" + Guid.NewGuid()));
    }

    // BR-M1-10: a team member is an organization member — the composite key.
    [Fact]
    public async Task Insert_ATeamMemberWhoIsNotAnOrganizationMember_IsRefusedByTheCompositeKey()
    {
        var structure = await database.CreateStructureAsync();
        var parameters = Parameters(structure);

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "INSERT INTO team_memberships (team_id, organization_id, user_id) VALUES (@team, @org, @user)", parameters));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal("fk_team_memberships_organization_memberships", error.ConstraintName);
    }

    // The user is a member of ANOTHER organization only: still refused — the key names the organization too.
    [Fact]
    public async Task Insert_ATeamMemberWhoIsAMemberOfAnotherOrganizationOnly_IsRefused()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        await database.AddMemberAsync(other.Organization.Id, "u-schema");

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "INSERT INTO team_memberships (team_id, organization_id, user_id) VALUES (@team, @org, @user)", Parameters(structure)));

        Assert.Equal("fk_team_memberships_organization_memberships", error.ConstraintName);
    }

    // A user is in a team at most once — the named primary key.
    [Fact]
    public async Task Insert_TheSameUserTwiceInOneTeam_IsRefusedByTheNamedPrimaryKey()
    {
        var structure = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-schema");
        await database.AddTeamMemberAsync(structure.Organization.Id, structure.SecondTeam.Id, "u-schema");

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "INSERT INTO team_memberships (team_id, organization_id, user_id) VALUES (@team, @org, @user)", Parameters(structure)));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("pk_team_memberships", error.ConstraintName);
    }

    // plan-backend § 3.3: a team is Restrict — teams are soft-deleted, and the deleting handler ends their memberships itself.
    [Fact]
    public async Task Delete_ATeamThatHasMembers_IsRestricted()
    {
        var structure = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-schema");
        await database.AddTeamMemberAsync(structure.Organization.Id, structure.SecondTeam.Id, "u-schema");

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => database.ExecuteAsync("DELETE FROM teams WHERE id = @team", Parameters(structure)));

        Assert.Equal("fk_team_memberships_teams_team_id", error.ConstraintName);
    }

    // BR-M1-36: removing an organization membership ends the user's memberships in every team of the organization, in the same
    // statement; other users keep theirs, and the same user's memberships in another organization are untouched.
    [Fact]
    public async Task Delete_AnOrganizationMembership_CascadesToThatUsersTeamMembershipsOnly()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        var organizationId = structure.Organization.Id;

        foreach (var user in new[] { "u-leaving", "u-staying" })
        {
            await database.AddMemberAsync(organizationId, user);
        }

        await database.AddMemberAsync(other.Organization.Id, "u-leaving");
        await database.AddTeamMemberAsync(organizationId, structure.SecondTeam.Id, "u-leaving");
        await database.AddTeamMemberAsync(organizationId, structure.Project.Team.Id, "u-leaving");
        await database.AddTeamMemberAsync(organizationId, structure.SecondTeam.Id, "u-staying");
        await database.AddTeamMemberAsync(other.Organization.Id, other.SecondTeam.Id, "u-leaving");

        await database.ExecuteAsync(
            "DELETE FROM organization_memberships WHERE organization_id = @org AND user_id = 'u-leaving'", [("org", organizationId)]);

        var remaining = await database.QueryAsync(
            "SELECT user_id || '@' || organization_id::text FROM team_memberships WHERE organization_id = ANY(@orgs) ORDER BY 1",
            ("orgs", new[] { organizationId, other.Organization.Id }));

        Assert.Equal(
            new[] { $"u-leaving@{other.Organization.Id}", $"u-staying@{organizationId}" }.Order(StringComparer.Ordinal),
            remaining);
    }
}
