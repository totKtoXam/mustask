using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Application.Abstractions;
using Mustask.Application.Features.Process;
using Mustask.Domain.Organizations;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// feat-041 (doc-016 OQ-5 A): <c>ProjectVisibleToOrganization</c> adds the column off for every existing project and logs, per project,
/// the members who saw it before and do not after. The cast: organization «Ромашка» with the department X; the project PA1 in X (its
/// node inherits), PA2 in X (switch off) and PA3 at the top level; members: the administrator, G (a grant on PA3), T (in a team linked
/// to PA3), D (a grant on X) and N (nothing). Every test has a database of its own, migrated to the migration before this one.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class ProjectVisibleToOrganizationMigrationTests(OrganizationsDatabaseFixture database) : IClassFixture<OrganizationsDatabaseFixture>
{
    private const string Migration = "_ProjectVisibleToOrganization";
    private static readonly DateTimeOffset Epoch = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    // OQ-5 A · the log names, per project, the members who lose sight; an administrator and a member who still reaches the project are not named.
    [Fact]
    public async Task Upgrade_LogsEveryMemberWhoLosesSight_PerProject()
    {
        var connectionString = await SeedLegacyAsync();

        var notices = await MigrateAsync(connectionString, null);

        Assert.Equal(
            ["PA1|G", "PA1|N", "PA1|T", "PA2|D", "PA2|G", "PA2|N", "PA2|T", "PA3|D", "PA3|N"],
            await QueryAsync(connectionString, "SELECT project_key || '|' || user_id FROM project_sight_upgrade_log ORDER BY project_key, user_id COLLATE \"C\""));
        Assert.Contains(notices, notice => notice.Contains("PA2", StringComparison.Ordinal) && notice.Contains("4 members lose sight", StringComparison.Ordinal));
    }

    // OQ-5 A · every existing project is off after the upgrade; the down step restores the old shape.
    [Fact]
    public async Task Upgrade_SwitchesEveryExistingProjectOff_AndDownRemovesTheColumnAndTheLog()
    {
        var connectionString = await SeedLegacyAsync();

        await MigrateAsync(connectionString, null);

        Assert.Equal(["0"], await QueryAsync(connectionString, "SELECT count(*) FROM projects WHERE visible_to_organization"));
        Assert.Equal(["3"], await QueryAsync(connectionString, "SELECT count(*) FROM projects"));

        await MigrateAsync(connectionString, "previous");

        Assert.Equal([""], await QueryAsync(connectionString, "SELECT coalesce(to_regclass('project_sight_upgrade_log')::text, '')"));
        Assert.Equal(["0"], await QueryAsync(connectionString, "SELECT count(*) FROM information_schema.columns WHERE table_name = 'projects' AND column_name = 'visible_to_organization'"));
    }

    // --- Seeding -------------------------------------------------------------------------------------------------------

    private async Task<string> SeedLegacyAsync()
    {
        var name = $"legacy_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await create.ExecuteNonQueryAsync();
        }

        var cs = new NpgsqlConnectionStringBuilder(database.ConnectionString) { Database = name, Pooling = false }.ConnectionString;

        await MigrateAsync(cs, "previous");

        var organization = await CreateOrganizationAsync(cs, "Ромашка");
        var department = Guid.CreateVersion7();

        await ExecuteAsync(
            cs,
            """
            INSERT INTO areas (id, organization_id, parent_id, type, project_id, name, normalized_name, inherits_access, created_by, created_at, color)
            VALUES (@d, @o, NULL, 1, NULL, 'X', 'X', true, 'legacy-0', @at, 'blue')
            """,
            ("d", department), ("o", organization), ("at", Epoch));

        await InsertProjectAsync(cs, organization, "PA1", department, inherits: true);
        await InsertProjectAsync(cs, organization, "PA2", department, inherits: false);
        var p3 = await InsertProjectAsync(cs, organization, "PA3", null, inherits: true);

        foreach (var (user, role) in new[] { ("admin", "admin"), ("G", "member"), ("T", "member"), ("D", "member"), ("N", "member") })
        {
            await ExecuteAsync(
                cs,
                "INSERT INTO organization_memberships (organization_id, user_id, role, added_by, added_at) VALUES (@o, @u, @r, 'legacy-0', @at)",
                ("o", organization), ("u", user), ("r", role), ("at", Epoch));
        }

        await ExecuteAsync(
            cs,
            "INSERT INTO project_grants (project_id, user_id, organization_id, role, granted_by, granted_at) VALUES (@p, 'G', @o, 'reader', 'legacy-0', @at)",
            ("p", p3), ("o", organization), ("at", Epoch));
        await ExecuteAsync(
            cs,
            "INSERT INTO area_grants (area_id, user_id, organization_id, role, granted_by, granted_at) VALUES (@a, 'D', @o, 'reader', 'legacy-0', @at)",
            ("a", department), ("o", organization), ("at", Epoch));

        var team = Guid.CreateVersion7();

        await ExecuteAsync(
            cs,
            "INSERT INTO teams (id, organization_id, name, normalized_name, status, department_id, color) VALUES (@t, @o, 'Team', 'TEAM', 'Active', @d, 'blue')",
            ("t", team), ("o", organization), ("d", department));
        await ExecuteAsync(
            cs,
            """
            INSERT INTO project_teams (project_id, team_id, organization_id, linked_by, linked_at) VALUES (@p, @t, @o, 'legacy-0', @at);
            INSERT INTO team_memberships (team_id, user_id, organization_id) VALUES (@t, 'T', @o)
            """,
            ("p", p3), ("t", team), ("o", organization), ("at", Epoch));

        return cs;
    }

    private static async Task<Guid> InsertProjectAsync(string cs, Guid organization, string key, Guid? department, bool inherits)
    {
        var project = Guid.CreateVersion7();

        await ExecuteAsync(
            cs,
            """
            INSERT INTO projects (id, organization_id, name, normalized_name, key, status, created_at, created_by, color)
            VALUES (@p, @o, @key, upper(@key), @key, 'Active', @at, 'legacy-0', 'blue');
            INSERT INTO areas (id, organization_id, parent_id, type, project_id, name, normalized_name, inherits_access, created_by, created_at, color)
            VALUES (gen_random_uuid(), @o, @dept, 2, @p, @key, upper(@key), @inherits, 'legacy-0', @at, NULL)
            """,
            ("p", project), ("o", organization), ("key", key), ("dept", (object?)department ?? DBNull.Value), ("inherits", inherits), ("at", Epoch));

        return project;
    }

    private static async Task<List<string>> MigrateAsync(string connectionString, string? target)
    {
        await using var context = new MustaskDbContext(new DbContextOptionsBuilder<MustaskDbContext>().UseNpgsql(connectionString).Options);
        var notices = new List<string>();

        ((NpgsqlConnection)context.Database.GetDbConnection()).Notice += (_, e) =>
        {
            if (e.Notice.MessageText.StartsWith("feat-041:", StringComparison.Ordinal))
            {
                notices.Add(e.Notice.MessageText);
            }
        };

        var migrations = context.Database.GetMigrations().ToList();
        var id = target is null ? null : migrations.TakeWhile(candidate => !candidate.EndsWith(Migration, StringComparison.Ordinal)).Last();

        await context.GetService<IMigrator>().MigrateAsync(id);

        return notices;
    }

    private async Task<Guid> CreateOrganizationAsync(string connectionString, string name)
    {
        await using var provider = database.BuildServices(connectionString);
        await using var scope = provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var seed = scope.ServiceProvider.GetRequiredService<IProcessSeed>();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var organizationId = Guid.CreateVersion7();

        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var processId = await seed.SeedAsync(organizationId, ct);

                context.Organizations.Add(
                    Organization.Create(
                        organizationId, OrganizationsDatabaseFixture.Name(OrganizationsDatabaseFixture.UniqueName(name)), processId, OrganizationsDatabaseFixture.Now, OrganizationsDatabaseFixture.Creator));
                await context.SaveChangesAsync(ct);

                return true;
            },
            CancellationToken.None);

        return organizationId;
    }

    private static async Task ExecuteAsync(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<string>> QueryAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add(reader.IsDBNull(0) ? "NULL" : Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!);
        }

        return rows;
    }
}
