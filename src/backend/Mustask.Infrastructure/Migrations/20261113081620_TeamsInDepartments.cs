using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <summary>
    /// mod-021 (doc-016 BR-TD-1, BR-TD-19 step 1, BR-TD-20; ADR-0040): every active team stands in one department, the department-team
    /// links retire, and every department, team and project gets a stored palette colour. The tables hold stand and production data,
    /// so the migration is additive, then transform, then constrain, all in this one migration's transaction (all or nothing):
    /// (1) the new columns are added nullable; (2) one PL/pgSQL block places every active team and colours every object, and writes
    /// team_place_upgrade_log (the MR names each placement, created department and retired link from it); (3) the columns turn
    /// required, the keys and checks are added and area_teams is dropped. Nothing is deleted before it is logged. The colour rule
    /// is the one of ColorPalette.Choose, written once more in SQL here; a parity test keeps the two equal.
    /// </summary>
    /// <inheritdoc />
    public partial class TeamsInDepartments : Migration
    {
        private const string Palette = "color IN ('blue', 'orange', 'green', 'purple', 'red', 'teal', 'amber', 'pink', 'indigo', 'lime', 'magenta', 'cyan')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Additive: nothing is required yet, so every existing row stays valid while the transform runs.
            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                table: "teams",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "teams",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "projects",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "areas",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            // The log outlives the migration run: a notice is lost with the console, a row is not. Not mapped by the model.
            migrationBuilder.Sql(
                """
                CREATE TABLE team_place_upgrade_log (
                    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    migration text NOT NULL,
                    organization_id uuid NOT NULL,
                    kind text NOT NULL,
                    team_id uuid NULL,
                    team_name text NULL,
                    department_id uuid NULL,
                    department_name text NULL,
                    rule text NULL,
                    logged_at timestamp with time zone NOT NULL DEFAULT now()
                );
                """);

            // 2. Transform. The colour rule (BR-TD-20) first, as a function that lives in this session only.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION pg_temp.mustask_choose_color(in_use text[]) RETURNS text
                LANGUAGE plpgsql AS $f$
                DECLARE
                    keys constant text[] := ARRAY['blue','orange','green','purple','red','teal','amber','pink','indigo','lime','magenta','cyan'];
                    hues constant integer[] := ARRAY[206,24,146,272,359,180,43,330,238,76,300,195];
                    counts integer[] := array_fill(0, ARRAY[12]);
                    k text;
                    i integer;
                    j integer;
                    used integer := 0;
                    best integer := 0;
                    best_distance integer := -1;
                    nearest integer;
                    d integer;
                    fewest integer;
                BEGIN
                    FOREACH k IN ARRAY coalesce(in_use, ARRAY[]::text[]) LOOP
                        i := array_position(keys, k);
                        IF i IS NOT NULL THEN
                            counts[i] := counts[i] + 1;
                        END IF;
                    END LOOP;

                    FOR i IN 1..12 LOOP
                        IF counts[i] > 0 THEN
                            used := used + 1;
                        END IF;
                    END LOOP;

                    IF used = 0 THEN
                        RETURN keys[1];
                    END IF;

                    IF used < 12 THEN
                        FOR i IN 1..12 LOOP
                            IF counts[i] = 0 THEN
                                nearest := 360;
                                FOR j IN 1..12 LOOP
                                    IF counts[j] > 0 THEN
                                        d := abs(hues[i] - hues[j]) % 360;
                                        nearest := least(nearest, least(d, 360 - d));
                                    END IF;
                                END LOOP;
                                IF nearest > best_distance THEN
                                    best := i;
                                    best_distance := nearest;
                                END IF;
                            END IF;
                        END LOOP;
                        RETURN keys[best];
                    END IF;

                    fewest := (SELECT min(c) FROM unnest(counts) AS c);
                    FOR i IN 1..12 LOOP
                        IF counts[i] = fewest THEN
                            RETURN keys[i];
                        END IF;
                    END LOOP;
                END
                $f$;
                """);

            // BR-TD-19 step 1, then BR-TD-20. Per organization. The order of the colours inside one scope is the order of creation,
            // ties by name: departments and projects by created_at; a team has no creation time, so by the millisecond clock a
            // version 7 id carries.
            migrationBuilder.Sql(
                """
                DO $block$
                DECLARE
                    v_org record;
                    v_team record;
                    v_dept record;
                    v_item record;
                    v_fallback_id uuid;
                    v_fallback_name constant text := 'Команды без департамента';
                    v_fallback_normalized constant text := 'КОМАНДЫ БЕЗ ДЕПАРТАМЕНТА';
                    v_target uuid;
                    v_rule text;
                    v_links integer;
                    v_tied integer;
                    v_color text;
                    v_used text[];
                    v_placed integer;
                    v_created integer;
                BEGIN
                    FOR v_org IN SELECT id FROM organizations ORDER BY id LOOP
                        v_fallback_id := NULL;
                        v_placed := 0;
                        v_created := 0;

                        -- Step 1 (BR-TD-19): each active team takes a department.
                        FOR v_team IN
                            SELECT id, name FROM teams
                            WHERE organization_id = v_org.id AND status = 'Active'
                            ORDER BY id
                        LOOP
                            v_target := NULL;
                            v_rule := NULL;
                            SELECT count(*) INTO v_links FROM area_teams WHERE team_id = v_team.id;

                            IF v_links = 1 THEN
                                SELECT area_id INTO v_target FROM area_teams WHERE team_id = v_team.id;
                                v_rule := 'one_link';
                            ELSE
                                -- The department holding most of the team's linked projects, any status; a project at the top
                                -- level counts for none.
                                SELECT p.dept_id,
                                       (SELECT count(*) FROM (
                                           SELECT node.parent_id, count(*) AS n
                                           FROM project_teams pt
                                           JOIN areas node ON node.project_id = pt.project_id AND node.type = 2
                                           WHERE pt.team_id = v_team.id AND node.parent_id IS NOT NULL
                                           GROUP BY node.parent_id) q WHERE q.n = p.n)
                                INTO v_target, v_tied
                                FROM (
                                    SELECT node.parent_id AS dept_id, count(*) AS n
                                    FROM project_teams pt
                                    JOIN areas node ON node.project_id = pt.project_id AND node.type = 2
                                    WHERE pt.team_id = v_team.id AND node.parent_id IS NOT NULL
                                    GROUP BY node.parent_id) p
                                JOIN areas a ON a.id = p.dept_id
                                ORDER BY p.n DESC, a.name COLLATE "ru-x-icu", a.id
                                LIMIT 1;

                                IF v_target IS NOT NULL THEN
                                    v_rule := CASE WHEN v_tied > 1 THEN 'tie_by_name' ELSE 'most_projects' END;
                                ELSIF v_links >= 2 THEN
                                    SELECT lnk.area_id INTO v_target
                                    FROM area_teams lnk JOIN areas a ON a.id = lnk.area_id
                                    WHERE lnk.team_id = v_team.id
                                    ORDER BY a.name COLLATE "ru-x-icu", a.id
                                    LIMIT 1;
                                    v_rule := 'first_link';
                                END IF;
                            END IF;

                            IF v_target IS NULL THEN
                                IF v_fallback_id IS NULL THEN
                                    SELECT id INTO v_fallback_id FROM areas
                                    WHERE organization_id = v_org.id AND parent_id IS NULL AND type = 1 AND normalized_name = v_fallback_normalized;

                                    IF v_fallback_id IS NULL THEN
                                        IF EXISTS (SELECT 1 FROM areas WHERE organization_id = v_org.id AND parent_id IS NULL AND normalized_name = v_fallback_normalized) THEN
                                            RAISE EXCEPTION 'mod-021: organization % has a top-level node named % that is not a department; rename it and run the migration again', v_org.id, v_fallback_name;
                                        END IF;

                                        v_fallback_id := gen_random_uuid();
                                        INSERT INTO areas (id, organization_id, parent_id, type, project_id, name, normalized_name, description, inherits_access, created_by, created_at)
                                        VALUES (v_fallback_id, v_org.id, NULL, 1, NULL, v_fallback_name, v_fallback_normalized, NULL, true, 'mod-021-upgrade', now());
                                        INSERT INTO team_place_upgrade_log (migration, organization_id, kind, department_id, department_name)
                                        VALUES ('mod-021', v_org.id, 'department_created', v_fallback_id, v_fallback_name);
                                        v_created := v_created + 1;
                                    END IF;
                                END IF;

                                v_target := v_fallback_id;
                                v_rule := 'fallback';
                            END IF;

                            UPDATE teams SET department_id = v_target WHERE id = v_team.id;
                            INSERT INTO team_place_upgrade_log (migration, organization_id, kind, team_id, team_name, department_id, department_name, rule)
                            SELECT 'mod-021', v_org.id, 'team_placed', v_team.id, v_team.name, a.id, a.name, v_rule FROM areas a WHERE a.id = v_target;
                            v_placed := v_placed + 1;
                        END LOOP;

                        -- BR-TD-20, departments of the organization: in order of creation, ties by name.
                        v_used := ARRAY[]::text[];
                        FOR v_dept IN
                            SELECT id FROM areas WHERE organization_id = v_org.id AND type = 1
                            ORDER BY created_at, name COLLATE "ru-x-icu", id
                        LOOP
                            v_color := pg_temp.mustask_choose_color(v_used);
                            UPDATE areas SET color = v_color WHERE id = v_dept.id;
                            v_used := v_used || v_color;
                        END LOOP;

                        -- Department by department: its teams, then its projects (any status; only an active one holds a colour in use).
                        FOR v_dept IN
                            SELECT id, color FROM areas WHERE organization_id = v_org.id AND type = 1
                            ORDER BY created_at, name COLLATE "ru-x-icu", id
                        LOOP
                            v_used := ARRAY[v_dept.color];
                            FOR v_item IN
                                SELECT id FROM teams
                                WHERE department_id = v_dept.id AND status = 'Active'
                                ORDER BY ('x' || substr(replace(id::text, '-', ''), 1, 12))::bit(48)::bigint, name COLLATE "ru-x-icu", id
                            LOOP
                                v_color := pg_temp.mustask_choose_color(v_used);
                                UPDATE teams SET color = v_color WHERE id = v_item.id;
                                v_used := v_used || v_color;
                            END LOOP;

                            v_used := ARRAY[v_dept.color];
                            FOR v_item IN
                                SELECT pr.id, pr.status FROM projects pr
                                JOIN areas node ON node.project_id = pr.id AND node.type = 2
                                WHERE node.parent_id = v_dept.id
                                ORDER BY pr.created_at, pr.name COLLATE "ru-x-icu", pr.id
                            LOOP
                                v_color := pg_temp.mustask_choose_color(v_used);
                                UPDATE projects SET color = v_color WHERE id = v_item.id;
                                IF v_item.status = 'Active' THEN
                                    v_used := v_used || v_color;
                                END IF;
                            END LOOP;
                        END LOOP;

                        -- Projects still at the top level (mod-022 places them): one scope of their own, no department colour.
                        v_used := ARRAY[]::text[];
                        FOR v_item IN
                            SELECT pr.id, pr.status FROM projects pr
                            JOIN areas node ON node.project_id = pr.id AND node.type = 2
                            WHERE pr.organization_id = v_org.id AND node.parent_id IS NULL
                            ORDER BY pr.created_at, pr.name COLLATE "ru-x-icu", pr.id
                        LOOP
                            v_color := pg_temp.mustask_choose_color(v_used);
                            UPDATE projects SET color = v_color WHERE id = v_item.id;
                            IF v_item.status = 'Active' THEN
                                v_used := v_used || v_color;
                            END IF;
                        END LOOP;

                        IF v_placed > 0 OR v_created > 0 THEN
                            RAISE NOTICE 'mod-021: organization %: % teams placed, % departments created', v_org.id, v_placed, v_created;
                        END IF;
                    END LOOP;

                    -- A deleted team stands in no department and frees its colour; the column is required, so it keeps the first entry.
                    UPDATE teams SET color = 'blue' WHERE color IS NULL;
                    -- Anything else still uncoloured (a project whose node is missing) takes the first entry as well.
                    UPDATE projects SET color = 'blue' WHERE color IS NULL;

                    -- The retiring links, every one named in the log before the table goes.
                    INSERT INTO team_place_upgrade_log (migration, organization_id, kind, team_id, team_name, department_id, department_name)
                    SELECT 'mod-021', l.organization_id, 'link_dropped', t.id, t.name, a.id, a.name
                    FROM area_teams l
                    JOIN teams t ON t.id = l.team_id
                    JOIN areas a ON a.id = l.area_id
                    ORDER BY l.organization_id, t.name, a.name;
                END
                $block$;
                """);

            migrationBuilder.Sql("DROP FUNCTION pg_temp.mustask_choose_color(text[]);");

            // 3. Constrain: now every row is complete, the columns are required and the keys can be added.
            migrationBuilder.AlterColumn<string>(
                name: "color",
                table: "teams",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "color",
                table: "projects",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_teams_organization_id_department_id",
                table: "teams",
                columns: new[] { "organization_id", "department_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_teams_color",
                table: "teams",
                sql: Palette);

            migrationBuilder.AddCheckConstraint(
                name: "ck_teams_department_place",
                table: "teams",
                sql: "(status = 'Active' AND department_id IS NOT NULL) OR (status = 'Deleted' AND department_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_projects_color",
                table: "projects",
                sql: Palette);

            migrationBuilder.AddCheckConstraint(
                name: "ck_areas_color",
                table: "areas",
                sql: "(type = 1 AND color IS NOT NULL AND " + Palette + ") OR (type <> 1 AND color IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_teams_areas_organization_id_department_id",
                table: "teams",
                columns: new[] { "organization_id", "department_id" },
                principalTable: "areas",
                principalColumns: new[] { "organization_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropTable(
                name: "area_teams");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The shape the previous build needs, with the link that stands for each active team's department. The links the upgrade
            // dropped are not restored (they are in team_place_upgrade_log, which goes with this Down); a stand reset is the clean way back.
            migrationBuilder.CreateTable(
                name: "area_teams",
                columns: table => new
                {
                    area_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    linked_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_area_teams", x => new { x.area_id, x.team_id });
                    table.ForeignKey(
                        name: "fk_area_teams_areas_organization_id_area_id",
                        columns: x => new { x.organization_id, x.area_id },
                        principalTable: "areas",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_area_teams_teams_organization_id_team_id",
                        columns: x => new { x.organization_id, x.team_id },
                        principalTable: "teams",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_area_teams_organization_id_area_id",
                table: "area_teams",
                columns: new[] { "organization_id", "area_id" });

            migrationBuilder.CreateIndex(
                name: "ix_area_teams_organization_id_team_id",
                table: "area_teams",
                columns: new[] { "organization_id", "team_id" });

            migrationBuilder.CreateIndex(
                name: "ix_area_teams_team_id_area_id",
                table: "area_teams",
                columns: new[] { "team_id", "area_id" });

            migrationBuilder.Sql(
                """
                INSERT INTO area_teams (area_id, team_id, organization_id, linked_by, linked_at)
                SELECT department_id, id, organization_id, 'mod-021-down', now()
                FROM teams
                WHERE status = 'Active' AND department_id IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_teams_areas_organization_id_department_id",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "ix_teams_organization_id_department_id",
                table: "teams");

            migrationBuilder.DropCheckConstraint(
                name: "ck_teams_color",
                table: "teams");

            migrationBuilder.DropCheckConstraint(
                name: "ck_teams_department_place",
                table: "teams");

            migrationBuilder.DropCheckConstraint(
                name: "ck_projects_color",
                table: "projects");

            migrationBuilder.DropCheckConstraint(
                name: "ck_areas_color",
                table: "areas");

            migrationBuilder.DropColumn(
                name: "color",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "color",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "color",
                table: "areas");

            migrationBuilder.Sql("DROP TABLE IF EXISTS team_place_upgrade_log;");
        }
    }
}
