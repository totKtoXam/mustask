# feat-049 — frontend plan

Slice: `src/frontend/src/features/organizations`, page `AreasPage` («Структура»). No backend change,
no new read: the area tree and `selector-offers` only. No new npm dependency. Reference:
`design.md`, `spec.md`, `prototype/index.html`.

## 1. Approach

SVG line layer behind absolutely positioned HTML cards (approach A). The layout is a pure function
`layoutDiagram(model, expansion, options)`; choosing a node changes emphasis only and never rebuilds
it, except in «Только связанные», whose kept set depends on the choice by nature.

## 2. Pieces

| Layer | File | Role |
|---|---|---|
| theme | `app/theme/tokens.ts` | `shell.levelThree` (#BF360C), `shell.levelFour` (#7B1FA2); contrast pairs in `contrast.test.ts` |
| kit | `shared/ui/actions/IconToggleGroup.tsx` | OQ-7 A: icon toggle group over MUI ToggleButtonGroup + Tooltip; showcase demo, test, `ui-kit.md` |
| kit | `shared/ui/card/BoxedTabs.tsx` | docblock sentence only: the view switch |
| lib | `diagramModel.ts` | areas + offers to nodes, team copies, sections, pairs, emphasis, search, summary |
| lib | `diagramLayout.ts` | horizontal lane and vertical tidy tree, chips, lines, limits (150 lines, 120 vertical note) |
| lib | `diagramLevels.ts`, `diagramZoom.ts` | level choice and colours; zoom 50–200 %, pointer-anchored scroll |
| lib | `areaTexts.ts` | `structureDiagram` copy (design § 7) |
| hooks | `useStructureSettings` | view / mode / layout, one localStorage key each, per user and organization |
| hooks | `useDiagramView` | page state kept across a switch, never stored |
| hooks | `useAreaRowLink` | the row link shared by tree and diagram (extracted from `AreasPage`) |
| components | `StructureDiagram`, `DiagramToolbar`, `DiagramNode`, `DiagramLines`, `DiagramIcons` | the diagram |
| page | `AreasPage` | `BoxedTabs` «Вид структуры», legend beside it, offers read only for the diagram |

## 3. States

Loading, error with retry and not-found come from the tree read (unchanged); the offers read adds a
notice «связи не загрузились» with retry, and a not-found answer from it takes the page's not-found.
An empty organization shows the existing empty state with no tabs. «Нет узлов на выбранных
уровнях» and the large-organization line note are in the diagram.

## 4. Accessibility

Tree role with a roving tabindex (one Tab stop), `aria-level` = true level + 1, keys ↑ ↓ Home End
→ ← Space Enter Esc; live-region announcements; chips are a named group.

## 5. Tests

`lib/diagram*.test.ts` (model, layout, zoom/levels), `IconToggleGroup.test.tsx`,
`hooks/useStructureSettings.test.tsx`, `pages/AreasPage.diagram.test.tsx` (switch, memory, tree
semantics, offers retry, zoom limits, keyboard, choosing). Existing `AreasPage.test.tsx`,
`kitGuard`, `colourLiterals` and the contrast test stay green.

## 6. Deviations from design.md

- `IconToggleGroup` is one Tab stop with arrow keys (MUI's roving focus), not a Tab stop per
  button as § 4.9 / BR-SD-10 say.
- Level chips are `span role="button"` with `aria-pressed` and a Tooltip: the kit's buttons take no
  colour and `<button>` is barred in features.
- Card rings are outlines, not shadows (kit rule KIT-16); the canvas frame has a divider border.
- Added aria-hidden chevron texts (`toggle`) not in the copy table.
- «Только связанные» force-expands every kept node so related teams under a collapsed department
  show.
- Level colours live in `shell.*` roles, because `secondary` is neutral text.

## 7. Left for QA

Real-browser performance (2 s first draw / 200 ms interaction), text contrast on the tints, Ctrl+wheel
and middle-button pan with a real pointer, the prototype side-by-side.
