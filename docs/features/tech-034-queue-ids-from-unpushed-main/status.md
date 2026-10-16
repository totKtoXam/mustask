# tech-034-queue-ids-from-unpushed-main

- type: tech
- size: S
- branch: tech/034-queue-ids-from-unpushed-main
- stage: 06-code-review
- epic: workflow
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

1. Repro: with an origin, two `scripts/queue.sh add` on local main before a
   push issue the same number. `backlog.sh --ids` reads docs/backlog.md and
   docs/backlog/withdrawn.md only from origin/main; numbering.md (source 3)
   counts them over every ref.
2. `scripts/backlog.sh` taken_ids: the queue files' numbers over every ref in
   refs/heads and refs/remotes, in one `git for-each-ref | git cat-file
   --batch` pass. The QUEUED summary keeps origin/main and says when local
   main has queue commits not on it.
3. `scripts/new-task.sh` hot path: the missing `|` before the grep over
   withdrawn.md, so a withdrawn hot number is never reissued.
4. Test with the code: `scripts/tests/queue-numbering-cases.sh` (a throwaway
   repo with a bare origin, the handoff-cases.sh pattern), failing against
   the unfixed scripts; verify.sh runs it when backlog.sh, queue.sh,
   new-task.sh or the cases file change.
5. numbering.md and the scripts' header comments say what is read from where,
   where their wording no longer matches.

## Log

- 2026-10-17 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-034-queue-ids-from-unpushed-main
- 2026-10-17 [orchestrator] route confirmed by the human (question form): tech, S, the full S route — development, QA and code review in clean contexts, then gate-3. The missing pipe in new-task.sh's hot withdrawn scan is folded in: same numbering defect, told to the human in chat.
- 2026-10-17 [orchestrator] after PR #5 merged, local main saved to backup/main-2026-10-17 (a360042c) and reset to origin/main 5fac020c with the human's approval; nothing on it was missing from origin. Take commit 15094ef8 is on local main, unpushed (rides the next sync).
- 2026-10-17 [task-executor] dev: backlog.sh reads the queue files over every ref in one cat-file pass and notes unpushed queue commits; new-task.sh hot pipe; 7 cases in scripts/tests/queue-numbering-cases.sh, wired in verify.sh; numbering.md. 26e84e91, 49af6133, e5043357.
- 2026-10-17 [orchestrator] `bash scripts/verify.sh` exit 0 (green). The dev run was cut by a session restart before its report; QA checks that the cases fail against the unfixed scripts.
- 2026-10-17 [orchestrator] standing approval from the human in chat for the night: do everything without them, record every question for the morning. E2E stays "no" (paused 2026-10-12). Gates decided by the orchestrator and marked for review; merges stay the human's.

## Notes

Summary (from the queue line or an argument): queue.sh add issues a number already held by an unpushed queue commit on local main: backlog.sh --ids reads the queue only from origin/main. Count queue-file numbers over every ref as numbering.md says, with a regression case; also the missing pipe in new-task.sh's hot withdrawn scan (found in doc-012: feat-039, mod-017 issued twice)
