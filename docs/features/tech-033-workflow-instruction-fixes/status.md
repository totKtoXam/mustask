# tech-033-workflow-instruction-fixes

- type: tech
- size: S
- branch: tech/033-workflow-instruction-fixes
- stage: complete
- epic: workflow
- deps: -
- gates: gate-3

## Gates

gate-3: approved (confirmed by the human in chat, 2026-10-16) — PR https://github.com/totKtoXam/mustask/pull/4

## Plan

1. Feature adapters (`.claude/commands/feature.md`,
   `.agents/skills/feature/SKILL.md`) read only `feature.md`; README and
   `routing.md` are read under its "Starting a task" condition (new task or
   route in question).
2. `M` gate package: the requirements-review stage and its command defer the
   gate-1 verdict on `M` to the package after stage 3; the design stage, its
   command and both ux-designer adapters accept a reviewed spec with
   `gate-1: pending` on `M`.
3. `M` single QA: `/dev-backend` skips stage 5 on `M` with a frontend, and
   `/dev-frontend` runs the merged QA pass over both layers.
4. Codex `qa-engineer.toml`: E2E per `05-frontend.md` / ADR-0023 (agent may
   run it after the human's yes, impersonation sign-in), the question stays.
5. `feature.md`: model tiers come from `dispatch.md`, not `routing.md`.

## Log

- 2026-10-16 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-033-workflow-instruction-fixes
- 2026-10-16 route confirmed by the human: tech, S, full cycle (one review round, gate-3). Grounds: ~15 instruction files, no runtime code, but M-route semantics change — not F, not light cycle.
- 2026-10-16 source: an external review of five defects (adapter over-reading, M gate conflict, M double QA, stale Codex E2E, tier link).
- 2026-10-16 queue synced via PR #3 (squash); local main still holds the pre-squash queue commits — realigning it and `queue.sh take` are pending.
- 2026-10-16 code review round 1 (S, one session): changes-requested — 1 major (F1: M package gate-1 rejected loop dead-ends at /design), 2 minor (F2, F3); findings in review.md
- 2026-10-16 F1, F2, F3 fixed: rework round accepts `gate-1: rejected` with a later stage-2 verdict; dev-backend step 4 conditional; M single pass noted in test-plan and dispatch.
- 2026-10-16 code review delta (round 1): approve — F1–F3 resolved, 0 blocker/major; minor F4 (routing.md "stays pending") fixed in place; details in review.md

## Notes

Summary (from the queue line or an argument): Workflow instruction fixes: conditional reading in the feature adapters, M gate-1/gate-2 package and single QA in stage commands, Codex QA E2E per ADR-0023, tier table link to dispatch.md
