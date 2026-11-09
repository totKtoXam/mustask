# tech-063-merge-commit-policy

- type: tech
- size: S
- branch: tech/063-merge-commit-policy
- stage: complete
- epic: workflow
- deps: -
- gates: gate-3

## Gates

gate-3: pending · PR (opened after this record; the link is on the PR itself) — light cycle, the human sees the diff in the PR and presses Merge

## Plan

Task PRs are merged with a merge commit, not squash: the branch keeps its commits, the PR title carries the short id `(tech-NNN)`. The rules that said squash follow: `AGENTS.md`, `docs/conventions/git.md` (method, merge settings, commit granularity), `stages/07-mr-complete.md` (the PR command is now `gh pr create`, no squash flag), `f-route.md`, `s-route.md`, `.claude/commands/ship.md`, `.claude/hooks/README.md`. Unchanged on purpose: queue PRs from `scripts/queue.sh` (squash) and the no-remote local ending `scripts/finish-task.sh` (local squash); scripts and `docs/features/*` history are not touched.

## Decisions

- Merge method for task PRs: merge commit (human, 2026-11-09).

## Log

- 2026-11-09 [orchestrator] `bash scripts/verify.sh` exit 0, RESULT: green; light cycle, docs/.claude only, no QA and no review session.
- 2026-11-09 [orchestrator] route confirmed by the human: "⭐ S, лёгкий цикл". Reason for the change, in the human's words: «я специально поставил мердж, т.к. по большим коммитам будет сложнее искать изменения и человеку и агенту, плюс к этому будет тратиться много токенов».
- 2026-11-09 [orchestrator] queue line by `scripts/queue.sh add` (PR #107, merged); taken by `queue.sh take` (PR #108). Binding: new-task.sh's worktree was removed and the task branch checked out in the session's own worktree, as for tech-055.
- 2026-11-09 [orchestrator] CI red on PR #109: touching `.claude/hooks/README.md` made verify.sh run the hook test tables on the Linux runner for the first time (the folder had not changed since bootstrap); 6 home-directory cases and the Windows-only PowerShell adapter fail there. Unrelated to this task, so the one-word README edit ("squash merges" → "merge commits") is reverted; the stale word stays until the hook tests are made runner-portable.

## Notes

Summary (from the queue line): Merge task PRs with a merge commit instead of squash, and update the rules
