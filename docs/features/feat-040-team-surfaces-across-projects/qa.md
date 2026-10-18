# QA — Team surfaces across projects (feat-040)

One QA pass over both layers (L, light route), clean context. The plan below was written from `spec.md`, `stories/`, `api-contract.md` and `design.md` before any implementation or developer plan was read (commit `56706191`); then the code and tests were read against it. Status: ✔ covered (where), ➕ test added by this pass, ☐ not checkable here.

Short names. Api.IntegrationTests: **TBA** `TeamBacklogAcrossProjects`, **TPF** `TeamProjectFilter`, **TOA** `TeamOrderAcrossProjects`, **TCM** `TeamCardAndMoves`, **CFT** `CreateFromTeamAcrossProjects`, **TVC** `TeamViewContainment`, **TBV** `TeamBacklogVolume`, **RTV** `RetiredTeamViewRoutes`, **QA** `TeamViewQa` (all `…EndpointTests` but TVC, TBV). Infrastructure.Tests: **TSC** `TeamViewStatementCount`, **TML** `TeamBacklogMemberLock`, **TDR** `TeamViewDeletionRace`, **MIG** `AddTeamLedWorkItemIndexMigration`, **DSP** `DeletedStateParity`. Frontend (`features/boards/pages` unless a path): **BP** `BacklogPage.projects`, **BDP** `BoardPage.projects`, **BCP** `BacklogPage.createProject`, **BPR** `BacklogPage.projectRows`, **BH** `BacklogPage.head`, **QAF** `BacklogPage.qa`, **VFR** `components/ViewFilterRow`, **UVF** `hooks/useViewFilter`, **TV** `workitems/lib/teamView`, **RB** `app/routes.boards`, **SW** `organizations/context/*` (`teamLevel`, `defaultTeam`, `resolveSelection`) with `app/shell/ContextSwitcher*`.

## Backend

### Coverage matrix

