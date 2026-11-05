using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <summary>
    /// feat-028 (BR-OL-1, BR-OL-4, BR-OL-13; ADR-0028 rules 4, 5, 12): teams become the organization's, and a project and a team are
    /// joined by a link. The human waived preservation of the existing data (doc-012 § Existing data) and chose to use it in full
    /// (OQ-1 C): <b>the upgrade drops every team</b>, in one atomic migration (plan-backend D-1). A team of the old shape belongs to
    /// one project; the new shape cannot place it, and the names are never compared or merged, so nothing is remapped.
    /// </summary>
    /// <inheritdoc />
    public partial class OrganizationLevelTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. The drop and its log (BR-OL-13), first, so every key that follows is created over empty team tables. The order is the
            // order of the keys: the places before the orders (their key) and before the items' owner (the place key onto the owner
            // is checked at the end of the UPDATE), the orders and the memberships before the teams. work_items.changed_at is NOT
            // written: no item's «Изменён» moves. Dropped: every team (any status, any organization), their memberships, every
            // team place and team order row, every default-team setting. Kept: organizations, memberships, projects, areas, work
            // items (project, number, identifier, assignee, state, estimates, parent, area, changed_at) and the project backlogs.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    teams_dropped integer;
                    memberships_dropped integer;
                    places_dropped integer;
                    orders_dropped integer;
                    settings_dropped integer;
                    items_unowned integer;
                BEGIN
                    DELETE FROM backlog_places;
                    GET DIAGNOSTICS places_dropped = ROW_COUNT;

                    DELETE FROM backlog_orders;
                    GET DIAGNOSTICS orders_dropped = ROW_COUNT;

                    UPDATE work_items SET owning_team_id = NULL WHERE owning_team_id IS NOT NULL;
                    GET DIAGNOSTICS items_unowned = ROW_COUNT;

                    UPDATE projects SET default_team_id = NULL WHERE default_team_id IS NOT NULL;
                    GET DIAGNOSTICS settings_dropped = ROW_COUNT;

                    DELETE FROM team_memberships;
                    GET DIAGNOSTICS memberships_dropped = ROW_COUNT;

                    DELETE FROM teams;
                    GET DIAGNOSTICS teams_dropped = ROW_COUNT;

                    RAISE NOTICE 'feat-028: % teams, % team memberships, % team places, % team order rows and % default-team settings dropped; % work items made without a team',
                        teams_dropped, memberships_dropped, places_dropped, orders_dropped, settings_dropped, items_unowned;
                END
                $$;
                """);

            // 2. The old shape goes: the keys onto teams (project_id, id) and onto teams (id), the project column with its indexes.
            migrationBuilder.DropForeignKey(
                name: "fk_work_items_teams_project_id_owning_team_id",
                table: "work_items");

            migrationBuilder.DropForeignKey(
                name: "fk_projects_teams_id_default_team_id",
                table: "projects");

            migrationBuilder.DropForeignKey(
                name: "fk_team_memberships_teams_team_id",
                table: "team_memberships");

            migrationBuilder.DropForeignKey(
                name: "fk_teams_projects_project_id",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "ix_teams_project_id_normalized_name",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "ix_teams_project_id",
                table: "teams");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_teams_project_id_id",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "project_id",
                table: "teams");

            // 3. The new shape. The generated default of the new organization_id is deliberately absent: the table is empty at this
            // step, and a default would let a row in with no organization.
            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "teams",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "teams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_teams_organization_id_id",
                table: "teams",
                columns: new[] { "organization_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_projects_organization_id_id",
                table: "projects",
                columns: new[] { "organization_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_teams_organization_id_normalized_name",
                table: "teams",
                columns: new[] { "organization_id", "normalized_name" },
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_teams_description_length",
                table: "teams",
                sql: "description IS NULL OR char_length(description) BETWEEN 1 AND 2000");

            migrationBuilder.AddCheckConstraint(
                name: "ck_projects_description_length",
                table: "projects",
                sql: "description IS NULL OR char_length(description) BETWEEN 1 AND 2000");

            migrationBuilder.AddForeignKey(
                name: "fk_teams_organizations_organization_id",
                table: "teams",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // 4. The link, and the three keys that now reference it or the team's new alternate key.
            migrationBuilder.CreateTable(
                name: "project_teams",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_teams", x => new { x.project_id, x.team_id });
                    table.ForeignKey(
                        name: "fk_project_teams_projects_organization_id_project_id",
                        columns: x => new { x.organization_id, x.project_id },
                        principalTable: "projects",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_teams_teams_organization_id_team_id",
                        columns: x => new { x.organization_id, x.team_id },
                        principalTable: "teams",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_teams_organization_id_project_id",
                table: "project_teams",
                columns: new[] { "organization_id", "project_id" });

            migrationBuilder.CreateIndex(
                name: "ix_project_teams_organization_id_team_id",
                table: "project_teams",
                columns: new[] { "organization_id", "team_id" });

            migrationBuilder.CreateIndex(
                name: "ix_project_teams_team_id_project_id",
                table: "project_teams",
                columns: new[] { "team_id", "project_id" });

            migrationBuilder.CreateIndex(
                name: "ix_team_memberships_organization_id_team_id",
                table: "team_memberships",
                columns: new[] { "organization_id", "team_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_projects_project_teams_id_default_team_id",
                table: "projects",
                columns: new[] { "id", "default_team_id" },
                principalTable: "project_teams",
                principalColumns: new[] { "project_id", "team_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_work_items_project_teams_project_id_owning_team_id",
                table: "work_items",
                columns: new[] { "project_id", "owning_team_id" },
                principalTable: "project_teams",
                principalColumns: new[] { "project_id", "team_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_team_memberships_teams_organization_id_team_id",
                table: "team_memberships",
                columns: new[] { "organization_id", "team_id" },
                principalTable: "teams",
                principalColumns: new[] { "organization_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the SHAPE the previous build needs. A team of the organization may have no project or many, so it cannot go
            // back to "its" project: the teams are emptied again, with what hangs on them (the same deletes as Up, without the log),
            // and no dropped row is recreated — but each project is given one team named after it at the end (see there). The previous
            // build needs a stand reset after a Down (plan-backend § 4.2).
            migrationBuilder.Sql(
                """
                DELETE FROM backlog_places;
                DELETE FROM backlog_orders;
                UPDATE work_items SET owning_team_id = NULL WHERE owning_team_id IS NOT NULL;
                UPDATE projects SET default_team_id = NULL WHERE default_team_id IS NOT NULL;
                DELETE FROM team_memberships;
                DELETE FROM project_teams;
                DELETE FROM teams;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_projects_project_teams_id_default_team_id",
                table: "projects");

            migrationBuilder.DropForeignKey(
                name: "fk_work_items_project_teams_project_id_owning_team_id",
                table: "work_items");

            migrationBuilder.DropForeignKey(
                name: "fk_team_memberships_teams_organization_id_team_id",
                table: "team_memberships");

            migrationBuilder.DropForeignKey(
                name: "fk_teams_organizations_organization_id",
                table: "teams");

            migrationBuilder.DropTable(
                name: "project_teams");

            migrationBuilder.DropIndex(
                name: "ix_team_memberships_organization_id_team_id",
                table: "team_memberships");

            migrationBuilder.DropIndex(
                name: "ix_teams_organization_id_normalized_name",
                table: "teams");

            migrationBuilder.DropCheckConstraint(
                name: "ck_teams_description_length",
                table: "teams");

            migrationBuilder.DropCheckConstraint(
                name: "ck_projects_description_length",
                table: "projects");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_teams_organization_id_id",
                table: "teams");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_projects_organization_id_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "description",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "description",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "teams");

            migrationBuilder.AddColumn<Guid>(
                name: "project_id",
                table: "teams",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_teams_project_id_id",
                table: "teams",
                columns: new[] { "project_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_teams_project_id",
                table: "teams",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_teams_project_id_normalized_name",
                table: "teams",
                columns: new[] { "project_id", "normalized_name" },
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.AddForeignKey(
                name: "fk_teams_projects_project_id",
                table: "teams",
                column: "project_id",
                principalTable: "projects",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_projects_teams_id_default_team_id",
                table: "projects",
                columns: new[] { "id", "default_team_id" },
                principalTable: "teams",
                principalColumns: new[] { "project_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_work_items_teams_project_id_owning_team_id",
                table: "work_items",
                columns: new[] { "project_id", "owning_team_id" },
                principalTable: "teams",
                principalColumns: new[] { "project_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_team_memberships_teams_team_id",
                table: "team_memberships",
                column: "team_id",
                principalTable: "teams",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // The earlier migrations' Down (RetireAreaOwnership) needs every project to have an active team, as mod-002 and mod-017 BR-OT-11
            // guaranteed; the new world lets a project have none. So each project gets one team named after it, as mod-002 created
            // them: the only rows the Down writes (a rollback past this migration is a stand reset, plan-backend § 4.2). The new key on
            // (project_id, normalized_name) is satisfied: one team per project.
            migrationBuilder.Sql(
                """
                INSERT INTO teams (id, project_id, name, normalized_name, status)
                SELECT gen_random_uuid(), project.id, project.name, project.normalized_name, 'Active'
                FROM projects AS project;
                """);
        }
    }
}
