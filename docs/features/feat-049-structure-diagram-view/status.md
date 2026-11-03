# feat-049-structure-diagram-view

- type: feature
- size: M
- branch: feat/049-structure-diagram-view
- stage: 01-analysis
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

- 2026-11-03 [orchestrator] task started in the app-made worktree .claude/worktrees/feat-049-structure-diagram-view-fa5b15 (branch feat/049-structure-diagram-view cut from origin/main 92de14d8; new-task.sh not used because the session is already bound here). Route confirmed by the human: feature, M as queued — no L threshold (no migration, project, slice, ADR expected, breaking change or auth change), frontend only inside features/organizations, more than 5 files so not S; selector-offers already carries projects[].teamIds and teams[].departmentIds. Route: 1 → 2 → 3 (HTML prototype clicked through) → 6 → 7 (one QA) → 8 (one Opus review with security) → gate-3 → squash PR; backend stages have no subject. gate-1 and gate-2 as one package after stage 3 (M shortcut). Diagram drawn with SVG/MUI, no new library (recorded at gate-2). Economy mode; E2E no. Block 3.3: only the queue line. Queue: `queue.sh take` PR #56 (auto-merge on).

## Notes

Summary (from the queue line): «Структура» gets a tree and diagram switch. The diagram shows the hierarchy of departments, projects and sub-areas, and which teams work on which projects and which projects each team works on; built on the selector-offers read and the area tree, no new read expected. Asked by the human at feat-043 gate-3, 2026-11-02.
