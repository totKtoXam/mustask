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
- Status: open, carried to gate-3 as a debt (minor). Two skipped cases in `AggregateViewQaTests.ACraftedCursor_WithOutOfRangeValues_Is400OnCursor` name it; unskip when fixed.

### Verdict

blockers: 0 · majors: 0 · minors: 1 (D-1) · result: pass for the backend section. The 1M-item budget rests on the orchestrator's run. The panel is not covered here.
