# QA — feat-032-board-card-checklists

Single pass of an M, backend and frontend together (2026-10-12). **E2E was
skipped by the human's decision** (status.md Log: "QA runs without the full E2E
run — unit + integration only"); no stand, Playwright or browser was started.
The test plan below was written from spec, stories and api-contract before any
implementation was read.

## Test plan (criterion -> check)

Backend, integration (`GET …/boards/{level}`):
B1 direct children only · B2 Removed child out · B3 only task/bug/testcase, a
story under a feature out · B4 child of another Область in · B5 no state filter
(«Разрешена» bug, «Закрыта» test case in) · B6 `[]`, never absent; card has no
`children`/`parent` · B7 order by number across kinds · B8 child bug is also a
card · B9 assignee null / known / unknown, >500 distinct · B10 children never
leak between cards · B11 `testcase` token round-trips (process read, checklist
type) · B12 board otherwise unchanged · B13 5 000 x 5 within 2 s · B14 the
existing PATCH/POST writes show in the next read.

Frontend, component (MSW): F1 counters, order, text, D-5 names · F2 `0/0` only
on a story, no row elsewhere · F3 Resolved not done, test case counted whatever
state, no flask without the type · F4 toggle, `aria-expanded`, Enter/Space,
independent lists, no request on open · F5 row content, avatar names, testcase
row without checkbox · F6 chevron focus, assignee on the card · F7 open state
across reads / reset on a new visit · F8 drag guard · F9 tick/untick, refusal,
no answer, no Completed state, one write at a time, keyboard · F10 add task
(button first, story only, payload, Enter/Escape/empty/256/double Enter,
refusal at the field, focus, notice) · F11 200 children.

## Backend

### Coverage matrix

| Criterion | Test | Status |
|---|---|---|
| US-1 counters data, order by number across kinds, Removed out (B1, B2, B7) | `BoardChecklistsEndpointTests.Get_Card_CarriesItsTasksAndBugs_ByNumber_NotTheRemoved`, `Get_GrandchildIsNotInTheStorysChecklist` | pass |
| US-1 «Разрешена» not done (B5) | `Get_Children_CarryTheirStateAndCategory_AResolvedBugIsNotCompleted` | pass |
| US-1 test cases whatever state / no flask without the type (B5, B11) | `Get_TestCases_AreChildren_WhateverTheirState`, `Get_WithoutATestCaseType_NoChildIsATestCase` | pass |
| US-1 empty story answers `[]` (B6) | `Get_CardWithoutChildren_AnswersAnEmptyList` | pass |
| US-1 row data, assignee, unknown assignee (B9) | `Get_Child_CarriesWhatARowShows`, `…AnAssigneeTheDirectoryDoesNotKnow…`, `Get_Card_KeepsItsAssignee`, `Get_MoreThan500DistinctAssigneesAmongChildren_IsAnsweredWhole` | pass |
| US-1 only task/bug/testcase listed (B3) | `Get_FeatureCard_HoldsItsBugsAndNotItsStories` | pass |
| US-1 other team's child counts, backlog tree does not (B4) | `Get_ChildOfAnotherTeam_IsInTheChecklist_NotInTheBacklogsTree` | pass |
| US-1 child bug is its own card (B8) | `Get_ChildBug_IsAlsoACardOfItsOwn` | pass |
| US-1 board unchanged, read changes nothing (B12) | `Get_ColumnsAndCards_AreUnchanged_ACardGainsOnlyChildren`, `Get_Board_ChangesNothingStored` | pass |
| US-1 5 000 cards x 5 children within 2 s (B13) | `BoardChecklistsVolumeTests` | pass, 1 032 ms in this run |
| US-2 tick / untick / bug / later write wins (B14) | `Tick_ATask_…`, `Untick_ATask_…`, `Tick_ABugThatIsAlsoACard_…`, `Untick_AfterAnotherUsersWrite_TheLaterDecides` | pass |
| US-3 create beneath a card, first task, refusal (B14) | `Create_ATaskBeneathACard_…`, `Create_ATaskBeneathAStoryWithNone_…`, `Create_BeneathARemovedCard_IsRefused_…` | pass |

### Added by QA (`BoardChecklistsQaTests`, 3 tests, all pass)

| # | Scenario | Expectation | Status |
|---|---|---|---|
| 1 | Two stories, each with children (B10) | each card holds only its own, in its own number order | pass |
| 2 | A test case in the Removed category (B2 for the third kind) | not in the list; the live one is | pass |
| 3 | A process with a `testcase` type (B11) | process read answers `appearance: "testcase"` on the «Задачи» level; the stories board answers 200, lists it as a child, shows no testcase card or level type | pass |

