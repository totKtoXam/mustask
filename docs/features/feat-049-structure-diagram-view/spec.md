# Spec: «Структура» diagram view (feat-049)

## Context and goal

«Структура» shows the organization's departments, projects and sub-areas only as a tree table. The tree cannot answer «which teams work on this project» or «which projects does this team work on» without opening each object's page. At feat-043 gate-3 (2026-11-02) the human asked for a second, diagrammatic view of the same page that shows the hierarchy and the team–project relations at a glance. The goal: a «Дерево» / «Диаграмма» switch on «Структура», the diagram being read-only and built from data the panel already reads.

## Roles

The page is read by every organization role («Администратор», «Участник», «Читатель»); both reads it uses declare the organization read code (feat-043 api-contract § 0; mod-018 area tree). Writes stay where they are today: the tree's row menu follows `area.write` and exists in the tree view only (BR-SD-5); the page-level «Создать департамент» follows `area.create` and stands in both views (OQ-3 A). The diagram itself writes nothing.

## Stories

| id | Title | depends-on | File |
|----|-------|------------|------|
| US-1 | Switch between the tree and the diagram, the choices remembered | - | `stories/US-1-view-switch.md` |
| US-2 | The hierarchy diagram: sections, levels, layouts, search, expand all, counters | US-1 | `stories/US-2-hierarchy-diagram.md` |
| US-3 | Team–project relations in both directions, three display modes | US-2 | `stories/US-3-team-project-relations.md` |

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
| BR-SD-6 | **Same links as the tree.** A department and a project open their own page, a sub-area opens its page under its project's menu, a team opens its page; deactivated project nodes and their sub-areas open nothing. | AreasPage `rowLink` |
| BR-SD-7 | **Which teams count.** Only active teams (the offers carry no deactivated or deleted team). A team's departments are the ones linked to it directly (`departmentIds`); a department→team line is drawn for those only. A team working on a project is not by that fact drawn under the project's department. | api-contract § 2 |
| BR-SD-8 | **Search never filters.** The tree's name filter does not apply to the diagram. The diagram has its own search «Найти узел», which only marks matches and jumps to them (unfolding sections and branches), and never removes or hides a node or changes the level choice; its emphasis of a chosen node is US-3. | scope; design § 12.2 |
| BR-SD-10 | **Gesture model.** Opening and choosing are different gestures. Opening: a click on the node's name (a link) or Enter on the focused node. Choosing (emphasis, US-3): a click on the node's body or Space. Choosable: department, active project, team; sub-areas and deactivated nodes are not. The diagram is one Tab stop with arrow keys inside, like the feat-043 tree (OQ-5 A); the toolbar and the level controls are separate stops before it. The tree role has these levels: a section header 1, a team 2, a hierarchy node its true nesting level + 1 (section headers are items too: Enter/Space fold them). |  review R-1; feat-043 design § 5.2 |
| BR-SD-11 | **Expansion and density.** Initial expansion follows the tree's D-3 (`TREE_EXPAND_ALL_LIMIT`); the diagram keeps its own expansion, separate from the tree's. A relation line into a collapsed branch ends at the nearest visible ancestor. In the modes «Затемнение» and «Подсветка» choosing a node expands the branches that hide what it emphasises; in «Только связанные» the ancestors of the shown nodes are forced open for the time of the choice without changing the user's expansion. Relation lines at volume: all of them up to 150 relation lines (a data count, independent of expansion), above that only the chosen node's (OQ-6 A). **Order of narrowing:** expansion first, then «Только связанные», then the level choice (BR-SD-14); a folded section that holds emphasised or found nodes is unfolded by choosing or finding, like a branch. Section folds are page state for the visit, as is the diagram's expansion. | review R-2; design § 12 |
| BR-SD-9 | **Remembered choices** are three: the view, the display mode (US-3) and the layout (US-2). Each is per user and organization, in the browser, like the selection of feat-043 (ADR-0032 § 6), under a key of its own: never a field added to the selection's `mustask.context.v2` value, whose shape ADR-0032 locks. A value outside the allowed set, or absent or refusing storage, is ignored without an error: the tree, «Затемнение», «Горизонтально». Written only on the user's own choice. **Not stored, by decision:** the level choice, the section folds and the search text — filters of one visit, a stored one would reopen the page with nodes missing. They, with the diagram's expansion, survive a view switch, are dropped by a reload or an organization change; the chosen node is dropped on any switch. | ADR-0032; gate-1+2 round 2 |
| BR-SD-12 | **Levels are text.** Nesting levels are marked in the picture by text (clickable level chips as a ruler and a number at the start of each hierarchy node), never by colour alone. A node's number is its true nesting level; a screen reader announces it plus 1 inside the tree (BR-SD-10), and chips and numbers are not read a second time. Teams carry no level number. | gate-1+2 remark 2 |
| BR-SD-14 | **Level choice.** One chip per level of the whole tree, multi-select; no chip pressed means all levels. «Только выбранные уровни» (default) shows exactly the pressed levels; «Уровень и вложенные» shows the topmost pressed level and every deeper one. Rows of a hidden level drop; a node whose parent is hidden is joined by a dotted join to its nearest visible ancestor, with none it stands unjoined. **Teams are never hidden by levels**; a relation line goes dotted to the nearest visible ancestor of its node, with none it is not drawn. Counts and summaries speak of all data. | gate-1+2 round 2 remark 1 |
| BR-SD-15 | **Layout.** «Горизонтально» (hierarchy left, teams right) and «Вертикально» (levels as rows, teams in a row below) show the same data, order, links, keyboard order and levels; only placement differs. Above 120 visible nodes the vertical layout shows a note and blocks nothing. | gate-1+2 round 2 remark 4 |
| BR-SD-13 | **Display mode.** Three modes say what choosing a node does to the rest: «Затемнение» (default), «Только связанные», «Подсветка» (US-3). All three show the same data; the mode changes only emphasis and, in «Только связанные», which nodes are shown. | gate-1+2 remark 3 |

