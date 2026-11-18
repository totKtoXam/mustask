# mod-022-projects-in-departments

- type: module
- size: L
- branch: mod/022-projects-in-departments
- stage: 7-frontend-qa
- epic: core/org-access
- deps: mod-021
- gates: gate-3

## Gates

gate-3: pending

## Plan

Route L from stage 4. The requirements and the design are doc-016's,
approved at gate-1 and gate-2 on 2026-11-12:
`docs/features/doc-016-teams-under-departments/` (`spec.md` BR-TD-3, -4,
-6, -19 steps 2-3, -20 for projects; stories US-5 and US-2; `design.md`;
`docs/adr/0040-*.md`). Their gates are not repeated here.

Stages: backend (plan in `plan-backend.md`, code, tests, `api-contract.md`)
→ backend QA → frontend («Расположение» and the move dialog without
«Верхний уровень», the move refused while linked, team–project link offers
inside one department) → frontend QA → code review (one Opus pass with the
security pass) → gate-3 → PR on GitHub with auto-merge (merge commit),
branch kept.

The upgrade (BR-TD-19 steps 2-3) is irreversible over populated tables: it
is idempotent, logs per organization what it placed and cut (counts and
names), and is tested on a populated fixture as mod-021's was. The release
that carries it is tagged by the human.

Out of scope: department managers running teams (mod-023), scoped team
links at project creation and the shared-board card flag (feat-054), the
cascading selectors (feat-063).

Economy mode: one agent per stage, one Opus review. E2E: no (paused).
Verification: affected tests locally, the full verify.sh in CI.

## Log

