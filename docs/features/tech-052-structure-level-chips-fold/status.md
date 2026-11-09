# tech-052-structure-level-chips-fold

- type: tech
- size: S
- branch: tech/052-structure-level-chips-fold
- stage: 08-review
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
- 2026-11-09 [orchestrator] queue line taken: PR #111 (auto-merge on green CI).
- 2026-11-09 [frontend-developer] 66827799: vertical layout — a folded section and one emptied
  by «Только связанные» keep all their level chips in a slim band under the header (a section
  emptied by the level choice already did; now pinned by a test); `emptyTop` clears the lowest
  chip. `AreasPage.diagram.qa.test.tsx` (outside the brief's zone, accepted by the
  orchestrator): its CR-4 test pinned the vanishing chips, now asserts they stay pressed; a
  new test unpresses a chip of a folded section by keyboard. `npx vitest run
  src/features/organizations/lib src/features/organizations/pages/AreasPage.diagram` → exit 0
  (277/277); `npm run lint` → 0 errors; `npx tsc -b` → ok. verify.sh not run (left to QA).
- 2026-11-09 [qa-engineer] 3fba4f85: frontend QA (S, light) at 4c0a2ebd — no blocker or
  major; 2 minors (QA-1 empty-text vs chip band position untested; QA-2 resetLevels fallback
  now near-dead). `bash scripts/verify.sh` → exit 0, green (the affected step ran all 163
  frontend files, 2356 tests, plus 14 guard files, 604 tests). E2E: no (standing answer).
- 2026-11-09 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-052-structure-level-chips-fold

## Notes

Summary (from the queue line or an argument): «Структура», «Диаграмма», vertical layout: level chips vanish with their section when it is folded or emptied by the level choice, pressed chips included, so a pressed level cannot be unpressed until the section unfolds. Keep the chips (or one shared row) visible whatever the folds; see feat-049 review.md CR-4
