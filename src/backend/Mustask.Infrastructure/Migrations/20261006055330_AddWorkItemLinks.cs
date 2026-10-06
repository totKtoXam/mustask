using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "parent_id",
                table: "work_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_work_items_project_id_id",
                table: "work_items",
                columns: new[] { "project_id", "id" });

            migrationBuilder.CreateTable(
                name: "work_item_hierarchy_locks",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_item_hierarchy_locks", x => x.project_id);
                    table.ForeignKey(
                        name: "fk_work_item_hierarchy_locks_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_work_items_project_id_parent_id",
                table: "work_items",
                columns: new[] { "project_id", "parent_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_parent_not_self",
                table: "work_items",
                sql: "parent_id IS NULL OR parent_id <> id");

            migrationBuilder.AddForeignKey(
                name: "fk_work_items_work_items_project_id_parent_id",
                table: "work_items",
                columns: new[] { "project_id", "parent_id" },
                principalTable: "work_items",
                principalColumns: new[] { "project_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_work_items_work_items_project_id_parent_id",
                table: "work_items");

            migrationBuilder.DropTable(
                name: "work_item_hierarchy_locks");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_work_items_project_id_id",
                table: "work_items");

            migrationBuilder.DropIndex(
                name: "ix_work_items_project_id_parent_id",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_parent_not_self",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "parent_id",
                table: "work_items");
        }
    }
}
