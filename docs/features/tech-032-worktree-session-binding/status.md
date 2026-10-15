# tech-032-worktree-session-binding

- type: tech
- size: S
- branch: tech/032-worktree-session-binding
- stage: 04-backend
- epic: workflow
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

1. Add the shared session-binding rule to AGENTS.md and the feature startup contract: after new-task, bind the actual agent session to its worktree before implementation/delegation.
2. Document native relocation when supported, session/root/branch confirmation, and a human-visible interactive handoff fallback when relocation is unavailable; tool-local cd/workdir is insufficient.
3. Align handoff vendor guidance and new-task.sh output with that rule; preserve the existing worktree/branch/files and avoid claiming unsupported automatic relocation.
4. Run proportional verify.sh, independent S QA and review; stop at human gate-3 before publication.

## Decisions

- Human confirmed route option 1: tech/S, four implementation files, ordinary QA/review/gate-3; no standing approval or merge authorization.
- Scope: AGENTS.md, docs/ai-workflow/feature.md, docs/ai-workflow/handoff.md, scripts/new-task.sh. No application runtime, API, secrets or dependency change.
- The rule is vendor-neutral. CLI cwd alone cannot rebind an existing desktop session; unsupported native relocation requires an explicit interactive handoff, not a claim of success.

## Log

- 2026-10-15 [route] Human confirmed option 1 in chat. Size S: four files in workflow tooling, no L threshold; only gate-3 applies.
- 2026-10-15 [queue] Local main contains four unrelated unpublished commits. Only this task's queue-add change was projected onto origin/main (b19abb44); foreign changes were not published or rewritten.
- 2026-10-15 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-032-worktree-session-binding

## Notes

Summary (from the queue line or an argument): Bind agent sessions to the task worktree after new-task
