# tech-052-structure-level-chips-fold

- type: tech
- size: S
- branch: tech/052-structure-level-chips-fold
- stage: 05-frontend
- epic: platform/ui
- deps: feat-049
- gates: gate-3

## Gates

gate-3: pending

## Plan

- What: in the vertical layout of «Структура» → «Диаграмма», `diagramLayout.ts` pushes level
  chips only for an unfolded section that keeps at least one visible card; a folded section,
  or one emptied by the level choice / «Только связанные», loses its chips, pressed ones
  included (feat-049 review.md CR-4).
- How (the human's choice, 2026-11-09): such a section keeps its chips in a slim band under
  its header — one chip per level of the section, each in a `SLIM_BAND`-high row — so the
  chips keep their place and meaning and a pressed level can always be unpressed.
- Where: `src/frontend/src/features/organizations/lib/diagramLayout.ts` (+ `diagramLayout.test.ts`),
  and a `StructureDiagram`/`AreasPage.diagram` test for the user path if needed.
- Check: layout tests — folded section, section emptied by the level choice and by «Только
  связанные» each emit all their level chips, no card overlaps them; a test unpresses a
  pressed chip of a folded section; the horizontal layout is unchanged; `scripts/verify.sh`.

## Log

- 2026-11-09 [orchestrator] route confirmed by the human: tech, S, frontend-developer →
  qa-engineer → code-reviewer → gate-3 → PR. Grounds: visible behaviour changes (not F), no L
  thresholds, ~1–2 h, 2–4 files, one layer (frontend organizations slice). The chip treatment
  was put to the human as a question: «Тонкая полоса в секции» chosen over one shared row.
  Economy mode: one agent per stage, one Opus review.
- 2026-11-09 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-052-structure-level-chips-fold

## Notes

Summary (from the queue line or an argument): «Структура», «Диаграмма», vertical layout: level chips vanish with their section when it is folded or emptied by the level choice, pressed chips included, so a pressed level cannot be unpressed until the section unfolds. Keep the chips (or one shared row) visible whatever the folds; see feat-049 review.md CR-4
