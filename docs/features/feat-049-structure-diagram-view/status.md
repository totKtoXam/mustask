# feat-049-structure-diagram-view
- type: feature
- size: M
- branch: feat/049-structure-diagram-view
- stage: 06-frontend
- epic: platform/ui
- deps: feat-043
- gates: gate-1, gate-2, gate-3
## Gates
gate-1: approved 2026-11-03 (the human, in chat, round 5): with the small edits below
gate-2: approved 2026-11-03 (the human, in chat, round 5): toggle icons redrawn by the orchestrator
gate-3: pending
## Plan
See [plan-frontend.md](plan-frontend.md).
## Decisions
- Gate-1+2 questions, 2026-11-03, the human in chat: OQ-1..OQ-7 all A (OQ-6 threshold 150); the switch is BoxedTabs with a docblock note. Verdict: rejected with remarks — 1) the group «Без проектов и департаментов» goes to the top; 2) mark nesting levels; 3) a control that switches how choosing a node shows the rest: today's dimming stays as the first mode, design proposes more convenient ones; 4) design proposes further convenience improvements.
- Rework 1 seams, decided by the orchestrator: expansion forcing is mode-specific (BR-SD-11); the diagram's expansion survives a view switch; in «Только связанные» the top group disappears when none of its teams is related; «Найти узел» matches names only.
- Gate-1+2 round 2, 2026-11-03, the human in chat: rejected with remarks — 1) the level ruler is clickable to show only the chosen levels, with a switch «только этот уровень / уровень и вложенные»; 2) the group «Без проектов и департаментов» and the hierarchy section collapse; 3) queue a task: drag-and-drop and drawn arrows to move elements between each other; 4) a vertical (top-down) tree layout beside the horizontal one; 5) a switch showing users as an avatar circle with full name and email. Scope, the human in chat: 1, 2, 4 here, folded into US-2, size stays M; 3 queued as feat-050 (PR #57); 5 queued as feat-051 (PR #58), users = team members plus organization members without a team.
- Gate-1+2 round 3, 2026-11-03, the human in chat: rejected with remarks — 1) teams are in practice created linked to a department; 2) icons for «Показ при выборе», «Раскладка», «Режим уровней», «Все уровни»; 3) a distinct outline colour per level; 4) teams stand inside the tree, not apart, in both layouts; 5) choosing a parent emphasises its whole branch, not only its relations. Answers: a team linked to several departments stands under each, with a new legend line for the copies; projects and teams with no department go to a section «Без департамента»; making a department mandatory is not wanted (model stays).
- Gate-1+2 round 4, 2026-11-03, the human in chat: rejected with remarks — 1) «Режим уровней» is a segmented icon toggle (like a code/preview switch), not a dropdown; its focus circle drifts off the icon; 2) level colours on the cards themselves (department, project, area, team), not only the badges; 3) «Показ при выборе» and «Раскладка» are segmented toggles too; 4) more contrast for the legend's line styles; 5) pan by dragging with the middle mouse button, zoom by Ctrl + wheel, beside the scrollbars; 6) «Режим уровней» stands beside «Раскладка»; 7) a stray empty circle beside the level chips goes; 8) each level chip has a short hover hint of what the level means. Folded into US-2, size stays M.
- Gate-1+2 round 5, 2026-11-03, the human in chat: OQ-7 answered A (add `IconToggleGroup` to shared/ui); approved with one small edit: better toggle icons, found or drawn. The orchestrator drew them (three-node mode icons, tree layouts, level bars) in the prototype and design § 4.9.
- Stage 6, decided by the orchestrator: each `IconToggleGroup` is one Tab stop with arrow keys inside (MUI roving, the designer's open question 2), not a stop per button; US-2 keyboard scenario updated. Other stage-6 deviations are in plan-frontend.md.
## Log
- 2026-11-03 [orchestrator] started in the app-made worktree feat-049-structure-diagram-view-fa5b15, branch cut from origin/main 92de14d8 (new-task.sh not used: session already bound here). Queue: `queue.sh take` PR #56. Usage at start: weekly 19%, 5-hour 18%.
- 2026-11-03 [orchestrator] route confirmed by the human: feature, M as queued; no L threshold, frontend only in features/organizations, selector-offers carries the links. Route 1 → 2 → 3 → 6 → 7 → 8 → gate-3; gate-1+2 one package. Economy mode; E2E no; 3.3: only the line.
- 2026-11-03 [orchestrator] stage 1 done (business-analyst): spec.md + US-1..US-3, OQ-1..OQ-4; verify.sh green.
- 2026-11-03 [orchestrator] stage 2 (requirements-reviewer): needs-human-decision, 0 blockers, 6 majors, 6 minors (review.md). Fix round (business-analyst): R-1..R-12 closed in the spec or carried by OQ-5..OQ-7. Open for the gate-1+2 package: OQ-1..OQ-7.
- 2026-11-03 [orchestrator] stage 3 done (ux-designer, 439e96fc): design.md, prototype; approach A (SVG + HTML nodes, no dependency), BoxedTabs as the switch (kit deviation). Prototype clicked through in the browser pane: all scenarios, no console errors. At gate-1+2.
- 2026-11-03 [orchestrator] gate-1+2 rejected with remarks 1-4. Rework 1: design + prototype (ux-designer, 91f8c523), spec + stories (business-analyst). Prototype clicked through again: group on top, level marks, three modes, search, expand/collapse all, counters. At gate-1+2 again.
- 2026-11-03 [orchestrator] round 2: design + prototype (ux-designer, 14271ccf), spec + stories (business-analyst, still three stories). Prototype clicked through: level chips and both modes, section folds, vertical layout. At gate-1+2, round 3.
- 2026-11-03 [orchestrator] round 3: design + prototype (ux-designer, c9c64848), spec + stories (business-analyst, BR-SD-16..19). Level 3 moved to deep orange 900 so badge text reaches 4.5:1. Prototype clicked through. At gate-1+2, round 4.
- 2026-11-03 [orchestrator] round 4: design + prototype (ux-designer, f0edee3d), spec + stories (business-analyst, BR-SD-20; OQ-7 reopened for `IconToggleGroup`). Design § 4.9 rewritten by the orchestrator (toggles, zoom). Prototype clicked through. At gate-1+2, round 5.
- 2026-11-03 [orchestrator] gate-1+2 approved (round 5) with redrawn toggle icons; OQ-7 A. Stage 6 next.
- 2026-11-03 [orchestrator] stage 6 done (frontend-developer, 734ea8b4, 2f1b15a6, 2c7d61b5): single files green, tsc clean, ESLint 0 errors. Full run pending the orchestrator's go.
## Notes
Summary (from the queue line): «Структура» gets a tree and diagram switch. The diagram shows the hierarchy of departments, projects and sub-areas, and which teams work on which projects and which projects each team works on; built on the selector-offers read and the area tree, no new read expected. Asked by the human at feat-043 gate-3, 2026-11-02.
