# Review: doc-013-navigation-earlier

## Round 1 — requirements

The light route's one round. Read in a clean context by a reviewer who wrote none of it: `git diff 12c26469..e84f3bd0`, that is, doc-013's spec.md and status.md, and its edits to doc-012 (spec.md, status.md, US-10, US-6), ADR-0028 and ADR-0029. Also read: the rest of doc-012, its other stories, `docs/adr/README.md`, and the queue at origin/main 8e100f1f (`docs/backlog.md`, `docs/backlog/withdrawn.md`). The merged artifacts of feat-028, feat-039 and mod-017 were searched for wording that assumes the old order.

**Verdict: changes-requested.** 3 major and 7 minor findings, none critical. The new order holds together. The gaps are what feat-043 used to get for free by coming last (R-1), and two US-10 edits that leave feat-043 with an ambiguous or unrunnable criterion (R-2, R-3).

**Fix round (business-analyst, 2026-10-18).** All ten findings are fixed in place; each Resolution below says how. R-1 adds OQ-7 (the merge-order rule or a feat-003 dep) for gate-1. The light route has no second round: gate-1 is the check.

### Checks

| Check | Result |
|---|---|
| The order is stated the same way everywhere | Holds in doc-013 (§ The decision, BR-NE-1, the lines table, status.md), doc-012 (the row order, the order paragraph, the Surface header), US-10's header, US-6, ADR-0028 (§ 7, the delivery table), ADR-0029 (the delivery list) and the queue table: feat-040 → mod-018 → feat-043 → mod-019 → feat-041 → feat-042 → mod-007 → feat-013 |
| The graph is acyclic and every line's deps can be met at its new place | Acyclic. mod-019 lists feat-043 directly; feat-041, feat-042, mod-007 and feat-013 reach it through mod-019. What feat-043 used to get from the old chain: R-1 (feat-003, mod-005), R-4 (mod-020) |
| Dangling interim or "until feat-043" wording in live rules | None in doc-012, its stories or the ADRs, apart from R-6. The merged feat-028, feat-039 and mod-017 artifacts say "until feat-043" without assuming an order, so they hold either way (history, not flagged) |
| The ADR edits follow `docs/adr/README.md` | Not substantive: only who delivers what and which pages are interim. The § 7 sentence sits in the Decision section, but the decision itself (retire without redirect) stands. Minor gaps: R-5, R-6, R-7 |
| The queue lines match the format and the spec | All eight `- [ ]` lines match `docs/backlog.md`'s format and the feat-045 line matches `withdrawn.md`'s. Line numbers 6-11 match the decomposition table, and feat-044 keeps "line 12". Missing rows: R-1, R-10 |
| The OQs are real decisions with honest options and a recommendation | OQ-1..OQ-6 each follow from the move, and each has a recommendation. Gaps: R-9 |
| The reference tracker is not named | Not named in any added line |
| Scope creep | None beyond what OQ-1 (feat-045) and OQ-2 (mod-020) put in. The section model is left to feat-043's own stages (§ Out of scope) |

### Rule → where applied

| Rule | Applied in | Gap |
|---|---|---|
| BR-NE-1 Order | doc-012 rows and the order paragraph; ADR-0028 table; ADR-0029 list; US-10 header; queue table | - |
| BR-NE-2 Interim entries | doc-012 order paragraph, Surface header, Task cells of mod-019, feat-041, feat-042 and mod-007; ADR-0028 § 7 | R-6 |
| BR-NE-3 Absent sections | US-10 intro and section lists | R-3 |
| BR-NE-4 Owed clauses | US-10 Background, sections and scenarios | R-2 |
| BR-NE-5 No visibility yet | doc-012 feat-043 and feat-041 rows; ADR-0029 bullets; US-6 | R-5, R-7 |
| BR-NE-6 Nothing else changes | the edits keep to it | R-1, R-4 |
| BR-NE-7 ADRs | ADR-0028 § 7 and table; ADR-0029 list | R-6, R-7 |

