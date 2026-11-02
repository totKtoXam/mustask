# Review: feat-043-area-project-team-navigation, requirements

Moved verbatim from `review.md` at code review round 2, for the file budget (`docs/features/README.md`, "Artifact budgets").

## Round 1 — requirements

Reviewed at 1fc87af8: `spec.md`, US-1..US-9, against doc-012 (BR-MX-10..-12, -27, -28, US-10), doc-013, ADR-0024, ADR-0028 rule 7, ADR-0031, mod-018 BR-AW-*, feat-003 BR-WS-*, mod-020 review CR-2, `docs/error-codes.md`, and the panel's present routes and navigation model.

**Verdict: `needs-human-decision`.** Open: 2 blockers, 14 majors, 9 minors. The reviewer's write zone is this file, so nothing is fixed in the spec: every finding goes back to the business-analyst. The questions only the human can decide are listed at the end.

### Rule → story mapping

| Rule | Stories | Coverage |
|---|---|---|
| BR-NV-1 | US-1, US-4 | covered |
| BR-NV-2 | US-1, US-3 | covered; the existing section addresses are not stated (R-15) |
| BR-NV-3 | US-1, US-3, US-4, US-5 | covered |
| BR-NV-4 | US-2, US-3, US-4, US-5, US-8 | covered; project nodes in menus open (R-9) |
| BR-NV-5 | US-1, US-3, US-9 | covered |
| BR-NV-6 | US-1, US-9 | covered |
| BR-NV-7 | US-1, US-3, US-5, US-7 | contradicted by US-6 (R-13) and US-8 (R-3) |
| BR-NV-8 | US-1, US-9 | covered |
| BR-NV-9 | US-2, US-7, US-8 | partial: list narrowing is not in the rule (R-19) |
| BR-NV-10 | US-8, US-9 | partial (R-3, R-10, R-11) |
| BR-NV-11 | US-3, US-4, US-7 | partial: placement failures (R-12) |
| BR-NV-12 | US-6 | partial (R-2, R-4, R-5, R-6, R-7) |
| BR-NV-13 | US-1, US-2, US-4 | partial: the tree and «Проекты» only (R-2) |
| BR-NV-14 | US-2, US-3, US-4, US-5 | covered |
| BR-NV-15 | none | no criterion; only a cast note in US-6 (R-22) |
| BR-MX-27 | US-1, US-2, US-3, US-8 | departed from under OQ-1 A, not recorded as an amendment (R-14) |
| BR-MX-28 | US-8 | covered; project nodes open (R-9) |
| BR-MX-10, -11 | US-4, US-8, US-9 | covered |
| BR-MX-12 | US-4, US-7 | covered from the project's end only (R-25) |
| doc-012 US-10 | US-1..US-8 | every clause placed; the owed clauses stay owed; the area-link clauses are replaced (R-14) |
| BR-NE-3, -4, -5 | US-3, US-4, US-5, US-6 | covered |
| BR-NE-8 | US-6 | partial: feat-003's owed cross-project question is not settled (R-5) |
| BR-NE-9 | US-4, US-8, US-9 | covered |
| tech-009 F4, bug-003, CR-2, CR-3, CR-6, tech-011 m4 | US-1, US-6, US-9 | covered; CR-2's direction contradicts BR-WS-11 (R-4) |
| feat-045 | US-2 | covered; the first screen's edges open (R-20) |

### R-1

Severity: blocker
Affects: US-2 "Empty states", BR-NV-4, OQ-1, BR-AW-0, BR-AW-2, ADR-0031 rule 2
Problem: The scenario gives «Север» "projects and teams and no area" and expects both users to see «В организации пока нет областей». Since ADR-0031 every project has a node, created in the project's own transaction, and under BR-NV-4 / OQ-1 A «Области» is the whole typed tree. An organization with projects therefore always has nodes, so the cast cannot exist and the scenario cannot pass. The text is carried from doc-012 US-10, which predates the tree. The empty state is reachable only with no project and no department, and the likely real case (projects, no department) has no criterion at all. It also depends on OQ-1: under B the empty state means "no department".
Required resolution: Restate the scenario for each OQ-1 answer's reality: with no node at all, the empty state; with project nodes and no department, the tree of top-level project nodes, plus what the administrator is offered («Создать департамент»). Say so in the OQ-1 text. Add the OQ-2 side: «Обзор» counts 0 areas there and its «Области» list is empty while «Области» is not.
Status: open
Resolution:

