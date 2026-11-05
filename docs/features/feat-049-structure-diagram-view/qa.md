# QA: feat-049 structure diagram view

Stage 7, one pass (economy mode), Frontend only. HEAD under test: 4f8a830b. E2E: not run, the human's
standing answer is NO. Verdict: **pass with minors**.

## Frontend

### Check matrix

| Criterion / screen | Check | Result |
|---|---|---|
| US-1 switch, default tree, memory per user and organization, damaged value | `AreasPage.diagram.test.tsx`, `useStructureSettings.test.tsx` | covered, green |
| US-1 only the two reads; no write on choosing, mode, layout, view switch | `AreasPage.diagram.qa.test.tsx` | added, green |
| US-1 offers failed: notice and retry | existing | covered |
| US-2 sections, team copies, levels, lines, layouts (pure) | `diagramModel/Layout.test.ts` | covered |
| US-2 find a node: count, ё/е, nothing found, jump with unfold, cycling | qa test | added, green |
| US-2 toast on a hidden level / removed by «Только связанные»; focus and chips untouched | qa test | added, green |
| US-2 level announcements (only, from, all), «Все уровни» enabled state | qa test | added; found F-2, fixed |
| US-2 «Нет узлов на выбранных уровнях» | qa test | added, green |
| US-2 section fold by arrows, announced | qa test | added, green |
| US-2 vertical note above 120 cards, none in horizontal | qa test (125 extra departments) | added, green |
| US-2 Ctrl+wheel: prevented only with Ctrl, 50-200 % clamp | qa test, synthetic WheelEvent | added, green |
| US-2 middle-button pan: only button 1, grabbing cursor, scroll follows, ends on release and on lost button | qa test | added, green |
| US-2 zoom value button, announcement | qa test | added, green |
| US-2 / US-3 deactivated project: tagged, no link, not choosable, announcements | qa test | added, green |
| US-3 choose, Escape/«Сбросить», «Выбор сброшен», summary announced | existing + qa test | covered |
| BR-SD-12 contrast, levels 1-6, text/outline/badge/chip/tints/lines | `lib/diagramContrast.test.ts` | added; found F-1, fixed |
| Tab stop per toggle button (BR-SD-10 original text) | plan deviation 1, decided by the orchestrator (status Decisions) | accepted, not a defect |

### Defects

| id | Severity | Blocks | Defect | State |
|---|---|---|---|---|
| F-1 | minor (a11y) | no | An included level chip («Уровень и вложенные», light 18 % tint) drew its digit in the level colour: 4.14:1 on level 2 and 4.24:1 on level 3, under BR-SD-12's 4.5:1. Repro: contrast computed from theme, `diagramContrast.test.ts`. | fixed in `StructureDiagram.tsx` (plain text colour on the included tint), test added |
| F-2 | minor | no | After pressing chips 2 and 3 in «Уровень и вложенные» the announcement read «Показаны уровни: 2, 3 и более глубокие»; US-2 requires «2 и более глубокие». | fixed (announces the topmost pressed level only in that mode) |
| F-3 | minor | no | Search jump (Enter) writes the ancestors of the match into the user's expansion even in «Только связанные», where the match is then removed by the mode and only a toast follows; BR-SD-11 says that mode must not change the user's expansion. Repro: mode «Только связанные», choose a department, collapse nothing, search a hidden node in a collapsed branch, Enter. | open, carried to gate-3 |
| F-4 | minor | no | The Ctrl+wheel handler zooms from the last rendered `view.zoom`; a burst of wheel events inside one render takes the same base, so a fast wheel gives fewer steps than events. Result stays within 50-200 %. Not reproduced in jsdom (needs a real wheel). | open, carried to gate-3 |

Tab order, focus ring drawn on the button, and the chip tooltips were read from code only; the plan lists
chips as `span role="button"`, accepted deviation.

### Not measured

- 2 s first draw and 200 ms choose / layout / level / zoom / pan at 200 projects x 200 teams: jsdom cannot
  measure them. By code: `layoutDiagram` sits in a `useMemo` that does not depend on the choice except
  through `related` (null outside «Только связанные»), so choosing in «Затемнение» and «Подсветка» with
  the branch already expanded rebuilds no layout (unfolding a collapsed branch on choose is an expansion
  change and does rebuild; a test counts `layoutDiagram` calls); zoom is a CSS transform plus a scroll fix, no layout. Not run on the stand.
- Real-pointer feel of Ctrl+wheel about the pointer and middle-button pan (handlers tested with synthetic
  events; the scroll anchor maths in `diagramZoom.test.ts`).
- Prototype side by side, screen-reader speech, 640 px wrap.
- E2E: skipped by the human's decision.

### Runs (single files)

- `AreasPage.diagram.qa.test.tsx`: 18 tests green (one run hit the 60 s timeout because the machine slept;
  rerun green).
- `diagramContrast.test.ts` + `AreasPage.diagram.test.tsx`: 77 tests green.
- ESLint clean and `tsc -b` clean on the touched files.