### R-1
Severity: major
Affects: doc-013 § What feat-043 provides now ("From feat-013: nothing"), § The decision ("the parallel lane mod-005 → feat-003 is untouched"); doc-012 feat-043 row; queue rows feat-043 and feat-003
Problem: The old chain put feat-043 after feat-013, and feat-013 has `feat-003` in its deps, and `feat-003` has `mod-005`. So feat-043 used to come after feat-003 and mod-005 by the graph. The new deps (feat-040, mod-018) drop that, so "From feat-013: nothing" is wrong. feat-043 builds the «Рабочие элементы» list across projects and turns the project's list into "the same list fixed to A" (US-10). feat-003 adds that list's filters: state with «Удалена» (the way into the recycle bin), assignee, tag and keyword. mod-005 is in flight and covers doc-001 BR-14 only "for the surfaces … that exist when it ships". Nothing now says, for either merge order:
- who puts feat-003's filters and keyword search on the cross-project list;
- whether feat-003's spec must name that list among its surfaces;
- whether feat-043's new list keeps «Удалена» items out by itself.
Both lines can each "ship on its own" and still leave the organization's list without the filters the project list has.
Required resolution: Correct § What feat-043 provides now. Then pick one of these and state it in doc-012's feat-043 row and in doc-013:
- add `feat-003` to feat-043's deps, and state the delay;
- or add a "whichever of feat-003 and feat-043 merges second" rule, like the one for feat-038: the second line carries feat-003's filters and keyword onto the cross-project «Рабочие элементы» list and its per-project section, noted in the other line's status.md.
Also state that feat-043's cross-project list follows mod-005's deleted-item rule when mod-005 merged first. Add a feat-003 row to § Queue changes, and the matching clause to feat-043's note.
Status: resolved
Resolution: § What feat-043 provides now no longer says "From feat-013: nothing": it names what the old chain gave through feat-013 (feat-003's filters and keyword, mod-005's deleted-item rule) and that the new deps drop it. New BR-NE-8 picks the merge-order rule: whichever of feat-003 and feat-043 merges second carries feat-003's filters and keyword onto the cross-project «Рабочие элементы» list and the project's section, logged in its status.md; whichever of mod-005 and feat-043 merges second keeps deleted items off them (mod-005 BR-DR-6, BR-DR-16). Stated in doc-012's feat-043 Task cell, § The decision, the lines table (new feat-003 and mod-005 rows), § Risks and § Out of scope. § Queue changes gains a feat-003 row and the matching clause in feat-043's note. The human chooses at gate-1 between the rule and the dep (new OQ-7, A recommended).

