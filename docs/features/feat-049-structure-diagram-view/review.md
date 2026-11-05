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

## Round 1 — code (stage 8)

Reviewer: code-reviewer, clean context, 2026-11-04, HEAD 550345ee. Input: `git diff 02fc1973..HEAD` (frontend and docs only, no backend), `spec.md`, US-1..US-3, `design.md`, `plan-frontend.md`, `qa.md`, `status.md` § Decisions, `docs/conventions/frontend.md`, `docs/conventions/ui-kit.md`. Size M: one round over the whole diff, and security is covered here (no separate L pass). Runs (single files only): kitGuard, colourLiterals, contrast, `IconToggleGroup.test.tsx`, `organizations/lib/*`, `useStructureSettings.test.tsx` — 793 green; ESLint 0 errors (1 old warning in `AreaTypeMark.tsx`); `tsc -b` clean.

**Verdict: `changes-requested`.** 0 blocker, 2 major, 7 minor. Both majors are fixed by a prose edit or one test, with no runtime change. QA's F-3 and F-4 stay open as minors and are not repeated here, except where CR-6 adds to F-4.

### Scope and architecture
- Shared changes are only `shared/ui/actions/IconToggleGroup.tsx` (with its test, export, showcase entry and `ui-kit.md` block), the `BoxedTabs` docblock, and `shell.levelThree` / `shell.levelFour` with their contrast pairs. Nothing changes under `app/navigation` or the route tree, and `src/test/kitGuard.allow.ts` is untouched. OK.
- No new npm dependency. MUI 9.4 `ToggleButtonGroup` provides the roving tab index (`useRovingTabIndexRoot`), so the "one Tab stop" claim is true of the library.
- Import of `../../auth/AuthContext` from the slice: the same pattern as `MembersPage` and the reference slice (auth has no `index.ts`). Not a finding.

### Security
- XSS: no `dangerouslySetInnerHTML` or `innerHTML` in the diff. Node and team names render as React text and `title` attributes, and the SVG has no data in markup. OK.
- `localStorage`: three keys `mustask.structure.<setting>.v1.<sub>.<org>`. Each read is matched against an allow-list with a default and wrapped in try/catch (`useStructureSettings.ts:31-38`), and values are written only from user handlers. The value of `mustask.context.v2` is not touched. OK.
- Requests: `selector-offers` runs only while the diagram is shown (`AreasPage.tsx:61`). Nothing else is fetched, and no write exists in the diagram. Navigation targets come from `useObjectLink` / `useAreaRowLink` (internal paths). No secrets. OK.
- Nothing widens what a role reads: both reads already exist for every organization role.

### Findings

#### CR-1
Severity: major
Where: `design.md:140` ("each button is its own Tab stop (MUI default)"), `spec.md:35` BR-SD-10 ("the toolbar controls (each button of the three toggles, …) … are separate stops")
Problem: the orchestrator's stage-6 decision (status § Decisions) made each `IconToggleGroup` one Tab stop. Four places follow it: US-2 (edited in this diff), `ui-kit.md`, the `IconToggleGroup` docblock and the test. But design § 4.9, rewritten in this same diff, still says one stop per button and calls that MUI's default (in MUI 9 it is not), and BR-SD-10 says the same. The diff contradicts itself, so this is major regardless of what the code does (stage file, "Severity"). A developer or QA who acts on the spec would "fix" the kit part back.
Fix: rewrite both sentences to say "each toggle group is one Tab stop, arrow keys between its buttons", citing the stage-6 decision. Prose only: one delta read, no QA.

#### CR-2
Severity: major
Where: `spec.md:59` NFR Volume: "Tests check what is stable: no read beyond the two, no layout rebuild on choosing in «Затемнение» and «Подсветка»"; `qa.md:47` ("choosing in «Затемнение» and «Подсветка» rebuilds no layout")
Problem: no test checks either property. No test spies on or counts `layoutDiagram`. The read test (`AreasPage.diagram.qa.test.tsx:313-328`) asserts no writes and `selector-offers` ≤ 2, but never that every GET is one of the two reads, despite its title "no new kind of read". The QA sentence is also wrong by code: `choose()` patches `expansion` / `sections` when an emphasised card is collapsed or folded (`StructureDiagram.tsx:190-196`), which rebuilds the layout (BR-SD-11 allows this). The guarantee an agent would rely on when touching the `useMemo` deps (`StructureDiagram.tsx:116-125`) does not exist.
Fix: either add one test, starting fully expanded: mock `../lib/diagramLayout` with a pass-through spy, choose a department in «Затемнение» and in «Подсветка», and assert no new `layoutDiagram` call; and assert that every GET path matches the areas or `selector-offers` URL. Or weaken both sentences to "checked by code review" and say that choosing a hidden node opens branches, which does rebuild. A test edit means QA re-runs only over that file.

