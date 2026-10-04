using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "processes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "backlog_levels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_backlog_levels", x => x.id);
                    table.UniqueConstraint("ak_backlog_levels_process_id_id", x => new { x.process_id, x.id });
                    table.ForeignKey(
                        name: "fk_backlog_levels_processes_process_id",
                        column: x => x.process_id,
                        principalTable: "processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_item_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    backlog_level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    appearance = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_item_types", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_item_types_backlog_levels_process_id_backlog_level_id",
                        columns: x => new { x.process_id, x.backlog_level_id },
                        principalTable: "backlog_levels",
                        principalColumns: new[] { "process_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_item_types_processes_process_id",
                        column: x => x.process_id,
                        principalTable: "processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "state_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_item_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    category = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_state_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_state_definitions_work_item_types_work_item_type_id",
                        column: x => x.work_item_type_id,
                        principalTable: "work_item_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_backlog_levels_process_id_name",
                table: "backlog_levels",
                columns: new[] { "process_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_backlog_levels_process_id_rank",
                table: "backlog_levels",
                columns: new[] { "process_id", "rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_processes_organization_id",
                table: "processes",
                column: "organization_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_state_definitions_work_item_type_id_name",
                table: "state_definitions",
                columns: new[] { "work_item_type_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_state_definitions_work_item_type_id_position",
                table: "state_definitions",
                columns: new[] { "work_item_type_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_item_types_process_id_backlog_level_id",
                table: "work_item_types",
                columns: new[] { "process_id", "backlog_level_id" });

            migrationBuilder.CreateIndex(
                name: "ux_work_item_types_process_id_appearance",
                table: "work_item_types",
                columns: new[] { "process_id", "appearance" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_work_item_types_process_id_key",
                table: "work_item_types",
                columns: new[] { "process_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_work_item_types_process_id_name",
                table: "work_item_types",
                columns: new[] { "process_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_work_item_types_process_id_position",
                table: "work_item_types",
                columns: new[] { "process_id", "position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "state_definitions");

            migrationBuilder.DropTable(
                name: "work_item_types");

            migrationBuilder.DropTable(
                name: "backlog_levels");

            migrationBuilder.DropTable(
                name: "processes");
        }
    }
}
