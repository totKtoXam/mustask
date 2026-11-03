# feat-049-structure-diagram-view

- type: feature
- size: M
- branch: feat/049-structure-diagram-view
- stage: 03-design
- epic: platform/ui
- deps: feat-043
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: rejected 2026-11-03 (the human, in chat): rework together with gate-2's remarks
gate-2: rejected 2026-11-03 (the human, in chat): remarks 1-4 in ## Decisions
gate-3: pending

## Plan

<!-- M: see plan-frontend.md -->

## Decisions

- Gate-1+2 questions, 2026-11-03, the human in chat: OQ-1..OQ-7 all A (OQ-6 threshold 150); the switch is BoxedTabs with a docblock note. Verdict: rejected with remarks — 1) the group «Без проектов и департаментов» goes to the top; 2) mark nesting levels; 3) a control that switches how choosing a node shows the rest: today's dimming stays as the first mode, design proposes more convenient ones; 4) design proposes further convenience improvements.
- Rework 1 seams, decided by the orchestrator: expansion forcing is mode-specific (BR-SD-11); the diagram's expansion survives a view switch; in «Только связанные» the top group disappears when none of its teams is related; «Найти узел» matches names only.
- Gate-1+2 round 2, 2026-11-03, the human in chat: rejected with remarks — 1) the level ruler is clickable to show only the chosen levels, with a switch «только этот уровень / уровень и вложенные»; 2) the group «Без проектов и департаментов» and the hierarchy section collapse; 3) queue a task: drag-and-drop and drawn arrows to move elements between each other; 4) a vertical (top-down) tree layout beside the horizontal one; 5) a switch showing users as an avatar circle with full name and email. Scope, the human in chat: 1, 2, 4 here, folded into US-2, size stays M; 3 queued as feat-050 (PR #57); 5 queued as feat-051 (PR #58), users = team members plus organization members without a team.
- Gate-1+2 round 3, 2026-11-03, the human in chat: rejected with remarks — 1) teams are in practice created linked to a department; 2) icons for «Показ при выборе», «Раскладка», «Режим уровней», «Все уровни»; 3) a distinct outline colour per level; 4) teams stand inside the tree, not apart, in both layouts; 5) choosing a parent emphasises its whole branch, not only its relations. Answers: a team linked to several departments stands under each, with a new legend line for the copies; projects and teams with no department go to a section «Без департамента»; making a department mandatory is not wanted (model stays).

## Log

- 2026-11-03 [orchestrator] started in the app-made worktree feat-049-structure-diagram-view-fa5b15, branch cut from origin/main 92de14d8 (new-task.sh not used: session already bound here). Queue: `queue.sh take` PR #56. Usage at start: weekly 19%, 5-hour 18%.
- 2026-11-03 [orchestrator] route confirmed by the human: feature, M as queued; no L threshold, frontend only in features/organizations, selector-offers carries the links. Route 1 → 2 → 3 → 6 → 7 → 8 → gate-3; gate-1+2 one package. Economy mode; E2E no; 3.3: only the line.
- 2026-11-03 [orchestrator] stage 1 done (business-analyst): spec.md + US-1..US-3, OQ-1..OQ-4; verify.sh green.
- 2026-11-03 [orchestrator] stage 2 (requirements-reviewer): needs-human-decision, 0 blockers, 6 majors, 6 minors (review.md). Fix round (business-analyst): R-1..R-12 closed in the spec or carried by OQ-5..OQ-7. Open for the gate-1+2 package: OQ-1..OQ-7.
- 2026-11-03 [orchestrator] stage 3 done (ux-designer, 439e96fc): design.md, prototype; approach A (SVG + HTML nodes, no dependency), BoxedTabs as the switch (kit deviation). Prototype clicked through in the browser pane: all scenarios, no console errors. At gate-1+2.
- 2026-11-03 [orchestrator] gate-1+2 rejected with remarks 1-4. Rework 1: design + prototype (ux-designer, 91f8c523), spec + stories (business-analyst). Prototype clicked through again: group on top, level marks, three modes, search, expand/collapse all, counters. At gate-1+2 again.
- 2026-11-03 [orchestrator] round 2: design + prototype (ux-designer, 14271ccf), spec + stories (business-analyst, still three stories). Prototype clicked through: level chips and both modes, section folds, vertical layout. At gate-1+2, round 3.

## Notes

Summary (from the queue line): «Структура» gets a tree and diagram switch. The diagram shows the hierarchy of departments, projects and sub-areas, and which teams work on which projects and which projects each team works on; built on the selector-offers read and the area tree, no new read expected. Asked by the human at feat-043 gate-3, 2026-11-02.
