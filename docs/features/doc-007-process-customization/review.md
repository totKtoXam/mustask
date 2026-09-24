# Review: doc-007-process-customization

## Round 1 — requirements

Reviewed: `spec.md` (243 lines) and `stories/US-1..US-5`, against doc-001
(`spec.md` § Module map / ER / MVP tables / Module order / BR-1, BR-2, BR-10 /
Operation completeness / Out of scope / decomposition, `status.md` D-1..D-12,
`stories/US-3-built-in-process.md`), `research/ado-boards.md`,
`screenshots/22..26`, and the `mod-001-process-seed` queue line in
`docs/backlog.md`.

Verdict: **needs-human-decision** — 1 blocker, 7 majors, 12 minors
(10 minors fixed in place by the reviewer, 2 left open).

Line budgets: `spec.md` 243 → 248 after the reviewer's fixes (guide ~250,
inside it); every story file well under 150. No budget finding.

### Coverage: binding rule → where it lands

| doc-001 rule / decision | Covered here by | Note |
|---|---|---|
| BR-1 (one project, one type of that project's organization's process) | BR-M2-1, US-1 sc. 3 | enforcement is M3's; the spec never says so — see R-5 |
| BR-2 (state → exactly one of five categories) | BR-M2-2, § MVP state table, US-2 sc. 3 | **R-1**: the table's mapping contradicts its own cited source |
| BR-6 (soft delete, «Удалена» IS the bin) | BR-M2-3, § MVP, US-3 sc. 3–4 | mechanism is mod-005's — R-7 |
| BR-10 (any↔any, restore defaults to the previous state) | BR-M2-3, BPMN, US-3 | nuance dropped — R-16 (fixed) |
| BR-4 (history) | US-5 sc. 3 only (release 3) | MVP state-change history is M3's; acceptable |
| BR-5 (codes from `ORG_MEMBERSHIP`) | § Roles | non-member case missing — R-13 (fixed) |
| D-2 / D-3 / D-12 (one process per org; five types; four levels; Russian names) | BR-M2-1, § MVP tables, US-1 | complete |
| D-5 («Удалена» is the recycle bin) | § MVP, BR-M2-3, US-3 | complete |
| D-6 (no Reason in MVP, editor and Reason in release 3) | BR-M2-3/7, § Release 3, US-5 | complete |
| doc-001 Operation completeness, Process row | § Operation completeness | contradicts open question 1 — R-10 (fixed) |
| doc-001 Out of scope (editor, custom fields/rules/layout, Reason = release 3) | § Out of scope, § Release 3 | release-3 section stays invariants-only, no screens — clean |

