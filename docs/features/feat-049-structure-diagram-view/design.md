# Design: «Структура» diagram view (feat-049)

Stage 3 of an M task, frontend only, inside `features/organizations`. It closes US-1 (switch), US-2 (hierarchy) and US-3 (relations). Prototype: `prototype/index.html` (self-contained; scenarios switch on the page). Interface copy is Russian and final; English prose is for the developer. Baseline: feat-043 design §§ 5.2 and 9, ADR-0032, `docs/conventions/ui-kit.md`.

## 1. Reuse first, and what the kit lacks

| Need | Taken from | Note |
|---|---|---|
| Page head, «Создать департамент» | `PageHeader` (`title`, `primaryAction`) | as today; `commands` stays empty in the diagram (no «Развернуть всё»; US-1) |
| The tree view | `TreeRegistry`, `ListFilter` | untouched |
| Loading (both views) | `RegistrySkeleton` (columns «Название», «Тип») | the page's own, the same in both views |
| Tree read failed | `ErrorBanner` + «Повторить» | the same in both views |
| Offers failed / loading | `InlineNotice` `warning` (+ `action`, `ref`) | § 6 |
| Summary of a chosen node | `InlineNotice` `info`, action «Сбросить» | `role="status"`: announces itself |
| Empty org, 404 | `StateFrame`, `NotFound` (`notFoundLevel`) | the tree's, unchanged |
| Type text + icon on a node | `AreaTypeMark` | |
| «деактивирован» | `Tag` | |
| Names that clip | `TruncatedText` | the full name is the node's accessible name |
| Expand/collapse chevron | `IconAction` (`tooltip={false}`, `tabIndex={-1}`) | like the tree's chevron |
| Announcements | `LiveRegion` (`serial`) | "Выбор сброшен", "Этот узел выбрать нельзя" |
| Links | `useObjectLink` + `rowLink` of `AreasPage` | extract `rowLink` into a function both views share |
| Colours | `theme.palette` roles only (`divider`, `primary.main`, `text.secondary`, `action.hover`) | `colourLiterals.test.ts`: no hex in features, SVG strokes included |

