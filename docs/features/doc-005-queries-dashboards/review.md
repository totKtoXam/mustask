# Review: doc-005-queries-dashboards

## Round 1 — requirements

Verdict: **needs-human-decision** — 2 blockers (R-1, R-2) contradict the
approved umbrella doc-001 and cannot be resolved by the reviewer, 7 majors
returned to the business-analyst, 7 minors fixed in place.

Reviewed: `spec.md` (189 lines as delivered) and `stories/US-1..US-5`,
against `docs/features/doc-001-domain-map/spec.md` (module map M6, MVP /
release-2 / release-3 tables, cross-module ER, BR-1..13, Operation
completeness, NFR, Out of scope, Decomposition),
`docs/features/doc-001-domain-map/status.md` § Decisions (D-1..D-12),
`research/ado-boards.md` §§ Queries, Security, Other product capabilities,
`screenshots/README.md` + `13-queries-list.png`, `14-query-editor.png`,
`16-dashboard.png`, `docs/backlog.md`,
`docs/features/doc-001-domain-map/review.md` (format precedent).

### Cross-reference check (every claim the spec makes about doc-001)

| Claim in doc-005 | Verified against doc-001 | Result |
|---|---|---|
| D-4 keeps M6 out of MVP; analysis only | status.md D-4; spec § Module map M6; § Module order | holds |
| Inline/free-text search stays M3/M4, `feat-003-work-item-search` | D-4; MVP table row M6; decomposition (b) | holds |
| Release 2 = personal saved queries + shared query folders (+ clause builder) | § Release 2 ("inline filters graduate into personal saved queries and shared query folders"); § Out of scope ("release 2, saved/shared queries with a clause builder") | matches exactly |
| Release 3 = dashboards, widgets, charts, WIQL-subset language, CSV import/export | § Release 3 (same five items) | matches exactly — but see R-5 (the decomposition breaks the line) |
| BR-1 sources "doc-001 BR-8" (tags are project-scoped) | BR-8 | exists, says that |
| BR-4 sources "doc-001 BR-3 (contrast)" (team owns Area → board) | BR-3 | exists, contrast is correct |
| BR-6 sources "doc-001 BR-9, D-11" (current iteration) | BR-9, D-11 | exist, say that |
| NFR audit sources "doc-001 BR-4" (history is WorkItem-only) | BR-4 | exists, WorkItem-scoped |
| NFR volumes "≤ 2 s for up to 5,000 items (doc-001 NFR)" | § NFR "within 2 s for up to 5,000 items in one project/team/level" | number exists, scope borrowed — see R-8 |
| Role model reuses `ORG_MEMBERSHIP.role` (D-9) | D-9, BR-5, § Roles | source correct, bundle contents contradicted — see R-2 |
| Open question 2 cites doc-001 BR-6 (soft delete) | BR-6 | exists — and the spec's own table contradicts it, see R-1 |
| Decomposition deps `mod-003-work-item-core`, `feat-003-work-item-search`, `mod-006-boards-backlogs`, `mod-007-iterations` | `docs/backlog.md` § Later | all four ids exist verbatim |
| Research §§ Queries, Security, Other product capabilities | `research/ado-boards.md` headings | all three exist |
| Nothing founds M6 in MVP; all stories `shippable: no` | D-4 | holds — US-1..US-5 all carry `shippable: no` |
| Glossary Russian terms vs D-7/D-12 | doc-001 § Glossary | no collision: doc-005 reuses «Рабочий элемент», «Статус», «Исполнитель», «Приоритет», «Область», «Итерация», «Тег» unchanged and adds only M6-own terms |

### Rule → story coverage (checklist item 1)

| Rule / promised capability | Covered by |
|---|---|
| BR-1 query scoped to one project | US-4 sc. 4 (import); no scenario for the query itself — out-of-scope bullet "cross-project queries" carries it |
| BR-2 personal query private | US-2 sc. 1 |
| BR-3 shared query, role split | US-2 sc. 2, 3 |
| BR-4 org-wide read of results | US-3 sc. 2 |
| BR-5 release-2 field set | US-1 sc. 3, 4 — the set itself is contradictory and the operator set is undefined → R-3, R-4 |
| BR-6 `@CurrentIteration` needs a team | US-1 sc. 2 (and spec open question 3) |
| BR-7 widgets never cached | US-5 sc. 4 |
| BR-8 CSV through M3's validation | US-4 sc. 2, 3 |
| Shared folder delete only when empty (ops table) | US-2 sc. 5 |
| Default dashboard cannot be deleted (ops table) | US-5 sc. 6 |
| Query/dashboard delete is hard and immediate (ops table) | **no story, and contradicts doc-001 BR-6** → R-1 |
| Viewer may create/update/delete their own personal query (Roles table) | US-2 sc. 3 (partially) — **contradicts doc-001 § Roles** → R-2 |
| Promoting an existing personal query to «Общие запросы» | **nothing** → R-10 |
| Result columns / column options (presupposed by US-4 sc. 1) | **nothing** → R-9 |
| Lifecycle of queries/dashboards when project, team or owner goes away | **nothing** → R-7 |
| Concurrent edits of a shared query/folder/dashboard | **nothing** → R-18 (fixed) |

