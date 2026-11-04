# QA — feat-048 aggregate level views

## Backend

Pass 1, 2026-11-03, commit 34d0c5cf. The plan was written from spec.md, US-11 and api-contract.md before the implementation was read.

### Coverage matrix

| Criterion | Check | Type | Status |
|-----------|-------|------|--------|
| BR-AV-1/2 groupings offered | `AggregateGroupingTests`, `AggregateParametersTests`, endpoint 400 on `grouping` | unit + integration | covered (dev) |
| BR-AV-4 order, first group of no department / no team | `Groups_ByTeam/ByDepartment/ByProject` | integration | covered (dev) |
| BR-AV-4 each item once, header count = pages | `EveryGrouping_PartitionsTheItems…` x4 groupings | integration | added, green |
| BR-AV-5 count vs total under filters | `Groups_WithAFilter…`; keyword by identifier on headers/rows/list | integration | covered + added |
| BR-AV-6 rank order, by-project cursor equals the project's own backlog (placed + unplaced) | `ByProjectCursor_AgreesWithTheProjectsOwnBacklog…` | integration | added, green |
| BR-AV-7 cells, per-column cursor | `Cells_…`, `ACellPages_AndItsCursorIsNotAnotherColumns` | integration | covered + added |
| BR-AV-8 flat list, 50 a page, deep page, grouped work-item rows by change time | `List_*`, `WorkItemRows_*` | integration | covered (dev) |
| BR-AV-9 cursor: no repeat, no loss, equal change times, forged/foreign cursor | `Rows_ByCursor…`, `EqualChangeTimes_PageStably…`, `ACraftedCursor…` | integration | covered + added; D-1 |
| BR-AV-9 limiter 6+6 / 40+20 / 3 s / 429 + Retry-After / abort | `AggregateReadLimiterTests` (fake clock) | integration | covered (dev) |
| BR-AV-10 team or department change between reads | `AnItemThatChangesTeam_AndAProjectThatChangesDepartment…` | integration | added, green |
| BR-AV-11 budgets at 1M items | `AggregateViewVolumeTests` | volume | passed per orchestrator, not rerun |
| BR-NV-13 deactivated project leaves every read; OQ-4 404 | `ADeactivatedProject_Leaves…`, `Rows_OfADeactivatedProjectGroup…` | integration | covered + added |
| BR-NV-17 team + department | `ATeamAndADepartment_Narrow…` | integration | added, green |
| BR-NV-15 caller signature, auth and permission per route | `AggregateViewReadSignatureTests`, `OrganizationAddressedRoutes` matrix | unit + integration | covered (dev) |
| Migration up/down | `AddTeamLedWorkItemIndexMigrationTests` (3) | integration | green |

### Additional scenarios

Equal change times; grouping that names nothing (`state=Nowhere` is 200, empty); bad, missing and unknown `group` (400 / 400 / 404); other level (epics) empty; page far past the end (200, empty); cursor crossed between columns (400); crafted cursor with `N=0` (400).

### Runs

| What | Result |
|------|--------|
| `dotnet test Mustask.Api.IntegrationTests --filter AggregateViewQaTests`, output `%TEMP%\qa048-run2.txt` | 17 passed, 2 skipped (D-1), 0 failed |
| First run `%TEMP%\qa048-run1.txt` | 1 failed: crafted `T=-1` gives 500 (D-1); the skip was added afterwards |
| `Mustask.Infrastructure.Tests --filter AddTeamLedWorkItemIndexMigrationTests`, `%TEMP%\qa048-migr.txt` | 3 passed |

The migrations apply on the clean database of every integration fixture. E2E: standing answer NO, not run.

### Defects

#### D-1: a crafted cursor position out of range is a 500

- Severity: minor (a signed-in member can only hurt their own request; no data leaks)
- Steps: take a `nextCursor` from `GET …/backlogs/stories/rows?grouping=team&group=none&limit=1`, base64url-decode it, set `"T":-1` (the change or creation time in ticks), re-encode and send it back. The cursor is unsigned and its fingerprint is a plain hash of the request, so it can be rebuilt.
- Expected: 400 `boards.validation_failed`, field `cursor`, reason `boards.invalid_cursor` (ADR-0036).
- Actual: 500. `GroupCursor.TryDecode` checks `N`, `U` and `P` but not that `T` is a valid tick count; `new DateTimeOffset(ticks, TimeSpan.Zero)` throws in `AggregateViewReadModel` (`ChangedCandidatesAsync`, `RankCandidatesAsync`). `T` above 3155378975999999999 is the same path, by code reading (the test case is skipped, not run).
- Fix: in `TryDecode`, require `T` in 0..`DateTime.MaxValue.Ticks`.
- Status: fixed. `TryDecode` now rejects `T` outside 0..`DateTime.MaxValue.Ticks`; `N`, `U` and `P` were already checked, and `L` (the place) is only compared in SQL, never converted. The two skipped cases are unskipped and pass.

### Verdict

blockers: 0 · majors: 0 · minors: 1 (D-1) · result: pass for the backend section. The 1M-item budget rests on the orchestrator's run. The panel is not covered here.

## Frontend

Pass 1, 2026-11-04, commit b8ec2192. The plan was written from spec.md, US-11 and design.md before `src/frontend` was read. E2E: skipped by the human's standing decision (NO). Flat-list column sorting is not built by decision (BR-WS-12), not a defect.