#### CR-3
Severity: minor
Where: `StructureDiagram.tsx:430-435`
Problem: `emptyText` treats "no card drawn" as "nothing related" or "no level matches". Folding both sections (no node chosen, no level pressed) shows «Нет связанных узлов» under the headers. With a level pressed it shows «Нет узлов на выбранных уровнях», which is also wrong there. The text at `rulerY + 56` also overlaps the second section header in the horizontal layout.
Fix: compute the empty case per unfolded section, or skip it when every section is folded. Position the text below the last header.

#### CR-4
Severity: minor
Where: `diagramLayout.ts:207-210, 281-286`; `StructureDiagram.tsx:260-262`
Problem: in the vertical layout, chips are pushed only for unfolded sections that keep at least one root. Folding both sections, or a section emptied by «Только связанные», removes the level chips, pressed ones included. «Все уровни» then sends focus to `[data-chip="1-0"]`, which does not exist, so focus falls to `body`.
Fix: emit the chips of a folded section in a slim band, or fall back to the horizontal ruler chips. In `resetLevels`, fall back to the tree's focus item when no chip exists.

#### CR-5
Severity: minor
Where: `pages/AreasPage.diagram.test.tsx:33, 89, 121, 158`
Problem: four test titles claim more than they assert. "…and a damaged value means the tree" asserts only the diagram. "reads only the area tree until the diagram is open, and the offers once" asserts no request. "chooses with Space, Escape clears" presses neither key. "«Только связанные» keeps the chosen branch" never checks which cards remain. US-1 «Switching by keyboard» ("confirm … the view changes") is also untested: only the focus move is.
Fix: add the missing assertions (request log; Space then `aria-selected`, Escape then none; a removed card absent; Enter on «Диаграмма» shows the tree role), or retitle the tests.

#### CR-6
Severity: minor (adds to QA F-4)
Where: `diagramZoom.ts:8-10, 18-20`; `StructureDiagram.tsx:297-302`
Problem: `wheelZoom` rounds through `clampZoom` to 0.01, and `zoomTo` returns when the rounded value is unchanged. Small Ctrl+wheel deltas (precision touchpads and pinch, deltaY of about 1-4) round back to the same zoom at every event, so the picture never zooms while the page zoom is still prevented.
Fix: with F-4, keep an unrounded zoom accumulator in a ref (updated per event, not per render). Round only for display and for the stored state.

#### CR-7
Severity: minor
Where: `plan-frontend.md:34` vs `stories/US-1-view-switch.md:124-127`
Problem: US-1 «The empty organization» says "When I choose «Диаграмма» … the same empty state". The code hides the switch in an empty organization (`AreasPage.tsx:194`), as the plan says, so nobody can choose. The plan does not list this under § 6 Deviations, and the story still reads as if the switch were there.
Fix: add it to plan § 6, and reword the story to "the switch is not shown; the empty state is the tree's, whatever view is remembered".

#### CR-8
Severity: minor
Where: `LevelChip` in `StructureDiagram.tsx:676-727`
Problem: the level chip is a hand-built toggle button: a `span role="button"` with its own key handling, focus ring and `aria-pressed`. `src/frontend/AGENTS.md` puts a missing pattern into the kit first. This is plan deviation 2, but no recorded decision accepts it: status § Decisions covers only the toggle Tab stop.
Fix: either the orchestrator records the deviation in § Decisions, or a coloured toggle chip goes to the kit as a follow-up line.

#### CR-9
Severity: minor
Where: `AreasPage.tsx:60`; `spec.md:96`
Problem: the comment cites BR-SD-7, which is superseded; the rule meant is BR-SD-3. The OQ-7 row says "Answered A" and still ends with "Awaiting the human; not decided here."
Fix: cite BR-SD-3, and drop the stale sentence. Both can go into CR-1's prose edit.