### R-2

Severity: blocker
Affects: BR-NV-7, BR-NV-12, BR-NV-13, OQ-3, US-1, US-2, US-3, US-5, US-6, US-7, BR-AW-10
Problem: The deactivated project is specified on the tree, on «Проекты», on its own address and in the switcher, and nowhere else, while BR-NV-13 removes its page. Each gap below breaks a row link or leaves a dead end:
- A department's «Проекты» (US-3: "each opening the project's page") and its count of projects: is the deactivated node listed? If not, the department reads «В департаменте нет проектов» yet refuses deletion, because the node still counts as a child (BR-AW-10), and the administrator cannot see why. If so, its row opens «Проект не найден».
- A team's «Проекты» and its counts (US-5), and the project end of the team's project-link multiselect.
- The organization's «Рабочие элементы» (BR-NV-12 says "every project the user may see"; a deactivated one is neither named nor excluded). An item row there opens a card under a project that answers «Проект не найден» (BR-WS-15). The «Команда» and «Исполнитель» options follow the same question.
- The node's address: BR-NV-13 "opens nothing" does not say what `…/areas/<node>` answers. BR-NV-7 gives «Область не найдена»; OQ-3 B gives a page.
- Its sub-areas: they stay in the tree (BR-AW-10), and nothing says whether they are links, marked, or answer not found, nor what a sub-area's address answers. OQ-3 does not mention them.
- A placement multiselect (US-7) and the project creation's sibling name check: the node's name stays taken (BR-AW-10) while the node is invisible to most lists.
Required resolution: One rule, or a table in BR-NV-13: each surface, and whether a deactivated project, its node, its sub-areas and its items appear, are counted and open. Extend OQ-3 to the sub-areas. Add criteria for the department's delete refusal naming the deactivated project, and for the cross-project list. The cast has E for this: give it items, a team link and a place.
Status: open
Resolution:

### R-3

Severity: major
Affects: BR-NV-7, BR-NV-10, BPMN, US-1 "An address that names nothing", US-8 "A remembered object that is gone"
Problem: Two rules answer one event. Olga's context is «Ромашка» / D, she is on D's page, Ann deactivates D, Olga reloads. BR-NV-7 and US-1 say «Проект не найден» and "the switcher's context does not change". US-8 and BR-NV-10 say the context falls back with a notice. If the first wins, the switcher holds an object that US-8 says it never offers, and every later page inherits it; if the second wins, BR-NV-7's sentence is false. The BPMN does not help: the address branch and the reload branch are separate paths.
Required resolution: Define the order. Suggested: a not-found address leaves the context alone only when the context names a different object; when the address names the remembered object, the page answers not found and the context falls back with the notice. Add the scenario.
Status: open
Resolution:

### R-4

Severity: major
Affects: US-6 "The entry keeps the filter", US-9 "CR-2's rest", feat-003 BR-WS-11, BR-WS-5, § Decomposition Breaks
Problem: US-6 and US-9 say the navigation's «Рабочие элементы» keeps the filter when it opens its own list. Merged feat-003 BR-WS-11 says the opposite in terms: "the main navigation's «Рабочие элементы», the card page's trail «Рабочие элементы» … opens the list unfiltered". BR-WS-5 repeats it for the trail. mod-020's CR-2 asked for either fix, so this is a real choice, and the spec makes it silently: feat-003 is neither in Breaks nor amended, and BR-OT-8 alone is named for restating. A selected navigation entry that does nothing is also a visible behaviour: the user has no one-click way to clear, beyond «Сбросить фильтр».
Required resolution: The human chooses (question H-1). Then either amend BR-WS-11 and list it in Breaks, or drop the scenario and write CR-2's other branch: every link to the page without the filter clears it, and the selected entry too.
Status: open
Resolution:

