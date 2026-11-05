using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <summary>
    /// mod-017 (the data drop, OQ-4 A: the human waived preserving the old data). Areas stop owning anything and no team is the
    /// first: the three markers (<c>areas.owner_team_id</c>, <c>areas.default_of_team_id</c>, <c>teams.is_first_team</c>) go, with
    /// their keys, indexes and checks. What is dropped, and only that: the <b>stale team places</b> (<c>backlog_places</c> rows
    /// whose team is not the item's owning team after <c>AddWorkItemOwningTeam</c>: places an area edit left behind, already
    /// unread) and the three markers' values. No item, area, team, project, project place, team membership or other row is
    /// deleted. Each count is logged with <c>RAISE NOTICE</c>.
    /// </summary>
    public partial class RetireAreaOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. The stale team places. A place is read only on the backlog of the item's owner, so a place that names another
            // team is a leftover of an area edit and unread; one that names the item's mapped owner stays (BR-OT-14). An item
            // with no owning team keeps no team place (its owner is none). The place key below would refuse them.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    stale integer;
                    kept integer;
                BEGIN
                    DELETE FROM backlog_places AS place
                    USING work_items AS item
                    WHERE item.id = place.work_item_id
                      AND place.team_id IS DISTINCT FROM item.owning_team_id;

                    GET DIAGNOSTICS stale = ROW_COUNT;

                    SELECT count(*) INTO kept FROM backlog_places;

                    RAISE NOTICE 'mod-017: % stale team places deleted; % team places kept', stale, kept;
                END
                $$;
                """);

            // 2. The place key (ADR-0028 rule 5, D-11): a team place names the item's current owning team. Added in SQL because
            // EF cannot declare a principal key over the nullable owning_team_id. A place of an item with no owner, or of
            // another team, is refused with 23503; the unique index (id, owning_team_id) of AddWorkItemOwningTeam is its target.
            migrationBuilder.Sql(
                """
                ALTER TABLE backlog_places
                    ADD CONSTRAINT fk_backlog_places_work_items_owner
                    FOREIGN KEY (work_item_id, team_id)
                    REFERENCES work_items (id, owning_team_id);
                """);

            // 3. The count of the markers about to go: explicit area owners, default areas, the first teams.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    owners integer;
                    defaults integer;
                    firsts integer;
                BEGIN
                    SELECT count(*) INTO owners FROM areas WHERE owner_team_id IS NOT NULL;
                    SELECT count(*) INTO defaults FROM areas WHERE default_of_team_id IS NOT NULL;
                    SELECT count(*) INTO firsts FROM teams WHERE is_first_team;

                    RAISE NOTICE 'mod-017: % area owners, % default areas and % first-team markers dropped', owners, defaults, firsts;
                END
                $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_areas_teams_project_id_default_of_team_id",
                table: "areas");

            migrationBuilder.DropForeignKey(
                name: "fk_areas_teams_project_id_owner_team_id",
                table: "areas");

            migrationBuilder.DropIndex(
                name: "ix_teams_project_id_first_team",
                table: "teams");

            migrationBuilder.DropCheckConstraint(
                name: "ck_teams_first_team_active",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "ix_areas_default_of_team_id",
                table: "areas");

            migrationBuilder.DropIndex(
                name: "ix_areas_project_id_default_of_team_id",
                table: "areas");

            migrationBuilder.DropIndex(
                name: "ix_areas_project_id_owner_team_id",
                table: "areas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_areas_default_owned",
                table: "areas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_areas_root_owned",
                table: "areas");

            migrationBuilder.DropColumn(
                name: "is_first_team",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "default_of_team_id",
                table: "areas");

            migrationBuilder.DropColumn(
                name: "owner_team_id",
                table: "areas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The place key goes first: it is the newest object and the old schema has none.
            migrationBuilder.Sql("ALTER TABLE backlog_places DROP CONSTRAINT IF EXISTS fk_backlog_places_work_items_owner;");

            migrationBuilder.AddColumn<bool>(
                name: "is_first_team",
                table: "teams",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "default_of_team_id",
                table: "areas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_team_id",
                table: "areas",
                type: "uuid",
                nullable: true);

            // What the old checks require, and nothing the drop took for good: the earliest active team of each project (UUIDv7
            // order, the order of creation) is its first team, and the root is owned by it and is its default area. The
            // explicit owners and the other teams' default areas are the data OQ-4 A dropped; they are not recreated, and the
            // previous build needs a stand reset after this rollback.
            migrationBuilder.Sql(
                """
                UPDATE teams AS team
                SET is_first_team = TRUE
                WHERE team.status = 'Active'
                  AND team.id = (
                      SELECT candidate.id
                      FROM teams AS candidate
                      WHERE candidate.project_id = team.project_id AND candidate.status = 'Active'
                      ORDER BY candidate.id
                      LIMIT 1);

                UPDATE areas AS root
                SET owner_team_id = team.id, default_of_team_id = team.id
                FROM teams AS team
                WHERE root.parent_id IS NULL AND team.project_id = root.project_id AND team.is_first_team;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_teams_project_id_first_team",
                table: "teams",
                column: "project_id",
                unique: true,
                filter: "is_first_team");

            migrationBuilder.AddCheckConstraint(
                name: "ck_teams_first_team_active",
                table: "teams",
                sql: "NOT (is_first_team AND status = 'Deleted')");

            migrationBuilder.CreateIndex(
                name: "ix_areas_default_of_team_id",
                table: "areas",
                column: "default_of_team_id",
                unique: true,
                filter: "default_of_team_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_areas_project_id_default_of_team_id",
                table: "areas",
                columns: new[] { "project_id", "default_of_team_id" });

            migrationBuilder.CreateIndex(
                name: "ix_areas_project_id_owner_team_id",
                table: "areas",
                columns: new[] { "project_id", "owner_team_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_areas_default_owned",
                table: "areas",
                sql: "default_of_team_id IS NULL OR default_of_team_id = owner_team_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_areas_root_owned",
                table: "areas",
                sql: "parent_id IS NOT NULL OR owner_team_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_areas_teams_project_id_default_of_team_id",
                table: "areas",
                columns: new[] { "project_id", "default_of_team_id" },
                principalTable: "teams",
                principalColumns: new[] { "project_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_areas_teams_project_id_owner_team_id",
                table: "areas",
                columns: new[] { "project_id", "owner_team_id" },
                principalTable: "teams",
                principalColumns: new[] { "project_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
