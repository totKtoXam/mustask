namespace Mustask.Infrastructure.Migrations;

/// <summary>
/// mod-022 (doc-016 BR-TD-19 steps 2 and 3, BR-TD-20): the upgrade's SQL, one script, kept apart from the migration so that a test can run
/// it a second time on a migrated database and show that it is idempotent (a second run finds no top-level project and no link across
/// two departments, so it changes and logs nothing). Per organization, in this order:
/// (2) every project standing at the top level, active or deactivated, moves into the department holding most of its linked teams as
/// mod-021 placed them (ties by department name), else into the department «Проекты без департамента» (reused when the organization has
/// one, created and coloured otherwise); (3) every project–team link whose ends stand in different departments is cut as feat-028 BR-OL-8
/// unlinks with «Без команды» as the destination: the team's items of that project, in any state, lose their owner and sprint pointer,
/// their places on the team's backlog go, the project's default team is cleared if it named the team, the link goes; as feat-028 BR-OL-13
/// did, no item's «Изменён» moves and no history entry is written (there is no actor). Every placement, created department and cut link
/// is a row of <c>project_place_upgrade_log</c> and a notice; a notice per organization sums up the counts. The colour of a project never
/// changes (BR-TD-20: a project moved keeps its colour); only the created department is coloured.
/// </summary>
public static class ProjectsInDepartmentsUpgrade
{
    public const string FallbackName = "Проекты без департамента";

    /// <summary>Creates the log table the script writes (the migration runs it once; the table is not mapped by the model).</summary>
    public const string CreateLog =
        """
        CREATE TABLE project_place_upgrade_log (
            id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            migration text NOT NULL,
            organization_id uuid NOT NULL,
            kind text NOT NULL,
            project_id uuid NULL,
            project_name text NULL,
            team_id uuid NULL,
            team_name text NULL,
            department_id uuid NULL,
            department_name text NULL,
            rule text NULL,
            item_count integer NULL,
            logged_at timestamp with time zone NOT NULL DEFAULT now()
        );
        """;

