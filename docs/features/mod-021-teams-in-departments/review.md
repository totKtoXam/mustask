# Code review: mod-021-teams-in-departments

## Round 1

Scope: `git diff origin/main...HEAD` (base 873935f7, head 2620c472), the whole diff, one Opus pass with the L security pass.
Read against doc-016 `spec.md` BR-TD-1, -4 (team part), -5, -19 step 1, -20 and its decomposition row, US-1, US-2, `design.md`,
ADR-0040 (proposed), and this folder's `plan-backend.md`, `api-contract.md`, `plan-frontend.md`, `qa.md`, `status.md`. No suite
was run; the existing tests were read.

Verdict: **changes-requested**. 1 major (prose only: no QA re-run is owed, one delta read), 5 minors (debts).

### Findings

#### R-1 (major): prior lines still describe the retired department–team links

The diff changes behaviour that merged artifacts outside it still describe, and doc-016's own "Changes to prior lines" table gives
this line those notes.
- `docs/features/feat-043-area-project-team-navigation/api-contract.md:50,56`: `selector-offers` `teams[].departmentIds` and the
  fit rule built on it. The field is gone (`departmentId` now). Someone writing a client from this contract builds against a
  field that does not exist.
- `docs/features/feat-049-structure-diagram-view/spec.md` BR-SD-3, BR-SD-16 and `design.md:46`: "a team is a child of every
  department it is linked to … a department is still optional for a team (no model change)". This branch draws a team once,
  in its one department.
- `docs/features/feat-028-org-level-teams/status.md`: doc-016 `spec.md` § "Changes to prior lines", row feat-028, asks for "a
  note in its `status.md`". It is not there.
- `docs/features/mod-018-area-workspace/`: the five link routes, BR-AW-6/-7 and the department-end link codes. No note says
  they are retired.

Fix: add one dated note to each of those `status.md` files (feat-028, feat-043, feat-049, mod-018). Each note names what retired
or changed and points to mod-021 and its `api-contract.md` § 1/§ 3. This is the precedent mod-018 set for its owners. Do not
rewrite the merged prose.

#### R-2 (minor): the retired error code is not marked deprecated

`contracts/errors/organizations.json:206` and `docs/error-codes.md:56` (`organizations.area.link_type_not_allowed`): `api-contract.md`
§ 1 says the code is "no longer raised", but the registry's `deprecated` flag and its Deprecated column are not set. Three
retired codes already carry them.
Fix: set `"deprecated": true` and `yes` in the column.

#### R-3 (minor): "a test keeps them equal" has no test behind it

`src/backend/Mustask.Infrastructure/Organizations/OrganizationsConstraints.cs:40-41`: the comment says a test keeps `ColorCheck`
equal to `ColorPalette`. No test compares the two, and none compares the migration's `Palette` constant
(`20261113081620_TeamsInDepartments.cs:20`) with `ColorPalette` either. `Upgrade_LeavesTheConstraintsOfThePlace` only checks
that one foreign key is refused.
Fix: add a domain-vs-constraint test (parse the `IN` list, or insert each palette key and expect it accepted), or weaken the
comment.

#### R-4 (minor): area writes give a project node `color: null`, while the reads give it the project's colour

`RenameAreaHandler`, `SetAreaDescriptionHandler`, `MoveAreaHandler.cs:126` return `area.Color`, which is always `null` for a
project node (`ck_areas_color`). `GET /areas` and `GET /areas/{id}` return that node with its project's colour
(`AreaTreeReadModel`). `api-contract.md` § 3 ("answers gain `color` as well") does not say which. Nothing breaks: the panel
reads the tree again.
Fix: fill the project colour in those three answers, or have the contract say `null` for a project node.

#### R-5 (minor): F-1's list depends on `team.delete` while the move needs `team.create`

`src/frontend/src/features/organizations/components/MoveTeamDialog.tsx:63` reads `GET …/teams/{id}/deletion-counts`, which
declares `organizations.team.delete`; the move declares `organizations.team.create`. Today both codes are the administrator's
alone, so this widens nothing: the server checks the read, and it returns only the team's own linked projects. Under mod-023
a department «Менеджер» may move a team without holding `team.delete`. The read then answers 403, the 403 is swallowed, and
F-1's empty list comes back. Also, the component doc comment (lines 17-24) does not mention the deactivated rows.
Fix: carry the dependency into mod-023's line (a links read under `team.read`, or the 409 carrying the projects), and update
the comment.