### R-5

Severity: major
Affects: BR-NV-12, US-6, feat-003 BR-WS-9 (c), BR-WS-15, § Owed by later lines
Problem: feat-003 hands this line a named debt: "across projects the keyword's number alone (BR-WS-9 (c)) is no longer unique, which feat-043 settles". The spec does not settle it. BR-NV-12 just says the filters are "on both", and US-6 tests only «оплат». Typing «42» on the organization's list either returns one item per project, or nothing, and BR-WS-15's "A keyword never finds another project's item, by its identifier" has no meaning on a list with no fixed project.
Required resolution: A rule: what (a), (b) and (c) of BR-WS-9 mean on the cross-project list (suggested: number matches in every listed project, each item once; a whole identifier matches its one item, since ADR-0028 rule 7 makes it unique in the organization). A scenario with MSK-1, BET-1 and FIN-1 and the keyword «1».
Status: open
Resolution:

### R-6

Severity: major
Affects: US-6 first and second scenarios, BR-NV-12, feat-003 BR-WS-12
Problem: US-6 orders "newest change first and then by identifier" and says the project's section has "the same order". BR-WS-12 orders the project's list "latest change first, then by number descending". An identifier is not a number: read as text, MSK-10 sorts before MSK-2, and the number direction flips. The tie-break must also be total for paging across projects (numbers repeat between projects): a non-total order can repeat or drop a row between pages, against BR-MX-16.
Required resolution: State one total order for both lists, in BR-NV-12: for instance change time, then number descending, then the project key. Say whether BR-WS-12 is kept or amended, and test the tie across two projects at a page boundary.
Status: open
Resolution:

### R-7

Severity: major
Affects: § NFR Volumes, US-6 "The list is read within the budget", BR-NV-12
Problem: The budget is 2 s at 5 000 items in 200 projects, a million rows, and the scenario measures only an unfiltered page. What the same page must serve at that volume is not bound: BR-WS-9's contains-match over «Заголовок», ignoring case and «ё»; the pager's matching count; the «Исполнитель» options read over every item of every listed project (BR-WS-6); «Удалена». Those were designed against one project's items. A list that passes the scenario can take far longer the moment a user types. The design has no number to size an index against.
Required resolution: Extend the scenario to the filtered reads: keyword, team, assignee, and the options read, each with its budget. If the human accepts a weaker keyword budget at this volume, record it. Note OQ-4: with no project filter, the keyword is the only way to narrow this list.
Status: open
Resolution:

### R-8

Severity: major
Affects: OQ-5, doc-013 § What it still takes over, queue note on feat-043
Problem: doc-013 settled the mechanism: "the orchestrator decides when feat-043 starts: if tech-019 has not started by then, feat-043 absorbs it and tech-019 is withdrawn". feat-043 has started and tech-019 is still queued unstarted. OQ-5 recommends "tech-019 first; this line starts on the kit", which means stopping a started L for an S. It presents as open what the rule has decided, and recommends against the rule without saying so. "The criteria here are look-neutral either way" is true only of the criteria: under A this line's shell code is on the kit, under B its size and tech-020's deps change.
Required resolution: Restate OQ-5 honestly: the default by doc-013 is B (absorb); A is a departure, and it pauses this line after gate-1. Say what absorbing adds to US-1 and US-9, and name the tech-020 dep change as B's consequence. The human decides at gate-1.
Status: open
Resolution:

### R-9

Severity: major
Affects: BR-NV-10, BR-MX-28, US-8 "The switcher offers", US-1, ADR-0031 rule 5
Problem: A project has two pages: its own and its node's, both named A. US-8 lists "the departments and the areas with their paths, the projects", which does not say whether project nodes are in the offer. If they are, A appears twice, once as an area with a path. If they are not, then opening a node's page by «Области» sets a context the switcher cannot show: the BPMN says context = "this object", and BR-MX-28 says the address sets it. Sub-areas are also areas: are 1 000 of them offered? And the US-8 line "«Ромашка» / A" is ambiguous between the two pages. The navigation marker on a node page is not stated either (presumably «Области»).
Required resolution: A rule on project nodes: excluded from the switcher's offer (suggested), and what the node's address sets as the context: the project, or the node shown as itself. A scenario for each, and the marker.
Status: open
Resolution:

### R-10

Severity: major
Affects: BPMN node SW, BR-NV-10, US-8 "Choosing an object", ADR-0028 rule 7
Problem: The BPMN says "same section when the kind has it", so from MARS's «Бэклог» choosing A gives A's «Бэклог», since a project has one. US-8 and ADR-0028 rule 7 say an object of another kind opens at its overview: A's «Общие». BR-NV-10 is silent, although the story's title says the section is kept "where it can". Also unspecified: whether the `:level` of a board or backlog and the address's filters survive a same-kind choice (a team's filters are not a project's); what a choice in the organization part of the switcher opens from an object's page. ADR-0024 § 2's landing rule is being replaced, so nothing else answers it.
Required resolution: Put ADR-0028's rule into BR-NV-10, correct the BPMN, and state the level, the filters and the organization change, each with a scenario.
Status: open
Resolution:

### R-11

Severity: major
Affects: BR-NV-10, US-8, BPMN node P, OQ-7
Problem: The context model has undefined states, and the new ADR cannot decide them without product answers:
- On an organization-level page, what does the context show? US-8 keeps «Ромашка» / MARS on «Участники» after a reload, so the address does not clear it, while ADR-0024 § 2 says the switcher shows the address's context and the BPMN's P only covers objects. Then does the organization's «Рабочие элементы» under the context MARS show MARS's items? It should not, and no criterion says so.
- Is the memory per organization? Olga is in «Ромашка» / MARS, goes to «Другая», returns: MARS or the organization alone? "Remembered per user in the browser" covers either.
- Two tabs on two objects: the last write wins for a reload? The address must win in its own tab.
- An address in «Другая» while the remembered context is in «Ромашка»: the notice must not fire; it is a change, not a loss.
Required resolution: State each in BR-NV-10, with scenarios, and name in the spec what the context affects (the switcher's display and the choice's start point only: it narrows no page), so that it is plainly not a filter.
Status: open
Resolution:

### R-12

