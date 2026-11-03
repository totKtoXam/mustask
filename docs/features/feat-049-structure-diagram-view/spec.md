# Spec: «Структура» diagram view (feat-049)

## Context and goal

«Структура» shows the organization's departments, projects and sub-areas only as a tree table. The tree cannot answer «which teams work on this project» or «which projects does this team work on» without opening each object's page. At feat-043 gate-3 (2026-11-02) the human asked for a second, diagrammatic view of the same page that shows the hierarchy and the team–project relations at a glance. The goal: a «Дерево» / «Диаграмма» switch on «Структура», the diagram being read-only and built from data the panel already reads.

## Roles

The page is read by every organization role («Администратор», «Участник», «Читатель»); both reads it uses declare the organization read code (feat-043 api-contract § 0; mod-018 area tree). Writes stay where they are today: the tree's row menu follows `area.write` and exists in the tree view only (BR-SD-5); the page-level «Создать департамент» follows `area.create` and stands in both views (OQ-3 A). The diagram itself writes nothing.

## Stories

| id | Title | depends-on | File |
|----|-------|------------|------|
| US-1 | Switch between the tree and the diagram, the choices remembered | - | `stories/US-1-view-switch.md` |
| US-2 | The diagram tree: sections, team cards, levels, layouts, search, expand all, counters | US-1 | `stories/US-2-hierarchy-diagram.md` |
| US-3 | Team–project relations in both directions, branch emphasis, three display modes | US-2 | `stories/US-3-team-project-relations.md` |

## Business process (BPMN)

Not drawn: the flow is a linear switch with no branching process (M, optional).

## Business rules

