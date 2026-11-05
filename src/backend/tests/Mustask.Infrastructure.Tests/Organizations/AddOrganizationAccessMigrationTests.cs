using Mustask.Infrastructure.Tests.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// mod-016 US-6 · "Existing organizations get their first administrator" (BR-M1-25, status.md D-3): the migration
/// <c>AddOrganizationAccess</c> gives every organization that exists when it runs exactly one membership — its recorded creator, as
/// admin, added by the creator at the organization's creation time — and each project's creator joins its first team when they
/// are then a member of its organization. Nothing else is seeded. It runs on a database with nothing to seed, reverts leaving
/// organizations, projects, teams and areas exactly as they were, and re-applies. The database is this class's own: the test rolls
/// a migration back.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class AddOrganizationAccessMigrationTests(OrganizationsDatabaseFixture database) : IClassFixture<OrganizationsDatabaseFixture>
{
    private const string MigrationSuffix = "_AddOrganizationAccess";

    [Fact]
    public async Task Migrator_AddOrganizationAccess_SeedsEachCreator_EmptyDatabaseIsFine_RevertsAndReapplies()
    {
        // Scenario "Running the migration on a database with nothing to seed": it succeeds and creates no membership.
        await RollBackAsync();
        await MigrateForwardAsync();

        Assert.Equal("0", Assert.Single(await database.QueryAsync("SELECT count(*) FROM organization_memberships")));
        Assert.Equal("0", Assert.Single(await database.QueryAsync("SELECT count(*) FROM team_memberships")));

        await RollBackAsync();

        // The data of the scenario, written while the tables do not exist (the organization rows are the old schema's).
        var a = await database.CreateLegacyStructureAsync(organizationName: OrganizationsDatabaseFixture.UniqueName("А"), projectName: "Пример проекта", key: "PRM");
        var b = await database.CreateLegacyStructureAsync(organizationName: OrganizationsDatabaseFixture.UniqueName("Б"), projectName: "Архив", key: "ARC");
        var c = await database.CreateLegacyStructureAsync(organizationName: OrganizationsDatabaseFixture.UniqueName("В"), projectName: "Тень", key: "SHD");
        var reports = await database.AddLegacyProjectAsync(a.OrganizationId, "Отчёты", "RPT");

        // A, active, by Anna; its project «Пример проекта» by Anna and «Отчёты» by Oleg, who is not a member of A.
        // B, deactivated, by Ivan; C by a creator no projection ever received (whatever the projection says — D-10).
        await database.ExecuteAsync("UPDATE organizations SET created_by = 'anna' WHERE id = @id", ("id", a.OrganizationId));
        await database.ExecuteAsync("UPDATE projects SET created_by = 'anna' WHERE id = @id", ("id", a.ProjectId));
        await database.ExecuteAsync("UPDATE projects SET created_by = 'oleg' WHERE id = @id", ("id", reports.ProjectId));
        await database.ExecuteAsync(
            "UPDATE organizations SET created_by = 'ivan', status = 'Deactivated' WHERE id = @id", ("id", b.OrganizationId));
        await database.ExecuteAsync("UPDATE projects SET created_by = 'ivan' WHERE id = @id", ("id", b.ProjectId));
        await database.ExecuteAsync(
            "UPDATE organizations SET created_by = 'ghost-never-projected' WHERE id = @id", ("id", c.OrganizationId));
        await database.ExecuteAsync(
            "UPDATE projects SET created_by = 'ghost-never-projected' WHERE id = @id", ("id", c.ProjectId));

        var structureBefore = await StructureFingerprintAsync();

        await MigrateForwardAsync();

        // Each existing organization has exactly one membership: its creator, admin, added by them at the creation time, no
        // role change — the deactivated one and the one whose creator the projection never knew included (D-3).
        var memberships = await database.QueryAsync(
            """
            SELECT concat_ws('|', o.id, m.user_id, m.role, m.added_by, (m.added_at = o.created_at)::text,
                coalesce(m.role_changed_by, '-'), coalesce(m.role_changed_at::text, '-'))
            FROM organization_memberships m JOIN organizations o ON o.id = m.organization_id
            WHERE o.id = ANY(@orgs)
            ORDER BY o.name
            """,
            ("orgs", new[] { a.OrganizationId, b.OrganizationId, c.OrganizationId }));

        Assert.Equal(
            [
                $"{a.OrganizationId}|anna|admin|anna|true|-|-",
                $"{b.OrganizationId}|ivan|admin|ivan|true|-|-",
                $"{c.OrganizationId}|ghost-never-projected|admin|ghost-never-projected|true|-|-",
            ],
            memberships);

        // A project's creator joins its first team only when they are then a member of its organization: «Пример проекта» gets
        // Anna, «Отчёты» (by Oleg, not a member of A) gets nobody, B's and C's projects get their creators; no other team —
        // not the second team «Платформа», not a first team of another project — gains a member.
        var teamMemberships = await database.QueryAsync(
            """
            SELECT concat_ws('|', t.name, tm.user_id, (tm.organization_id = p.organization_id)::text)
            FROM team_memberships tm JOIN teams t ON t.id = tm.team_id JOIN projects p ON p.id = t.project_id
            WHERE p.organization_id = ANY(@orgs)
            ORDER BY t.name
            """,
            ("orgs", new[] { a.OrganizationId, b.OrganizationId, c.OrganizationId }));

        Assert.Equal(
            ["Архив|ivan|true", "Пример проекта|anna|true", "Тень|ghost-never-projected|true"],
            teamMemberships);
        Assert.Equal(
            "0",
            Assert.Single(await database.QueryAsync(
                "SELECT count(*) FROM team_memberships WHERE team_id = ANY(@teams)",
                ("teams", new[] { a.SecondTeamId, reports.FirstTeamId }))));

        // Nobody else holds a membership in A, B or C (D-3).
        Assert.Equal(
            "3",
            Assert.Single(await database.QueryAsync(
                "SELECT count(*) FROM organization_memberships WHERE organization_id = ANY(@orgs)",
                ("orgs", new[] { a.OrganizationId, b.OrganizationId, c.OrganizationId }))));

        // Scenario "The migration can be reverted": the membership data is gone and the structure is exactly as before.
        await RollBackAsync();

        Assert.False(await TableExistsAsync("organization_memberships"));
        Assert.False(await TableExistsAsync("team_memberships"));
        Assert.Equal(structureBefore, await StructureFingerprintAsync());

        // Re-applies, and seeds the same again.
        await MigrateForwardAsync();

        Assert.Equal(
            "3",
            Assert.Single(await database.QueryAsync(
                "SELECT count(*) FROM organization_memberships WHERE organization_id = ANY(@orgs)",
                ("orgs", new[] { a.OrganizationId, b.OrganizationId, c.OrganizationId }))));
        Assert.Equal(structureBefore, await StructureFingerprintAsync());
    }

    /// <summary>Migrates to the migration before AddOrganizationAccess, found by name, never hard-coded.</summary>
    private async Task RollBackAsync()
    {
        await using var context = database.NewContext();

        var previous = context.Database.GetMigrations()
            .TakeWhile(id => !id.EndsWith(MigrationSuffix, StringComparison.Ordinal))
            .Last();

        await context.GetService<IMigrator>().MigrateAsync(previous);
    }

    /// <summary>
    /// Migrates forward to <c>AddOrganizationAccess</c> itself, never to head (feat-028 plan-backend § 9.1b): the legacy rows below are
    /// written in the old shape, and <c>OrganizationLevelTeams</c> would drop exactly the teams and memberships the test reads.
    /// </summary>
    private async Task MigrateForwardAsync()
    {
        await using var context = database.NewContext();
        var target = context.Database.GetMigrations().Single(id => id.EndsWith(MigrationSuffix, StringComparison.Ordinal));

        await context.GetService<IMigrator>().MigrateAsync(target);
    }

    private async Task<bool> TableExistsAsync(string table) =>
        Assert.Single(await database.QueryAsync("SELECT to_regclass(@table)::text", ("table", table))) != "NULL";

    /// <summary>
    /// A digest of everything the migration must leave alone: organizations, projects, teams and areas, column by column.
    /// The columns mod-017 adds and drops (the area owner, the default area, the first-team flag) are left out: they are the
    /// schema's of another line, and its rollback restores them for the rows' own state, not the dropped markers (OQ-4 A).
    /// </summary>
    private async Task<string> StructureFingerprintAsync() =>
        Assert.Single(await database.QueryAsync(
            """
            SELECT md5(
                coalesce((SELECT string_agg(o::text, ';' ORDER BY o.id) FROM organizations o), '') || '#' ||
                coalesce((SELECT string_agg(concat_ws('|', p.id, p.organization_id, p.name, p.normalized_name, p.key, p.status, p.created_at, p.created_by), ';' ORDER BY p.id) FROM projects p), '') || '#' ||
                coalesce((SELECT string_agg(concat_ws('|', t.id, t.project_id, t.name, t.normalized_name, t.status), ';' ORDER BY t.id) FROM teams t), '') || '#' ||
                coalesce((SELECT string_agg(concat_ws('|', a.id, a.project_id, a.parent_id, a.name, a.normalized_name), ';' ORDER BY a.id) FROM areas a), ''))
            """));
}
