# Design: «Структура» diagram view (feat-049)

Stage 3 of an M task, frontend only, inside `features/organizations`. Closes US-1 (switch), US-2 (hierarchy, sections, levels, layouts, toolbar) and US-3 (relations, display modes). Prototype: `prototype/index.html` (self-contained; scenarios switch on the page). Interface copy is Russian and final; prose is English. Baseline: feat-043 design §§ 5.2 and 9, ADR-0032, `docs/conventions/ui-kit.md`. Rework 1 and 2 (gate-1+2 remarks) are folded into the body; §§ 12-13 index them and list the spec deltas.

## 1. Reuse first, and what the kit lacks

| Need | Taken from | Note |
|---|---|---|
| Page head, «Создать департамент» | `PageHeader` (`title`, `primaryAction`) | `commands` stays empty in the diagram |
| Tree view | `TreeRegistry`, `ListFilter` | untouched |
| Loading, tree failed | `RegistrySkeleton` (columns «Название», «Тип»), `ErrorBanner` «Повторить» | the page's own, the same in both views |
| Offers failed/loading; summary; notes | `InlineNotice` (`warning`, `info`; `action`, `ref`) | § 6 |
| Empty org, 404 | `StateFrame`, `NotFound` (`notFoundLevel`) | the tree's |
| Type text + icon, tag | `AreaTypeMark`, `Tag` «деактивирован» | |
| Clipped names | `TruncatedText` | full name is the accessible name |
| Chevrons, level chips, section toggles | `IconAction` (`tooltip={false}`, `tabIndex={-1}`) / `ActionButton` | like the tree's chevron |
| The three menus (display mode, layout, level mode) | `MenuButton` `selection="radio"` | trigger an `ActionButton` «<название>: <значение>» |
| Search, «Развернуть всё», «Все уровни» | kit search field, `ActionButton` | |
| Announcements | `LiveRegion` (`serial`) | |
| Links | `useObjectLink` + `rowLink` of `AreasPage` | extract `rowLink` so both views share it |
| Colours | `theme.palette` roles only | `colourLiterals.test.ts`: no hex in features, SVG strokes included |

**The kit has no same-address view switch.** `PageHeader views` are addresses (BR-SD-1 forbids a new one: OQ-2 B/C), `ToggleButtonGroup` is forbidden in features (KIT-2), and the `BoxedTabs` docblock says "Not for page views". The design takes **`BoxedTabs` + `BoxedTabPanel`** (values `tree`/`diagram`, name «Вид структуры», arrow keys, Enter/Space confirm). It passes `kitGuard` and needs no new part; the docblock names an intended use, not a guard, so this is a recorded deviation (decided OQ-7 package; the developer adds a sentence to the docblock).

## 2. Screen map

One screen, one address: «Структура», two views. Nothing in `src/app/navigation/*` or `routeTree.tsx` changes. Links are the tree's (BR-SD-6). US-1: § 3; US-2: §§ 4-5; US-3: §§ 4.7-4.8.

## 3. The page, the switch and what is remembered

```
+------------------------------------------------------------------+
| h1 Структура                              [+ Создать департамент] |
| [ Дерево | Диаграмма ]          вложенность · команда работает · |
| [Найти узел] [Развернуть всё] [Показ при выборе ▾] [Раскладка ▾] |  diagram toolbar
|                           Департаментов: 2 · Проектов: 5 · ...   |
| (offers notice) / (summary of the chosen node)                   |
| +--------------------------------------------------------------+ |
| | frame: scrolls both ways, max-height min(70vh,560px)         | |
| +--------------------------------------------------------------+ |
| Tab · стрелки · Пробел выбрать · Enter открыть · Esc сбросить    |
+------------------------------------------------------------------+
```

