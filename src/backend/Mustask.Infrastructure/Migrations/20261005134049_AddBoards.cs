using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBoards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "backlog_orders",
                columns: table => new
                {
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    backlog_level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_backlog_orders", x => new { x.team_id, x.backlog_level_id });
                    table.ForeignKey(
                        name: "fk_backlog_orders_backlog_levels_backlog_level_id",
                        column: x => x.backlog_level_id,
                        principalTable: "backlog_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_backlog_orders_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "backlog_places",
                columns: table => new
                {
                    work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    backlog_level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_backlog_places", x => x.work_item_id);
                    table.CheckConstraint("ck_backlog_places_position", "position >= 1");
                    table.ForeignKey(
                        name: "fk_backlog_places_backlog_orders_team_id_backlog_level_id",
                        columns: x => new { x.team_id, x.backlog_level_id },
                        principalTable: "backlog_orders",
                        principalColumns: new[] { "team_id", "backlog_level_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_backlog_places_work_items_work_item_id",
                        column: x => x.work_item_id,
                        principalTable: "work_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_work_items_project_id_type_id_area_id",
                table: "work_items",
                columns: new[] { "project_id", "type_id", "area_id" });

            migrationBuilder.CreateIndex(
                name: "ix_backlog_orders_backlog_level_id",
                table: "backlog_orders",
                column: "backlog_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_backlog_places_team_id_backlog_level_id",
                table: "backlog_places",
                columns: new[] { "team_id", "backlog_level_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backlog_places");

            migrationBuilder.DropTable(
                name: "backlog_orders");

            migrationBuilder.DropIndex(
                name: "ix_work_items_project_id_type_id_area_id",
                table: "work_items");
        }
    }
}