| # | Rule | Source |
|---|------|--------|
| BR-SD-1 | **Same page, same address.** The switch changes how «Структура» is drawn, not where it lives; no new route, no menu entry. | task, scope |
| BR-SD-2 | **The tree stays the default.** A user who never chose sees the tree, exactly as today. | feat-043 behaviour kept |
| BR-SD-3 | **No new read.** The diagram is built from the area tree (`GET …/areas`) and the selector offers (`GET …/selector-offers`) that the panel already has; the team–project links come from `projects[].teamIds`, a team's direct departments from `teams[].departmentIds`, a project's key from `projects[].key`. A project node of the tree is matched to its offer by `AreaNode.projectId = projects[].id`. | queue line; feat-043 api-contract § 2 |
| BR-SD-4 | **Deactivated projects.** The tree shows the node of a deactivated project tagged «деактивирован», plain text, without commands (feat-043 OQ-3 A). The diagram shows the node and its sub-areas the same way: tagged, not a link. The selector offers hold no deactivated project, so such a project is drawn with no team lines and no claim about its teams (OQ-1 A). | AreasPage; api-contract § 2 |
| BR-SD-5 | **Read-only diagram.** No create, rename, move, delete or link command in the diagram; those are done in the tree view or on the object's page. | scope |
| BR-SD-6 | **Same links as the tree.** A department and a project open their own page, a sub-area opens its page under its project's menu, a team (any copy) opens its page; deactivated project nodes and their sub-areas open nothing. | AreasPage `rowLink` |
| BR-SD-7 | Superseded by BR-SD-16 (there is no department–team line any more); the number is kept so that references stay valid. | gate-1+2 round 3 |
| BR-SD-8 | **Search never filters.** The tree's name filter does not apply to the diagram. The diagram has its own search «Найти узел», which matches names only, marks matches and jumps to them (unfolding sections and branches; a team jumps to its first copy), and never removes or hides a node or changes the level choice; its emphasis of a chosen node is US-3. | scope; design § 12.2 |
| BR-SD-10 | **Gesture model.** Opening and choosing are different gestures. Opening: a click on the node's name (a link) or Enter on the focused node. Choosing (emphasis, US-3): a click on the node's body or Space. Choosable: department, active project, team (any copy); sub-areas and deactivated nodes are not. The diagram is one Tab stop with arrow keys inside, like the feat-043 tree (OQ-5 A); the toolbar controls (each button of the three toggles, «Все уровни», the zoom buttons) and the level chips are separate stops before it. The tree role has these levels: a section header 1, every card its true nesting level + 1 (a team under a department 3, a team in «Без департамента» 2). Section headers are items too: Enter/Space fold them. A team's copies are separate items in the arrow order. | review R-1; feat-043 design § 5.2; gate-1+2 round 3 |
| BR-SD-11 | **Expansion and density.** Initial expansion follows the tree's D-3 (`TREE_EXPAND_ALL_LIMIT`); the diagram keeps its own expansion, separate from the tree's. A relation line into a collapsed branch ends at the nearest visible ancestor. In the modes «Затемнение» and «Подсветка» choosing a node expands the branches that hide what it emphasises, the departments of a team's copies included; in «Только связанные» the ancestors of the shown cards are forced open for the time of the choice without changing the user's expansion. **Order of narrowing:** expansion first, then «Только связанные», then the level choice (BR-SD-14); a folded section that holds emphasised or found cards is unfolded by choosing or finding, like a branch. Section folds are page state for the visit, as is the diagram's expansion. Lines at volume: BR-SD-17. | review R-2; design § 12 |
| BR-SD-9 | **Remembered choices** are three: the view, the display mode (US-3) and the layout (US-2). Each is per user and organization, in the browser, like the selection of feat-043 (ADR-0032 § 6), under a key of its own: never a field added to the selection's `mustask.context.v2` value, whose shape ADR-0032 locks. A value outside the allowed set, or absent or refusing storage, is ignored without an error: the tree, «Затемнение», «Горизонтально». Written only on the user's own choice. **Not stored, by decision:** the level choice, the section folds, the search text and the zoom — filters and viewing state of one visit, a stored one would reopen the page with nodes missing or magnified. They, with the diagram's expansion, survive a view switch, are dropped by a reload or an organization change; zoom also survives a layout switch; the chosen node is dropped on any switch. | ADR-0032; gate-1+2 rounds 2, 4 |
| BR-SD-12 | **Levels are text and colour.** Nesting levels are marked by a number at the start of each card and by clickable level chips as a ruler, and each level has its own colour, worn by the card itself (a left edge, an outline and a light tint of its fill), by its chip and by its badge. A level chip also carries a hover and focus tooltip that says what its level holds and what a click does. Colour never stands alone: the number stays. The number is the card's true nesting level: department 1, its project or team 2, a sub-area 3; a top-level project and a team in «Без департамента» 1. A screen reader announces the level plus 1 inside the tree (BR-SD-10); chips and numbers are not read a second time. **Contrast:** every level outline and edge colour is at least 3:1 against the diagram background; the text of a level badge or chip is at least 4.5:1 against its own fill; the text of a card is at least 4.5:1 against the level tint it stands on, in every state (at rest, hover, related, chosen), levels 1 to 5 and deeper. Emphasis adds to the level colour and never replaces it; a deactivated card keeps its outline on a neutral fill. | gate-1+2 remarks 2-3, round 3, round 4 remark 2 |
| BR-SD-14 | **Level choice.** One chip per level of the whole tree, multi-select; no chip pressed means all levels. «Только выбранные уровни» (default) shows exactly the pressed levels; «Уровень и вложенные» shows the topmost pressed level and every deeper one. Cards of a hidden level drop, **team cards included** (a team under a department is level 2, a team in «Без департамента» level 1). A card whose parent is hidden is joined by a dotted join to its nearest visible ancestor, with none it stands unjoined. A relation line goes dotted to the nearest visible ancestor of its end, with none it is not drawn. Counts and summaries speak of all data. | gate-1+2 round 2 remark 1, round 3 |
| BR-SD-15 | **Layout.** «Горизонтально» (one lane, a card per row, indented by level) and «Вертикально» (levels as rows, a parent over its children) show the same cards, order, links, keyboard order, levels and lines; only placement differs. Above 120 visible cards the vertical layout shows a note and blocks nothing. | gate-1+2 round 2 remark 4 |
| BR-SD-13 | **Display mode.** Three modes say what choosing a node does to the rest: «Затемнение» (default), «Только связанные», «Подсветка» (US-3). In all three, choosing a department or a project emphasises its whole branch (BR-SD-18). All three show the same data; the mode changes only emphasis and, in «Только связанные», which cards are shown. | gate-1+2 remark 3, round 3 |
| BR-SD-16 | **Teams stand in the tree.** A team is a child of every department it is linked to directly (`departmentIds`), drawn as a copy under each, after that department's projects, every copy marked «ещё в: <остальные департаменты>»; copies are one team (choosing any emphasises all, counters count it once) and consecutive copies are joined by a dash-dot line. A team with no department link stands in «Без департамента», whether or not it works on projects. Only active teams are drawn (the offers carry no deactivated or deleted team). A department is still optional for a team (no model change). A link to a department missing in the tree is ignored: the team stands under its other departments, else in «Без департамента». | gate-1+2 round 3 |
| BR-SD-17 | **Lines and volume.** Lines: nesting (solid, dotted across hidden levels), a team works on a project (dashed), the same team in another department (dash-dot), a relation through a hidden card (dotted); the four are shown in a legend. The four styles are heavy and dark enough to be told apart at a glance (each stroke at least 3:1 against the diagram background), the legend sample and the line on the canvas are drawn in the same stroke, and they still differ by pattern. A project–team line uses the team's copy under the project's own department when there is one, else its first copy. The line count is the project–team pairs plus the copy joins, whatever is expanded or hidden; up to 150 all are drawn, above that none until a node is chosen, then only the chosen branch's (OQ-6 A). Lines differ by style and not by colour alone. | gate-1+2 round 3; OQ-6 A |
| BR-SD-18 | **Branch emphasis.** Choosing a department emphasises it, every project, sub-area and team under it (all their copies too), the project–team relations of that branch and the far ends of those relations. Choosing a project emphasises it, its sub-areas, the teams working on it (all copies) and those relations. Choosing a team emphasises all its copies, its projects with their lines and its departments. | gate-1+2 round 3 remark 5 |
| BR-SD-19 | **Toolbar toggles.** «Показ при выборе», «Раскладка» and «Режим уровней» are three segmented icon toggles (one value of the set at a time, every value visible as its own icon button, no menu). The group is named after the control; the accessible name of each button is its value; the tooltip reads «<управление>: <значение>. <подсказка>». The focus indicator is drawn around the focused button itself and nowhere else. «Режим уровней» stands in the toolbar beside «Раскладка». «Все уровни» is a text button right after it, disabled while no level chip is pressed. | gate-1+2 round 3 remark 2, round 4 remarks 1, 3, 6, 7 |
| BR-SD-20 | **Pan and zoom.** The picture is panned by dragging with the middle mouse button (the cursor changes while dragging) and zoomed from 50 % to 200 % with Ctrl + wheel around the pointer, without zooming the browser page; the keyboard control «− 100% +» does the same by buttons, and its value button returns the zoom to 100 %. Zoom starts at 100 %, is not stored (BR-SD-9) and changes only how big the picture is drawn: no card, line, level or emphasis changes. Pan and zoom sit beside the frame's scrollbars, which stay. | gate-1+2 round 4 remark 5 |

