# Review: doc-003-backlogs-boards

## Round 1 — requirements

Reviewed: `spec.md` and `stories/US-1..US-3` against `doc-001-domain-map`
(spec, `status.md` § Decisions D-1..D-12, US-7/US-8,
`research/ado-boards.md`) and `doc-003-backlogs-boards/status.md`
(§ Plan, § Decisions).

**Verdict: needs-human-decision.** One blocker (R-1) cannot be closed from
`doc-001`/D-1..D-12 — it is a modelling decision about the column↔state
mapping that also touches M2 (`doc-007`). Two more findings need a human
answer (R-6, R-8) plus the spec's own open question about the mapping write.
Ten findings were fixed in place; six remain open for the business-analyst
(R-1, R-2, R-5, R-6, R-9, R-10, R-11 — of which R-1/R-6 wait on the human).

### Rule → story mapping

| Rule | Covered by | Note |
|---|---|---|
| BR-1 three boards, Задачи backlog only | US-1 sc.1; US-2 sc.3 (added) | the Задачи half was uncovered — R-16 |
| BR-2 columns per used category, fixed order | US-1 sc.2 | testable, but the target-state rule is missing — R-1 |
| BR-3 no stored card position, drag writes state | US-1 sc.3 | which state? — R-1; order inside a column — R-10 |
| BR-4 one default swimlane | US-1 sc.5 | ok |
| BR-5 one entry per (team, level); no owning team → none | US-2 sc.9 | ok |
| BR-6 new/re-owned entry lands at the bottom | US-2 sc.5, sc.8 | rank direction clarified — R-13 |
| BR-7 order is global, not per filtered view | US-2 sc.6 | ok |
| BR-8 drag is the only reorder primitive | US-2 sc.2 (added) | was uncovered — R-7 |
| BR-9 hierarchy read from M3, expand ≠ reorder | US-3 sc.1 | ok |
| BR-10 mapping creates/replaces the one link | US-3 sc.2, sc.3 (added) | replace case was uncovered — R-17 |
| BR-11 orphan/foreign parent shown top level | US-3 sc.5, sc.6 | sc.6 not exercisable in MVP — R-2 |
| BR-12 stale-move conflict notice | US-1 sc.8; US-2 sc.7 | BPMN contradicted it — R-3 |
| `doc-001` BR-6 soft delete («Удалена») | — | no rule, no scenario — R-11 |
| `doc-001` BR-7 iteration pointer | Out of scope (added) | R-12 |
| `doc-001` BR-11 optimistic concurrency on a work item | — | collides with BR-12 — R-6 |

### R-1
Severity: blocker
Affects: BR-2, BR-3, US-1 (scenarios 2, 3, 4), `doc-001` ER model
Problem: the spec makes a board column a *state category* and a card's
column a computed property of the item's state, but a drag has to write a
concrete state — and the spec never says which. The reference process makes
this ambiguous in two directions at once
(`doc-001/research/ado-boards.md` § Work item types, line 18):
(a) one type can hold **two** states in one category — Agile
Пользовательская история has `Active` and `Resolved`, both InProgress — so
"drag to the InProgress column" has two possible targets;
(b) a column can exist for a category one of the level's types has **no**
state in — on the Истории board the Resolved column exists because «Баг»
has a Resolved-category state, while «Пользовательская история» has none,
so a story card cannot legally occupy the column it can be dropped into.
Nothing in the spec says whether such a drop is rejected, disabled or
silently mapped elsewhere. On top of that, `doc-001`'s ER model states
`BOARD_COLUMN }o--|| STATE_DEFINITION : "maps to"` — a column maps to a
*state*, not to a category — which the spec's BR-2 silently replaces without
reconciling the two. Left as is, the first implementer invents the mapping,
and each level's real column set (three columns on Эпики/Фичи, four on
Истории, if the Agile scheme is seeded) is unknown at acceptance time.
Required resolution: a human decision on the model, then in the spec: a rule
giving, per level and per type, the state a column maps to (the reference's
per-type mapping, seeded by M2 — then say so and record the dependency on
`doc-007`; or a constrained seed of at most one non-Removed state per
category per type — then say that M2 owes that constraint); a rule for a
card whose type has no state in a column (drop target disabled vs. refused
move); a note reconciling BR-2 with `doc-001`'s `BOARD_COLUMN → STATE_DEFINITION`
edge; and US-1 scenario 3 restated so the expected resulting state is
computable from the rule. Scenario 2 should name the concrete expected
column set per level once the seed is fixed.
Status: resolved
Resolution: DD-1 — BR-2 rewritten to a per-type mapping (a column maps, for
each type, to that type's first state in the column's category, M2's seed
order, `doc-007` owed); BR-3 updated for the write; new BR-18 for the
disabled/refused drop when a type has no state mapped in the target column.
The new Data model section reconciles `doc-001`'s
`BOARD_COLUMN → STATE_DEFINITION` edge as a derived per-(column, type)
relationship. US-1's board-scope and per-column scenarios restated so the
resulting state and column set follow from the rule, plus a new refused-drop
scenario for BR-18.