- 2026-11-18 [orchestrator] task started on branch mod/022-projects-in-departments in worktree .claude/worktrees/kanban-test-case-2c0b58 (host DESKTOP-2S17CA7, git rev-parse --show-toplevel and git branch --show-current confirmed), cut from origin/main 731260bc.
- 2026-11-18 [orchestrator] queue line taken by scripts/queue.sh take mod-022, PR #220 with squash auto-merge.
- 2026-11-18 [orchestrator] route confirmed by the orchestrator session (coordination delegated by the human, 2026-11-18): L from stage 4 (the queue line said M; L thresholds fired: an irreversible migration cutting project–team links over populated tables, and a breaking API change — a project with no department is refused, BR-TD-6); spec and design inherited from doc-016 (gate-1, gate-2 approved 2026-11-12); gate-3 only. 3.1: no regulatory basis. 3.3: nothing beyond the line. The upgrade must be idempotent, log its cuts per organization, and be tested on a populated fixture.
- 2026-11-18 [backend] stage 4 done: commits 97ccbc31..716f2eaf on mod/022-projects-in-departments (plan and contract, link/move/create rules, the upgrade migration with its log table, tests). Tests: Application 911, Domain 1153, Contracts 258, Architecture 82, Infrastructure (migration tests 70 incl. 27 new/updated mod-022 and mod-021 ones; the rest 512 green apart from RichText timing classes not rerun), Api.IntegrationTests 1742+ green, one load-sensitive test (SaveWorkItemDescriptionQaTests burst) fails in a parallel run and passes alone. Deviations: (1) the lock of project nodes is one statement for all projects (statement-count rule), so the pinned statement counts moved by their stated amounts (link 4 to 5, AddTeam 10 to 11, CreateProject 10 to 13, scoped project check 3 to 4); (2) a request naming no department by a caller lacking the top-level code still gets 403 before the 400; (3) retired two tests that existed for a project node sharing the top level with a department (name race, OneNameSpacePerLevel) and reshaped the move/depth tests to bare projects; (4) the upgrade makes «Проекты без департамента» when no department can take a project (rule fallback). Contract unchanged since it was fixed.
- 2026-11-18 [backend QA] stage 5 done: 0 blockers, 0 majors, 2 minors (D-1 403 before 400 for a request naming no department, D-2 stale `Project.Create` comment) in qa.md; 14 QA tests added (13 pass, 1 skipped pin for D-1); RichText timing classes fail alone on a loaded machine and the branch touches none of them.
- 2026-11-18 [backend] QA minors fixed: D-1 (CreateProjectHandler judges no place for a request naming no department, so it is a 400 before any place authorization; the pin test is un-skipped, and the Retrofit/ScopedAccess tests now expect 400 or name a department) and D-2 (Project.Create refuses a null or empty parent, comment fixed, Domain test added; the Application fake keeps a top-level node for flat-store handler tests). Tests: Domain 1154, Application 911, Api integration ProjectsInDepartments/CreateProject/Retrofit/ScopedAccess 86, all green.
- 2026-11-18 [frontend] stage 6 done (code commit 316748f1; plan in `plan-frontend.md`): the four new error codes and the `parentAreaId` field key; «Новый проект» with a required «Расположение» (departments only, preset by entry point or the only department) and «Команды» of that department; the project move dialog without «Верхний уровень», `project_has_teams` kept in the dialog with the linked teams and «Отключить» (unlink, then back with the department kept); «Вынести на верхний уровень» removed; link pickers of both ends offer the other end's department only; colour marks on the projects list and the project page. Tests: Vitest over `features/organizations`, `shared/api`, `src/test` (1570 pass; the only failure, `httpClient.files.test.ts` apiFetchBlob, is in a file this branch does not touch), `tsc -b`, ESLint and Prettier on touched files green; the MSW mock follows the contract and the fixture's projects now stand in «Платформа». Deviations: (1) the selector's «＋ Создать проект» and the organization-«все» entries are not built (feat-052/feat-063 own them); (2) «Добавить проект» on a department keeps the server's `project_has_teams` text as an alert, without the team list; (3) no project row menu «Переместить» on a department's «Проекты» (the move stays on the project's «Общие»); (4) the stale link picker shows the server's text under the field, not an ErrorBanner. Copy written by the developer for gate-3: «Сначала выберите расположение.», «Нет другого департамента, в который вы можете переместить проект.», «Команды:» / «Отключить» in the move refusal, «Команда уже подключена ко всем проектам департамента.», «В департаменте «X» нет проектов. Создайте проект в этом департаменте.». E2E: not run (standing NO).
- 2026-11-18 [frontend QA] stage 7 done: 0 blockers, 0 majors, 4 minors (F-1 stale refusal after last unlink, F-2 stale picker text vs ErrorBanner, F-3 no team list in «Добавить проект» refusal, F-4 no row «Переместить») and 1 nit in qa.md; selector «Создать проект»/organization «все» judged out of scope (feat-052/063); canUnlink is not a permission hole; httpClient.files failure is jsdom Blob.text, not the branch's; 2 QA tests added (pass). E2E: not run (standing NO).
- 2026-11-18 [frontend] QA minors fixed: F-1 (the move dialog drops the «Проект связан с командами…» refusal once the last listed team is unlinked; test), F-2 (stale link picker shows the server text in an alert banner, list re-read), F-3 («Добавить проект» on `project_has_teams` names the teams with «Отключить» through the shared `ProjectTeamsRefusal`; test), F-5 (test title renamed, no «В департаменте «» …» without a name). F-4 stays a gate-3 debt. Vitest over `features/organizations` 958 pass, `tsc -b`, ESLint, Prettier green. No new copy. E2E: not run (standing NO).
- 2026-11-18 [code-reviewer] stage 8 round 1 (whole diff, security pass included) on 8b329a0f: approved — 0 blockers, 0 majors, 4 minors open as debts (R-1..R-4) in review.md; merge origin/main before the PR.
- 2026-11-18 [orchestrator] review minors R-1 (stale top-level comments, a dead check in PlaceProjectDialog), R-2 (plan-frontend after F-2/F-3) and R-4 (migration comment: Down drops the log) fixed in 64b08a59; tsc -b, Prettier, AreaPage tests green. R-3 (BR-TD-19 "no name can clash" against the migration stopping on a top-level «Проекты без департамента») and QA F-4 stay debts for gate-3.
- 2026-11-18 [orchestrator] merged origin/main (45 commits) fitted to projects in departments: snapshot consistent (has-pending-model-changes clean); RefusalLogTests and the inventory body (OrganizationAddressedRoutes) now carry parentAreaId; SearchTestKit, SearchDataSet, SightTwin and the other main fixtures already placed projects in departments. Domain 1185, Application 927, Architecture 82, Contracts 258 green; Infrastructure and Api (Search, Sight, Organizations, Authorization, Boards, Iterations, Imports) green except load-timing volume tests (RichText reducer, SearchDatabase VolumeReads p95 640 vs 600 ms, three Boards volume tests); the inventory and refusal-log tests re-run green (543); tsc -b clean, no frontend file from main.
