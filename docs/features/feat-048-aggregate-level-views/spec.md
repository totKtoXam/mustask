# Spec: Aggregate level views (feat-048)

## Context and goal

This line is «the second line» of `feat-043-area-project-team-navigation` (its OQ-9 split, human, 2026-10-30). It adds, after «Обзор», «Рабочие элементы», «Канбан» and «Бэклог» at the organization and department level and «Рабочие элементы» at the team level: the organization-wide work-item list, and the aggregate backlogs and boards of every active project (department: the projects placed in it) as one collapsible group (swimlane on the board) per project in the project's own rank order, with a move inside a group, rows loaded per project as a group opens, the reads capped and paged by a cursor. The requirements are **reused, not restated**: they were approved at feat-043's gate-1 and gate-2 and live in that folder; this index points at them and adds only the Delta below. Nothing in the approved text is reworded or reopened here.

**Amendment, 2026-11-03 (the human, in chat, before gate-2):** the three views get a **grouping switch** («Группировка»: none, by department, by project, by team), which amends BR-NV-20's "one group per project" into "by project is the default of the aggregate levels, and the user may choose another grouping or none". The amendment is this line's own rule (BR-AV-1..12, § Business rules); feat-043's files are not edited and carry a "Breaks" note (recorded in this task's report to the orchestrator). Gate-2 is open again after the design rework.

Folder `F43` = `docs/features/feat-043-area-project-team-navigation/`. «The reference tracker» is never named.

## Roles

Unchanged from F43 `spec.md` § Roles: access is the organization role's bundle (ADR-0027) until `mod-019`; «Администратор» and «Участник» move a group's rows and a lane's cards where the project's own board or backlog allows it, «Читатель» reads; a non-member sees «Организация не найдена».

## Stories

US-6 and US-10 are written and approved in F43; this line adds one story of its own, US-11, for the grouping switch (the amendment above). Each is `shippable: no` on its own; they ship together.

| id | Title | depends-on | File |
|----|-------|------------|------|
| US-6 | «Рабочие элементы» at every level: organization, department, team, project | US-1, US-4 (merged) | [`F43/stories/US-6-work-items-across-projects.md`](../feat-043-area-project-team-navigation/stories/US-6-work-items-across-projects.md) |
| US-10 | «Канбан» and «Бэклог» at the organization and the department; one group per project | US-1 (merged), US-6 | [`F43/stories/US-10-aggregated-board-and-backlog.md`](../feat-043-area-project-team-navigation/stories/US-10-aggregated-board-and-backlog.md) |
| US-11 | The grouping switch on «Бэклог», «Канбан» and «Рабочие элементы» | US-6, US-10 | [`stories/US-11-view-groupings.md`](stories/US-11-view-groupings.md) |

## Rules carried

The rule text is in F43 `spec.md` § Business rules and § Non-functional requirements; ids only here.

