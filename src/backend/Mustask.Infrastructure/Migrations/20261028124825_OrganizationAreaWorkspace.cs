using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationAreaWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_areas_areas_project_id_parent_id",
                table: "areas");

            migrationBuilder.DropForeignKey(
                name: "fk_work_items_areas_project_id_area_id",
                table: "work_items");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_areas_project_id_id",
                table: "areas");

            migrationBuilder.DropIndex(
                name: "ix_areas_parent_id_normalized_name",
                table: "areas");

            migrationBuilder.DropIndex(
                name: "ix_areas_project_id",
                table: "areas");

            migrationBuilder.DropIndex(
                name: "ix_areas_project_id_parent_id",
                table: "areas");

            migrationBuilder.DropIndex(
                name: "ix_areas_project_id_root",
                table: "areas");

            migrationBuilder.AlterColumn<Guid>(
                name: "project_id",
                table: "areas",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "areas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "areas",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "areas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "inherits_access",
                table: "areas",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "areas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<short>(
                name: "type",
                table: "areas",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            // The data (mod-018 OQ-13 A). Every project tree is kept: its root becomes the project's node at the top of the
            // organization's tree (type 2), the areas below it become sub-areas (type 3). A tree deeper than the limit (10 levels,
            // the root counted as the first) cannot be placed: the upgrade fails, naming the project, and changes nothing (the
            // migration is one transaction).
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    too_deep text;
                BEGIN
                    WITH RECURSIVE walk(id, project_id, level) AS (
                        SELECT id, project_id, 1 FROM areas WHERE parent_id IS NULL
                        UNION ALL
                        SELECT child.id, child.project_id, walk.level + 1
                        FROM areas child JOIN walk ON child.parent_id = walk.id
                    )
                    SELECT string_agg(projects.name || ' (' || projects.key || ')', ', ' ORDER BY projects.name) INTO too_deep
                    FROM projects
                    WHERE projects.id IN (SELECT project_id FROM walk WHERE level > 10);

                    IF too_deep IS NOT NULL THEN
                        RAISE EXCEPTION 'mod-018: the area tree of the project(s) % is deeper than 10 levels; shorten it before the upgrade', too_deep;
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql(
                """
                UPDATE areas
                SET organization_id = projects.organization_id,
                    type = CASE WHEN areas.parent_id IS NULL THEN 2 ELSE 3 END,
                    created_by = projects.created_by,
                    created_at = projects.created_at
                FROM projects
                WHERE projects.id = areas.project_id;

                -- BR-AW-14, US-5: the counts of each kind are logged (0 and 0 on an empty database).
                DO $$
                DECLARE
                    nodes bigint;
                    subs bigint;
                BEGIN
                    SELECT count(*) FILTER (WHERE type = 2), count(*) FILTER (WHERE type = 3) INTO nodes, subs FROM areas;
                    RAISE NOTICE 'mod-018: typed % project nodes, kept % sub-areas', nodes, subs;
                END
                $$;
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_areas_organization_id_id",
                table: "areas",
                columns: new[] { "organization_id", "id" });

            migrationBuilder.CreateTable(
                name: "area_teams",
                columns: table => new
                {
                    area_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_area_teams", x => new { x.area_id, x.team_id });
                    table.ForeignKey(
                        name: "fk_area_teams_areas_organization_id_area_id",
                        columns: x => new { x.organization_id, x.area_id },
                        principalTable: "areas",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_area_teams_teams_organization_id_team_id",
                        columns: x => new { x.organization_id, x.team_id },
                        principalTable: "teams",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "area_tree_locks",
                columns: table => new
                {
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_area_tree_locks", x => x.organization_id);
                    table.ForeignKey(
                        name: "fk_area_tree_locks_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ak_areas_project_id_id",
                table: "areas",
                columns: new[] { "project_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_areas_organization_id_parent_id_normalized_name",
                table: "areas",
                columns: new[] { "organization_id", "parent_id", "normalized_name" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_areas_organization_id_type",
                table: "areas",
                columns: new[] { "organization_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_areas_project_id_node",
                table: "areas",
                column: "project_id",
                unique: true,
                filter: "type = 2");

            migrationBuilder.AddCheckConstraint(
                name: "ck_areas_description_length",
                table: "areas",
                sql: "description IS NULL OR char_length(description) BETWEEN 1 AND 2000");

            migrationBuilder.AddCheckConstraint(
                name: "ck_areas_type_shape",
                table: "areas",
                sql: "(type = 1 AND project_id IS NULL AND parent_id IS NULL) OR (type = 2 AND project_id IS NOT NULL) OR (type = 3 AND project_id IS NOT NULL AND parent_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_area_teams_organization_id_area_id",
                table: "area_teams",
                columns: new[] { "organization_id", "area_id" });

            migrationBuilder.CreateIndex(
                name: "ix_area_teams_organization_id_team_id",
                table: "area_teams",
                columns: new[] { "organization_id", "team_id" });

            migrationBuilder.CreateIndex(
                name: "ix_area_teams_team_id_area_id",
                table: "area_teams",
                columns: new[] { "team_id", "area_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_areas_areas_organization_id_parent_id",
                table: "areas",
                columns: new[] { "organization_id", "parent_id" },
                principalTable: "areas",
                principalColumns: new[] { "organization_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_areas_organizations_organization_id",
                table: "areas",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // The item's «Область» is one of its project's nodes (BR-AW-11). The model leaves this key to SQL: an area's project_id is
            // nullable, which an EF alternate key cannot be. The key targets the unique index above (a plain unique index serves).
            migrationBuilder.AddForeignKey(
                name: "fk_work_items_areas_project_id_area_id",
                table: "work_items",
                columns: new[] { "project_id", "area_id" },
                principalTable: "areas",
                principalColumns: new[] { "project_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_work_items_areas_project_id_area_id",
                table: "work_items");

            // Back to the per-project trees: a project's node is a root again, a department has no place and goes with its links
            // (the link table is dropped below).
            migrationBuilder.Sql("UPDATE areas SET parent_id = NULL WHERE type = 2;");
            migrationBuilder.Sql("DELETE FROM area_teams;");
            migrationBuilder.Sql("DELETE FROM areas WHERE type = 1;");

            migrationBuilder.DropForeignKey(
                name: "fk_areas_areas_organization_id_parent_id",
                table: "areas");

            migrationBuilder.DropForeignKey(
                name: "fk_areas_organizations_organization_id",
                table: "areas");

            migrationBuilder.DropTable(
                name: "area_teams");

            migrationBuilder.DropTable(
                name: "area_tree_locks");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_areas_organization_id_id",
                table: "areas");

            migrationBuilder.DropIndex(
                name: "ak_areas_project_id_id",
                table: "areas");

            migrationBuilder.DropIndex(
                name: "ix_areas_organization_id_parent_id_normalized_name",
                table: "areas");

            migrationBuilder.DropIndex(
                name: "ix_areas_organization_id_type",
                table: "areas");

            migrationBuilder.DropIndex(
                name: "ix_areas_project_id_node",
                table: "areas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_areas_description_length",
                table: "areas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_areas_type_shape",
                table: "areas");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "areas");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "areas");

            migrationBuilder.DropColumn(
                name: "description",
                table: "areas");

            migrationBuilder.DropColumn(
                name: "inherits_access",
                table: "areas");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "areas");

            migrationBuilder.DropColumn(
                name: "type",
                table: "areas");

            migrationBuilder.AlterColumn<Guid>(
                name: "project_id",
                table: "areas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_areas_project_id_id",
                table: "areas",
                columns: new[] { "project_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_areas_parent_id_normalized_name",
                table: "areas",
                columns: new[] { "parent_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_areas_project_id",
                table: "areas",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_areas_project_id_parent_id",
                table: "areas",
                columns: new[] { "project_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "ix_areas_project_id_root",
                table: "areas",
                column: "project_id",
                unique: true,
                filter: "parent_id IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_areas_areas_project_id_parent_id",
                table: "areas",
                columns: new[] { "project_id", "parent_id" },
                principalTable: "areas",
                principalColumns: new[] { "project_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_work_items_areas_project_id_area_id",
                table: "work_items",
                columns: new[] { "project_id", "area_id" },
                principalTable: "areas",
                principalColumns: new[] { "project_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