### R-2
Severity: major
Affects: US-2 (scenario "re-owned to another team"), US-3 (scenario
"parent belongs to a different team's backlog"), BR-5, BR-11
Problem: MVP has **exactly one team per project** — `doc-001` MVP table, M1
row ("exactly one default team per project") and Operation completeness
(Team create: "one default per project, MVP"; delete: release 2). Both
cross-team scenarios therefore describe a configuration the MVP cannot be
put into: there is no Team B to re-own an Area to, and no foreign team to
own a parent. As acceptance criteria for `mod-boards-backlogs` they are
untestable, and QA will either fake a second team (outside the MVP
behaviour) or silently skip them.
Required resolution: state the MVP constraint explicitly in the spec (one
team per project ⇒ one set of boards/backlogs per project in MVP, the
per-team model being what makes release 2 additive), and mark the two
scenarios as model invariants verified at the domain/unit level rather than
end-to-end acceptance — or replace them with the variants MVP *can* reach
(an Area re-pointed to an unowned node, a parent that simply does not exist
one level up).
Status: resolved
Resolution: BR-13 added, stating the one-team-per-project MVP constraint and
that the general cross-team rules (BR-5/BR-11) are exercised in MVP through
their single-team-reachable variants. US-2's "re-owned to another team"
scenario replaced with "A work item's Area is re-pointed to an unowned
node"; US-3's "parent belongs to a different team's backlog" scenario
replaced with "A child's linked parent is not one level above it".

### R-3
Severity: major
Affects: BPMN, BR-12, US-1 sc.8, US-2 sc.7, `doc-001` BR-11
Problem: the BPMN diagram branched "same entry moved elsewhere first? →
yes, stale → **move rejected**", while BR-12 and `doc-001` BR-11 say
"**last write wins**, every other pending-move client sees a one-time notice
and refreshes". Reject-the-second and last-write-wins are opposite
behaviours; the two stories' concurrency scenarios follow the rule, the
process diagram followed the opposite one. A developer reading the diagram
implements the wrong semantics.
Required resolution: the diagram must follow BR-12.
Status: resolved
Resolution: BPMN rewritten — the conflict check moved after the write, the
move is applied, and the one-time notice goes to the clients holding the
now-stale view.

### R-4
Severity: major
Affects: Operation completeness (Board row), US-1 sc.6
Problem: the Board row said a board is seeded "when the team gains that
level's first work item", while US-1's empty-board scenario opens a board
for a level with no work items at all — under lazy seeding there is no board
to open. `doc-001` ("MVP seeds one board per team per level (D-3)") implies
the board is not conditional on content.
Required resolution: one unambiguous seeding trigger, compatible with the
empty-board scenario.
Status: resolved
Resolution: seeding moved to team creation (M1, `doc-006`), with an explicit
"a board exists and opens empty before the team has a single work item".

