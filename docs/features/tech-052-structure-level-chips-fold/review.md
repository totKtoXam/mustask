# tech-052 QA (frontend, light S form)

Verdict: no blocker, no major. Two minors (coverage only).

Checked at 4c0a2ebd: folded section, both folded, emptied by level choice, emptied by
«Только связанные» (vertical), horizontal unchanged, keyboard unpress of a folded chip,
no chip/item overlap, and chip ids `${level}-${occurrence}` (unchanged code, one `seen` map
across sections) are pinned by tests or read straight from the diff.

## QA-1
Severity: minor
Where: `StructureDiagram.tsx` `emptyTop` (new chip term); `diagramLayout.test.ts`
Problem: the empty text's position below the last chip band is asserted by no test (layout
tests check chips vs cards only). A regression in `emptyTop` would put «Нет связанных узлов»
over a chip with every test green.
Fix: one component test comparing the empty text's top with the lowest chip's bottom.

## QA-2
Severity: minor
Where: `StructureDiagram.tsx:293` (`resetLevels` fallback to the tree's focus item)
Problem: with chips now kept in both layouts, the «no chip» branch is practically
unreachable and untested. Harmless; note for cleanup.

E2E: no (standing answer).

## Round 1

**Verdict: `approved`.** Whole diff `origin/main...ce9aa9e9`. No blocker, no major; two minors
(debt). Targeted run: `npx vitest run src/features/organizations/lib
src/features/organizations/pages/AreasPage.diagram` → 277/277.

Checked: the Plan is met. Vertical folded section → header + one `SLIM_BAND` row per level of
`sectionDepth` (chip offset 3 px, same as an open slim band); a section emptied by «Только
связанные» is kept (header + slim bands) only when vertical and only when it has roots;
emptied by the level choice was already kept. Horizontal path untouched (both new branches are
guarded by `vertical`; test pins the dropped section and one ruler). `emptyTop` now clears the
lowest chip. Header of a vertical layout with no cards gets `w = -32`, which `DiagramNode.tsx:66`
already renders as auto width (pre-existing, unchanged). Chip ids stay unique (one `seen` map).
No security or architecture surface (pure layout function inside the slice).

### CR-1
Severity: minor
Where: `src/frontend/src/features/organizations/components/StructureDiagram.tsx:289-290`
Problem: the `resetLevels` comment says the layout draws no chip for «folded sections,
vertical layout» — no longer true after this diff (QA-2 is the code side of the same point).
Fix: drop the parenthetical, or say the fallback covers a model with no levels.

### CR-2
Severity: minor
Where: `docs/features/feat-049-structure-diagram-view/design.md:165` (not in the diff)
Problem: the state table says «A section folded | header only; count stays»; in the vertical
layout a folded section now also shows its slim chip band. Nobody acting on it does harm, but
a later layout change could "restore" header-only.
Fix: «header only (vertical: plus its slim level-chip band, tech-052); count stays».

### Behavioural claims
- `diagramLayout.ts:169-170` comment (vertical keeps an emptied section, horizontal drops it) →
  `diagramLayout.ts:171`; tests «keeps a header and all chips … «Только связанные»».
- `diagramLayout.ts:210` comment (folded section keeps all chips in a slim band) →
  `diagramLayout.ts:211-216`; test «keeps every chip of a folded section…».
- `StructureDiagram.tsx:488` comment (empty text below last header / chip) → `:489-493`;
  untested (QA-1).
- `diagramLayout.test.ts` final comment (horizontal unchanged) → asserted in the same test.
- `AreasPage.diagram.qa.test.tsx` comment (folded sections keep chips, pressed) → asserted next line.
- status.md Plan/Log claims (folded and related-emptied keep chips; level-choice already did;
  keyboard unpress test) → the tests above and the new US-2 page test.
- Direction 2: changed behaviour «vertical chips of folded / emptied sections»; grepped
  `chip`, `folded`, `section … drop/hidden`, `draws none`, `SLIM_BAND` over feat-049
  `spec.md`, `design.md`, `stories/`, and `src/frontend/src`. Hits: `design.md:62,71`, US-2:112
  (chips per section band — still true); `design.md:165` → CR-2; `StructureDiagram.tsx:290` →
  CR-1. Not swept: `.claude/`, `AGENTS.md` files (no identifier of this behaviour named there).