| Rule | What it fixes | Where |
|------|---------------|-------|
| BR-NV-12 | The cross-project work-item list, its order and total, the keyword rules, the team as the selector's state, the entry clearing filters never selectors | F43 spec, BR-NV-12; US-6 |
| BR-NV-20 (amended by BR-AV-1..12) | Aggregate «Бэклог» and «Канбан»: one group or swimlane per project (now the default grouping, not the only one), open-state memory, cursor paging (Q-13 A), the move inside a group (Q-10 A), the card move inside a lane (Q-12 A), the grouped count read and the per-group read, the cap of 4 in flight per tab (Q-11) | F43 spec, BR-NV-20; US-10 |
| BR-NV-1, BR-NV-2, BR-NV-3 | Which menu entries and addresses this line adds (department `areas/:a/…`, team `teams/:t/workitems`, organization `…/workitems|boards|backlogs`) | F43 spec, the menu table |
| BR-NV-17, the clauses marked "from the second line" | This line owns exactly these: (1) the content of «Рабочие элементы», «Канбан» and «Бэклог» at the department (the projects placed in it) and at the organization (every active project); (2) a team chosen **together with a department** narrows the team's own feat-040 «Бэклог» and «Канбан» (and its «Рабочие элементы») to the projects placed in that department, shown as one group or swimlane per project, each in its project's rank order, with **no reorder while narrowed** (the Q-9 A hint) and the line «сужено: команда …, департамент …»; this changes an existing view, not only the new ones; (3) the team-level «Рабочие элементы» across the team's projects. A team with no department keeps feat-040's views as today | F43 spec, BR-NV-17; US-6, US-10 scenarios "A chosen team narrows" |
| BR-NV-13, BR-NV-15 | A deactivated project leaves every view; every read across projects takes the caller (signature test) | F43 spec |
| Budgets | NFR "Organization-wide list" and "Aggregate views" (H-4 A, Q-11) | F43 spec § NFR |
| Resolved | Q-6 A (the selector is the only team narrowing), Q-7 B, Q-8 A, Q-9 A, Q-10, Q-11, H-1 A (the entry clears filters, never selectors), H-4 A; Q-12 A and Q-13 A are taken from F43 `status.md` § Decisions (their boxes in F43 `spec.md` are still unticked) | F43 spec § Open questions, F43 status.md |

Design input already set (F43 `design.md` § 12): the layout, the accordions, drag and drop through `BacklogRow` and `BoardColumns`, the keyboard path. **Owed to this line's design by § 12:** the server-side concurrency limiter on the group and cell reads (per user 4 to 6, global about 40, 429 when full; the numbers are this line's), and the short ADR or amendment note for the cursor (opaque `nextCursor`, forward only), which deviates from ADR-0014.

### The team filter of the project's list, a stated requirement

