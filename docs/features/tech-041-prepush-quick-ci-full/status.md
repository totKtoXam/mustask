# tech-041-prepush-quick-ci-full

- type: tech
- size: S
- branch: tech/041-prepush-quick-ci-full
- stage: 04-backend
- epic: workflow
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

1. Today `.githooks/pre-push` forces `VERIFY_FULL=1` (since tech-028 R1-2) and `.github/workflows/ci.yml` runs plain `sh scripts/verify.sh`: the whole frontend suite is gated on every push, and CI, the required check on main, is the lighter one.
2. Turn that round: the hook runs `scripts/verify.sh` as the caller left `VERIFY_FULL` (default off: build, lint, affected tests, change-proportionate); `VERIFY_FULL=1 git push` asks for the full run. CI sets `VERIFY_FULL=1` in its Verify step. The job name stays: it is the required check's context in main's protection.
3. Docs follow where they describe pre-push: git.md (hook table, CI note), 07-mr-complete.md (steps 1 and "Opening the MR"), ai-workflow README ("One full run per tree"), src/frontend/AGENTS.md, the verify.sh header; finish-task.sh keeps `VERIFY_FULL=1` (the local squash has no CI behind it).
4. Check: `BASE=origin/main bash scripts/verify.sh` (shell, ci, prose buckets), exit code into the Log; light cycle, no QA or review session.

## Decisions

- E2E = NO (standing answer since 2026-10-12).
- CI job name `verify.sh (change-proportionate)` is unchanged: main's protection requires exactly this context, a rename would hang every PR.

## Log

- 2026-10-18 [orchestrator] route S, light cycle (docs/scripts/hooks/.github only; the human extended it to .github/ when queueing); standing approval of 2026-10-18 evening (work alone, PRs auto-merged by the orchestrator once CI is green), no route confirmation asked
- 2026-10-18 [new-task] task started, worktree .claude/worktrees/tech-041-prepush-quick-ci-full

## Notes

Summary (from the queue line or an argument): pre-push keeps only the quick checks (build, lint, format); CI runs verify.sh with VERIFY_FULL=1 as the required check on main (protection on since 2026-10-18); git.md, 07-mr-complete.md and the frontend AGENTS.md follow
