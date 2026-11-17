# tech-074-ci-full-run-speed

- type: tech
- size: M
- branch: tech/074-ci-full-run-speed
- stage: gate-3 (awaiting the human)
- epic: platform/ci
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

1. `scripts/verify.sh` learns parts: `VERIFY_PART`, `VERIFY_DOTNET_FILTER`,
   `VERIFY_SHARD`, `VERIFY_COVERAGE_OUT`, `VERIFY_NEEDS_OUT`; a part leaves no
   green stamp. Without them a local run is unchanged.
2. `.github/workflows/ci.yml`: plan → static / backend ×3 (by test assembly) /
   frontend ×6 (Vitest shards) in parallel → `verify.sh (change-proportionate)`,
   the required check, keeps its name and checks the coverage floors on the
   merged reports (`scripts/ci-coverage-gate.sh`).
3. ~~`ReducerBoundCostTests` cold figure~~ — out of scope, tech-073's.
4. ~~The slow `WorkItemFormPage.*` / `WorkItemsPage.keyword` tests~~ — out of
   scope, tech-073's (see [scope] in the log).
5. Before/after minutes from CI runs in the PR description.

Scope: CI run speed only — jobs, sharding, the backend split. No test, test
timeout or test budget is edited here; a change to worker counts or test
parallelism is agreed with tech-073 first.

## Log

- 2026-11-16 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-074-ci-full-run-speed
- 2026-11-16 [route] human confirmed: tech, size M, Dev → QA → Review → PR, gate-3 only; a new line that absorbs tech-068 (withdrawn, PR #202). Queue: add #201, take #204
- 2026-11-16 [baseline] PR #197 run 43859011673: one job, Verify step 60 min (backend 16.5 min: Infrastructure 14.5, Api.IntegrationTests 11.8 side by side; Vitest 2526 s wall, 7277 s summed, 445 files, 3 workers); slowest files WorkItemFormPage.related 471 s / 32 tests, .sections 403 s, .planning 351 s
- 2026-11-16 [overlap] tech-073 (flaky CI tests) owns the test timeouts, `vi.setConfig` removal, routes.members and the reducer cold-figure `Budget(5)`; tech-074 keeps CI parallelism and the form tests' own speed. Reverted the reducer edit here.
- 2026-11-16 [measure] split run 43899184051: backend jobs infrastructure 10.8 min, api-integration 10.0, rest 1.8 (parallel; was 16.5 min). Profile run 43898877800, quiet runner: WorkItemFormPage.related + .membership 187 s for 38 tests (1-10 s each, single fork) — slow alone, not only under load.
- 2026-11-16 [scope] the human's decision, relayed by the orchestrator: tech-074 is CI run speed only. Flaky and slow tests (Vitest timeouts, wall-clock/cost tests, WorkItemFormPage*) belong to tech-073 (session "Fix flaky CI tests"); the profiling of the form tests is dropped and its temporary workflow removed. The 6 Vitest shards were reported to tech-073.
- 2026-11-16 [measure] full frontend runs (a temporary one-line touch of `main.tsx`, reverted): run 43922066342 11.3 min, run 43927683530 11.1 min end to end; shards 5.6-9.9 min (shard 3 the pole), 432 files = every test file of the branch; merged coverage 97.96 % lines, floor ok in the verify job's log. A docs/scripts-only PR: 37 s (run 43920631146). Estimate for a full-stack PR: max(backend ~11, frontend ~10) + plan/verify ~1.5 ≈ 12-13 min, from ~65.
- 2026-11-16 [dev] done: verify.sh parts + 9 new cases (35 green locally), ci.yml parallel jobs, scripts/ci-coverage-gate.sh, git.md. tech-073 confirmed no change to shards or workers is needed for its fixes.
- 2026-11-17 [qa] CI pass: case scripts green (35 + 32, 8 new stamp cases for slices); no blocker/major; minor D-1: the backend coverage merge was never exercised in CI; see qa-report.md
- 2026-11-17 [review] round 1 (whole diff, one session): approve; 0 blocker/major open, 4 minor + 1 nit as debts (R1-1: record the 4e189aae run's green backend floors before reverting it); see review.md
- 2026-11-17 [measure] D-1/R1-1 closed: run 44073047217 (temporary Program.cs touch 4e189aae, reverted in 5a2c6e18) — backend groups infrastructure 11.9 min, api-integration 8.4, rest 2.3, end to end 12.4 min; "Backend coverage floors" green on the merged reports (Domain 90.5, Application 82.5, Shared 77.8, Infrastructure 92.2, Api 11.8, integration 86.3 %), as the single-job figures of run 43859011673. Review fixes R1-3..R1-5 in b3e62e61; R1-2 (a case script for ci-coverage-gate.sh) and D-2 (unexpanded names of skipped matrix jobs) left as debts. No src/ change against the merge-base; merges cleanly into main.

## Notes

Summary (from the queue line or an argument): Make the CI full run (VERIFY_FULL=1 verify.sh, required check 'verify.sh (change-proportionate)') markedly faster: backend and frontend as parallel jobs, Vitest sharded across a matrix, the required-check name kept stable. (Narrowed 2026-11-16: the flaky/slow tests are tech-073's.) Baseline PR #197 run 43859011673: ~65 min (backend 16 min, Vitest 2625 s wall, sequential). Absorbs tech-068
