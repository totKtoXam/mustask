# Review: feat-049-structure-diagram-view

## Round 1 — requirements

Reviewer: requirements-reviewer, clean context, 2026-11-03. Input: `spec.md`, `stories/US-1..US-3`, checked against feat-043 (`spec.md`, `design.md` § 5.2, `api-contract.md` §§ 0-2), ADR-0032 § 6, `AreasPage.tsx`, `lib/areaTree.ts`, `lib/areaTexts.ts`, `api/navigationApi.ts`, `api/organizationsApi.ts` (`AreaNode`), `hooks/navigationQueries.ts`, `hooks/mutations.ts`.

**Verdict: `needs-human-decision`.** OQ-1..OQ-4 stay open, and R-1, R-2 and R-6 add decisions only the human can make (gesture model, density at volume, kit and dependency scope). M with a UI: gate-1 stays `pending` and the questions go into the stage 3 package. Counts: 0 blocker, 6 major, 6 minor; 3 fixed in place by the reviewer (R-4, R-5 in part, R-11).

### Data claims checked

- `GET …/areas` (`AreaNode`): id, parentId, type, projectId, projectActive, name, description, path, depth, pre-order. **No project key.**
- `GET …/selector-offers`: departments, active projects (`id`, `key`, `departmentId`, `teamIds` = active teams), active teams (`departmentIds` = direct links). No deactivated project, no deleted or deactivated team, no sub-area. BR-SD-3, BR-SD-4, BR-SD-7 hold against the contract.
- Every write invalidates `navigationKeys.all` (`mutations.ts`), so a write in the tree refreshes both reads: US-1 «Edits survive a switch» is supported.
- ADR-0032 § 6 matches BR-SD-9 (per `sub` and organization, malformed means absent).

### Rule → story

| Rule | Covered by |
|---|---|
| BR-SD-1 same address | US-1 «Switch to the diagram» |
| BR-SD-2 tree default | US-1 «The tree is the default», «A damaged memory falls back» |
| BR-SD-3 no new read | US-3 «Volume» (no read on selection); no scenario that the diagram sends no read beyond the two (R-3) |
| BR-SD-4 deactivated project | US-2 «A deactivated project», US-3 «Deactivated project, deactivated team» |
| BR-SD-5 read-only | US-3 «Read-only»; the page-level create action is OQ-3 (R-5) |
| BR-SD-6 same links | US-2 «Nodes open their pages», US-3 «A team opens its page»; conflicts with selection (R-1) |
| BR-SD-7 which teams | US-3 «A team's departments», «A team linked to a project only», «Deactivated…» |
| BR-SD-8 search | not tested anywhere: no scenario that a typed filter does not carry into the diagram (R-12) |
| BR-SD-9 memory | US-1 «remembered», «per organization and per user», «damaged memory» |
| NFR volume | US-2 «A large structure», US-3 «Volume» (R-2, R-8) |
| NFR accessibility | US-1 keyboard, US-2 «Keyboard and screen reader», «Narrow window», US-3 «Selection by keyboard» (R-1, R-7) |

