using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <summary>
    /// mod-018 (BR-AW-1, BR-AW-2, BR-AW-11, BR-AW-14; ADR-0028 rules 1, 4, 5): areas become the organization's, one tree per
    /// organization, and a project or a team is linked to an area instead of living in one. The human chose to use the existing
    /// data in full (OQ-1 A): <b>the upgrade drops every per-project area</b> and the work item's «Область» with them. The old shape
    /// cannot place an area (it is keyed by a project), and an item's area was only a project's root or a classification path, so
    /// nothing is remapped. Everything else is kept; <c>work_items.changed_at</c> is NOT written: no item's «Изменён» moves.
    /// </summary>
    /// <inheritdoc />
    public partial class OrganizationAreaWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. The log (BR-AW-14), first, over the rows as they stand: what the drop below removes, per kind.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    areas_dropped integer;
                    roots_dropped integer;
                    items_cleared integer;
                BEGIN
                    SELECT count(*), count(*) FILTER (WHERE parent_id IS NULL) INTO areas_dropped, roots_dropped FROM areas;
                    SELECT count(*) INTO items_cleared FROM work_items;

                    RAISE NOTICE 'mod-018: % areas dropped (% of them project roots) and % work items whose area it cleared',
                        areas_dropped, roots_dropped, items_cleared;
                END
                $$;
                """);

            // 2. The keys onto areas go, then the item's column, then the rows: the self key is gone before the delete, so one
            // statement removes the whole forest.
            migrationBuilder.DropForeignKey(
                name: "fk_areas_areas_project_id_parent_id",
                table: "areas");

            migrationBuilder.DropForeignKey(
                name: "fk_areas_projects_project_id",
                table: "areas");

            migrationBuilder.DropForeignKey(
                name: "fk_work_items_areas_project_id_area_id",
                table: "work_items");

            migrationBuilder.DropIndex(
                name: "ix_work_items_project_id_area_id",
                table: "work_items");

            migrationBuilder.DropIndex(
                name: "ix_work_items_project_id_type_id_area_id",
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

            migrationBuilder.DropColumn(
                name: "area_id",
                table: "work_items");

            migrationBuilder.Sql("DELETE FROM areas;");

            // 3. The new shape, over an empty table.
            migrationBuilder.RenameColumn(
                name: "project_id",
                table: "areas",
                newName: "organization_id");

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

            migrationBuilder.AddUniqueConstraint(
                name: "ak_areas_organization_id_id",
                table: "areas",
                columns: new[] { "organization_id", "id" });

            migrationBuilder.CreateTable(
                name: "area_projects",
                columns: table => new
                {
                    area_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_area_projects", x => new { x.area_id, x.project_id });
                    table.ForeignKey(
                        name: "fk_area_projects_areas_organization_id_area_id",
                        columns: x => new { x.organization_id, x.area_id },
                        principalTable: "areas",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_area_projects_projects_organization_id_project_id",
                        columns: x => new { x.organization_id, x.project_id },
                        principalTable: "projects",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "ix_work_items_project_id_type_id",
                table: "work_items",
                columns: new[] { "project_id", "type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_areas_organization_id_parent_id_normalized_name",
                table: "areas",
                columns: new[] { "organization_id", "parent_id", "normalized_name" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_areas_description_length",
                table: "areas",
                sql: "description IS NULL OR char_length(description) BETWEEN 1 AND 2000");

            migrationBuilder.CreateIndex(
                name: "ix_area_projects_organization_id_area_id",
                table: "area_projects",
                columns: new[] { "organization_id", "area_id" });

            migrationBuilder.CreateIndex(
                name: "ix_area_projects_organization_id_project_id",
                table: "area_projects",
                columns: new[] { "organization_id", "project_id" });

            migrationBuilder.CreateIndex(
                name: "ix_area_projects_project_id_area_id",
                table: "area_projects",
                columns: new[] { "project_id", "area_id" });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_areas_areas_organization_id_parent_id",
                table: "areas");

            migrationBuilder.DropForeignKey(
                name: "fk_areas_organizations_organization_id",
                table: "areas");

            migrationBuilder.DropTable(
                name: "area_projects");

            migrationBuilder.DropTable(
                name: "area_teams");

            migrationBuilder.DropTable(
                name: "area_tree_locks");

            // The organization-level areas cannot be keyed by a project: they go, and a root per project is made below (BR-M1-11).
            migrationBuilder.Sql("DELETE FROM areas;");

            migrationBuilder.DropIndex(
                name: "ix_work_items_project_id_type_id",
                table: "work_items");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_areas_organization_id_id",
                table: "areas");

            migrationBuilder.DropIndex(
                name: "ix_areas_organization_id_parent_id_normalized_name",
                table: "areas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_areas_description_length",
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

            migrationBuilder.RenameColumn(
                name: "organization_id",
                table: "areas",
                newName: "project_id");

            migrationBuilder.AddColumn<Guid>(
                name: "area_id",
                table: "work_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_areas_project_id_id",
                table: "areas",
                columns: new[] { "project_id", "id" });

            // The old shape: a root area per project, named after it, and every item in its project's root.
            migrationBuilder.Sql(
                """
                INSERT INTO areas (id, project_id, parent_id, name, normalized_name)
                SELECT gen_random_uuid(), p.id, NULL, p.name, p.normalized_name FROM projects p;

                UPDATE work_items w SET area_id = a.id FROM areas a WHERE a.project_id = w.project_id AND a.parent_id IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "area_id",
                table: "work_items",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_items_project_id_area_id",
                table: "work_items",
                columns: new[] { "project_id", "area_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_items_project_id_type_id_area_id",
                table: "work_items",
                columns: new[] { "project_id", "type_id", "area_id" });

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
                name: "fk_areas_projects_project_id",
                table: "areas",
                column: "project_id",
                principalTable: "projects",
                principalColumn: "id",
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
