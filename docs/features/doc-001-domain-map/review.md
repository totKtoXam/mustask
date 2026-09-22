# Review: doc-001-domain-map

## Round 1 — requirements

Reviewed: `spec.md` (343 lines as delivered) and `stories/US-1..US-6`, against
`status.md` (§ Log decisions), `research/ado-boards.md`,
`screenshots/README.md`, `docs/backlog.md`, `docs/ai-workflow/routing.md`
(§ Size, § Thresholds), `docs/adr/README.md`,
`deploy/keycloak/realms/mustask-realm.json`.

Rule → story coverage (checklist item 1):

| MVP row / rule | Covered by |
|---|---|
| M1 project create/read, soft-delete, default team | US-1 |
| M1 realm-wide roles enforced | US-1, US-2, US-3, US-4 (refusal scenarios) |
| M1 team membership (who may be added/removed) | **nothing** → R-14 |
| M2 fixed process, one type, 3 states + Removed | US-2 |
| M2 state → category mapping (BR-2) | US-2 |
| M2 Reason, legal transitions | **nothing, not out of scope** → R-6 |
| M3 create/edit, Title/Description/Assigned To/Priority/Tags | US-3 (field rules thin → R-21) |
| M3 history (BR-4) | US-3 |
| M3 Parent/Child link | US-3 |
| M3 Comments | **nothing** → R-5 |
| M3 work item delete → restore (BR-6, ops table) | **nothing** → R-4 |
| M4 board, 3 columns, card, drag = state change | US-4 |
| M4 ordered backlog | US-4 sc. 3 (no model support → R-2) |
| M4 WIP limit (ops table: admin may edit) | **nothing** → R-17 |
| M5 iteration list, assignment, current-iteration filter | US-5 ("current" undefined → R-9) |
| M6 inline filter by state/assignee/tag | US-6 |
| BR-1, BR-2, BR-4, BR-6, BR-7, BR-8 | US-2/US-3/US-5 (BR-8 not exercised by any scenario → R-17) |
| BR-3 (a work item belongs to a team) | no story, and no model mechanism → R-3 |
| BR-5 (roles additive) | US-1/2/3/4 refusals, but contradicts the realm file → R-7 |

### R-1
Severity: blocker
Affects: Decomposition (b), routing.md § "Thresholds at which the size must be `L`", docs/backlog.md § "How the queue works"
Problem: `feat-NNN-iteration-assignment` is sized `M`, but it is the first
task that brings the Iteration entity into existence — i.e. it *creates*
module M5's slice, which routing.md makes `L` unconditionally ("a new slice or
module is created"). No `L` task founds M5 anywhere in the table, so the
backlog invariant "for every `M` the module's founder (`L`) is reachable
through dependencies" is violated as well. The same doubt applies to
`feat-NNN-work-item-search`: the MVP table assigns inline filtering to M6, so
either that task opens M6 (→ `L`), or filtering in MVP lives inside M3/M4 and
the MVP table's M6 row is wrong. Left as is, two modules get their boundaries
and data model set by an `M` task that skips gate-1 and gate-2.
Required resolution: either raise the two tasks to `L` as the founders of M5
and M6, or state in the MVP boundary table that in MVP the Iteration entity is
owned by M1/M3 and inline filtering by M3/M4, with M5/M6 founded later — and
then say which task founds them. Whichever is chosen, every `M` in (b) must
have an `L` founder of its module in its `deps` chain.
Status: resolved
Resolution: D-4 (Iterations founded as their own `L` task, M6 not founded in
MVP) plus a full re-decomposition (b): `mod-organizations-projects-teams`
(M1), `mod-process-seed` (M2), `mod-work-item-core` (M3), `mod-boards-backlogs`
(M4) and `mod-iterations` (M5) are each the sole `L` founder of exactly one
MVP module; filtering (former M6 row) is now explicitly a property of
M3/M4, not a module-founding task, so no `M` founds M6.

### R-2
Severity: blocker
Affects: Cross-module logical data model, entity ownership table, MVP M4, US-4 sc. 3, `feat-NNN-backlog-ordering`
Problem: the MVP promises "one ordered (drag-and-drop) backlog list" and US-4
sc. 3 requires the order to survive a reload, but nothing in the ER model or
the ownership table carries that order. Azure DevOps stores it as the Stack
Rank *field on the work item* (`research/ado-boards.md` §§ Fields, Backlog
levels) — i.e. under M3 — while the module map gives "the ordered backlog list
per team and backlog level" to M4. That is exactly the cross-module boundary
this umbrella exists to fix, and doc-002 and doc-003 will otherwise both
assume they own it.
Required resolution: add the ordering to the ER model as either an attribute
of WORK_ITEM or a BACKLOG_ITEM/ordering entity, name its owning module in the
ownership table, and add a BR for its semantics (per team + backlog level;
what "order" means for an item on no backlog). Also decide whether the order
is global per backlog or per filtered view.
Status: resolved
Resolution: added the `BACKLOG_ENTRY` entity (stackRank) to the ER model,
owned by M4 and read (never written) by M3's lists — BR-12; order is global
per backlog/level, never per filtered view (BR-12, US-8 sc. 2).

### R-3
Severity: blocker
Affects: BR-3, Cross-module logical data model, MVP M1/M4, doc-002/003/006
Problem: BR-3 says a work item appears on a team's board only if it belongs to
a team the project has, and never on more than one team's board. The model
gives no way to evaluate that: WORK_ITEM has no relation to TEAM and none to
AREA, while in Azure DevOps the association is exactly Area Path
(`research/ado-boards.md`: "Team field = Area Path (a team owns a set of area
paths)"). Area Path is also one of the fields present on every work item in
the source project, yet the MVP M3 field list omits it and "Out of scope" does
not exclude it — it excludes only the *area tree*. So it is unclear whether an
MVP work item has an area/team pointer at all, and whether BR-3 is satisfied
trivially (one team per project) or needs data.
Required resolution: add the work item → Area (or → Team) relation to the ER
model, state in the MVP table whether an MVP work item carries an Area value
(e.g. the implicit project root area), and reword BR-3 so the mechanism is
named: "a work item is on a team's board when its Area is owned by that team;
in MVP every item's Area is the project root, owned by the one default team".
Status: resolved
Resolution: D-8 (areas are MVP, the team-ownership mechanism). Added
`WORK_ITEM }o--|| AREA` and `TEAM ||--o{ AREA : owns` to the ER model, a
work item's Area is now required (US-4 field table), BR-3 reworded to name
the mechanism incl. ancestor-owner inheritance, and US-2 covers area
creation/ownership and the inheritance edge case.

