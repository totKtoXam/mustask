# tech-065 review

## QA (light, S route; session: qa-engineer, 2026-11-10)

Commit under test: 8daf32bc (branch tech/065-mod019-review-debts). Not the author.

### Plan coverage

| Plan item | Test | Would it catch a regression |
|---|---|---|
| D-5 races (give vs membership removal, role change vs revoke, move vs switch) | `ScopedAccessRacesTests` (8 facts, commit-gated, real Postgres) | Yes for the order of locks and the end state in both orders. One assertion is loose: `RemovalThenGive` accepts any 4xx (see M-1) |
| D-5 refusal log line | existing `RefusalLogTests` | Yes (pre-existing; the claim holds, the file is in the tree) |
| D-5 500 ms seed | `ScopedAccessStatementCountTests` (pre-existing) | Judged below |
| G-1 destructuring guard | `scopeGuard.test.ts` (+3: rename/default/alias, unregistered surface, negative) | Yes |
| G-2 team notice dropped, codes notice | `ScopedAccessDebts.test.tsx` (team none, department/project keep, notice once) | Yes |
| G-3 6 rows + skeleton | `TableCard.test.tsx` (maxRows) + `ScopedAccessDebts` (computed max-height 256 px, overflow auto, skeleton not progressbar) | Yes |
| G-4 deciding skeleton | `ScopedAccessDebts` (held parent read, then the switch; reader sees the fact at once) | Yes |

### Tests run

- Frontend: `ScopedAccessDebts.test.tsx`, `scopeGuard.test.ts`, `src/shared/ui/table`, `kitGuard.test.ts` -> 12 files, 522 passed, 1 skipped (pre-existing), 0 failed.
- Backend: `ScopedAccessRacesTests` + `RefusalLogTests` -> 10/10 passed.
- Regression around the change: other users of `TableCard`/`DataTable` pass (table folder + their page tests inside the full run); `maxRows` is opt-in, default path unchanged (`maxHeightOffset` branch preserved verbatim). `InheritAccessControl` has one caller (`AreaPage`), `deciding` defaults false. `GrantsSection` change is `scope !== 'team'` only; department and project keep the notice (tested).
- `VERIFY_FULL=1 bash scripts/verify.sh` (no stamp, ran for real, machine loaded by neighbouring sessions): exit 1. Failures, none in files this diff touches:
  - backend `AggregateViewVolumeTests.EveryGrouping_OfAMillionItemsInTwoHundredProjects_IsReadWithin2s` (a 2 s volume read, after a 10-minute integration run) -> passes alone (1/1).
  - frontend, 6 tests in 5 `features/workitems` files (60 s timeouts / readonly attribute races under load): membership, readOnly, planning re-run alone -> 41/41 passed. `acceptanceCriteria` (60 s timeout) and `WorkItemsPage.assignee` (2000-people list) were not re-run alone; same timeout class, `features/workitems` is not in the diff.
  - Classification: load-induced timing flakes, not caused by tech-065. The orchestrator's own run of this tree was green (exit 0, see Log). Not filed as defects of this task; the volume test and the workitems 60 s tests are candidates for a flake tech task.

### Open points judged

- No `/ui-kit` showcase entry for `maxRows`: not a defect. `docs/conventions/ui-kit.md` says the table family has no showcase section yet (tech-016) and the coverage check (KIT-19/KIT-20) is not armed; `kitGuard.test.ts` is green. The guide block in ui-kit.md was updated. The new prop gets its demo when tech-016 builds the table section. Debt for tech-016, not for this task.
- 500 ms timing seed not run: accepted. The count of statements is pinned by `ScopedAccessStatementCountTests`; a wall-clock assertion would be exactly the kind of flake seen above. Nothing in the diff changes production backend code.

### Defects

Blocker: none.

Major: none.

Minor:
- M-1 `ScopedAccessRacesTests.RemovalThenGive_...` asserts only `4xx`, not the documented code, so a change of the refusal (e.g. 404 instead of 422/409) is not noticed. Pin the status and error code.
- M-2 `docs/conventions/ui-kit.md` line about `TableCard` grew into one over-long line (the `maxRows` clause was inserted mid-paragraph); re-wrap at the file's width. Cosmetic.
- M-3 `scopeGuard.ts` destructuring regex requires a trailing `;` and a single identifier on the right (`const { x } = a.b;` or a function-parameter pattern is not matched). Acceptable for the current code style (prettier adds `;`), but a known blind spot; note it in the guard's comment.