### R-5
Severity: major
Affects: the whole spec (missing section), `doc-003` status.md § Plan item 2
Problem: the task's plan makes this deep dive responsible for the
`BOARD` / `BOARD_COLUMN` / `SWIMLANE` / `BACKLOG_ENTRY` entities, and the
spec has no data-model section at all — no attributes, no keys, no statement
of what M4 stores versus computes. Concretely unanswered: what a
`BACKLOG_ENTRY` holds besides `stackRank` (does it carry team + level, or
are they derived?), whether `BOARD_COLUMN` stores an order index and a
category or a state reference (see R-1), what a `SWIMLANE` holds when there
is exactly one unnamed default, and whether a `BOARD` stores anything at all
beyond team + level. The founding `L` task has to invent the schema, which
is exactly what an `L` slice founder is supposed to receive from analysis.
Required resolution: add a data-model section (entity, key attributes,
owning module, stored vs. computed, the uniqueness constraints implied by
BR-5) refining `doc-001`'s ER fragment for M4.
Status: resolved
Resolution: a Data model section added to `spec.md` — `BOARD`,
`BOARD_COLUMN`, `SWIMLANE`, `BACKLOG_ENTRY`, each with key attributes,
stored-vs-computed, the BR-5-implied uniqueness and the owning module.

### R-6
Severity: major
Affects: BR-3, BR-12, `doc-001` BR-11, ADR-0010
Problem: BR-3 says a card drag performs "the same write editing the item's
form performs", but the two writes are declared under *opposite* concurrency
policies: `doc-001` BR-11 puts work item editing under optimistic
concurrency (`xmin`, the second write rejected) and board/backlog moves
under last-write-wins with a notice. The spec never says what happens when
the two race on the same `WORK_ITEM.state` — a drag landing while another
member has the form open, or a form save landing while a drag is in flight.
Read literally, a drag bypasses the version check and silently overwrites a
form edit, including one made under an explicit conflict dialog.
Required resolution: a human answer, then a rule: the concurrency policy
attaches to the operation (drag = last write wins, form save = `xmin`) and
what that means for the mixed race — whether a drag is exempt from the
version check entirely, and whether the form-editing client is notified.
Status: resolved
Resolution: DD-2 — new BR-19 attaches the concurrency policy to the
operation (drag/reorder = last-write-wins, no `xmin`; form save = `xmin`
unconditionally) and states the mixed-race outcome: a drag always lands
without checking or notifying an open form, and a form save landing after a
drag gets the normal `xmin` conflict. US-1 gained a drag-vs-form-save
scenario.

### R-7
Severity: major
Affects: BR-8, US-2, Operation completeness (BacklogEntry), decomposition
row, spec's open question 2
Problem: BR-8 named "insert at top", "insert at bottom" and "drag between
two neighbours" as the MVP reorder primitives, citing the reference — but in
the reference "add at top/bottom" is a *creation* affordance (a new work
item created at a position), not a reorder command, so the rule was built on
a misread; and no acceptance criterion covered any of the three primitives
except "drag between neighbours", leaving the story's central rule untested.
The spec's open question 2 asked the human to decide this.
Required resolution: settle the primitive set from `doc-001` rather than
from the reference, and give it a criterion.
Status: resolved
Resolution: answerable without the human — `doc-001`'s MVP table, M4 row,
says "one ordered, **drag-and-drop** backlog per team per level". BR-8
rewritten: drag and drop is the only reorder primitive, an entry reaches
either end by being dragged there, no numeric rank field and no discrete
insert command (the reference's "add at top/bottom" is release-2 creation
ground, and in MVP a created item lands at the bottom, BR-6). US-2 gained a
"drag to either end" scenario; the Operation completeness cell and the
decomposition row were reworded; open question 2 removed.

