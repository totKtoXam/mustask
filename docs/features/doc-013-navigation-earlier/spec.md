# Spec: Navigation right after the area workspace (doc-013)

## Context and goal

doc-012 (`docs/features/doc-012-matrix-organization-model/`, merged 2026-10-17) put `feat-043-area-project-team-navigation` last, after mod-007 and feat-013. Every line before it therefore named an interim entry in the organization's or the project's settings, which feat-043 was to move onto the objects' own pages at the end.
The human, 2026-10-18: «Текущее переключение организация/проект/команда - максимально неудобное». The human first kept the old order, then reversed it: «Давай всё таки 043 раньше».
Goal: the navigation model of BR-MX-27, BR-MX-28 and US-10 lands right after `mod-018-area-workspace`. The top-level «Области», «Проекты» and «Команды», the object pages with their sections, the breadcrumbs, the switcher and the remembered context all come early. The lines after it build their UI on the objects' own pages instead of adding interim entries. This task amends doc-012's decomposition, its stories' owed-by clauses and the ADR consequences that name the order. It also hands the orchestrator the reworded queue lines.

## Roles

- The human approves the new order and the open questions at gate-1, and merges (gate-3).
- The orchestrator rewords the queue lines after gate-1, in a queue commit to main (§ Queue changes).
- The analysts of the delivery lines read the amended doc-012.
- No product role changes: § Roles of doc-012 stands.

## Stories

None of its own. This is a docs task: it amends doc-012's US-10 and US-6 (§ Edits). There is no process to draw, so there is no BPMN.

## The decision

The chain was feat-040 → mod-018 → mod-019 → feat-041 → feat-042 → mod-007 → feat-013 → feat-043. It becomes **feat-040 → mod-018 → feat-043 → mod-019 → feat-041 → feat-042 → mod-007 → feat-013**. feat-039, mod-017 and feat-028 have merged. The parallel lane mod-005 → feat-003 is untouched, and feat-013 still waits for feat-003. `feat-044-area-analytics` stays outside the chain.

## Business rules

| # | Rule | Source |
|---|------|--------|
| BR-NE-1 | **Order.** The chain is the one above. Each line still ships on its own and reaches past no line before it (doc-012 § Decomposition). | the human, 2026-10-18 |
| BR-NE-2 | **Interim entries.** A line before feat-043 names its interim entry and address, and feat-043 retires each one. A line after feat-043 adds its sections to the objects' own pages, at their final addresses, and names no interim entry. | doc-012 § Decomposition, amended |
| BR-NE-3 | **Absent sections.** A section whose content a later line brings is absent until that line adds it. It is not disabled and not a placeholder, as US-10 already says of an entry the user may not open (OQ-4). | US-10; ADR-0024 § 1 |
| BR-NE-4 | **Owed clauses.** A US-10 clause that needs a grant, a visibility other than «Организация», an area view or a sprint is owed by mod-019, feat-041, feat-042 or mod-007. That line adds the section and re-runs the clause. This is doc-012 § Stories' owed-by rule, unchanged. | doc-012 § Stories |
| BR-NE-5 | **No visibility yet.** feat-043 shows every project of the organization: until feat-041, every project is visible to every member (ADR-0029 § 8). feat-041 then applies the set form to feat-043's surfaces, as part of "every surface that exists when it merges". | ADR-0029 §§ 7, 8 |
| BR-NE-6 | **Nothing else in doc-012 changes.** BR-MX-1..-28, the fate table, the NFRs, out of scope and the BPMN stand. What moves: the order paragraph, the Surface header, the deps, Task, size, Rewrites and Breaks cells, and the owed-by clauses. Two scope changes follow the open questions: the organization's «Обзор» takes feat-045 in (OQ-1), and feat-040 keeps mod-020's fallback (OQ-2). | - |
| BR-NE-7 | **The ADRs.** An accepted ADR changes only in what its consequences assign to a delivery line and in which of its pages are interim. Every decision of ADR-0028 and ADR-0029 stands: four addresses, no redirect, retirement by «Страница не найдена», the set form on every listed surface, the answer order. Each edit is marked "doc-013, 2026-10-18", and no superseding ADR is owed (`docs/adr/README.md`, Process 3). | docs/adr/README.md |

## The lines: old and new