Note for the reader of the stories: the «Задачи» level hosts no board
(`BoardLevel` is epics/features/stories), so a test case is never a card.

### Runs

| What | Result |
|---|---|
| `dotnet test Mustask.Api.IntegrationTests --filter BoardChecklists` | 26 run: 25 pass, 1 fail — my own new test (wrong JSON property `types`, a test bug, fixed); 3 added tests pass after the fix |
| Migrations | none added (`appearance` is `varchar(16)` with a converter; the unique index is per process and appearance, so one `testcase` type per process); fixture inserts of `'testcase'` are accepted by the schema Testcontainers builds from the migrations |
| `bash scripts/verify.sh` | green, exit 0 (see the end of this file) |

### Notes on the developers' flags (backend)

- Volume test: 1 032 ms against the 2 s budget in this single run on a loaded
  workstation (the developer saw ~0.9 s). Headroom is about 2x; it flaked once
  under load before. Not re-run (run economy). Statement count is fixed (one
  rows statement plus one lookup per 500 assignees), so what can grow is time,
  not queries.
- `WorkItemField.Of(Testcase)` throws `ArgumentOutOfRangeException` (read in
  `WorkItemField.cs`; `WorkItem.TakeDefaults` and the field guards call it). Not
  reachable here: no testcase type is seeded and the board only reads. It will
  be a 500 on create or save of a test case until feat-031 extends it. D-4 below.
- Fixture numbering differs from the stories (the world numbers items as it
  creates them: `MSK-TC-9/10`, not `-10/-12`); order of creation is the same.
  No effect on what is verified.

## Frontend

### Coverage matrix

| Criterion | Test | Status |
|---|---|---|
| US-1 counters, `1/3`, `1/2`, D-5 names, MSK-TASK-7 out | `BoardPage.checklist.test.tsx` "A card shows its counters…" (3), `lib/checklist.test.ts` | pass |
| US-1 Resolved not done; test cases flask `2`; no flask; testcase row without checkbox | same file, "Test cases are a count only…" (3) | pass |
| US-1 `0/0` on a story only; no row on others | "A story with no children…" (2) | pass |
| US-1 open/close, keyboard Enter/Space, independent, kind order, no request | "A counter opens its own list…" (5) | pass |
| US-1 rows: order by number, checkbox/icon/avatar/link, avatar names, unknown assignee, assignee on card, title link | "The rows" (6) | pass |
| US-1 chevron closes and focuses the counter | "The chevron closes a list" | pass |
| US-1 open state across reads, reset on a new visit | "What was opened stays opened…" (2) | pass (the "new visit" test covers one of the three routes: see D-5) |
| US-1 drag guard (counter, checkbox, link, avatar, chevron refused; card surface drags) | "Controls inside the card do not start a card drag" (2) | pass |
| US-1 feature board, other team's child, child bug is a card, 200 children, board unchanged | last four describes (5) | pass (200: rows drawn, no request, no time: D-3) |
| US-1 design § 7.2 re-read keeps focus; lost row moves focus to its counter | "a re-read while a list is open" (2) | pass |
| US-2 tick, untick, bug, resolved-bug round trip, own-card moves at read, Space, refusal, no answer, no Completed state, one write at a time, later write wins, testcase has no box | `BoardPage.tick.test.tsx` (13) | pass |
| US-3 first line, keyboard, story only, create, first task on `0/0`, Escape, empty, double Enter, replay `requestId`, refusal at field (also folded), 256 chars, one field open, typing does not drag | `BoardPage.addTask.test.tsx` (16) | pass |
| Theme: `testcase` flask and colour | `workTracking.test.tsx`, `Marks.test.tsx` | pass |

### Added by QA (`BoardPage.checklist.qa.test.tsx`, 2 tests, pass)

| # | Scenario | Expectation | Status |
|---|---|---|---|
| 1 | Tab order in an open card (design § 8) | counters, «Добавить задачу», each checkbox then title link, chevron; avatars no stops | pass |
| 2 | Two lists open, a bug ticked | only the bug counter moves; the task list, counter and boxes are as they were; both stay expanded | pass |

### Reconciliation with design.md and api-contract.md

Screens, states, texts and permission behaviour match design.md § 3–§ 9 on
reading code and tests. The board type (`BoardCardItem.checklist`,
`ChecklistChild`) matches api-contract § 2 field for field, state category codes
are the wire strings (`Completed`, `Proposed`), the writes are the contract's
PATCH `{stateId}` and feat-027's POST with `parentId`. The MSW fixtures are the
frontend's own copy of the contract: backend and frontend were not run against
each other (no E2E), so the pairing is by reading.

### Notes on the developers' flags (frontend)