| # | Criterion | Check | Covered by | Status |
|---|---|---|---|---|
| B1 | US-1, BR-TS-2 | items of every active project; not VENUS's, not unowned | TBA | ✔ |
| B2 | BR-TS-1 | levels epics/features/stories; `tasks`, junk, capitals → `boards.level.not_found` | TBA, TVC, the three `…ContextNotFound` theories | ✔ |
| B3, B4 | BR-TS-2 | every state but Removed; beneath a row only the team's children | TBA | ✔ |
| B5 | BR-TS-3 | `project` on row, child, card of team and project views; rename shows | TBA (four views, current name) | ✔ |
| B6 | feat-039 | project views unchanged | TBA | ✔ |
| B7 | ADR-0028 r9 | deactivated project leaves, returns with its places | TBA, TOA | ✔ |
| B8 | BR-TS-2 | deleted, unknown, malformed, foreign team alike; no-project and all-deactivated teams read alike, `projects: []` | TBA | ✔ |
| B9 | BR-TS-1 | three old routes `http.not_found`, every method, member or not | RTV | ✔ |
| B10–B13 | BR-TS-8 | options by name, never narrowed; any value matches; invalid selects nothing, beside a valid adds nothing; tree over matches, checklist whole, reorder ignores it | TPF | ✔ |
| B11, B12+ | BR-TS-8 | blank, braces, no hyphens, spaces, NUL, 300 chars, 120 repeated values: 200, never an error | QA | ➕ |
| B14 | BR-TS-6 | placed first; time, key by ordinal, number as number; board the same | TOA, `StackRankTests`, `BacklogTreeTests` | ✔ |
| B15 | BR-TS-14 | equal positions sort alike on every read; the first reorder is dense | TOA | ✔ |
| B16, B18 | BR-TS-7 | reorder across projects; project orders untouched; deactivated project's places kept and back | TOA, TML | ✔ |
| B17 | BR-TS-7 | stale alike (VENUS, deactivated, Removed, foreign, unknown, malformed; moved and anchor) | TOA, TVC | ✔ |
| B19, B27 | BR-TS-13 | 401, org, 403 before team, team, level, 400, 409 | TOA, TVC | ✔ |
| B20 | BR-TS-7 | reorder vs unlink / team delete / team change / deactivation; read vs delete; no 5xx | TOA `Race_*`, TDR, TML, `ReorderVersusTeamChange` | ✔ |
| B21 | BR-TS-4, -5 | card, move, child, parent write in the item's project; foreign parent 404; foreign state 409; Reader 403 | TCM | ✔ |
| B22, B23 | BR-TS-9 | create in the chosen project, unplaced; 404 project, 409 not linked, 404 team; races | CFT | ✔ |
| B24 | §4.1, §4.2 | `isMember`; `project` on get, save | `GetOrganizationTeams…`, `GetWorkItem…` | ✔ |
| B24+ | §4.2 | `project` on create, delete, restore | QA | ➕ (parent write: D-B1) |
| B+ | §2.2, §2.3 | `projects` in Russian collation, case not deciding; board `order` = place in the whole level | QA | ➕ |
| B25, B26 | BR-TS-13 | non-member (instance admin too); foreign team, project, item ids name nothing; project views keep their boundary | TVC | ✔ |
| B28 | BR-TS-13 | inventory: three new rows, old gone, count 59 | TVC | ✔ |
| B29 | NFR | 50 projects, 5 000 stories: read < 2 s filtered or not; reorder < 1 s | TBV (dev's first run: 241/367 ms, move 379 ms) | ✔ |
| B30 | NFR | same statement count, 1 project/10 items vs 50/5 000/10 values | TSC (backlog, board, reorder) | ✔ |
| B31 | BR-TS-14 | index migration, clean DB, repeatedly, `Down` | MIG, `MigrationsTests` | ✔ |
| B32 | ADR-0030 | deleted items off every read; new reads in the matrix and parity | `DeletedItemSurfaceMatrix`, DSP | ✔ |

### Additional scenarios

| # | Scenario | Expectation | Status |
|---|---|---|---|
| BA1 | odd or repeated `projectId` values | 200, valid values only | ➕ QA |
| BA2 | equal createdAt and equal key | impossible: (key, number) is unique, so the order is total | ✔ by reading |
| BA3 | reorder `after` itself / no anchor | unchanged / 409 | ✔ `ReorderBacklog…` (mod-006) |
| BA4 | concurrent reorders of one backlog | serialized on the order row | ✔ TML lock order; no separate test |
| BA5 | deactivated organization | org 404 by the generic scoped check | ☐ not run, mod-016's |

### Defects

No blocker, no major. One doc item, a debt for gate-3.

#### D-B1: api-contract § 4.2 names the parent write among the answers that return a `WorkItem`

- Severity: minor (doc) · Layer: docs
- Steps: `PUT …/workitems/:id/parent` answers `{ parent }` (`SetWorkItemParentResponse`, a `WorkItemRef`): no `project`, no `projectId`. Create, save, delete and restore return the item with `project` (the added test reads create, delete and restore; the developer's reads get and save).
- Expected / Actual: § 4.2 says the field rides «every answer that returns a `WorkItem` (create, save, parent write, restore)»; the parent write is not one. Fix the sentence; no code change.
- Status: open

## Frontend

### Coverage matrix

| # | Criterion | Check | Covered by | Status |
|---|---|---|---|---|
| F1 | BR-TS-1 | team addresses render; old addresses and bad levels «Страница не найдена», no redirect, no read | RB, `paths.test` | ✔ |
| F2 | design §10 | team frames: not found, read failed, re-read failed keeps the table, no-project (a) with no filter row or command, own empty (b) | BP, BDP, `BoardPage.reread` | ✔ |
| F2+ | BR-TS-7 | team deleted while its page is open: the next read gives the frame | QAF | ➕ |
| F3 | BR-TS-11 | `h1`, «Команды» of the organization's teams, no «Весь проект», same entry and level, keyboard | BH | ✔ |
| F3+ | NFR | 200 teams: ArrowDown opens on the current, End and Home reach the ends, Enter opens that page | QAF | ➕ |
| F4 | BR-TS-11 | project head: no drop-down | `ProjectBacklogPage`, `ProjectBoardPage` | ✔ |
| F5 | BR-TS-8 | filter in the address as repeated `projectId`; kept by level, tab, grey command; dropped by the team menu | BP, BDP, UVF, `addressFilter.test` | ✔ |
| F5+ | BR-TS-4/8 | card opened over a filtered backlog keeps `projectId`, closes to it; by address too | QAF | ➕ |
| F6, F7 | BR-TS-8 | invalid value: «Фильтр задан», filtered-empty frame, clear; keyboard; 200 projects, End | BP, VFR, QAF | ✔/➕ |
| F8, F10 | BR-TS-3 | «Проект» column, card line, hidden prefix, token, band field read-only | BP, BDP, BPR, `WorkItemCard.project`, `theme.test` | ✔ |
| F9 | BR-TS-4 | card in `project.id`; by address via the key; unknown/malformed/lower-case key: frame, no request; deactivated | BP | ✔ |
| F9+ | BR-TS-4 | item read answering «Проект не найден.» for a listed project: the work item's frame | QAF | ➕ |
| F11–F13 | BR-TS-5 | move, tick, «+», «Добавить задачу», parent write in the item's project; other-project drop refused first, nothing sent; picker of the row's project | BDP, BPR, `drop.test` | ✔ |
| F14–F18 | BR-TS-9 | dialog: «Проект» first, preselect rules, «Выберите проект» and focus, «Команда» read-only then reset with announcement, server refusals under the right field, notice reasons, no command for no-project/Reader | BCP, TV, BDP | ✔ |
| F19–F21 | BR-TS-10 | team level of the organization's teams, own first; no project needed; failed/loading hides entries; chosen team survives project change and reload, fallback does not; mod-020 default; address sets org+team; gone-team notice | SW, `ContextSwitcher*`, RB | ✔ |
| F22 | BR-TS-12 | «Бэклог проекта», «Доска проекта» always | `TeamsPage.test` | ✔ |
| F23 | roles | Reader: no drag, grip, «+», menu, create; ticks disabled | `BacklogPage.gating`, `BoardPage.roles`, BCP | ✔ |
| F25 | E2E | standing NO | below | ☐ |
| F26 | design §11 | 640 px: filter wraps, table scrolls inside its card, card line, dialog | jsdom has no layout | ☐ not checkable; D-F3 |


### Defects

No blocker, no major. Four minors, debts for gate-3 (not sent back, no re-check). Layer: frontend.

#### D-F1: choosing the selected team in «Команды» drops an active project filter

- Severity: minor (design lines disagree)
- Steps: open `/organizations/O/teams/T/backlogs/stories?projectId=P`; «Сменить команду»; choose T, the selected entry.
- Expected / Actual: design § 3.2 says «Choosing the selected team moves nothing»; the entry links to T's page without a query (`TeamHead.tsx`, `pathOf`), so the address loses `?projectId=P` and the view widens (probe, not kept: address `""`). Design § 4.1 says the head menu drops the filter, which is meant for another team. feat-039's «Весь проект» entry kept the filter, so there «moves nothing» held. Suggested: the selected entry links to the current address.
- Status: open

#### D-F2: the in-memory scoped check still lists the three retired routes

- Severity: minor (test double, nothing user-facing)
- Steps: `features/organizations/testMembership.ts` lines 100–102 (`/projects/:p/teams/:t/backlogs|boards…`), pinned by `testMembership.test.ts:167`; the three team-addressed rows are absent.
- Expected / Actual: the table mirrors the route inventory (BR-TS-1, mod-016 rows 35–37); retired rows are kept and the new ones missing. No test fails: an unknown route falls through the guard.
- Status: open

#### D-F3: the card's third row may wrap and grow (D-1 says it does not)

- Severity: minor, unverified
- Steps: `BoardColumns.tsx`: the assignee/priority row keeps `flexWrap: 'wrap'` and the project (at most half the row) joins it; in a 280 px column an assignee name, «Приоритет: n» and a project name of ordinary length may not fit on one line (an estimate, not a measurement).
- Expected / Actual: design § 5.2 «the card does not grow»; jsdom has no layout, so it is not checkable here. Needs a look on a running stand, with the 640 px items of F26.
- Status: open

#### D-F4: a card opened by address flashes «Рабочий элемент не найден» when the cached project list lacks its key

- Severity: minor (reproduced by a probe, not kept)
- Steps: the shell's list of active projects is cached; another user creates project OPS; open a card of an OPS item (`?workitem=OPS-US-1`, or a row's title). `WorkItemCardDialog.tsx` computes `unknownProject` from `projects.isSuccess` and ignores `isFetching`.
- Expected / Actual: wait for the read that is already running; the frame shows while it lasts (probe: present with the list answering after 1.5 s), then the card. If that read fails, the frame stays and no retry is offered.
- Status: open

## Runs and verdict

| Date | What | Result |
|---|---|---|
| 2026-10-19 | `BASE=$(git merge-base HEAD origin/main) bash scripts/verify.sh`, once (merge-base `5de279f7`, no VERIFY_FULL) | exit 0, `RESULT: green`. Backend build; tests Shared 53, Application 782, Architecture 86, Contracts 248, Domain 750, Infrastructure 433, Api.IntegrationTests 2 931 passed, 17 skipped (the live Keycloak switches). Frontend lint, build; Vitest affected 285 files, 4 062 passed, 5 skipped; source-reading guards 13 files, 590 passed |
| 2026-10-19 | QA tests added by this pass, alone: `vitest run BacklogPage.qa.test.tsx` (7); `dotnet test` of `TeamViewQaEndpointTests` from a scratch build (5) | 12 passed. The verify run began before the backend file existed. First run of the backend file: 2 of 3 failed on my own assumptions (a parent write answers `{ parent }`, D-B1; MARS has three projects), fixed in the test |
| 2026-10-19 | Probes, deleted after: the selected team chosen with `?projectId=` (D-F1); a card of a project the cached list lacks (D-F4) | both reproduced |

Migrations: `AddTeamLedWorkItemIndex` applied on a clean database in `MigrationsTests` and `MIG` (verify run); no data touched. No process of mine is left running; the three containers running (`postgres-5432`, `postgres-kjdpknwf`, `mustask-e2e-keycloak-5010`) were there before.

**E2E: skipped by the human's standing decision** (NO since 2026-10-12): not asked, not run, nothing under `src/frontend/e2e/` edited. By inspection the change breaks:

- `support/membersUi.ts` `backlogAddress`/`boardAddress` (project-nested), used by spec 28 (lines 270, 277) and 29 (324–325, 430, 438, 559);
- specs 23 (75, 289–292, 329), 24 (62), 25 (59), 26 (142; the menu name «Команды проекта» at 627 and 878, a URL at 881) and 27-work-item-card (135), which build `…/projects/:p/teams/:t/…`;
- spec 19: after an organization choice its request list (927–931) expects only the projects and the first project's teams, while the switcher now also reads `GET /api/organizations/:o/teams`.

Specs 21 and 30 are not expected to break (21 creates one project with one team; 30 only uses `membersUi` helpers other than the two addresses).

Coverage was judged from test names and, where it mattered, bodies; not every test body was read. Nothing here is for the human to decide.

blockers: 0 · majors: 0 · open minors: 5 (D-B1, D-F1..D-F4) · result: **pass**
