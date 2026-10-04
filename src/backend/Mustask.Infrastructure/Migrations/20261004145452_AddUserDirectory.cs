using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "directory_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enumeration_start = table.Column<long>(type: "bigint", nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    begun_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    page_count = table.Column<int>(type: "integer", nullable: true),
                    entry_count = table.Column<int>(type: "integer", nullable: true),
                    received = table.Column<int>(type: "integer", nullable: false),
                    added = table.Column<int>(type: "integer", nullable: false),
                    changed = table.Column<int>(type: "integer", nullable: false),
                    unchanged = table.Column<int>(type: "integer", nullable: false),
                    held_as_deleted = table.Column<int>(type: "integer", nullable: false),
                    marked_absent = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_directory_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "directory_users",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    first_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: ""),
                    last_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: ""),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: ""),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lifecycle = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    moment = table.Column<long>(type: "bigint", nullable: false),
                    display_name = table.Column<string>(type: "character varying(511)", maxLength: 511, nullable: false),
                    search_name = table.Column<string>(type: "text", nullable: false),
                    search_email = table.Column<string>(type: "text", nullable: false),
                    sort_key = table.Column<byte[]>(type: "bytea", nullable: false),
                    sort_key_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_directory_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "directory_snapshot_pages",
                columns: table => new
                {
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_number = table.Column<int>(type: "integer", nullable: false),
                    received = table.Column<int>(type: "integer", nullable: false),
                    added = table.Column<int>(type: "integer", nullable: false),
                    changed = table.Column<int>(type: "integer", nullable: false),
                    unchanged = table.Column<int>(type: "integer", nullable: false),
                    held_as_deleted = table.Column<int>(type: "integer", nullable: false),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_directory_snapshot_pages", x => new { x.snapshot_id, x.page_number });
                    table.ForeignKey(
                        name: "fk_directory_snapshot_pages_directory_snapshots_snapshot_id",
                        column: x => x.snapshot_id,
                        principalTable: "directory_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_directory_snapshots_enumeration_start",
                table: "directory_snapshots",
                column: "enumeration_start");

            migrationBuilder.CreateIndex(
                name: "ix_directory_users_is_active_sort_key",
                table: "directory_users",
                columns: new[] { "is_active", "sort_key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "directory_snapshot_pages");

            migrationBuilder.DropTable(
                name: "directory_users");

            migrationBuilder.DropTable(
                name: "directory_snapshots");
        }
    }
}
