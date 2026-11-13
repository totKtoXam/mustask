# feat-065-global-search

- type: feature
- size: L
- branch: feat/065-global-search
- stage: 03-ux-design
- epic: platform/ui
- deps: feat-041
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: approved (by the human in chat, 2026-11-13)
gate-2: pending
gate-3: pending

## Plan

<!-- S/M: implementation plan, 3-10 lines. L: see plan-backend.md / plan-frontend.md -->

## Log

- 2026-11-13 [new-task] task started; session bound to worktree .claude/worktrees/laughing-bartik-3ad2cc (the script's worktree was removed and the branch checked out in the session's own)
- 2026-11-13 [new-task] size mismatch: queue has size:M, human confirmed L
- 2026-11-13 [orchestrator] route confirmed by the human in chat: L (thresholds: a new search slice; an ADR departing from ADR-0015 for full-text/trigram), BA → requirements review → gate-1 → UX design + HTML prototype → gate-2; stop after gate-2, implementation waits for feat-041. Economy mode: one agent per stage, one Opus review. Queue line taken by PR #159.
- 2026-11-13 [business-analyst] spec.md and US-1..US-6 written; OQ-1..OQ-6; decomposition into four lines.
- 2026-11-13 [requirements-reviewer] round 1: needs-human-decision. R-1..R-24 (6 major, 18 minor) in review.md; all fixed in spec and stories but R-22 (accepted-risk, left to the design and the volume test). Palette line now deps on feat-062 and names feat-063. gate-1 asks OQ-1..OQ-6 and the four questions of review.md (R-1, R-4, R-5, R-10).
- 2026-11-13 [orchestrator] the human at gate-1: OQ-1 two modes — the palette stays quick (identifier + title), a full search page (descriptions, full-text) is reached from a palette row and designed here as its own delivery line; OQ-2 pg_trgm plus a typo-tolerant fallback tier (≥ 4 characters); OQ-3 name and email; OQ-4 localStorage; OQ-5 any opening; OQ-6 the person's work items (R-10 empty list accepted); R-1 the editor keeps Ctrl/Cmd+K; R-4 the palette waits for feat-062; R-5 the palette's last row goes to the full search page; OQ-7 the page lists work items only.
- 2026-11-13 [business-analyst] gate-1 decisions folded in: US-7 full search page, BR-GS-17 typo tier, BR-GS-18; six delivery lines. No round-2 review (economy mode).
- 2026-11-13 [orchestrator] gate-1 approved by the human in chat. Stage 3 started: one ux-designer run.

## Notes

Summary (from the queue line or an argument): A Ctrl+K palette that searches areas, departments, teams, projects, users and work items within the user's sight, with recent items and keyboard navigation; needs its own analysis first (index, ranking, sight)