E2E: standing answer NO (not asked, not run).

## Round 1

Code review (stage 08, session: code-reviewer, 2026-11-10). Diff `origin/main...db3f33a1`, whole branch. Not the author.

### Checked

- Plan vs diff: D-5, G-1..G-4 each implemented as planned; nothing extra beyond the opt-in `maxRows` prop the G-3 cap needs. Copy matches design § 2, § 5, § 7 (notice dropped only for `scope === 'team'`, `FieldsSkeleton` per design § 4/§ 5 loading rows).
- Races really race: `TeamTestData.RaceAsync` (TeamTestData.cs:53-69) holds the first request at its commit gate and asserts via `PgLocks.WaitUntilBlockedAsync` that the second is blocked on a Postgres lock before release; all 8 facts use it.
- Frontend tests fail without the fix: reverted the 7 production files to `origin/main` and ran the three test files: 7 fail (4 in `ScopedAccessDebts`, 2 in `scopeGuard.test.ts`, 1 in `TableCard.test.tsx`); with the fix 35/35 pass. The two that pass either way pin existing behaviour by design (team codes notice, reader fact).
- G-4 `deciding` (AreaPage.tsx:549): guarded by `parentId !== null`, so a disabled query's permanent `isPending` cannot leave a skeleton; a failed parent read falls back to the read-only fact.
- Scope-guard regex (scopeGuard.ts:51): resolves aliases, renames and defaults; no false positive on organization codes (negative fixture). Blind spots as QA M-3, plus a rest element (`const { ...all } = workItemsPermissions`) names nothing; same fix (state them in the comment).
- QA minors M-1..M-3 agreed, left as debts.

### Findings

Blocker: none. Major: none.

Minor:
- R-1 `src/frontend/src/shared/ui/table/TableCard.tsx:60-61` still says "a sticky header needs `maxHeightOffset`"; `maxRows` now also makes the card scroll with a sticky header, and the cap does not count a `foot` (a `TablePager` inside the scrollport eats rows). Fix: "needs `maxHeightOffset` or `maxRows`" and note on `maxRows` that it assumes one-line rows and no foot.
- R-2 `src/frontend/src/features/organizations/components/SwitchOffDialog.tsx:25,35` render `UserLine truncate={false}`; in the dialog's width a long name wraps, the row outgrows `registryRowHeight`, and fewer than 6 rows show. The test (ScopedAccessDebts.test.tsx:436) checks the computed max-height only, which jsdom cannot contradict. Fix: `truncate` in these two cells (the tooltip keeps the full name), or accept "about 6".
- R-3 Commit history: `ScopedAccessRacesTests.cs` landed in the guard commit bddd8a3c, the G-2/G-3 page tests in the G-4 commit 109cda63, and 680e4b6d carries no test. The Log discloses it; nothing to redo, noted because PRs are merged with a merge commit and the branch history is kept.
- R-4 `ScopedAccessDebts.test.tsx:373` the title says it keeps the notice on the project and the department; that case asserts the team only (the `it.each` below covers the rest). Fix: drop the second half of the title.

### Behavioural claims