Not a question: BR-NV-12, Q-6 A and US-6 decide it. The shipped `TeamFilter` («Команда», a multi-select over the project's teams, then «Без команды») on the project's «Рабочие элементы» is replaced, in this line, by one control, «Без команды». **Exclusive** means: choosing «Без команды» while a team is chosen in the team selector resets the team selector to «все» with the notice «Выбор сброшен: …» (the F43 reset notice); choosing a team while «Без команды» is on clears «Без команды». The project's **backlog and board** (`ViewFilterRow`) keep the shipped «Команда» control until feat-013, and for those two views the rule of BR-NV-17 (the selector and the control are one state, mod-020) is as today; a project's list and its backlog therefore differ in this one control until feat-013, recorded here.

## Business process (BPMN)

The grouping choice and the group read, with the failures the approved text leaves open (§ Delta).

```mermaid
flowchart TD
    S([The user opens «Бэклог», «Канбан» or «Рабочие элементы»]) --> Q[Grouping = remembered choice for this level, else the default; BR-AV-3]
    Q --> Q1{Grouping}
    Q1 -->|none| R0[One stream of rows or cards, no headers; read as one group, BR-AV-9]
    Q1 -->|department, project, team| H[One grouped read: headers' and column counts]
    R0 --> R
    Q1 -.->|the user changes the switch| Q    H --> G{A group or lane is open?}
    G -->|first visit: the first| R[Group read, at most 4 in flight per tab]
    G -->|restored, at most 10| R
    G -->|user opens one| R
    R --> L{Server accepts the read?}
    L -->|waits in the queue, then accepted| P
    L -->|refused: limit reached| X[Refusal handling, OQ-1; slot released while waiting]
    X --> R
    L -->|accepted| P[First 50 rows or cards in the project's rank order]
    P --> M{«Показать ещё»?}
    M -->|yes| C[Read after the cursor]
    C --> K{Answer}
    K -->|400 naming the field cursor| Kx[Reload the group from the first page, OQ-4]
    K -->|project not found: deactivated meanwhile| Dx[Drop the group, re-read the headers, OQ-4]
    K -->|ok| P
    M -->|no| E([Rows shown])
```

## Business rules

The stated requirement above, and the grouping rules below (the human's answers of 2026-11-03, in chat, before gate-2; the text is written for the recommendation of each open OQ-7..OQ-9 and follows the human's answer). «Level» below is the menu level of BR-NV-1: the most specific chosen object (project, else team, else department, else the organization). «Group» means a swimlane on «Канбан». The rules **amend BR-NV-20** (one group per project, the project's rank order, no merged list) and, for the team level, read BR-NV-17 clause 2 together with BR-AV-3; where they differ BR-AV wins on this line.

| # | Rule | Source |
|---|------|--------|
| BR-AV-1 | **The switch.** «Бэклог», «Канбан» and «Рабочие элементы» have a control «Группировка» with the options «Без группировки», «По департаменту», «По проекту», «По команде», one chosen at a time. There is no «По организации»: every view is inside one organization. The control sits in the view's header beside the narrowing line and is not a filter: it is not cleared by the entry, by a selector change or by filters (H-1 A is about filters and selectors only). Choosing an option re-reads the view from its first page; filters and selectors are kept; the grouping is **not in the address** (BR-NV-2 stands: a pasted link opens the receiver's own remembered or default grouping). | human 2026-11-03; BR-NV-2 |
| BR-AV-2 | **Which groupings a level offers** (only those that can give more than one group): organization — none, department, project, team; department — none, project, team; team — none, project; project — none, team. A dimension a selector fixes is not offered: at project level with a team chosen only «Без группировки» remains, and when a level offers a single option the control is absent. The offer is the same for the three views. | human 2026-11-03 |
| BR-AV-3 | **Default and memory.** Defaults: «Бэклог» and «Канбан» — **by project** at organization and department level (as BR-NV-20 approved) and at team level **when a department is chosen** (BR-NV-17 clause 2), **none** at team level with no department (feat-040's views stay as today, OQ-7 A) and at project level; «Рабочие элементы» — **none** at every level (the flat list approved in BR-NV-12). The choice is **remembered per user, organization, view and level** in the browser, as the open groups are (BR-NV-20), and restored on return; a remembered option the level or selectors no longer offer falls back to the default silently. A view's three memories (backlog, board, list) are independent. | human 2026-11-03; BR-NV-17, -20 |
| BR-AV-4 | **Which groups exist and their order.** *Department:* a group «Без департамента» **first** for the rows of projects placed at the top level (a project node may sit at the top level, ADR-0031, so it can occur), then the departments holding at least one row after the filters, by department name. *Project:* by name, then key (BR-NV-20 as approved). *Team:* «Без команды» **first** for items with no owning team, then the owning teams by team name. The group of rows with no department or no team goes on top (the human at gate-2, 2026-11-03), so it is also the group the first-group-open rule of BR-NV-20 opens. A group with no row after the filters has no group; with none at all the page says «Ничего не найдено» or the empty state. Each item is in exactly one group and appears once. Ties on a name are broken by the object's id, so the order is stable. | human 2026-11-03; BR-NV-20 |
| BR-AV-5 | **Header.** Name (and key for a project), and the row count after the filters; narrowed by a selector or a filter, «3 из 40» (BR-NV-20). The headers come from one grouped count read for the chosen grouping, with no read per group. With «Без группировки» there is no header and no lane: one list or one board; the view shows its count once as the list's or the board's own total, from the same count read. | BR-NV-20 |
| BR-AV-6 | **Row order inside a group and the move («Бэклог»).** *By project* (organization and department level, no team chosen, no filter narrowing): each group is the project's own backlog, in the project's rank order, with the in-group move of BR-NV-20 (Q-10 A, the codes and the part-loaded behaviour of OQ-6 A); no move across groups. *Every other grouping and «Без группировки» at organization and department level:* rows go by change time descending, then number descending, then project key (BR-NV-12's order), **no reorder at all**; the move menu and the drag handle are absent, and the view says «При этой группировке строки идут по времени изменения, порядок не меняется. Выберите «По проекту», чтобы менять его.» once under the page header (the place of the Q-9 hint, design D-1). *By project while a team or filter narrows:* the rank order is kept and the Q-9 A hint of BR-NV-17 shows as approved. *Team and project level:* «Без группировки» is the view as shipped today (project: its rank and move; team: feat-040's order, no reorder while a selector narrows), OQ-8 A; **by team at project level** and **by project at team level** follow the rows above (change time for by team; the project's rank, no reorder while narrowed, for by project at team level). No sorting in any grouped or aggregate backlog (columns not sortable, BR-NV-20). | human 2026-11-03; BR-NV-17, -20 |
| BR-AV-7 | **«Канбан».** *Grouped:* one collapsible swimlane per group of BR-AV-4, the columns the organization process's states, a cell (lane × state) shows at most 50 cards then «Показать ещё», the column header counts all its cards, the header, open-state memory and filters of BR-NV-20; a card moves between columns **inside its lane** by the item's project's code (Q-12 A); no reorder. *«Без группировки»:* one board without lanes, the columns' cells holding up to 50 cards then «Показать ещё», the same card move, the same codes. Cards in a cell: by project, the project's rank (as approved); in every other grouping and without grouping, change time as BR-AV-6. A board has no reorder, so the "no reorder" hint is not shown on it. | human 2026-11-03; BR-NV-20 |
| BR-AV-8 | **«Рабочие элементы» grouped.** *«Без группировки»:* exactly the list of BR-NV-12 — the page envelope, the pager, the total, the fixed order of BR-WS-12 (change time descending, then number; the list has no sort control, feat-003 puts column sorting out of scope). *Grouped:* collapsible groups of BR-AV-4 with the open-state memory, the headers' counts and the first-group-open rule of BR-NV-20, each group paged by the cursor ("Показать ещё", 50 a page, forward only), rows by change time descending, then number descending, then project key; **no pager, no page numbers and no sorting** (OQ-9 A); the filters, the keyword rules and the selectors of BR-NV-12 apply to every group and change the counts. Both forms show the same rows for the same filters; no create command. | human 2026-11-03; BR-NV-12 |
| BR-AV-9 | **Reads.** Opening a grouped view issues one grouped count read for the chosen grouping (the headers, and the board's column counts), and the rows of a group load when it opens: one bounded read per group, 50 rows or cards, the next page the same read with the cursor of the previous one (Q-13 A, ADR-0036). **Every grouping uses the cursor**, and the cursor names a position in the group's order: the project's rank for by project, and change time (with number and key) for every other group and for «Без группировки» (read as one group), so a cursor never fails because its last row moved or was deleted; a malformed or foreign cursor is the existing 400. A row whose change time moved it above the cursor is not seen until the group is reopened or the page refreshed (the approved stale-row rule); no row is shown twice (the panel drops a held row by id). The filter parameters of OQ-5 A apply to every grouping's reads. **The limiter and the cap apply to every group read** of every grouping, «Без группировки» included: at most 4 in flight per tab, the server limit per user and per process (OQ-2 A, OQ-3 A), a restored set of at most 10 open groups, the refusal and recovery of OQ-1 A and OQ-4 A. The open-state memory is kept **per user, organization, view, level and grouping** (a group is named by its department, project or team, «Без команды» and «Без департамента» by their own names), 10 at most; a remembered open group no longer in the set is ignored. | BR-NV-20; ADR-0036 |
| BR-AV-10 | **Change between two reads.** What a view shows is what the last read said; the panel does not move rows between groups by itself. An item whose owning team changes is in its new team group at the next read of the headers and of that group, and its old group counts one fewer then; a project moved to another department takes all its rows to the new department's group (or to «Без департамента» and back) at the next read; a department that loses its last row has no group at the next headers read, and its open-state entry is ignored. Until then a held row stays where it was and its commands still work by the item's own project (a command on a row that no longer exists is refused as today). The user's own card move updates the card in its cell in place; the cell is re-read from its first page when it is part-loaded (design D-3 A), and the card may then sit at a new change-time position. | human 2026-11-03; BR-NV-20 |
| BR-AV-11 | **Budgets stated for every grouping.** At 200 projects and 1 000 000 items, as the NFR of F43: the grouped count read within 2 s unfiltered and with a team, assignee or state filter; each group's first 50 rows (a cell's first cards) and each «Показать ещё» within 2 s from the start of the read, **also for a department group, a team group and the one stream of «Без группировки»**, which each span many projects (the approved sentence "it reads one project" holds only for by project and is not assumed here); first screen within 4 s; statement counts constant for opening the page and per group opened, whatever the number of groups or projects. If a design finds a grouping cannot meet 2 s at that volume it is reported as an OQ at the design stage, not relaxed here. | BR-NV-20; F43 NFR |
| BR-AV-12 | **What this amends** (recorded, not edited in F43): BR-NV-20 "one group per project" becomes the default (BR-AV-3) with the groupings of BR-AV-2; its "no merged list, no ordinal" holds for by project and is replaced for the other groupings by BR-AV-6's change-time order; its "Which groups exist" and "Group order" are extended by BR-AV-4; BR-NV-17 clause 2 is unchanged but now only fixes a default (team + department, by project); BR-NV-12 is unchanged for «Без группировки» and gains the grouped form. US-10's scenarios stand for the default grouping. | human 2026-11-03 |

## Operation completeness

As F43 `spec.md` § Operation completeness, rows "Work item list" and "Board and backlog views, aggregate": no create command on the aggregate views; a move inside a group and a card move inside a lane by the item's project's code; no delete or cancel. The behaviour of a move in a part-loaded group is OQ-6. Added: the grouping choice is read and written by the user (a browser memory, BR-AV-3), cleared by nothing but the user's next choice; a backlog row move exists only under by project (BR-AV-6).

## Non-functional requirements

As F43 `spec.md` § Non-functional requirements: the "Organization-wide list" and "Aggregate views" budgets, statement counts constant, read concurrency capped at 4 per tab. Added by the Delta:

- **The limiter, testable:** one user with two tabs, each restoring 10 groups (so 8 group reads in flight at once), receives no 429; a ninth concurrent read from the same user may be waited on or refused (OQ-3). A test with a fake clock asserts both sides.
- **The limiter's key and scope (behaviour fixed, numbers the design's):** the key is the signed-in user (the Keycloak subject), the same across organizations, tabs and devices; the scope is **per API process**, since the pool it protects is per process (a replica adds its own pool and its own limiter). The global limit is likewise per process.
- **Every grouping, one limiter (BR-AV-9, BR-AV-11):** a test restores 10 open groups under each of the four groupings in one tab and asserts at most 4 reads in flight and the constant statement count per group; a test of the cursor under change-time order asserts no row twice and none lost when an item's change time moves between two pages.
- **The refusal's contract:** a new catalogue entry for the refusal (status 429, Russian and Kazakh text) is owed to `docs/error-codes.md` and `contracts/errors/*.json`, with the wait it asks for in the standard `Retry-After` header.

## Out of scope

- The views' filter bar and its memory (feat-013) beyond OQ-5; the project set and visibility (feat-041); per-object codes (mod-019); the overview's figures and the switch «Включая дочерние области» (feat-042); sprints (mod-007).
- Everything F43 already excludes: a «Проект» filter on the cross-project list, creating an item from it, a batch move, views of a sub-area or a project's node.
- Rewording any approved rule or story of F43 (the grouping amendment is this line's own rule, BR-AV-12).
- Grouping by organization, by state, assignee or any field other than department, project and team; a user-defined grouping; grouping in the address; sorting inside a grouped list (OQ-9); a reorder under any grouping but by project.

## Dependencies

feat-043 (the shell, selectors, menu table, addresses; merged, #49, `9391d048`), feat-003 (work-item list and filters), mod-006 (levels «Эпики», «Фичи», «Истории»), mod-017 (backlog and team views), mod-020 (`teamId`), feat-040 (team boards and backlogs). All six are merged on origin/main, as the orchestrator's check recorded in this task's `status.md` Log.

## Decomposition for the backlog

| id | Task | size | deps |
|----|------|------|------|
| `feat-NNN-aggregate-level-views` | US-6 and US-10 as one task: the read side (grouped count read, group and cell reads, cross-project list), the limiter, the cursor, and the panel entries and views | L (route confirmed by the human: an ADR is owed for the cursor and for the limiter) | feat-043 |

No further tasks: the queue line already exists as `feat-048-aggregate-level-views`.

## Delta

What F43's approved text leaves open and this line must settle. Checked and dropped as already settled: the concurrency cap's role and the pool argument (BR-NV-20); the cursor's ordering and stale-row behaviour (BR-NV-20, US-10); the menu table and addresses (the shipped `MENU_TABLE` matches F43's table); the ADR-0014 deviation as a decision (the design's); the project list's team control (a stated requirement above).

- **OQ-1 (resolved A, the human, gate-1 2026-11-03) What the user sees when the server refuses a group read (429).**
  - (A, recommended) The panel retries by itself, waiting the time `Retry-After` names (else 1 s, doubling, at most 3 tries), the group showing its loading state; **a group waiting to retry releases its slot of the tab's 4**, so others are not blocked behind its backoff. After the last try the group shows inline «Не удалось загрузить элементы проекта. Сервис занят, попробуйте ещё раз.» with «Повторить»; other groups are unaffected. Why: a refusal passes in seconds, a bounded retry never loops, and the button is the exit.
  - (B) No automatic retry: the message and «Повторить» at once. Simpler; a restored set of 10 groups on a busy moment needs many clicks.
  - (C) The generic error message. Loses the cause.
- **OQ-2 (resolved A, the human, gate-1 2026-11-03) What the limiter covers.**
  - (A, recommended) The group and cell reads and their «Показать ещё» (the fan-out), per user and globally. The grouped count read and the lists are single reads. **Recorded accepted risk:** the cross-project list's title keyword read (budget up to 5 s) is the longest-held connection of this line and stays outside the limiter.
  - (B) As A, and the cross-project list's keyword read falls under the **global** limit only (not the per-user one), so the longest read is counted against the pool.
  - (C) Every read of this line, the grouped count included; a refused count read leaves the headers empty and the page unusable.
- **OQ-3 (resolved A, the human, gate-1 2026-11-03) The per-tab value of 4 and the server's behaviour at the per-user limit.**
  - (A, recommended) 4 per tab stays. The per-user limit is 6 with a **short bounded wait queue** (a read waits up to a few seconds for a free place before it is refused), so two tabs (8 reads) wait briefly instead of being refused, and a 429 stays abnormal. The queue length and wait are the design's; the behaviour is the NFR above.
  - (B) No queue; the per-user limit is raised to at least 8 so two tabs never meet it. Simple; a third tab or a script meets refusals at once, and the pool's share per user grows.
  - (C) No queue and per-user 6, as design § 12 has it: a second tab restoring 10 groups meets refusals, handled by OQ-1. Fails the NFR above.
- **OQ-4 (resolved A, the human, gate-1 2026-11-03) Recovery when a read after the first page fails.**
  - (A, recommended) (1) A 400 whose validation error names the field `cursor` reloads the group from the first page, dropping the rows held, and re-reads the header counts, without a message; a 400 naming any other field (a level, a filter) is shown as an error and does not reload. (2) A refusal keeps the rows held, shows «Показать ещё» again and the OQ-1 message under the group. (3) «Проект не найден» on «Показать ещё», a row move or a card move (the project was deactivated while its group was open) removes the group or lane and re-reads the headers, with the notice «Проект больше недоступен: группа скрыта.»
  - (B) The same inline message and «Повторить» for all three cases, rows kept. The user chooses; a stale cursor fails again, and a deactivated project's group stays visible.
- **OQ-5 (resolved A, the human, gate-1 2026-11-03) Which filters the aggregate views and the cross-project list have in this line (feat-013 is unmerged).** Feat-003's filter bar is on the work-item list; the board and backlog filter bar is feat-013's. The approved text still speaks of filters ("«3 из 40»", the keyword scenario, "with a team, assignee or state filter" in the budget).
  - (A, recommended) «Рабочие элементы» (organization, department, team) has feat-003's filters, as US-6 says. The grouped, group and cell reads of «Бэклог» and «Канбан» **accept feat-003's filter parameters now** (so the budgets with a team, assignee or state filter are measured on the reads), but the aggregate views have **no filter bar until feat-013**: only the selectors narrow them, the headers read «3 из 40» when a selector narrows, and the filter clauses of US-10 ("Filters change the counts…", the keyword) are tested at the read level in this line and in the panel when feat-013 lands. The budget "with a team, assignee or state filter" is read as the read with those parameters. The project's backlog and board keep the shipped `ViewFilterRow`.
  - (B) No filter parameters on the aggregate reads and no filter bar: only the selectors narrow; the filtered budgets and US-10's filter scenarios wait for feat-013. Smaller, but feat-013 then changes the reads' contract.
  - (C) Build the aggregate views' filter bar here. Scope growth, against the human's answer for this line (the queue line only).
- **OQ-6 (resolved A, the human, gate-1 2026-11-03) A move in a part-loaded group.** A group holds only the pages loaded so far; the shipped move menu («В начало», «Выше», «Ниже», «В конец») and the drag target were written for a whole backlog.
  - (A, recommended) «В конец» means the end of the project's backlog: the row leaves the loaded set and appears once, at the end, when the pages reach it. «Ниже» on the last loaded row is disabled until «Показать ещё» loads more; a drop lands only between loaded rows (the end of the loaded rows is not a drop target while more rows exist). The user's own move across the cursor never shows the row twice (the panel drops a held row by id) and never loses it. Scenarios: «В конец» in a 120-row group with 50 loaded; «Ниже» on row 50; a drop below row 50.
  - (B) Moves are limited to the loaded rows: «В конец» and a drop at the end are absent while more rows exist. Simpler to test; the user must load every page to move a row to the end.

- **OQ-7 (resolved A, the human, 2026-11-03) The default grouping of «Бэклог» and «Канбан» at team level.** BR-NV-17 clause 2 (approved) says a team with no department keeps feat-040's views as today, and a team with a department gets groups per project.
  - (A, recommended) Default **none** for a team with no department (feat-040's views untouched until the user chooses «По проекту»), **by project** with a department; BR-AV-3 is written so. Why: it honours the approved clause and changes nothing for a user who never touches the switch.
  - (B) Default by project at team level always. Consistent across levels, but changes feat-040's shipped team views for everybody and contradicts BR-NV-17 clause 2 (so BR-NV-17 would need the amendment too).
- **OQ-8 (resolved A, the human, 2026-11-03) Order under «Без группировки» at team and project level.** The human said: outside by project, rows go by change time with no reorder. At project level the project's own backlog has a rank and a move (shipped), and the team's view has feat-040's order.
  - (A, recommended) «Без группировки» at project and team level is the shipped view (rank and move for a project; feat-040's order for a team); change time with no reorder applies at organization and department level and to by team at project level (BR-AV-6). Why: a single project's backlog without its rank is a regression of a shipped, working view.
  - (B) Change time, no reorder, everywhere. Literal reading; the project's «Бэклог» then loses its reorder under the default «Без группировки», or the default there must be moved to by project (which a project does not offer).
- **OQ-9 (resolved A, the human, 2026-11-03) Sorting in a grouped «Рабочие элементы».** The flat list keeps BR-WS-12's fixed order and has no sort control (the orchestrator's clarification, 2026-11-04; see status.md Decisions).
  - (A, recommended) Sorting is off when grouped (BR-AV-8): a group reads by a cursor on change time, as the aggregate backlog's columns are not sortable (BR-NV-20).
  - (B) Keep sorting inside groups. Needs a cursor per sort column and an index per column at a million rows; the keyword and assignee sorts do not meet 2 s there.

OQ-1, OQ-4 and OQ-6 may become scenarios in a further story if the human wants them as acceptance criteria; US-11 covers the grouping switch only.
