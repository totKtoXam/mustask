using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemDescriptionFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_description_length",
                table: "work_items");

            migrationBuilder.AddColumn<string>(
                name: "description_format",
                table: "work_items",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Plain");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_description_format",
                table: "work_items",
                sql: "description_format IN ('Plain', 'Html')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_description_length",
                table: "work_items",
                sql: "char_length(description) <= 1000000");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_empty_description_plain",
                table: "work_items",
                sql: "description_format = 'Plain' OR description <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_plain_description_length",
                table: "work_items",
                sql: "description_format = 'Html' OR char_length(description) <= 100000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // feat-018, ADR-0027 Consequences "Rollback": the old bound is 100 000 characters. Refuse with a message
            // that names the cause, before anything is dropped, rather than let the restored check fail on a row.
            // Html rows that fit stay as strings in a plain field (docs/runbooks/rich-text-rollback.md).
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM work_items WHERE char_length(description) > 100000) THEN
                        RAISE EXCEPTION 'Cannot roll back AddWorkItemDescriptionFormat: a work item description is longer than 100000 characters (rich text, feat-018). See docs/runbooks/rich-text-rollback.md.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_description_format",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_description_length",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_empty_description_plain",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_plain_description_length",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "description_format",
                table: "work_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_description_length",
                table: "work_items",
                sql: "char_length(description) <= 100000");
        }
    }
}
