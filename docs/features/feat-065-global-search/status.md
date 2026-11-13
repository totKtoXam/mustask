# feat-065-global-search

- type: feature
- size: L
- branch: feat/065-global-search
- stage: 02-requirements-review
- epic: platform/ui
- deps: feat-041
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

<!-- S/M: implementation plan, 3-10 lines. L: see plan-backend.md / plan-frontend.md -->

## Log

- 2026-11-13 [new-task] task started; session bound to worktree .claude/worktrees/laughing-bartik-3ad2cc (the script's worktree was removed and the branch checked out in the session's own)
- 2026-11-13 [new-task] size mismatch: queue has size:M, human confirmed L
- 2026-11-13 [requirements-reviewer] round 1: needs-human-decision. R-1..R-24 (6 major, 18 minor) in review.md; all fixed in spec and stories but R-22 (accepted-risk, left to the design and the volume test). Palette line now deps on feat-062 and names feat-063. gate-1 asks OQ-1..OQ-6 and the four questions of review.md (R-1, R-4, R-5, R-10).

## Notes

Summary (from the queue line or an argument): A Ctrl+K palette that searches areas, departments, teams, projects, users and work items within the user's sight, with recent items and keyboard navigation; needs its own analysis first (index, ranking, sight)