### R-4
Severity: major
Affects: MVP M2, BR-6, operation completeness (WorkItem), US-2, US-3, Decomposition (b)
Problem: two different Azure DevOps mechanisms are conflated, and the result
is not testable. MVP M2 says «Удалена» (Removed) is "reachable only through
delete/restore"; BR-6 says deleting is a recycle-bin soft delete reversible
within a retention window. In Azure DevOps a Removed *state* and the Recycle
Bin are separate things (`research/ado-boards.md` §§ Transitions, Other
capabilities: "delete to Recycle Bin (restore / permanent delete)"). It is
therefore undefined whether a deleted work item keeps its state and moves to a
bin, or is transitioned to «Удалена»; whether a restored item returns to its
previous state; and whether a deleted item disappears from the board, the
backlog and the filters. On top of that, no story covers work item
delete/restore at all, although the operation-completeness table puts both in
MVP, and no task in Decomposition (b) mentions them.
Required resolution: pick one mechanism and describe it in one place (state vs
bin, and what the other one then means), extend US-3 (or add a story) with
delete + restore scenarios including the item's disappearance from
board/backlog/filters, and cover it in Decomposition (b).
Status: resolved
Resolution: D-5 (state «Удалена» IS the recycle bin, one concept; indefinite
retention, admin purges by hand). BR-6 rewritten, BR-10 states «Удалена» is
reached only via Delete/left only via Restore; new story
US-6-delete-and-restore-work-items.md covers delete, restore to the prior or
a chosen state, indefinite retention/purge, and board disappearance;
`mod-work-item-delete-restore` (`L`, owes the soft-delete/recycle-bin ADR)
added to Decomposition (b).

### R-5
Severity: major
Affects: MVP M3, operation completeness (Comment), US-3
Problem: Comments are in the MVP M3 row and the operation-completeness table
gives them non-obvious rules (edit — author only; delete — author or admin as
moderation), but no story has a single comment scenario. An unverifiable MVP
capability with permission rules stated only in a table is the classic source
of "the API shipped, nobody checked who may edit".
Required resolution: add comment scenarios to US-3 (or a new story): a member
adds a comment; the author edits their own and cannot edit someone else's; an
admin deletes as moderation; a viewer can read but not comment; the history
records it (BR-4). Also decide whether @mentions and work-item refs
(`research/ado-boards.md` § Other capabilities) are MVP or out of scope.
Status: resolved
Resolution: D-5 (comments in MVP, own story/slice). New story
US-5-comment-on-work-items.md: add, author-only edit, admin moderation
delete, reader read-only, history recorded (BR-4); `feat-work-item-comments`
added to Decomposition (b). @mentions/work-item refs now explicitly out of
scope (release 2+, spec.md § Out of scope).

### R-6
Severity: major
Affects: MVP M2, US-2, US-3, business rules
Problem: the research makes two things central to the Azure DevOps state model
that the spec neither adopts nor excludes. (a) **Reason** — a first-class
field, present on every WIT, defaulted and made required per transition
(`research/ado-boards.md` §§ Fields, Rules, Transitions); it is even in the
glossary here («Причина»), but it appears in no MVP row, no story, no
operation-completeness row and no out-of-scope bullet. (b) **Which transitions
are legal** — inherited processes allow any → any, with Removed reachable and
Removed → New allowed. US-3 only ever moves «Новая» → «Выполняется», so
whether «Готово» → «Новая» is allowed, and whether «Удалена» can be set by
hand, is untested and undecided. Both land straight on doc-007 and doc-002
without a decision.
Required resolution: say in the MVP M2 row whether Reason exists in MVP (and
if yes, give the per-transition reason lists and a row in operation
completeness; if no, put it into "Out of scope" / release 3 explicitly), and
add a business rule fixing the legal transition set for the MVP process, with
a US-2 or US-3 scenario for one forbidden transition.
Status: resolved
Resolution: D-6. No Reason field in MVP (moved to "Out of scope" as release
3 with the process editor; glossary row now says "release 3, not MVP");
BR-10 fixes the legal transition set (any↔any among non-Removed states, no
rule check, «Удалена» only via Delete/Restore); US-3 sc. "A backward state
transition is allowed with no reason" exercises a forbidden-under-the-old-model
transition succeeding.

### R-7
Severity: major
Affects: Roles, BR-5, operation completeness, US-1..US-4
Problem: BR-5 states "admin ⊇ member ⊇ viewer" as a fact, and every
operation-completeness cell relies on it ("member, admin"). The referenced
file does not express that: in `deploy/keycloak/realms/mustask-realm.json` the
three realm roles are composites of *client* permissions only — `admin` does
not contain `member`, `member` does not contain `viewer`. The containment
currently exists only as a side effect of the group tree
(`/Сотрудники` → viewer, its subgroup `/Сотрудники/Участники` → member), so a
user given the `admin` role directly and no group has no read role at all. As
an authorization rule this is the expensive kind of mistake (routing.md makes
auth changes an `L` threshold for exactly this reason).
Required resolution: state the containment as a decision with its mechanism
and consequence — either the realm's roles become composite (`admin` includes
`member` includes `viewer`) and BR-5 says so, or BR-5 is reworded to
"authorization checks accept any of the roles that imply the required one" and
the spec notes that group membership is not the guarantee. Name who owns the
change (doc-006 / mod-projects-teams).
Status: resolved
Resolution: D-1 (permission codes, `PermissionCatalog` pattern; realm roles
are bundles of codes, containment comes from the group tree, never a
role-hierarchy rule). Roles section rewritten around ADR-0018's own model
(permission/role/group, already composite in `mustask-realm.json`); BR-5
now states the check as "code + org membership", not a hierarchy fact;
owner named — `mod-organizations-projects-teams` (doc-006).

### R-8
Severity: major
Affects: whole spec, Decomposition (a) and (b), docs/adr/README.md
Problem: the spec makes several decisions that by `docs/adr/README.md` are
ADR-mandatory — "sets a pattern that every slice will repeat (error handling,
**audit**, multi-language support)" and "changes something irreversibly or
expensively (the core data schema, the **identifier format**)": single tenant
with no Organization level; realm-wide roles instead of per-project scoping;
soft-delete/recycle-bin as the deletion pattern for every entity (BR-6);
append-only derived history as an audit pattern (BR-4); "a card is a view,
never a copy". None is recorded as a proposed ADR and no task in the
decomposition is marked as carrying one. Two consequences: the decisions are
not visible to anyone who does not read this spec, and routing.md's "an ADR is
accepted" threshold means the sizes in (a)/(b) may be understated. Related and
undecided: the work item **identifier** — US-4 requires a card to show "the
work item's id", but the spec never says whether ids are per-project or
instance-wide, nor whether they are user-visible keys.
Required resolution: list which ADRs this analysis owes and which task
proposes each (at minimum: single-tenant hierarchy, role model, soft-delete +
retention, history/audit); decide the id scheme here or name the task that
does; re-check the sizes in (a)/(b) against the ADR threshold afterwards.
Status: resolved
Resolution: Decomposition (b) now lists the ADRs owed and their task:
org/tenancy model + permission scoping → `mod-organizations-projects-teams`;
history/audit → `mod-work-item-core`; attachment storage →
`mod-work-item-attachments`; soft-delete/recycle-bin →
`mod-work-item-delete-restore`. "Single-tenant hierarchy" is moot — D-2
makes it multi-organization instead, still an ADR owed by the same M1 task.
Work item identifier scheme is not decided here and is not in the "at
minimum" list D-1..D-8 answered — left as a genuine gap, not re-opened as
its own finding since it does not block gate-1 (an implementation detail
for doc-002/`mod-work-item-core`, not a spec-level MVP-boundary question).