| Line | Old position · deps | New position · deps | What changes in its scope |
|---|---|---|---|
| `feat-040-team-surfaces-across-projects` | 4th · feat-028 | 4th · feat-028 | Its interim team level of the switcher stays (OQ-3). Whichever of it and mod-020 merges second keeps mod-020's fallback team on that level (OQ-2) |
| `mod-018-area-workspace` | 5th · feat-028 | 5th · feat-028 | None. Its area pages in the organization settings and the project's area links in the project's settings stay interim, now for one line |
| `feat-043-area-project-team-navigation` | 11th, last · mod-007, feat-013 | **6th · feat-040, mod-018** | It moves only what feat-039, mod-017, feat-028, feat-040, mod-018 and mod-020 left interim. It builds the object pages with the sections whose content exists, and leaves the rest absent (BR-NE-3). It shows every project (BR-NE-5). Its «Обзор» becomes the organization's home page (OQ-1). It retires mod-020's fallback and keeps its filter in the address (OQ-2). It is still L, on a smaller base |
| `mod-019-scoped-access` | 6th · mod-018 | 7th · **mod-018, feat-043** | Its grants become sections of the object pages: «Участники и доступ» of a project and of a team, beside the team's members, and an area's «Участники». They no longer go to project-settings or organization-settings pages. Its per-object codes decide which sections show. It re-runs US-10's grant clauses |
| `feat-041-project-visibility` | 7th · mod-019, feat-040 | 8th · unchanged | The visibility is set in the project's «Настройки». Its non-disclosure now covers feat-043's lists, switcher, «Обзор» and cross-project «Рабочие элементы», which feat-043 used to filter itself (ADR-0029). It re-runs US-10's visibility clauses |
| `feat-042-area-board-and-overview` | 8th · mod-018, feat-041 | 9th · unchanged | Its views are the area page's «Бэклог» and «Канбан», and its figures fill the area's «Обзор». They are no longer reached from the area's organization-settings page. It re-runs US-10's area-view clauses |
| `mod-007-iterations` | 9th · feat-040, feat-041, feat-042 | 10th · unchanged | The team's and the project's «Спринты» are sections of their pages from the start, and the area's «Обзор» gains the active sprints. No interim entries. It re-runs US-10's sprint clauses |
| `feat-013-board-backlog-filter` | 10th · feat-003, mod-007, feat-042 | 11th · unchanged | Position only: feat-043 no longer waits for it. Its views are sections of the object pages. Its per-view memory sits beside feat-043's remembered context and mod-020's filter in the address, as mod-020's note already says |
| `feat-045-organization-home` | queued after mod-018 · mod-018 | **withdrawn**, folded into feat-043 (OQ-1) | Its home page is feat-043's «Обзор». Its open question, whether projects show under their areas, is answered by BR-MX-27 and US-10: no list nests one object inside another |
| `mod-020-team-switcher-default-and-filter-address` | Next · mod-017 | unchanged (OQ-2) | Its note only: feat-043 retires the fallback team and keeps the filter in the address |
| `feat-044-area-analytics` | outside the chain · mod-009, feat-042, feat-043 | unchanged | None. Its «Аналитика» section joins the area's page, as before |

## What feat-043 provides now

**What it used to receive and now does without, or provides itself:**

- *From mod-019:* codes served per object, which hid sections such as «Настройки». feat-043 now shows entries and sections by the codes served today (the organization role, ADR-0027 rule 5). mod-019 switches the source to per-object codes. The grants pages are no longer feat-043's to move.
- *From feat-041:* the visibility setting to move, and the set form on its lists. Neither is needed now (BR-NE-5). Each of its reads still takes the caller's access as an argument (ADR-0029 § 7), so feat-041 can apply the set form without changing the read's interface.
- *From feat-042:* the area's views and overview to place. feat-043 builds the area's «Обзор» from what exists: its parent, its child areas and its description. feat-042 adds the views and the figures.
- *From mod-007:* the «Спринты» sections to place. They stay absent until mod-007 adds them.
- *From feat-013:* nothing; feat-013 was a dependency only to keep feat-043 last.

**New to it:** a section model for each object kind that later lines extend, with BR-NE-3 (ADR-0024 says each line registers its own entry). Also the team's «Участники и доступ» holding only the team's members, the project's «Настройки» without visibility (name, description, default team), and the organization's «Обзор» as its home page (OQ-1).

**Left absent for the lines after it:**

