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
