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