### R-8
Severity: major
Affects: decomposition row `mod-boards-backlogs`, open question 1
Problem: two defects in one row. (a) The row claims "parent/child hierarchy
display and parent assignment from the backlog view (BR-9..BR-11)" while its
deps were `mod-work-item-core, doc-003-backlogs-boards` — but BR-9 says the
hierarchy *reads M3's Parent/Child link*, which is delivered by the separate
`feat-work-item-links` (`doc-001` decomposition (b), queued as
`feat-001-work-item-links`). As written the task would start before the
entity it reads exists. (b) Size: the row founds the slice (schema,
contract, migrations) **and** ships two surfaces (board + backlog, front and
back), four levels of backlog, the hierarchy tree, the mapping write and the
conflict-notice mechanics. `L` is the ceiling size, so the row is formally
"≤ L", but `routing.md` ("Clarification for the greenfield stage": `L` is
the *first* task of a slice, subsequent tasks inside it are `M`) gives a
cheaper shape, and `doc-003/status.md` § Plan explicitly permits a split if
the analysis shows M4 needs one.
Required resolution: (a) fix the deps — done; (b) a human decision together
with open question 1: keep one `L`, or split into `mod-boards-backlogs` (L,
founds M4: boards, columns, the default swimlane, the ordered backlog, the
conflict notice) plus a follow-up `M` inside the slice for the hierarchy
display and the mapping write, dependent on `feat-work-item-links`. The
second shape also makes open question 1 cheap to answer either way.
Status: resolved
Resolution: DD-3 — the decomposition row split: `mod-boards-backlogs` (L)
founds M4 without the hierarchy (deps `mod-work-item-core`,
`doc-003-backlogs-boards`); a new `feat-NNN-backlog-hierarchy` (M, deps
`mod-boards-backlogs`, `feat-work-item-links`) delivers BR-9..BR-11 and
US-3. US-3's header and the story table note which task delivers it; the
spec's open question is removed (answered by DD-3).

### R-9
Severity: major
Affects: US-1 (card), US-2 (backlog row), the value statement of both
Problem: nothing in the spec says what a card or a backlog row *shows*. US-1
promises the member can "track my team's flow visually instead of opening
every work item", which is unverifiable while the card's content is
undefined; the same holds for the backlog row (title only? type? assignee?
state? tags? priority? the item's identifier?). This is not a styling
question (styling is correctly out of scope) but the minimum information
content, and with gate-2 inapplicable for this task there is no later place
where it gets decided before code.
Required resolution: a rule naming the MVP card fields and the MVP backlog
row columns, drawn from `doc-001`'s M3 MVP field list (identifier,
Заголовок, Исполнитель, Приоритет, Теги, and for the backlog also Статус),
with per-type/per-card customization staying release 2.
Status: resolved
Resolution: BR-14 added, naming the card fields (identifier, Заголовок,
Исполнитель, Приоритет, Теги) and the backlog row columns (the same plus
Статус), with per-type/per-card customization confirmed release 2. US-1
gained "A card shows its minimum required fields"; US-2 gained "A backlog
row shows its minimum required columns".

### R-10
Severity: major
Affects: BR-3, BR-7, US-1
Problem: the order of cards *inside* a board column is unspecified, and so
is what a vertical drag within a column does. The natural answer (cards are
ordered by the same stack rank as the level's backlog, and a vertical drag
rewrites it) collides with BR-3's "a card has no stored board position";
leaving it open means one implementer renders an arbitrary order and another
makes vertical dragging a silent backlog reorder.
Required resolution: a rule for the in-column order (stack rank of the
level's `BACKLOG_ENTRY`, or an explicit "unspecified, any stable order" if
that is the intent) and an explicit statement of whether a vertical drag
inside a column is a backlog reorder or a no-op in MVP, with a criterion in
US-1.
Status: resolved
Resolution: BR-15 added — in-column order is the level's backlog stack
rank, a vertical drag within a column reorders the backlog exactly as a
backlog-view drag would, and a drag that changes column only changes state
(BR-3), never repositioning within the new column in the same gesture. US-1
gained "Cards within a column are ordered by the backlog's stack rank" and
"Dragging a card vertically within its column reorders the backlog".