### R-2
Severity: major
Affects: doc-012 US-10, the Background ("Pavel's and Irina's grants, and every clause that reads them, are owed by `mod-019-scoped-access`") and the scenario "Links are added through a searchable multiselect"
Problem: Every step of that scenario is Pavel's, so the Background owes the whole scenario to mod-019. That includes the parts that do not need a grant:
- the multiselect leaves out teams already linked;
- typing narrows it;
- the link appears without a reload;
- B's «Общие», an area's «Проекты»/«Команды» and a team's «Проекты»/areas work the same way;
- Olga sees the rows and no command.
Moving the link commands onto five object-page sections is feat-043's work (BR-MX-27; ADR-0028 delivery table). feat-043 now runs US-10 first, and the only link clause left for it is the admin's single link of D to VENUS. Before this edit Pavel existed when feat-043 merged, so this gap is new.
Required resolution: Split the scenario. One version runs at feat-043 with an organization admin: offered VENUS and JUPITER, not MARS; «ven» narrows to VENUS; no reload; the same on the other sections; the unlink command for the admin; Olga sees the rows and no command. Keep "(owed by `mod-019-scoped-access`) Pavel is offered VENUS only, not JUPITER" as a separate clause.
Status: resolved
Resolution: US-10's Background now says each clause that reads Pavel's or Irina's grants is marked owed by mod-019 and the rest of its scenario runs from feat-043 on. The multiselect scenario is split: an organization admin's version runs at feat-043 (VENUS and JUPITER offered, not MARS; «ven» narrows; no reload; the other sections the same way; the admin's unlink command; Olga sees the rows and no command), and a separate scenario owed by mod-019 keeps "Pavel is offered VENUS only" and the unlink command by «Управление». The grants on VENUS, in the clause on VENUS's «Участники и доступ», are marked owed too.

### R-3
Severity: major
Affects: doc-012 US-10, "An area's page", its Then line and the «Проекты» line
Problem: "its sections are «Обзор», «Канбан» and «Бэклог» (owed by feat-042), «Проекты», «Команды» and «Участники» (owed by mod-019)" pairs each parenthesis with a group of three. Read that way:
- «Обзор» is owed by feat-042, but doc-013 has feat-043 build it, and the same scenario and "Choosing an object" open «Логистика»'s «Обзор» at feat-043;
- «Проекты» and «Команды» are owed by mod-019, but they are feat-043's.
Read literally, feat-043's area page has nothing but «Настройки». The next line ("and not C, which … «Логистика»'s «Обзор» counts") also asserts a feat-042 figure without marking it owed. Stages 4-5 read the story, not doc-013's table.
Required resolution: Reword: "its sections are «Обзор», «Проекты» and «Команды»; «Канбан» and «Бэклог» are added by `feat-042-area-board-and-overview` and «Участники» by `mod-019-scoped-access` (owed)". Mark "«Логистика»'s «Обзор» counts" as owed by feat-042.
Status: resolved
Resolution: The area page now reads: its sections are «Обзор», «Проекты» and «Команды»; «Канбан» and «Бэклог» are added by feat-042 and «Участники» by mod-019 (owed), each in its place in the stated order. "«Логистика»'s «Обзор» counts" and Irina's «Участники» clause are marked owed by feat-042 and mod-019. The project and team pages are reworded the same way, and their «Спринты» clauses are marked owed by mod-007.

### R-4
Severity: minor
Affects: OQ-2 A; doc-012 feat-043 row (Rewrites: "retires mod-020's fallback team … keeps its team filter in the address"); BR-NE-6
Problem: feat-043's Rewrites cell assumes mod-020 merges first, but feat-043's deps do not include mod-020, and OQ-2 A allows "before or beside feat-040". mod-020 also amends BR-MX-11 ("the default no longer affects creation only"). When feat-043 retires the fallback, that amendment goes stale, while BR-NE-6 says BR-MX-1..-28 stand.
Required resolution: Either add `mod-020` to feat-043's deps, or add "if mod-020 has not merged, feat-043 builds OQ-9's filter in the address and mod-020 is withdrawn". State that feat-043 restores BR-MX-11's "affects BR-MX-10 only" when it retires the fallback, and note it in mod-020's queue row.
Status: resolved
Resolution: mod-020 joins feat-043's deps (new BR-NE-9; doc-012 feat-043 deps cell; the queue row). BR-NE-9 and doc-012's Rewrites cell state that retiring the fallback restores BR-MX-11's "The default affects BR-MX-10 only", and BR-NE-6 says BR-MX-11 reads as mod-020 amends it only until then. mod-020's queue row and the lines table say the same.

### R-5
Severity: minor
Affects: ADR-0029, the feat-043 bullet ("each read model taking the caller's access as § 7 requires"); doc-013 § What feat-043 provides now ("without changing the read's interface") and § Risks ("keeps that change inside the read")
Problem: § 7's "caller's access" means the resolved role, objects and effective codes from § 6's evaluator, which mod-019 builds (the mod-019 bullet: §§ 2, 3, 5, 6, 7, …), after feat-043. As written the bullet asks feat-043 for more than the line before it provides. mod-019 also changes the access type (`PermissionSource`, `ResourceScope`), so the read's interface changes in mod-019 whatever feat-043 does.
Required resolution: Rephrase the bullet so feat-043 can meet it, e.g. "each read model takes the caller, today's resolved membership (ADR-0027 rule 3), as an argument; mod-019 replaces it with § 7's resolved access, and feat-041 joins the set form". Make doc-013's two claims say no more than that.
Status: resolved
Resolution: ADR-0029's feat-043 bullet now says each read model takes the caller as a required argument, today the membership and role that ADR-0027 rule 3 resolves; mod-019 replaces it with § 7's resolved access, and feat-041 adds the set form. doc-013 § What feat-043 provides now says mod-019 changes the reads' signature in any order and feat-041 adds no argument; § Risks says the argument keeps only feat-041's change inside the read.

### R-6
Severity: minor
Affects: ADR-0028 delivery table, feat-042 row ("the area prefix for its views"); BR-NE-7
Problem: feat-043 now adds the area prefix `/organizations/:o/areas/:areaId/…` with the area's «Обзор», «Проекты», «Команды» and «Настройки» (doc-012's feat-042 row: the figures of its «Обзор», "at `…/areas/:a/overview`"). Under the old order, feat-042 added that prefix through its interim entry. BR-NE-7 says every assignment of the consequences was moved, and this one was not.
Required resolution: Reword feat-042's cell to "its views under the area prefix", and name the area prefix in feat-043's cell.
Status: resolved
Resolution: ADR-0028's delivery table: feat-042's cell reads "its views under the area prefix (doc-013, 2026-10-18)", and feat-043's cell names the area prefix `/organizations/:o/areas/:areaId/…` with the area's «Обзор», «Проекты», «Команды» and «Настройки».

