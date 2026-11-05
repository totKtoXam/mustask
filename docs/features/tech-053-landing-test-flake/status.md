# tech-053-landing-test-flake

- type: tech
- size: S
- branch: tech/053-landing-test-flake
- stage: 05-frontend
- epic: platform/ci
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

<!-- S/M: implementation plan, 3-10 lines. L: see plan-backend.md / plan-frontend.md -->

## Log

- 2026-11-06 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-053-landing-test-flake
- 2026-11-06 [orchestrator] route confirmed by the human (question form): S, tech, 05-frontend → light QA → one review round → PR → gate-3; E2E no; queue take PR #86
- 2026-11-06 [orchestrator] weekly 43% at start

## Notes

Summary (from the queue line or an argument): WorkItemFormPage.landing.test.tsx is flaky in full Vitest/hook runs: the pager text «1–50 из 119» is not found in time (failed 2 of 4 runs on an unchanged tree, passes alone); make the wait deterministic so load does not break pushes