- status.md Log (backend) "8 race tests on commit gates and Postgres locks" -> ScopedAccessRacesTests.cs (4 facts + 2-row theory + 2 facts) via TeamTestData.cs:53-69. Holds.
- status.md Log "Refusal log line already pinned by `RefusalLogTests`" -> RefusalLogTests.cs:27,49 (route, code, scope, user). Holds.
- status.md Log "500 ms seed not run (pinned by `ScopedAccessStatementCountTests`)" -> the statement count is pinned there, the wall clock is not; the sentence says "not run". Holds.
- status.md Log "no defect, no production change" (backend) -> diff touches no `src/backend/src`. Holds.
- status.md Log "6 fail with the fixes reverted" -> reproduced: 6 in ScopedAccessDebts + scopeGuard (7 with TableCard.test). Holds.
- status.md Log "team codes notice pinned by a test" -> ScopedAccessDebts.test.tsx:389 against grantTexts.ts:177; passes without the fix, i.e. pins, does not fix. Holds.
- status.md Log "File landed in bddd8a3c" -> `git show --stat bddd8a3c`. Holds (R-3).
- status.md Log verify green / QA's full-run exit 1 -> not re-run; QA's flake classification stated as theirs.
- ui-kit.md:243 "`maxRows` caps it at that many rows for a table in a dialog" -> TableCard.tsx:92-99. Holds, with R-1/R-2 caveats (one-line rows, no foot).
- TableCard.tsx:46-47 "Ignored with `maxHeightOffset`" -> TableCard.tsx:90 branch. Holds.
- SwitchOffDialog.tsx:15 "shows this many rows and scrolls" -> R-2 (true for one-line rows only).
- scopeGuard.ts:34 "or by destructuring one" -> scopeGuard.ts:51-64; partial set (QA M-3, rest element). Minor, as M-3.
- QA "`maxRows` is opt-in, default path unchanged" -> TableCard.tsx:90-103 (`maxHeightOffset` branch verbatim, `minHeight` alone as before). Holds.
- Direction 2: behaviours changed = team grants notice (grepped «Здесь показан только выданный доступ»: mod-019 design.md:29,40 and prototype say project/department, consistent; qa.md:215 is the debt itself), TableCard scrolling/sticky (grepped `maxHeightOffset`, "sticky header needs": TableCard.tsx:60 -> R-1; tech-015 design.md:1412 is a merged design record, not swept), scope guard (grepped `scopeGuard`, `destructur` under docs/.claude: mod-019 qa.md/status.md record G-1 as the open debt, nothing else describes the guard's reach), inherit-switch loading and switch-off loading (design.md:111,140 already say `FieldsSkeleton`). mod-019 qa.md still lists G-1..G-4/D-5 as open: historical record of that task, not swept.

### Verdict

**approve** — blockers 0, majors 0, minors open 4 (R-1..R-4) plus QA M-1..M-3; all are debts for gate-3.

## Round 2 (delta)

Delta read of fix round 1 (95cdf06c, `git diff 19bbc820..95cdf06c`), session: code-reviewer, 2026-11-10. Runs: `ScopedAccessRacesTests` 8/8; frontend `features/organizations`, `scopeGuard.test.ts`, `shared/ui/table` 76 files, 1086/1086.

| Item | Status |
|---|---|
| M-1 | Fixed. ScopedAccessRacesTests.cs:86 pins 404 `MembershipNotFound`, the code api-contract.md:50,53 documents for a membership removed first. |
| M-2 | Fixed. ui-kit.md:240-251 re-wrapped at the file's width; content unchanged apart from the R-1 caveat. |
| M-3 | Fixed. scopeGuard.ts:51-53 states the limits; a rest element takes `Object.keys(object)` (scopeGuard.ts:62-65); fixture scopeGuard.test.ts:227 fails on the old split (member `...rest` resolved nothing). |
| R-1 | Fixed. TableCard.tsx:46-47 (one-line rows, no foot) and :62 (`maxHeightOffset` or `maxRows`); ui-kit.md carries the same caveat. |
| R-2 | Fixed for the row count: SwitchOffDialog.tsx:24,34 use `UserLine` with truncation, so rows stay one line and the 6-row cap holds. See R-5. |
| R-3 | Left, as decided (history only). |
| R-4 | Fixed. ScopedAccessDebts.test.tsx:36. |

New:
- R-5 (minor) `SwitchOffDialog.tsx:17-36` `GIVEN_COLUMNS` set no `width`, so `DataTable` passes no `columns` (DataTable.tsx:97) and the table stays `table-layout: auto`: a `nowrap` name widens its cell instead of being cut, and a very long name scrolls the card sideways (the horizontal scrollbar then takes part of the sixth row) rather than showing the ellipsis and tooltip. Fix: give the role column a width (fixed layout), or accept. Not a regression of the cap.

Claims in the fix: the comments added (scopeGuard.ts:51-53, TableCard.tsx:46-47, :62, ui-kit.md:243-244) anchor to the lines cited above; "A rest element is matched" -> scopeGuard.ts:62-65. Direction 2: behaviours changed = the give's race answer (grepped `membership.not_found`: api-contract.md:50,52,53, error-codes.md:70, consistent) and the guard's rest handling (grepped `destructur`: no prose outside this task describes it). None stale.

### Verdict

**approve** — blockers 0, majors 0; open minors: R-3 (left by decision), R-5 (new). Debts for gate-3.