### R-11
Severity: major
Affects: BR-5, Operation completeness (BacklogEntry), US-1, US-2,
`doc-001` BR-6/D-5
Problem: a soft-deleted work item («Удалена», the recycle bin per D-5) is
mentioned only in a parenthesis of the BacklogEntry "Delete" cell. There is
no business rule that a deleted item disappears from the board and the
backlog, no rule at all for **restore** — does the restored item return at
its former position, or at the bottom as a re-entering item (BR-6)? — and no
scenario for either. `mod-work-item-delete-restore` is a queued MVP slice,
so this is live MVP behaviour, not release-2 ground.
Required resolution: a rule (a work item whose state is in the Removed
category is on no board and no backlog), a rule for the restore position,
and criteria in US-1/US-2 covering delete → disappears and restore →
reappears at the defined position.
Status: resolved
Resolution: BR-16 added (a Removed-category item is on no board/backlog,
its `BACKLOG_ENTRY` retained but hidden) and BR-17 (restore reinstates the
entry at its retained stack-rank position, not appended at the bottom).
Operation completeness's BacklogEntry row updated to cite both. US-1 gained
"Deleting a work item removes its card from the board" and "Restoring a
deleted work item returns its card to the board"; US-2 gained "Deleting a
work item removes it from the backlog" and "Restoring a work item returns
it to its former backlog position".

### R-12
Severity: minor
Affects: Out of scope, `doc-001` BR-7
Problem: `doc-001` BR-7 says "boards, backlogs and planning all read that
one [iteration] pointer", and the spec never said whether the MVP board and
backlog are iteration-scoped. Without that, "the team's backlog" is
ambiguous between a product backlog and a sprint backlog.
Status: resolved
Resolution: derivable from `doc-001` (the MVP M5 row has no sprint backlog
and no taskboard) — an Out of scope entry added: MVP views list every item
of the team and level whatever its iteration; iteration-scoped views are
release 2 / M5 (`doc-004`).

### R-13
Severity: minor
Affects: BR-6
Problem: "appended at the bottom (lowest rank)" fixes a rank direction the
spec has no business fixing, and does it in a way the reader cannot check
(in the reference, the bottom of the list carries the *highest* stack rank).
Status: resolved
Resolution: reworded to "appended at the bottom, i.e. as the last row", with
the numeric direction marked a design matter.

### R-14
Severity: minor
Affects: US-1, US-2, US-3 (the viewer scenarios)
Problem: "attempting to drag is refused" is not testable — it does not say
whether the UI hides the affordance, whether the API rejects a hand-made
request, or both; a build that only greys out the button passes.
Status: resolved
Resolution: all three viewer scenarios now assert both layers — no drag
handle/drop target/mapping control in the UI, and a direct API request
rejected on the permission check with nothing changed.

### R-15
Severity: minor
Affects: US-2 (edge case)
Problem: US-1 covered the empty board, US-2 had no empty-backlog scenario.
Status: resolved
Resolution: "An empty backlog shows an empty list, not an error" added.

### R-16
Severity: minor
Affects: BR-1, US-2
Problem: BR-1's second half (Задачи has an ordered backlog and no board) had
a criterion for the missing board only (US-1 sc.1) and none for the backlog
that does exist, although it is one of the four MVP backlogs.
Status: resolved
Resolution: a Задачи-backlog scenario added to US-2.

### R-17
Severity: minor
Affects: BR-10, US-3
Problem: BR-10 says the mapping action "creates **or replaces**" the link;
US-3 covered create (no parent yet) and clear, never replace — the case
where a duplicate or a second parent is most likely to appear.
Status: resolved
Resolution: "Assigning a different parent replaces the existing link" added.

### R-18
Severity: minor
Affects: Non-functional requirements, spec's open question 3
Problem: open question 3 asked the human whether the conflict notice needs
near-real-time push to idle viewers. It is answerable without the human:
`doc-001` BR-11 promises the notice to a *pending-move* client, not to every
viewer, and `doc-001`'s non-functional requirements ask for no real-time
channel anywhere.
Status: resolved
Resolution: the Refresh model paragraph now derives the answer from BR-11
instead of deferring it, live push to idle viewers moved to Out of scope
(release 2), open question 3 removed.

### R-19
Severity: minor
Affects: Out of scope
Problem: the reference's per-team "which backlog levels are visible" setting
(`research/ado-boards.md` § Backlog levels) was neither in MVP nor in Out of
scope — an omission that invites it back in during design.
Status: resolved
Resolution: added to Out of scope as release-2 ground; in MVP every level is
always available.

