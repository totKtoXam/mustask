# tech-023-artifact-size-budgets

- type: tech
- size: S
- branch: tech/023-artifact-size-budgets
- stage: complete
- epic: workflow
- deps: -
- gates: gate-3

## Gates

gate-3: approved (confirmed by the human in chat, 2026-10-12)

## Plan

1. docs/features/README.md, "Size budget": a per-artifact table — design.md ≤ 32 KB; review.md ≤ 16 KB a round, 32 KB the file; qa.md ≤ 16 KB a pass or run, 32 KB the file; spec and stories keep their line budgets in 01 (pointer only).
2. 03-ux-design.md: the design budget, over it the designer returns a split proposal (system-wide theme work is its own task).
3. 04-backend.md § 5 and 05-frontend.md: qa.md budget; an E2E run records totals, failures and the stand record, not a row per passing test.
4. 06-code-review.md: review.md budget; a finding is a few lines, the claims list one line each.
5. verify.sh: a note (not a failure) when a changed in-flight design/review/qa.md passes 32 KB — in-flight designs are already at 74–212 KB, and rewriting an approved design costs more than it saves.
Check: `bash scripts/verify.sh`, then a code-reviewer session.

## Log

- 2026-10-12 [orchestrator] gate-3 approved by the human in chat; stage complete; local squash via finish-task.sh
- 2026-10-12 [code-reviewer] round 2 (delta): approve, nothing open (review.md); stage gate-3
- 2026-10-12 [orchestrator] 0b7f1da: the R1-2 note had nested double quotes, escaped and re-tested
- 2026-10-12 [orchestrator] R1-1..R1-3 fixed (prose and the note wording); verify.sh exit 0; delta read next
- 2026-10-12 [code-reviewer] round 1: changes-requested, 1 major, 2 minor (review.md)
- 2026-10-12 [orchestrator] plan done; item 3 needed no 05-frontend edit (05 points at 04 "Run economy", where the budget sits); note tested on a 39 KB file; verify.sh exit 0; stage 06-code-review
- 2026-10-12 [orchestrator] route confirmed by the human (question form): S, docs and scripts, a code-reviewer session, gate-3; scope re-cut from tech-022 (withdrawn); worked in worktree slow-task-execution-fedb18
- 2026-10-12 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-023-artifact-size-budgets

## Notes

Summary (from the queue line or an argument): Size budgets for design.md, review.md (per round) and qa.md beside the existing spec, story and status.md ones: stated once in docs/features/README.md, pointed at from stages 03, 04 and 06, a verify.sh note when a changed file passes its budget. Evidence: feat-027 design.md 77 KB, review.md 154 KB, qa.md 28 KB, read by every agent after them
