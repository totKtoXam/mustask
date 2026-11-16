using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <summary>
    /// feat-041 (doc-016 BR-TD-12 (c), OQ-5 A; ADR-0040 § 4-5): the project's «Видим всей организации» column, off for every
    /// existing and every new project. Until this migration every member saw every project; after it a non-administrator sees a
    /// project through a reaching grant or a linked team's membership only. The migration therefore names, per project, the members
    /// who lose sight: <c>project_sight_upgrade_log</c> (one row per project and member, kept after the run so the MR can quote it)
    /// and a notice per project with the count. Administrators keep sight and are not logged. Additive; the log table is not mapped.
    /// </summary>
    /// <inheritdoc />
    public partial class ProjectVisibleToOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "visible_to_organization",
                table: "projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                CREATE TABLE project_sight_upgrade_log (
                    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    migration text NOT NULL,
                    organization_id uuid NOT NULL,
                    project_id uuid NOT NULL,
                    project_key text NOT NULL,
                    user_id text NOT NULL,
                    logged_at timestamp with time zone NOT NULL DEFAULT now()
                );

                -- A member who saw the project before (every member did) and does not after: not an administrator, with no project
                -- grant, no department grant through the node (its switch on), and no membership of a team linked to the project.
                INSERT INTO project_sight_upgrade_log (migration, organization_id, project_id, project_key, user_id)
                SELECT 'feat-041', p.organization_id, p.id, p."key", m.user_id
                FROM projects AS p
                JOIN organization_memberships AS m ON m.organization_id = p.organization_id AND m.role <> 'admin'
                WHERE NOT EXISTS (SELECT 1 FROM project_grants g WHERE g.project_id = p.id AND g.user_id = m.user_id)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM areas n
                      JOIN area_grants ag ON ag.area_id = n.parent_id AND ag.user_id = m.user_id
                      WHERE n.organization_id = p.organization_id AND n.project_id = p.id AND n.type = 2 AND n.inherits_access)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM project_teams l
                      JOIN team_memberships tm ON tm.team_id = l.team_id AND tm.user_id = m.user_id
                      WHERE l.project_id = p.id);

                DO $$
                DECLARE v_row record;
                BEGIN
                    FOR v_row IN
                        SELECT project_id, project_key, count(*) AS lost
                        FROM project_sight_upgrade_log WHERE migration = 'feat-041' GROUP BY project_id, project_key ORDER BY project_key
                    LOOP
                        RAISE NOTICE 'feat-041: project % (%): % members lose sight', v_row.project_key, v_row.project_id, v_row.lost;
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS project_sight_upgrade_log;");

            migrationBuilder.DropColumn(
                name: "visible_to_organization",
                table: "projects");
        }
    }
}