### Questions for the human (gate-1)

1. **R-1 (blocker).** How does a board column map to a state? Per-type
   mapping seeded by M2 (`doc-007`, the reference's model and `doc-001`'s ER
   edge), or a seed constrained to at most one non-Removed state per
   category per type? And what happens to a card whose type has no state in
   a column that exists for another type at the level (a story card and the
   Resolved column on Истории) — disabled drop target, or refused move?
2. **R-6 (major).** A card drag and a form save racing on the same state
   field: is the drag really exempt from the optimistic-concurrency check
   (`doc-001` BR-11 for work item edits), i.e. may it silently overwrite a
   form edit?
3. **R-8 + the spec's open question 1 (major).** Is the parent-assignment
   write from the backlog in MVP, and does `mod-boards-backlogs` stay one
   `L`, or split into an `L` slice founder (boards + ordered backlog +
   conflict notice) plus an `M` follow-up for the hierarchy and the mapping
   write, dependent on `feat-work-item-links`?

## Round 2 — requirements

Delta check: the round-1 open findings (R-1, R-2, R-5, R-6, R-8, R-9, R-10,
R-11), the rules added since (BR-13..BR-19), the Data model section and the
decomposition split — against each other, `doc-001`, D-1..D-12 and the
human's DD-1..DD-3 (`status.md` § Decisions). The reviewing session had no
shell, so `git diff dc70674 HEAD` was not run: the delta was taken by
comparing the current files with the round-1 state recorded above.
`scripts/verify.sh` was not run by the reviewer either.

**Verdict: approved-by-ai.** Every round-1 resolution holds, as corrected
below. The rework introduced four major defects: a regression against
DD-1, a Data model sentence contradicting BR-5/BR-6, an ambiguous hierarchy
display model, and new rules missing from the decomposition. The reviewer
fixed all four in place, together with three minors. None needs a human
decision. R-22 does pick a display model — derived from BR-7/BR-9, not
decided by the reviewer — and the gate-1 summary should mention it.

### Verification of round-1 resolutions

| Finding | Holds? | Note |
|---|---|---|
| R-1 | yes, after R-20 | BR-2/BR-18 match DD-1 verbatim; BR-3's read side had regressed — R-20 |
| R-2 | yes, after R-22 | BR-13 correct against `doc-001` M1 row; US-3's replacement scenario was not well-defined until R-22 |
| R-5 | yes, after R-21 | logical level kept (no types, tables or endpoints); one sentence contradicted BR-5/BR-6 — R-21 |
| R-6 | yes | BR-19 and the US-1 mixed-race scenario match DD-2 and leave `doc-001` BR-11 unchanged |
| R-8 | yes, after R-23 | split matches DD-3 and `routing.md` (the `L` founds the slice, the follow-up is `M`); the founder row omitted the new rules — R-23 |
| R-9 | yes | BR-14 draws only on `doc-001`'s M3 MVP fields |
| R-10 | yes | BR-15 consistent with BR-3/BR-7; its cross-column clause lacked a criterion — R-25 |
| R-11 | yes | BR-16/BR-17 consistent with `doc-001` BR-6/BR-10 and D-5; restore scenarios tightened — R-24 |

### R-20
Severity: major
Affects: BR-3, DD-1, US-1
Problem: the rework made a card's column "the column whose per-type mapping
equals the work item's current state". Under DD-1 a column maps, per type,
only to that type's *first* state in the category, so a work item in any
other state of that category (the reference's Пользовательская история in
`Resolved`, its second InProgress-category state) matches no column and
would vanish from the board. The mapping is meant to fix the *write*; the
read must still go by category (BR-2, `doc-001` BR-2).
Required resolution: the card's column is its current state's category;
the per-type mapping decides only what a drag writes.
Status: resolved
Resolution: BR-3 reworded accordingly. US-1 gained "A card in a type's
non-first state of a category still has a column", which also asserts that
dragging into that column writes the first state.

