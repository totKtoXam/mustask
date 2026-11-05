# tech-053-landing-test-flake

- type: tech
- size: S
- branch: tech/053-landing-test-flake
- stage: complete
- epic: platform/ci
- deps: -
- gates: gate-3

## Gates

gate-3: approved · PR #94 https://github.com/totKtoXam/mustask/pull/94

## Plan

- Cause: not a timer or debounce. The test "shows the first page of the list" builds 120 rows; every step (list render, next page, card, purge, list render again) is a heavy MUI table render (6.4 s alone). `asyncUtilTimeout` is 5 s per `findBy`, so under CPU load the waits on a 50-row list (the page-2 text and the final `1–50 из 119`) run out of the 5 s budget while the render is still going. Reproduced on the old test under 30 CPU burners: red with exactly «Unable to find ... 1–50 из 119».
- Fix (test only): the waits that follow a full 50-row list render in that test get an explicit budget (`LIST_RENDER`, 30 s, inside the file's 120 s test timeout). Assertions, texts and their count are unchanged; the waits stay condition-based, so a quiet machine returns as soon as the text appears.
- Proof: 20 sequential runs of the file, then runs under 30 CPU burners (old test red, new test green); counts go in the Log.

## Log

- 2026-11-06 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-053-landing-test-flake
- 2026-11-06 [orchestrator] route confirmed by the human (question form): S, tech, 05-frontend → light QA → one review round → PR → gate-3; E2E no; queue take PR #86
- 2026-11-06 [orchestrator] weekly 43% at start
- 2026-11-06 [frontend-developer] cause: 5 s findBy budget vs a CPU-starved 50-row MUI list render (the test alone takes 6.4 s), not a timer race; fix is test-only (LIST_RENDER, 30 s, on the waits after a list render in "shows the first page of the list"), assertions unchanged. Old test under 30 CPU burners: 3 red of 4 (all on «1–50 из 119»); new test under the same load: 5 of 5 green; 20 sequential runs: 20 of 20 green. ESLint and Prettier clean. stage set to qa (no QA value is documented for the S route; 05-frontend.md names 06-code-review)
- 2026-11-06 [qa] light QA: root cause confirmed, assertions unchanged, no blocking defects (review.md); 3/3 sequential and 2/2 under 30 burners green, workitems folder 909 tests green; two low notes. stage set to 06-code-review
- 2026-11-06 [code-reviewer] round 1: approve; 0 blocker, 0 major, 1 minor (R1-1, debt) open in review.md
- 2026-11-06 [orchestrator] merged main; gate-3 pending with the human; debts D-1 and R1-1 (move LIST_RENDER to a shared helper if another heavy render flakes); weekly 43% at start, 46% at gate-3
- 2026-11-06 [orchestrator] gate-3 approved: the human asked to turn on auto-merge («Включить автомердж»); squash auto-merge on PR #94, it merges when CI is green

## Notes

Summary (from the queue line or an argument): WorkItemFormPage.landing.test.tsx is flaky in full Vitest/hook runs: the pager text «1–50 из 119» is not found in time (failed 2 of 4 runs on an unchanged tree, passes alone); make the wait deterministic so load does not break pushes
