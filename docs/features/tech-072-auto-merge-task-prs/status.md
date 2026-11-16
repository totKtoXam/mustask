# tech-072-auto-merge-task-prs

- type: tech
- size: S
- branch: tech/072-auto-merge-task-prs
- stage: complete
- epic: workflow
- deps: tech-063
- gates: gate-3

## Gates

gate-3: pending · PR (opened after this record; the link is on the PR itself) — light cycle, the human sees the diff in the PR

## Plan

Every task PR, opened after `gate-3: approved`, gets auto-merge with a merge commit right away: `gh pr merge <N> --auto --merge` (no `--delete-branch`). It merges itself once CI is green; a red CI keeps it open and Auto-fix handles it; the human can turn it off per PR ("Disable auto-merge"). Rules that said "only a human presses Merge" follow: `AGENTS.md`, `stages/07-mr-complete.md`, `stages/f-route.md`, `feature.md`, `.claude/commands/ship.md`, `docs/conventions/git.md`, and the heading quoted in `scripts/finish-task.sh` (comments and a message string only).

Unchanged on purpose: gates gate-1/2/3 are the human's; merge commit, not squash; branches are never deleted; queue sync PRs stay squash + auto-merge; no-remote `scripts/finish-task.sh` stays a local squash against a recorded acceptance; the agent never runs `gh pr merge` without `--auto` and never bypasses CI. `.claude/hooks/README.md` is not edited (an edit there runs the hooks offline tables, see tech-063 commit 8cac70e1); its "who presses Merge" wording is left as a known lag.

## Decisions

- 2026-11-16 E2E: no (standing answer; no panel change).

## Log

- 2026-11-16 [human] decided that every task PR gets auto-merge with a merge commit; confirmed the route (tech, S, light cycle) in chat.
- 2026-11-16 [orchestrator] route: tech, S, docs/scripts-comments only → light cycle, no QA/review sessions; the human sees the diff in the PR.