#### R-6 (minor): mojibake in a doc comment

`src/backend/Mustask.Application/Features/Organizations/GetAreaTeams/GetAreaTeamsHandler.cs:18`: "areaâteam" is a mis-decoded
en dash.
Fix: change it to "area–team".

### Checked and sound (no finding)

- **Migration, BR-TD-19 step 1** (`TeamsInDepartments.cs:164-236`):
  - Each rule is checked in the spec's order: one link → most linked projects (any status; a top-level project counts for
    none; ties by `name COLLATE "ru-x-icu"`, then id) → with 2+ links, the first link by name → fallback.
  - The fallback department is reused only when it is a top-level type-1 node. If a non-department holds the name, the
    migration raises an error.
  - Deleted teams get no department.
- **All or nothing:** the whole migration is one EF migration in one transaction. `Upgrade_ThatCannotPlaceATeam_FailsAsAWhole`
  proves it: schema and links unchanged.
- **Log:** `team_place_upgrade_log` records every placement with its rule, every created department and every dropped link
  (`Upgrade_LogsEveryPlacement…`). It is written before `area_teams` is dropped.
- **Down:** it recreates `area_teams` with one link per active team. As documented, it loses the dropped links and the log.
  `Down_Restores…` re-runs Up afterwards.
- **BR-TD-20 order in the migration:**
  - Departments by `created_at`, then name. The fallback is created with `now()`, so it is coloured last, with its teams.
  - Then, department by department, teams by their v7-id clock and projects by `created_at`. Every team id in the code is
    `Guid.CreateVersion7`; the only `gen_random_uuid()` team insert is in feat-028's Down.
  - A deactivated project gets a colour but is not counted as in use.
  - The SQL rule matches `ColorPalette.Choose` step for step (strict `>` keeps the earlier palette entry on a tie; fewest-used
    with the same tie rule). The parity test covers the all-used case.
- **Locking and performance:**
  - The migration takes ACCESS EXCLUSIVE on `teams`/`projects`/`areas` for its run, as every constraint-adding migration
    here does (applied explicitly, ADR-0002).
  - The per-team reads use `ix_area_teams_team_id_area_id` and the `project_teams` (team, project) index.
  - The colour loops scan per department before `ix_teams_organization_id_department_id` exists. That is O(departments ×
    rows) and negligible at stand/prod size.
  - Nothing touches `work_items`.
- **Colour concurrency:** every creator of a department, team or project holds the organization tree lock
  (`AddTeamHandler:126`, `CreateAreaHandler`, `CreateProjectHandler:133`) before it reads the colours in use, so two
  creations serialize. These are the only `Create` call sites. The races are tested: `CreateTeam_TwoAtOnce…`,
  `CreateDepartment_TwoAtOnce…`.
