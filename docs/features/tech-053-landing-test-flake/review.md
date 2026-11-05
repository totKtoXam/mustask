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
