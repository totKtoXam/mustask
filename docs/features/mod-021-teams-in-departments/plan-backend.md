# Plan (backend): mod-021-teams-in-departments

Route L from stage 4. Requirements: `doc-016-teams-under-departments` (`spec.md` BR-TD-1, -4 team part, -5, -19 step 1,
-20; stories US-1, US-2 step 1), decisions in [ADR-0040](../../adr/0040-teams-in-departments-cascading-selectors-sight-by-participation.md)
(left `proposed`; the human accepts it at gate-3). Contract: [api-contract.md](api-contract.md).

## Lanes

None. The work is one connected change of one model (the migration, the team, the area links), so it runs in one
sequence: migration and model, domain, slices, tests.

## Model and migration (additive, then transform, one transaction)

`20261113…_TeamsInDepartments` (one EF migration, one transaction, so all or nothing):

1. **Additive:** `teams.department_id uuid NULL`, `teams.color varchar(16) NULL`, `areas.color`, `projects.color` (all nullable
   for the moment); table `team_place_upgrade_log` (kept, so the MR can name every placement and created department after
   the fact; the `RAISE NOTICE` lines of feat-028 are lost once the migration run's console is gone).
2. **Transform (one PL/pgSQL block):** BR-TD-19 step 1 per organization, in the order of the rule (one department link;
   else most linked projects, active or deactivated, whose project node stands in the department, ties by department name;
   else the first of several links by name; else the department «Команды без департамента», reused by name or created).
   Deleted teams get none. Then BR-TD-20 colours: per organization, departments (creation order, ties by name), then
   department by department its teams and its projects (creation order, ties by name), then projects at the top level; the
   two fallback departments are created before colouring and so are coloured in the same pass. The choice rule is
   implemented once in SQL (hue table in the block) and checked against the domain function by a parity test.
   Every placement, every created department and every dropped department–team link is written to
   `team_place_upgrade_log` and raised as a notice.
3. **Constrain:** `department_id` FK `(organization_id, department_id) -> areas (organization_id, id)` RESTRICT, check
   `ck_teams_department_place` (active has a department, deleted has none), `color` NOT NULL on teams and projects, check
   on areas (`color` set exactly for type 1), palette-key checks, index on `(organization_id, department_id)`;
   drop `area_teams`.
4. **Down:** recreates `area_teams` and one link per placed team (data of dropped links is not restored, as feat-028's Down);
   drops the columns and the log.

Idempotence: the transform only runs once because it runs in the migration; the "running again changes no colour" scenario is
checked by running the block's colouring a second time in the test.

## Domain

- `ColorPalette` (12 keys in order, hues) and `ColorPalette.Choose(IReadOnlyCollection<string> inUse)`: the pure rule
  (none in use: first; an unused entry exists: largest smallest hue-circle distance, ties by palette order; all used: fewest
  uses, ties by palette order). Unit tests: tie, all-used, none-used, the spec's worked examples.
- `Team`: `DepartmentId`, `Color`; `Create(organizationId, departmentId, color, name, description)`, `MoveTo(departmentId)`,
  `Delete()` clears the department. `Area`: `Color` for a department. `Project`: `Color`.
- `AreaTeam`, `AreaLinks` retire.
- Error codes: `organizations.team.has_links`, `organizations.area.has_teams`, field reason `organizations.team.department_required`.

## Slices (Application) and routes (Api)

| Slice | Change |
|---|---|
| AddTeam | `departmentId` required (validation field error, then 404/409 place checks); locks tree lock, department `FOR SHARE`; colour chosen from the department's active teams plus its own colour; `areaIds` retire |
| MoveTeam (new) | `PUT /teams/{teamId}/department`: tree lock, destination `FOR SHARE`, team `FOR NO KEY UPDATE`, link check after the lock; same department is a no-op; keeps the colour |
| DeleteArea | `has_teams` before `not_empty`, after the department's lock |
| DeleteTeam | clears the department; no area links to remove |
| CreateArea | `teamIds` retire; colour for a department |
| CreateProject | colour (department scope, or the top-level scope while projects may stand there: mod-022 retires it) |
| GetAreaTeams / GetAreaDeletion | the department's teams by place |
| GetTeam / GetOrganizationTeams / team writes | `departmentId`, `departmentName`, `color` |
| Selector offers | team `departmentIds` becomes `departmentId`; `color` on the three kinds |
| LinkTeamsToArea, UnlinkTeamFromArea, LinkAreasToTeam, UnlinkAreaFromTeam, GetTeamAreas | removed with their routes, repository, entity and tests |

Authorization is unchanged (OQ-6 is mod-023): team creation keeps `organizations.team.create`; the move declares the same
code (the administrator's) until mod-023 gives it to the department «Менеджер». `organizations.area.link` stays in the
catalog (its bundle contract is not touched here) and is simply unused by routes.

## Concurrency

Every writer that changes a team's place or a department's existence holds the organization's tree lock first
(creation, move, deletion), so they serialize; the destination is then judged by the lock statement's own answer, so a
deleted destination answers 404 `organizations.area.not_found` and never a 500. A link takes the team `FOR SHARE`, the move
`FOR NO KEY UPDATE`: the link check of the move is read after its team lock.

## Tests

Domain (palette, team, area), Application (handlers on fakes), Infrastructure (migration on populated legacy data including
the spec's cast; schema checks), Api integration (every endpoint, races, error codes), existing tests adjusted to the required
department. Only the affected projects run locally.