- The tree view keeps `ListFilter` and «Развернуть всё» as today. The diagram has its own toolbar: search «Найти узел», «Развернуть всё»/«Свернуть всё» (its own expansion), the two menus, counters. «Создать департамент» stays in both views for `area.create` (OQ-3 A).
- **Page state** (in `AreasPage`, kept across a view switch for the visit, never stored): tree filter, tree expansion and selection; the diagram's expansion, search text, section folds and level choice. Dropped on a switch: the chosen node.
- **Stored in `localStorage`** per user and organization, each under its own key, never inside `mustask.context.v2`; a value outside the allowed set, an absent or refused storage means the default, no error; written on a user's choice only: `mustask.structure.view.v1.<sub>.<org>` (`tree` default / `diagram`), `mustask.structure.mode.v1.<sub>.<org>` (`dim` default / `only` / `hl`), `mustask.structure.layout.v1.<sub>.<org>` (`h` default / `v`). One hook `useStructureSettings(organizationId)` reads and writes the three.
- Not stored, on purpose: the **level choice** (§ 4.4), the section folds and the search text. They are filters of one visit: a stored filter reopens the page missing nodes and the user cannot tell why.

## 4. The diagram

### 4.1 Sections

The frame has two sections, each with a header that is a disclosure (`treeitem` with `aria-expanded`, a chevron, the title and a count; click on it, `Enter`/`Space`, `←`/`→` fold and unfold it):

