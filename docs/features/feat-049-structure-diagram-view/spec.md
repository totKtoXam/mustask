# Spec: «Структура» diagram view (feat-049)

## Context and goal

«Структура» shows the organization's departments, projects and sub-areas only as a tree table. The tree cannot answer «which teams work on this project» or «which projects does this team work on» without opening each object's page. At feat-043 gate-3 (2026-11-02) the human asked for a second, diagrammatic view of the same page that shows the hierarchy and the team–project relations at a glance. The goal: a «Дерево» / «Диаграмма» switch on «Структура», the diagram being read-only and built from data the panel already reads.

## Roles

The page is read by every organization role («Администратор», «Участник», «Читатель»); both reads it uses declare the organization read code (feat-043 api-contract § 0; mod-018 area tree). Writes stay where they are today: the tree's row menu follows `area.write` and exists in the tree view only (BR-SD-5); the page-level «Создать департамент» follows `area.create`, and whether it also stands in the diagram view is OQ-3. The diagram itself writes nothing.

## Stories

| id | Title | depends-on | File |
|----|-------|------------|------|
| US-1 | Switch between the tree and the diagram, the choice remembered | - | `stories/US-1-view-switch.md` |
| US-2 | The hierarchy diagram: departments, projects, sub-areas | US-1 | `stories/US-2-hierarchy-diagram.md` |
| US-3 | Team–project relations in both directions | US-2 | `stories/US-3-team-project-relations.md` |

## Business process (BPMN)

Not drawn: the flow is a linear switch with no branching process (M, optional).

## Business rules

| # | Rule | Source |
|---|------|--------|
| BR-SD-1 | **Same page, same address.** The switch changes how «Структура» is drawn, not where it lives; no new route, no menu entry. | task, scope |
| BR-SD-2 | **The tree stays the default.** A user who never chose sees the tree, exactly as today. | feat-043 behaviour kept |
| BR-SD-3 | **No new read.** The diagram is built from the area tree (`GET …/areas`) and the selector offers (`GET …/selector-offers`) that the panel already has; the team–project links come from `projects[].teamIds`, a team's direct departments from `teams[].departmentIds`, a project's key from `projects[].key`. A project node of the tree is matched to its offer by `AreaNode.projectId = projects[].id`. | queue line; feat-043 api-contract § 2 |
| BR-SD-4 | **Deactivated projects.** The tree shows the node of a deactivated project tagged «деактивирован», plain text, without commands (feat-043 OQ-3 A). The diagram shows the node and its sub-areas the same way: tagged, not a link. The selector offers hold no deactivated project, so such a project shows no teams (it is not drawn as having none by choice: see OQ-1). | AreasPage; api-contract § 2 |
| BR-SD-5 | **Read-only diagram.** No create, rename, move, delete or link command in the diagram; those are done in the tree view or on the object's page. | scope |
| BR-SD-6 | **Same links as the tree.** A department and a project open their own page, a sub-area opens its page under its project's menu, a team opens its page; deactivated project nodes and their sub-areas open nothing. | AreasPage `rowLink` |
| BR-SD-7 | **Which teams count.** Only active teams (the offers carry no deactivated or deleted team). A team's departments are the ones linked to it directly (`departmentIds`); a department→team line is drawn for those only. A team working on a project is not by that fact drawn under the project's department. | api-contract § 2 |
| BR-SD-8 | **Search.** The tree's name filter does not apply to the diagram; the diagram has its own emphasis (US-3). | scope |
| BR-SD-10 | **Gesture model.** Opening and choosing are different gestures. Opening: a click on the node's name (a link) or Enter on the focused node. Choosing (emphasis, US-3): a click on the node's body or Space. Choosable: department, active project, team; sub-areas and deactivated nodes are not. The diagram is one Tab stop with arrow keys inside, like the feat-043 tree (alternative: OQ-5). | review R-1; feat-043 design § 5.2 |
| BR-SD-11 | **Expansion and density.** Initial expansion follows the tree's D-3 (`TREE_EXPAND_ALL_LIMIT`); a relation line into a collapsed branch ends at the nearest visible ancestor; choosing a node expands the branches that hide what it emphasises; team placement and order are left to design; relation lines at volume follow OQ-6. | review R-2 |
| BR-SD-9 | **Remembered choice** is per user and organization, in the browser, like the selection of feat-043 (ADR-0032 § 6), under a key of its own: never a field added to the selection's `mustask.context.v2` value, whose shape ADR-0032 locks; a value that is not one of the two views is ignored and the tree is shown. | ADR-0032 |

## Operation completeness