**Run status: the QA tests below were written but NOT run.** The coordinator paused every Vitest, tsc and ESLint run mid-pass (the orchestrator's full-suite run holds the lane). They are left uncommitted in the working tree; no result is claimed for them.

### Coverage matrix

| Criterion / screen | Check | Type | Status |
|---|---|---|---|
| BR-AV-2 options per level, order | `AggregateViews.test` (organization, team), `grouping.test` | unit + page | covered (dev) |
| BR-AV-3 default, memory, silent fallback | `AggregateViews.test`, `grouping.test` | page | covered (dev) |
| BR-AV-4 groups first: «Без команды» | `AggregateViews.test` | page | covered (dev) |
| BR-AV-4 groups first: «Без департамента»; «3 из 40» | `AggregateViews.qa.test` | page | added, not run |
| BR-AV-6 no move/handle outside by project; hint once | `moves.test` (team), `qa.test` (department) | page | covered + added |
| BR-AV-6 move menu, part-loaded rules, drag | `moves.test`, `aggregateMove.test` | page + unit | covered (dev) |
| Viewer: no «Ещё действия», no handle | `qa.test` | page | added, not run |
| «Без группировки» backlog: no headers, count line, no «Свернуть все» | `qa.test` | page | added, not run |
| BR-NV-20 «Свернуть все» | `qa.test` | page | added, not run |
| OQ-4 A deactivated project hides group + notice | `qa.test` | page | added, not run |
| BR-AV-7 board: lanes, «Без команды» first, cells per lane; no lanes with none | `qa.test`; `AggregateViews.test` (cells); `moves.test` (drag, targets) | page | covered + added |
| BR-AV-8 grouped list: no pager, no sort; flat list keeps both | `AggregateViews.test` (shape), `qa.test` (no buttons in headers) | page | covered + added |
| OQ-1 429 retry, refusal, cursor 400, headers error, empty | `AggregateViews.test`, `useGroupRead.test` | page + unit | covered (dev) |
| Design § 2 switch announced through StatusRegion | none possible (not built) | - | **D-1** |
| Design § 3 narrowed-empty «Ничего не найдено» (backlog, board) | none possible (not built) | - | **D-2** |
| Design § 3 board «Без группировки» count line | none possible (not built) | - | **D-3** |

### Defects

#### D-1: choosing a grouping is not announced
- Severity: minor (accessibility; design § 2: "announced through `StatusRegion` («Группировка: По команде»)")
- Steps: open «Бэклог» at the organization, choose «По команде». Code: `groupingAnnouncement` in `lib/grouping.ts` has no caller; `AggregatePage`/`AggregateWorkItemsPage` pass `changeGrouping` straight to the switch.
- Expected: a status announcement «Группировка: По команде». Actual: none.

#### D-2: a narrowed empty backlog or board says «В бэклоге пока нет элементов»
- Severity: minor (design § 3 "Whole view" row: narrowed or filtered gives «Ничего не найдено»)
- Steps: department level with a team chosen whose groups read returns no groups. `AggregateBacklogView` and `AggregateBoardView` always use `emptyBacklog` / `emptyBoard`. The list page handles it right.

#### D-3: the board without grouping has no "Элементов: N" line
- Severity: minor (design § 3 "Без группировки": the count shown once for backlog and board). The backlog shows it, the board does not.

### Verdict (provisional)

blockers: 0 · majors: 0 · minors: 3 (D-1..D-3, debt for gate-3). The new tests must be run (single file, then tsc and ESLint) before this pass counts as closed.

### Frontend pass 2, 2026-11-04, commit 4dfd6397

Runs (single files, outputs in `%TEMP%`): `vitest run src/features/boards/pages/AggregateViews` (4 files) `qa048-fe1.txt`: 34 passed, 2 failed, both in my own new file (wrong selectors: the no-team lane's `group` param is `none`, and a board without lanes repeats the card in each column). Fixed in the test; `AggregateViews.qa.test.tsx` rerun `qa048-fe2.txt`: 9 passed. ESLint on the two files: clean (`qa048-lint.txt`). `tsc -b` (`qa048-tsc.txt`): 1 error, D-4 below. The four earlier new-test claims above ("added, not run") are now run and green.

Re-check of D-1..D-3 against 4dfd6397: D-1 fixed (`useAnnouncement`, test in `AggregateViews.states.test.tsx`, green), D-2 fixed (same file, green), D-3 fixed (same file, green).

#### D-4: `tsc -b` fails, `npm run build` is broken
- Severity: major (the production build and CI type check fail; Vitest does not see it)
- Steps: `npx tsc -b` in `src/frontend`.
- Actual: `src/features/boards/hooks/useAnnouncement.ts(2,37): error TS2459: Module '../lib/grouping' declares 'Grouping' locally, but it is not exported.`
- Fix: import `Grouping` from `../api/aggregateApi` (where `lib/grouping.ts` takes it from). Introduced by 4dfd6397. Returned to the frontend-developer.

### Verdict (final for pass 2)

blockers: 0 · majors: 0 open (D-4 fixed by the orchestrator, 2026-11-04: `Grouping` imported from `../api/aggregateApi`; re-check `tsc -b` clean, `AggregateViews.states.test.tsx` 3/3) · minors: 0 open (D-1..D-3 fixed and verified). Verdict: pass. E2E: skipped by the human's decision.
