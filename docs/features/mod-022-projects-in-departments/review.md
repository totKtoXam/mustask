# Code review — mod-022-projects-in-departments

## Round 1

One pass over the whole diff `origin/main...HEAD` (merge base 731260bc, HEAD 8b329a0f), security pass included. Clean context,
not the author. Inputs: doc-016 `spec.md` BR-TD-3, -4, -6, -19 steps 2-3, -20; US-2, US-5; `design.md`; ADR-0040; this folder's
plans, contract, `qa.md`, `status.md`; mod-021's review for comparison. No suite run (economy mode); no finding needed proof.

**Verdict: approved.** 0 blockers, 0 majors, 4 minors (debts, see below).

### Checked and found right

- Upgrade `ProjectsInDepartmentsUpgrade.Script`: placement by most linked active teams (deleted teams stand in none, as
  `ck_teams_department_place` guarantees), ties by department name (`ru-x-icu`, then id), `tie_by_name` rule logged when tied;
  fallback «Проекты без департамента» reused by normalized name (same rule as `EntityName.Normalize`), else created and coloured
  over the organization's departments (BR-TD-20 department scope; areas are hard-deleted, so all type-1 rows are active).
- Link cut matches the live unlink (`UnlinkTeamHandler` → `ClearDefaultTeamAsync`, `DropForTeamInProjectAsync`,
  `ReassignOwnedAsync`) on default team, backlog places, owner and sprint pointer, any item state; deliberately without
  `changed_at` and history (BR-TD-19 step 3 / BR-OL-13). Pinned by `ProjectsInDepartmentsMigrationTests` on a populated two-org cast.
- Idempotent (selects only remaining work; test re-runs the script and compares a fingerprint); all-or-nothing (EF migration
  transaction; failure test shows log table absent, data and links untouched); per-org row log + named notices + per-org summary
  (tested); Down relaxes the check (tested Up/Down/Up). Sits after 20261116104040 / feat-061, mod-024, feat-041, mod-021; the
  test resolves "previous" by name, so main's later 20261117110034 does not disturb it.
- `ck_areas_type_shape` now requires a parent for project nodes; model snapshot and `AreaConfiguration` agree.
- Authorization: CreateProject keeps the place check for a named department and the non-admin department-only 403; a request
  naming none is a pure 400 with nothing created and no place disclosed (QA D-1). Link routes judge BR-TD-3 after their 404/403s;
  `LinkPlaces` locks the project nodes `FOR SHARE` in one statement, id order; MoveArea reads links after the node's
  `FOR NO KEY UPDATE` lock — the two conflict, so link vs move ends in one order (both orders have endpoint tests). Raw SQL is
  interpolated (parameterised); no injection, no secrets, no personal data in the log beyond object names.
- Statement counts: link 4→5, AddTeam 10→11 are the one new lock statement; CreateProject 10→13 and scoped check 3→4 come from the
  tests now naming a department (place check, team nodes); small==large still asserted.
- Frontend: four codes in `errorCodes.ts` (drift test), `parentAreaId` → «Расположение», `team.other_department` intercepted as a
  banner with re-read, `project_has_teams` with the team list; «Отключить» gated by `canUnlink` and the server re-checks
  (`UnlinkTeamHandler` 403), so the `?? true` default is UI only.

### Findings

- **R-1 (minor) — stale "absent = top level" prose in code.** `src/backend/Mustask.Application/Features/Organizations/CreateProject/CreateProjectCommand.cs:12-14`
  ("absent or null is the top level"), `CreateProjectHandler.cs:51-53` ("area.create at the top level"),
  `src/frontend/src/features/organizations/api/organizationsApi.ts:168` ("absent: the top level"); dead top-level branch in
  `PlaceProjectDialog.tsx:81` (`node.parentId === null ||`). Fix: say "required; absent or null is 400
  `project.department_required`", drop the dead branch.
