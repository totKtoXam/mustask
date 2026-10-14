using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organization_memberships",
                columns: table => new
                {
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    added_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    role_changed_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    role_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organization_memberships", x => new { x.organization_id, x.user_id });
                    table.CheckConstraint("ck_organization_memberships_role", "role IN ('admin', 'member', 'viewer')");
                    table.CheckConstraint("ck_organization_memberships_role_change", "(role_changed_by IS NULL) = (role_changed_at IS NULL)");
                    table.ForeignKey(
                        name: "fk_organization_memberships_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "team_memberships",
                columns: table => new
                {
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team_memberships", x => new { x.team_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_team_memberships_organization_memberships",
                        columns: x => new { x.organization_id, x.user_id },
                        principalTable: "organization_memberships",
                        principalColumns: new[] { "organization_id", "user_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_team_memberships_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_organization_memberships_user_id",
                table: "organization_memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_team_memberships_organization_id_user_id",
                table: "team_memberships",
                columns: new[] { "organization_id", "user_id" });

            // The seed of mod-016 BR-M1-25 / status.md D-3, written in raw SQL on purpose: a migration must not depend on the
            // domain's evolution. It is in the migration, not at startup, so it is atomic with the tables (there is never
            // an instant with the tables and no administrator), runs exactly once, and is reversed by the same mechanism
            // as the schema. It needs only columns already stored.
            //
            // 1. Each existing organization, active or deactivated, gets exactly one membership: its recorded creator as
            //    admin, added by the creator at the organization's creation time, no role change — whatever the user
            //    projection says of the creator (D-10). Nobody else is seeded (D-3).
            migrationBuilder.Sql(
                """
                INSERT INTO organization_memberships (organization_id, user_id, role, added_by, added_at)
                SELECT id, created_by, 'admin', created_by, created_at
                FROM organizations;
                """);

            // 2. A project's recorded creator joins its first team, and only when they are then a member of the project's
            //    organization (BR-M1-10); no other team gains a member. Under feat-028 (teams move to the organization)
            //    this statement is dropped when this migration is regenerated on its snapshot (BR-M1-25's merge-order clause).
            migrationBuilder.Sql(
                """
                INSERT INTO team_memberships (team_id, organization_id, user_id)
                SELECT t.id, p.organization_id, p.created_by
                FROM projects p
                JOIN teams t ON t.project_id = p.id AND t.is_first_team
                JOIN organization_memberships m ON m.organization_id = p.organization_id AND m.user_id = p.created_by;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "team_memberships");

            migrationBuilder.DropTable(
                name: "organization_memberships");
        }
    }
}