### R-1
Severity: blocker
Affects: spec.md § "MVP: the seeded process" (state table, row 1), BR-M2-2,
US-2, doc-001 BR-2
Problem: the seeded state table maps «Решена» to category **Resolved** for
Эпик, Фича, Пользовательская история *and* Баг, and cites
`screenshots/26-process-userstory-states.png` plus `research/ado-boards.md`
§ "Work item types" as the shape it follows. Both cited sources say the
opposite for three of those four types. Screenshot 26 groups User Story's
states as Proposed: New / **In Progress: Active, Resolved** / Completed:
Closed / Removed: Removed — the Resolved *category* is empty; the research
note records the same thing (`User Story (New, Active, Resolved[InProgress],
Closed, Removed)`) and reserves `Resolved[Resolved]` for Bug only. So the
spec both misreports its reference and lumps Баг together with three types
that behave differently. This is not cosmetic: `mod-001-process-seed` seeds
exactly this table, M4 derives board columns from categories (doc-001 ER
`BOARD_COLUMN }o--|| STATE_DEFINITION`, BR-2), so the choice decides whether
the Истории board shows «Активна» and «Решена» as one In-Progress group
(ADO's behaviour) or as two categories. Changing it after the seed ships
means migrating STATE_DEFINITION rows plus every board's column mapping.
Required resolution: a human decision, then one of —
(a) mirror the reference: «Решена»→InProgress for Эпик/Фича/Пользовательская
история, «Решена»→Resolved for Баг alone (and note that in MVP only Баг ever
uses the Resolved category, which doc-003 must know); or
(b) keep «Решена»→Resolved for all four, and say in the spec that MUSTASK
deliberately diverges from Azure DevOps here and why, dropping screenshot 26
as evidence for the mapping.
Either way the table must be split so Баг is not shown as identical to the
three portfolio/requirement types, and the citation must match what the
sources actually show (screenshot 23 is the *type list*, it shows no states
at all — it cannot support a state claim).
Status: resolved
Resolution: human decision P-1/P-2 (`status.md`) — Эпик/Фича/Пользовательская
история mirror the reference («Решена»→InProgress); Баг gets its own flow
with masculine-agreeing names («Новый»/«В работе»/«Исправлен»/«Закрыт»),
«Исправлен»→Resolved, Resolved used by Баг alone. Table in `spec.md` split
accordingly; screenshot 23 dropped as evidence, citation narrowed to
screenshot 26 and the research note's state-shape lines.

### R-2
Severity: major
Affects: spec.md § "MVP: the seeded process", US-1, consumer
`mod-001-process-seed` / doc-002
Problem: no state is designated the initial state of a new work item, and the
states carry no order. Azure DevOps has both (`research/ado-boards.md`
§ Fields: "State (req, default New)"; screenshot 26 shows the states ordered
inside their categories). Without them the seeded process is not usable:
M3 cannot create a work item (which state?), the state picker has no defined
order, and a category holding two states (Active + Resolved in ADO's grouping
— relevant whichever way R-1 goes) has no defined column order for M4.
`mod-001-process-seed` would have to invent both, which is exactly what this
analysis exists to prevent.
Required resolution: state the default state per type («Новая» for all five,
unless decided otherwise) and the ordering of states within a type, in the
§ MVP state table; add a US-1 criterion asserting both.
Status: resolved
Resolution: `spec.md` § "MVP: the seeded process" now lists each type's
states in order with its first state marked as the default (BR-M2-2
amended); US-1 scenario 1 asserts both the order and the default.

### R-3
Severity: major
Affects: spec.md § "MVP: the seeded process" ("The call is idempotent"),
US-1 scenario "The seed never runs twice for the same organization"
Problem: idempotency is asserted with no key. The spec never says what
identifies a WorkItemType, a StateDefinition or a BacklogLevel inside a
process. The Russian display name cannot be the identity: release 3 (US-4,
§ Release 3 "Custom states. Add, rename or hide per type") explicitly allows
renaming, and BR-M2-2 already insists that consumers read the category,
"never the state name" — yet doc-001's ER has M3's work item and M4's board
column pointing at a STATE_DEFINITION, so *something* stable must be pointed
at. Consequences: (1) US-1's "none of its types, states or backlog levels are
duplicated" cannot be turned into a test — duplicated by what criterion?
(2) a release-3 rename silently invalidates every reference; (3) the
scenario covers a sequential retry only, not two concurrent organization
creations racing on the same organization (doc-001 BR-11 is about work items
and boards, it does not cover this).
Required resolution: state, in business terms, that every type, state and
backlog level carries a stable identity independent of its display name, and
that the seed is keyed on (organization, that identity); extend the US-1
scenario to a concurrent double call, not only a retry.
Status: resolved
Resolution: new BR-M2-9 in `spec.md` states the stable-identity rule and the
(organization, identity) idempotency key, including under concurrent calls;
§ "Logical model (M2)" carries the same statement in prose; US-1's "seed
never runs twice" scenario now covers a concurrent race, not only a retry.

### R-4
Severity: major
Affects: spec.md § "MVP: the seeded process", § Non-functional requirements,
US-1; consumer `mod-002-organizations-projects-teams`
Problem: the seed "is synchronous and blocks organization creation", but the
failure path is never stated. Can an organization exist without a process? If
it can, BR-M2-1 ("exactly one Process per organization") is violated from the
first failed seed and every downstream module meets an organization with no
types; if it cannot, organization creation must fail as a whole. This is a
business rule, not a design detail — M1's story for organization creation
depends on the answer, and the reviewer's checklist item "an integration
failure mid-process" has no answer anywhere in the spec or the stories.
Required resolution: add the rule (recommended: an organization never becomes
visible without its process — a failed seed fails the organization creation)
and a US-1 scenario for it.
Status: resolved
Resolution: orchestrator default, to be confirmed at gate-1 (`status.md` R-4;
wording corrected in round 2, R-23);
new BR-M2-8 in `spec.md` states it, and US-1 gets a "A failed seed fails the
whole organization creation" scenario.

### R-5
Severity: major
Affects: spec.md § "Logical model (M2)" (`ORGANIZATION ||--|| PROCESS`),
§ "MVP: the seeded process"; doc-001 § Module map ("M2 … Needs nothing from
M1 — resolves the former M1↔M2 cycle, R-10") and § Module order (M2 before
M1); queue lines `mod-001-process-seed` (deps: doc-007) and
`mod-002-organizations-projects-teams` (deps: …, mod-process-seed)
Problem: M2's own logical model opens with an ORGANIZATION — an M1-owned
entity — and says the process is owned by it. doc-001 resolved the M1↔M2
cycle in the other direction: M2 needs nothing from M1 and is built first.
The spec never says which side knows the other, and the module order makes
that load-bearing: `mod-001-process-seed` ships before any organization
exists. If the process is defined as *belonging to* an organization, the
founding slice cannot stand alone and doc-001's order and the queue deps are
wrong; if the organization is the side that records which process is its own
(the seed capability hands one back), M2 stays independent and everything
holds.
Required resolution: state the direction in business terms in the § Logical
model and in the M1→M2 capability paragraph. If the answer turns out to be
"the process must know its organization", escalate: it contradicts doc-001
§ Module map/Module order and two queue lines, and that is a human call.
Status: resolved
Resolution: orchestrator resolution R-5 (`status.md`) — M2's own definitions
carry no reference to ORGANIZATION; the seed hands its identity back to the
caller and M1 records the organization→process pointer. § "Logical model
(M2)" and § "MVP: the seeded process" in `spec.md` reworded to say so; the
erDiagram relation's label now names M1 as the side that records it;
doc-001's module order (M2 before M1) stands unchanged.

### R-6
Severity: major
Affects: spec.md § Roles ("Reading means …"), § Operation completeness (Read
rows), US-2; the size of `mod-001-process-seed`
Problem: the MVP read surface is undefined. US-2's scenarios have a user
"open the project's work item type list" and "look for a way to add, rename
or remove" — i.e. a screen. The queue line for `mod-001-process-seed` says
only "no editing UI", and the spec never states whether M2 ships a read-only
view in MVP (and where — project settings? organization settings?) or whether
"read" means only the capability M3/M4 consume plus the type and state
pickers on the work item form. The difference turns the founding slice from
backend-only into full-stack, and the task skips stage 3 (no screens designed
here), so this spec is the last place it can be settled before
`mod-001-process-seed` is planned.
Required resolution: state in § MVP which read surface exists in MVP, and
rewrite US-2's criteria against it (if there is no screen, express them as
the capability plus the pickers).
Status: resolved
Resolution: orchestrator resolution R-6 (`status.md`) — MVP ships a
read-only process-definition screen (project settings); no editing screen.
§ "MVP: the seeded process" in `spec.md` states this and that it makes
`mod-001-process-seed` full-stack; US-2 scenario 1 now names that screen and
asserts the ordered state list, default, category, icon/colour and backlog
level it shows.

