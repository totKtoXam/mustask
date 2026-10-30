# ADR-0032: Give every area, project and team a page of its own, hold the work context as four selectors, and build the left menu from the most specific chosen object

- Status: proposed
- Date: 2026-10-30 (reworked after the gate-2 rejection of the same day)
- Context: `feat-043-area-project-team-navigation`, stage 3 (design) — [spec](../features/feat-043-area-project-team-navigation/spec.md) BR-NV-1..18, OQ-7 A (gate-1) and the human's gate-2 requirement (status.md § Decisions); [design](../features/feat-043-area-project-team-navigation/design.md). **Replaces** [ADR-0024](0024-app-shell-and-visual-language.md) § 2 (the fixed prefixes and the three-level switcher) and the "context it needs is real" clause of § 1. **Consolidates** [ADR-0028](0028-matrix-organization-model.md) rule 7 (addresses, switcher, breadcrumbs, old addresses; its "one list grouped by kind" switcher and its "trail" are replaced by § 5 below) and [ADR-0031](0031-typed-area-tree.md) rule 5 (the area prefix serves every node type). Nothing else in those texts changes. Markers on them are written by the implementation (spec US-9). Only the human sets `accepted`.

## Problem

Four lines after feat-043 (mod-019, feat-041, feat-042, mod-007) add sections for areas, projects and teams. Without one contract each would pick its own address segment, its position in the section list and its rule for who sees it. Three older texts disagree: ADR-0024 § 2 nests a team under a project, ADR-0028 rule 7 leaves the section list open and keeps interim pages "until feat-043", and ADR-0031 rule 5 adds a project node served by the area prefix. The first design of this line gave each object a page with a tab strip; the human rejected it: reaching «Общие», «Рабочие элементы», «Канбан», «Бэклог» or «Команды» of a project must not need a list, the header must be four selectors (organization, department, team, project), each «все» or a chosen object, and the left menu must hold the sections of what is chosen, with «Настройки» where they always were. The remembered context has no key and no fallback in any ADR, and the switcher still carries feat-040's interim team level.

## Options considered

### Option 1: pages with tabs, one switcher (the first design)

- Pros: one object per address; a small context (organization plus one object).
- Cons: rejected by the human: a list stands between the user and a project's sections; the context cannot say "MARS inside A".

### Option 2: four selectors, the menu of the most specific object (chosen)

- Pros: any section of any object is one choice away; narrowing (a team inside a project) is the model, not a filter on each page; the section list stays one table.
- Cons: a selection may name three objects while an address names one primary object, so the others ride in the query; the selectors must narrow each other and reset a choice that no longer fits.

### Option 3: the union of the chosen objects' menus

- Pros: nothing is hidden.
- Cons: the same names twice («Канбан» of the team and of the project), a long menu; rejected (spec Q-4).

## Decision

Option 2.

**1. Addresses.** All under `/organizations/:organizationId/`: the organization's own pages (`overview`, `workitems`, `areas`, `projects`, `teams`, `members`, `settings/…`) and one prefix per kind, `areas/:areaId/…`, `projects/:projectId/…`, `teams/:teamId/…`. **The prefix names the most specific chosen object** (project, else team, else department); the other chosen selectors ride as the query `teamId` and `departmentId` (the form mod-020 founded), so `…/projects/:p/backlogs/stories?teamId=…` is project A narrowed to a team. Single-object addresses are exactly the approved ones. A project's «Структура» is the node's page on the area prefix (ADR-0031 rule 5) and carries the project and its department in the selectors; no second address exists. No address nests one object under another. Owned objects stay under their owner (a work item under its project, a sprint under its team). Pages with no organization («Главная», instance settings) keep their addresses and touch no selection. Route ids are untyped strings that the page validates and compares the way the router does. A bare object address opens the first entry of the object's menu. A selector value that names nothing, or does not fit the primary object, answers not found for its level.

**2. The menu is the contract.** The left menu is built from the most specific chosen object; an entry is a path segment under that object's prefix. The table is the only list; a line adds its row where it says, and the entry is absent (not disabled, not a placeholder) until then. «Настройки» is not a row: it is the shell's derived last entry (mod-015 BR-19) and is always last. "Code" is the permission the page asks for; until mod-019 it is the organization role's bundle. There are no tabs on an object's page.

| Most specific chosen | Entries in order (segment) | Added later, and where |
|---|---|---|
| Nothing | «Обзор» (`overview`), «Рабочие элементы» (`workitems`), «Области» (`areas`), «Проекты» (`projects`), «Команды» (`teams`), «Участники» (`members`) | - |
| Department | «Обзор» (`overview`), «Проекты» (`projects`), «Команды» (`teams`) | «Канбан» (`boards/:level`), «Бэклог» (`backlogs/:level`) before «Проекты» (feat-042); «Участники» (`members`, mod-019); «Аналитика» (`analytics`, feat-044) |
| Team | «Обзор», «Канбан», «Бэклог», «Проекты» (`projects`), «Участники и доступ» (`members`) | «Спринты» (`sprints`, mod-007) after «Бэклог»; grants join `members` (mod-019) |
| Project | «Общие» (`general`), «Структура» (the node: `areas/:nodeId/overview`), «Рабочие элементы» (`workitems`), «Канбан», «Бэклог», «Команды» (`teams`) | «Спринты» (`sprints`, mod-007) after «Бэклог»; «Участники и доступ» (`access`, mod-019) |
| A project's node or sub-area opened from «Области» | the project's menu, «Структура» marked | «Канбан», «Бэклог» of the node (feat-042) |

