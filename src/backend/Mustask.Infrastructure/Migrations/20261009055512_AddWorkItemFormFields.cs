using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemFormFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "acceptance_criteria",
                table: "work_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "activity",
                table: "work_items",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "business_value",
                table: "work_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "completed_work",
                table: "work_items",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "effort",
                table: "work_items",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "original_estimate",
                table: "work_items",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "work_items",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "remaining_work",
                table: "work_items",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "repro_steps",
                table: "work_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "risk",
                table: "work_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "severity",
                table: "work_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "start_date",
                table: "work_items",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "story_points",
                table: "work_items",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "system_info",
                table: "work_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "target_date",
                table: "work_items",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "time_criticality",
                table: "work_items",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "value_area",
                table: "work_items",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            // feat-022 BR-M3-37 (its api-contract § 4): every item that exists takes the values a new item of its type
            // takes. «Приоритет» 2 came with the column's default; the rest by the type's kind, its appearance token,
            // never its name. changed_at is not touched, so neither «Изменён» nor the list's order moves, and no
            // backlog place changes.
            migrationBuilder.Sql(
                """
                UPDATE work_items AS item
                SET severity = CASE WHEN kind.appearance = 'bug' THEN 3 END,
                    value_area = CASE WHEN kind.appearance IN ('epic', 'feature', 'story') THEN 'Business' END,
                    acceptance_criteria = CASE WHEN kind.appearance = 'story' THEN '' END,
                    repro_steps = CASE WHEN kind.appearance = 'bug' THEN '' END,
                    system_info = CASE WHEN kind.appearance = 'bug' THEN '' END
                FROM work_item_types AS kind
                WHERE kind.id = item.type_id
                  AND kind.appearance IN ('epic', 'feature', 'story', 'bug');
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_acceptance_criteria_length",
                table: "work_items",
                sql: "char_length(acceptance_criteria) <= 100000");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_activity",
                table: "work_items",
                sql: "activity IN ('Deployment', 'Design', 'Development', 'Documentation', 'Requirements', 'Testing')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_business_value",
                table: "work_items",
                sql: "business_value BETWEEN 0 AND 9999");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_completed_work",
                table: "work_items",
                sql: "completed_work BETWEEN 0 AND 9999");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_effort",
                table: "work_items",
                sql: "effort BETWEEN 0 AND 9999");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_original_estimate",
                table: "work_items",
                sql: "original_estimate BETWEEN 0 AND 9999");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_priority",
                table: "work_items",
                sql: "priority BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_remaining_work",
                table: "work_items",
                sql: "remaining_work BETWEEN 0 AND 9999");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_repro_steps_length",
                table: "work_items",
                sql: "char_length(repro_steps) <= 100000");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_risk",
                table: "work_items",
                sql: "risk BETWEEN 1 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_severity",
                table: "work_items",
                sql: "severity BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_start_date",
                table: "work_items",
                sql: "start_date BETWEEN DATE '2000-01-01' AND DATE '2099-12-31'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_story_points",
                table: "work_items",
                sql: "story_points BETWEEN 0 AND 9999");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_system_info_length",
                table: "work_items",
                sql: "char_length(system_info) <= 100000");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_target_date",
                table: "work_items",
                sql: "target_date BETWEEN DATE '2000-01-01' AND DATE '2099-12-31'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_target_not_before_start",
                table: "work_items",
                sql: "target_date >= start_date");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_time_criticality",
                table: "work_items",
                sql: "time_criticality BETWEEN 0 AND 9999");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_value_area",
                table: "work_items",
                sql: "value_area IN ('Business', 'Architectural')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_acceptance_criteria_length",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_activity",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_business_value",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_completed_work",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_effort",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_original_estimate",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_priority",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_remaining_work",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_repro_steps_length",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_risk",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_severity",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_start_date",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_story_points",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_system_info_length",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_target_date",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_target_not_before_start",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_time_criticality",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_value_area",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "acceptance_criteria",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "activity",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "business_value",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "completed_work",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "effort",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "original_estimate",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "remaining_work",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "repro_steps",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "risk",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "severity",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "start_date",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "story_points",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "system_info",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "target_date",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "time_criticality",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "value_area",
                table: "work_items");
        }
    }
}