### R-7
Severity: major
Affects: stories/US-3 header (`shippable: no — delivered by mod-process-seed`),
also US-2; spec.md § Decomposition
Problem: US-3's four scenarios are work item behaviour — setting a state,
deleting to «Удалена», restoring. By doc-001 § Module order M2 ships before
M1 and M3: when `mod-001-process-seed` is delivered there is no WorkItem, no
state control and no delete/restore, so not one of US-3's criteria can be
verified in that slice. Those behaviours belong to `mod-003-work-item-core`
and `mod-005-work-item-delete-restore`, which are queued and *not* listed as
consumers of this analysis. Left as is, the slice is either scoped into M3's
territory or the criteria are verified by nobody.
Required resolution: attribute US-3 to the slices that actually deliver it
(and say what `mod-001-process-seed` does deliver: the definitions and
BR-M2-3 as a constraint those slices must honour). Check US-2 the same way
once R-6 is answered.
Status: resolved
Resolution: orchestrator resolution R-7 (`status.md`) — US-3's header now
reads "delivered by mod-003-work-item-core (state transitions) and
mod-005-work-item-delete-restore (delete/restore)"; `spec.md`'s
`mod-001-process-seed` decomposition row states it delivers the definitions
and the read screen only, with BR-M2-3/BR-M2-7 as constraints those two
slices enforce. US-2 checked against R-6 and left attributed to
`mod-001-process-seed` — the read screen is that slice's own deliverable.

