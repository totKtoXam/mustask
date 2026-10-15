# tech-032-worktree-session-binding

- type: tech
- size: S
- branch: tech/032-worktree-session-binding
- stage: complete
- epic: workflow
- deps: -
- gates: gate-3

## Gates

gate-3: approved

## Plan

1. Add the shared session-binding rule to AGENTS.md and the feature startup contract: after new-task, bind the actual agent session to its worktree before implementation/delegation.
2. Document native relocation when supported, session/root/branch confirmation, and a human-visible interactive handoff fallback when relocation is unavailable; tool-local cd/workdir is insufficient.
3. Align handoff vendor guidance and new-task.sh output with that rule; preserve the existing worktree/branch/files and avoid claiming unsupported automatic relocation.
4. Run proportional verify.sh, independent S QA and review; stop at human gate-3 before publication.

## Decisions

- Human declined browser E2E for tech-032; actual desktop session transfer remains a manual check.
- Human confirmed route option 1: tech/S, four implementation files, ordinary QA/review/gate-3; no standing approval or merge authorization.
- Scope: AGENTS.md, docs/ai-workflow/feature.md, docs/ai-workflow/handoff.md, scripts/new-task.sh, scripts/handoff.sh. No application runtime, API, secrets or dependency change.
- The rule is vendor-neutral. CLI cwd alone cannot rebind an existing desktop session; unsupported native relocation requires an explicit interactive handoff, not a claim of success.

## Log

- 2026-10-15 [route] Human confirmed option 1 in chat. Size S: four files in workflow tooling, no L threshold; only gate-3 applies.
- 2026-10-15 [queue] Local main contains four unrelated unpublished commits. Only this task's queue-add change was projected onto origin/main (b19abb44); foreign changes were not published or rewritten.
- 2026-10-15 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-032-worktree-session-binding

- 2026-10-15 [scope] Added scripts/handoff.sh as the fifth implementation file: its generated removal/fresh-checkout advice contradicted the preservation rule. Size remains S: five files in workflow tooling, same route and gates.

- 2026-10-15 [verify] scripts/verify.ps1 exit 0; proportional docs/workflow/tooling checks and all four handoff short-ID resolution cases passed. git diff --check passed. No runtime service was started; actual desktop session relocation was not exercised.

- 2026-10-15 [QA/review round 1] Two majors returned to implementation: detached QA/review checkout proof and queue bookkeeping before unsupported-relocation handoff. Findings are in qa.md and review.md; corrections stay within the five workflow files.

- 2026-10-15 [fix round 1] Binding proof now supports the documented detached QA/review checkout at the expected task commit. Startup Log and queue take precede a possible session transfer. Proportional verify.ps1 passed after script edits; final branch-confirmation wording received git diff --check and reviewer delta instead of a duplicate suite. QA is not repeated for prose/output-only corrections.

- 2026-10-15 [review delta] APPROVE: F1/F2 closed, no open blocker/major/minor findings. QA round-1 report is retained as history; prose/output-only corrections were checked by the reviewer delta. Gate-3 remains pending; no PR opened.
- 2026-10-15 [E2E] Human selected no browser E2E for tech-032. No browser E2E, application stand or desktop relocation was run; actual session transfer remains a manual check.

- 2026-10-15 [gate-3] Human explicitly approved in chat. Marked complete for the PR squash; publication follows, merge remains human-owned.

## Notes

Summary (from the queue line or an argument): Bind agent sessions to the task worktree after new-task