### R-21
Severity: major
Affects: Data model (BACKLOG_ENTRY), BR-5, BR-6
Problem: the Data model said the stored (team, level) and rank are retained
"to survive an Area re-scope", with team/level "re-set" on change. This
contradicts BR-6 (a re-scoped item is appended at the *bottom* of its new
team's backlog, so its old rank must not carry over) and BR-5 (an item whose
Area has no owning team has *no* entry, so nothing is retained).
Required resolution: the rank is retained for delete/restore only (BR-16/BR-17).
Status: resolved
Resolution: sentence rewritten: retention serves delete/restore; a move to
another team re-scopes the entry and appends it at the bottom; an Area
becoming unowned removes it, and re-ownership appends at the bottom again.
Also, "row" wording in the BOARD_COLUMN/SWIMLANE cells replaced, so the
section reads as a logical model rather than a table design.

### R-22
Severity: major
Affects: BR-9, BR-11, US-3 (clear-parent and "parent not one level above"
scenarios), `feat-NNN-backlog-hierarchy`
Problem: the spec mixed two display models. BR-9 and US-3's first scenario
say that on a level's backlog, expanding a row shows its children from the
level below. BR-11 and the reworked US-3 scenarios ("shown at the top level,
ungrouped *from then on*", "on the child's own level … nothing to nest it
under") assume a different model: on a lower level's backlog, entries are
grouped under their parents from the level above. No rule defines that model.
Its parent rows would also conflict with BR-7's single per-level stack-rank
order. As written, two of US-3's criteria test nothing: on its own level, an
entry is always top-level under BR-9.
Required resolution: one display model, with criteria that can fail.
Status: resolved
Resolution: expand-only model, derived from BR-7 and BR-9. BR-11 rewritten:
own-level entries are top-level rows in stack-rank order. Children show only
beneath an expanded row, and only those exactly one level below on the same
team's backlog. An item whose parent is missing, more than one level up or
on another team is nested beneath nothing. Parent grouping rows on a lower
level ("Parents: Show" in the reference) added to Out of scope as release 2.
US-3's clear-parent scenario now asserts the child's absence beneath the
expanded former parent. The Story→Epic scenario now checks the Эпики
backlog's expanded Epic.

### R-23
Severity: major
Affects: Decomposition (`mod-boards-backlogs`), BR-13..BR-17
Problem: the founder row listed BR-1..BR-8, BR-12, BR-18, BR-19, and
`feat-NNN-backlog-hierarchy` listed BR-9..BR-11. BR-13..BR-17 (single-team
scope, card/row fields, in-column order, delete hides, restore returns) were
in neither row. BR-16/BR-17 also cannot be exercised before
`mod-work-item-delete-restore` exists: the «Удалена» state is reached only
through its Delete action (`doc-001` BR-10). That dependency was missing.
Required resolution: assign the rules and add the dependency.
Status: resolved
Resolution: BR-13..BR-17 added to the `mod-boards-backlogs` row, and
`mod-work-item-delete-restore` added to its deps. When entering the queue
lines, the orchestrator may instead move BR-16/BR-17 into the
delete/restore slice, if running the two slices in parallel matters more.
The requirements hold either way.

### R-24
Severity: minor
Affects: US-1, US-2 (restore scenarios), BR-17
Problem: "the same position it held before deletion" is only checkable if
nothing was reordered meanwhile. BR-17 restores the retained *rank*, not a
row index.
Status: resolved
Resolution: both restore scenarios gained "And no other entry of that
backlog was reordered while it was deleted".

### R-25
Severity: minor
Affects: BR-15, US-1
Problem: BR-15's "a drag into another column never sets a position within
it" had no criterion.
Status: resolved
Resolution: US-1's "Moving a card changes the work item's state" now
asserts that the card sits at its unchanged stack-rank position wherever it
was dropped.

### R-26
Severity: minor
Affects: Roles, US-2
Problem: leftovers of round-1 wording: the Roles table's "drag/insert an
entry" (BR-8 has no insert command), and US-2's "last (lowest-ranked)"
(BR-6 no longer fixes a rank direction, R-13).
Status: resolved
Resolution: both reworded.