### R-8
Severity: major
Affects: spec.md § "Decomposition for the backlog", rows 2, 4 and 6
Problem: three of the five release-3 slices are not ≤ M (checklist item 5;
`docs/ai-workflow/routing.md`):
- `feat-NNN-process-custom-types-fields` bundles a custom *type* editor with
  custom *fields* of eight kinds. Custom fields found a new pattern — values
  for a schema unknown at compile time — and change M3's work item storage
  and form rendering. That is a module-founding L, and it is two slices.
- `feat-NNN-process-rules-form-layout` bundles a rule engine
  (conditions × actions) with a form-layout editor. Two slices, each ≥ M.
- `feat-NNN-process-custom-backlog-levels` adds portfolio levels, and by
  doc-001 D-3 every portfolio/requirement level implies a board and an
  ordered backlog per team in M4 — cross-module, at least L.
These rows are what the human approves at gate-1 and what the orchestrator
later turns into queue lines, so the sizes have to be honest now.
Required resolution: split and re-size the three rows; where a slice founds a
pattern, mark it L and name the pattern.
Status: resolved
Resolution: per the task's own direction (every release-3 decomposition row
stays ≤ M, split further rather than sized L), the three rows became six:
`feat-NNN-process-custom-types` / `feat-NNN-process-custom-fields` (the
field-value pattern named explicitly, sized M by splitting definition from
the type editor); `feat-NNN-process-rules` / `feat-NNN-process-form-layout`;
`feat-NNN-process-custom-backlog-levels` (M2 side) /
`feat-NNN-process-custom-backlog-boards` (M4 side, depends on
`mod-006-boards-backlogs`). `spec.md` § "Decomposition for the backlog"
updated with all nine rows and their dependencies.

### R-9
Severity: minor
Affects: spec.md § "MVP: the seeded process" («Удалена» paragraph)
Problem: "even to Bug and Task where ADO's own configuration screen doesn't
list it as an editable workflow state" is false for Task —
`research/ado-boards.md` § "Work item types" records `Task (New, Active,
Closed, Removed)` — and unevidenced for Bug: no screenshot in the repository
shows Bug's or Task's states (23 is the type list, 26 is User Story). A false
premise in a normative document gets copied forward.
Required resolution: drop the claim; keep the real divergence — ADO allows
Removed as an ordinary destination (§ "Transitions"), MUSTASK does not.
Status: resolved
Resolution: paragraph rewritten by the reviewer in `spec.md`.

### R-10
Severity: minor
Affects: spec.md § Operation completeness (Process / Update), § Open questions
Problem: the table asserts "release 3: none — a process is customized in
place, not replaced" while open question 1 asks the human to confirm exactly
that (in-place mutation vs. an ADO-style inherited fork). A table stating a
decision the same document calls undecided is a contradiction a reader will
resolve the wrong way.
Required resolution: mark the cell as the assumption it is and point at the
open question.
Status: resolved
Resolution: cell reworded by the reviewer in `spec.md`.

### R-11
Severity: minor
Affects: spec.md § "Release 3" (Custom fields, Rules)
Problem: both bullets are presented as Azure DevOps's own shape while
diverging from the cited note. `research/ado-boards.md` § Fields lists
string, html, integer, double, dateTime, treePath, identity, plainText,
history, picklist — no boolean; § Rules lists six condition types and eight
actions, the spec names two and five without saying it is a subset.
Required resolution: label both as a proposed subset and mark boolean as
going beyond the note (open question 2 already asks for the final list).
Status: resolved
Resolution: both bullets reworded by the reviewer in `spec.md`.

### R-12
Severity: minor
Affects: spec.md BR-M2-7, Source column
Problem: the cell quotes `research/ado-boards.md` § "Transitions" as saying
"any state → any state is allowed" *even with Reason lists configured*. The
note says "In inherited processes any state → any state is allowed" and,
as a separate sentence, "Each transition has a Reason list". The conclusion
is sound; the quotation is not in the source.
Required resolution: cite the two facts separately instead of quoting a
sentence the note does not contain.
Status: resolved
Resolution: Source cell rewritten by the reviewer in `spec.md`.