- **Move and deletion guards** (`MoveTeamHandler`, `DeleteAreaHandler`):
  - The order is: tree lock → destination `FOR SHARE` (its presence is the lock's own answer, so a gone destination is 404) →
    team `FOR NO KEY UPDATE` → links read.
  - Every link writer takes the team `FOR SHARE` (`LinkProjectsToTeam`, `LinkTeamsToProject`, `CreateProject`), which
    conflicts with the move's lock.
  - A move to the same department is a no-op before the link check.
  - `has_teams` is decided in the domain before `not_empty` and only for a department. The `RESTRICT` FK is the backstop.
  - Races are covered in `AreaRacesEndpointTests:307,340,372,404` and `TeamPlacesQaTests:227`.
- **Security:**
  - The inventory is 100 → 96 routes. `OrganizationAddressedRoutes` is updated.
  - `POST /teams` and `PUT /teams/{id}/department` declare `organizations.team.create` at organization scope.
    `contracts/access/role-bundles.json` is untouched. `organizations.area.link` stays in the catalog and is exempted by
    name in the inventory test.
  - A foreign team or department is 404 (`teamReads.FindAsync` and `LockForShareAsync` filter by organization; a
    composite-key FK backs it up). A non-member gets 404 from the route filter.
  - The retired routes answer 404/405 (`RetiredLinkRoutes…`, `RetiredUnlinkRoute…`), and the retired body fields are
    ignored.
  - All SQL is interpolated (parameterized). There are no secrets.
  - The log stores names and ids only, no personal data.
  - `deletion-counts` (F-1) is server-authorized and scoped to the team: see R-5.
- **F-1 fix (2620c472):**
  - Deactivated linked projects show after `has_links`, tagged «деактивирован», with no «Отключить», plus the restore-first
    line.
  - The claim that such a link cannot be unlinked holds: `UnlinkTeamHandler:84-87` answers 404 for a project that is not
    reachable.
  - The test is now a passing `it` that asserts the name, the tag, the line and the absence of «Отключить».
  - The restore-first copy is the developer's, flagged for gate-3.

### Behavioural claims in prose

Direction 1: claims the diff adds, each traced to what makes it true.
- `api-contract.md` § 1, the retired routes answer 404 → `TeamPlacesEndpointTests:197`, `TeamPlacesQaTests:201`.
- `api-contract.md` § 1, `link_type_not_allowed` no longer raised → no raise left in the code. The registry is not marked:
  R-2.
- `api-contract.md` § 2, the order of answers → `MoveTeamHandler:42-104`.
- `api-contract.md` § 2, the races end in one order → `AreaRacesEndpointTests:307,372`.
- `api-contract.md` § 3, `POST /teams` ignores `areaIds` → `TeamPlacesQaTests:201`.
- `api-contract.md` § 3, the colour on area writes → R-4.
- `api-contract.md` § 3, `DELETE /areas` has `has_teams` before `not_empty` → `Area.cs` `EnsureDeletable`,
  `TeamPlacesQaTests:33`.
- `api-contract.md` § 5, the log columns → migration `:56-67`.
- `plan-backend.md`, "every writer of a place holds the tree lock" → `AddTeamHandler:126`, `MoveTeamHandler:71`,
  `DeleteAreaHandler` `LockTreeAsync`.
- `plan-backend.md`, the SQL rule checked by a parity test → `TeamsInDepartmentsMigrationTests:285`.
- `plan-backend.md`, idempotence → `TeamsInDepartmentsMigrationTests:152`.
- `OrganizationsConstraints.cs:40`, "a test keeps them equal" → R-3.
- `status.md`, backend: deleted teams blue with no department → migration `:301`, `ck_teams_department_place`.
- `status.md`, frontend: the move uses `team.create` → `OrganizationEndpoints` `MapPut("/{teamId}/department")`.
- `status.md`, the F-1 fix: no contract change → it uses the existing `deletion-counts` route.
- `qa.md` D-2: ICU on prod → closed by the orchestrator in `status.md` (`OrganizationsCollation.cs`, `docs/deploy/vm.md`).
- `MoveTeamDialog.tsx:17-24`, the doc comment → stale: R-5.
- `teamTexts.ts` `restoreFirst`, "cannot be unlinked" → `UnlinkTeamHandler:84-87`.

Direction 2: behaviour the diff changes, the identifiers grepped, and what the hits were.
- **Identifiers grepped:** `area_teams`, `AreaTeam`, `/teams/{teamId}/areas`, `areas/{areaId}/teams`,
  `link_type_not_allowed`, `organizations.area.link`, `departmentIds`, `areaIds`.
- **Where:** `docs/`, `.claude/`, `contracts/`, the `AGENTS.md` files and the sibling features.
- **Hits:**
  - ADR-0029/0031/0038: superseded in part by ADR-0040. The index lines are added on acceptance (ADR-0040 § Index), at
    gate-3.
  - `contracts/access/role-bundles.json`: `area.link` stays, as intended.
  - `docs/error-codes.md`, `contracts/errors`: R-2.
  - feat-043 `api-contract.md`, feat-049 `spec.md`/`design.md`, feat-028 `status.md`, mod-018 artifacts: R-1.
  - doc-012 audit and doc-015 analysis: dated analyses, not behaviour claims.
- **Not swept:** prose on the selector fit's dropped "through a project placed in it" path beyond feat-043. Those lines
  (feat-061..065) are out of scope and read doc-016.
