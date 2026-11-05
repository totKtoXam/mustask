# tech-053 QA (light form, S route)

Verdict: no blocking defects. The fix is test-only and the assertions are unchanged.

## Checks

1. Root cause. A 5 s `asyncUtilTimeout` (`src/test/setup.ts`) against a CPU-starved 50-row MUI render is credible
   and matches the old failure text. Fake timers do not fit: there is no timer or debounce, only render cost.
   A per-wait budget is acceptable. It is condition-based, so a quiet machine returns as early as before,
   and it stays inside the file's 120 s test timeout. Raising the global `asyncUtilTimeout` would loosen every
   test in the repo, so the local budget is the better choice. A smaller dataset would barely help: page 1 and
   the final list still render 50 rows, only page 2 would shrink.
2. Assertions. Same texts, same count. `table()` is inlined as the same `findByRole('table', ...)` query;
   `getByRole` became `findByRole` (more tolerant of render lag, not weaker). No skip, no looser matcher.
3. Other tests in the file use `listFilterDataset()` (a handful of rows) and carry no such risk.

## Runs

- Sequential: 3 of 3 green (file, 6 tests; 22-25 s of tests).
- Under 30 CPU burners: 2 of 2 green (tests 84 s and 147 s; the burners were killed afterwards).
- `src/features/workitems` folder: 61 files, 909 tests, all green.

## Defects

- D-1 (low, debt): the budget is a hand-made constant in one file. Under extreme load (the second run took 147 s
  of tests) the 120 s per-test ceiling, not the 30 s waits, is the next limit. No action now; if another
  heavy-render test appears, move `LIST_RENDER` to a shared test helper.
- D-2 (info): the status.md Plan names the constant `LIST_RENDER_MS`; the code has `LIST_RENDER`. Cosmetic.

## Round 1

Verdict: **approve**. No blocker or major; one minor, left as a debt. Diff read: `git diff origin/main...HEAD`
(3 files; the branch is behind main by tech-057, with no overlap with this diff).

### Answers to the round's questions

- Fix choice. The page size is fixed (`WORK_ITEMS_PAGE_SIZE = 50`, `api/workItemsApi.ts:241`; `rowsPerPageOptions={[]}`),
  so page 1 before and after the purge is 50 rows whatever the dataset. Trimming cannot remove that cost, and a
  narrower query does not shorten a render. A local, condition-based budget is the cheapest correct fix, and keeping
  `asyncUtilTimeout` global at 5 s is right.
- Assertions. Unchanged in text and count. `table()` is inlined as the same query with the budget. The two
  `getByRole` → `findByRole` changes still throw when the element is absent; they only wait. Nothing weakened.
- The 147 s is the file's summed `tests` time, not one test. Reviewer run under 30 CPU burners (22 cores):
  file `tests` 105 s, the 50-row test 33.0 s (31 %), the others 8-21 s. Quiet run: 4.5 s of 19.7 s. The heaviest
  test sits at about a quarter of the 120 s ceiling at that load, so the flake has not moved to the test timeout.
- Comments. `WorkItemFormPage.landing.test.tsx:28-30` match the code (see claims).

### Findings

- R1-1 (minor, debt) `WorkItemFormPage.landing.test.tsx:107,113,119` and QA check 3 above. "Other tests ... carry
  no such risk" is overstated. Under the same 30 burners the first test (a 2-row list) took 20.7 s, against 33.0 s
  for the 50-row one, so its default 5 s waits after the purge are next in line. No failure has been seen there
  (every observed red was on «1–50 из 119»), so no action now. Fix if it ever flakes: give the waits after a
  purge or navigation in this file the same budget, or move `LIST_RENDER` to a shared helper (joins QA D-1).

### Behavioural claims

- `:28` "a 50-row list is a heavy MUI render (about a second alone)" → measured: the 50-row test 4.5 s vs 2.4 s for
  the comparable 1-row Back test; rationale-grade, holds roughly.
- `:29` "the default findBy budget is 5 s" → `src/test/setup.ts` `configure({ asyncUtilTimeout: 5000 })`.
- `:29-30` "still inside the test timeout" → `:26` `testTimeout: 120_000` > 30 000 per wait; worst measured test 33 s.
- `:30` "return as soon as the condition holds" → Testing Library `findBy*` is `waitFor` (polling plus
  MutationObserver); the quiet run is unchanged (4.5 s).
- status.md Plan "assertions, texts and their count are unchanged" → test lines 132-143, verified against the diff.
- Direction 2: the behaviour changed is the wait budget of one test. Grepped `LIST_RENDER`, `asyncUtilTimeout`,
  `landing.test` in `docs/`, `.claude/`, `AGENTS.md`, `src/frontend/AGENTS.md`. Hits: `docs/backlog.md:36` (the queue
  line, intent) and feat-003 `plan-frontend.md:26`, `qa.md:5`, `review-code.md:57` (they name the file only).
  None describes the wait budget; nothing stale.