---

### R-1
Severity: blocker
Affects: spec § Operation completeness (Query personal/shared, Query folder,
Dashboard, Dashboard widget), spec § Open questions (2), doc-001 BR-6, D-5
Problem: the Operation completeness table states as settled fact that
queries, folders, dashboards and widgets are deleted hard and immediately
("owner only, immediate (no recycle bin for queries)", "member, admin,
immediate"). doc-001 BR-6, sourced to the human decision D-5, is written
without an entity exception: "Deletion is always soft, never a hard delete,
and retention is indefinite: only an administrator purges a deleted object
permanently, by hand." An approved cross-module rule and this module's
normative table now say opposite things, and the founder task would
implement whichever it reads last. The spec is also self-inconsistent: the
same behaviour is asserted in the table and questioned in Open questions —
a spec states the rule it is going to be built from, it does not assert and
ask at the same time.
Required resolution: a human decision on whether BR-6 covers metadata
objects (queries, folders, dashboards, widgets) or only work items,
organizations and projects. Then either (a) doc-005 adopts soft delete for
these entities and the table says so, or (b) BR-6 is amended in doc-001
(new task, not here) to name the entities it covers and doc-005 cites that
amendment. Either way, spec open question 2 is removed and the table states
a single rule.
Status: resolved
Resolution: D-13 — BR-6 covers metadata objects too; added BR-9 (soft
delete for query/folder/dashboard/widget, same restore split as WorkItem —
owner for personal, member/admin for shared), rewrote the Operation
completeness Delete/Cancel columns, dropped open question 2, and added the
widget's «Запрос удалён» placeholder for a soft-deleted query to BR-9 and
US-5.

### R-2
Severity: blocker
Affects: spec § Roles, BR-2, § Operation completeness (Query personal),
US-2 sc. 3, doc-001 § Roles, BR-5, D-9
Problem: doc-005 grants `viewer` create/read/update/delete on their own
personal queries and, via BR-2, an owner-private folder. doc-001's Roles
table defines the viewer bundle as "read codes only — read everything a
member can write", and BR-5 makes a user's codes *exactly* that bundle,
with no per-object escape hatch. So doc-005 introduces the first write a
reader may perform, silently, in a module analysis — and M1's founder
(`mod-002-organizations-projects-teams`, which owes the ADR-0018 amendment)
would build a permission catalog that cannot express it. The reference
product does allow Readers to save personal queries
(`research/ado-boards.md` § Security: "Query folders: read, contribute…" is
about shared folders only), so the behaviour is defensible — but it is a
change to the role model, and the role model is D-9's ground, decided by
the human.
Required resolution: a human decision — either (a) the viewer bundle stays
read-only and personal queries require `member` (doc-005's Roles table and
BR-2 change, US-2 sc. 3 gains "a viewer cannot save a personal query
either"), or (b) the bundle admits writes on objects the acting user owns
privately, which must be stated as a rule here *and* carried back into
doc-001's Roles section by a separate task so M1 builds the catalog for it.
Status: resolved
Resolution: D-14 — (b): BR-2 and Roles now state the ownership-based
write explicitly and flag it as a departure from doc-001's Roles/BR-5;
added decomposition row `doc-NNN-personal-object-ownership` (docs task
amending doc-001's Roles section and M1's permission catalog, ADR-0018
amendment) and made M6's founder depend on it; US-2 gained an explicit
viewer-writes-own-query scenario.

### R-3
Severity: major
Affects: BR-5, US-1 (all four scenarios), doc-001 MVP table row M6
Problem: BR-5 contradicts itself and the stories in three ways. (1) It
defines the release-2 field set as the MVP inline filter's fields "plus
columns already on the work item list (type, priority, title)" and then
claims this "adds persistence and a folder, not a new filterable surface" —
filtering by type, priority or title is precisely a new filterable surface
next to doc-001's MVP row ("inline filter … by state/assignee/tag/area").
(2) Итерация is not in the list at all, yet US-1's second scenario builds
the clause "Итерация = @CurrentIteration" and BR-6 gives that macro a rule
of its own — so either the field set is incomplete or the headline story is
out of scope. (3) "plus columns already on the work item list" is not a
definition: which columns those are is M3's (doc-002) unwritten ground, so
the set cannot be frozen by reading this spec. The founder task would size
and build a different clause builder than the one the stories describe.
Required resolution: replace the prose with an explicit release-2 field
table (field → source module → why it is in the set), including or
explicitly excluding Итерация, and drop the "not a new filterable surface"
claim or restrict the set to state/assignee/area/tag so the claim becomes
true.
Status: resolved
Resolution: BR-5 now points to an explicit field table (US-1 § Query
language, grounded in doc-001's own MVP field row, not M3's unwritten
ground) that includes Итерация with its `@CurrentIteration` macro; the
"not a new filterable surface" claim is dropped — the set is stated as a
deliberately broader surface than the MVP inline filter's four fields.

### R-4
Severity: major
Affects: BR-5, US-1 sc. 3, § Out of scope ("raw-text query editor"),
status.md § Plan ("query-language subset boundary")
Problem: the "query-language subset boundary" is the named deliverable of
this task, and it is not delivered. No operator set is stated for release 2
anywhere; the only operators named in the whole spec are the release-3 ones
(Under, Was Ever, Changed Date). US-1 sc. 3 therefore reads "only the
release-2 operator set is offered" against a set that does not exist — the
criterion cannot be turned into a test, and neither can "the query is
valid" in sc. 1. The macro set has the same hole: `@Me` is used in US-1
sc. 1, `@CurrentIteration` has BR-6, `@Today` is pushed to release 3, and
`@Project`, `@Follows`, `@RecentMentions` (`research/ado-boards.md`
§ Queries) are never mentioned in or out. Finally, § Out of scope excludes
"a raw-text query editor" while release 3 promises "a WIQL-subset query
language" — a language with no text surface is just the clause builder, and
the spec never says which it means.
Required resolution: one table, field × operator × macro, with a release-2
and a release-3 column, closing every macro the research lists; and one
sentence saying whether release 3 exposes a text syntax at all or the
"subset language" is only the clause builder's internal model. Then rewrite
US-1 sc. 3 against a named operator.
Status: resolved
Resolution: US-1 § Query language now has three tables — release-2
field/operator/macro, release-3 additions per field, and a macro-status
table closing all six research macros (`@Project`/`@Follows`/
`@RecentMentions` = never, with reasons) — plus one sentence stating
release 3's "language" is the clause builder's internal model only, no
raw-text editor; US-1 sc. 3 now names the release-2 operators for Область
explicitly.

### R-5
Severity: major
Affects: § Decomposition row 1 (`mod-NNN-queries-dashboards`), § Operation
completeness (Dashboard), doc-001 § Release 2 / § Release 3
Problem: the founder task's description is the release-2 cut *plus* "one
default per-team dashboard with query-tile and markdown widgets (US-5
baseline)". Dashboards and widgets are release 3 in doc-001's release table,
in this spec's own Context section, and in this spec's own Operation
completeness table ("one default seeded automatically per team (release
3)"). As written, one `L` task straddles two releases, the release line
stops being traceable in the backlog, and `feat-NNN-dashboard-charts`
depends on a task whose release-3 half may never have been built.
Required resolution: either cut the dashboard baseline out of the founder
(release 2 only: query model, folders, results) and add a separate
release-3 task founding Dashboard/DashboardWidget with the query-tile and
markdown kinds, or keep it in one task and mark the row explicitly as
spanning release 2 + 3 with the reason. The first is consistent with the
rest of the spec.
Status: resolved
Resolution: split as suggested — `mod-NNN-queries-dashboards` is now
release-2 only (Query/QueryFolder, US-1/2/3); a new row
`mod-NNN-dashboards-widgets` founds Dashboard/DashboardWidget at both
levels (D-15) with query-tile/markdown widgets (US-5 baseline), depending
on the release-2 founder.

### R-6
Severity: major
Affects: § Decomposition row 3 (`feat-NNN-dashboard-charts`, size M),
checklist item 5
Problem: one `M` task is asked to deliver chart widgets *and* the whole
release-3 language extension — new operators (Under over the Area
hierarchy, Was Ever, Changed Date), the `@Today` macro, *and* the tree and
linked query types. Tree and "work items and direct links" queries are not
an operator addition: they change the result shape from a list to a
hierarchy (link traversal, parent rollup, a different result contract and a
different UI), which is a data-model and API change of its own, not a
feature on top of the flat query. Sized `M` it will be discovered as `L`
mid-flight, after its route was confirmed.
Required resolution: split row 3 — chart widgets (M, needs grouping only)
apart from the query-language/tree-and-linked extension (its own task, size
argued explicitly); or state why tree/linked queries do not change the
result contract.
Status: resolved
Resolution: split as suggested — `feat-NNN-dashboard-charts` (M) is now
chart widgets only (grouping/rendering on the existing flat query model);
a new task `mod-NNN-query-hierarchy-operators` (L) carries the operator/
macro extension plus the tree/linked query types, with the size argued
explicitly in the decomposition row (new result contract: link traversal,
parent rollup, not an operator add-on).

### R-7
Severity: major
Affects: § Logical data model, BR-1, BR-2, Operation completeness, doc-001
BR-6, D-10
Problem: two lifecycle holes in the model. (1) The ER says
`USER ||--o{ QUERY_FOLDER : "has (My Queries root, per user, private)"`
while `QUERY }o--|| PROJECT : "is scoped to"` — so it is undefined whether
a user has one personal root per project (as in the reference hub, where
«Мои запросы» and «Общие запросы» both sit inside a project,
`screenshots/13-queries-list.png`) or one global root holding queries of
many projects. That decides navigation, the uniqueness rule for query names
inside a folder, and whether a personal query survives losing access to its
project. (2) Nothing says what happens to a query, a folder, a dashboard or
a widget when its project is deactivated (doc-001 BR-6), when its team is
deleted (release 2, doc-001 Operation completeness), or when its owner is
removed from the organization (D-10) — a personal folder with no owner and
a team dashboard with no team are both reachable states today.
Required resolution: fix the personal-root cardinality in the ER (per user
*per project* recommended, matching the reference) and add a rule covering
orphaning: project deactivated → queries follow it; owner removed → their
personal queries are deleted or transferred (pick one); team deleted →
its dashboards follow. Add the name-uniqueness scope while there.
Status: resolved
Resolution: ER updated to one personal root per user per project (BR-10);
added BR-10 covering name-uniqueness (per immediate folder) and orphaning
— project deactivation carries queries/folders/dashboards with it, owner
removal soft-deletes their personal queries automatically (no transfer —
one owner, nothing to transfer to), team deletion soft-deletes its team
dashboards.

### R-8
Severity: major
Affects: § Non-functional requirements (Volumes), BR-7, US-5
Problem: the only performance statement is borrowed out of its own scope
and the expensive case has none. doc-001's bound is "the work item list and
a board/backlog view … within 2 s for up to 5,000 items in one
project/team/level"; a query result set is explicitly *not* narrowed to a
team or a level (BR-4) and, in release 3, is a hierarchy (tree/linked), so
the 5,000-item figure is asserted for a different shape of read than the
one it was measured for. Worse, BR-7 forbids any caching of widget data
("re-runs that query every time the dashboard is displayed or refreshed"),
which makes opening one dashboard with ten widgets ten full query
executions — and no NFR bounds a dashboard load, a widget count per
dashboard or a refresh interval. This is the requirement that decides
whether dashboards are buildable at all.
Required resolution: state the query-result bound in its own terms
(project-scoped, flat and tree), and add a dashboard bound: max widgets per
dashboard, the load-time target for a full dashboard, and whether widget
queries run in parallel or a short server-side cache window is allowed
(which would soften BR-7 — say so there too).
Status: resolved
Resolution: NFR § Volumes now states the query bound in its own terms
(project-scoped, 2 s flat / 4 s tree up to 5,000 items, explicitly
contrasted with doc-001's team/level-scoped bound); a new § Dashboards
bullet adds a 10-widget cap, a 3 s full-dashboard target, parallel widget
loading, and states that same-page request sharing for an identical query
does not soften BR-7's ban on cross-session caching.

### R-9
Severity: major
Affects: US-3, US-4 sc. 1, BR-8
Problem: US-4 sc. 1 exports "the visible result columns", but no story or
rule ever defines what a result's columns are or whether a user can choose
them. US-3 has a sorting scenario that names three columns in passing
("state, assignee, priority") and nothing else; the reference product has
Column options per query (`research/ado-boards.md` § Queries,
`screenshots/14-query-editor.png`). So the CSV export criterion is not
testable, and the founder task has no basis for deciding whether a saved
query stores a column set.
Required resolution: either add column selection to US-3 as release-2
behaviour and say that a saved query persists its column set (then US-4's
"visible columns" is defined), or out-of-scope column options and fix the
export column set explicitly in BR-8.
Status: resolved
Resolution: added BR-12 (default column set + persistence) and two US-3
scenarios (default set, choose-and-save); US-4's export scenario now
names "the query's persisted column set" instead of "visible columns".

### R-10
Severity: minor
Affects: US-2, § Operation completeness (Query personal → shared)
Problem: there is no way to turn an existing personal query into a shared
one. US-2 covers saving personal, saving shared, and moving a query between
shared sub-folders — but promoting «Мои запросы» → «Общие запросы» is the
normal path (build it privately, then share it) and it raises a question no
other scenario answers: does the query keep its owner, and may a viewer who
owns a personal query ask a member to share it (see R-2)?
Required resolution: add a scenario for the move personal → shared (and
back), naming who may do it and what happens to ownership; or out-of-scope
it explicitly.
Status: resolved
Resolution: added BR-11 (promotion rule) and two US-2 scenarios (member
promotes; viewer cannot promote themselves and must ask a member/admin).
"And back" (shared → personal) is not modelled — a shared object has no
single owner to demote it to, left implicit by BR-3/BR-11's asymmetry.

### R-11
Severity: minor
Affects: US-4, § Operation completeness
Problem: CSV is specified only in terms of rows and validation. Who may
export is never stated (a viewer can read results — can they export them?);
neither are the file's encoding and delimiter, which for a Russian-language
product opened in Excel is a real decision (UTF-8 with BOM, `;` vs `,`),
nor a row/size limit for import, nor whether an import is one transaction
or row-by-row (sc. 2 implies row-by-row for failures, but sc. 3's updates
never say).
Required resolution: add the export permission to the Operation
completeness table or to BR-8, and state encoding/delimiter, the row limit
and the per-row (not per-file) transaction boundary.
Status: resolved
Resolution: BR-8 rewritten with export permission (read-only, viewer
included), import permission (member/admin only), UTF-8+BOM,
semicolon-delimited, 5,000-row cap and one-row-per-transaction; US-4
gained a viewer-cannot-import scenario and both import scenarios now say
"one row is one transaction".

### R-12
Severity: minor
Affects: § Decomposition row 1 deps
Problem: the founder task lists `mod-006-boards-backlogs` as a dependency,
but nothing in this spec has M6 read anything from M4 — the only M4 mention
is BR-4's contrast ("never narrowed to the requester's own team the way a
board is"), which is a non-dependency. The dep is also already implied
transitively through `feat-003-work-item-search`
(`deps:mod-003,mod-006`). Left as is it delays the founder for no stated
reason.
Required resolution: drop `mod-006-boards-backlogs`, or name in the spec
what M6 reads from M4 (doc-001's module diagram draws `M4 --> M6`, so if
something is read, this is the place to say what).
Status: resolved
Resolution: dropped — `mod-006-boards-backlogs` is no longer a dep of
`mod-NNN-queries-dashboards`; nothing in the rewritten spec has M6 read
from M4.

### R-13
Severity: minor
Affects: § Decomposition rows 2 and 3 (deps)
Problem: rows 2 and 3 depend on `mod-queries-dashboards`, an id that
appears nowhere — row 1 defines the task as `mod-NNN-queries-dashboards`.
The queue is filed character by character from these lines.
Status: resolved
Resolution: both deps rewritten to `mod-NNN-queries-dashboards` (spec.md
§ Decomposition).

### R-14
Severity: minor
Affects: § Operation completeness (Dashboard, Create)
Problem: the cell reads "no: one default seeded automatically per team
(release 3); member/admin create additional ones" — it says "no" and then
names who creates them, in the same cell.
Status: resolved
Resolution: rewritten to "member, admin (additional dashboards); the team's
first dashboard is seeded automatically, not created by hand (release 3)".

### R-15
Severity: minor
Affects: US-1 sc. 1
Problem: the scenario uses the state name «Выполняется», which no approved
artifact defines — D-12 fixes only the type and backlog-level names, and
concrete state names are M2/M3 ground (doc-007, doc-002, both unwritten).
A criterion pinned to an invented value will be rewritten later.
Status: resolved
Resolution: the clause now names the field and the state *category*
(InProgress, doc-001 BR-2) and says the concrete names belong to M2.

### R-16
Severity: minor
Affects: US-5 sc. 5
Problem: "the widget shows a broken/removed state" names two different
behaviours (an error placeholder that stays vs. a widget that disappears),
so the scenario is not verifiable.
Status: resolved
Resolution: one behaviour — the widget keeps its place and shows an error
placeholder «Запрос удалён» until a member re-points or removes it; it is
never removed automatically.

### R-17
Severity: minor
Affects: BR-3, § Operation completeness (Query shared, Read)
Problem: BR-3 grants a shared query to "every organization member with read
access to the project", while BR-4 states read visibility as org-wide
(D-9). doc-001 BR-5 says MVP scopes no finer than the organization and puts
per-project overrides in M1's release 2 — so "read access to the project"
is a concept that does not exist yet, and the two rules of the same spec
describe the audience differently.
Status: resolved
Resolution: BR-3 now states org-wide read explicitly (D-9, doc-001 BR-5)
and notes per-project overrides as M1 release-2 ground not modelled here;
the ops table cell now says "(org-wide read, D-9)".

### R-18
Severity: minor
Affects: § Non-functional requirements, doc-001 BR-11
Problem: no concurrency statement at all, although shared queries, shared
folders and dashboard layouts are by definition multi-writer objects and
doc-001 BR-11 (ADR-0010) covers only work items and board/backlog moves.
Checklist item 3 requires concurrent access to be answered.
Status: resolved
Resolution: added a **Concurrency** bullet to the NFR section adopting the
platform default (optimistic concurrency, second write rejected with a
conflict) for shared queries, folders and dashboard layouts, and noting
that a personal query has a single owner and cannot conflict.

### R-19
Severity: minor
Affects: § Glossary, § Non-functional requirements (Localization), US-1
Problem: US-1 quotes the clause connector «И» as UI copy, and the whole
clause builder (Field / Operator / Value columns,
`screenshots/14-query-editor.png`) has no Russian terms in the glossary —
while the release-3 operators are quoted in English ("Under", "Was Ever")
with no statement of whether they are UI labels or internal codes. For a
Russian-UI product (AGENTS.md) that is an open localization question hidden
in punctuation.
Status: resolved
Resolution: glossary gained a clause row («Условие»: «И»/«ИЛИ», «Поле»,
«Оператор», «Значение»), and the macro note now also says operator labels
are ordinary Russian UI copy whose English WIQL names (Under, Was Ever,
Contains) are internal codes.

---

### Human-only questions for gate-1

1. **R-1** — does doc-001 BR-6 ("deletion is always soft") cover metadata
   objects (queries, folders, dashboards, widgets), or only work items,
   organizations and projects? A hard delete here is cheaper and matches
   the reference product; a soft delete keeps one rule for the system.
2. **R-2** — may a `viewer` create, edit and delete their own private
   queries, i.e. does the read-only bundle admit writes on objects the
   acting user owns privately? Answering "yes" changes what M1's permission
   catalog must be able to express (D-9, ADR-0018 amendment).
3. Spec open question 1 — team-only dashboards, or also a project-wide one
   (the reference has both, `research/ado-boards.md`: "Dashboards per
   team/project")?
4. Spec open question 3 — may a personal query bind `@CurrentIteration` to
   any team the owner belongs to, or only to a team inside the query's own
   project (BR-1 argues for the latter)?

Files changed by the reviewer: `spec.md` (R-13, R-14, R-17, R-18, R-19),
`stories/US-1-clause-builder-query-model.md` (R-15),
`stories/US-5-dashboards-widgets-and-charts.md` (R-16).

---

## Round 2 — requirements

Verdict: **approved-by-ai** — every round-1 Resolution holds in the text;
D-13..D-16 are reflected and nowhere contradicted; the rework introduced one
major (R-20) and nine minors (R-21..R-29), all small enough to fix in place
and all fixed. No question is left that only the human can answer; the
numbers listed at the end are the analyst's and the reviewer's product
choices, flagged for the human to see at gate-1, not open questions.

Scope (delta, per stage rules): the round-1 findings, the human decisions
D-13..D-16 (`status.md` § Decisions), the reworked `spec.md` (235 lines as
delivered) and `stories/US-1..US-5`, cross-checked against doc-001
§§ Roles, MVP/Release 2/Release 3, BR-5/6/9/11, Operation completeness, NFR,
Decomposition, `research/ado-boards.md` § Queries (macro list),
`docs/backlog.md` (dep ids) and `docs/ai-workflow/routing.md` (L thresholds).

### Verification of round-1 Resolutions

| Finding | Resolution claim | Verified in text | Result |
|---|---|---|---|
| R-1 | BR-9 soft delete for all four M6 objects, ops table rewritten, open question 2 dropped, «Запрос удалён» placeholder | BR-9, ops table Delete/Cancel columns, US-5 sc. "Soft-deleting a query used by a widget" / "Restoring…", § Open questions | holds; the mechanism named («Удалена», a WorkItem state) was wrong → R-23 |
| R-2 | BR-2 + Roles state the ownership write as a departure; carry-back row; founder depends on it; US-2 viewer scenario | § Roles paragraph, BR-2, decomposition row 1 and founder deps, US-2 sc. 2 | holds; departure stated honestly, not as doc-001 fact; quote misattributed → R-28; carry-back row claimed an ADR → R-20 |
| R-3 | explicit release-2 field table incl. Итерация; "not a new surface" claim dropped | US-1 § Query language table 1 | holds |
| R-4 | field × operator × macro tables, six macros closed, raw-text sentence, US-1 sc. 3 names operators | US-1 tables 1–3, closing paragraph, sc. 3 ("=", "In") | holds; all six research macros (`ado-boards.md` line 79) closed; release-3 table had defects → R-26 |
| R-5 | founder release-2 only; `mod-NNN-dashboards-widgets` founds dashboards | decomposition rows 2–3 | holds; release labels were still missing on most rows → R-25 |
| R-6 | charts split from hierarchy/operators, L argued | decomposition rows 5–6 | split holds; the L argument cited no threshold → R-25 |
| R-7 | per-user-per-project root, uniqueness, orphaning | ER, BR-10 | holds |
| R-8 | project-scoped 2 s / 4 s bound, 10 widgets, 3 s, parallel, BR-7 note | NFR § Volumes, § Dashboards | holds; 3 s contradicted 4 s → R-21; cap untested → R-29 |
| R-9 | BR-12 default + persisted columns, US-3 two scenarios, US-4 names the persisted set | BR-12, US-3 sc. 4–5, US-4 sc. 1 | holds |
| R-10 | BR-11 promotion, two US-2 scenarios | BR-11, US-2 sc. 5–6 | holds |
| R-11 | BR-8 export/import permission, encoding, delimiter, cap, per-row transaction | BR-8, US-4 sc. 1–4 | holds; behaviour beyond the cap undefined → R-22 |
| R-12 | `mod-006-boards-backlogs` dropped | founder deps | holds (still reached transitively through `feat-003-work-item-search`) |

D-13..D-16: D-13 → BR-9, ops table, US-2/US-5; D-14 → § Roles, BR-2, ops
table, US-2, decomposition row 1 + founder dep; D-15 → ER (`level`,
`isDefault`), glossary, ops table (two Dashboard rows), US-5 sc. 1, 2, last;
D-16 → ER (`QUERY }o--o| TEAM`), BR-6, US-1 sc. 2 (narrowed beyond D-16 →
R-27). No contradiction left after the fixes below.

Release line vs doc-001: release 2 = founder (queries, folders, clause
builder, results) — doc-001 § Release 2; release 3 = dashboards/widgets,
charts, CSV, hierarchy/operators ("WIQL-level language") — doc-001
§ Release 3. Matches after R-25 labelled every row.

Line budget: `spec.md` ~240 lines after the fixes, under the ~250 ceiling
(`stages/01-business-analysis.md`); the US-1 tables are user-observable
behaviour (which field accepts which operator and macro), so requirements,
not design.

### R-20
Severity: major
Affects: § Decomposition row `doc-NNN-personal-object-ownership`, founder deps,
doc-001 § Decomposition (ADR note), routing.md L thresholds
Problem: the carry-back row was sized `M` while listing "ADR-0018 amendment"
as its content. Accepting an ADR is an L threshold (routing.md), and doc-001
already states an analysis task writes none — the ADR is owed by the code
task that implements the check (`mod-002-organizations-projects-teams`
for D-9). As written, M6's founder also depended only on a docs task: a
document landing does not make M1's catalog able to express an ownership
check, so the dependency did not guarantee what the founder needs.
Required resolution: the row writes doc-001's amendment only, names the M1
code task that owes the ADR and the check, and M6's founder depends on that
code task as well.
Status: resolved
Resolution: row rewritten — amends doc-001 Roles/BR-5 and states what M1's
catalog must express, writes no ADR, names `mod-002-organizations-projects-teams`
(or a follow-up M1 task if that has shipped) as owner of the ADR-0018
amendment and the check, and makes M6's founder depend on it too; `M`
argued (no threshold fires).

### R-21
Severity: minor
Affects: NFR § Volumes, § Dashboards
Problem: a tree/linked query may take 4 s, yet a dashboard holding a widget
backed by one had to be interactive in 3 s — two numbers of the same spec
that no implementation can both meet.
Status: resolved
Resolution: the 3 s bound now applies to widgets backed by flat queries
within the stated volume; a tree/linked widget is held to its own 4 s and
the rest of the dashboard does not wait for it.

### R-22
Severity: minor
Affects: BR-8, US-4
Problem: "capped at 5,000 rows" did not say what happens at 5,001 — refuse,
or truncate (silently losing rows) — for either export or import. Not
testable.
Status: resolved
Resolution: BR-8 — an import over the cap is refused whole before any row
is processed; an export over the cap is refused with a request to narrow
the query; never a truncated file. US-4 gained a scenario for both.

### R-23
Severity: minor
Affects: BR-9, § Operation completeness, US-2, US-5, BR-2
Problem: (1) BR-9 moved M6 objects "to «Удалена»" — that is the WorkItem
process state (doc-001 BR-6), which queries and dashboards do not have, and
the spec's own data-model note says they carry a status like
`PROJECT.status`; the label is also feminine, wrong for «Запрос».
(2) "Only an administrator purges" collided with BR-2's "visible only to the
owner regardless of role" for a personal query.
Status: resolved
Resolution: BR-9 names the deleted-status mechanism (project-style, doc-001
BR-6) and says purge is the only admin action on another user's personal
object, with no read of its content and no restore; ops table and US-2/US-5
say "soft (BR-9)" / "soft-deleted".

### R-24
Severity: minor
Affects: BR-7, BR-2, US-5
Problem: nothing stopped a widget from being backed by a personal query, so
every org member reading the dashboard would see results of an owner-only
query — a hole in BR-2.
Status: resolved
Resolution: BR-7 — only a shared query may back a widget; US-5 gained a
scenario.

### R-25
Severity: minor
Affects: § Decomposition (all M6 rows)
Problem: (1) only one row named its release, so the release-2/3 line of
doc-001 was not readable from the backlog rows; (2) the L rows argued size
by volume ("new result contract", "founds entities"), not by routing.md's
thresholds — and a new endpoint/DTO is explicitly not breaking there;
(3) deps missing: the founder soft-deletes (BR-9) but did not depend on
`mod-005-work-item-delete-restore`, which founds that pattern and owes its
ADR; tree/linked queries traverse Parent/Child links but did not depend on
`feat-001-work-item-links`.
Status: resolved
Resolution: every row labelled Release 2 / Release 3; L argued by threshold
— founder: a new module; dashboards: a new slice plus seeding on M1's team
creation (a boundary change of another module); hierarchy/operators: a new
history read surface in M3 (boundary change); deps added
(`mod-005-work-item-delete-restore` to the founder,
`feat-001-work-item-links` to hierarchy/operators). Both ids verified in
`docs/backlog.md`.

### R-26
Severity: minor
Affects: US-1 release-3 table
Problem: "Changed Date" was listed as an operator on Статус (it is a field,
already a separate row as Дата изменения); "In Group" named a concept
MUSTASK does not have; Дата изменения had no source.
Status: resolved
Resolution: Статус adds Was Ever only (reads history, doc-001 BR-4); In
Group's group is a team of the query's project; Дата изменения is defined
as the latest history revision's time; decomposition row text aligned.

### R-27
Severity: minor
Affects: US-1 sc. 2, BR-6, D-16
Problem: (1) the scenario offered only "the member's teams" of the project —
a restriction D-16 does not make (D-16: any team inside the query's own
project); (2) a query bound to a team that is later deleted (release 2+)
had no rule.
Status: resolved
Resolution: US-1 sc. 2 offers any team of the query's project; BR-6 — a
query whose bound team is deleted cannot run until another team of its
project is chosen.

### R-28
Severity: minor
Affects: § Roles, § BPMN, US-2 sc. 3 and "Organizing shared queries"
Problem: (1) § Roles put in quotes, as doc-001 BR-5 text, a phrase ("no
per-object escape hatch") that is round-1 reviewer paraphrase, not doc-001;
(2) R-17's fix reached `spec.md` only — US-2 still said "with read access to
the project" and the BPMN "project-wide read".
Status: resolved
Resolution: quote replaced with doc-001 § Roles' actual sentence; US-2 and
the BPMN now say org-wide read (BR-3).

### R-29
Severity: minor
Affects: US-5, NFR § Dashboards
Problem: the 10-widget cap had no scenario; US-5 sc. 1 seeded a default
dashboard only for a "newly created team", leaving every team that existed
before release 3 without one.
Status: resolved
Resolution: US-5 gained an eleventh-widget-refused scenario; sc. 1 covers
teams existing when dashboards ship; the dashboards row names both.

### For the human at gate-1 (not open questions)

Product numbers chosen in the rework, now normative — confirm or change at
gate-1: 10 widgets per dashboard (the reference allows far more); 3 s flat /
4 s tree dashboard and 2 s / 4 s query bounds; 5,000-row CSV cap with
refusal, not truncation (R-22, reviewer's choice); CSV as UTF-8 + BOM with
`;`.

Files changed by the reviewer in round 2: `spec.md` (R-20..R-25, R-27,
R-28, R-29), `stories/US-1-clause-builder-query-model.md` (R-26, R-27),
`stories/US-2-personal-and-shared-queries.md` (R-23, R-28),
`stories/US-4-csv-import-export.md` (R-22),
`stories/US-5-dashboards-widgets-and-charts.md` (R-23, R-24, R-29).