| Entity | Create | Read | Update | Delete/deactivate | Cancel/reverse |
|--------|--------|------|--------|-------------------|----------------|
| View choice (tree / diagram) | no: it is a browser preference, made by choosing | every role | every role (choose the other view) | no: not a stored object; a cleared browser returns the tree | no: choose the other view again |
| Department, project, sub-area, team (as drawn) | no: the diagram creates nothing (BR-SD-5) | every role | no: tree view / object page | no: tree view / object page | no: none |
| Team–project link (as drawn) | no: object pages and area dialogs | every role | no: same | no: same | no: same |

## Non-functional requirements

- **Volume:** up to 1 000 areas, 200 projects, 200 teams (feat-043 volumes): the diagram appears within 2 s of both reads answering, and choosing a node changes the emphasis within 200 ms; both timings are measured on the stand with a seeded organization of that size in a named browser at stage 6, not in jsdom. Tests check what is stable: no read beyond the two, no layout rebuild on choosing.
- **Accessibility:** the diagram is reachable by keyboard (one Tab stop, arrow keys inside, a visible focus), is announced as a structure with levels (tree role), and has a text alternative: one accessible name per node (type, name, parent, relation count for a project or team), the full lists in the announced summary of US-3; the tree view is the text alternative for the hierarchy. It works down to 640 CSS px (ADR-0024 § 5), scrolling inside its own frame when wider. Meaning never rides on colour alone (a deactivated node carries the tag).
- **Not a security control:** nothing here widens what a role reads.

## Out of scope

- Writing from the diagram: drag to move, drag to link, any dialog (BR-SD-5).
- Teams linked to sub-areas below a department, if the data offers them (the offers carry department links only).
- Export (image, PDF), print layout, zoom persistence.
- Showing work items, members or counts on nodes.
- App navigation, the route table, a diagram on any other page.
- A new read or endpoint, a backend change. How the diagram is built (SVG, kit part, library) is OQ-7; nothing beyond its answer is in scope.

## Decomposition for the backlog

One task, as queued; the three stories are one capability and none ships alone (no capability left unused).

| id | Task | size | deps |
|----|------|------|------|
| feat-NNN-structure-diagram-view | The switch, the hierarchy diagram and the team–project relations on «Структура» (frontend only) | M | feat-043 (merged) |

## Open questions

- [ ] **OQ-1 Deactivated projects' teams.** The offers omit deactivated projects, so their team links are unknown. (A, recommended) Draw the deactivated project and its sub-areas tagged «деактивирован», with no team lines and no claim about teams. (B) Hide deactivated projects from the diagram: the diagram then differs from the tree. (C) Add a read for them: contradicts «no new read», would turn the task into backend work.
- [ ] **OQ-2 Where the remembered choice lives.** (A, recommended) Browser storage per user and organization (BR-SD-9), the address stays plain. (B) A query parameter in the address (shareable link; a bookmark opens the diagram), with no memory of its own. (C) Both: the address wins, storage fills in when absent.
- [ ] **OQ-3 «Создать департамент» in the diagram.** (A, recommended) Shown in the page header in both views for the holder of `area.create`, as it is page-level. (B) Tree only (BR-SD-5 read literally): the diagram has no commands at all.
- [ ] **OQ-4 Teams that work on no project and no department.** (A, recommended) Drawn in their own group «Без проектов и департаментов», after the hierarchy, in the offers' order, choosable (US-3). (B) Not drawn: the diagram shows relations only.
- [ ] **OQ-5 Gesture and Tab model (BR-SD-10).** (A, recommended) Name opens, body chooses; one Tab stop and arrow keys, as the tree. (B) Every node in the Tab order, name as a link: up to ~1 400 Tab stops before the rest of the page. (C) Opening and choosing by two separate small controls on each node: no gesture ambiguity, but twice the controls on a dense picture.
- [ ] **OQ-6 Relation lines at volume (BR-SD-11).** (A, recommended) All lines while the organization has up to 150 relation lines (design may tune the number); above it, lines only for the chosen node, with the note of US-3. (B) Always all lines: may be thousands at 200 x 200, unreadable. (C) Lines only for the chosen node always: calm, but the diagram shows no relation at a glance, against the goal.
- [ ] **OQ-7 How the diagram is built (scope, route).** (A, recommended) Plain SVG and existing MUI parts inside `features/organizations`: no new dependency, no new part in `src/shared/ui`; stays M. (B) A reusable diagram part in `src/shared/ui` (component, test, showcase entry, guide block per the UI kit rule): wider scope, whole-suite verify, may push towards L. (C) A layout library: needs the human's yes, probably an ADR, may push to L.
