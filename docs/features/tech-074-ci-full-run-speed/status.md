# tech-074-ci-full-run-speed

- type: tech
- size: M
- branch: tech/074-ci-full-run-speed
- stage: 04-backend
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
3. `ReducerBoundCostTests` cold figure: the CI-widened `Budget()` like its siblings.
4. The slow `WorkItemFormPage.*` / `WorkItemsPage.keyword` tests (tech-068, absorbed):
   find why a scenario takes 12-29 s and cut it, or justify what stays.
5. Before/after minutes from CI runs in the PR description.

## Log

- 2026-11-16 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-074-ci-full-run-speed
- 2026-11-16 [route] human confirmed: tech, size M, Dev → QA → Review → PR, gate-3 only; a new line that absorbs tech-068 (withdrawn, PR #202). Queue: add #201, take #204
- 2026-11-16 [baseline] PR #197 run 43859011673: one job, Verify step 60 min (backend 16.5 min: Infrastructure 14.5, Api.IntegrationTests 11.8 side by side; Vitest 2526 s wall, 7277 s summed, 445 files, 3 workers); slowest files WorkItemFormPage.related 471 s / 32 tests, .sections 403 s, .planning 351 s

## Notes

Summary (from the queue line or an argument): Make the CI full run (VERIFY_FULL=1 verify.sh, required check 'verify.sh (change-proportionate)') markedly faster and stop its flakes: backend and frontend as parallel jobs, Vitest sharded across a matrix, the required-check name kept stable; stabilise WorkItemFormPage.membership/readOnly/description and WorkItemsPage.keyword (12-29 s per scenario, time out under load) and the ReducerBoundCostTests timing figure. Baseline PR #197 run 43859011673: ~65 min (backend 16 min, Vitest 2625 s wall, sequential). Absorbs tech-068
