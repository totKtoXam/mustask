# tech-073-flaky-ci-tests

- type: tech
- size: S
- branch: tech/073-flaky-ci-tests
- stage: 05-frontend
- epic: platform/ci
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

Evidence: nine failed CI runs, 2026-11-13..16 (PR #188 ×3, #191, #197 ×2, main ×2), each green on rerun. Causes and fixes:

1. Work item form page tests (`WorkItemFormPage.membership` ×4, `.readOnly` ×2, `.description` ×2, also on main): on the 4-core runner with v8 coverage a test of these files takes about 20 s (14 description tests: 310 s), and one re-render after a role change or a keystroke outlasted the 5 s `asyncUtilTimeout`; `readOnly` was cut by the 60 s test timeout. Fix: `src/test/setup.ts` 15 s on CI (5 s locally), `vite.config.ts` `testTimeout` 180 s on CI (120 s locally). No retries.
2. `vi.setConfig({ testTimeout })` in 48 test files was dead under `isolate: false`: only the first file a worker runs gets it (reproduced with two files on one worker) — why readOnly was cut at 60 s under its own 120 s. Fix: the calls go, the one budget lives in `vite.config.ts`.
3. `routes.members.test.tsx` «Вы больше не состоите…» (#191): not timing — a product defect. The notice lives about 100 ms on the members page, then the shell's fallback notice replaces it; the test passed whenever it caught those 100 ms. Fix: the test reads what stays after the move and is `it.fails` against bug-007 (queued, PR #206), by the human's choice.
4. `ReducerBoundCostTests.Reduce_FirstCeilingSizedDocumentOfTheProcess_Figure` (#197): a bare 5 000 ms cold-start assertion, without the CI margin (`RichTextTestSupport.Budget`) the class's other budgets have; dotnet test runs the assemblies side by side on 4 cores. Fix: `Budget(5)`.

Not in scope: `NavigationReadsQaTests` (#188 first run) and the two route-inventory failures of #197's first run were real failures of those branches, fixed by their next commits.

## Decisions

- 2026-11-16 D-1 (human): routes.members — `it.fails` + a bug task (bug-007), not a product fix in this PR and not a test that accepts the fallback notice.
- 2026-11-16 E2E: no (standing answer; no panel change).

## Log

- 2026-11-16 [orchestrator] route confirmed by the human: tech, S, dev → light QA → one Opus review → gate-3 → PR (merge commit, auto-merge). Economy mode: CI is the verification. Queue add PR #200, take PR #207; bug-007 queued, PR #206.
