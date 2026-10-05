using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_item_counters",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_item_counters", x => x.project_id);
                    table.CheckConstraint("ck_work_item_counters_last_number", "last_number >= 0");
                    table.ForeignKey(
                        name: "fk_work_item_counters_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignee_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    area_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    creation_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_items", x => x.id);
                    table.CheckConstraint("ck_work_items_description_length", "char_length(description) <= 100000");
                    table.CheckConstraint("ck_work_items_number_positive", "number >= 1");
                    table.CheckConstraint("ck_work_items_title_length", "char_length(title) BETWEEN 1 AND 255");
                    table.ForeignKey(
                        name: "fk_work_items_areas_project_id_area_id",
                        columns: x => new { x.project_id, x.area_id },
                        principalTable: "areas",
                        principalColumns: new[] { "project_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_items_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_items_state_definitions_state_id",
                        column: x => x.state_id,
                        principalTable: "state_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_items_work_item_types_type_id",
                        column: x => x.type_id,
                        principalTable: "work_item_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_work_items_project_id_area_id",
                table: "work_items",
                columns: new[] { "project_id", "area_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_items_project_id_changed_at_number",
                table: "work_items",
                columns: new[] { "project_id", "changed_at", "number" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_work_items_project_id_creation_request_id",
                table: "work_items",
                columns: new[] { "project_id", "creation_request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_items_project_id_number",
                table: "work_items",
                columns: new[] { "project_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_items_state_id",
                table: "work_items",
                column: "state_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_type_id",
                table: "work_items",
                column: "type_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_item_counters");

            migrationBuilder.DropTable(
                name: "work_items");
        }
    }
}
