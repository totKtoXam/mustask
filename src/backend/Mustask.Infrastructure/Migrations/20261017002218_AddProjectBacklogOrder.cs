using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectBacklogOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_backlog_orders",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    backlog_level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_backlog_orders", x => new { x.project_id, x.backlog_level_id });
                    table.ForeignKey(
                        name: "fk_project_backlog_orders_backlog_levels_backlog_level_id",
                        column: x => x.backlog_level_id,
                        principalTable: "backlog_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_backlog_orders_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_backlog_places",
                columns: table => new
                {
                    work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    backlog_level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_backlog_places", x => x.work_item_id);
                    table.CheckConstraint("ck_project_backlog_places_position", "position >= 1");
                    table.ForeignKey(
                        name: "fk_project_backlog_places_project_backlog_orders_project_id_bac~",
                        columns: x => new { x.project_id, x.backlog_level_id },
                        principalTable: "project_backlog_orders",
                        principalColumns: new[] { "project_id", "backlog_level_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_backlog_places_work_items_work_item_id",
                        column: x => x.work_item_id,
                        principalTable: "work_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_backlog_orders_backlog_level_id",
                table: "project_backlog_orders",
                column: "backlog_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_backlog_places_project_id_backlog_level_id",
                table: "project_backlog_places",
                columns: new[] { "project_id", "backlog_level_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_backlog_places");

            migrationBuilder.DropTable(
                name: "project_backlog_orders");
        }
    }
}