### Behavioural claims (item 6)
- `ui-kit.md:76, 120-124` IconToggleGroup (one stop, arrows, `aria-pressed`, tint + 3 px underline, ring inside): anchored at `IconToggleGroup.tsx:36-49, 86-94` (frame.thick 2 + 1) and `IconToggleGroup.test.tsx:73-82`.
- `BoxedTabs.tsx:146-148` (exception for a same-address view): anchored at `AreasPage.tsx:205-214` (no route change).
- `tokens.ts` "5.6:1", "8:1": anchored at `contrast.test.ts:136-143`.
- `IconToggleGroup.tsx` docblock "MUI's roving focus": anchored in `@mui/material/ToggleButtonGroup/ToggleButtonGroup.js:205` (v9.4).
- `useStructureSettings.ts` docblock (own keys, allow-list, written only on choice): anchored at `:31-38, 80-100` and the test `:17-46`.
- `useDiagramView.ts` docblock (kept across switch, dropped on organization change): anchored at `:34-38`; the search and zoom halves by `AreasPage.diagram.test.tsx:55-64`.
- `useAreaRowLink.ts` docblock (BR-SD-6 links): anchored at `:15-22`.
- `spec.md` Out of scope ("the one new shared part is IconToggleGroup"): anchored by the diff (`shared/ui` holds only that file new).
- `spec.md:35` BR-SD-10 toggle stops → CR-1. `spec.md:59` "Tests check …" → CR-2. `spec.md:96` stale sentence → CR-9.
- `design.md:140` "its own Tab stop (MUI default)" → CR-1. `design.md` Kit point 1 "Accepted by the human at gate-1+2 round 5": anchored at status § Decisions, round 5.
- `stories/US-2` keyboard scenario (one stop per toggle): anchored at `IconToggleGroup.test.tsx:73-82`.
- `plan-frontend.md` § 3 (offers notice, offers 404 takes not-found): anchored at `AreasPage.tsx:84-86`, `StructureDiagram.tsx:477-488`. Empty organization → CR-7.
- `plan-frontend.md` § 4 keys: anchored at `StructureDiagram.tsx:339-410`. § 6 deviations 1, 3-6: anchored at `IconToggleGroup.tsx:36`, `DiagramNode.tsx:101-110`, `diagramLayout.ts:136-145`, `diagramLevels.ts:30-42`. Deviation 2 → CR-8.
- `qa.md:47` "rebuilds no layout" → CR-2. `qa.md` F-1 fix: anchored at `StructureDiagram.tsx:707-713` and `diagramContrast.test.ts`. F-2 fix: anchored at `StructureDiagram.tsx:240-249`.
- Direction 2: the changed behaviours are the tree view's page-head «Развернуть всё» hidden in the diagram, `rowLink` moved to `useAreaRowLink`, `selector-offers` now read on «Структура», and the toggle-group pattern. I grepped `AreasPage|rowLink|IconToggleGroup|ToggleButtonGroup|BoxedTabs` over `docs/conventions`, `docs/adr`, `AGENTS.md`, `src/frontend/AGENTS.md` and `.claude`. The only hit is `ui-kit.md`, already in the diff and consistent. ADR-0032 § 6 (the selection's key) is untouched by the new keys. Not swept: the merged feat-043 artifacts that describe «Структура» as a tree only. They are a closed feature's record, not live instructions.

## Round 2 — fix check

Reviewer: code-reviewer, 2026-11-04. Delta only: `git diff 21ed9913..e17214ad`, read against the Round 1 code findings and QA F-3/F-4. HEAD 20dac4f8 is status-only. CR-8 was accepted by the orchestrator (status § Decisions) and not re-checked.

Run: the CR-2 no-rebuild test, re-run from a throwaway copy of `AreasPage.diagram.test.tsx` with `expect(built).toBeGreaterThan(0)` added. It is green, so the `vi.mock` spy really intercepts `StructureDiagram`'s import and the test is not vacuous. I deleted the copy and committed nothing from it. The developer ran the page and lib files; the coordinator ran kitGuard and colourLiterals.