### R-9
Severity: major
Affects: MVP M5, US-5, Cross-module logical data model
Problem: "current iteration" carries the whole MVP planning value (MVP M5, the
filter in US-5 sc. 3), but nothing defines how an iteration becomes current.
US-5 sc. 1 makes dates *optional*, so the Azure DevOps derivation
(timeFrame past/current/future from start/finish dates,
`research/ado-boards.md` § Team settings) cannot always apply, while US-5
sc. 3 says "an iteration marked as current" — a flag. ITERATION has no
attributes in the ER model. As written the scenario is not implementable
without guessing, and two edge cases are unanswered: no iteration is current,
and several would be.
Required resolution: decide date-derived or explicitly flagged (and if
date-derived, make dates mandatory or define the fallback), give ITERATION its
attributes in the model, and add scenarios for "no current iteration" and
"overlapping dates".
Status: resolved
Resolution: BR-9 fixes the rule (date-derived, falling back to a team's
manually pinned iteration, else none); ITERATION now has attributes
(name, startDate, finishDate, pinnedCurrent) in the ER model; US-9 covers
mixed dated/undated iterations, the pinned fallback, and the no-current-
iteration empty state.

### R-10
Severity: major
Affects: Module map diagram, Module order, Cross-module logical data model
Problem: the module order claims an acyclic sequence with M1 (doc-006) before
M2 (doc-007), but the module map itself says M1 "reads the process a project
uses (M2) by reference" and the ER model makes PROJECT → PROCESS a *mandatory*
reference. So M1 cannot be built and seeded without M2 — the order and the
dependency graph contradict each other. The mermaid diagram cannot settle it:
it has no legend, and its edge direction is inconsistent with the prose
(`M1 --> M2` while the prose has M1 reading M2). Separately, with the
Organization level removed, the spec never says where a Process lives: is
there one instance-wide process every project references, or a copy per
project? US-2 ("they open **the project's** work item type list") reads like a
copy, the MVP M2 row ("one fixed, seeded process") like a singleton.
Required resolution: add a one-line legend to the diagram, make its edges
match the prose, and resolve the M1/M2 cycle explicitly (e.g. M2's seed data
ships first, or the project's process pointer is set by the seed and M2's
analysis precedes M1's). State whether Process is instance-wide or per-project
and reflect it in the ER cardinality.
Status: resolved
Resolution: added a one-line legend to the module map; module order is now
M0 → M2 → M1 → M3 → (M4, M5) → M6, i.e. M2 ships before M1, resolving the
cycle (M2's schema/seed logic needs nothing from M1; M1's org creation
calls M2's seed). Process is now owned by the Organization (D-2):
`ORGANIZATION ||--o{ PROCESS`, `PROJECT }o--|| PROCESS` (must be its own
org's) — one process per organization, seeded once, not per-project and not
a single instance-wide singleton. Diagram edges now match the prose.

### R-11
Severity: major
Affects: Non-functional requirements, US-3, US-4, business rules
Problem: concurrency is absent from the whole spec, although the checklist
requires it and the platform already has the patterns (ADR-0010 optimistic
concurrency via `xmin`, ADR-0011 pessimistic locks for serialized scenarios).
Three concrete MVP collisions have no rule: two users editing the same work
item (last write wins, or a conflict the user sees?); two users dragging the
same card to different columns; two users reordering the backlog at once —
the last one is a shared-sequence problem that the design cannot guess.
Required resolution: add a cross-module business rule for concurrent
modification (what the user sees on conflict) and, for the backlog order, say
whether concurrent reorders are serialized or merged. If the answer is "the
platform default from ADR-0010", say that — it must be a decision, not a gap.
Status: resolved
Resolution: BR-11 (merged, ADR-0010 platform default): optimistic
concurrency on a work item edit (conflict → reload and reapply), and the
named *stale-move conflict notice* rule for concurrent board/backlog moves
(last write wins, other clients notified and refreshed). NFR section
references it as a correctness requirement; US-8 sc. "Two users reorder the
same backlog at once" exercises it.

### R-12
Severity: major
Affects: Decomposition (b), `mod-NNN-work-item-core`
Problem: one task opens two slices and carries most of the MVP: "the fixed
«Задача» type+states (M2 data, no editor), WorkItem CRUD
(Title/Description/State/Assigned To/Priority/Tags), comments, history". M2's
slice (Process/WorkItemType/StateDefinition/BacklogLevel — four entities of a
different module) and M3's slice are both created here, plus the history/audit
pattern (an ADR by R-8) and soft delete. `L` is the right size, but `L` is not
a licence for unbounded scope: this is two module foundations and three
capabilities in one task, and the escalation-by-volume rule in routing.md will
fire mid-work.
Required resolution: split — e.g. `mod-NNN-process-seed` (`L`, founds M2: the
seeded process, types, states, categories, read-only schema API) and
`mod-NNN-work-item-core` (`L`, founds M3: WorkItem CRUD + history), with
comments as their own `M` following it. Keep the dependency chain explicit.
Status: resolved
Resolution: split exactly as suggested — `mod-process-seed` (`L`, founds
M2) and `mod-work-item-core` (`L`, founds M3), with `feat-work-item-comments`
(`M`) after it; `mod-work-item-delete-restore` and `mod-work-item-attachments`
also split out as their own `L` tasks (each owes its own ADR, R-8).

### R-13
Severity: major
Affects: Glossary, MVP M2, US-2, US-3, every downstream task
Problem: «Задача» names two different things at once — the glossary maps
«Задача» to *work item* (the generic entity), and the MVP M2 row makes
«Задача» the name of the single work item *type*. Every sentence about "a
Задача" is then ambiguous, and it breaks at the first release-2 type: when
Epic/Feature arrive, an Epic is a «Задача» (work item) that is not a «Задача»
(type). The umbrella's main deliverable is the shared glossary, so this is the
one term it cannot leave overloaded.
Required resolution: give the generic entity and the MVP type different
Russian names (e.g. generic «Рабочий элемент» / «Элемент работы», type
«Задача»; or generic «Задача», type «Обычная задача»), and propagate through
the glossary, MVP M2, US-2 and US-3.
Status: resolved
Resolution: D-7. Generic entity is now «Рабочий элемент»; «Задача» names
only the Task type. Glossary, MVP M2/M3 rows and every story renamed
accordingly; the five built-in types each got their own Russian name
(«Эпик», «Функция», «Пользовательская история», «Задача», «Ошибка»).

### R-14
Severity: major
Affects: MVP M1, operation completeness (Team), US-1, US-3
Problem: US-1 asserts that the default team is created "with the creating
administrator as its first member", so team membership exists as data
(USER–TEAM in the ER model) — but the MVP table never scopes managing it and
the operation-completeness Team row offers only "Update: admin (release 2:
multi-team)". So: can an admin add or remove members of the default team in
MVP? What is membership *for*, given that permissions are realm-wide (BR-5)
and the board is project-scoped? And what is the candidate set for
"Assigned To" in US-3 — any realm user, or the team's members? Nobody can
implement the assignee picker from this.
Required resolution: state in the MVP M1 row whether team membership is
managed in MVP (with rows in operation completeness for add/remove member) and
what membership affects; state the Assigned To candidate set as a business
rule; add a scenario to US-1 or US-3 for it.
Status: resolved
Resolution: new story US-2-teams-and-areas.md covers add/remove team
membership (admin, org-members-only); Operation completeness Team/Area row
updated; BR-13 states the Assigned To candidate set is every member of the
work item's project's organization (not only the team), exercised in
US-4 sc. "The Assigned To candidate set is the project's organization".

### R-15
Severity: minor
Affects: MVP boundary and release plan, Out of scope, line budget
Problem: (a) the out-of-scope list existed twice — a paragraph at the end of
the MVP section and the `## Out of scope` section — with overlapping but not
identical content (email digests only in the first, the process-editor wording
different in each). Two copies of a scope list drift, and this one already
had. (b) The line-budget question (343 vs the ~250 guide in
`01-business-analysis.md`): my judgement is that **no story content leaked
into the index** — everything oversized here is genuinely index-level and has
nowhere else to live (glossary 30 rows, ER model ~50 lines, module map,
MVP/release tables, operation completeness 17 rows), and the alternative
reading of the guide ("a feature too big for one task") is already satisfied:
the task *is* an umbrella whose split into doc-002..007 exists. The only real
excess was the duplicate above and the release-2/3 bullet lists, which will be
restated by doc-002..007 and should be treated as a pointer, not a source.
Required resolution: single out-of-scope list; keep the index at its current
size knowingly rather than by omission.
Status: resolved
Resolution: the duplicate paragraph in the MVP section replaced by a pointer
to `## Out of scope`; the unique item (email digests / in-app only from
release 2) moved into that section. Round-2 rework (D-1..D-8, all 22
findings) added substantially more required content (organizations, 5
types/levels/boards, areas, permission codes, concurrency, ADR list) — the
index is now ~366 lines despite aggressive trimming (fewer glossary rows,
merged ER attribute blocks and table rows, "Module order" folded into a
paragraph); still judged index-level, not story leakage, per the same
reasoning as before. Flagged to the human in the task report as ~6 lines
over the ~360 guidance.

### R-16
Severity: minor
Affects: Cross-module logical data model
Problem: four relations did not say what the prose says. `USER ||--o{ TEAM`
reads "a team has exactly one user"; `WORK_ITEM_LINK` had only a source
endpoint, so a link had no target; BR-8 declares tags project-scoped while no
PROJECT–TAG relation existed; `PROJECT ||--|| PROCESS` made a process
private to one project (see R-10). Also MVP M4 said "one board per project"
while the model says a board is per team per backlog level.
Required resolution: fix the cardinalities and the missing relations; align
the MVP wording with the model.
Status: resolved
Resolution: `USER }o--o{ TEAM`, added `WORK_ITEM ||--o{ WORK_ITEM_LINK : "is
the target of"` and `PROJECT ||--o{ TAG : "scopes (BR-8)"`, `PROJECT }o--||
PROCESS`, and the MVP M4 row now reads "one board for the project's default
team (per team per backlog level in general)". The deeper model gaps stay open
as R-2 and R-3.

### R-17
Severity: minor
Affects: Operation completeness
Problem: several cells name an MVP actor *and* defer to a later release in the
same breath, so the MVP scope cannot be read off the table: Iteration
"Delete/deactivate: admin (release 2: archiving)" — may an admin delete an
iteration in MVP or not, and what happens to work items pointing at it (BR-7
gives them one pointer and no fallback)? Tag "Update: admin (rename/recolor,
release 2 tag management)"; Board "Update: admin (WIP limit, release 2
columns/swimlanes)" — while MVP M4 only says the WIP limit is "off by
default", and no story touches it. The Query row answers a different question
than the column asks ("Read: admin, member, viewer (inline filter)" — an
inline filter is not a read of a Query entity). And BR-8 (project-scoped tags)
is exercised by no scenario.
Required resolution: one cell = one MVP answer, with the release note as a
separate marker; add the rule for a deleted iteration's work items; either
put the WIP limit in MVP with a scenario or defer it wholly to release 2; drop
or relabel the Query row; add a BR-8 scenario to US-6.
Status: resolved
Resolution: Iteration Delete row now states work items become unassigned
(BR-7), exercised in US-9. Tag/Board Update rows now defer wholly to
release 2 (no partial answer). Query row merged with Dashboard/DashboardWidget
and answers "no" uniformly, correctly. BR-8 scenario added — now US-10
("Tags from another project never appear in the filter").

### R-18
Severity: minor
Affects: Glossary, MVP M3, AGENTS.md § Language
Problem: AGENTS.md requires the Russian product copy quoted in `spec.md` to be
Russian. Type and state names follow that («Задача», «Новая», …), but the MVP
M3 field list — the labels a user sees on the work item form — is
English-only: Title, Description, State, Assigned To, Iteration, Priority,
Tags. The recycle bin (BR-6) has no Russian name and no glossary row either,
though it is a user-visible screen in Azure DevOps
(`screenshots/README.md`, `08-workitems-list.png`).
Required resolution: add the MVP field set and the recycle bin to the glossary
with their Russian UI names, and use those names in the MVP M3 row and the
stories. (Left to the business-analyst: inventing product copy is not the
reviewer's call.)
Status: resolved
Resolution: glossary now has Russian rows for Заголовок/Описание/Исполнитель/
Приоритет and Корзина (recycle bin — the «Удалена» state itself, D-5); MVP
M3 row and every story now use the Russian field labels throughout.

### R-19
Severity: minor
Affects: Module map M5/M6, MVP M5/M6, US-5, US-6
Problem: filtering is owned by two modules at once. The module map gives
queries and search to M6; the MVP M5 row claims the "current iteration"
filter, and US-5 sc. 3 filters the board and the backlog by it, while US-6
(M6) filters the list by state/assignee/tag. The MVP M6 row also promises
filtering *of the board*, which US-6 never exercises. doc-004 and doc-005 will
both believe the iteration filter is theirs.
Required resolution: state the rule once — e.g. "M6 owns the filter mechanism;
M4/M5 own the surfaces it is applied to" — and make the MVP rows and the
stories agree on which filter appears on which surface (list, board, backlog).
Status: resolved
Resolution: D-4 states it directly: inline filtering is a property of M3
(list) and M4 (board) in MVP, not M6's; M6 itself is not founded in MVP.
Module map M5/M6 bullets and MVP table rewritten so neither claims filtering
as its own; US-10 sc. "The same filter applies on a board" makes the single
surface rule explicit and testable.

### R-20
Severity: minor
Affects: Non-functional requirements, BR-6, US-1, Open questions
Problem: (a) the NFR section has no number anyone can test: "board/backlog
reads dominate writes" and "expected to comfortably cover this" are not
criteria in the sense of `01-business-analysis.md` ("the list takes ≤ 2s on
10k rows" is). With thousands of work items per project and an unpaginated
board, a target is exactly what is missing. (b) BR-6's "retention window" has
no value, so BR-6 cannot be tested at all; worse, US-1 attributes the number
to doc-006 while the open question attributes it to doc-002 — the retention
window is one instance-wide decision and belongs here.
Required resolution: give the list/board read a measurable target at the
stated volumes, and either fix the retention window in BR-6 or name a single
owner for it (and make US-1 and the open question agree).
Status: resolved
Resolution: NFR Volumes bullet now states 2s/5,000-item read, 1s write
targets. D-5 answers (b) directly: retention is indefinite, not a numeric
window — BR-6 and the Retention NFR bullet say so; US-1's project-delete
scenario and US-6's work-item-delete scenario both now say "indefinite ...
until an administrator purges it by hand", no window number to disagree on.

### R-21
Severity: minor
Affects: US-3 sc. 1, MVP M3
Problem: US-3 sc. 1's "it carries Description, Assigned To, Priority and Tags
as optional fields, all left as filled or empty by the author" is not a
testable criterion — it states neither which fields are required nor their
defaults, and it contradicts the reference model, where Title is required,
State defaults to New and Priority defaults to 2 of 1–4
(`research/ado-boards.md` § Fields). Description's format (rich text vs plain)
is also undecided, though the research marks it html and the MVP table is
silent; tag count/length and title length have no limits.
Required resolution: give the MVP field set a small table — field, required,
default, format/limits — and rewrite the scenario against it (e.g. "creating
with no Priority yields Priority 2").
Status: resolved
Resolution: US-4 now opens with a required/default field table (Заголовок
required, Область defaults to project root, Приоритет defaults to 2, etc.)
and its first scenario is rewritten against it ("A team member creates a
work item with the defaults applied"). Per-type extra fields and exact
format/limits (title length, tag count, Description rich-text vs plain)
stay for doc-002 — this is module depth, out of the umbrella's scope.

### R-22
Severity: minor
Affects: US-1, US-4, US-5
Problem: edge cases are covered in one story only. US-6 has an empty-result
scenario; nothing covers the empty board / empty backlog / no projects at all
(the first screen every new instance shows), an item deleted while another
user has the board open, reordering a *filtered* backlog, or assigning a work
item to an iteration of another project (US-5 assumes "in the same project"
without a refusal scenario).
Required resolution: add the empty-state scenarios to US-1/US-4, a refusal
scenario for cross-project iteration assignment, and state what a reorder
means while a filter is applied.
Status: resolved
Resolution: US-1 sc. "A user with no organizations sees an empty state";
US-7 sc. "An empty board shows empty columns, not an error"; US-6 sc. "A
deleted item leaves a board another user has open"; US-8 sc. "The order is
global, not per filtered view" (states what reordering under a filter
means); US-9 sc. "Assigning a work item to another project's iteration is
refused".

## Verdict

**needs-human-decision.**

Counts: 22 findings — 3 blockers (all open), 11 majors (all open, 3 with a
partial reviewer fix), 8 minors (2 resolved, 6 open).

Why not `approved-by-ai`: three blockers change the artifact's own
conclusions, not its wording — the decomposition sizes (R-1) would let two
modules be founded by tasks that skip gate-1/gate-2, and the cross-module
model, which is this task's main deliverable, cannot express two rules it
states itself (backlog order R-2, a work item's team/area R-3). On top of
that, five questions are the human's alone and none of them is in the spec's
current "Open questions" list:

1. Roles: does the realm get composite roles (`admin` ⊇ `member` ⊇ `viewer`),
   or does every check enumerate the accepted roles? (R-7 — an auth decision.)
2. Does MVP found M5 and M6 at all, or do Iteration and inline filtering live
   inside M1/M3/M4 until release 2? (R-1 — it changes the queue.)
3. Are Comments and work item delete/restore really in MVP? Both are in the
   MVP tables with no story (R-4, R-5).
4. Is Reason in MVP, and which state transitions are legal? (R-6.)
5. The naming collision on «Задача» — generic entity vs the single MVP type
   (R-13), plus the retention window number (R-20) and which ADRs this
   analysis owes (R-8).

The spec's own five open questions stand and are all still unanswered.

Reviewer edits to the artifacts (small fixes, recorded above): `spec.md` only
— Roles preamble (R-7), ER relations and MVP M4 board scoping (R-16), BR-4
extended to comments (R-5), out-of-scope de-duplicated (R-15). No story file
was changed; no finding, verdict or discussion was placed in `spec.md` or
`stories/`.

## Round 2 — requirements

Delta check over round 1: `status.md` § Decisions (D-1..D-8), the reworked
`spec.md` (367 lines), the ten new stories US-1..US-10 (the old six deleted),
and the 22 round-1 resolutions. Not re-investigated: the research note, the
screenshots index and the routing/ADR references already used in round 1 —
only the parts the rework touches (routing § "Thresholds", ADR-0018's
permission model, ADR-0020/0021 on storage) were re-read.

### Verification of the round-1 findings

- R-1 **verified.** Five `L` founders, exactly one per founded MVP module
  (M1 `mod-organizations-projects-teams`, M2 `mod-process-seed`, M3
  `mod-work-item-core`, M4 `mod-boards-backlogs`, M5 `mod-iterations`); M6 is
  explicitly not founded (D-4); every `M` in (b) has its module's founder in
  its `deps` chain; the graph by slug is acyclic. `mod-work-item-attachments`
  and `mod-work-item-delete-restore` are `L` for their ADR, not as founders,
  and the spec says so.
- R-2 **verified.** `BACKLOG_ENTRY`(stackRank) in the ER model, owned by M4,
  BR-12 names the scope and the read-only use by M3, US-8 exercises it.
- R-3 **verified.** `WORK_ITEM }o--|| AREA`, `TEAM |o--o{ AREA`, BR-3 names
  the inheritance mechanism, US-2 covers ownership and the un-owned node,
  US-8 covers the item that lands on no backlog.
- R-4 **verified.** One mechanism (D-5), BR-6/BR-10, US-6 covers delete,
  restore to the prior or a chosen state, purge and disappearance from the
  board; `mod-work-item-delete-restore` is in (b).
- R-5 **verified.** US-5 covers add, author-only edit, admin moderation,
  reader read-only, history; @mentions out of scope.
- R-6 **verified.** No Reason in MVP (Out of scope, release 3), BR-10 fixes
  the transition set, US-3 exercises a backward transition and the fact that
  «Удалена» is not offered by the state control.
- R-7 **verified** as far as D-1 goes (permission codes, group-tree
  containment, no role hierarchy). The *scoping* half that D-2 added is a new
  defect — R-23 below, not a re-open of R-7.
- R-8 **verified in part.** The ADR list and its owners are in (b). The work
  item identifier was left open by the resolution; fixed here, see R-29.
- R-9 **REOPENED (open).** See below.
- R-10 **verified.** Legend added, edges match the prose, order is
  M0 → M2 → M1 → M3 → (M4, M5) → M6, Process is per organization.
- R-11 **verified** (BR-11, NFR, US-8). The edit-conflict half had no
  scenario — fixed here, see R-28.
- R-12 **verified.** `mod-process-seed` and `mod-work-item-core` split, with
  comments, links, attachments and delete/restore as their own tasks.
- R-13 **verified.** «Рабочий элемент» generic, «Задача» only the Task type;
  no story uses «Задача» generically.
- R-14 **verified.** US-2 covers add/remove membership limited to org
  members; BR-13 fixes the Assigned To candidate set and US-4 exercises it.
- R-15 **verified.** One out-of-scope list; the MVP section only points at
  it. One wording clash inside it fixed here (R-25e).
- R-16 **verified** for the four relations named; a further cardinality
  cluster is R-25 below.
- R-17 **verified.** Iteration delete → unassigned (BR-7, US-9); Tag and
  Board update rows defer wholly; Query row merged and uniform; BR-8
  exercised by US-10.
- R-18 **verified.** Russian labels in the glossary, the MVP M3 row and every
  story; Корзина present.
- R-19 **verified.** M6 not founded, filtering is an M3/M4 surface property,
  US-10 makes the single-surface rule testable.
- R-20 **verified.** 2 s / 5,000 items, 1 s write; retention indefinite in
  BR-6, the NFR, US-1 and US-6 alike.
- R-21 **verified.** US-4 opens with the required/default field table and its
  first scenario is written against it.
- R-22 **verified.** All five edge-case scenarios are present.

### R-9 (re-opened)
Severity: major
Affects: BR-9, ER `ITERATION.pinnedCurrent`, `TEAM }o--o{ ITERATION`,
Operation completeness (Iteration), US-9, MVP M5
Problem: the current-iteration rule now exists but says three different
things about who pins. BR-9: "the **team's** manually pinned iteration is
current instead" — a pin per team. The ER model: `ITERATION.pinnedCurrent`,
a boolean on the iteration — one pin per project, the same for every team.
Operation completeness: "Iteration … Update: admin (dates, **pin as
current**)" and US-9 "an administrator has pinned one iteration as current" —
again project-wide. With one default team per project the three coincide, so
nothing fails in MVP, and doc-004 will pick one at random for release 2, when
teams multiply. Two related gaps: (a) overlapping dates — BR-9 makes *every*
iteration whose dates cover today current, so two overlapping iterations give
two current ones with no tie-break, and round 1 asked for exactly this
scenario; (b) `TEAM }o--o{ ITERATION : "selects"` is in the model but appears
in no MVP row, no operation-completeness row and no story — if that relation
is the per-team pin, say so; if it is the ADO "team selects which iterations
it plans in", it is release-2 scope and should be marked.
Required resolution: make the pin's scope one thing (per project or per
team) across BR-9, the ER model, the operation table and US-9; define what
happens when several iterations' dates cover today, with a scenario; give
`TEAM–ITERATION` an MVP answer or mark it release 2. The human is already
asked to confirm BR-9 (Open question 3) — this belongs with that answer.
Status: resolved
Resolution: D-11 — iterations belong to the project; a team selects which of
the project's iterations it uses (`TEAM }o--o{ ITERATION`, now an MVP row/
operation row/US-9 scenario) and current is per team: the selected iteration
whose dates cover today, earliest finish on overlap, else the team's default
iteration (a team setting, `pinnedCurrent` removed from ITERATION), else
none.

### R-23
Severity: blocker
Affects: Roles, BR-5, ER `ORG_MEMBERSHIP`, US-1, Operation completeness
(Organization), doc-006, `mod-organizations-projects-teams`
Problem: the spec states two incompatible permission models at once. The
Roles section: roles are **realm** roles, "the token carries only a flat list
of codes; the code never sees role or group names", and scope is a *binary*
membership test — "holding a code is necessary but not sufficient — a check
also requires the acting user to be a member of the organization". The ER
model: `USER ||--o{ ORG_MEMBERSHIP : "holds (role admin/member/viewer,
D-1)"` — a role **per organization**, i.e. exactly the scoped assignment
ADR-0018 says the model does not have ("a role assignment applies to the
whole realm; if needed, a group attribute in the token and …"). Under the
first reading a user who is `admin` anywhere is `admin` in every
organization they belong to; under the second the token's codes are not the
authority and ORG_MEMBERSHIP.role is. The two cannot both be implemented,
and the question they disagree about is the ordinary one for a multi-org
instance (D-2): may a user be an administrator of org A and a plain reader
of org B? Worse, the new self-service creation path makes the first reading
a privilege escalation: US-1 says the creator "becomes its first `admin`
member automatically", and `admin` by the ADR-0018 convention test "holds
every permission" realm-wide — so anyone who creates their own organization
gains admin codes that, combined with membership in someone else's
organization, administer that one too.
Required resolution: pick one and write it once — either (a) the realm role
stays global and ORG_MEMBERSHIP carries no role, in which case say
explicitly that a user is the same kind of actor in every organization they
belong to, and say what self-service org creation grants instead of `admin`;
or (b) the effective permissions are the intersection of the token's codes
with the role recorded on ORG_MEMBERSHIP for that organization, in which
case ADR-0018 gets a successor decision (an ADR owed by
`mod-organizations-projects-teams`, already listed) and BR-5 must state the
intersection rule. This is an authorization rule — a decision for the human,
not a wording fix (D-1 is compatible with both readings).
Status: resolved
Resolution: D-9 (option b) — the admin/member/viewer role lives on
`ORG_MEMBERSHIP` itself, scoping that membership's organization only;
Keycloak keeps only authentication plus two instance-level roles (instance
administrator, dictionaries administrator); BR-5 and Roles rewritten around
it; `mod-organizations-projects-teams` owes the ADR-0018 amendment.

### R-24
Severity: major
Affects: MVP M1, Operation completeness, US-1, US-2, Roles
Problem: nothing says how a user becomes a member of an organization. The
Roles table gives the admin "manage org membership", the ER model has
`ORG_MEMBERSHIP`, US-1 and US-2 both *assume* org members exist ("a user who
is not a member of an organization", "a user who is already a member of the
project's organization") — but there is no operation-completeness row for
OrgMembership, no MVP row that scopes it and no scenario that grants it. As
written, an MVP organization has exactly one member for ever: its creator.
Everything else in the MVP — team membership (US-2), the Assigned To
candidate set (BR-13), the whole multi-org premise of D-2 — is empty without
it.
Required resolution: add an OrgMembership row to operation completeness (who
adds/removes a member, from which candidate set — every Keycloak realm user,
or an invitation flow — and with which role, per R-23) and a scenario in
US-1 or US-2 for adding and removing an organization member, including the
refusal for a non-admin. Not fixed here because the "with which role" half
is R-23's open decision.
Status: resolved
Resolution: D-10 — organizations are created by an instance administrator or
a grantee of the instance-level "create organizations" permission; the
creator becomes the org's first admin; an org administrator adds members by
searching existing realm users by name/e-mail. New "Organization membership"
row in operation completeness; US-1 scenarios for adding a member and for a
non-member not seeing the organization.

### R-25
Severity: minor
Affects: Cross-module logical data model, Operation completeness, MVP M3,
Out of scope
Problem: a cluster of model/cross-reference slips of the same kind as R-16.
(a) `USER ||--o{ WORK_ITEM : "is assigned"` makes an assignee mandatory
while Исполнитель is optional (US-4 field table). (b) `ITERATION ||--o{
WORK_ITEM` makes the iteration mandatory while its own label says "optional
pointer". (c) `TEAM ||--o{ AREA` makes every area's owner mandatory while
its own label says "a node may have no explicit owner" (BR-3 and US-2 depend
on it not being). (d) `ORGANIZATION ||--o{ PROCESS` allows many processes
per organization while the prose, the MVP M2 row and D-2 say exactly one.
(e) Operation completeness cited BR-13 (the Assigned To candidate set) as
the rule for adding a team member, and named an "org/project admin" that the
role model does not have. (f) The MVP M3 field list includes Итерация with
no hint that the field is founded by M5's task, the one cross-module
ownership question this table exists to answer. (g) Out of scope said "Full
WIQL parity" is out while release 3 promises "a WIQL-level query language".
Required resolution: fix the cardinalities and the references.
Status: resolved
Resolution: fixed by the reviewer in `spec.md` — `USER |o--o{ WORK_ITEM`,
`ITERATION |o--o{ WORK_ITEM`, `TEAM |o--o{ AREA`, `ORGANIZATION ||--||
PROCESS`; the Team/Area Update cell now cites US-2 and says "org admin"; the
M3 row says the Итерация field is added by M5's founding task (D-4); the
WIQL bullet now reads "the release-3 query language is a deliberate subset".

### R-26
Severity: minor
Affects: BR-6, Operation completeness (Organization, Project), US-1
Problem: BR-6 was worded only for work items ("Deleting a work item moves it
to the «Удалена» state"), but the operation table applies "soft, BR-6" to
Organization and Project, and US-1 cites BR-6 for archiving a project. An
organization has no «Удалена» state, so the rule as worded did not cover the
two rows that lean on it — and "is a deleted project's soft-delete the same
pattern or a different one?" is precisely a cross-module question doc-006
would otherwise answer alone.
Required resolution: state the general soft-delete pattern and its two
mechanisms in BR-6.
Status: resolved
Resolution: BR-6 rewritten by the reviewer — deletion is always soft with
indefinite retention and a manual purge; for a work item the mechanism is
the «Удалена» state, for an organization/project a deactivated status.

### R-27
Severity: minor
Affects: US-2, US-3, US-5, Operation completeness, Roles, BR-5
Problem: three stories acted out a "project administrator" — a role the
model denies: the Roles table has only the organization administrator, MVP
M1 says "no per-project role override" and BR-5 scopes no finer than the
organization. A reader of US-2 alone would implement a project-scoped admin.
Required resolution: use the role names the Roles section defines.
Status: resolved
Resolution: renamed to "organization administrator" in US-2 (title, actor),
US-3 (actor, scenario 1), US-5 (moderation scenario), the spec's story table
row for US-2 and the Team/Area operation row; US-2 also notes that MVP has no
project-scoped admin role.

### R-28
Severity: minor
Affects: BR-11, US-4
Problem: BR-11 has two halves, and only the board/backlog half was
exercised (US-8). The optimistic-concurrency half — two users editing the
same work item — is the one the platform already has a pattern for
(ADR-0010) and the one a developer is most likely to skip; no scenario
demanded it.
Required resolution: add a concurrent-edit scenario to US-4.
Status: resolved
Resolution: US-4 scenario "Two members edit the same work item at once" —
first save wins, the second is rejected as a conflict to reload and reapply,
never silently overwritten.

### R-29
Severity: minor
Affects: Decomposition (b), R-8 residue
Problem: R-8's resolution left the work item identifier scheme undecided and
named no owner — "an implementation detail for doc-002". An unowned
cross-module decision is exactly what this umbrella exists to prevent: the
id appears on cards, in links and in comment references, so three modules
would each assume one.
Required resolution: name the task that decides it.
Status: resolved
Resolution: `mod-work-item-core`'s row in (b) now carries the identifier
scheme (per-project or instance-wide, user-visible key or not), decided
there with doc-002. No story requires an id in MVP, so nothing is blocked.

### R-30
Severity: minor
Affects: US-1, US-9, Operation completeness
Problem: two MVP capabilities the operation table grants had no scenario at
all — restoring a soft-deleted project ("Cancel/reverse: org admin
(restore)") and the admin-only nature of the iteration list (Create/Update/
Delete: admin). Round 1's R-5 was the same pattern: a permission stated only
in a table ships unchecked.
Required resolution: add the two scenarios.
Status: resolved
Resolution: US-1 "An organization administrator restores an archived
project"; US-9 "Only an administrator manages the iteration list" (a member
may still assign an existing iteration).

### R-31
Severity: minor
Affects: Decomposition (b), `feat-work-item-search`
Problem: `feat-work-item-search` (`M`) adds the inline filter to two
modules' surfaces at once — M3's work item list and M4's board. By
routing.md that is still `M` (the change is additive, and the `L` thresholds
are about breaking boundary changes), but it is the shape that most reliably
triggers "escalation by volume": one task, two slices, two API contracts.
Required resolution: either split it per surface (`feat-list-filter` on
`mod-work-item-core`, `feat-board-filter` on `mod-boards-backlogs`) or
record at route confirmation that the M4 half is additive and stays inside
the same task. Left to the orchestrator/human at queueing time — it changes
the queue, not the requirements.
Status: accepted-risk

### Verdict (round 2)

**needs-human-decision.**

Counts, round 2: 21 of 22 round-1 findings verified, 1 re-opened (R-9,
major); 9 new findings — 1 blocker (R-23), 1 major (R-24), 7 minor (R-25..
R-31, of which 6 fixed here by the reviewer and 1 accepted-risk).

Why not `approved-by-ai`: the permission model — the thing D-1 and D-2 were
supposed to settle — states two mutually exclusive rules (R-23), and one of
the two readings makes self-service organization creation a privilege
escalation. That is an authorization decision, which routing.md treats as
`L`-grade risk, and it cannot be resolved by wording. R-24 follows from it:
until the human says what a per-organization role is, there is no way to
describe how anyone joins an organization, and without that the multi-org
MVP is one user per organization. R-9's re-open is smaller but of the same
kind: three places disagree about who pins the current iteration, and the
human is already being asked about BR-9.

Questions for the human (the spec's three, plus one):

1. Self-service organization creation — the spec's Open question 1.
2. The Russian names of the five types and the backlog levels — Open
   question 2.
3. The current-iteration fallback — Open question 3, **and with it R-9**:
   is the pin per project or per team, and what happens when two
   iterations' dates both cover today?
4. **R-23:** is a user the same kind of actor in every organization they
   belong to (global realm role, membership is a binary gate), or does each
   ORG_MEMBERSHIP carry its own role (a scoped assignment that ADR-0018
   explicitly does not have and that needs a successor ADR)? R-24 — how a
   user becomes an organization member — is answerable only after this.

Reviewer edits to the artifacts in this round (all recorded above as
resolved findings): `spec.md` — four ER cardinalities, BR-6, the Team/Area
operation row, the MVP M3 Итерация note, the identifier owner in (b), the
WIQL bullet, the US-2 title; `stories/` — US-1 (+restore scenario), US-2
(actor), US-3 (actor), US-4 (+concurrent-edit scenario), US-5 (actor), US-9
(+admin-only scenario). No finding, verdict or discussion was placed in
`spec.md` or `stories/`; `status.md` and `docs/backlog.md` untouched.

## Round 2 — delta check (D-9..D-12)

Narrow check of the D-9..D-12 rework only (not a new investigation): the
reopened/new findings R-9, R-23, R-24 marked resolved by the business-analyst,
the current `spec.md` (378 lines) and the stories the rework touched (US-1,
US-9 rewritten; US-3, US-7 renamed). `git diff` was not available in this
session (no shell), so the delta was reconstructed from the resolution notes
plus the current text of the affected sections.

Verified:

- **R-23 holds (D-9).** The permission model now says one thing in all five
  places: Roles preamble ("that role lives on `ORG_MEMBERSHIP` itself …
  never on a Keycloak realm role", "No realm role named
  `admin`/`member`/`viewer` exists or is checked"), the roles table (captioned
  "Role (organization membership)"), ER `USER ||--o{ ORG_MEMBERSHIP : "holds
  (role … scopes this membership only, D-9)"`, BR-5, MVP M1 and the M1 module
  bullet. No surviving "realm-wide"/"realm role as the authority" wording in
  `spec.md` or `stories/` — the two remaining "realm role" mentions are the
  explicit negations in the Roles preamble and BR-5, plus the ADR-0018
  amendment line in Decomposition (b), which is the amendment D-9 asks for.
  The escalation path R-23 named is closed: `admin` no longer means anything
  outside its own organization.
- **R-24 holds (D-10).** Organization creation is instance-administrator-gated
  with a grantable instance-level permission (Roles preamble, MVP M1,
  Operation completeness "Organization" row); the "Organization membership
  (`ORG_MEMBERSHIP`)" row gives create/read/update/delete with the candidate
  set (existing realm users by name/e-mail search) and the role choice; US-1
  has the three creation scenarios (instance admin, grantee, refusal), the
  add-a-member scenario and the non-member-cannot-see-it scenario. "Not
  self-service" is stated once and contradicted nowhere.
- **R-9 holds (D-11).** `pinnedCurrent` is gone from the ER model and from
  every other place; ITERATION carries name/startDate/finishDate only,
  `PROJECT ||--o{ ITERATION : "owns"`, `TEAM }o--o{ ITERATION : "selects"` and
  `TEAM }o--o| ITERATION : "defaults to"`. BR-9, MVP M5, the Iteration
  operation row, the M5 module bullet, `doc-004`/`mod-iterations` in the
  decomposition and US-9 all state the same three-step derivation (dates cover
  today → earliest finish on overlap → team default → none). No "project pin"
  or "pinned as current" wording survives.
- **D-12 names.** «Эпик», «Фича», «Пользовательская история», «Задача», «Баг»
  and the levels Эпики/Фичи/Истории/Задачи are used in the glossary, MVP
  M2/M4, the decomposition and US-3/US-7/US-8. No «Функция» or «Ошибка» is
  left in `spec.md` or any story (both survive only in R-13's round-1
  resolution text here, which is history, not requirements).
- **No new contradiction with the MVP table, the ER model, the
  operation-completeness table or the decomposition**, with the three
  exceptions below (R-32..R-34, all minor and fixed here). Spot-checked the
  pairs the delta could break: MVP M1 ↔ Roles ↔ Organization/OrgMembership
  rows; MVP M5 ↔ BR-9 ↔ Iteration row ↔ ER; decomposition (a)/(b) rows that
  name D-9/D-10/D-11 against the sections they cite.
- **US-1 and US-9 are verifiable.** Every scenario names an actor, a trigger
  and an observable outcome; the permission-sensitive ones (create refused
  without the permission, non-member cannot see the org, admin-only iteration
  management) have explicit refusals, and US-9 covers all four branches of
  BR-9 including the empty "no current iteration" state.

### R-32
Severity: minor
Affects: US-1 (empty-state scenario), D-10, Roles
Problem: the round-1 empty-state scenario survived the D-10 rework unchanged
and still promises every organization-less user "a way to create one" — which
is exactly what D-10 removed, and which the same story's own refusal scenario
contradicts two scenarios earlier. As written, the first screen a new user
sees offers an action they are not allowed to take.
Required resolution: condition the create action on the permission and say
what the other users see instead.
Status: resolved
Resolution: fixed here — the scenario now offers the create action only to an
instance administrator or a holder of "create organizations" (D-10), and
otherwise tells the user to ask an administrator to be added.

### R-33
Severity: minor
Affects: US-1, Operation completeness (Project), BR-5
Problem: the scenario "Only an organization member may create a project in
it" tests only a non-member's refusal, while the operation table gives project
creation to the **org admin** alone. With D-9 making the role a per-membership
value, "member" is now a specific role name, so the scenario title asserts a
weaker rule than the table — a reader of US-1 alone implements membership as
sufficient.
Required resolution: align the scenario with the table.
Status: resolved
Resolution: fixed here — renamed to "Only an organization administrator may
create a project in it" and the Given now also covers a member-role user, with
the refusal citing the Project row.

### R-34
Severity: minor
Affects: spec.md § Context and goal
Problem: the opening paragraph still said the review "raised 22 findings and
forced eight decisions (D-1..D-8)" and summarised the permission model as
"permission codes scoped by org/project membership" — the pre-D-9 reading that
R-23 found ambiguous. The index's own summary of itself was one revision
behind the normative sections below it.
Status: resolved
Resolution: fixed here — the paragraph now names D-1..D-12, the 31 findings,
and states the model as the role recorded on the organization membership.

### Verdict (round 2 delta check)

**approved-by-ai.**

R-9, R-23 and R-24 are resolved in the text and match D-9/D-10/D-11 exactly;
D-12's names are used consistently; the delta introduced no contradiction the
reviewer could not fix in place. Three new minors (R-32..R-34) were found and
fixed here, none of them a decision. R-31 remains accepted-risk (a queueing
question for the orchestrator, not a requirement). R-8's residue — the work
item identifier scheme — stays owned by `mod-work-item-core` (R-29) and blocks
nothing in MVP. Nothing is left that only the human can decide; `gate-1` is
the human's to set.

Reviewer edits in this check: `stories/US-1-organization-and-project.md` (the
empty-state scenario R-32, the project-creation scenario R-33) and `spec.md`
(§ Context and goal, R-34). No other file touched; no finding, verdict or
discussion placed in `spec.md` or `stories/`; `status.md` and
`docs/backlog.md` untouched.