Severity: major
Affects: BR-NV-11, US-7 "A project is placed…", "A project leaves a department…", § Decomposition (API), BR-AW-3, BR-AW-4, BR-AW-14
Problem: Placement is sold as a multiselect, but the API moves one node (`PUT …/areas/{areaId}/parent`) and team links have an all-or-none batch that placement does not. US-7 tests one project only. Unsaid: can several projects be chosen; all or none or partial; and what the user sees when one fails. Two failures of the rule set have no criterion in this story: a sibling name at the destination (`name_taken`; only US-4 covers it, and not on a move to the top) and the depth limit, where a tree "that fits only at the top level" is refused in a department (BR-AW-14). «Remove» from a department can fail on the name too, which is a surprising answer to an unlink-shaped command. The spec's additive API names no batch move.
Required resolution: Decide: one project at a time here (a single-select, reworded, in a multiselect's shape), or a new all-or-none batch named in § Decomposition. Add scenarios for both refusals, on the department's end and on the top-level move.
Status: open
Resolution:

### R-13

Severity: major
Affects: US-6 "A project the caller cannot reach", BR-NV-7, ADR-0028 rule 7, feat-003 BR-WS-15
Problem: US-6 says an address naming an item of «Другая» in «Ромашка» answers «Рабочий элемент не найден». An item's address is `…/projects/:p/workitems/:identifier`, so that address names the foreign project first, and BR-NV-7, ADR-0028 rule 7 and BR-WS-15 all give the outermost level: «Проект не найден». BR-NV-7 has no item level at all. The scenario is reachable only through `?workitem=` over a list, which US-6 does not use.
Required resolution: Replace with «Проект не найден» for the path address. If the organization's list hosts a card by `?workitem=`, say so and state that case separately.
Status: open
Resolution:

### R-14

Severity: major
Affects: § Breaks, § Context, BR-NV-4, OQ-1, doc-012 BR-MX-27, US-10, doc-013 BR-NE-6
Problem: Breaks does not list what this spec really departs from. BR-MX-27 still says no trail or list puts a project under an area, and US-3's trail «Логистика / A / Склад» and US-2's tree do so. ADR-0031 changed the model but left BR-MX-27 as written, and doc-013 BR-NE-6 says BR-MX-1..-28 stand, so a later reader finds them in force. US-10 still holds area–project links in its Background and scenarios, «Логистика» counting 3 projects, «Проект не входит ни в одну область», and «Логистика / Склад». Also missing from Breaks: feat-003 BR-WS-11 (R-4), ADR-0024 § 1 ("the context it needs is real") and § 4 (a new not-found level), and doc-012 fate-table lines naming a board as a navigation entry. US-9's "docs brought to date" is scoped to interim pages, so it does not reach these.
Required resolution: Add them to Breaks, and to US-9's last scenario as a named list: BR-MX-27 and the US-10 clauses, each marked "feat-043, ADR-0031". Whatever OQ-1 settles is the text those marks carry.
Status: open
Resolution:

### R-15

Severity: major
Affects: BR-NV-2, § Retired addresses, OQ-6, US-1, US-4, US-5, ADR-0028 rule 7, doc-013 queue lines
Problem: Three things disagree on the board and backlog addresses. Today: `…/teams/:t/backlogs/:level`, `…/boards/:level`, and the same under `…/projects/:p`. The queue lines of feat-042 and mod-007 fix `areas/:a/backlogs/:level`, `areas/:a/overview` and `teams/:t/sprints`, and ADR-0028 rule 7 says each prefix has its sections and `…/settings`. OQ-6 reopens this with "a path segment `…/teams/:t/backlog` or a query", though the ADR has already chosen paths, and its example is a fourth shape that would change addresses in use without their being on the retired list. The retired list also names "the entries «Бэклоги» and «Канбан» and their organization-level pages": the panel has no such pages, only the team and project routes above, so the sentence names no address a test could open. Finally, `…/projects/deactivated` (BR-NV-13's view) needs to stand beside `:projectId`.
Required resolution: Close OQ-6 as a statement: section addresses are path segments; team and project boards and backlogs keep their addresses. Remove the phantom from the retired list. Give the retired table only addresses a test can open.
Status: open
Resolution:

### R-16

Severity: major
Affects: OQ-8, § Stories, doc-013 OQ-5, panel's organization list
Problem: OQ-8 rests on feat-038: "whichever merges second". This checkout has no `docs/features/feat-038*`, no queue line and no finished or withdrawn record for it, only doc-012's and doc-013's mentions. So "feat-038's landing" may not exist, or may be lost. Meanwhile the panel's actual landing is unspecified: the organizations list's rows and its create flow go to `…/settings/projects`, which this line retires, and no story says where they go. «Главная» and the choice of an organization in the switcher need the same answer.
Required resolution: The orchestrator confirms where feat-038 stands. Rephrase OQ-8 as "where a chosen or new organization opens", covering the organizations list, its create flow and the switcher, and add a scenario to US-1 or US-2: no link in the panel still targets a retired address.
Status: open
Resolution:

### R-17

Severity: minor
Affects: US-3 "Deleting a node", `docs/error-codes.md`, BR-AW-6
Problem: US-3 quotes «Нельзя удалить область, в которой есть вложенные области или рабочие элементы». `organizations.area.not_empty` reads «Нельзя удалить область, в которой есть проекты, вложенные области или рабочие элементы». The same scenario has «Транспорт»'s confirmation name "its team links", which a sub-area cannot have (BR-AW-6). The department's delete refusal is not tested.
Required resolution: Quote the catalog. Move the team-links clause to a department's deletion.
Status: open
Resolution:

### R-18

Severity: minor
Affects: BPMN, BR-NV-10
Problem: The BPMN asks "Address names an organization the user belongs to?" before "no context: «Главная», dictionaries", so a page that has no organization reaches «Организация не найдена». The fallback to "the first organization" has no answer for a user who has lost their only organization: the notice «Выбрано: …» has nothing to name.
Required resolution: Move the no-context branch up. State the zero-organization case.
Status: open
Resolution:

### R-19

Severity: minor
Affects: BR-NV-9, US-2 last scenario
Problem: The narrowing scenario cites BR-NV-9, which says nothing of narrowing, and covers «Проекты» only. «Команды» at 200 and the tree at 1 000 nodes have no way to narrow, and what narrowing a tree means (keeping ancestors) is unstated.
Required resolution: Put it in the rule, naming the three lists or saying why only one.
Status: open
Resolution:

### R-20

Severity: minor
Affects: US-2 first scenario, OQ-2
Problem: The «Обзор» has "the top-level areas", while D's node also stands at the top level and OQ-2 A says departments only. "Shows her team VENUS first" has no case for a member of no team or of several. The organization's own list ending «Все организации» is not bounded for a user of many organizations.
Required resolution: Say «департаменты» in the scenario and add the two team cases.
Status: open
Resolution:

### R-21

Severity: minor
Affects: US-3, § Operation completeness, ADR-0031 rule 5
Problem: A project has a description and its node has another: ADR-0031 says the node's page shows the project's data, and US-3 has Ann describe the node. Which does the node's page show? The table row for departments reads "move (not: it stands at the top)", which says there is and is not a move.
Required resolution: Name the description shown and the one edited. Remove "move" from that cell.
Status: open
Resolution:

### R-22

Severity: minor
Affects: BR-NV-15, US-9 "tech-011 m4"
Problem: BR-NV-15's "takes the caller as a required argument" has no criterion. US-9's tech-011 scenario reads the queue "after this line merges", so it cannot pass within the MR that is checked.
Required resolution: Add an inventory or signature check for the first. Move the second to the orchestrator's post-merge step, or state it in the MR's list.
Status: open
Resolution:

### R-23

Severity: minor
Affects: § Decomposition, OQ-9, checklist item 5
Problem: Size: the task is L, above the ≤ M line. The human confirmed it at doc-013 OQ-6 A and at the route, so this is recorded, not contested. OQ-9 reopens that answer with a recommendation to keep it: harmless, but it adds a question. The row still says `feat-NNN-area-project-team-navigation`.
Required resolution: Write `feat-043`. Keep OQ-9 only if the analysis has found something new; if so, name it.
Status: open
Resolution:

### R-24

Severity: minor
Affects: US-8, US-6, feat-003 BR-WS-5
Problem: No trail or marker is given for a card. On the path address from the organization's list: is the marker «Рабочие элементы» or «Проекты», does the context become the project, and does the card's trail «Рабочие элементы» go to the project's section or the organization's list? BR-WS-5's purge landing returns to "that list's address", which holds for the new list only if a row of it counts.
Required resolution: A scenario in US-8 for the card, from both lists.
Status: open
Resolution:

### R-25

Severity: minor
Affects: US-4, US-7, BR-MX-12, BR-NV-11
Problem: US-4 offers the unlink on "each row of a team that has items", BR-NV-11 on every row. The reassigning unlink is tested from B's «Команды» only, not from the team's «Проекты», where the operation table also places it. US-7 has one race (a team deleted during a dialog); a department deleted, or a project moved or deactivated, while a dialog is open has none.
Required resolution: Reword US-4. Add the team end, and one more race on a placement.
Status: open
Resolution:

### Questions for the human at gate-1

Beyond the spec's own OQ-1, OQ-2, OQ-3, OQ-4, OQ-5, OQ-7, OQ-8 and OQ-9 (OQ-6 is not a product question: R-15):

- **H-1 (R-4).** Does the selected navigation entry «Рабочие элементы» keep the page's filter (US-6, amending feat-003 BR-WS-11), or clear it (BR-WS-11, then CR-2 closes by the written rule)?
- **H-2 (R-2, with OQ-3).** Do a deactivated project's items, sub-areas and department row stay visible, and where?
- **H-3 (R-12).** One project at a time when placing into a department, or a new all-or-none batch?
- **H-4 (R-7).** If the 2 s budget does not hold for a keyword over a million rows, may it be weaker at that volume?
- **OQ-5 (R-8):** the rule of doc-013 already says absorb; choosing A is a departure from it.