## Operation completeness

| Entity | Create | Read | Update | Delete/deactivate | Cancel/reverse |
|--------|--------|------|--------|-------------------|----------------|
| View choice (tree / diagram), display mode, layout | no: browser preferences, made by choosing | every role | every role (choose the other value) | no: not a stored object; a cleared browser returns the defaults | no: choose the other value again |
| Level choice, section folds, search text, zoom | no: page state of a visit, not stored | every role | every role | no: dropped by reload (zoom: also by the value button, back to 100 %) or «Все уровни» | no: «Все уровни», unfold, the zoom value button |
| Department, project, sub-area, team card (as drawn) | no: the diagram creates nothing (BR-SD-5) | every role | no: tree view / object page | no: tree view / object page | no: none |
| Team–project link (as drawn) | no: object pages and area dialogs | every role | no: same | no: same | no: same |

## Non-functional requirements

- **Volume:** up to 1 000 areas, 200 projects, 200 teams (feat-043 volumes): the diagram appears within 2 s of both reads answering, in either layout; choosing a node changes the picture within 200 ms in every display mode and in both layouts, and a layout switch or a level change within 200 ms; zoom and pan stay smooth at 200 projects × 200 teams: a zoom step or a pan movement changes the picture within 200 ms and, being a transform of the drawn picture, rebuilds no layout and sends no read; all timings are measured on the stand with a seeded organization of that size in a named browser at stage 6, not in jsdom. Tests check what is stable: no read beyond the two, no layout rebuild on choosing in «Затемнение» and «Подсветка» («Только связанные», a layout switch and a level change rebuild by nature).
- **Accessibility:** zoom and pan have a keyboard way (the «− 100% +» buttons; the frame's scrolling); the diagram is reachable by keyboard (one Tab stop, arrow keys inside, a visible focus), is announced as a structure with levels (tree role), and has a text alternative: one accessible name per node (type, name, parent, relation count for a project or team), the full lists in the announced summary of US-3; the tree view is the text alternative for the hierarchy. It works down to 640 CSS px (ADR-0024 § 5), scrolling inside its own frame when wider. Meaning never rides on colour alone (a deactivated node carries the tag; levels carry numbers; line kinds differ by style). Contrast: BR-SD-12.
- **Not a security control:** nothing here widens what a role reads.

## Out of scope

- Writing from the diagram: drag to move, drag to link, any dialog (BR-SD-5).
- Teams linked to sub-areas below a department, if the data offers them (the offers carry department links only).
- Making a department mandatory for a team: not wanted (round 3 answer).
- Drag and drop and drawn arrows to move elements into each other: queued as feat-050.
- Showing users (avatar, full name, email): queued as feat-051.
- Export (image, PDF), print layout, minimap, zoom persistence.
- Follow-ups proposed at design and not taken: hover preview of relations without choosing, a «focus» mode that pulls related teams next to the chosen node.
- Showing work items or members on nodes (the counters line and per-node team counts are in scope).
- App navigation, the route table, a diagram on any other page.
- A new read or endpoint, a backend change. The diagram is built inside the organizations feature without a new dependency; a shared part is the open OQ-7.

## Decomposition for the backlog

One task, as queued; the three stories are one capability and none ships alone (no capability left unused).

| id | Task | size | deps |
|----|------|------|------|
| feat-NNN-structure-diagram-view | The switch, the hierarchy diagram (sections, levels, two layouts) and the team–project relations on «Структура» (frontend only) | M | feat-043 (merged) |

## Open questions

One, open, for the human at this gate: **OQ-7**. OQ-1..OQ-6 were answered A by the human at gate-1+2 (2026-11-03; OQ-6 threshold 150) and are folded in above; OQ-7 was answered A in round 1 and is reopened by round 4:

| OQ | Resolved as | Where |
|----|-------------|-------|
| OQ-1 | Deactivated project drawn tagged, no team lines | BR-SD-4, US-2, US-3 |
| OQ-2 | Browser storage per user and organization, address plain | BR-SD-9, US-1 |
| OQ-3 | «Создать департамент» in both views for `area.create` | Roles, US-1 |
| OQ-4 | Teams with no department, and top-level projects, in the section «Без департамента» on top (round 3; it replaced «Без проектов и департаментов») | BR-SD-16, US-2, US-3 |
| OQ-5 | Name opens, body chooses, one Tab stop | BR-SD-10, US-2 |
| OQ-6 | All lines up to 150, above that the chosen branch's only | BR-SD-17, US-3 |
| OQ-7 | **Open.** Answer A of round 1 said «no new shared part». Round 4 needs one: the segmented icon toggle (`IconToggleGroup` in the shared UI kit), because the kit rules bar the raw toggle-group part in features (KIT-2). Options: A) add that small shared part (the design's proposal, size stays M); B) build the toggles inside the organizations feature from kit buttons, which keeps the earlier answer but breaks KIT-2 or duplicates the part. Awaiting the human; not decided here. | Out of scope; US-2, US-3 |
