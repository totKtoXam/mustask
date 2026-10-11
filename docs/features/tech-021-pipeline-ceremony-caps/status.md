# tech-021-pipeline-ceremony-caps

- type: tech
- size: S
- branch: tech/021-pipeline-ceremony-caps
- stage: complete
- epic: workflow
- deps: -
- gates: gate-3

## Gates

gate-3: approved (confirmed by the human in chat, 2026-10-12)

## Plan

1. routing.md: a "Ceremony caps by size" table (review rounds, E2E runs, extra passes per F/S/M/L) as the one source; an M holds at most 3 user stories, more is split at classification.
2. 06-code-review.md: on S/M one round over the whole diff after the last dev stage, at most one delta read, no lens/skeptic fan-out; points at the caps.
3. 00-orchestrator.md: a pass beyond the caps runs only when the human asks for it.
4. features/README.md: a Log entry is one line of at most 300 characters, details go to commit messages; a byte budget beside the line budget.
5. verify.sh: the byte budget (warn > 12 KB, fail > 24 KB) and a note on entries over 300 characters.
Check: `bash scripts/verify.sh`, then a code-reviewer session (the human's choice at route confirmation).

## Log

- 2026-10-12 [orchestrator] gate-3 approved by the human in chat; stage complete; local squash via finish-task.sh
- 2026-10-12 [orchestrator] F7 (minor) worded as 04-backend § 5 item 3; verify.sh exit 0; stage gate-3
- 2026-10-12 [code-reviewer] round 2 (delta): approve, F1-F6 resolved, F7 minor (review.md)
- 2026-10-12 [orchestrator] round 1 fixes F1-F6 (review.md); verify.sh exit 0; delta read next
- 2026-10-12 [code-reviewer] round 1: changes-requested, 4 major, 2 minor (review.md)
- 2026-10-12 [orchestrator] plan items 1-5 done; `bash scripts/verify.sh` exit 0 (green); stage 06-code-review
- 2026-10-12 [orchestrator] work moved to worktree slow-task-execution-fedb18 (the session cannot write into another worktree); the one new-task.sh made is removed
- 2026-10-12 [orchestrator] route confirmed by the human: S, docs and scripts only, a code-reviewer session before gate-3, no QA session (nothing runtime to test)
- 2026-10-12 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-021-pipeline-ceremony-caps

## Notes

Summary (from the queue line or an argument): Cap per-task ceremony on the short route: one review round and one E2E run per task made binding in the orchestrator and code-review stages (the multi-agent review with skeptics only for L, a re-check only for major findings); a status.md log entry is one line, details go to commit messages, with a size cap; bugfix and small tech tasks write no spec or design, only status.md and the failing test; an M whose code diff passes ~5k lines is split. Evidence: feat-027 ran two review rounds with skeptics, two fix passes and two E2E runs against the 2026-10-09 decision; bug-002 wrote 401 lines of docs for a 16-line fix
