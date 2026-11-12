using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScopedGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "area_grants",
                columns: table => new
                {
                    area_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    granted_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_area_grants", x => new { x.area_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_area_grants_areas_organization_id_area_id",
                        columns: x => new { x.organization_id, x.area_id },
                        principalTable: "areas",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_area_grants_organization_memberships",
                        columns: x => new { x.organization_id, x.user_id },
                        principalTable: "organization_memberships",
                        principalColumns: new[] { "organization_id", "user_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_grants",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    granted_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_grants", x => new { x.project_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_project_grants_organization_memberships",
                        columns: x => new { x.organization_id, x.user_id },
                        principalTable: "organization_memberships",
                        principalColumns: new[] { "organization_id", "user_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_project_grants_projects_organization_id_project_id",
                        columns: x => new { x.organization_id, x.project_id },
                        principalTable: "projects",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_grants",
                columns: table => new
                {
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    granted_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team_grants", x => new { x.team_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_team_grants_organization_memberships",
                        columns: x => new { x.organization_id, x.user_id },
                        principalTable: "organization_memberships",
                        principalColumns: new[] { "organization_id", "user_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_team_grants_teams_organization_id_team_id",
                        columns: x => new { x.organization_id, x.team_id },
                        principalTable: "teams",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_area_grants_organization_id_area_id",
                table: "area_grants",
                columns: new[] { "organization_id", "area_id" });

            migrationBuilder.CreateIndex(
                name: "ix_area_grants_organization_id_user_id",
                table: "area_grants",
                columns: new[] { "organization_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_project_grants_organization_id_project_id",
                table: "project_grants",
                columns: new[] { "organization_id", "project_id" });

            migrationBuilder.CreateIndex(
                name: "ix_project_grants_organization_id_user_id",
                table: "project_grants",
                columns: new[] { "organization_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_team_grants_organization_id_team_id",
                table: "team_grants",
                columns: new[] { "organization_id", "team_id" });

            migrationBuilder.CreateIndex(
                name: "ix_team_grants_organization_id_user_id",
                table: "team_grants",
                columns: new[] { "organization_id", "user_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "area_grants");

            migrationBuilder.DropTable(
                name: "project_grants");

            migrationBuilder.DropTable(
                name: "team_grants");
        }
    }
}
