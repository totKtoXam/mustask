using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSprints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "sprint_id",
                table: "work_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sprint_counters",
                columns: table => new
                {
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sprint_counters", x => x.team_id);
                    table.CheckConstraint("ck_sprint_counters_last_number", "last_number >= 0");
                    table.ForeignKey(
                        name: "fk_sprint_counters_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sprints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sprints", x => x.id);
                    table.UniqueConstraint("ak_sprints_id_team_id", x => new { x.id, x.team_id });
                    table.CheckConstraint("ck_sprints_closed_marks", "(state = 'Closed') = (closed_at IS NOT NULL AND closed_by IS NOT NULL)");
                    table.CheckConstraint("ck_sprints_dates_order", "start_date IS NULL OR end_date >= start_date");
                    table.CheckConstraint("ck_sprints_dates_paired", "(start_date IS NULL) = (end_date IS NULL)");
                    table.CheckConstraint("ck_sprints_number_positive", "number >= 1");
                    table.CheckConstraint("ck_sprints_started_have_dates", "state = 'Planned' OR start_date IS NOT NULL");
                    table.CheckConstraint("ck_sprints_state", "state IN ('Planned', 'Active', 'Closed')");
                    table.ForeignKey(
                        name: "fk_sprints_teams_organization_id_team_id",
                        columns: x => new { x.organization_id, x.team_id },
                        principalTable: "teams",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sprint_snapshot_items",
                columns: table => new
                {
                    sprint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignee_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    story_points = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    is_done = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sprint_snapshot_items", x => new { x.sprint_id, x.work_item_id });
                    table.ForeignKey(
                        name: "fk_sprint_snapshot_items_sprints_sprint_id",
                        column: x => x.sprint_id,
                        principalTable: "sprints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_work_items_sprint_id",
                table: "work_items",
                column: "sprint_id",
                filter: "sprint_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_sprint_id_owning_team_id",
                table: "work_items",
                columns: new[] { "sprint_id", "owning_team_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_sprint_needs_team",
                table: "work_items",
                sql: "sprint_id IS NULL OR owning_team_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sprint_snapshot_items_sprint_id_level_id",
                table: "sprint_snapshot_items",
                columns: new[] { "sprint_id", "level_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sprints_organization_id_team_id",
                table: "sprints",
                columns: new[] { "organization_id", "team_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sprints_team_id_active",
                table: "sprints",
                column: "team_id",
                unique: true,
                filter: "state = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_sprints_team_id_end_date_closed",
                table: "sprints",
                columns: new[] { "team_id", "end_date" },
                filter: "state = 'Closed'");

            migrationBuilder.CreateIndex(
                name: "ix_sprints_team_id_normalized_name",
                table: "sprints",
                columns: new[] { "team_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sprints_team_id_number",
                table: "sprints",
                columns: new[] { "team_id", "number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_work_items_sprints_sprint_id_owning_team_id",
                table: "work_items",
                columns: new[] { "sprint_id", "owning_team_id" },
                principalTable: "sprints",
                principalColumns: new[] { "id", "team_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_work_items_sprints_sprint_id_owning_team_id",
                table: "work_items");

            migrationBuilder.DropTable(
                name: "sprint_counters");

            migrationBuilder.DropTable(
                name: "sprint_snapshot_items");

            migrationBuilder.DropTable(
                name: "sprints");

            migrationBuilder.DropIndex(
                name: "ix_work_items_sprint_id",
                table: "work_items");

            migrationBuilder.DropIndex(
                name: "ix_work_items_sprint_id_owning_team_id",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_sprint_needs_team",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "sprint_id",
                table: "work_items");
        }
    }
}