**Verdict: `approved`.** Both majors are closed. One new minor (CR-10) and one partial minor (CR-4) stay as debt.

| Finding | State | Evidence |
|---|---|---|
| CR-1 (major) | fixed | `design.md:140` and `spec.md:35` BR-SD-10 now say each toggle group is one Tab stop with arrows; they agree with US-2, `ui-kit.md` and the code. |
| CR-2 (major) | fixed | Spy test `AreasPage.diagram.test.tsx` «rebuilds no layout…» (sanity-checked above). The read test now asserts every GET is `/areas` or `/selector-offers`, and offers exactly once. `spec.md:59` and `qa.md:45-48` now name the collapsed-branch rebuild. |
| CR-3 | fixed | `StructureDiagram.tsx` `emptyText` requires an unfolded section, and «Нет связанных узлов» only under a choice in «Только связанные»; `emptyTop` sits below the last header. Test «shows no empty text when both sections are folded». |
| CR-4 | partly fixed (minor, debt) | `resetLevels` falls back to any `[data-chip]`, then to the tree's focus item, so focus stays in the diagram (test «keeps focus in the diagram…»). The chips themselves still vanish in the vertical layout when sections are folded, pressed ones included; recovery is still possible through «Все уровни», which is enough for a minor. |
| CR-5 | fixed | Titles match their assertions. New tests cover Space/Escape, Enter on the tab confirming the switch, and «Только связанные» removing cards. |
| CR-6 + F-4 | fixed | `zoomRaw` (unrounded accumulator) and `zoomApplied` (base for a burst) are refs; the scroll anchor chains through `pendingScroll`; buttons step from `zoomApplied`; the refs reset on organization change and on remount. Test: 50 × deltaY −4 gives 122 % (e^0.2). |
| CR-7 | fixed | US-1 «The empty organization» reworded; `plan-frontend.md` § 6 lists the deviation. |
| CR-9 | fixed | `AreasPage.tsx:60` cites BR-SD-3; the stale OQ-7 sentence is gone. |
| F-3 | fixed, with CR-10 below | `jump()` writes neither expansion nor folds while `related` is set. Test «writes nothing to the expansion…». |

#### CR-10
Severity: minor (new, from the F-3 fix)
Where: `StructureDiagram.tsx` `jump()`, the `if (!related)` block
Problem: the guard skips `unfoldSections` as well as `openNodes`. In «Только связанные» the layout force-opens the ancestors of kept cards, but it never unfolds a folded section (`diagramLayout.ts`, `if (!open) continue`). So finding a **related** card in a folded section neither unfolds it nor focuses it, and shows the wrong toast: «Узел скрыт режимом «Только связанные»». BR-SD-11 says a folded section holding found cards is unfolded by finding, and `choose()` still does this in every mode.
Fix: in «Только связанные», skip only `openNodes`; call `unfoldSections([id])` when the match is kept (`related.keep` / `related.teams`). Pick the toast by the actual cause.

### Behavioural claims (delta)
- `spec.md:35` BR-SD-10 toggle stops: anchored at `IconToggleGroup.tsx:36` (MUI roving) and `IconToggleGroup.test.tsx:73-82`.
- `design.md:140`: same anchor.
- `spec.md:59` "Tests check … no read beyond the two, no layout rebuild … when the chosen branch is already expanded": anchored at the two tests in `AreasPage.diagram.test.tsx` named above.
- `qa.md:45-48` (a collapsed branch rebuilds; a test counts calls): anchored at `StructureDiagram.tsx` `choose()` and the spy test.
- `stories/US-1` «The empty organization»: anchored at `AreasPage.tsx:194` (tabs only with areas).
- `plan-frontend.md` § 6 new line: same anchor.
- Code comments in the fix (zoom refs, «Только связанные» never writes expansion or folds): anchored by the code they sit on. "or folds" is exactly what CR-10 questions against BR-SD-11.
- Direction 2: the changed behaviours are search jump in «Только связанные» and wheel zoom accumulation. I grepped `Найти узел|jump|Ctrl + wheel|wheel` in the feature folder: BR-SD-8, BR-SD-11, BR-SD-20 and design § 4.9 are consistent except BR-SD-11's folded-section sentence, which is CR-10. Nothing outside the feature folder describes either behaviour.
