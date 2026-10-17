# QA — Project backlog and board (feat-039)

One QA pass over both layers (M shortcut, `05-frontend.md` § 7), clean context, 2026-10-17. The test plan was written from
`spec.md`, `stories/US-1..3`, `api-contract.md` and `design.md` before any implementation file was read; the matrices keep
its shape. **E2E: the standing answer is NO (paused 2026-10-12, night delegation) — not asked, not run, no spec drafted.**
The orchestrator records it in `status.md` under Decisions (QA does not edit that file). Owed clauses (no-team items,
scoped grants, visibility, Removed category writes) have no code to test yet and are listed once at the end.

Names: GPB = `GetProjectBacklogEndpointTests`, GPD = `GetProjectBoardEndpointTests`, RPB = `ReorderProjectBacklogEndpointTests`,
VOL = `ProjectBacklogVolumeTests`, CNT = `ProjectBacklogStatementCountTests`, MIG = `AddProjectBacklogOrderMigrationTests`,
QA = `ProjectBoardsQaTests` (added by QA).

## Backend

### Coverage matrix

| Criterion | Check | Type | Status |
|---|---|---|---|
| US-1 every item of the project from every team, «Порядок» 1..n, nothing of another project, none twice | GPB `Get_HoldsEveryItemOfTheProject…`, `Get_AreasWith…` | integration | ✔ |
| US-1 level separation, every state but Removed (also on the board) | GPB `Get_HoldsTheLevelsItemsInEveryStateButRemoved`, GPD `Get_EachLevelHoldsItsOwnItems`, QA `Board_CardOrder…` | integration | ✔ |
| US-1 children beneath the parent whatever the team; team backlog keeps its own | GPB `Get_ChildrenStand…`, `Get_ASameLevelChild…`; tasks by number: `Get_TasksBeneathAStory…` | integration | ✔ |
| US-1 add child writes no place, takes parent's area | QA `CreateChild_BeneathARow…` | integration | ✔ |
| US-1 head: project `{id,name}`, no team; empty list | GPB `Get_AnswersTheProjectAndTheLevel…`, `Get_NoItemAtTheLevel…` | integration | ✔ |
| US-1 BR-PB-8 team views unchanged; a read writes nothing | GPB `Get_WritesNothing` + every team suite (below) | integration | ✔ |
| US-1 who reads: viewer = member, outsider 404, anonymous 401, instance admin without membership 404 | GPB/GPD `Get_ByRole…`; the instance admin via `ScopedAccessTests` (loops all inventory routes, now 47) | integration | ✔ |
| US-1 address told from outside in (9 + 8 cases), upper-case and all-zero ids | GPB/GPD `Get_ContextNotFound…`, QA `Get_ProjectIdInUpperCase…` | integration | ✔ |
| US-1 5 000 / four levels: read ≤ 2 s, statements equal for 50 and 2 000 per level | VOL, CNT (pinned 9 / 8 / 11 / 12) | integration | ✔ |
| US-2 top / bottom / after / after-self; no item field or «Изменён» changes | RPB `Reorder_Top…`, `_KeyboardEntries…`, `_AfterAnItem…`, `_ChangesNoWorkItem` | integration | ✔ |
| US-2 project and team orders independent (both directions, at the tables); change of team keeps the project place | RPB `_TheProjectsOrderAndATeamsOrder…`, `_WritesOnlyItsOwnPlaces`, `_AnItemThatChangesTeam…` | integration | ✔ |
| US-2 unplaced after placed, by creation time then number; created item last | GPB `Get_Unplaced…`, `Get_NeverReordered…`; RPB `_ThenACreatedItem…` | integration | ✔ |
| US-2 stale 409: other project / other level / Removed / unknown / malformed / missing; task; item of another organization | RPB `_ItemsThatAreNotOnThisBacklog…`, `_ItemOrAnchorNamingNothing` (6); QA `_ATask…`, `_AnItemOfAnotherOrganization…` | integration | ✔ |
| US-2 two at once: later wins, nobody told; project vs team reorder fail neither; 8 at once | RPB gate tests (3); QA `Reorder_EightAtOnce…` (all 204, places exactly 1..8) | integration | ✔ |
| US-2 deactivated: 404, order kept on restore; deactivation racing the reorder | RPB `_DeactivatedProject…`, `_ProjectDeactivatedWhileWaiting…` | integration | ✔ |
| US-2 who may reorder; answer order 401, 404, 403, 404, 404, 400; 400 field reason | RPB `_ByRole…`, `_OrderOfAnswers…`, `_InvalidPosition` (3) | integration | ✔ |
| US-2 5 000 stories: reorder ≤ 1 s, same statement count | VOL, CNT | integration | ✔ |
| US-3 columns from the process, cards once, union of the team boards = the project's | GPD `Get_HoldsTheBacklogsItems…` | integration | ✔ |
| US-3 column in the project's order, not a team's; card `order` = position in the whole order (gaps) | GPD `Get_AColumn…`, `Get_NeverReordered…`; QA `Board_CardOrder…` | integration | ✔ |
| US-3 card move writes the state only, places unchanged; foreign-type state 409 | GPD `CardMove_WritesOnlyTheState…`; QA `CardMove_AStateOfAnotherType…` | integration | ✔ |
| US-3 checklist of children whatever the team; viewer reads, cannot write; empty board | GPD `Get_ACardsChecklist…`, `Get_ByRole…`, `Get_NoItemAtTheLevel…` | integration | ✔ |
| api-contract § 3.1 deep JSON (chain at the 64 bound answered whole) | QA `Get_AChainOfTheBoundsLength…` (epics 63, stories 61 deep) | integration | ✔ |
| § 6 migration: clean DB, additive, rollback drops exactly two tables, re-applies, no model drift, FK/check/cascade | MIG (3) | integration | ✔ |
| § 5 inventory 47 routes, OpenAPI, no new error code; layering | `OrganizationAccessInventoryTests`, Contracts.Tests, Architecture.Tests | integration | ✔ |

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|---|---|---|
| 1 | Same reorder twice | same dense places, no drift (QA) | ✔ |
| 2 | `top` / `bottom` with a stray, malformed `afterItemId` | 204, ignored (QA) | ✔ |
| 3 | Reorder naming a task | 409, nothing placed (QA) | ✔ |
| 4 | Foreign organization's item in body or anchor | 409, no place anywhere (QA) | ✔ |
| 5 | 8 parallel reorders | none starves, deadlocks or is told stale (QA) | ✔ |
| 6 | Upper-case project id / all-zero id | 200 / project 404 (QA) | ✔ |
| 7 | Moving a nested same-level row via the API | not probed: the panel offers moves on top-level rows only; same behaviour as the team route | — |

