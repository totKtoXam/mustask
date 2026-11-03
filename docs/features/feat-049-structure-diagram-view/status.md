# feat-049-structure-diagram-view

- type: feature
- size: M
- branch: feat/049-structure-diagram-view
- stage: 03-design
- epic: platform/ui
- deps: feat-043
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

<!-- M: see plan-frontend.md -->

## Log

- 2026-11-03 [orchestrator] started in the app-made worktree feat-049-structure-diagram-view-fa5b15, branch cut from origin/main 92de14d8 (new-task.sh not used: session already bound here). Queue: `queue.sh take` PR #56. Usage at start: weekly 19%, 5-hour 18%.
- 2026-11-03 [orchestrator] route confirmed by the human: feature, M as queued; no L threshold, frontend only in features/organizations, selector-offers carries the links. Route 1 → 2 → 3 → 6 → 7 → 8 → gate-3; gate-1+2 one package. Economy mode; E2E no; 3.3: only the line.
- 2026-11-03 [orchestrator] stage 1 done (business-analyst): spec.md + US-1..US-3, OQ-1..OQ-4; verify.sh green.
- 2026-11-03 [orchestrator] stage 2 (requirements-reviewer): needs-human-decision, 0 blockers, 6 majors, 6 minors (review.md). Fix round (business-analyst): R-1..R-12 closed in the spec or carried by OQ-5..OQ-7. Open for the gate-1+2 package: OQ-1..OQ-7.

## Notes

Summary (from the queue line): «Структура» gets a tree and diagram switch. The diagram shows the hierarchy of departments, projects and sub-areas, and which teams work on which projects and which projects each team works on; built on the selector-offers read and the area tree, no new read expected. Asked by the human at feat-043 gate-3, 2026-11-02.
