# tech-046-unit-coverage-60

- type: tech
- size: S
- branch: tech/046-unit-coverage-60
- stage: gate-3
- epic: platform/ci
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

1. Measure line and branch coverage of the backend test projects (coverlet) and the frontend (Vitest, v8) before any change.
2. `src/backend/coverage.runsettings`: Cobertura output, EF migrations and generated code excluded. `Directory.Build.props`: with `MustaskCoverageDir` set, every test project writes its report into its own folder.
3. `scripts/coverage-gate.mjs` + `scripts/coverage-floors.json`: line-coverage floors, checked per assembly for the unit-test projects, overall for `*.IntegrationTests`, and over the whole frontend suite.
4. `scripts/verify.sh`: collect coverage and run the gate on the backend run that includes integration tests (Docker up) and on the full Vitest run (`VERIFY_FULL=1`, which CI sets); `@vitest/coverage-v8` and a `coverage` block in `vite.config.ts`.
5. Check: `VERIFY_FULL=1 bash scripts/verify.sh` once, before the push.

## Decisions

- Floors (line %), set by the human 2026-10-28: frontend 90, Application 60, Domain 90, Shared 70, Infrastructure 30, Api 5, integration tests 30. The brief's «60%» became per-area floors that catch a drop. Today every area is already above its floor, so no new tests were needed.
- Unit coverage merges every test project except `*.IntegrationTests`. `Mustask.Infrastructure.Tests` counts as unit even though some of its tests need Docker, so the gate runs only where they run.
- Coverage is gated only on full runs: a partial run (`vitest --changed`, or backend without Docker) would read as a false drop.
- E2E = NO (standing answer since 2026-10-12); no runtime change.

## Coverage

Before, on `a653917f`, unit-test projects only, lines (branches): Domain 93.6 (92.1), Application 64.5 (56.0), Shared 78.8 (67.9), Infrastructure 28.6 (37.0), Api 5.9 (5.9); frontend 98.3 (94.0). That run lost hits from part of the Infrastructure suite.

On `e4d8f124` (current main) with `coverage.runsettings`, lines: Domain 95.0, Application 87.2, Shared 77.8, Infrastructure 91.6, Api 8.4 (unit); integration tests 95.3 overall. Frontend, from the final verify run on `265e5793`: lines 98.5, branches 94.6.

## Log
- 2026-10-29 [orchestrator] pushed once with `--no-verify`, approved by the human (via the orchestrator, «⭐ Один push с --no-verify»): three pre-push quick runs failed only on time-budget tests (backend volume tests, Vitest timeouts), a different set each time, while other worktrees ran their suites; CI's required full verify.sh is the check
- 2026-10-29 [orchestrator] weekly usage 10% after the build (0% at the start, 2026-10-27; account-wide, other sessions ran in parallel)
- 2026-10-29 [orchestrator] `BASE=origin/main VERIFY_FULL=1 bash scripts/verify.sh` on `265e5793`: RESULT green, backend tests including integration, backend coverage floors and frontend coverage floor all ok
- 2026-10-29 [orchestrator] the first full run failed two verify.sh self-test cases (the sandbox had no stub for the new gate: a `node` stub and a "floor missed: red" case were added) and `WorkItemFilterVolumeTests` once (the 2 s budget, under machine load). Run alone with coverage, its reads took 40–620 ms, and it passed in both other full runs
- 2026-10-28 [orchestrator] route tech S confirmed by the human, together with the floors (option 1, Infrastructure raised to 30)
- 2026-10-28 [orchestrator] queue line via sync PR #33 (`scripts/queue.sh add`); auto-merge was refused by the permission classifier, so the human merges it
- 2026-10-27 [orchestrator] task started from the human's «сделай unittests покрытие 60%» (2026-10-27)

## Notes

Summary (from the queue line): Unit-test coverage floors in CI: frontend 90, Application 60, Domain 90, Shared 70, Infrastructure 30, Api 5, integration 30 (line %)