- 100 ms / 200 children: not verifiable here. A throwaway jsdom probe (not
  committed) took about 2.3 s to open 200 rows in a dev build of MUI under jsdom;
  that is an upper bound by an order of magnitude, not a measurement. Only a
  browser can answer. D-3.
- Checkbox `disabled` until the process loads: confirmed in code, `tickable` is
  empty while `useProcessDefinition` has no data. A process read that never
  succeeds leaves every checkbox silently disabled and out of the Tab order,
  with no `0/0` counter and no «Добавить задачу». D-1.
- No Firefox fallback: confirmed, there is none in `BoardColumns`/
  `ChecklistAddField`; unverifiable without Firefox. D-2.

### Runs

| What | Result |
|---|---|
| `npx vitest run` of `BoardPage.checklist.qa.test.tsx` | 2 pass |
| Throwaway probes (200-row open time; row redraws on a drag) | run once, deleted, not committed |
| `bash scripts/verify.sh` | green, exit 0 (see the end of this file) |

## Defects

#### D-1: checkboxes are silently disabled when the process is not read (frontend)

- Severity: minor
- Steps: serve the board, make `GET /api/organizations/{id}/process` fail or hang; open the task list of a story with tasks.
- Expected: BR-C-7 disables a checkbox only for a type with no Completed state.
- Actual: `disabled={!tickable}` with `tickable` built from `process.data?.workItemTypes ?? []`; every checkbox is disabled (and leaves the Tab order), no notice. Normally a short window; permanent if the read fails. Read from code, not run.
- Status: open, carried to gate-3 as a debt.

#### D-2: no Firefox fallback for selecting text in the add field (frontend)

- Severity: minor
- Steps: Firefox, open the add field inside a card, select its text with the pointer.
- Expected: BR-C-12 / design § 9 fallback `draggable={false}` on the card while the field is open, if a build refuses selection.
- Actual: no fallback; behaviour in Firefox unknown (a draggable ancestor is a known cause of this). Chromium-only suite, no E2E this time, not tried.
- Status: open, debt.

#### D-3: the 100 ms / 200-children opening budget is not measured anywhere (frontend)

- Severity: minor
- Steps: open a story card with 200 children in a browser, time the list opening.
- Expected: within 100 ms (spec NFR). Actual: no measurement exists; jsdom cannot give one.
- Status: open, debt (unverified requirement, not a known miss).

#### D-4: `WorkItemField.Of(Testcase)` throws (backend, for feat-031)

- Severity: minor (latent, not reachable in this task)
- Steps: a process with a `testcase` type; create or save an item of it.
- Expected: a test case has a field set. Actual: `ArgumentOutOfRangeException` -> 500. Already in the developer's log.
- Status: open, belongs to feat-031; make sure it is on that task.

#### D-5: rows redraw on every drag event; "memoized" does not hold (frontend)

- Severity: minor
- Steps: open the task list of a card, start a drag of any card from its surface and drag over columns, counting renders of the row's avatar (probe, not committed): 3 rows rendered 3 times on open, then 3 more at `dragstart` and 12 more over 5 enter/over pairs.
- Expected: `ChecklistRow`/`CardChecklists` are `memo` "so a drag over a column redraws no row" (code comment, design § 7.3).
- Actual: the `actions` object changes on each render of `BoardColumns` (probable cause: `useChecklists` puts the whole `useMutation` results `setState` and `createItem` in the dependency lists of `tick` and `submitAdd`, and those objects are new on each render), so `env` changes and every row of every open card redraws. With a 200-row list open, each drag event redraws 200 rows (about 2 s in jsdom, unknown in a browser). Cause not confirmed by a fix.
- Status: open, debt; worth fixing with D-3's measurement.

Also noted, not a defect: the "new visit" reset (US-1) is tested for one route
(unmount and mount of the page). Reload, another level and another team rest on
the same remount: the open set is `useState` in `useChecklists`, and design § 4.3
and `BoardPage.tsx` keys `BoardColumns` by level and the team board by team. That keying is
read from code, not tested.

## Verdict

blockers: 0 · majors: 0 · minors: 5 (D-1..D-5, carried to gate-3 as debts,
not sent back) · result: **pass**. E2E skipped by the human's decision, so the
two sides were never run against each other and nothing was measured in a
browser (D-2, D-3).

## verify.sh

`bash scripts/verify.sh` with the QA tests in: exit 0. Docs checks ok; backend
build ok; backend tests 1 601 passed and 15 skipped in `Mustask.Api.IntegrationTests`
(the other six assemblies pass: 53, 550, 189, 83, 510, 196), frontend lint and
build ok, `vitest` 157 files passed.
