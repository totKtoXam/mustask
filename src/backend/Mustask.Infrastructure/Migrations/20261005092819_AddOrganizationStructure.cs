using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                    table.CheckConstraint("ck_organizations_name_length", "char_length(name) BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_organizations_status", "status IN ('Active', 'Deactivated')");
                    table.ForeignKey(
                        name: "fk_organizations_processes_process_id",
                        column: x => x.process_id,
                        principalTable: "processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                    table.CheckConstraint("ck_projects_key_format", "key ~ '^[A-Z][A-Z0-9]{1,9}$'");
                    table.CheckConstraint("ck_projects_name_length", "char_length(name) BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_projects_status", "status IN ('Active', 'Deactivated')");
                    table.ForeignKey(
                        name: "fk_projects_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_first_team = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teams", x => x.id);
                    table.UniqueConstraint("ak_teams_project_id_id", x => new { x.project_id, x.id });
                    table.CheckConstraint("ck_teams_first_team_active", "NOT (is_first_team AND status = 'Deleted')");
                    table.CheckConstraint("ck_teams_name_length", "char_length(name) BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_teams_status", "status IN ('Active', 'Deleted')");
                    table.ForeignKey(
                        name: "fk_teams_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "areas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    owner_team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    default_of_team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_areas", x => x.id);
                    table.UniqueConstraint("ak_areas_project_id_id", x => new { x.project_id, x.id });
                    table.CheckConstraint("ck_areas_default_owned", "default_of_team_id IS NULL OR default_of_team_id = owner_team_id");
                    table.CheckConstraint("ck_areas_name_length", "char_length(name) BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_areas_root_owned", "parent_id IS NOT NULL OR owner_team_id IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_areas_areas_project_id_parent_id",
                        columns: x => new { x.project_id, x.parent_id },
                        principalTable: "areas",
                        principalColumns: new[] { "project_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_areas_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_areas_teams_project_id_default_of_team_id",
                        columns: x => new { x.project_id, x.default_of_team_id },
                        principalTable: "teams",
                        principalColumns: new[] { "project_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_areas_teams_project_id_owner_team_id",
                        columns: x => new { x.project_id, x.owner_team_id },
                        principalTable: "teams",
                        principalColumns: new[] { "project_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_areas_default_of_team_id",
                table: "areas",
                column: "default_of_team_id",
                unique: true,
                filter: "default_of_team_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_areas_parent_id_normalized_name",
                table: "areas",
                columns: new[] { "parent_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_areas_project_id",
                table: "areas",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_areas_project_id_default_of_team_id",
                table: "areas",
                columns: new[] { "project_id", "default_of_team_id" });

            migrationBuilder.CreateIndex(
                name: "ix_areas_project_id_owner_team_id",
                table: "areas",
                columns: new[] { "project_id", "owner_team_id" });

            migrationBuilder.CreateIndex(
                name: "ix_areas_project_id_parent_id",
                table: "areas",
                columns: new[] { "project_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "ix_areas_project_id_root",
                table: "areas",
                column: "project_id",
                unique: true,
                filter: "parent_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_organizations_normalized_name",
                table: "organizations",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_organizations_process_id",
                table: "organizations",
                column: "process_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_organization_id_key",
                table: "projects",
                columns: new[] { "organization_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_organization_id_normalized_name",
                table: "projects",
                columns: new[] { "organization_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_teams_project_id",
                table: "teams",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_teams_project_id_first_team",
                table: "teams",
                column: "project_id",
                unique: true,
                filter: "is_first_team");

            migrationBuilder.CreateIndex(
                name: "ix_teams_project_id_normalized_name",
                table: "teams",
                columns: new[] { "project_id", "normalized_name" },
                unique: true,
                filter: "status = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "areas");

            migrationBuilder.DropTable(
                name: "teams");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "organizations");
        }
    }
}