### Runs

| What | Result |
|---|---|
| `VERIFY_FULL=1 bash scripts/verify.sh` | **red, not by this task**: prose and lint green, frontend build green, backend build fails (D-1); frontend suite: 5 timeouts (below) |
| `dotnet test` per test project (the solution cannot build, D-1) | Domain 631, Shared 53, Application 750, Architecture 83, Contracts 242, Infrastructure 288 (incl. MIG 3, CNT 3), Api.IntegrationTests 2 234 passed + 17 skipped (opt-in live-Keycloak tests), 0 failed; 6 m 12 s. The two timing tests (VOL) are inside the budget |
| QA tests, `ProjectBoardsQaTests` | 11 of 11 green on first run; they found no defect |

### Defects

#### D-1: `scripts/verify.sh` backend build is red on an advisory in `Mustask.AppHost` (environment, not feat-039)

- Severity: **major** (blocks the green run that `finish-task.sh` and pre-push demand), layer: backend / repository
- Steps: `dotnet build Mustask.sln` (or `VERIFY_FULL=1 bash scripts/verify.sh`) in this worktree.
- Expected / Actual: builds / `error NU1902: Mustask.AppHost.csproj: package MessagePack 2.5.302 has a known vulnerability GHSA-qhrr-8q5h-9q3h` (warnings are errors). The task changes no `.csproj`, `.props`, `Mustask.sln` or AppHost file (`git diff a653917f..HEAD` on them is empty), so main fails the same way; the test projects build and run when built one by one.
- Fix, outside this task: bump the package through Aspire or add a `NuGetAuditSuppress` for that advisory — a queue line; `Directory.*.props` go through the orchestrator.
- Status: open (orchestrator)

