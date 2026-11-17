using Mustask.Infrastructure.Tests.Organizations;

namespace Mustask.Infrastructure.Tests.Search;

/// <summary>
/// feat-071: a bulk-written organization for the search's database tests (raw <c>generate_series</c> inserts, so a doc-012-sized set takes
/// a moment). Departments, then projects each with its project node under a department, then sub-areas under the nodes, teams and members.
/// </summary>
internal sealed record SearchDataSet(Guid OrganizationId, IReadOnlyList<Guid> Projects, IReadOnlyList<Guid> Teams, IReadOnlyList<Guid> Departments, IReadOnlyList<Guid> Areas)
{
    public static async Task<SearchDataSet> SeedAsync(
        OrganizationsDatabaseFixture database, int departments, int projects, int subAreas, int teams, int members)
    {
        var structure = await database.CreateStructureAsync();
        var org = structure.Organization.Id;
        var tag = Guid.CreateVersion7().ToString("N")[^8..];

        await database.ExecuteAsync(
            """
            INSERT INTO areas (id, organization_id, project_id, parent_id, type, name, normalized_name, color, created_at, created_by, inherits_access)
            SELECT gen_random_uuid(), @org, NULL, NULL, 1, 'Отдел ' || n, upper('Отдел ' || n), 'blue', now(), 'seed', true FROM generate_series(1, @departments) n
            """,
            ("org", org), ("departments", departments));

        await database.ExecuteAsync(
            """
            INSERT INTO projects (id, organization_id, name, normalized_name, "key", status, color, created_at, created_by, visible_to_organization)
            SELECT gen_random_uuid(), @org, 'Проект ' || n, upper('Проект ' || n), 'K' || lpad(n::text, 4, '0'), 'Active', 'blue', now(), 'seed', false
            FROM generate_series(1, @projects) n;
            INSERT INTO areas (id, organization_id, project_id, parent_id, type, name, normalized_name, color, created_at, created_by, inherits_access)
            SELECT gen_random_uuid(), @org, p.id, d.id, 2, p.name, p.normalized_name, NULL, now(), 'seed', true
            FROM (SELECT p.*, row_number() OVER (ORDER BY p.name) rn FROM projects p WHERE p.organization_id = @org AND p.created_by = 'seed') p
            JOIN (SELECT a.*, row_number() OVER (ORDER BY a.name) rn FROM areas a WHERE a.organization_id = @org AND a.type = 1 AND a.created_by = 'seed') d
              ON d.rn = (p.rn % @departments) + 1;
            INSERT INTO areas (id, organization_id, project_id, parent_id, type, name, normalized_name, color, created_at, created_by, inherits_access)
            SELECT gen_random_uuid(), @org, node.project_id, node.id, 3, 'Область ' || n, upper('Область ' || n), NULL, now(), 'seed', true
            FROM generate_series(1, @subAreas) n
            JOIN (SELECT a.*, row_number() OVER (ORDER BY a.name) rn FROM areas a WHERE a.organization_id = @org AND a.type = 2 AND a.created_by = 'seed') node
              ON node.rn = (n % @projects) + 1;
            INSERT INTO teams (id, organization_id, department_id, name, normalized_name, status, color)
            SELECT gen_random_uuid(), @org, (SELECT id FROM areas WHERE organization_id = @org AND type = 1 AND created_by = 'seed' LIMIT 1),
                   'Команда ' || n, upper('Команда ' || n), 'Active', 'blue'
            FROM generate_series(1, @teams) n;
            INSERT INTO directory_users (id, username, first_name, last_name, display_name, email, enabled, is_active, lifecycle, changed_at, moment, search_name, search_email, sort_key, sort_key_version)
            SELECT 'u' || @tag || n, 'user' || @tag || n, 'Имя' || n, 'Фамилия' || n, 'Имя' || n || ' Фамилия' || n, 'user' || n || '@seed.test', true, true, 'Present', now(), 0,
                   search_fold('Имя' || n || ' Фамилия' || n), search_fold('user' || n || '@seed.test'), '\x00'::bytea, 'seed'
            FROM generate_series(1, @members) n;
            INSERT INTO organization_memberships (organization_id, user_id, role, added_at, added_by)
            SELECT @org, 'u' || @tag || n, 'member', now(), 'seed' FROM generate_series(1, @members) n
            """,
            ("org", org), ("tag", tag), ("projects", projects), ("departments", departments), ("subAreas", subAreas), ("teams", teams), ("members", members));

        async Task<List<Guid>> IdsAsync(string sql) => (await database.QueryAsync(sql, ("org", org))).Select(Guid.Parse).ToList();

        return new SearchDataSet(
            org,
            await IdsAsync("SELECT id FROM projects WHERE organization_id = @org AND created_by = 'seed'"),
            await IdsAsync("SELECT id FROM teams WHERE organization_id = @org AND name LIKE 'Команда %'"),
            await IdsAsync("SELECT id FROM areas WHERE organization_id = @org AND type = 1 AND created_by = 'seed'"),
            await IdsAsync("SELECT id FROM areas WHERE organization_id = @org AND type = 3 AND created_by = 'seed'"));
    }
}
