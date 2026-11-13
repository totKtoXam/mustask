# mod-021-teams-in-departments

- type: module
- size: L
- branch: mod/021-teams-in-departments
- stage: backend
- epic: core/org-access
- deps: mod-019,doc-016
- gates: gate-3

## Gates

gate-3: pending

## Plan

Route L from stage 4. The requirements and the design are doc-016's,
approved at gate-1 and gate-2 on 2026-11-12:
`docs/features/doc-016-teams-under-departments/` (`spec.md` BR-TD-1, -4,
-5, -19 step 1, -20; stories US-1 and US-2; `design.md`;
`docs/adr/0040-*.md`). Their gates are not repeated here.

Stages: backend (plan in `plan-backend.md`, code, tests, `api-contract.md`)
→ backend QA → frontend (the team form's and page's «Департамент», a
department's «Команды», the feat-049 diagram's team placement, the selectors'
fit without department–team links, the colour marks) → frontend QA → code
review (one Opus pass with the security pass) → gate-3, where ADR-0040 is
accepted → PR on GitHub, merged by the human with a merge commit, branch kept.

Out of scope: projects in departments (mod-022), department managers running
teams (mod-023), every sight and selector line (feat-061..065,
feat-041/052/054).

Economy mode: one agent per stage, one Opus review. E2E: no (paused).
Verification: affected tests locally, the full verify.sh in CI.

## Log

- 2026-11-13 [orchestrator] task started on branch mod/021-teams-in-departments in worktree .claude/worktrees/dreamy-einstein-34d195 (host DESKTOP-2S17CA7, git rev-parse --show-toplevel and git branch --show-current confirmed), cut from origin/main 873935f7.
- 2026-11-13 [orchestrator] queue line taken by scripts/queue.sh take mod-021, PR #158 with squash auto-merge.
- 2026-11-13 [orchestrator] route confirmed by the human in chat: L from stage 4 (the queue line said M; L thresholds fired: ADR-0040 accepted at this gate, department–team links retired as a breaking model change, a data migration that drops links); spec and design inherited from doc-016 (gate-1, gate-2 approved 2026-11-12); gate-3 only. Question 3.2: the tables hold important data (stand/prod), so the migration is treated as irreversible and logs every placement. Usage at start: weekly 4%.
- 2026-11-13 [backend] stage 4 done: plan-backend.md and api-contract.md fixed, code and tests committed (53b4eae5..6b77efbe). Domain 981, Application 877 pass; Infrastructure and Api integration affected sets pass (migration tested on a populated cast, with the all-or-nothing failure and Down/Up). Deviations: organizations.area.link stays in the catalog with no route; project nodes carry the project colour through the read model while areas.color is NULL; deleted teams get blue and no department; the check ck_teams_department_place reads status <> 'Deleted' so a bad status still hits ck_teams_status. AggregateViewVolumeTests (2 s budget) fails under local load, not touched by this task. ADR-0040 stays proposed.
- 2026-11-13 [backend QA] backend QA done: qa.md (Backend). No blocker or major; 2 minors (ICU collation needed by the migration, load-bound timing tests not caused by the branch). Added 3 migration tests (ties by name, first_link, SQL-vs-domain colour parity incl. all-used, only-deleted-teams) and 13 API tests (`TeamPlacesQaTests`). Full Infrastructure and Api suites run once; only load-bound timing tests failed. E2E not asked (standing NO).
- 2026-11-13 [frontend] stage 6 done: plan-frontend.md; commits 3b3c17ec..HEAD (`mod(org): ... (mod-021)`). Required «Департамент» on «Новая команда»; the team page names the department (colour mark) and moves the team (409 `has_links` stays in the dialog with the projects and «Отключить»); a department lists its teams and creates one; deletion refuses with `has_teams`; «Команды» list gets a «Департамент» column; selector fit and the diagram read the team's one department; all UI of the retired link routes removed; kit `ColorLabel` + theme `objectColours`. Tests: the affected Vitest sets (app, features, shared, test) green except suites that need the uninstalled `@tiptap/*` (RichTextEditor, work item card/form), which fail the same way without this branch; `tsc -b` clean apart from the same tiptap errors; ESLint and Prettier clean on touched files. No api-contract change. E2E not run (standing NO). Deviations: one hex per key (the panel has a light theme only); no colour dot on the team page title, in the Projects list, the header selectors or the structure tree (design places them in the structure-list lines); «Список»/«Дерево» of «Структура» not built; the move uses the `team.create` code (no manager scoping, mod-023).
- 2026-11-13 [frontend QA] frontend QA done: qa.md (Frontend). `npm ci` and `tsc -b` clean (the earlier tiptap errors were the missing install); full Vitest 6486 pass, 12 load-bound timeouts outside the branch's files. 1 major (F-1: a team linked only to a deactivated project gets `has_links` with nothing to unlink), 2 minors (F-2 no colour mark in the team page title, F-3 no department mark in the «Команды» list column). Added `TeamPlaces.qa.test.tsx` (6 tests, F-1 as `it.fails`). E2E not asked (standing NO). Light theme only confirmed.
- 2026-11-13 [orchestrator] backend QA D-2 (ICU collation) closed as checked: COLLATE "ru-x-icu" has served production reads since mod-002 (2026-10-05, OrganizationsCollation.cs), and prod's PostgreSQL 16 is the distro package, built with ICU (docs/deploy/vm.md). Frontend QA F-1 (major) goes back to the frontend developer with the dialog option (list deactivated linked projects, restore-first hint; backend keeps refusing for any link per BR-TD-4); F-2 and F-3 (minor, in scope) fixed in the same pass.