## Operation completeness

| Entity | Create | Read | Update | Delete/deactivate | Cancel/reverse |
|--------|--------|------|--------|-------------------|----------------|
| View choice (tree / diagram), display mode, layout | no: browser preferences, made by choosing | every role | every role (choose the other value) | no: not a stored object; a cleared browser returns the defaults | no: choose the other value again |
| Level choice, section folds, search text | no: page state of a visit, not stored | every role | every role | no: dropped by reload or «Все уровни» | no: «Все уровни», unfold |
| Department, project, sub-area, team (as drawn) | no: the diagram creates nothing (BR-SD-5) | every role | no: tree view / object page | no: tree view / object page | no: none |
| Team–project link (as drawn) | no: object pages and area dialogs | every role | no: same | no: same | no: same |

## Non-functional requirements

- **Volume:** up to 1 000 areas, 200 projects, 200 teams (feat-043 volumes): the diagram appears within 2 s of both reads answering, in either layout; choosing a node changes the picture within 200 ms in every display mode and in both layouts, and a layout switch or a level change within 200 ms; all timings are measured on the stand with a seeded organization of that size in a named browser at stage 6, not in jsdom. Tests check what is stable: no read beyond the two, no layout rebuild on choosing in «Затемнение» and «Подсветка» («Только связанные», a layout switch and a level change rebuild by nature).
- **Accessibility:** the diagram is reachable by keyboard (one Tab stop, arrow keys inside, a visible focus), is announced as a structure with levels (tree role), and has a text alternative: one accessible name per node (type, name, parent, relation count for a project or team), the full lists in the announced summary of US-3; the tree view is the text alternative for the hierarchy. It works down to 640 CSS px (ADR-0024 § 5), scrolling inside its own frame when wider. Meaning never rides on colour alone (a deactivated node carries the tag).
- **Not a security control:** nothing here widens what a role reads.

## Out of scope

- Writing from the diagram: drag to move, drag to link, any dialog (BR-SD-5).
- Teams linked to sub-areas below a department, if the data offers them (the offers carry department links only).
- Drag and drop and drawn arrows to move elements into each other: queued as feat-050.
- Showing users (avatar, full name, email): queued as feat-051.
- Export (image, PDF), print layout, zoom and minimap, zoom persistence.
- Follow-ups proposed at design and not taken: hover preview of relations without choosing, a «focus» mode that pulls related teams next to the chosen node.
- Showing work items or members on nodes (the counters line and per-node team counts are in scope).
- App navigation, the route table, a diagram on any other page.
- A new read or endpoint, a backend change. The diagram is built inside the organizations feature without a new dependency or a new shared part (OQ-7 A).

## Decomposition for the backlog

One task, as queued; the three stories are one capability and none ships alone (no capability left unused).

| id | Task | size | deps |
|----|------|------|------|
| feat-NNN-structure-diagram-view | The switch, the hierarchy diagram (sections, levels, two layouts) and the team–project relations on «Структура» (frontend only) | M | feat-043 (merged) |

## Open questions

None. OQ-1..OQ-7 were answered A by the human at gate-1+2 (2026-11-03; OQ-6 threshold 150) and are folded in above:

| OQ | Resolved as | Where |
|----|-------------|-------|
| OQ-1 | Deactivated project drawn tagged, no team lines | BR-SD-4, US-2, US-3 |
| OQ-2 | Browser storage per user and organization, address plain | BR-SD-9, US-1 |
| OQ-3 | «Создать департамент» in both views for `area.create` | Roles, US-1 |
| OQ-4 | Teams with no project and no department in the group «Без проектов и департаментов», now at the top (remark 1) | US-2, US-3 |
| OQ-5 | Name opens, body chooses, one Tab stop | BR-SD-10, US-2 |
| OQ-6 | All lines up to 150, above that the chosen node's only | BR-SD-11, US-3 |
| OQ-7 | Inside the organizations feature, no new dependency or shared part | Out of scope |