### R-7
Severity: minor
Affects: ADR-0029, the feat-043 and feat-041 bullets; BR-NE-7; doc-013 § Roles
Problem:
- Both bullets name feat-043's project lists, switcher and cross-project list, but not the organization's «Обзор». Its counts, and with OQ-1 A its object lists, are a surface that doc-012's feat-041 row, doc-013's lines table and the feat-041 queue line all name.
- BR-NE-7 says each edit is marked "doc-013, 2026-10-18", but the feat-041 bullet says only "doc-013".
- Only the human governs an accepted ADR (README, Processes 2-3), yet § Roles has the human approve "the new order and the open questions" only, not the ADR edits.
Required resolution: Add «Обзор» to both bullets. Date the mark. In § Roles or BR-NE-7, have gate-1 ask the human to confirm the ADR-0028/0029 edits as non-substantive.
Status: resolved
Resolution: ADR-0029's feat-043 and feat-041 bullets name the organization's «Обзор»; the feat-041 mark reads "doc-013, 2026-10-18"; ADR-0028's feat-043 cell names «Обзор» too. § Roles and BR-NE-7 have gate-1 ask the human to confirm the ADR-0028/0029 edits as not substantive (`docs/adr/README.md`, Processes 2-3).

### R-8
Severity: minor
Affects: doc-012 US-10, "Each object appears once" (the «Обзор» clause, folded in from feat-045)
Problem: «Обзор» "lists the area tree, the projects and the teams". No order and no bound is given at the NFR volumes (1 000 areas over 10 levels, 200 projects, 200 teams), though US-10 sets one for the switcher. A test cannot tell whether the home page should draw 1 400 rows, and the three lists repeat «Области», «Проекты» and «Команды».
Required resolution: State each list's order and what «Обзор» shows at the NFR volumes (for example the tree's top level, or the first N rows with a way to the full list). Or say outright that feat-043's design decides it, under a volume clause like the switcher's.
Status: resolved
Resolution: US-10's «Обзор» clause now gives the order (organizations by name; VENUS first; then the top-level areas, the projects and the teams, each by name) and a volume line: at 1 000 areas, 200 projects and 200 teams each list shows only its first rows, as many as feat-043's design sets, and ends with a link to its full list; child areas are on «Области» and the area's page. doc-013 § What feat-043 provides now and OQ-1 A say the same.

### R-9
Severity: minor
Affects: OQ-1 (C), OQ-2 (C); § Queue changes, its last paragraph
Problem: OQ-1 C and OQ-2 C give no cost or benefit, while every A and B option does. The paragraph on other answers covers only the queue rows. Yet the doc-012 edits already apply OQ-1 A and OQ-2 A:
- feat-043's Task and Rewrites name feat-045 and mod-020;
- feat-040's row has the mod-020 clause;
- US-10's «Обзор» clause says "feat-045, folded in".
Required resolution: Give each C option its trade-off. Name the doc-012 and US-10 edits that a B or C answer reverses.
Status: resolved
Resolution: OQ-1 C and OQ-2 C now each give their benefit and cost. § Queue changes ends with a table naming, for each B or C answer (OQ-1..OQ-7), the queue rows and the doc-012, US-10 and doc-013 edits it reverses, and § Open questions opens by pointing to it.

### R-10
Severity: minor
Affects: doc-013 § What it still takes over or absorbs (tech-019); § Queue changes
Problem: The new rule "if tech-019 has not started by then, feat-043 absorbs it and tech-019 is withdrawn" leaves `tech-020-ui-kit-m5-legacy-out` with `deps:…,tech-019`, which would then be withdrawn. No row covers it.
Required resolution: Add a conditional row: under that case, tech-020's deps take `feat-043` in place of `tech-019`. Or drop the new sentence and keep the unchanged rule.
Status: resolved
Resolution: § Queue changes gains a conditional tech-020 row: if feat-043 absorbs tech-019 and tech-019 is withdrawn, tech-020's deps take feat-043 in place of tech-019. § What it still takes over and the lines table say the same.
