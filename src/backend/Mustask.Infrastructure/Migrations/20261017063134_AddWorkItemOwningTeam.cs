using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemOwningTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "owning_team_id",
                table: "work_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "default_team_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            // mod-017 BR-OT-14: every item takes the team that owned its area, resolved through the tree (the nearest owned
            // ancestor, at the latest the root, which the first team owned); the changed_at of an item is not written, so no
            // item's «Изменён» moves. No project gets a named default team. An owner that is a deleted team gives no team
            // (expected 0: the first team owns the root and was never deleted, and a deletion passed the rest to it). The
            // mapping runs before the keys are created, so the foreign key checks the finished column once. The counts are
            // the migration's log: items mapped, items left without a team.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    mapped integer;
                    unowned integer;
                BEGIN
                    WITH RECURSIVE resolved (id, owner_id) AS (
                        SELECT area.id, area.owner_team_id
                        FROM areas AS area
                        WHERE area.parent_id IS NULL
                        UNION ALL
                        SELECT child.id, COALESCE(child.owner_team_id, resolved.owner_id)
                        FROM areas AS child
                        JOIN resolved ON child.parent_id = resolved.id
                    )
                    UPDATE work_items AS item
                    SET owning_team_id = resolved.owner_id
                    FROM resolved
                    JOIN teams AS team ON team.id = resolved.owner_id AND team.status = 'Active'
                    WHERE item.area_id = resolved.id;

                    GET DIAGNOSTICS mapped = ROW_COUNT;

                    SELECT count(*) INTO unowned FROM work_items WHERE owning_team_id IS NULL;

                    RAISE NOTICE 'mod-017: % work items took the team that owned their area; % work items have no team', mapped, unowned;
                END
                $$;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_work_items_id_owning_team_id",
                table: "work_items",
                columns: new[] { "id", "owning_team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_items_project_id_owning_team_id_type_id",
                table: "work_items",
                columns: new[] { "project_id", "owning_team_id", "type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_projects_id_default_team_id",
                table: "projects",
                columns: new[] { "id", "default_team_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_projects_teams_id_default_team_id",
                table: "projects",
                columns: new[] { "id", "default_team_id" },
                principalTable: "teams",
                principalColumns: new[] { "project_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_work_items_teams_project_id_owning_team_id",
                table: "work_items",
                columns: new[] { "project_id", "owning_team_id" },
                principalTable: "teams",
                principalColumns: new[] { "project_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_projects_teams_id_default_team_id",
                table: "projects");

            migrationBuilder.DropForeignKey(
                name: "fk_work_items_teams_project_id_owning_team_id",
                table: "work_items");

            migrationBuilder.DropIndex(
                name: "ix_work_items_id_owning_team_id",
                table: "work_items");

            migrationBuilder.DropIndex(
                name: "ix_work_items_project_id_owning_team_id_type_id",
                table: "work_items");

            migrationBuilder.DropIndex(
                name: "ix_projects_id_default_team_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "owning_team_id",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "default_team_id",
                table: "projects");
        }
    }
}