- **R-2 (minor) — frontend prose behind the QA fixes F-2/F-3.** `plan-frontend.md:15` ("a refusal for a linked project is the
  server's text as the alert") and `:18` ("a stale list … is the field's refusal") and the `PlaceProjectDialog.tsx` doc comment
  (same alert-only claim) describe the pre-fix behaviour; the code now lists the teams with «Отключить» (`ProjectTeamsRefusal`) and
  shows the stale-picker text as the dialog banner. Fix: reword the two rows and the comment.
- **R-3 (minor) — spec "No name can clash" is not true in one corner.** doc-016 `spec.md:82` (BR-TD-19 (2)) says no name can
  clash; `ProjectsInDepartmentsUpgrade.cs:166-168` raises and stops the migration when a top-level node named «Проекты без
  департамента» exists when the fallback is first needed — e.g. a top-level *project* of that name not yet moved. The stop is safe
  (all-or-nothing, the message names the remedy, tested). Fix: weaken the spec sentence (name the stop) or place a project of
  that name before creating the fallback.
- **R-4 (minor) — Down drops the record its own comment points to.** `20261117221218_ProjectsInDepartments.cs:13` says Down does
  not restore cut links "(the log names them …)", but Down executes `DROP TABLE IF EXISTS project_place_upgrade_log` (`:50`),
  so after Down nothing names them. Fix: keep the log on Down, or reword ("export the log first").

### Notes (not findings)

- The branch is behind `origin/main` by feat-062 and feat-071 (migration 20261117110034, `UnlinkTeamHandler` and search test
  kit). Nothing seen conflicts in kind (main's new test seeds name a department), but `origin/main` must be merged and CI green
  before the PR.
- `AggregateViewReadModel.cs:260-262` (the department group with no id) is now unreachable; untouched by this diff.
- QA F-4 (no row «Переместить» on a department's «Проекты») stays the gate-3 debt it was declared.

### Behavioural claims checked

- `api-contract.md` § 1 create: 400 field `parentAreaId`, no place judged → `CreateProjectHandler.cs:57-62,113-116`; test `Create_WithoutADepartment_ByADepartmentManager_Is400_NotA403`.
- § 1 create `teamIds` other department 409 → `CreateProjectHandler.cs:177-181`.
- § 1 move to top 409 `project_needs_department` → `Area.cs:222-225`; 403 first without area.create → `MoveAreaHandler.cs:46`.
- § 1 move while linked 409, same department no-op → `MoveAreaHandler.cs:114-120`; test `Move_ALinkedProject_ToItsOwnDepartment_IsA200…`.
- § 1 link vs move one order → `LinkRules.cs` `LinkPlaces` + `AreaRepository.LockProjectNode(s)ForShareAsync`; two race tests.
- § 1 three link routes 409 after 404/403 → `LinkTeamsToProjectHandler.cs:106-118`, `LinkProjectsToTeamHandler.cs:102-103`, `AddTeamHandler.cs:136-137`.
- § 2 codes and texts → `contracts/errors/organizations.json`, `docs/error-codes.md`, `OrganizationErrorCodes.cs`.
- § 3 upgrade log columns and shape check → `ProjectsInDepartmentsUpgrade.cs:21-38`, migration `ShapeAfter`.
- `plan-backend.md` idempotence, Down, colours → script selects / migration Down / colour function; Down claim also R-4.
- `plan-backend.md` "Import creates no project" → no project creation under `Features/Imports` (grep `Project.Create`: none).
- `status.md` statement-count deviation (1) → `TeamStatementCountTests.cs`, `ScopedAccessStatementCountTests.cs` comments match.
- `status.md` QA D-1/D-2 fixes → `CreateProjectHandler.cs:57`, `Project.cs:134-137`.
- `status.md` frontend F-1/F-2/F-3 fixes → `PlaceProjectDialog.tsx` (`refusal` hidden on empty teams), `LinkDialog.tsx` banner, `ProjectTeamsRefusal.tsx`.
- `plan-frontend.md` rows 15, 18 → R-2. `CreateProjectCommand`/handler/TS comments → R-1. spec BR-TD-19 "No name can clash" → R-3.
- Direction 2: changed behaviours "project at the top level", «Вынести на верхний уровень», optional `parentAreaId`; grepped
  `Вынести на верхний уровень`, `parentAreaId`/`ParentAreaId`, `top level` in `src/backend/Mustask.Api`, `docs/`, `.claude/`,
  `AGENTS.md` files. Hits: feat-043 `design.md`, its prototype and US-7 (merged feature history, superseded by doc-016 BR-TD-6,
  not swept further); code hits → R-1. Not swept: the mod-018/mod-019 contracts' top-level wording (closed features' history).