### Verdict

blockers: 0 · feature defects: 0 · result: **pass** for the backend of feat-039; the repository's verify stays red on D-1 until it is fixed elsewhere.

## Frontend

### Coverage matrix

Names: PB = `ProjectBacklogPage.test.tsx`, PR = `.reorder.test.tsx`, PL = `.large.test.tsx`, PBd = `ProjectBoardPage.test.tsx`, SH = `app/routes.projectBoards.test.tsx`, PV = `navigation/projectViews.test.ts`, API = `api/projectApi.test.ts`, SC = `lib/scope.test.ts`, HD = amended `BacklogPage.head.test.tsx`, QAf = `ProjectBoards.qa.test.tsx` (added by QA).

| Criterion / screen (design) | Check | Type | Status |
|---|---|---|---|
| Routes `/projects/:p/backlogs|boards/:level`; `tasks`, `sprints`, `Epics`, missing level: «Страница не найдена», no request | PB, PBd, SH, `paths.test.ts` | component | ✔ |
| Head § 3: `h1` = project name (fallbacks), one tab, «Уровень: …» links, «Показать канбан/бэклог» to project pages, no «Новый рабочий элемент», tab title | PB, PBd | component | ✔ |
| Drop-down § 3.4: «Весь проект» last after a divider, selected + `aria-current`, teams keep order/indices, same entry and level, focus on arrival, ArrowDown/End/Escape, hidden with no teams | PB (8 cases), PBd, HD (list + one-team case) | component | ✔ |
| Backlog § 4: columns, tree, first expanded state, «+» adds a child with no team, title opens the card | PB | component | ✔ |
| Reorder: «Ещё действия» four moves, disabled first/last, announcement, focus, pointer from the title cell, independent of the team order, stale notice, no-answer notice, 403 notice | PR (10) | component | ✔ |
| Row actions OQ-5: drop onto a row = parent write (no place, no team, project re-read); «Изменить родителя…» | QAf (2) | component | ✔ (added) |
| Board § 5: counts/names of lists, cards of every team once, project order, no «Новый элемент», move by menu and drag, announcement, focus, refusal returns the card, story-only column | PBd | component | ✔ |
| Card move refused by the scoped check (403) on the project board | QAf | component | ✔ (added) |
| Checklists: counter over all teams, tick, «Добавить задачу» with no team, no place | PBd | component | ✔ |
| Reader: no grip/«+»/«Ещё действия»/drag; checkboxes disabled; one hint for every role | PB, PBd | component | ✔ |
| States § 7: 3 organization / 7 project not found (frame, one `h1`, no head), 5 page, 6 skeleton with head, 8 error + «Повторить» and level-not-found as banner, 9 re-read failure keeps rows/cards, 10 both empty frames, 11 large (every top-level row drawn, none expanded, «Показано строк: 7.») | PB, PBd, PL | component | ✔ |
| States 1, 2, 4 (codes loading, failed, no read code) | shared `OrganizationPage` wrapper, covered by `OrganizationGate.test.tsx`; not repeated on the project route (all roles hold the read code) | — | ◐ |
| Shell BR-PB-9 (a): «Бэклоги» / «Канбан» current at three levels, no trail, landing kept; (b) team level never «Весь проект»; (c) project / organization switch keeps page and level, no project → «Главная», team switch moves nothing, team pages keep BR-18; no team → entries hidden | SH, PV (marker, resolver, trailing slash, other project's page, code asked for the new organization) | component + unit | ✔ |
| Query keys: project and team never under each other's prefix; read-again per scope; tab focus re-reads | SC, PB, PBd | unit + component | ✔ |
| Contract: project routes, `project{id,name}`, reorder body, 404 order | API (against the in-memory server, which the backend tests pin) | unit | ✔ |
| BR-PB-8: only `BacklogPage.head.test.tsx` (entry list gains «Весь проект») and the inventory count changed among existing tests; `E2E 26-…` finds menu entries by name | diff + `grep` | review | ✔ |
| Interface texts vs design § 10 (two new strings, all others copied) | read against the code and asserted in PB/PBd | review | ✔ |
| 640 px scroll inside table/board, real pointer drag in a browser, 2 000-row feel | jsdom has no layout; E2E is NO | manual | ✘ not verified |

### Additional scenarios

| # | Scenario | Expectation | Status |
|---|---|---|---|
| 1 | Project drop onto a row | `PUT …/parent` only, no `/reorder`, no `/teams/` request, project backlog re-read, row nests (QAf) | ✔ |
| 2 | Parent dialog on a project row | one parent write, no reorder (QAf) | ✔ |
| 3 | 403 on a project card move | «Недостаточно прав» at once, card back in «Новая» (QAf) | ✔ |
| 4 | Stale cache after a team write | project query is stale at 0 ms and read again when it mounts (app defaults) — read from `App.tsx`, no test of its own | ✔ |

### Runs

| What | Result |
|---|---|
| Full `vitest` inside `VERIFY_FULL=1 verify.sh`, 286 files | 4 340 passed, 6 skipped, **5 failed** — all `Test timed out in 60000ms` with wall times of ~1 630 000 ms (the process was frozen ~27 min mid-run, a suspended host), in `ProjectBacklogPage.reorder.test.tsx` (1) and two untouched `MembersPage.*` files (4) |
| Hypothesis tested, once: those 3 files alone | 36 of 36 green in 31 s — environmental, not a defect |
| `ProjectBoards.qa.test.tsx` | 3 of 3 green; `tsc -b`, ESLint and Prettier clean on the new file |
| Lint, build (`tsc -b && vite build`) | green |
| E2E | not run: standing answer NO |

### Defects

#### D-2: the team board's behaviour on a failed re-read changed (BR-PB-8)

- Severity: **minor**, layer: frontend
- Steps: open a team board, make the next read fail (500; tab refocus), look at the page.
- Expected / Actual: BR-PB-8 and the design (§ 2, "everything not in this table is the team page's") keep the team page as it was — the banner replaced the columns. Now `ScopedBoard` keeps the columns and shows the banner above them (new test `BoardPage.reread.test.tsx` pins it). It is the backlog's rule (feat-027 D-6) and design § 7 state 9 asks for it on the project board, so it is harmless, but it touches a team view and the spec says additive.
- Status: open, carried to gate-3 as a debt ("Deliberately left open"): human to confirm keeping it, or gate it to the project scope.

#### D-3: artifacts of the earlier stages are not in git, and `status.md` is behind

- Severity: **minor**, layer: docs (orchestrator-owned files)
- Steps: `git status` — `spec.md`, `stories/US-1..3`, `review.md` of this feature are untracked; `status.md` says `stage: 01-business-analysis`, gates pending, and has no developers' plan (`api-contract.md` points at its `## Plan` section, which has no plan).
- Expected / Actual: the PR carries the spec it is judged against, and `status.md` shows the stage and the plan / Actual: neither.
- Status: open (orchestrator: commit the three paths by name, update `status.md`: stage, plan, E2E = no under Decisions, the OQ/D "decided … for the human" lines).

### Verdict

blockers: 0 · majors: 0 in the feature · result: **pass** (defects D-2, D-3 minor; D-1 environmental, see Backend).

## Owed, not testable yet

No-owning-team items on the views (`mod-017-work-item-owning-team`); scoped grants and visibility, BR-MX-21..24 (`mod-019-scoped-access`, `feat-041-project-visibility`); Removed-category writes (`mod-005-work-item-delete-restore` — the tests make a Removed item through the existing delete); the 2 000-card bound (`feat-040-team-surfaces-across-projects`); the "no team in the project" switcher case beyond an empty list (`feat-028-org-level-teams`).