«Настройки» opens the settings navigation (the main menu as a rail): the group «Организация» («Общие», «Процесс»), the instance sections, and for a holder of the write code one group per chosen kind («Департамент», «Команда», «Проект»; «Область» while a node or sub-area page is open), each with «Параметры» at `…/settings` under that object's prefix. «Проекты», «Команды», «Области» and «Участники» are menu entries of the nothing-chosen menu, not settings. A segment the menu lacks answers «Страница не найдена»; one whose code the user lacks, «Доступ ограничен», and its entry is absent.

**3. Not found by level.** The outermost missing level answers (organization, then the object the prefix names, then a selector in the query); a deleted, deactivated or foreign object answers as one that does not exist. A work item's path address names its project first.

**4. Retired addresses.** `…/settings/teams[/:t]`, `…/settings/areas[/:a]`, `…/settings/projects[/deactivated]`, `…/settings/members`, `…/projects/:p/settings/general|teams|areas` answer «Страница не найдена», redirect nowhere, and are held in one table that a test reads. A non-member sees «Организация не найдена» first. No link in the panel targets one. (`…/projects/:p/settings` itself is the new «Параметры».)

**5. The work context is four selectors.** The header holds, in this order, organization, department, team, project; each shows «все» or its chosen object and is choosable. The selection is the organization plus at most one department, one team and one project. It builds the menu (§ 2) and narrows the content: a chosen team narrows a project's items to those the team owns (the one `teamId` state of mod-020), a project «все» with a team chosen spans the team's projects, and a department narrows the selectors' offers and the lists «Проекты» and «Команды» but no work-item view before feat-042. The selectors narrow each other: a project is offered in the chosen department and linked to the chosen team, a team when linked to the chosen project and to the chosen department (directly, or through a project placed in it), a department when it holds the chosen project and is linked to the chosen team. A chosen value the others no longer fit resets to «все» with the notice «Выбор сброшен: <вид и название>.». Choosing a value keeps the current entry when the new menu has it (same segment, same level, the old scope's filters dropped), else opens the new menu's first entry. Choosing another organization opens its «Обзор» with «все» in the other three; «Все организации» clears all four. **Opening an object from a row or a link makes it the most specific one:** selectors more specific than it reset to «все» without a notice, the less specific ones that still fit stay (from A's «Команды» opening MARS leaves MARS alone with the department; from a department's «Проекты» opening A keeps the department). A project's node and sub-areas set the project selector and the department selector to the project's place; nodes and sub-areas are never offered. The project's default team chooses nothing; feat-040's interim team level and mod-020's fallback team retire.

**6. The remembered selection.** Stored in the browser's `localStorage`, per user and per organization, under `mustask.context.v2.<sub>.<organizationId>` with the value `{ "department": id | null, "team": id | null, "project": id | null }`, and the last organization under `mustask.context.v2.<sub>.organization`; `<sub>` is the signed-in user's subject claim. It is written when an address sets the selection or the user chooses (the last write wins for a reload) and read when a page carries no object of its own; the address wins in its tab. An unreadable or malformed value is absent. Fallback, with one notice «Ранее выбранный контекст недоступен. Выбрано: <название>.» (without the name when nothing is left): each remembered object that is gone or no longer offered drops to «все», the organization stays; the organization gone or the user no longer a member, then the user's first organization by name; no organization, then no selection and «Главная». Opening an address of another organization changes the selection with no notice; a notice only reports a vanished remembered object, and an address that names that very object answers not found and drops it as well.

## Consequences

- **Simpler:** later lines add one row to the menu table; a project's sections are one choice from any page; narrowing is the model, so no page re-implements a team or department filter.
- **Harder:** the selectors need an offer/reset algorithm with a test table; a link must carry the selection; the settings navigation is built from the selection; every old link breaks at once (no redirect, by ADR-0028 rule 7's waiver) and the MR names the e2e specs it breaks (E2E is paused). `localStorage` is per browser.
- **Locked in for the long term:** the menu segments in the table (a changed segment breaks shared links), the query names `teamId` and `departmentId`, and the storage key's version, `v2`.
- **Spawned:** nothing new in the queue; US-9 of feat-043 marks ADR-0024 §§ 1, 2 and 4, ADR-0028 rule 7 and ADR-0031 rule 5 with a pointer here. `tech-020-ui-kit-m5-legacy-out` takes feat-043 as a dependency.
- **Revisit when:** a kind gains a nested object that needs its own page (a sprint is the first candidate); the selection must follow the user across browsers (a server-side preference); or the four selectors stop fitting the header at the widths the panel supports.