1. **«Без проектов и департаментов»** at the **top** (OQ-4 A): teams with no relation, wrapped cards, the offers' order (name), choosable, no lines. Shown only when such teams exist.
2. **«Департаменты, проекты и области»**: the hierarchy **and the teams lane** (the lane's lines have no anchor without the hierarchy, so folding the section folds both). Its controls row holds the level mode menu and «Все уровни» (§ 4.4).

A fold is page state. Choosing a node folds nothing, but it **unfolds** a folded section that holds what it emphasises (BR-SD-11), as it unfolds hidden branches. Both folded: the frame shows two headers.

### 4.2 Horizontal layout (default «Горизонтально»)

```
 ▾ Без проектов и департаментов · 1
   [ ATLAS ]
 ▾ Департаменты, проекты и области · 12
   [Режим уровней ▾] [Все уровни]
   (1)(2)(3)(4)   <- level chips = the ruler, aligned with the columns
 +-- 1 Департамент Логистика ---------- 1 команда ·····.      Команды
 |  +- 2 Проект Складской портал WH --- 2 команды ----( VENUS )
 |  |   +- 3 Область Склад                            \( MARS  )
 +-- 1 Департамент Финансы ...
```

- **Hierarchy lane (left):** one row per visible node, pre-order of the read (never re-sorted), a full-width card indented 22 px per true level, all ending at one right edge; level badge in each node; faint dotted level guides; nesting joins are thin solid elbows in the indent gutter.
- **Teams lane (right):** one card (220 px) per team with a drawn anchor, at the mean height of its anchors, sorted by it (ties by name), pushed down to avoid overlap. A team whose every anchor is hidden (levels, folds) stays in the lane at the bottom with no line.
- Why rows and a lane, not a node-link tree: a left-to-right tree of 1 000 areas over 10 levels is unreadable; rows are the tree the user knows, keyboard order equals visual order, lines are only team relations fixed to one edge.

### 4.3 Vertical layout («Вертикально», remark R2-3)

```
 ▾ Без проектов и департаментов · 1
   [ ATLAS ]
 ▾ Департаменты, проекты и области · 12
   [Режим уровней ▾] [Все уровни]
 (1)      [Логистика]            [Финансы]         [Мобильное прил.]
 (2)  [Складской портал]  [Транспорт]  [Биллинг]  [Архив 2023]
 (3)  [Склад]                                       [Сверка]
 (4)  [Приёмка] [Отгрузка]
                         Команды
                 [ MARS ] [ VENUS ] [ JUPITER ] [ SATURN ]
```

- **Rows are levels:** departments and top-level projects on the top row, projects under departments on the second, sub-areas below. A **tidy top-down tree**: leaves take consecutive slots (156 px card + 12 px gap), a parent stands over the middle of its visible children. The level chips stand in a left gutter, one per row (chip = row label, same clicks as horizontal).
- **Group on top:** the same band, full frame width.
- **Teams:** one row **below the whole hierarchy**, each card at the mean x of its anchors (sorted, pushed right on overlap), a «Команды» caption. A team's lines run down from the anchor's bottom edge, curving to the card's top edge, behind the nodes they cross.
- **Nesting joins:** from the parent's bottom centre down to a rail, across, down into the child's top; a join across hidden levels is dotted.
- **Nodes** are 156 × 58 px: line 1 chevron, badge, type; line 2 name (link); line 3 key, «деактивирован», count. Team cards 156 × 52.
- **Large organization:** a vertical tree is as wide as its leaf count. The initial expansion (D-3, above 100 nodes top level only) keeps the first view narrow; above **120 visible nodes** a note «Вертикальная раскладка широкая: показано узлов — N. Сверните ветви, выберите уровни или переключитесь на горизонтальную.» stands above the frame (`InlineNotice` info; nothing is blocked). Levels and folds are the tools to narrow it; the horizontal layout is the answer to a wide picture.
- **Is the layout still a small pure function? Yes.** One function `layoutDiagram(model, expansion, options{orientation, levels, sections, related})` first fixes the visible set and nearest visible ancestors (shared), then either assigns rows (horizontal: index → y, level → x) or runs the tidy placement (vertical: leaf slots, parents centred, level → y). The teams lane is the same "mean of anchors, sort, push" on y or on x. About 150 lines, unit-tested without a DOM; approach A stands.

### 4.4 Levels (remark R2-1)

- **Chips = ruler.** One toggle chip per level of the whole tree (1 … N, N from the area tree, so chips never jump): a chip in the horizontal layout stands at its column's left edge above the first row, in the vertical layout in the left gutter at its row. Clicking toggles it (`aria-pressed`, name «Уровень 2»); multi-select. A chip pressed shows full; in «Уровень и вложенные» the deeper chips it includes show a light tint. The same chips mark depth in both layouts, so level marks are text in a chip and a badge, never colour alone.
- **Mode menu** (`MenuButton` radio, «Режим уровней: <значение>»), default **«Только выбранные уровни»**: shown are exactly the pressed levels. **«Уровень и вложенные»**: shown are the **topmost pressed level and every deeper one** (a second pressed chip below the first changes nothing; the tint says so). No chip pressed means all levels, in both modes.
- **«Все уровни»** (`ActionButton`, disabled while nothing is pressed) unpresses every chip; the mode stays.
- **Rows of a hidden level drop** (not collapse into an ancestor): the picture holds only what was asked for. A node whose parent is hidden **attaches to its nearest visible ancestor** by a **dotted** nesting join (a solid join means a direct parent); one with no visible ancestor stands at its true column or row with no join. Indent stays by true level (horizontal) and rows stay at their level (vertical), so gaps show that levels are missing and the chips stay aligned; in the vertical layout a level with no shown node is a thin 30 px strip.
- **Lines and teams:** teams are never hidden by levels. A relation line goes to the node's nearest visible ancestor (dotted, "связь через скрытый узел", as for a collapsed branch) and, with no visible ancestor, is not drawn; the team keeps its card (bottom of the lane). Counts and summaries (US-3) are unaffected: they speak of data, not of what is drawn.
- **Interplay:** the level filter applies after expansion and after «Только связанные» (both narrow; their intersection shows). A chosen node on a hidden level stays chosen and emphasised through its lines and the summary; search that finds a node on a hidden level toasts «Узел скрыт выбором уровней».
- **Keyboard/aria:** chips and the menu are real buttons in a `group` named «Уровни вложенности», **before** the tree in the Tab order (so each is a Tab stop; the group is outside `role="tree"`); chips toggle with Space/Enter; changing the choice announces «Показаны уровни: 2, 3» (+ «и более глубокие») or «Показаны все уровни» and keeps focus on the chip. After a level change focus on a tree item moves to the nearest visible one.
- **Survives a view switch** for the visit (page state), is dropped on organization change, is **not stored** (§ 3).

### 4.5 Node anatomy

| Node | Shows | Choosable | Opens |
|---|---|---|---|
| Department | chevron, level badge, «Департамент», name (link), «N команд» | yes | department page |
| Active project | chevron if children, badge, «Проект», name, key, «N команд» | yes | project «Общие» |
| Sub-area | chevron if children, badge, «Область», name | no | its page under the project's menu |
| Deactivated project / its sub-areas | type, plain-text name, `Tag` «деактивирован»; project: no key, no count | no | nothing |
| Team | «Команда», name (link), «N проектов» | yes | team page |
| Section header | chevron, title, count | no | folds/unfolds |

### 4.6 Lines

| Line | Style |
|---|---|
| Nesting | 1 px solid grey elbow; **dotted** across hidden levels |
| Relation (project–team, department–team, same style: BR-SD-7) | 1.5 px dashed primary curve with a dot at the hierarchy end |
| Relation through a hidden node (collapsed branch or hidden level) | dotted, ends at the nearest visible ancestor |
| Emphasised relation | 3 px; the others fade (see modes) |

Constants (named in `lib/diagramLayout.ts`): horizontal `ROW 40`, `INDENT 22`, `HIERARCHY_WIDTH 480`, `LANE_GAP 200`, `TEAM_WIDTH 220`; vertical `NODE 156×58`, `SLOT_GAP 12`, `BAND 88`, `TEAM_ROW_GAP 70`; `RELATION_LINE_LIMIT 150`, `VERTICAL_NOTE_LIMIT 120`. Initial expansion: `initialExpansion` (D-3). Rendering: absolutely positioned HTML nodes (`Box`), one `<svg aria-hidden>` layer behind; the canvas is the layout's width and height.

### 4.7 Choosing and the display mode (R1-3)

Click on the body or Space chooses, again/`Esc`/«Сбросить» clears; the name opens; a chevron toggles and chooses nothing. Emphasis per the US-3 table; the summary is an `InlineNotice` info with «Сбросить» above the frame (`role="status"`, full lists scroll inside it). **«Показ при выборе»** (`MenuButton` radio) decides what choosing does to the rest:

| Mode | Effect | Layout rebuilt |
|---|---|---|
| **«Затемнение»** (default) | chosen and related emphasised, the rest at 50 %, a project's sub-areas stay, other lines fade | no |
| **«Только связанные»** | only the chosen node, what it relates to, their ancestors (forced open) and sub-areas of a related project; unrelated rows and teams removed, the lanes close up; one more summary line «Показаны только выбранный узел и связанные с ним.» | yes, on choose and clear |
| **«Подсветка»** | nothing dimmed or hidden; related nodes get a stronger accent frame, related lines 3 px accent, other lines grey | no |

The choice applies at once and to later choices. A «focus» mode pulling related teams beside the chosen node is not proposed (teams already stand near their anchors). Choosing also unfolds the branches and section that hide emphasised nodes.

### 4.8 Large organization (OQ-6) and convenience

- **Lines:** up to **150** relation lines (data pairs, independent of expansion) all drawn; above, none until a node is chosen, then only its lines, with the note «Линии связей показаны для выбранного узла». The prototype's large organization has 192. Limit 150: about one line per 4 px of lane edge in a 560 px frame, past that a blur; adjustable at the stage-6 stand.
- ~1 000 positioned elements and at most ~150 paths: no virtualization planned; if the 2 s measure fails at stage 6, virtualizing rows outside the frame is a layout-only fix.
- **Convenience in scope:** «Найти узел» marks matches (ring), Enter jumps to the next (unfolds section and branches, focuses, scrolls), case and «ё/е» ignored, filters nothing (BR-SD-8); «Развернуть всё»/«Свернуть всё»; counters «Департаментов · Проектов · Областей · Команд» (all nodes). **Follow-ups:** hover preview of relations (needs a keyboard equivalent), zoom and minimap (not cheap with approach A: scaling blurs text and breaks 640 px), drag and drop (feat-050), people (feat-051); none are designed here.

## 5. Keyboard and screen reader (BR-SD-10, OQ-5 A)

- **One Tab stop for the tree**: `role="tree"` named «Диаграмма структуры», roving `tabIndex`. Names, chevrons, team links inside have `tabIndex -1`. The toolbar controls and the levels group are separate stops before it.
- **Order:** group header, its teams, hierarchy header, hierarchy in pre-order (**in both layouts**), the lane's teams. In the vertical layout the arrow order is still pre-order, not left to right.
- **Levels (`aria-level`):** section headers 1; everything inside a section 2 for teams, **true level + 1** for hierarchy nodes (the badge shows the true level; ruler chips and badges are decoration for sighted users, `aria-hidden` badges).
- Keys: `↑`/`↓` previous/next; `Home`/`End`; `→` unfolds a section or branch, or on an open one goes to its first child; `←` folds, or goes to the parent (a node to its nearest visible ancestor, a team to its section header); `Space` chooses (header: toggles; non-choosable: announces «Этот узел выбрать нельзя»); `Enter` opens the page (header: toggles; deactivated: announces «Узел деактивированного проекта ничего не открывает»); `Esc` clears the choice. After a rebuild the same item keeps focus by id.
- Roles: `treeitem`, `aria-expanded` on sections and branches, `aria-selected` on choosable items, `aria-label` as in § 7; focus ring 2 px primary always. A summary or fold announces through `role="status"`/`LiveRegion`.
- The tree view is the text alternative of the hierarchy.

## 6. States

| State | The user sees (diagram) |
|---|---|
| Tree loading / failed / 404 | `RegistrySkeleton` / `ErrorBanner` «Повторить» (every failed read) / `NotFound`, no tabs, no create button; never teams alone |
| Offers loading | hierarchy without teams, keys, counts; warning «Команды и ключи проектов не загружены» |
| Offers failed | the same with «Повторить» (offers only; focus back to it if it fails again); the hierarchy stays |
| Empty org | `StateFrame` «В организации пока нет областей» (tree's), create only for `area.create` |
| Only projects | projects at the top level, no empty frame |
| Deactivated project | tagged, plain text, no lines, not choosable, no count |
| Team with no relation | in the top group; choosing: «Проекты команды: нет. Департаменты команды: нет» |
| Reads disagree | project without offer: no key, no lines; offer without node ignored; a team whose projects have no node and no department lands in the group |
| Many lines, nothing chosen | the note of § 4.8 |
| Level choice hides everything | «Нет узлов на выбранных уровнях» in the hierarchy section (teams stay) |
| Group or hierarchy section folded | header only; count stays |
| «Только связанные», group team chosen | «Нет связанных проектов и департаментов» |
| Vertical, over 120 visible nodes | the note of § 4.3 |
| Reader / member | same diagram, no «Создать департамент», no other command for any role |
| Narrow (640 px) | the frame scrolls both ways; head and toolbar wrap |
| After a write in the tree | both reads refetch; the diagram redraws without a reload |

## 7. Interface copy (final, Russian)

| Place | Text |
|---|---|
| Tablist / tabs / tree | «Вид структуры» / «Дерево», «Диаграмма» / «Диаграмма структуры» |
| Legend | «вложенность», «команда работает», «связь через скрытый узел» |
| Sections | «Без проектов и департаментов», «Департаменты, проекты и области»; count after the title |
| Toolbar | «Найти узел»; «Найдено: N. Enter — к следующему» / «Ничего не найдено»; «Развернуть всё» / «Свернуть всё»; counters «Департаментов: N · Проектов: N · Областей: N · Команд: N» |
| Display mode menu | «Показ при выборе: <режим>»; «Затемнение» (hint «Остальное приглушается»), «Только связанные» («Показаны выбранный узел и связанные с ним»), «Подсветка» («Всё на месте, связанное выделено цветом») |
| Layout menu | «Раскладка: <значение>»; «Горизонтально» («Иерархия слева направо, команды справа»), «Вертикально» («Департаменты сверху, команды снизу») |
| Levels | group «Уровни вложенности»; chips «Уровень N»; menu «Режим уровней: <значение>»; «Только выбранные уровни» («Показаны отмеченные уровни»), «Уровень и вложенные» («Самый верхний из отмеченных и все более глубокие»); «Все уровни»; announcements «Показаны уровни: 2, 3», «… и более глубокие», «Показаны все уровни»; «Нет узлов на выбранных уровнях» |
| Teams caption | «Команды» |
| Counts | «N команда / команды / команд», «N проект / проекта / проектов» |
| Chevron / section | «Свернуть: <имя>» / «Развернуть: <имя>»; announced «<раздел>: развёрнуто» / «<раздел>: свёрнуто» |
| Summary | «Команды департамента: A, B» / «…: нет»; «Команды проекта: …»; «Проекты команды: A, B. Департаменты команды: C» (each «нет» when empty); «Показаны только выбранный узел и связанные с ним.» |
| Clear | «Сбросить»; announced «Выбор сброшен» |
| Notes | «Линии связей показаны для выбранного узла»; «Команды и ключи проектов не загружены» + «Повторить»; «Нет связанных проектов и департаментов»; «Вертикальная раскладка широкая: показано узлов — N. Сверните ветви, выберите уровни или переключитесь на горизонтальную.» |
| Toasts | «Узел скрыт режимом «Только связанные»», «Узел скрыт выбором уровней» |
| Not choosable / not opening | «Этот узел выбрать нельзя» / «Узел деактивированного проекта ничего не открывает» |
| Hint under frame | «Tab входит в диаграмму один раз. Стрелки — переход, Пробел — выбрать, Enter — открыть, Esc — сбросить.» |
| Node names | «Департамент Логистика, команд: 1» · «Проект Складской портал (WH), в департаменте Логистика, команд: 2» (top: «на верхнем уровне»; deactivated: «, деактивирован», no count) · «Область Приёмка, в «Склад»» · «Команда MARS, проектов: 2, департаментов: 0» (group: «, без проектов и департаментов») · «Раздел «<название>», развёрнут/свёрнут, команд/узлов: N» |
| Reused | «Структура», «Создать департамент», «деактивирован», «В организации пока нет областей», «Повторить», `areaTexts.ts` |

New strings go into `areaTexts.ts`, not inline.

## 8. Diagram approach (OQ-7)

| | A: SVG + HTML nodes in `features/organizations` | B: part in `shared/ui` | C: layout library |
|---|---|---|---|
| New dependency | none | none | yes: the human's yes, probably an ADR |
| Kit work | none | component, test, showcase, guide, whole-suite verify | as A plus bundle/licence review |
| Fit | two small pure layouts (rows + lane; tidy tree + row), unit-tested without a DOM | generic API before a second user | solves node-link graphs; this is lanes |
| Accessibility | native tree, roving focus like `TreeRegistry` | same behind an API | libraries draw their own canvas; tree role rebuilt on top |
| Size | stays M | may become L | probably L |

**Recommendation (decided): A.** Files: `components/StructureDiagram.tsx` (frame, tree role, keys), `DiagramNode.tsx`, `DiagramToolbar.tsx`, `lib/diagramModel.ts`, `lib/diagramLayout.ts`, `hooks/useStructureSettings.ts`. Tests: model and layouts as pure unit tests (both orientations, level modes, nearest-visible-ancestor); page tests for the switch, the three keys, states, the one Tab stop, "only two reads", and "choosing does not rebuild the layout" in «Затемнение»/«Подсветка» (spy). Promote to `shared/ui` when a second page needs it.

## 9. Recommended answers (all decided A at gate-1+2; threshold 150)

OQ-1 A deactivated project tagged, no lines · OQ-2 A browser storage (no address change) · OQ-3 A «Создать департамент» in both views · OQ-4 A the group, now on top · OQ-5 A name opens, body chooses, one Tab stop · OQ-6 A, 150 lines · OQ-7 A, approach A.

## 10. Flags for the orchestrator (no spec edit made)

1. **Kit deviation:** the switch uses `BoxedTabs` (decided); its docblock gets a sentence.
2. `src/app/navigation/*` and `routeTree.tsx`: not needed. No new read or endpoint.
3. Spec detail: BR-SD-10's `tree` role has no row for sections; sections are level-1 items, teams level 2, hierarchy nodes true level + 1 (§ 5).
4. The diagram's expansion, level choice, folds and search are page state for the visit; the spec spoke only of the tree's expansion.
5. «Только связанные» and the vertical layout rebuild the layout on a choice/switch; the "no layout rebuild on choosing" test is for «Затемнение» and «Подсветка».
6. Size check: three stored keys, two layouts, levels and sections all fit US-1..US-3; if the analyst's deltas need a fourth story, the levels filter (§ 4.4) and the vertical layout (§ 4.3) are the two separable cuts.

## 11. Prototype

`prototype/index.html`. Scenario strip: normal organization (deactivated project, sub-areas, no-project group), large organization, offers loading, offers failed with «Повторить» (second press works), loading, tree failed, empty, not found; role switch; 640 px window. In the diagram: both layouts, level chips with both modes and «Все уровни», section folds, the three display modes, search, expand/collapse all, choosing a project or team in both layouts, keyboard model. MUI parts are drawn by hand; layout, constants and copy are the design's.

## 12. Rework 1 (gate-1+2 remarks, folded in)

R1-1 group to the top (§ 4.1); R1-2 level marks (badges, chips, guides, § 4.4); R1-3 display modes (§ 4.7); R1-4 convenience: search, expand/collapse all and counters in scope, hover preview, zoom/minimap as follow-ups (§ 4.8).

## 13. Rework 2 (gate-1+2, second rejection)

| Remark | Answer |
|---|---|
| R2-1 clickable ruler, level modes | § 4.4: toggle chips, mode menu «Только выбранные уровни» / «Уровень и вложенные», «Все уровни»; hidden rows drop, children join the nearest visible ancestor by a dotted join; lines to the nearest visible ancestor; teams stay; not stored, kept for the visit |
| R2-2 collapsible sections | § 4.1: both section headers are disclosures; the teams lane folds with the hierarchy; choosing unfolds what it needs |
| R2-3 vertical layout | § 4.3: rows are levels, tidy top-down tree, group on top, teams in a row below, wide-org note above 120 nodes; switch «Раскладка», own stored key; still one small pure layout function |
| feat-050 (drag and drop), feat-051 (people) | queued; not designed; listed under follow-ups |

### Spec deltas for the analyst (rounds 1 and 2)

- **US-1:** the diagram has its own toolbar (search, expand/collapse all, display mode, layout, counters); the tree's filter and head command stay tree-only. Three stored keys (view, mode, layout) with fallback to the defaults and no error; BR-SD-9 names them. «What survives a switch»: the diagram's search, folds, level choice, expansion and settings survive, the chosen node does not. Level choice, folds and search are explicitly not stored.
- **US-2:** (a) two sections with disclosure headers; the top group «Без проектов и департаментов» stands before the hierarchy (replaces "after the hierarchy"); folding the hierarchy folds the teams lane too; choosing unfolds a section that holds emphasised nodes. (b) Levels: toggle chips (one per level of the tree), modes «Только выбранные уровни» / «Уровень и вложенные», «Все уровни», rows of hidden levels dropped, nearest-visible-ancestor joins (dotted), relation lines to the nearest visible ancestor, teams never hidden, not stored, announcements, group «Уровни вложенности». (c) Layouts: «Горизонтально»/«Вертикально», rows as levels, teams row below, same keyboard order, note above 120 visible nodes. (d) «Найти узел», diagram «Развернуть всё»/«Свернуть всё», counters over all nodes. (e) Accessible levels: section 1, teams 2, nodes true level + 1; badge shows the true level.
- **US-3:** emphasis, clearing and many-relations criteria per display mode («Затемнение», «Только связанные», «Подсветка»); the mode control and its default; "Volume": the no-rebuild check covers «Затемнение»/«Подсветка», «Только связанные» and vertical layout switches get their own 200 ms criterion at 200 × 200; a group team chosen in «Только связанные».
- **BR-SD-8:** the diagram's search never filters. **BR-SD-10:** section headers are tree items (toggle on Space/Enter). **BR-SD-11:** «Только связанные» forces ancestors open without changing the expansion; the level filter applies after expansion and after «Только связанные».
- **Out of scope** gains: drag and drop (feat-050), people (feat-051), hover preview, zoom/minimap.
- **Non-functional:** no new read; timings unchanged.
