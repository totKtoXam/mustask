# QA — bug-004-board-shared-state-one-column

Short route under the human's standing approval of 2026-10-05: one QA pass,
and it is the E2E alignment — the developers' tests and `scripts/verify.sh`
stand in for the rest. This file covers that pass only; there is no Backend
section, because no backend QA session was run (D-3 pins the backend in the
developers' unit, integration and migration tests). The E2E question is
answered: D-3 (the human, 2026-10-12) says "E2E 23 and any spec that names the
old headers are aligned and run". The specs below are **aligned and listed, not
yet run** — the stand is the human's, and the run that counts is the
orchestrator's, against a stand brought up from nothing
(`docs/ai-workflow/stages/05-frontend.md` § 7).

## Frontend

The matrix was fixed from D-3 and the mod-006 / mod-001 contracts before the
components were read; the board and backlog components were read afterwards,
for locators only (a column's `h2`, its list's accessible name, the card's
state line, the dot's class).

### Coverage matrix

| Criterion (D-3, mod-006) | Check | Test type | Status |
|---|---|---|---|
| (3) «Истории» headers are state names: «Новая», «Активна», «Разрешена», «Закрыта»; no category label as a header | 23.4 step 1 (the `h2` list, each column's count); 23.1 (the columns it names) | e2e | aligned, not run |
| (3) «Эпики» and «Фичи» headed the same | 23.4 last step (an epic in «Разрешена», a feature in «Активна»; one item per board, because an empty board shows no columns) | e2e | aligned, not run |
| (1) + D-1: the story's and the bug's «Разрешена» stand in one column | 23.4 step 1 (two cards in one list); step 4 (the API board read: keys `Proposed-1`, `InProgress-1`, `Resolved-1`, `Completed-1`, the Resolved column's `targets` are the story's and the bug's own «Разрешена», no `null` target on any column) | e2e | aligned, not run |
| (3) a move writes the state of the card's own type | 23.4 step 3: a story and a bug moved from «Новая» by the card menu; the work item read has the `Resolved` category and the id of that type's «Разрешена» (and the two ids differ); the board keeps the four cards in one column after a reload | e2e | aligned, not run |
| US-5 the menu offers the other columns by their names | 23.1: «Активна», «Разрешена», «Закрыта» | e2e | aligned, not run |
| US-6 a new card is last in the first column | 23.1 (now «Новая») | e2e | aligned, not run |
| (1) «Разрешена» is Resolved on Эпик, Фича, Пользовательская история, Баг | 20.1 on the screen («Решённые» on all four) and on the contract (`state.category.code`) | e2e | aligned, not run |
| (2) a state name has one category on every type | 20.1 over the whole process read: no name stands in two codes | e2e | aligned, not run |
| (4) the state dot of «Разрешена» is the Resolved colour on a story, a bug and an epic | 23.4 step 2 (cards and backlog rows, `rgb(0, 121, 107)` = the theme's `Resolved`, `tokens.ts`) and the last step (the epic's card) | e2e | aligned, not run |

### Specs read and left as they are

| Spec | Why nothing changes |
|---|---|
| 20.1 lines 41-43 (`TASK_STATES`) | «Задача»'s own categories; D-3 does not touch them |
| 22.1 (132, 147, 160) | the bug's «Разрешена» as a name only; no category read |
| 24, 25 | read the «Канбан» region for an identifier or a card's priority; no header, no column |
| 26 (197-198, 435-436, 837-839, 844) | the dots are «Новая», «Активна» (story), «Закрыта» (bug); the comments name category labels, which stay |
| 19, 21 | no board, no state |

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|----------|-------------|--------|
| 1 | an empty board | shows «Канбан пуст» and no columns, so the headers of «Эпики» and «Фичи» are checked with one card each | covered by 23.4 |
| 2 | the pointer drop onto a board column | the same `move` as the card menu; the file's rule (header of spec 23) is to drive the keyboard paths and leave the pointer drags to the component tests | not in E2E (O-1) |
| 3 | an existing organization whose process still has the old seed | moved by the migration; proved by its Testcontainers test; E2E creates fresh organizations, so it cannot see it | not in E2E (O-2) |

### Runs

| Date | What was run | Result |
|------|--------------|--------|
| 2026-10-12 | `npx tsc -b` in `src/frontend` (covers `src/` and `e2e/`) | no errors |
| 2026-10-12 | ESLint and `prettier --check` on `20-process-definition.spec.ts` and `23-backlogs-boards.spec.ts` | clean |
| 2026-10-12 | `npx playwright test --list` | 70 tests in 25 files; 20.1-20.6 and 23.1-23.4 listed (23.4 is new) |
| 2026-10-12 | `bash scripts/verify.sh` (the whole branch against main: docs checks, backend build and tests, frontend lint, build and 1922 tests), started before this file and the `status.md` line were written | `RESULT: green`; the docs checks re-done by hand for those two files: no links in either, `status.md` 47 lines |
| not run | `npm run e2e` | by instruction: the stand is the human's; the orchestrator runs it against a stand started from nothing and quotes `e2e-results/stand.json` here |

### Observations (not defects)

- O-1: the board's pointer drop is not exercised by E2E (23.4 moves by the card
  menu, as 23.1 does). The component tests drive the drag
  (`boards/testDrag.ts`). If the human wants the drop in a real browser, it is a
  second step in 23.4 using the recipe of spec 26's `dragOver` (mouse down on
  the card's non-link text, move over the column, `mouse.up()`); it was left out
  because it cannot be tried in this pass.
- O-2: the migration's effect on an existing database is checked only by the
  developers' migration test. The human's stand needs `dotnet ef database
  update` once; an organization created before it keeps the story's «Разрешена»
  in progress until then, and 23.4 would not notice (it creates its own).
  After the update, run the check of `docs/runbooks/local-migrations.md`
  ("Migrations that report"): the migration's warning lands in the server log,
  not in the terminal, so the check is how a breach it left is found. The move
  is idempotent but runs once per database: an organization created by a
  checkout built before it, on a shared database, has the old shape, and the
  runbook's `UPDATE` is run by hand again (review R1-1, R1-3).
- O-3: 23.4 reads the dot colour as the literal `rgb(0, 121, 107)`, the theme's
  `Resolved` (`#00796B`). A change of that token is a change here too, as with
  spec 26's `D10` palette.

### Defects

None open from this pass. Nothing in the aligned specs or in the code they read
contradicts D-3.

### Verdict

blockers: 0 · result: aligned and listed; the E2E run is the orchestrator's

## Run 1 — E2E, the whole suite, a stand from nothing (2026-10-12)

This run is the one the sections above left to the orchestrator; it supersedes
their «aligned, not run» statuses and the «not run | `npm run e2e`» row.

- Branch tip: `6da5313`. Command, in `src/frontend`:
  `E2E_PANEL_URL=http://localhost:3100 E2E_API_URL=http://localhost:5100
  E2E_ALLOW_DOCKER=1 npx playwright test` (the whole suite, once).
- Stand: external PostgreSQL database `mustask_bug004` (created empty by
  `scripts/stand-reset.sh`, no Keycloak volume existed), `dotnet ef database
  update` (all nine migrations, `MoveResolvedStatesToResolvedCategory` among
  them — on an empty database it moves nothing, O-2 stands),
  `dotnet run --project src/backend/Mustask.AppHost` (panel 3100, API 5100,
  Keycloak 5010), `scripts/keycloak-seed.sh`. The `mustask-panel` client
  already pointed at 3100/5100 (read, not changed).
- Stand record (`e2e-results/stand.json`, `checkedAt` 2026-10-12T00:19:14Z,
  the run's own — the report's `startTime` is 00:19:13Z): **`"stand": "warm"`**,
  `waitedMs` 10. The stand was brought up from nothing, and `/health` still
  answered 503 while the users were seeded, but the seed and a read of the
  panel client sat between the stand answering and the suite starting, and
  the API's realm check had latched by then (§ 7: the 30-second window was
  not observed). Not repeated here — the brief was one run. Carry it as the
  warm-stand deviation of `05-frontend.md` § 7 unless the orchestrator repeats
  it; nothing bug-004 changed is on the session flow.
- Result: **70 passed, 0 failed, 0 skipped, 0 flaky**, 25 files, 13.3 min.
  Per file (passed of total): 01-11 and 13-14 1 of 1 each, 15 3 of 3,
  16-18 1 of 1 each, 19 13 of 13, **20 6 of 6**, 21 8 of 8, 22 3 of 3,
  **23 4 of 4**, 24 5 of 5, 25 6 of 6, 26 6 of 6.
  20.1 (the four types' «Разрешена» in «Решённые», no state name in two
  categories) and 23.1-23.4 (the four headers, one «Разрешена» column for the
  story and the bug, the move writing the card's own type's state, the Resolved
  dot, the board read's keys and targets, «Эпики» and «Фичи») are among them.
- Spec defects fixed: none. Product defects: none.
- Screenshots, 1536 × 790, taken on this stand by a temporary spec (not
  committed, deleted; its organization deactivated), outside the repository, in
  `C:/Users/khamb/AppData/Local/Temp/claude/C--Users-khamb-Workspace-devex-internal-products-mustask--claude-worktrees-consolidate-project-requirements-1fc945/ada62ddc-ca85-4ff4-b9da-b40708aaf41b/scratchpad/shots-004/`:
  - `1-board-stories.png` — «Истории»: «Новая 1», «Активна 1», «Разрешена 2»,
    «Закрыта 1»; the story `US-1` and the bug `BUG-2` stand together in the one
    «Разрешена», each card's state line is «Разрешена» with the Resolved dot.
  - `2-board-epics.png` — «Эпики»: the same four headers, one epic in each.
  - `3-backlog-stories.png` — the team backlog: three stories and two bugs; the
    story's and the bug's «Разрешена» carry the same Resolved dot.
  - `4-process-settings.png` (and `4-process-settings-full.png`, the whole page,
    because the viewport ends in the fourth row of the story and the bug) — «Процесс»:
    «Разрешена» is «Решённые» on Эпик, Фича, Пользовательская история and Баг;
    «Задача» has no such state.
- Left after the run: the database `mustask_bug004`; nothing else (the stand's
  processes stopped, the Keycloak container gone, ports 3100, 5100 and 5010
  free).

### Verdict (Run 1)

blockers: 0 · majors: 0 · result: D-3's E2E question is answered — 20.1 and
23.1-23.4 ran green with the rest of the suite; one deviation to carry, the
warm stand record above.
