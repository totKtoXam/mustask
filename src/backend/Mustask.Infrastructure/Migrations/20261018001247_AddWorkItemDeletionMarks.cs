using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemDeletionMarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "work_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "work_items",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_from_state_id",
                table: "work_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_items_deleted_from_state_id",
                table: "work_items",
                column: "deleted_from_state_id",
                filter: "deleted_from_state_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_deletion_marks",
                table: "work_items",
                sql: "(deleted_from_state_id IS NULL AND deleted_by IS NULL AND deleted_at IS NULL) OR (deleted_from_state_id IS NOT NULL AND deleted_by IS NOT NULL AND deleted_at IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_work_items_state_definitions_deleted_from_state_id",
                table: "work_items",
                column: "deleted_from_state_id",
                principalTable: "state_definitions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_work_items_state_definitions_deleted_from_state_id",
                table: "work_items");

            migrationBuilder.DropIndex(
                name: "ix_work_items_deleted_from_state_id",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_deletion_marks",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "deleted_from_state_id",
                table: "work_items");
        }
    }
}