### R-13
Severity: minor
Affects: spec.md § Roles, stories/US-2
Problem: the Roles table covers admin/member/viewer only. A user with no
`ORG_MEMBERSHIP` in the organization is never mentioned, although doc-001
BR-5/D-9 makes the answer clear (no membership → no codes → no access) and
MUSTASK is multi-organization by D-2, so cross-organization reads are the
normal failure to guard. US-2 has no negative scenario at all.
Required resolution: state it in § Roles and add the negative scenario.
Status: resolved
Resolution: sentence added to § Roles in `spec.md`; scenario "A user with no
membership in the organization reads nothing" added to US-2.

### R-14
Severity: minor
Affects: stories/US-1 (sc. 1), US-2 (sc. 2), US-3 (sc. 3)
Problem: three criteria are universal negatives over the whole product — "no
process-editing screen is offered anywhere in the product", "no such action
exists anywhere in the product", "this is the only way that state is ever
reached". None can be turned into a test; a tester can only confirm they did
not find one.
Required resolution: restate as concrete, checkable assertions (no create /
rename / delete operation on a type, state or backlog level is exposed by any
MVP surface; a direct state change to «Удалена» is rejected).
Status: resolved
Resolution: all three rewritten by the reviewer in the story files.

### R-15
Severity: minor
Affects: stories/US-1, scenario 1
Problem: "each type already has its full set of states and its backlog level
assigned" — a test cannot distinguish "full" from "some". The concrete lists
exist one file away, in § MVP.
Required resolution: assert the spec's table by reference and name what is
checked.
Status: resolved
Resolution: criterion rewritten by the reviewer in US-1. (The *content* of
that table is still subject to R-1.)

### R-16
Severity: minor
Affects: spec.md BR-M2-3, BPMN, stories/US-3 sc. 4
Problem: doc-001 BR-10 says restore "defaults to (but does not require) the
state held immediately before deletion". The spec's BR-M2-3 keeps only
"left only via Restore", and US-3 only the default. Whether the restorer may
pick a different state is precisely what `mod-005-work-item-delete-restore`
must implement, and the binding rule already answers it.
Required resolution: carry doc-001 BR-10's nuance into BR-M2-3 and US-3.
Status: resolved
Resolution: BR-M2-3 and US-3 sc. 4 reworded by the reviewer.

### R-17
Severity: minor
Affects: stories/US-4, scenario 2
Problem: "removes or narrows that field" — "narrows" is undefined, so the
criterion is not testable.
Required resolution: define it inline.
Status: resolved
Resolution: US-4 sc. 2 now says "narrows (changes its kind or its allowed
values so that a stored value no longer fits)"; the same wording added to the
§ Release 3 Custom fields invariant.