**The kit has no same-address view switch (a finding).** `PageHeader views` are addresses (BR-SD-1 forbids a new one; it is OQ-2 B/C), `ToggleButtonGroup`/`ToggleButton` are forbidden in features (KIT-2), and the `BoxedTabs` docblock says "Not for page views". The design takes **`BoxedTabs` + `BoxedTabPanel`** (value `tree`/`diagram`, `aria-label` «Вид структуры», icons `AccountTreeOutlined`/`HubOutlined` via the kit's icon rule): it is the kit's tablist, passes `kitGuard`, needs no new part, and gives arrow keys, Enter/Space to confirm (US-1 "arrow keys and confirm"; MUI tabs do not follow focus). The docblock names the intended use, not a guard, so the developer reads it as a recorded deviation and adds one sentence to that docblock. The alternative, a new `SegmentedControl` in `shared/ui`, is OQ-7 B's scope (component, test, showcase, guide) and is not recommended.

## 2. Screen map

One screen, one address (BR-SD-1): «Структура» with two views. No change to `src/app/navigation/*` or `routeTree.tsx` is needed or planned. The diagram opens object pages through the same links as the tree (BR-SD-6).

| Story | Closed by |
|---|---|
| US-1 | § 3 (switch, memory), § 6 (states) |
| US-2 | § 4 (layout), § 5 (keyboard) |
| US-3 | § 4.4 (choosing), § 4.5 (large organization) |

## 3. The page and the switch

```
+------------------------------------------------------------------+
| h1 Структура                              [+ Создать департамент] |  PageHeader
+------------------------------------------------------------------+
| [ Дерево | Диаграмма ]          вложенность · команда работает ·  |  BoxedTabs + legend
| [Найти узел] [Развернуть всё] [Показ при выборе: Затемнение ▾]   |  diagram toolbar (§ 12)
+------------------------------------------------------------------+
| (notice: offers not loaded)  /  (summary of the chosen node)     |  InlineNotice
| +--------------------------------------------------------------+ |
| | frame: scrolls in both directions, max-height min(70vh,560px)| |  diagram, § 4
| +--------------------------------------------------------------+ |
| Tab · стрелки · Пробел выбрать · Enter открыть · Esc сбросить    |  hint
+------------------------------------------------------------------+
```

- The tabs stand under the head, left; the legend (decorative, `aria-hidden`, three sample lines with words) at the right, diagram only. The tree view shows `ListFilter` and the head command «Развернуть всё» as today; the diagram view shows neither (US-1) but has its own toolbar (§ 12.2). «Создать департамент» stays in both views for `area.create` (OQ-3 A).
- **State held by `AreasPage`, not by the view:** the tree's filter text, expansion and selection already live there, so a switch keeps them (US-1 "What survives a switch"). The diagram keeps its own expansion (starts from `initialExpansion`, D-3) and its chosen node; switching away drops the chosen node, keeps the expansion for the visit. The panel unmounting loses only scroll position.
- **Memory (BR-SD-9, OQ-2 A):** `localStorage` key `mustask.structure.view.v1.<sub>.<organizationId>`, value `tree` or `diagram`; read once when the page opens; anything else (absent, damaged, storage refused) means `tree`, no error. Written on a user's choice only. A small hook `useStructureView(organizationId)`; never a field of `mustask.context.v2`.
- The page title and address do not change; the tablist is named «Вид структуры».

## 4. The diagram layout

### 4.1 Recommended layout: hierarchy lane at the left, teams lane at the right

```
 Без проектов и департаментов            [ ATLAS ] [ ... ]       <- band on top
 ......................................................................
 Департаменты, проекты и области · уровни                     Команды
  1  2  3  4   (level ruler; each node also has a level badge)
 +-- Департамент Логистика ----------------- 1 команда ·····.         .
 |  +- Проект Складской портал WH --------- 2 команды ----( VENUS )
 |  |   +- Область Склад                                  \( MARS  )
 |  |   |   +- Область Приёмка                             ( JUPITER )
 |  +- Проект Транспорт TR ------------------ 1 команда --/
 +-- Департамент Финансы ...
```

- **Hierarchy lane (left).** One row per visible node in the tree's pre-order (the read's order, never re-sorted: US-2 "Order as in the tree"). A row is a full-width card whose left edge is indented one step (22 px) per level; every card ends at the same right edge, so relation lines leave from one vertical line. Nesting joins are thin solid grey elbows in the indent gutter (parent's chevron column, down and across). The organization is not a node (as in the tree).
- **Why rows and lanes, not a node-link tree.** 1 000 areas over 10 levels make a left-to-right tree 10 columns wide and unreadable; rows are the tree the user already knows, read top to bottom, and keyboard order equals visual order. Lines are only team relations, so they stay few and fixed to one edge.
- **Teams lane (right).** Teams that have at least one drawn relation, one card (220 px) per team. Vertical position: the mean of the vertical centres of its anchors (the visible node each of its lines ends at), teams sorted by it, ties by name, then pushed down where two would overlap. Result: a team stands near its projects and lines run short and rarely cross. Positions change only when expansion changes, never on choosing (US-3 Volume).
- **Group «Без проектов и департаментов» (OQ-4 A).** A band at the **top** of the frame, above both lanes (rework 1): a caption and the team cards wrapped in rows, the offers' order (name). Choosable, no lines.
- **Level marks (rework 1).** A ruler row «1 2 3 …» above the first node, a circled level number at the start of every hierarchy node and faint dotted level guides; all `aria-hidden` text, see § 12.
- **Frame.** A bordered `Surface variant="card"` area with `overflow: auto`, `max-height: min(70vh, 560px)`, `tabIndex` on none of its own (the tree item is the Tab stop). The page never scrolls sideways; at 640 px the frame scrolls (content is about 940 px wide; every node reachable by scroll, a clipped name has its full text in the accessible name and, on hover, `title`).
- **Rendering.** Nodes are absolutely positioned HTML (`Box`), one `<svg aria-hidden>` layer behind them for the elbows and relation curves. HTML keeps links, `Tag`, `AreaTypeMark`, text selection and focus rings native; only lines are SVG. The size of the canvas is the layout's width and height.

### 4.2 Node anatomy

| Node | Shows | Choosable | Opens |
|---|---|---|---|
| Department | chevron, `AreaTypeMark` «Департамент», name (link), count «N команд» | yes | department page |
| Active project | chevron (if sub-areas), «Проект», name (link), key, count «N команд» | yes | project «Общие» |
| Sub-area | chevron (if children), «Область», name (link) | no | its page under the project's menu |
| Deactivated project / its sub-areas | type, name as plain text, `Tag` «деактивирован»; a project shows no key and no count | no | nothing |
| Team | «Команда», name (link), «N проектов» | yes | team page |

Type is always text (and icon), a deactivated node is tagged, a chosen node has a 2 px frame and a chosen-state tint, never colour alone.

### 4.3 Lines

| Line | Style |
|---|---|
| Nesting | 1 px solid grey elbow |
| Relation (project–team, department–team; same style, BR-SD-7) | 1.5 px dashed primary curve (cubic, from the hierarchy lane's right edge to the team card's left edge), a dot at the hierarchy end |
| Relation into a collapsed branch | dotted, ends at the nearest visible ancestor; tooltip-free (legend names it «связь со скрытым узлом») |
| Emphasised relation | 3 px, others dimmed to 15 % |

Constants (named in `lib/diagramLayout.ts`): `ROW 40`, `INDENT 22`, `HIERARCHY_WIDTH 480`, `LANE_GAP 200`, `TEAM_WIDTH 220`, `RELATION_LINE_LIMIT 150`. The initial expansion is `initialExpansion` of `areaTree.ts` (D-3, `TREE_EXPAND_ALL_LIMIT`); a chevron collapses or expands a branch; collapsing a branch that hides the chosen node's emphasised nodes simply turns their lines into collapsed-branch lines.

### 4.4 Choosing and emphasis (US-3)

- Click on a node's body, or Space: choose; again, `Esc` or «Сбросить»: clear. Click on the name opens (a link); the click on the chevron toggles the branch and chooses nothing.
- Emphasised nodes and lines per the table in US-3 (department: itself and its direct teams; project: itself and its teams; team: itself, its projects, its direct departments). A project's sub-areas are not dimmed; everything else is at 50 % opacity, still readable.
- Choosing expands the branches that hide emphasised nodes (BR-SD-11); the chosen node stays focused. The layout is rebuilt only if an expansion changed.
- The summary is an `InlineNotice` `info` above the frame with «Сбросить» and the texts of § 7; it is announced (`role="status"`) and holds the full lists. The names in it are plain text. Long lists scroll inside the notice (max 4 lines).

### 4.5 Large organization (OQ-6)

- Up to **150** relation lines (counted as data pairs: project–team plus department–team, independent of expansion, so the mode does not flip while expanding): all lines are drawn. Above 150: no lines until a node is chosen, then only that node's lines; while nothing is chosen the note «Линии связей показаны для выбранного узла» stands above the frame.
- Why 150: with the teams lane at 40 px a 560 px frame shows about 14 cards; 150 curves leave ~1 line per 4 px of lane edge, the limit before the picture becomes a blur (read in the prototype: "Большая организация" holds ~190 lines). It is a named constant, tunable by the QA stand measurement at stage 6.
- 1 000 areas are ~1 000 positioned elements and at most ~150 paths: no virtualization is planned; if the 2 s measure fails at stage 6, virtualizing rows outside the frame is the fix, a layout-only change.

## 5. Keyboard and screen reader (BR-SD-10, OQ-5 A)

- **One Tab stop**: the frame holds a `role="tree"` named «Диаграмма структуры»; the focused item has `tabIndex 0`, the others `-1` (roving, as `TreeRegistry`). Names, chevrons and team links inside have `tabIndex -1`. The tabs of the switch are a separate stop before it.
- **Item order** (the DOM and arrow order; rework 1): the group's teams (top band), the hierarchy in pre-order, then the teams of the lane (top to bottom). Teams are level-1 items after the hierarchy; the captions «Команды» and «Без проектов и департаментов» are `aria-hidden`, the group's membership is in the team's accessible name.
- Keys: `↑`/`↓` previous/next item; `Home`/`End` first/last; `→` expands a collapsed branch, or on an expanded one goes to its first child; `←` collapses, or on a collapsed node or leaf goes to its parent; `Space` chooses (a non-choosable node announces «Этот узел выбрать нельзя»); `Enter` opens the page (a deactivated node announces «Узел деактивированного проекта ничего не открывает»); `Esc` clears the choice. Focus after a choice stays on the node; after a collapse stays on the collapsed node; after the layout rebuilds the same node keeps focus (`focusItem` by id, without scrolling away).
- Roles: `treeitem` with `aria-level` (depth; teams 1), `aria-expanded` on branches, `aria-selected` on choosable items (true on the chosen one), `aria-label` as in § 7. A visible focus ring (2 px primary, offset) is always drawn. The summary and the live region announce a choice and its clearing.
- The tree view is the text alternative of the hierarchy (the page keeps one tab away); a line has no text of its own, the relation is in the summary and in each node's count.

## 6. States

| State | The user sees (diagram view) |
|---|---|
| Tree loading | `RegistrySkeleton`, the same as the tree; the switch is usable |
| Tree failed (offers anything) | `ErrorBanner` «Повторить» (retries every read that failed: tree and failed offers); no teams drawn alone (US-1) |
| Tree 404 / offers 404 | the tree's `NotFound` (`notFoundLevel`), in both views |
| Offers loading | hierarchy without teams, keys and counts; `InlineNotice` warning «Команды и ключи проектов не загружены» (no button while loading) |
| Offers failed | the same notice with «Повторить» (offers only); after a retry that fails again focus returns to the button (`ref`); no error replaces the hierarchy |
| Both reads failed | the tree-failed row: one `ErrorBanner` «Повторить» |
| Empty org | `StateFrame` «В организации пока нет областей», hint and «Создать департамент» only for `area.create` (the tree's) |
| Only projects, no department | projects at the top level, no empty frame (US-2) |
| Deactivated project | node and sub-areas tagged, plain text, no team lines, not choosable, no count |
| Team with no relation | in the group band; choosing gives «Проекты команды: нет. Департаменты команды: нет» |
| Reads disagree | project node without offer: no key, no lines; offer without a node: ignored; a team whose every project has no node and no department lands in the group |
| Nothing emphasised, many lines | the note «Линии связей показаны для выбранного узла» |
| Reader / member | the same diagram; no «Создать департамент»; the diagram has no other command for any role |
| Narrow (640 px) | the frame scrolls both ways; the tabs and head wrap like feat-043 § 9 |
| After a write in the tree | both reads are refetched by the existing mutations; the diagram redraws from the new data without a reload (US-1) |

## 7. Interface copy (final, Russian)

| Place | Text |
|---|---|
| Tablist name | «Вид структуры» |
| Tabs | «Дерево», «Диаграмма» |
| Tree role name | «Диаграмма структуры» |
| Legend | «вложенность», «команда работает», «связь со скрытым узлом» |
| Lane captions | «Департаменты, проекты и области», «Команды», «Без проектов и департаментов» |
| Counts | «N команда / команды / команд» (project, department), «N проект / проекта / проектов» (team) |
| Chevron | «Свернуть: <имя>», «Развернуть: <имя>» |
| Summary: department | «Команды департамента: A, B» / «Команды департамента: нет» |
| Summary: project | «Команды проекта: A, B» / «Команды проекта: нет» |
| Summary: team | «Проекты команды: A, B. Департаменты команды: C» (each «нет» when empty) |
| Clear | «Сбросить»; announced: «Выбор сброшен» |
| Many lines | «Линии связей показаны для выбранного узла» |
| Offers notice | «Команды и ключи проектов не загружены»; button «Повторить» |
| Not choosable / not opening | «Этот узел выбрать нельзя» / «Узел деактивированного проекта ничего не открывает» |
| Hint under the frame | «Tab входит в диаграмму один раз. Стрелки — переход, Пробел — выбрать, Enter — открыть, Esc — сбросить.» |
| Node names (`aria-label`) | «Департамент Логистика, команд: 1» · «Проект Складской портал (WH), в департаменте Логистика, команд: 2» (top level: «на верхнем уровне»; deactivated: «, деактивирован», no count) · «Область Приёмка, в «Склад»» · «Команда MARS, проектов: 2, департаментов: 0» (group: «, без проектов и департаментов») |
| Toolbar (rework 1) | «Найти узел»; «Найдено: N. Enter — к следующему» / «Ничего не найдено»; «Развернуть всё» / «Свернуть всё»; «Показ при выборе: Затемнение» (menu «Затемнение», «Только связанные», «Подсветка», each with a one-line hint, see prototype); counters «Департаментов: N · Проектов: N · Областей: N · Команд: N»; «Нет связанных проектов и департаментов»; «Показаны только выбранный узел и связанные с ним.»; «Узел скрыт режимом «Только связанные»» |
| Reused | «Структура», «Создать департамент», «деактивирован», «В организации пока нет областей», «Повторить», tree texts from `areaTexts.ts` |

New strings go into `areaTexts.ts` (`areasPage.view`, `areasPage.diagram`), not inline.

## 8. Diagram approach (OQ-7)

| | A: plain SVG + HTML nodes + existing parts in `features/organizations` | B: reusable part in `src/shared/ui` | C: layout library |
|---|---|---|---|
| New dependency | none | none | yes: needs the human's yes, probably an ADR |
| Kit work | none; reuses `Tag`, `InlineNotice`, `BoxedTabs`, `StateFrame`, `AreaTypeMark` | component, test, showcase entry, guide block, whole-suite verify | same as A, plus bundle and licence review |
| Fit for this data | layout is 120 lines of pure code (rows + barycentre lane), tested without a DOM | generic API invented before a second user exists | solves node-link graphs; this picture is lanes, not a graph |
| Accessibility | a native tree with roving focus, as `TreeRegistry` | same, behind a generic API | libraries draw their own canvas or SVG; keyboard and tree role usually rebuilt on top |
| Size | stays M | may become L | probably L |

**Recommendation: A.** No strong reason against it was found: the layout is a function of the area tree and offers, the diagram has one user, and the kit rule is satisfied (no heading, button or table of its own; `IconAction` for the chevron; no hex colours). Promote to `shared/ui` the day a second page needs the lanes (that is then B's own task). Proposed files: `components/StructureDiagram.tsx` (frame, tree role, keys), `components/DiagramNode.tsx`, `lib/diagramModel.ts` (links, emphasis, summaries, counts), `lib/diagramLayout.ts` (positions, anchors, line count, constants), `hooks/useStructureView.ts`. Tests: the model and the layout as pure unit tests; the page tests for the switch, memory, states, the one-Tab-stop keys, "only two reads", and "choosing does not rebuild the layout" (spy on the layout function).

## 9. Recommended answers for the gate-1+2 package

| OQ | Recommended | Why, in a line |
|---|---|---|
| OQ-1 | **A**: draw the deactivated project tagged, no team lines | the diagram then matches the tree; B hides data, C is backend work |
| OQ-2 | **A**: browser storage per user and organization | no address change (BR-SD-1); B/C would be the kit's own `PageHeader views` pattern (a `?view=` address, shareable) and is the fallback if the human wants links |
| OQ-3 | **A**: «Создать департамент» in both views | it is page-level; hiding it in one view surprises the administrator |
| OQ-4 | **A**: the group «Без проектов и департаментов» | a team vanishing from the diagram looks like a defect |
| OQ-5 | **A**: name opens, body chooses, one Tab stop | B is ~1 400 Tab stops; C doubles controls; the prototype shows A works |
| OQ-6 | **A, threshold 150** relation lines, counted as data pairs | beyond it the lane is a blur; constant `RELATION_LINE_LIMIT`, adjustable at stage 6 |
| OQ-7 | **A**: SVG lines + HTML nodes inside `features/organizations` | see § 8 |

## 10. Flags for the orchestrator (no spec edit made)

1. **Kit gap, § 1:** the switch uses `BoxedTabs`, whose docblock says "not for page views". Decide at the gate whether that stands; the alternative is a new kit part (B's scope) or `PageHeader views` with `?view=` (OQ-2 B/C).
2. `src/app/navigation/*` and `routeTree.tsx`: not needed.
3. Superseded by rework 1: the group stands at the top (§ 12).
4. The diagram's expansion is its own and kept for the visit; the spec is silent (US-1 speaks of the tree's expansion only). Adopted here.
5. Spec detail, not a problem: BR-SD-10's `tree` role has no row for the teams; teams are level-1 items after the hierarchy (§ 5).
6. Not planned and not needed: a new read or endpoint.

## 11. Prototype

`prototype/index.html`. Scenarios (top strip): normal organization (with a deactivated project and the no-project group), large organization (lines only for the chosen node), offers loading, offers failed with «Повторить» (second press succeeds), page loading, tree failed, empty organization, not found; role switch (Администратор/Читатель) for «Создать департамент»; a 640 px window. Inside the diagram: choose a project (its teams emphasised) or a team (its projects), name click opens (a toast stands for navigation), chevrons and arrow keys, Space, Enter, Esc, «Сбросить». The prototype's dimming, layout constants and copy are the design's; its MUI parts are drawn by hand.

## 12. Rework 1 (gate-1+2 remarks)

The human rejected the package on 2026-11-03 with four remarks (status.md `## Decisions`); OQ-1..OQ-7 are all A. §§ 4.1, 5, 7 and 11 above are updated; this section is the index.

| Remark | Answer |
|---|---|
| 1. Group to the top | «Без проектов и департаментов» is now a band at the top of the frame, above the hierarchy (caption plus wrapped team cards, offers' order). Keyboard order follows the picture: the group's teams, the hierarchy, the lane's teams. Choosing a team of the group works as before. |
| 2. Nesting levels | Three marks, all text, none by colour: a **level ruler** (1, 2, 3 … at the left edge of each level, above the first node), a **level badge** (small circled number) at the start of every hierarchy node, and faint dotted **level guides** down the lane. The caption reads «Департаменты, проекты и области · уровни вложенности». The ruler shows only the levels present; teams carry no badge. `aria-level` already carries the level for screen readers, so ruler and badge are `aria-hidden`. Chosen over «Уровень N» column headers because a header reads badly with a 22 px step, and the badge works at any depth. |
| 3. Display mode | § 12.1 |
| 4. More convenience | § 12.2 |

### 12.1 «Показ при выборе» (what choosing a node does to the rest)

A kit `MenuButton` (`selection="radio"`, label «Показ при выборе», trigger an `ActionButton` «Показ при выборе: <режим>») in the diagram's toolbar row, beside the search and «Развернуть всё». Three modes:

| Mode | What choosing does | Layout rebuild |
|---|---|---|
| **«Затемнение»** (default; today's behaviour) | the chosen node and what it relates to are emphasised, the rest at 50 %; a project's sub-areas stay; the chosen node's relation lines at full strength, the others faded | no |
| **«Только связанные»** | only the chosen node, what it relates to, their ancestors (forced open) and the sub-areas of a related project are shown; unrelated rows and teams are removed and the lanes close up; relation lines are the chosen node's only; the summary adds «Показаны только выбранный узел и связанные с ним.»; clearing the choice restores the full picture with the user's expansion untouched | **yes**, on every choose and clear |
| **«Подсветка»** | nothing is dimmed or hidden; related nodes get the accent frame and a stronger tint, related lines turn 3 px accent and the other lines turn neutral grey | no |

- **Default and memory:** «Затемнение»; remembered per user and organization in `localStorage`, own key `mustask.structure.mode.v1.<sub>.<organizationId>`, the rules of the view key (a value outside the three is ignored, no error; written on a user's choice only; never inside `mustask.context.v2`). A change applies at once to the current choice and to every later one.
- A «focus» mode that pulls related teams next to the chosen node is **not** proposed: teams already stand at the mean height of their anchors, so it moves little and re-orders the lane under the user's eyes.
- In «Только связанные» the layout is rebuilt on a choice; the "no layout rebuild on choosing" test (US-3 Volume) therefore covers «Затемнение» and «Подсветка» only, and «Только связанные» has its own 200 ms criterion at 200 x 200 (stage-6 stand measure).
- In «Только связанные», jumping by search to a node the mode hides toasts «Узел скрыт режимом «Только связанные»».

### 12.2 Convenience proposals

| Proposal | Verdict | Notes |
|---|---|---|
| Display modes | **in scope**, US-3 | remark 3 |
| **«Найти узел»**: a search field in the diagram's toolbar marks every matching node (ring) and, on Enter, jumps to the next match: expands its branches, focuses and scrolls to it. Case and «ё/е» ignored; «Найдено: N. Enter — к следующему» / «Ничего не найдено». It filters nothing, so BR-SD-8 holds | **in scope**, US-2 | a pure function over the two reads; the kit's search field |
| **«Развернуть всё» / «Свернуть всё»** for the diagram (its own expansion; a toolbar `ActionButton`, disabled when no branch exists) | **in scope**, US-2 | a big organization starts collapsed (D-3); without it expanding is row by row |
| **Counters line** «Департаментов: 2 · Проектов: 5 · Областей: 12 · Команд: 5» at the toolbar's right end (teams only while the offers are loaded) | **in scope**, US-2 | text only; counts all nodes, not only the visible ones |
| Keyboard hints under the frame | already in § 7 | kept |
| Hover preview of relations (emphasis without choosing) | follow-up | mouse-only, needs a keyboard equivalent and a rule against fighting the chosen node |
| Zoom / minimap | follow-up | the frame already scrolls; scaling HTML nodes blurs text and breaks the 640 px rule; a minimap is a second picture to keep in step; neither is cheap with approach A |
| «Focus» mode (teams pulled next to the chosen node) | follow-up, not recommended | § 12.1 |

The additions fit the three stories, no fourth: group, level marks, search, expand/collapse all and counters in US-2; modes in US-3; toolbar and mode memory in US-1.

### 12.3 Spec deltas for the analyst

- **US-1:** (a) reword "the tree's filter and «Развернуть всё»/«Свернуть всё» are not shown beside the diagram": the tree's own filter and head command are not shown, the diagram has its own toolbar (search, expand/collapse all, display mode, counters). (b) New scenario: the display mode is remembered per user and organization under its own key; a damaged value falls back to «Затемнение», no error. (c) BR-SD-9 names the second key. (d) "What survives a switch": the diagram's search text and mode survive, the chosen node does not.
- **US-2:** (a) the group «Без проектов и департаментов» stands at the top, before the hierarchy (the layout line leaves "Layout details are left to design"; US-3 "the group stands after the hierarchy" is replaced). (b) New: levels are marked by text (ruler and badge), never by colour alone, and equal the accessible levels. (c) New: «Найти узел» (marks, Enter jumps and expands branches, «Ничего не найдено», filters nothing, ё/е ignored); «Развернуть всё»/«Свернуть всё» on the diagram's own expansion; the counters line over all nodes. (d) Keyboard scenario: the group's teams come first in the arrow order.
- **US-3:** (a) "What choosing emphasises", "Clearing" and "Many relations" are stated per mode: «Затемнение» is today's; «Подсветка» dims and hides nothing and recolours related nodes and lines; «Только связанные» removes unrelated rows and teams (ancestors open, sub-areas of a related project kept) and restores them on clearing. (b) New: the mode control (`MenuButton` radio, arrow keys, Esc), default «Затемнение», applies at once to the current choice. (c) "Volume": the no-rebuild check is limited to «Затемнение»/«Подсветка»; «Только связанные» gets its own 200 ms criterion. (d) A group team chosen in «Только связанные»: the hierarchy shows «Нет связанных проектов и департаментов».
- **BR-SD-11:** «Только связанные» forces the ancestors of shown nodes open without changing the user's expansion. **BR-SD-8:** the diagram's search never filters.
- **Non-functional:** no new read; the group band, ruler and toolbar change no timing.