    /// <summary>The colour rule of BR-TD-20 as a function of this session, then the transform block.</summary>
    public const string Script =
        """
        CREATE OR REPLACE FUNCTION pg_temp.mustask_choose_color(in_use text[]) RETURNS text
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

        DO $block$
        DECLARE
            v_org record;
            v_project record;
            v_link record;
            v_fallback_id uuid;
            v_fallback_name constant text := 'Проекты без департамента';
            v_fallback_normalized constant text := 'ПРОЕКТЫ БЕЗ ДЕПАРТАМЕНТА';
            v_target uuid;
            v_target_name text;
            v_rule text;
            v_tied integer;
            v_items integer;
            v_created integer;
            v_placed integer;
            v_cut integer;
            v_released integer;
        BEGIN
            FOR v_org IN SELECT id FROM organizations ORDER BY id LOOP
                v_fallback_id := NULL;
                v_created := 0;
                v_placed := 0;
                v_cut := 0;
                v_released := 0;

                -- Step 2 (BR-TD-19): every project still at the top level takes a department.
                FOR v_project IN
                    SELECT pr.id, pr.name, node.id AS node_id
                    FROM projects pr
                    JOIN areas node ON node.project_id = pr.id AND node.type = 2
                    WHERE pr.organization_id = v_org.id AND node.parent_id IS NULL
                    ORDER BY pr.created_at, pr.name COLLATE "ru-x-icu", pr.id
                LOOP
                    v_target := NULL;
                    v_rule := NULL;

                    -- The department holding most of the project's linked teams, as step 1 placed them (an active team only: a deleted
                    -- team stands in none); ties by department name.
                    SELECT p.dept_id,
                           (SELECT count(*) FROM (
                               SELECT t.department_id, count(*) AS n
                               FROM project_teams pt JOIN teams t ON t.id = pt.team_id
                               WHERE pt.project_id = v_project.id AND t.department_id IS NOT NULL
                               GROUP BY t.department_id) q WHERE q.n = p.n)
                    INTO v_target, v_tied
                    FROM (
                        SELECT t.department_id AS dept_id, count(*) AS n
                        FROM project_teams pt JOIN teams t ON t.id = pt.team_id
                        WHERE pt.project_id = v_project.id AND t.department_id IS NOT NULL
                        GROUP BY t.department_id) p
                    JOIN areas a ON a.id = p.dept_id
                    ORDER BY p.n DESC, a.name COLLATE "ru-x-icu", a.id
                    LIMIT 1;

                    IF v_target IS NOT NULL THEN
                        v_rule := CASE WHEN v_tied > 1 THEN 'tie_by_name' ELSE 'most_teams' END;
                    ELSE
                        IF v_fallback_id IS NULL THEN
                            SELECT id INTO v_fallback_id FROM areas
                            WHERE organization_id = v_org.id AND parent_id IS NULL AND type = 1 AND normalized_name = v_fallback_normalized;

                            IF v_fallback_id IS NULL THEN
                                IF EXISTS (SELECT 1 FROM areas WHERE organization_id = v_org.id AND parent_id IS NULL AND normalized_name = v_fallback_normalized) THEN
                                    RAISE EXCEPTION 'mod-022: organization % has a top-level node named % that is not a department; rename it and run the migration again', v_org.id, v_fallback_name;
                                END IF;

                                v_fallback_id := gen_random_uuid();
                                INSERT INTO areas (id, organization_id, parent_id, type, project_id, name, normalized_name, description, inherits_access, created_by, created_at, color)
                                VALUES (v_fallback_id, v_org.id, NULL, 1, NULL, v_fallback_name, v_fallback_normalized, NULL, true, 'mod-022-upgrade', now(),
                                        pg_temp.mustask_choose_color(ARRAY(SELECT color FROM areas WHERE organization_id = v_org.id AND type = 1 AND id <> v_fallback_id)));
                                INSERT INTO project_place_upgrade_log (migration, organization_id, kind, department_id, department_name)
                                VALUES ('mod-022', v_org.id, 'department_created', v_fallback_id, v_fallback_name);
                                RAISE NOTICE 'mod-022: organization %: department created: %', v_org.id, v_fallback_name;
                                v_created := v_created + 1;
                            END IF;
                        END IF;

                        v_target := v_fallback_id;
                        v_rule := 'fallback';
                    END IF;

                    UPDATE areas SET parent_id = v_target WHERE id = v_project.node_id;
                    SELECT name INTO v_target_name FROM areas WHERE id = v_target;
                    INSERT INTO project_place_upgrade_log (migration, organization_id, kind, project_id, project_name, department_id, department_name, rule)
                    VALUES ('mod-022', v_org.id, 'project_placed', v_project.id, v_project.name, v_target, v_target_name, v_rule);
                    RAISE NOTICE 'mod-022: organization %: project % placed in % (%)', v_org.id, v_project.name, v_target_name, v_rule;
                    v_placed := v_placed + 1;
                END LOOP;

                -- Step 3 (BR-TD-19): every link whose ends stand in different departments is cut like an unlink to «Без команды».
                FOR v_link IN
                    SELECT pt.project_id, pt.team_id, pr.name AS project_name, t.name AS team_name, node.parent_id AS project_department
                    FROM project_teams pt
                    JOIN projects pr ON pr.id = pt.project_id
                    JOIN teams t ON t.id = pt.team_id
                    JOIN areas node ON node.project_id = pr.id AND node.type = 2
                    WHERE pt.organization_id = v_org.id AND t.department_id IS NOT NULL AND t.department_id IS DISTINCT FROM node.parent_id
                    ORDER BY pr.name COLLATE "ru-x-icu", t.name COLLATE "ru-x-icu", pt.project_id, pt.team_id
                LOOP
                    -- In the order of the keys: the setting that names the link, the places and the owner on its items, then the link.
                    UPDATE projects SET default_team_id = NULL WHERE id = v_link.project_id AND default_team_id = v_link.team_id;

                    DELETE FROM backlog_places AS place
                    USING work_items AS item
                    WHERE item.id = place.work_item_id AND item.project_id = v_link.project_id AND place.team_id = v_link.team_id;

                    -- Every state: a deleted item is owned until its team releases it. «Изменён» (changed_at) is not touched.
                    UPDATE work_items SET owning_team_id = NULL, sprint_id = NULL
                    WHERE project_id = v_link.project_id AND owning_team_id = v_link.team_id;
                    GET DIAGNOSTICS v_items = ROW_COUNT;

                    DELETE FROM project_teams WHERE project_id = v_link.project_id AND team_id = v_link.team_id;

                    INSERT INTO project_place_upgrade_log (migration, organization_id, kind, project_id, project_name, team_id, team_name, department_id, item_count)
                    VALUES ('mod-022', v_org.id, 'link_cut', v_link.project_id, v_link.project_name, v_link.team_id, v_link.team_name, v_link.project_department, v_items);
                    RAISE NOTICE 'mod-022: organization %: link cut: team % from project % (% items made without a team)', v_org.id, v_link.team_name, v_link.project_name, v_items;
                    v_cut := v_cut + 1;
                    v_released := v_released + v_items;
                END LOOP;

                IF v_created > 0 OR v_placed > 0 OR v_cut > 0 THEN
                    RAISE NOTICE 'mod-022: organization %: % departments created, % projects placed, % links cut, % items made without a team', v_org.id, v_created, v_placed, v_cut, v_released;
                END IF;
            END LOOP;
        END
        $block$;
        """;
}
