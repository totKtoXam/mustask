using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImportTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_dry_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_version = table.Column<int>(type: "integer", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    totals = table.Column<string>(type: "jsonb", nullable: false),
                    notes = table.Column<string>(type: "jsonb", nullable: false),
                    rows = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_dry_runs", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_dry_runs_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_files", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_files_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    author_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    settings = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_templates_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_import_dry_runs_expires_at",
                table: "import_dry_runs",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_import_dry_runs_organization_id",
                table: "import_dry_runs",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_files_expires_at",
                table: "import_files",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_import_files_organization_user",
                table: "import_files",
                columns: new[] { "organization_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ux_import_templates_organization_name_key",
                table: "import_templates",
                columns: new[] { "organization_id", "name_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_dry_runs");

            migrationBuilder.DropTable(
                name: "import_files");

            migrationBuilder.DropTable(
                name: "import_templates");
        }
    }
}
