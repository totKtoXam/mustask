# tech-039-restore-executable-bit

- type: tech
- size: S
- branch: tech/039-restore-executable-bit
- stage: complete
- epic: workflow
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

1. Repro: `git ls-files -s scripts .githooks` lists 100644 on check-gates.sh, finish-task.sh, keycloak-seed.sh, warm-worktree.sh, tests/verify-stamp-cases.sh and the three .githooks files; backlog.sh, queue.sh and verify.sh are already 100755 on main (tech-034 touched them).
2. `git update-index --chmod=+x` on every one of those that is 100644, in one commit.
3. scripts/verify.sh, the shell bucket: the mode is read from the index (`git ls-files -s`), not by `[ -x ]`, which on Windows answers yes for every *.sh and shebang file whatever mode git holds; an untracked file keeps the `[ -x ]` test.
4. A case in scripts/tests/verify-stamp-cases.sh (a throwaway repo): a 100644 script fails the tooling check, the same script at 100755 passes.
5. Check: `bash scripts/verify.sh`, exit code into the Log (light cycle, no QA or review session).

## Log

- 2026-10-18 [new-task] task started (scripts/new-task.sh; its worktree was removed so a worktree-isolated agent could take the branch)
- 2026-10-18 [orchestrator] route S, light cycle — confirmed by the human in chat («⭐ S, лёгкий цикл»), which also grants standing approval for the light cycle on docs/.claude/scripts/hooks-only S tasks until revoked
- 2026-10-18 [task-executor] dev: 8 files to 100755 (bf628cea; backlog.sh, queue.sh, verify.sh already were); verify.sh reads the mode from `git ls-files -s`; 3 cases in verify-stamp-cases.sh, 2 of them fail on the old verify.sh here (56fa037a)
- 2026-10-18 [task-executor] `bash scripts/verify.sh` RESULT: green, exit 0 (shell and prose buckets, 22 cases). Light cycle: no QA or review session, no E2E (no runtime change). gate-3 left pending for the human
- 2026-10-18 [task-executor] CI red on PR #15: verify.sh ran `sh -n` (dash on Linux) on bash-shebang keycloak-seed.sh; the syntax check now follows the shebang, 2 cases added; verify.sh green

## Notes

Summary (from the queue line or an argument): Restore the executable bit (100755) on main's scripts and hooks that are 100644: scripts/backlog.sh, check-gates.sh, finish-task.sh, keycloak-seed.sh, queue.sh, verify.sh, warm-worktree.sh, tests/verify-stamp-cases.sh and .githooks/commit-msg, pre-commit, pre-push. CI's tooling check fails any PR that touches one of them, and verify.sh's own check should cover the mode before push on Windows
