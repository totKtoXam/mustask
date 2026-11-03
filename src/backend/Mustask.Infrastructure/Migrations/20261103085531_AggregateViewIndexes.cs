using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AggregateViewIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_work_items_owning_team_id_type_id",
                table: "work_items");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_assignee_id_changed_at",
                table: "work_items",
                columns: new[] { "assignee_id", "changed_at" },
                descending: new[] { false, true },
                filter: "assignee_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_no_team_changed_at",
                table: "work_items",
                columns: new[] { "changed_at", "number" },
                descending: new bool[0],
                filter: "owning_team_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_owning_team_id_type_id_changed_at_number",
                table: "work_items",
                columns: new[] { "owning_team_id", "type_id", "changed_at", "number" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_work_items_project_id_type_id_changed_at_number",
                table: "work_items",
                columns: new[] { "project_id", "type_id", "changed_at", "number" },
                descending: new[] { false, false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_work_items_assignee_id_changed_at",
                table: "work_items");

            migrationBuilder.DropIndex(
                name: "ix_work_items_no_team_changed_at",
                table: "work_items");

            migrationBuilder.DropIndex(
                name: "ix_work_items_owning_team_id_type_id_changed_at_number",
                table: "work_items");

            migrationBuilder.DropIndex(
                name: "ix_work_items_project_id_type_id_changed_at_number",
                table: "work_items");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_owning_team_id_type_id",
                table: "work_items",
                columns: new[] { "owning_team_id", "type_id" });
        }
    }
}