### R-1
Severity: major
Affects: US-2 «Nodes open their pages», «Keyboard and screen reader»; US-3 «From a project to its teams», «Selection by keyboard»; BR-SD-6
Problem: One node has two actions that the stories give the same gesture. US-2 says activating a project, a department or a sub-area opens its page. US-3 says a click chooses (selects, does not open) a project, and that Enter or Space chooses it "as by a click". On a link, Enter opens it, so the two stories cannot both pass. Nothing says which node types can be chosen: departments can (US-3 «A team's departments»), but sub-areas and deactivated project nodes (not links, yet possibly choosable) are not covered. US-2 also asks for a Tab stop on every linked node. At the stated volume that is up to ~1 400 Tab stops before the user reaches anything after the diagram, while the tree it replaces uses one stop with arrow-key movement (`TreeRegistry`).
Required resolution: Define the gesture model in the spec: which part of a node opens the page (for example the name as a link) and which chooses it (the node body, or a separate control), the keys for each, and the list of choosable types. State the Tab model: either one Tab stop for the diagram with arrow keys inside it (recommended, as the tree does) or every node in the Tab order, and fix US-2 to match. This is a decision for the human (proposed OQ-5), carried into the stage 3 package.
Status: open
Resolution:

### R-2
Severity: major
Affects: US-2 «A large structure»; US-3 «Teams are drawn», «From a team to its projects»; NFR volume; the goal's «at a glance»
Problem: Readability at the stated volume is not specified. 200 teams joined to 200 projects can mean thousands of lines in one frame, so the stated goal "at a glance" fails, and no criterion can show that it fails. The spec does not say: where teams stand in the layout (a band, beside their projects, or in groups) or in what order; the initial expansion of the diagram (the tree uses D-3, all nodes expanded up to `TREE_EXPAND_ALL_LIMIT` and collapsed above it); what a relation line to a node inside a collapsed branch does (hidden, or drawn to the nearest visible ancestor); and whether choosing a team whose project is collapsed expands the branch so the emphasis can be seen.
Required resolution: Add to US-2 the initial expansion rule (reuse D-3's limit) and the line-to-collapsed-node rule. Add to US-3 that choosing a node reveals every node it emphasises. Decide whether relation lines are always drawn or only for the chosen node when the organization is past a threshold (proposed OQ-6, for the human). Team placement and order may be left to design, but the spec must say that it is left to design.
Status: open
Resolution:

### R-3
Severity: major
Affects: US-1 «Loading and failure follow the page»; BR-SD-3; US-2, US-3
Problem: Today the page has one read, and the diagram needs two. The stories cover only "loading or a read failed" as a whole. Not covered: the tree arrived but the offers are still loading or failed (draw the hierarchy without teams and keys and show an inline error with «Повторить»? or fail the whole view?); the offers answer 404 (should give `NotFound` as the tree does). Also not covered: the two reads disagree, which happens when the refetch after a write is between the two responses, or after feat-041, where the offers become visibility-filtered (api-contract § 0, BR-NV-15) while the tree may not be. In that case a project node has no offer, or an offer has no node. There is also no scenario showing that the diagram sends no read beyond the two.
Required resolution: Add to US-1 or US-2 scenarios for the partial state (one read failed or still pending, with the copy shown and what «Повторить» retries), for the offers' 404, and for a project node with no matching offer (drawn without key and team lines, no error). Add an offer with no node (ignored). Add a scenario that opening the diagram sends only the two reads.
Status: open
Resolution:

### R-4
Severity: major
Affects: US-2 «Each node says what it is»; BR-SD-3
Problem: The criterion said every project node shows its key. `AreaNode` carries no key; only `selector-offers` does, and the offers hold no deactivated project. A deactivated project's node therefore had no source for its key, so the criterion could not pass. BR-SD-3 also did not say how a tree node is matched to its offer.
Required resolution: Show the key for active projects only, and name the join key.
Status: resolved
Resolution: US-2 now reads «an active project also shows its key (a deactivated project shows none…)». BR-SD-3 now names `projects[].key` and the match `AreaNode.projectId = projects[].id`.

### R-5
Severity: major
Affects: Roles; OQ-3; US-1 «Switch to the diagram», «The empty organization»
Problem: Three problems. (a) The Roles section decided OQ-3 as B ("exist in the tree view only"), while OQ-3 recommends A and US-1's empty-organization scenario shows the create action in the diagram view. (b) The button is «Создать департамент» (`areasPage.create`, feat-043 design § 5.2); «Новый департамент» is the title of its dialog. (c) US-1 hid "the page title" beside the diagram, which would leave the page without its heading «Структура».
Required resolution: Keep Roles neutral until OQ-3 is answered, and correct the copy and the title. Once OQ-3 is answered, make US-1 «The empty organization» follow it. Under OQ-3 B it must still say whether the empty state's own create action stays, because the empty frame is shared with the tree.
Status: open (a: the wording is neutral now, the answer is owed; b, c resolved)
Resolution: Roles now defers the page-level «Создать департамент» to OQ-3, and OQ-3's title uses the real copy. US-1 now keeps the title «Структура» and hides only «Найти область» and «Развернуть всё» / «Свернуть всё».

### R-6
Severity: major
Affects: scope ("features/organizations"), Out of scope (library), size M
Problem: The UI kit rule (`src/frontend/AGENTS.md`, `docs/conventions/ui-kit.md`) says a pattern the kit lacks goes into `src/shared/ui` first: component, test, showcase entry, guide block. The kit has no diagram (node, connector, framed scrolling canvas). The spec states the scope as features/organizations only, and leaves the library decision to design. A new npm dependency for graph layout is a decision that may need an ADR. Either fact puts the change outside the stated scope and can push M toward L, and `src/shared/ui/**` changes make `verify.sh` run the whole suite.
Required resolution: Say in the spec whether the diagram parts go into the kit (in scope) or are built only from existing kit parts, and whether a new runtime dependency is allowed (and if so, that design records it, with an ADR if the convention asks for one). This is a human decision (proposed OQ-7) and affects the route.
Status: open
Resolution:

### R-7
Severity: minor
Affects: NFR accessibility; US-2 «Keyboard and screen reader»; US-3 «Selection by keyboard»
Problem: The two definitions of a node's accessible name differ. The NFR says type, plus teams for a project and projects for a team. US-2 says type, name and parent. With 30 teams, a project's name read in full is unusable. No criterion gives the diagram a structure that a screen reader can announce (a tree or list role with level and position), so a screen reader user meets the relations only node by node.
Required resolution: One definition: type, name, parent, and a relation count, with the full list in the announced summary of US-3. Add a criterion for the structural role (for example `tree` / `treeitem` with `aria-level`). Alternatively, state that the tree view is the text alternative for the hierarchy and the summary is the alternative for the relations.
Status: open
Resolution:

### R-8
Severity: minor
Affects: US-2 «A large structure» (2 s), US-3 «Volume» (200 ms), NFR volume
Problem: Timings in jsdom Vitest are not reliable, and the stories do not say where the timings are measured. As written, neither criterion can be turned into a stable test.
Required resolution: Name the measurement: on the stand with a seeded organization of the stated size, at stage 6, in a named browser. Or restate the criteria in a form a test can check, for example that choosing a node does not rebuild the layout and sends no read. The second half of US-3 «Volume» ("no read is sent") is already testable.
Status: open
Resolution:

### R-9
Severity: minor
Affects: US-3 «A team's departments», «From a project…», «From a team…»
Problem: The emphasis set is defined for a project and for a team only. For a department it says "its directly linked teams too", and "too" is not defined: are its projects included? their teams? its sub-areas? The summary copy for a department is missing, and so is the outcome for a sub-area or a deactivated node. It is also not said whether choosing a team emphasises its department lines.
Required resolution: A table in US-3: chosen type → emphasised nodes and lines → summary copy (Russian), with a row for every choosable type from R-1.
Status: open
Resolution:

### R-10
Severity: minor
Affects: US-3 «A team with no relation»; OQ-4
Problem: US-3 already asserts OQ-4 A. The group's position, its order (the offers come by name), and whether its teams can be chosen (with the summary «Проекты команды:» and nothing after it?) are not specified.
Required resolution: After OQ-4 is answered, state the group's place and order and what choosing an unrelated team shows.
Status: open
Resolution:

### R-11
Severity: minor
Affects: BR-SD-9
Problem: "Like the selection of feat-043" could be read as a field in the `mustask.context.v2.<sub>.<org>` value, whose shape ADR-0032 locks for the long term.
Required resolution: A key of its own.
Status: resolved
Resolution: BR-SD-9 now requires a key of its own, never a field in the selection's value.

### R-12
Severity: minor
Affects: US-1; BR-SD-8
Problem: The stories do not say what survives a switch and back: the tree's filter text, its expansion and its selected row, or the diagram's emphasis and collapsed branches. BR-SD-8 has no scenario.
Required resolution: A scenario in US-1. Proposed: the filter text and the expansion are kept for the visit and the emphasis is dropped on a switch. The filter shows no effect in the diagram, and is still in place on return to the tree.
Status: open
Resolution:

### Open questions for the human (carried into the stage 3 package)

- OQ-1..OQ-4 as in `spec.md`; the reviewer agrees with the recommendations (A).
- OQ-5 (R-1): the gesture model: what opens and what chooses, and one Tab stop with arrow keys or every node in the Tab order.
- OQ-6 (R-2): at volume, are relation lines always drawn or only for the chosen node past a threshold?
- OQ-7 (R-6): the diagram parts as a new kit component and/or a new runtime dependency, which widens the stated scope.

## Fix round 1 (analyst, 2026-11-03)

Not re-reviewed yet; the statuses above are the reviewer's, these are the analyst's closures for the re-check.

- R-1: BR-SD-10 (name opens, body chooses, Enter opens, Space chooses; choosable = department, active project, team; one Tab stop with arrow keys). US-2 «Nodes open their pages» and «Keyboard and screen reader», US-3 «Choosing is separate from opening» and «Selection by keyboard» aligned. Alternatives: OQ-5.
- R-2: BR-SD-11; US-2 «The initial expansion» (D-3 limit), «A relation line into a collapsed branch», «Layout details are left to design»; US-3 «Choosing reveals what it emphasises», «Many relations». Volume of lines: OQ-6.
- R-3: US-1 «Only the offers are not ready», «Only the tree is not ready», «The organization is not found», «The two reads disagree», «Only the two reads are sent»; «Loading and failure» says what «Повторить» retries.
- R-5 (a): still OQ-3, answer owed. R-6: OQ-7, options A/B/C, recommended A.
- R-7: one accessible name (type, name, parent, relation count), tree role with levels, full lists in the summary (NFR accessibility, US-2).
- R-8: timings measured on the stand at stage 6, tests check no read and no layout rebuild (NFR volume, US-2 «A large structure», US-3 «Volume»).
- R-9: table «What choosing emphasises» in US-3, a row per choosable type, Russian summaries.
- R-10: stated under OQ-4 A (place after the hierarchy, offers' order, choosable with «нет» summaries); valid only if OQ-4 is A.
- R-12: US-1 «What survives a switch».

Open questions for the human, recommended option: OQ-1 A, OQ-2 A, OQ-3 A, OQ-4 A, OQ-5 A, OQ-6 A, OQ-7 A.