| Page | Section | Added by |
|---|---|---|
| Area | «Канбан», «Бэклог»; the figures of «Обзор» | feat-042 |
| Area | «Участники» | mod-019 |
| Area | the active sprints on «Обзор» | mod-007 |
| Area | «Аналитика» | feat-044 |
| Project | «Участники и доступ» | mod-019 |
| Project | «Спринты» | mod-007 |
| Project | the visibility in «Настройки» | feat-041 |
| Team | the grants in «Участники и доступ» | mod-019 |
| Team | «Спринты» | mod-007 |

**What it still takes over or absorbs:**

- *tech-009 F4* (the switcher compares address ids case-sensitively): unchanged.
- *bug-003* (route matching for the marker and the trail): unchanged.
- *tech-011 m4*: still obsolete, now sooner.
- *tech-019* (the shell onto the UI kit): the rule is unchanged ("after tech-019, or absorbs it"). feat-043 now starts sooner, so the orchestrator decides when feat-043 starts: if tech-019 has not started by then, feat-043 absorbs it and tech-019 is withdrawn.
- *New:* feat-045 (OQ-1) and mod-020's fallback team (OQ-2).
- *Already feat-043's:* the interim entries of feat-039 (the project's views in the team drop-down) and of feat-028 (BR-OL-10: «Бэклог проекта» and «Доска проекта» on the project's «Команды»).

## Risks

- **feat-043 is L and breaking, and it lands earlier.** The base is smaller. Its Breaks cell loses the grant and visibility pages, and gains feat-040's interim team level and mod-020's fallback. The E2E specs its address changes break (E2E stays paused, the standing NO) are named in its MR sooner than planned. The size stays L.
- **The section model becomes a contract for four lines** (mod-019, feat-041, feat-042, mod-007), and a wrong model costs each of them. feat-043's design fixes the sections of each object kind and BR-NE-3. Each later line's spec names the US-10 clauses it re-runs.
- **feat-041 grows.** Its non-disclosure now also covers feat-043's switcher, «Обзор», three project lists and cross-project list. US-6 already names the list, the switcher and the team's and area's project lists; US-10's owed scenario names the rest. It stays L.
- **The cross-project «Рабочие элементы» list meets its 2 s budget before visibility exists** (doc-012 NFR; ADR-0028's revisit clause). When feat-041 joins the set form, the query changes and its statement-count test is pinned again. ADR-0029 § 7's caller argument (§ What feat-043 provides now) keeps that change inside the read.
- **Interim work that is still built:** feat-040's team level and mod-018's area pages in the organization settings, each living one or two lines. That is the price of the human's "after mod-018", and it is not removed here.
- **Nothing in flight is reworked.** Every moved line is queued and none has started. feat-038 (in flight) keeps its landing rule (OQ-5). doc-011 (at gate-1) restates its rows against BR-MX-17 and -18, which do not change.

## Edits made

The edits follow doc-010's convention for amending merged analyses (doc-001, doc-004, doc-006): the merged files are rewritten in place, each change cites its source inline ("doc-013"), and the amended task's status.md gets a "[doc-013] amended" Log line.

- **doc-012 spec.md:**
  - the Operation completeness Surface header;
  - feat-040's row (the mod-020 clause);
  - feat-043's row, moved to line 6: deps, Task, size, Rewrites and Breaks;
  - mod-019's row (Task, deps); the Task cells of feat-041, feat-042 and mod-007;
  - the order paragraph.
- **doc-012 status.md:** the "[doc-013] amended" Log line.
- **US-10:**
  - the header (shippable) and the intro sentence (BR-NE-3, BR-NE-4);
  - the Background (Pavel's and Irina's grants are owed by mod-019);
  - the main navigation (an area's board and backlog);
  - sections decided by the object's own codes;
  - «Обзор» as the home page (feat-045);
  - the sections of the area, project and team pages, and the area's «Обзор» counts;
  - the «Ограниченный» scenario and the fallback scenario.
- **US-6:** the organization's «Рабочие элементы» is no longer owed by feat-043, and the switcher's groups replace its levels.
- **ADR-0028:** § 7 *Old addresses* (the interim-pages sentence), and the delivery table (feat-043's row moved after mod-018, its cell).
- **ADR-0029:** *What the delivery lines implement* (feat-043's bullet moved after mod-018 and without the set form; feat-041's bullet takes that set form).
- **Not touched:**
  - ADR-0024, which names no order;
  - the stories whose owed-by clauses name no order (US-1..US-5, US-7..US-9, US-11, US-12);
  - doc-012's review.md and audits, which are archives;
  - the merged lines' specs.

## Out of scope

- mod-018's own ADR-0028 amendment (rule 8's FOR NO KEY UPDATE, A-9's corollary): it stays with mod-018.
- The queue itself: the orchestrator edits it after gate-1. feat-038's status.md note, if OQ-5 changes it, is the orchestrator's on that branch.
- Any rule, NFR or fate of doc-012 (BR-NE-6). Any change to mod-018's or feat-040's scope beyond OQ-2's clause.
- The design of the object pages and their section model, which belongs to feat-043's own stages.

## Queue changes for the orchestrator

To paste after gate-1, in one queue commit to main. The current lines are those of origin/main at 8e100f1f. feat-043's line moves to directly after mod-018's. feat-044's note ("line 12") keeps its number.

| Line | Change | New line |
|---|---|---|
| feat-043 | deps, description, note | `- [ ] feat-043-area-project-team-navigation — Top-level «Области», «Проекты» and «Команды»; the object pages with their sections, a section absent until the line that owns it adds it; the organization's «Обзор» as its home page, the user's organizations beside the content (feat-045 folded in); the «Рабочие элементы» list across projects; breadcrumbs with no false hierarchy; the switcher and the remembered context; empty states. A retired address answers «Страница не найдена», nothing redirects, and the interim pages of the earlier lines (the team and area pages in the organization settings, the project's teams, area links and default team in its settings) move onto the objects' own pages (doc-012 BR-MX-27, -28; US-10) · epic:platform/ui · size:L · deps:feat-040,mod-018 · note:doc-012 decomposition line 6, moved right after mod-018 by doc-013 (the human, 2026-10-18: today's organization/project/team switching is the worst pain point); after tech-019 or absorbs it; breaks the shell's address and context model (ADR-0024 § 2); retires feat-040's interim team level and mod-020's fallback team, keeps mod-020's filter in the address; takes over tech-009 F4 and bug-003; makes tech-011 m4 obsolete` |
| mod-019 | deps + feat-043; interim entry → sections; line 7 | `- [ ] mod-019-scoped-access — Area, project and team grants on top of organization membership; inheritance through the area tree; the effective-permissions policy on every route, served to the panel per object, which shows an object page's sections by it; boards.projectbacklog.reorder; the inventory test per scope and the bundle contract. The grants are sections of the objects' own pages: «Участники и доступ» of a project and of a team (beside the team's members), «Участники» of an area (doc-012 BR-MX-20, -22, -23, -26; US-5, US-10; ADR-0029) · epic:core/org-access · size:L · deps:mod-018,feat-043 · note:doc-012 decomposition line 7; after feat-043 by doc-013, 2026-10-18; changes the authorization rules (mod-016 api-contract §§ 0.2, 1.1; mod-006 BR-M4-27); answers doc-011's scoped-permission-overrides; takes over tech-026 OP-7` |
| feat-041 | feat-043's surfaces named; interim entry → section; line 8 | `- [ ] feat-041-project-visibility — Project visibility («Организация», «Участники областей», «Ограниченный») and non-disclosure on every surface that exists when this line merges, each named, feat-043's project lists, switcher, «Обзор» and cross-project «Рабочие элементы» among them; the answer order; the assignee picker offers only those who can read the project. The visibility is set in the project's «Настройки» on its own page (doc-012 BR-MX-21, -24, -25; US-6, US-10) · epic:core/org-access · size:L · deps:mod-019,feat-040 · note:doc-012 decomposition line 8; after feat-043 by doc-013, 2026-10-18; changes the authorization rules and the answer order (mod-016 api-contract § 0.1 rows 2-4, § 3.4; BR-M1-26); rewords mod-008's premise that results are readable across the organization` |
| feat-042 | interim entry → sections; line 9 | `- [ ] feat-042-area-board-and-overview — Area backlogs and boards aggregating the projects linked to the area and, on request, its child areas, in flat sorted pages; the area overview: linked projects and teams, participants, counts by status. They are the «Бэклог» and «Канбан» sections of the area's own page (areas/:a/backlogs/:level, areas/:a/boards/:level) and the figures of its «Обзор» (areas/:a/overview) (doc-012 BR-MX-17; US-8, US-10) · epic:core/backlogs-boards · size:M · deps:mod-018,feat-041 · note:doc-012 decomposition line 9; after feat-043 by doc-013, 2026-10-18; additive: applies ADR-0029's set form and changes no rule; answers doc-011's team-area-sets` |
| mod-007 | interim entries → sections; line 10 | `- [ ] mod-007-iterations — MVP. Founds M5 on team sprints: the lifecycle (Planned, Active, Closed), numbering, the team's sprint list, the item's sprint pointer with the team rule, closing with its outcome record and the move of unfinished items, the team sprint board, active sprints on the area overview; a deleted team's Closed sprints and their outcomes stay readable on the projects' «Спринты». The team's «Спринты» (teams/:t/sprints) and a project's «Спринты» (projects/:p/sprints) are sections of their own pages (doc-012 BR-MX-13..-15; US-9, US-12, US-10) · epic:core/iterations-planning · size:L · deps:feat-040,feat-041,feat-042 · note:doc-001 decomposition (b), refined by doc-004; doc-012 decomposition line 10, replacing doc-004's iteration tree, releases and team selection; withdraws feat-010; fills the card's iteration slot (feat-029 design § 4.8); reworded by doc-012, 2026-10-17, and by doc-013, 2026-10-18 (after feat-043)` |
| feat-013 | note: line 11 | `- [ ] feat-013-board-backlog-filter — MVP. One filter specification for the team, project and area views, extending the shared specification that the project views' team filter and the team views' project filter began (their request parameters kept): project, team (with «Без команды»), assignee, type, state, priority, sprint, target date and keyword; options and counts after project visibility; a filtered backlog hides rows but reorders against the full list (doc-003 US-2 filtered-order scenario, doc-010 D-14); the level and the filters remembered per view, «Включая дочерние области» per area (doc-012 US-11) · epic:core/backlogs-boards · size:M · deps:feat-003,mod-007,feat-042 · note:doc-010 D-14; doc-012 decomposition line 11: its tag filter moves to feat-016, its area filter retires with the item's «Область» (OQ-1 A); reworded by doc-012, 2026-10-17; feat-043 no longer waits for it (doc-013, 2026-10-18)` |
| feat-040 | note: mod-020 (OQ-2) | `- [ ] feat-040-team-surfaces-across-projects — Team backlogs and boards addressed by the team alone, over all its projects, with a project filter; rows and cards carry their project and owning team; a card opens in its own project; the tie-break; the switcher's team level lists the organization's teams (doc-012 BR-MX-17, -18; US-7) · epic:core/backlogs-boards · size:L · deps:feat-028 · note:doc-012 line 4; takes over tech-011 m2 and m3 and feat-028's card-dialog re-check; whichever of it and mod-020 merges second keeps mod-020's fallback team on the interim team level, which feat-043 retires (doc-013)` |
| mod-020 | note: its fate (OQ-2) | `- [ ] mod-020-team-switcher-default-and-filter-address — The human's answers to mod-017 OQ-8 B and OQ-9 B (2026-10-17). OQ-8: with no team chosen, the team views' switcher opens the project's default team, else the first team by name; amend doc-012 BR-MX-11 (the default no longer affects creation only). OQ-9: the team filter on «Рабочие элементы», the project backlog and the project board lives in the page address too, so a link carries it, and survives re-reads and level changes as before (BR-OT-8) · epic:core/backlogs-boards · size:S · deps:mod-017 · note:frontend; feat-013's per-view memory overlaps the filter in the address; the gate-1 change of mod-017, recorded in its status.md; whichever of it and feat-040 merges second keeps the fallback on feat-040's team level; feat-043 retires the fallback with the «Бэклоги» and «Канбан» entries and keeps the filter in the address on the project's sections (doc-013)` |
| feat-045 | withdrawn (OQ-1): its line leaves docs/backlog.md, and this line goes to docs/backlog/withdrawn.md, dated by the queue commit | `- [x] feat-045-organization-home — withdrawn: absorbed by feat-043-area-project-team-navigation, which now runs right after mod-018 and makes the organization's «Обзор» its home page (doc-013 OQ-1 A), <date of the queue commit>` |

If the human picks another option at gate-1, the rows change as follows. OQ-1 B keeps feat-045 with deps:feat-043. OQ-2 B withdraws mod-020 and drops the mod-020 clauses from feat-040 and feat-043. OQ-6 B adds a line that only the orchestrator can number.

## Open questions

- [ ] **OQ-1 feat-045, the organization's home page.** It was queued after mod-018 as the organization's entry page "until the navigation line", and the navigation line now follows mod-018 directly.
  - (A, recommended) Fold it into feat-043 and withdraw it. The organization's «Обзор» becomes the home page: the user's organizations beside the content, then her teams first, the counts, and the area tree, projects and teams, each row opening its page. The page is built once, from the lists feat-043 builds anyway. feat-045's open question is already answered by BR-MX-27 and US-10. feat-043 grows by one page.
  - (B) Keep it as its own M line right after feat-043 (deps feat-043). It would extend the counts-only «Обзор» of US-10 with the organizations and the object lists. feat-043 stays smaller, but «Обзор» goes through two lines and two gates.
  - (C) Keep it before feat-043, as queued: an interim home page that the next line replaces.
  - The analysis goes on with A.
- [ ] **OQ-2 mod-020 versus the new switcher.** mod-020 is S, frontend and ready (deps mod-017). It carries the human's answers of 2026-10-17 to mod-017's OQ-8 (the fallback team) and OQ-9 (the filter in the address). OQ-8 lives on the old switcher only: in feat-043's model a team's views are always reached from the team. OQ-9 survives on the project's sections.
  - (A, recommended) Keep it where it is, before or beside feat-040. Whichever of the two merges second keeps the fallback on feat-040's interim team level. feat-043 retires the fallback with the «Бэклоги» and «Канбан» entries and keeps the filter in the address. The human's answers ship at once for an S, and only the fallback is interim.
  - (B) Fold it into feat-043 and withdraw it. The filter joins feat-043's address model and the fallback is never built. This saves an S, but drops an answer the human chose a day earlier, and the filter waits two lines.
  - (C) Move it after feat-043, restated to the filter in the address alone.
  - The analysis goes on with A.
- [ ] **OQ-3 feat-040's level of the switcher.** feat-040 addresses team views by the team alone, and ADR-0024 § 2 has the switcher show the address's context, so the old three-level switcher cannot show a team with no project.
  - (A, recommended) Keep feat-040's interim team level as queued, and no larger: the organization's teams the user may see, own teams first, whatever project is chosen. It has no grouping, memory or fallback notice; those are feat-043's (BR-MX-28).
  - (B) feat-040 builds the final switcher, and feat-043 the rest. BR-MX-28 then splits across two L lines and the switcher breaks twice, while choosing an object still needs feat-043's pages.
  - (C) feat-040 keeps the project-nested team addresses and the old switcher. A project's address would then show a team's items from other projects, against BR-MX-27 and US-7's address clause, and US-7's owed-by would move.
  - The analysis goes on with A.
- [ ] **OQ-4 How mod-019, feat-041, feat-042, mod-007 and feat-013 put their sections on the object pages.**
  - (A, recommended) Each line adds its section to the page feat-043 built, at the section's final address. Until then the section is absent (BR-NE-3), and the line re-runs the US-10 clauses it owes. feat-013 adds no section; it filters the views that are sections. Nothing is built twice.
  - (B) feat-043 builds every section up front as a placeholder («Раздел появится позже»). The page looks complete, but each placeholder is an interim screen whose test is later thrown away.
  - (C) feat-043 builds the sections with their real empty states («У команды нет спринтов»), which promise an action no line offers yet.
  - The analysis goes on with A.
- [ ] **OQ-5 feat-038's landing page.** feat-038 (first-organization onboarding, in stage 1 on its branch since 2026-10-15) lands on the new organization's project list.
  - (A, recommended) Keep doc-012's rule: whichever of feat-038 and feat-043 merges second points the landing at «Проекты» (`…/projects`), noted in feat-038's status.md. Creating the first project is the onboarding's next step, and «Проекты» is where US-10 offers it. feat-038 is now the likelier second.
  - (B) Land on the organization's «Обзор», the home page under OQ-1 A. For an empty organization it shows empty lists and sends the user on to «Проекты» anyway.
  - The analysis goes on with A.
- [ ] **OQ-6 Splitting feat-043.**
  - (A, recommended) Keep it one L line. It lands on a smaller base than first planned: no grant, visibility, area-view or sprint page to move, and no visibility filter. Its address and context model is one breaking change, and the switcher's "choosing an object opens its page" needs the pages. Its own stage 1 still applies the size rules and may propose a cut.
  - (B) Split off the organization-level surfaces, «Обзор» (with feat-045) and the cross-project «Рабочие элементы» list, into one M line right after it. The pain fix lands sooner, and the one new organization-wide read leaves the breaking line. The cost is a third gate cycle and an interim organization landing on «Проекты».
  - (C) Split the shell and address model from the object pages. That makes two breaking changes of the address model instead of one, and an interim switcher with nothing to open.
  - The analysis goes on with A.
