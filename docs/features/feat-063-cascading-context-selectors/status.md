# feat-063-cascading-context-selectors

- type: feature
- size: M
- branch: feat/063-cascading-context-selectors
- stage: gate-1+2
- epic: core/org-access
- deps: mod-022,feat-061
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

<!-- S/M: implementation plan, 3-10 lines. L: see plan-backend.md / plan-frontend.md -->

## Log

- 2026-11-20 [new-task] task started; the branch was moved into the orchestrating session's worktree `.claude/worktrees/admiring-bohr-8bfe03` (bound: `git branch --show-current` = feat/063-cascading-context-selectors)
- 2026-11-20 [route] confirmed by the orchestrator session (doc-012-matrix-organization-model), acting for the human under the 2026-11-17 coordination delegation: feature, M, as in the queue. Grounds: no L threshold (ADR-0040 already accepted, no new project or slice, no migration, no breaking contract expected — any cascade/search params additive, no auth change); not S (panel + offers read, more than 5 files, two layers); 2 stories (doc-016 US-3, US-4 within one organization). Route: BA → requirements review → UX + prototype → gate-1 + gate-2 as one package → backend only if additive params are needed → frontend → one QA pass (E2E: no, standing answer) → one code-review round → gate-3 → PR (merge commit, auto-merge). Scope held strictly: feat-052 «Создать…» and feat-064 cross-organization stay out. Economy mode in force.
- 2026-11-20 [stage 1-2] spec + 3 stories; requirements review round 1 needs rework (R-1..R-10), round 2 approved-by-ai with R-11 fixed by the orchestrator (MARS kept through a grant). Open point for the orchestrator: ADR-0040 § 3 says «все» resets every lower level, BR-TD-10 (governing) says team/project «все» reset nothing — an ADR correction note is owed, outside this task's write zone.
- 2026-11-20 [stage 3] design.md + prototype/index.html (b401453e); prototype clicked through in a browser by the orchestrator (panel opens, choosing MARS selects «Логистика», projects narrow to A, B). gate-1 + gate-2 package sent to the orchestrator session.

## Notes

Summary (from the queue line or an argument): Rework feat-043's four header selectors into one context path button with type icons opening a panel: a search on one organization's objects and four cascading columns with short colour stripes (doc-016 BR-TD-21, -22); offers cascade, choosing a lower object selects its parents, lower levels reset (BR-TD-7..-11, -18); «Остальные» retired; remembered selections repaired. doc-016 US-3, US-4 within one organization
