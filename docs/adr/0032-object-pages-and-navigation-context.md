# ADR-0032: Give every area, project and team a page of its own with a fixed section list, and hold the work context as one remembered object

- Status: proposed
- Date: 2026-10-30
- Context: `feat-043-area-project-team-navigation`, stage 3 (design) — [spec](../features/feat-043-area-project-team-navigation/spec.md) BR-NV-1..16, OQ-7 A (gate-1, 2026-10-30); [design](../features/feat-043-area-project-team-navigation/design.md). **Replaces** [ADR-0024](0024-app-shell-and-visual-language.md) § 2 (the fixed prefixes and the three-level switcher) and the "context it needs is real" clause of § 1. **Consolidates** [ADR-0028](0028-matrix-organization-model.md) rule 7 (addresses, switcher, breadcrumbs, old addresses) and [ADR-0031](0031-typed-area-tree.md) rule 5 (the area prefix serves every node type). Nothing else in those texts changes. Markers on them are written by the implementation (spec US-9). Only the human sets `accepted`.

## Problem

Four lines after feat-043 (mod-019, feat-041, feat-042, mod-007) add sections to the pages of areas, projects and teams. Without one contract each would pick its own address segment, its own position in the section list and its own rule for who sees it. Three older texts also disagree: ADR-0024 § 2 nests a team under a project, ADR-0028 rule 7 says "no prefix nests one object" but leaves the section list open and keeps interim pages "until feat-043", and ADR-0031 rule 5 adds a project node that is served by the area prefix. The remembered context (BR-MX-28) has no key and no fallback in any ADR, and the switcher still carries feat-040's interim team level.

## Options considered

### Option 1: one more amendment note on ADR-0028 rule 7

- Pros: no new document.
- Cons: the rule would be amended by a third text (rule 7, ADR-0031 rule 5, this note); ADR-0024 § 2 stays contradicted; the section list and the context key have no home.

### Option 2: a new ADR that states the contract once (chosen)

- Pros: one text holds the addresses, the section list, the retirement list and the context; the four later lines extend a table instead of arguing.
- Cons: one more document; the three older texts carry a pointer.

## Decision

Option 2.

**1. Addresses.** The panel has four prefixes, all under `/organizations/:organizationId/`: the organization's own pages (`overview`, `workitems`, `areas`, `projects`, `teams`, `members`, `settings/…`), and one per kind, `areas/:areaId/…`, `projects/:projectId/…`, `teams/:teamId/…`. An address names the organization, one object and a section; no prefix nests one object under another. Owned objects stay under their owner (a work item under its project, a sprint under its team). A project's node is served by the area prefix, and no second address exists. Pages with no organization («Главная», instance settings) keep their addresses and touch no context. Route ids are untyped strings that the page validates, compared the way the router compares them. A bare object address opens the object's first section.

**2. The section list is the contract.** A section is a path segment under the object's prefix. The table is the only list; a line adds its row where the table says and the section is absent (not disabled, not a placeholder) until then. "Code" is the permission the page asks for; until mod-019 it is the organization role's bundle.

| Kind | Sections in order (segment) | Added later, and where |
|---|---|---|
| Department | «Обзор» (`overview`), «Проекты» (`projects`), «Команды» (`teams`), «Настройки» (`settings`, admin) | «Канбан» (`boards/:level`), «Бэклог» (`backlogs/:level`) before «Проекты» (feat-042); «Участники» (`members`, mod-019) before «Настройки»; «Аналитика» (`analytics`, feat-044) before «Настройки» |
| Project's node, sub-area | «Обзор», «Настройки» (admin) | «Канбан», «Бэклог» after «Обзор» (feat-042) |
| Project | «Общие» (`general`), «Рабочие элементы» (`workitems`), «Канбан», «Бэклог», «Команды» (`teams`), «Настройки» | «Спринты» (`sprints`, mod-007) after «Бэклог»; «Участники и доступ» (`access`, mod-019) before «Настройки» |
| Team | «Обзор», «Канбан», «Бэклог», «Проекты» (`projects`), «Участники и доступ» (`members`), «Настройки» | «Спринты» (`sprints`, mod-007) after «Бэклог»; grants join `members` (mod-019) |

«Настройки» is always last and shown to the holder of the write code only. A section the kind lacks answers «Страница не найдена»; one whose code the user lacks answers «Доступ ограничен» and is absent from the tabs. Section order is the tab order.

**3. Not found by level.** The outermost missing level answers (organization, then the one object the address names); a deleted, deactivated or foreign object answers as one that does not exist. A work item's path address names its project first.

**4. Retired addresses.** `…/settings/teams[/:t]`, `…/settings/areas[/:a]`, `…/settings/projects[/deactivated]`, `…/settings/members`, `…/projects/:p/settings/general|teams|areas` answer «Страница не найдена», redirect nowhere, and are held in one table that a test reads. A non-member sees «Организация не найдена» first. No link in the panel targets one.

**5. The work context is the organization and at most one area, project or team.** It is shown by the switcher and is the start of a choice; it narrows no page and no list. A department's page sets it to the department; a project's page, its node's page and a sub-area's page set it to the project; a team's page, to the team; an organization page and «Рабочие элементы» leave the object as it was. The address wins in its own tab. The switcher offers the organization alone, the user's own teams, the departments, the projects and the other teams; choosing an object of the kind the page already shows opens the same section and level with no query, any other kind opens the object's first section the user may open; choosing another organization opens its «Обзор». feat-040's interim team level and mod-020's fallback team retire; the project's default team affects creation only (BR-MX-10, BR-MX-11).

**6. The remembered context.** Stored in the browser's `localStorage`, per user and per organization, under `mustask.context.v1.<sub>.<organizationId>` with the value `{ "kind": "area" | "project" | "team" | null, "id": string | null }`, and the last organization under `mustask.context.v1.<sub>.organization`; `<sub>` is the signed-in user's subject claim. It is written when an address sets the context or the user chooses in the switcher (the last write wins for a reload). It is read when a page carries no object of its own. An unreadable or malformed value is treated as absent. Fallback, in order, with one notice «Ранее выбранный контекст недоступен. Выбрано: <название>.» (without the name when nothing is left): the remembered object gone or no longer offered, then the organization alone; the organization gone or the user no longer a member, then the user's first organization by name; no organization, then no context and «Главная». Opening an address of another organization changes the context with no notice; a notice only reports a vanished remembered object, and an address that names that very object answers not found and falls back as well.

## Consequences

- **Simpler:** four later lines each add one row and one page; a link names its object once; a reload keeps the place; the switcher has one list.
- **Harder:** every old link breaks at once (no redirect, by ADR-0028 rule 7's waiver); the MR names the e2e specs it breaks (E2E is paused). `localStorage` is per browser, so another browser starts from the first organization.
- **Locked in for the long term:** the section segments in the table (a changed segment breaks shared links) and the storage key's version, `v1`.
- **Spawned:** nothing new in the queue; US-9 of feat-043 marks ADR-0024 §§ 1, 2 and 4, ADR-0028 rule 7 and ADR-0031 rule 5 with a pointer here. `tech-020-ui-kit-m5-legacy-out` takes feat-043 as a dependency.
- **Revisit when:** a kind gains a nested object that needs its own page (a sprint is the first candidate); the context must follow the user across browsers (a server-side preference); or an organization has so many objects that the switcher's single list stops working.