### R-18
Severity: minor
Affects: spec.md § Decomposition (rows 1, 5, 6), stories/US-1..US-3 headers
Problem: the slice is called `mod-process-seed`, but it is already queued in
`main` as `mod-001-process-seed` (`docs/backlog.md`: "· size:L · deps:doc-007
· note:doc-001 decomposition (b)"). doc-001 wrote the unnumbered slug because
no number existed yet; it does now, and the un-numbered form breaks the link
between this analysis and the slice that consumes it.
Required resolution: use the issued id.
Status: resolved
Resolution: `mod-001-process-seed` everywhere in `spec.md` and in the story
headers. (Whether US-3 belongs to that slice at all is R-7.)

### R-19
Severity: minor
Affects: spec.md § Non-functional requirements, first bullet
Problem: "it must stay well inside `mod-organizations-projects-teams`'s own
organization-creation budget — this task sets no separate number" defers to a
budget that does not exist: doc-001 § NFR sets numbers for list/board reads
(2 s / 5,000 items) and for "a state/board/backlog write" (1 s), none for
organization creation. As written the requirement cannot be verified by
anyone. Left open deliberately: picking the number is a product call, not a
reviewer's.
Required resolution: either bind the seed to a number here (doc-001's 1 s
write budget is the obvious ceiling) or delete the bullet and say the seed
carries no performance requirement.
Status: resolved
Resolution: orchestrator default, to be confirmed at gate-1 (`status.md`
R-19; wording corrected in round 2, R-23) —
the dangling reference removed; the bullet now says the seed sets no
performance budget of its own.

### R-20
Severity: minor
Affects: spec.md § "MVP: the seeded process", § Out of scope
Problem: a work item type in the reference product carries display
attributes — an icon and a colour (visible per type in
`screenshots/23-process-agile-types.png` and `24-process-agile-backlog-levels.png`,
which also notes "The iteration backlog does not have an associated color").
M4's cards and backlog rows will want them, and the owning module is M2 if
they exist at all. The spec neither seeds them nor puts them out of scope, so
doc-003 has to guess.
Required resolution: either add the per-type display attributes to the seeded
definition, or list them in § Out of scope with the release that brings them.
Status: resolved
Resolution: human decision P-5 (`status.md`) — fixed icon and colour per
type, not editable in MVP. `spec.md`'s logical model gains `icon`/`colour`
attributes on `WORK_ITEM_TYPE`, § "MVP: the seeded process" states they are
seeded and fixed, § "Out of scope" notes they are not editable in MVP and
are not part of the release-3 customization surface analysed here either.

## Human-only questions for gate-1

1. **(R-1)** «Решена» — category Resolved, or InProgress as in Azure DevOps
   (leaving Resolved used by Баг alone)? This is the data
   `mod-001-process-seed` seeds and what M4's board columns group by.
2. **(R-6)** Does MVP ship a read-only screen showing the process, or is
   "read" only the capability M3/M4 consume plus the work item form's type
   and state pickers? It decides whether the founding slice is full-stack.
3. **(R-4)** Confirm: an organization is never created without its process —
   a failed seed fails the whole organization creation?
4. **(R-5)** Only if the answer to "does the process belong to the
   organization, or does the organization record its process?" turns out to
   be the former: doc-001's module order (M2 before M1) and two queue lines
   have to change.
5. The three questions the spec itself raises, unchanged and still open:
   in-place mutation vs. an inherited fork in release 3; the release-3 field
   kind list; whether "exactly one seed template, forever" is permanent.

## Round 2 — requirements (delta)

Reviewed: the rework of `spec.md` and `stories/US-1..US-5` after round 1,
against `status.md` § Decisions (P-1..P-5, orchestrator resolutions R-5/R-6/
R-7, defaults R-4/R-19/OQ-2). Not re-investigated: doc-001 and the research
note, except where a round-1 resolution cites them.

Verdict: **approved-by-ai** — 3 majors and 7 minors found, all fixed in place
by the reviewer; one line-budget note (R-31) returned to the orchestrator as a
proposal. Nothing needs a human decision beyond what gate-1 already carries:
the three orchestrator defaults (R-4, R-19, OQ-2) are still unconfirmed and
are now listed as such in `spec.md` § Open questions, and R-27 raises one
release-3 row to `L`, which the human should see in the gate-1 summary.

### Round-1 findings: do the resolutions hold?

| Finding | Holds? | Note |
|---|---|---|
| R-1 | yes, after R-21 | P-1/P-2 categories exact; the table had dropped «Удалена», which both decisions list |
| R-2 | yes | order and default stated; BR-M2-2 amended; US-1 sc. 1 asserts both |
| R-3 | yes, after R-22 | the key as written could not work and contradicted R-5 |
| R-4 | yes, after R-23/R-26 | resolution claimed human confirmation the status file does not record; partial-seed case added |
| R-5 | yes, after R-22 | "PROCESS carries no reference back" was incompatible with a seed keyed on the organization |
| R-6 | yes, after R-25 | "project settings" was an addition to the orchestrator's resolution |
| R-7 | yes, after R-28 | US-3 header correct; the seed row cited BR-M2-7, a release-3 rule, as an MVP constraint |
| R-8 | no → R-27 | "every release-3 row stays ≤ M" has no source in `status.md`; the pattern-founding row is `L` |
| R-9..R-18 | yes | reviewer's own round-1 fixes, unchanged by the rework |
| R-19 | yes, after R-23 | same attribution slip as R-4 |
| R-20 | partly → R-24 | goes beyond P-5 by also excluding release-3 editing |

P-3 (in place, no fork) and P-4 (one template forever) are reflected exactly:
§ Release 3 lead-in, Operation completeness, BR-M2-1, Out of scope. No
statement goes beyond them.

### R-21
Severity: major
Affects: spec.md § "MVP: the seeded process" (state table), US-1 sc. 1,
US-2 sc. 1; P-1, P-2
Problem: P-1 and P-2 give each flow five states, the last being «Удалена»
→ Removed. The reworked table listed four (three for Задача) and said
«Удалена» is "not … in the order above". US-1 asserts "exactly the states
listed", so the seed would create no Removed state at all — yet doc-001's ER
has M3's work item point at a STATE_DEFINITION, and a deleted item has to
point at one. The screen's contents were ambiguous for the same reason.
Required resolution: put «Удалена»→Removed in every row, last, and say that
it is seeded like any other state but kept off the ordinary state control.
Status: resolved
Resolution: fixed in place — all three rows end with «Удалена»→Removed; the
lead-in and the following paragraph say it is seeded on every type and never
offered by the state control. US-1/US-2 need no change: they reference the
table.

### R-22
Severity: major
Affects: spec.md § "Logical model (M2)" (erDiagram label, Direction and
Identity paragraphs), BR-M2-9, US-1 sc. 3; R-3, R-5
Problem: two defects in the idempotency rule. (1) It keyed the seed on
"(organization, identity)" while the Direction paragraph and the ER label
said M2's entities carry no reference to an organization — the seed cannot
de-duplicate by a value it never stores. (2) The identity is "fixed when the
seed creates it", so a second call creates new identities and a key on them
de-duplicates nothing. The orchestrator's R-5 resolution already has the
answer: the seed takes the organization identifier from its caller.
Required resolution: key the seed on the caller-supplied organization
identifier, held by M2 as an opaque key (no dependency on M1); at most one
Process per key; uniqueness of types, states and levels inside a process.
Status: resolved
Resolution: fixed in place — BR-M2-9 reworded accordingly; the Direction
paragraph states the opaque key and points to BR-M2-9; the Identity
paragraph, a restatement of BR-M2-9, was removed; the ER label is
corrected. US-1 sc. 3's Given, which said "already seeded" while the When
described a race with the first call, now reads "has been, or is being,
called for".

### R-23
Severity: minor
Affects: spec.md BR-M2-8 (Source), § Open questions; review.md round 1
R-4/R-19 resolutions
Problem: BR-M2-8 and the round-1 resolutions said the R-4 and R-19 defaults
were "confirmed by the human", and § Open questions said "none outstanding".
`status.md` lists R-4, R-19 and OQ-2 as "defaults proposed by the
orchestrator, to confirm at gate-1". The human would be reading a spec that
reports as decided three things they have not yet decided.
Required resolution: attribute them as defaults pending gate-1 and list
them.
Status: resolved
Resolution: fixed in place — BR-M2-8's source reads "to be confirmed at
gate-1"; § Open questions lists the three defaults; R-4/R-19 round-1
resolution wording corrected.

### R-24
Severity: minor
Affects: spec.md § Logical model, § Release 3 (Custom work item types),
§ Out of scope; P-5
Problem: P-5 fixes icon and colour in MVP only. The spec also excluded them
from release-3 editing, which is more than the human decided. It also
created a hole: icon/colour were "set by the seed", and a custom release-3
type is not seeded, so it had no source for either.
Required resolution: narrow the exclusion to MVP and leave release 3 open
explicitly.
Status: resolved
Resolution: fixed in place — Out of scope now covers MVP only; the logical
model says the seed sets them for the five built-in types; the Custom work
item types bullet records the release-3 gap (custom types' icon/colour,
editability of the built-in ones) as a question for release 3's own
analysis.

### R-25
Severity: minor
Affects: spec.md § Roles, § "MVP: the seeded process", US-2 sc. 1; R-6
Problem: the orchestrator's R-6 resolution grants a read-only view and
cites doc-001 US-3 ("the project's work item type list"); it does not place
it in project settings. That is a navigation decision for the founding
slice, and an odd one for an organization-level object.
Required resolution: tie it to a project, as the source does, and leave
navigation to design.
Status: resolved
Resolution: fixed in place — "reached from a project … the exact navigation
is `mod-001-process-seed`'s design"; US-2 sc. 1 says "from one of its
projects". § Roles no longer repeats the screen's contents and points to
§ MVP instead.

### R-26
Severity: minor
Affects: BR-M2-8, US-1 sc. 2
Problem: "a failed seed fails organization creation" did not say what
happens to a partly created process. A leftover half-process under an
idempotent key would make the retry "succeed" with missing types.
Required resolution: say the seed is all-or-nothing.
Status: resolved
Resolution: fixed in place — BR-M2-8 opens "The seed is all-or-nothing";
US-1 sc. 2 asserts that no partial set of types, states or levels remains.
This follows from the R-4 default and is not a new decision.

### R-27
Severity: major
Affects: spec.md § Decomposition (custom fields row and its dependants); R-8
Problem: R-8's resolution kept every release-3 row at `M` "per the task's own
direction". No such direction appears in `status.md` or the queue line.
`routing.md` makes the first task that creates a slice or pattern `L`, and
accepting an ADR is an `L` threshold. The custom-fields row "founds the
schema-flexible field-value pattern" in M3's storage and form. That is the
same kind of work as `mod-004-work-item-attachments` and
`mod-005-work-item-delete-restore`, both `mod`/`L` in the queue, and the
choice of storage is an ADR-level decision. The pattern cannot be split away
from its first use. Understating size is forbidden, and a doubt means the
larger size.
Required resolution: size it `L` under the `mod` prefix and update the rows
that depend on it.
Status: resolved
Resolution: fixed in place — `mod-NNN-process-custom-fields`, `L`, with the
reason in the row; `feat-NNN-process-rules` and `feat-NNN-process-form-layout`
now depend on it. The other seven rows are honestly ≤ M (inside existing
slices, additive). To show at gate-1: if the ≤ M direction did come from the
human, this finding is theirs to overrule.

### R-28
Severity: minor
Affects: spec.md § Decomposition
Problem: (1) the seed row named BR-M2-7, a release-3 rule, as a constraint
mod-003/mod-005 enforce in MVP. (2) Deps used the un-numbered
`feat-process-custom-types` / `feat-process-custom-backlog-levels` next to
`feat-NNN-…` ids. (3) Custom types, and Reason (US-5 sc. 3 writes into M3's
history), act on work items but depended on `mod-001-process-seed` only.
Required resolution: drop BR-M2-7 from the seed row, use one id form, add
`mod-003-work-item-core` where work items are touched.
Status: resolved
Resolution: fixed in place.

### R-29
Severity: minor
Affects: US-2 sc. 3
Problem: "an attempt through the read capability is refused" cannot be
tested: a read capability has no write attempt to refuse.
Required resolution: assert that any request that would change the
definition is refused.
Status: resolved
Resolution: fixed in place.

### R-30
Severity: minor
Affects: spec.md § "MVP: the seeded process" (Баг lead-in); P-2
Problem: the lead-in said Баг's names agree in gender with «Баг», but P-2
keeps the feminine «Удалена» for Баг too. A later reader "fixing" it to
«Удалён» would break the single recycle-bin state (D-5).
Required resolution: name the exception.
Status: resolved
Resolution: fixed in place — "except «Удалена», which stays one uniform
recycle-bin state on every type".

### R-31
Severity: minor
Affects: spec.md (290 lines after this round's fixes, guide ~250)
Problem: the stage-1 rule treats > ~250 as either story content that leaked
into the index or a feature too big for one task. Checked: no Gherkin, no
per-story steps and no story-level detail sits in `spec.md`. The overage
comes from content the plan puts in the index on purpose: § Release 3 (~50
lines of analysis-only invariants), a 9-row decomposition, and the BR and
Operation-completeness tables. The rework's duplication (the Identity
paragraph restating BR-M2-9, § Roles restating the screen) was removed in
this round. Round 2's own additions (the «Удалена» column, the three pending
defaults, the release-3 icon gap) are requirements, not padding.
Required resolution: proposal to the orchestrator: accept the size as
legitimate index content. The only real split would be to move § Release 3
into a separate analysis document at release-3 planning. That is not worth a
new task now, because nothing in MVP consumes it.
Status: accepted-risk
Resolution: left as is; reported to the orchestrator.

### Outside the delta (noted, not raised)

Operation completeness, WorkItemType/Update, says "rename, recategorize
within BR-M2-2/5" for release 3. Types have no category, only their states
do, and US-4 sc. 4 says built-in states keep their categories. This is
pre-existing text and not part of the rework. Release-3 analysis should
settle it: which built-in type or state attributes an admin may change.
