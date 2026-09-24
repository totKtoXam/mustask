# Requirements review — doc-004-iterations-planning

## Round 1 — requirements

Verdict: **needs-human-decision** — 3 blockers, 7 majors, 6 minors (all six
minors fixed in place by the reviewer). The blockers and the majors marked
"human" below are questions the spec cannot answer out of doc-001: they either
widen an MVP operation doc-001 did not grant, or pick a scope for an already
queued task.

Input used as the yardstick: doc-001 `status.md` § Decisions D-1..D-12,
doc-001 `spec.md` (§ Module map M5, § Cross-module logical data model, § MVP
boundary and release plan, BR-3/4/6/7/9/11/12, § Operation completeness,
§ Out of scope, § Decomposition), doc-001 `stories/US-2-teams-and-areas.md`,
`US-9-plan-by-iteration.md`, `US-10-filter-work-items.md`,
`research/ado-boards.md`, `docs/backlog.md` (the queued `mod-007-iterations`
and `feat-003-work-item-search` lines).

### Rule → story coverage

| Rule | Covered by | Note |
|---|---|---|
| BR-1 (project-owned, unique name) | US-1 | collides with BR-8 → R-3 |
| BR-2 (paired optional dates) | US-1, US-4 | ok |
| BR-3 (flat list) | US-1 | ok; open question 3 |
| BR-4 (team's selected subset) | US-2, US-3 | undefined for an item with no owning team → R-6 |
| BR-5 (default within the selection) | US-2 | ok |
| BR-6 (single pointer) | US-3 | write constraint contradicts the BPMN → R-5 |
| BR-7 (current iteration) | US-4 | clock undefined → R-8; tie-break undefined → R-7; no team context on the M3 list → R-1 |
| BR-8 (soft removal + pointer clearing) | US-1 | reactivation asserted but questioned → R-2; destructive bulk write unbounded → R-9 |
| BR-9 (dates constrain nothing) | US-3 | ok; open question 6 |
| BR-10 (permissions) | US-1, US-2, US-3 | ok |
| BR-11/12/13 (release 2/3) | US-7, US-8, US-10 | out of MVP, correctly flagged |

No doc-001 rule about M5 is left uncovered, and nothing in the release-2/3
stories claims MVP delivery: every one of US-5..US-10 carries
`shippable: no` plus the release and the founding task, the MVP table and
§ Out of scope repeat the cut, and the Задачи-level board stays out per
D-3. Point (3) of the brief checks out.

### R-1
Severity: blocker
Affects: BR-7, US-4 (scenarios 1 and "The iteration filter respects team
ownership"), spec § Decomposition (`mod-007-iterations`)
Problem: «Текущая итерация» is defined per team (BR-7, doc-001 BR-9), but the
spec puts the filter on the **M3 work item list**, which is project-scoped,
not team-scoped (doc-001 US-10: the list filters by state/assignee/tag/area
with no team in the picture; doc-001 BR-3 scopes *boards and backlogs* to the
owning team, not the list). The spec never says which team resolves "current"
on that surface — the project's team, the acting user's team, a team picker —
and US-4's last scenario silently applies board-style area scoping to the list
as well. A developer of `mod-007-iterations` cannot implement the filter
without inventing the answer, and two surfaces will invent it differently.
Required resolution: state in BR-7 (or a new rule) what supplies the team
context on each surface — board/backlog take the team they belong to; the M3
list either takes an explicit team selector, the user's single team, or does
not offer «Текущая итерация» at all — and rewrite the US-4 list scenarios
against that answer. Interacts with R-10.
Status: resolved — E-2: BR-8/BR-9 put «Текущая итерация» on team surfaces only; the project list filters by a specific iteration (US-13, new owner of the list scenarios).

### R-2
Severity: blocker
Affects: BR-8, § Operation completeness (Iteration, Cancel/reverse), US-1
("Removal is soft"), spec § Open questions item 4
Problem: the spec makes reactivation of a removed iteration a normative MVP
operation (Operation completeness gives admin "reactivate"; US-1 has a
Gherkin scenario for it) while its own open question 4 still asks whether MVP
shows a deactivated iteration at all. doc-001's Operation completeness row for
Iteration says Delete = "work items pointing at it become unassigned" and
Cancel/reverse = **n/a** — so reactivation is an operation doc-001 did not
grant, i.e. an MVP scope addition, not a derivation from BR-6. On top of that
the spec never says what reactivation restores besides the iteration itself:
BR-8 removes it "from every team's selection", and nothing says whether the
selections and the team defaults come back or stay lost.
Required resolution: decide (human) whether MVP has an admin-visible
reactivation. If yes — BR-8 and Operation completeness must also state what
happens to team selections and team defaults on reactivation, and the
deviation from doc-001's `n/a` must be recorded as a decision; if no — drop
the reactivate column and the US-1 scenario and keep the soft delete as a
storage property with a manual purge (doc-001 BR-6).
Status: resolved — E-3: reactivation dropped from BR-10, Operation completeness (Cancel/reverse = n/a) and the stories; US-14 asserts no reactivation is offered.

### R-3
Severity: blocker
Affects: BR-1, BR-8, US-1 (duplicate-name and reactivation scenarios)
Problem: BR-1 makes the iteration name unique inside the project (trimmed,
case-insensitive) and BR-8 makes removal a deactivation that keeps the row.
The spec never says whether uniqueness is checked against active iterations
only. Both readings break: if deactivated names stay reserved, an
administrator can never reuse «Спринт 5» and gets «Итерация с таким именем
уже есть» pointing at something invisible; if they are free, reactivating the
old «Спринт 5» (R-2) produces two active iterations with the same name, which
BR-1 forbids. This lands directly on the unique index of
`mod-007-iterations`.
Required resolution: state the scope of uniqueness in BR-1 (proposal: among
*active* iterations of the project) and state in BR-8 what happens when a
reactivation would collide (proposal: refuse it and require a rename first),
with matching scenarios in US-1.
Status: resolved — E-10 + E-3: BR-2 numbers per project, never reissued; BR-3 names unique among active siblings, removed names free (no reactivation to collide); US-1 scenarios; name editability left as the one open question.

### R-4
Severity: major (human — it changes a queued line)
Affects: spec § Decomposition (`mod-007-iterations`), doc-001 § Module order,
`docs/backlog.md` lines `mod-007-iterations` / `feat-003-work-item-search`
Problem: the gap the BA reports is real — `feat-003-work-item-search` is
"state/assignee/tag/area only" and no queued task covers an iteration filter
— but the assignment as written does not hold quietly. It (a) adds
`mod-006-boards-backlogs` to the deps of the queued `mod-007-iterations`
line, which today reads `deps:mod-003,doc-004`; (b) thereby breaks doc-001
§ Module order, which has M4 and M5 built **in parallel**, both only reading
M3/M1 — with the new dep M5 can no longer start until M4 is done; (c) puts a
second filter implementation on the same two surfaces
`feat-003-work-item-search` already owns, so the filter chrome gets built
twice or `feat-003` has to wait for `mod-007`; (d) grows a task that is
already `L` purely as a module founder by a cross-module UI slice, with no
re-check that it is still one task.
Required resolution: the human picks one — (1) keep it in `mod-007` and have
the orchestrator amend the queue line (scope + `deps:mod-003,mod-006,doc-004`)
and record the departure from doc-001's parallel module order; (2) add the
iteration dimension to `feat-003-work-item-search` and give that line a dep on
`mod-007`; (3) a new `feat-NNN-iteration-filter` (M) with
`deps:mod-007,mod-006,feat-003`, leaving `mod-007` as the pure module founder.
Whatever is chosen, the spec's decomposition row must match the queue line
verbatim in deps, because "already queued — this spec refines what it founds"
is otherwise false.
Status: resolved — E-4: new `feat-NNN-iteration-filter` (M; mod-007, mod-006, feat-003) owns US-13; mod-007's row now matches its queue deps `mod-003,doc-004` verbatim.

### R-5
Severity: major
Affects: BR-6, § Business process (gate H), US-3 ("The picker offers only the
owning team's iterations", "An iteration outside the team's selection is kept
but not offered")
Problem: BR-6 states exactly one write constraint — the iteration must belong
to the work item's project — and describes the team selection as a *picker*
property ("the picker does not offer it for new writes"). The BPMN gate H
refuses the write itself ("Same project and offered to the item's owning
team? → no → Refused"). So for an API call that sets a project iteration the
owning team has not selected, the spec says both "allowed, merely not offered"
and "refused". The two readings differ in practice: under the strict one, an
administrator cannot repoint an item at an iteration the team lost, and any
future bulk/import path is blocked; under the loose one, the picker is
advisory and the invariant is not enforceable server-side.
Required resolution: make BR-6 state the write rule explicitly — either the
team selection is enforced on write (and then say what an item whose team
changed may still be repointed to), or only the project is enforced and the
selection is a picker filter — and align the BPMN gate and the US-3
scenarios with it.
Status: resolved — BR-7 enforces the owning team's selection on every write (form, pane, direct request, admin too); clearing always allowed; a kept stale pointer is never rewritten; BPMN gate H and US-3 aligned.

### R-6
Severity: major
Affects: BR-4, BR-6, US-3, US-4
Problem: every picker and filter rule is phrased as "the work item's owning
team", but doc-001 US-2 explicitly allows a work item whose area has no owner
up any ancestor: "the answer is undefined and the work item is not shown on
any team's board until an owner is set". The spec says nothing about such an
item: is its iteration picker empty, does it offer the whole project list, is
the field disabled? The same question applies to the project-wide list filter
(R-1). This is the normal state right after an area tree is created, so it is
not an exotic edge.
Required resolution: add a rule (and a US-3 scenario) covering a work item
with no owning team — proposal: the picker is empty with an explicit state
naming the missing area owner, an existing pointer is still displayed, and the
item is invisible to every team's iteration filter, consistent with doc-001
BR-3.
Status: resolved — BR-7 + US-3: an item with no owning team cannot be scheduled («У области элемента нет команды-владельца»), its pointer is shown and clearable; it is on no team surface and is found by the project-list filter (US-13).

### R-7
Severity: major
Affects: BR-7, US-4 ("Two overlapping iterations finishing on the same day")
Problem: the criterion demands that the tie-break be "deterministic, never a
random pick" but never says what it is, so it cannot be turned into a test —
any implementation passes it by being stable, including one that differs
between two surfaces of the same team. doc-001 BR-9 stops at "earliest finish
date", which genuinely ties when two selected iterations finish on the same
day.
Required resolution: name the second (and if needed third) sort key in BR-7 —
proposal: earliest finish, then earliest start (an undated start last), then
the iteration name compared case-insensitively — and restate the US-4
scenario as a concrete expected winner.
Status: resolved — BR-8 keys: earliest finish, then latest start (a sprint beats its containing release), then lowest number (unique, E-10); US-4 has concrete winners for both ties.

### R-8
Severity: major (human — spec § Open questions item 1)
Affects: BR-7, all of US-4, NFR § Derivation cost
Problem: "dates cover today" is the whole of the current-iteration rule and
"today" has no defined clock. Until it is fixed, none of US-4's criteria is
verifiable: a test written in UTC and a test written in the viewer's zone
disagree on the boundary days, which US-4 deliberately exercises ("The first
and the last day count as covered"). The BA proposes the server's UTC date;
that is a product-visible choice (a team in UTC+6 sees the sprint flip at
06:00 local), not a design detail `mod-007-iterations` can settle alone.
Required resolution: the human answers open question 1; BR-7 then states the
clock in one sentence, and US-4 gets one boundary scenario phrased in that
clock.
Status: resolved — E-1: BR-8 states the server's UTC date; US-4 "The boundary days are covered, in UTC" (incl. a UTC+6 viewer).

### R-9
Severity: major
Affects: BR-8, § Non-functional requirements, US-1 ("Removing an iteration
unschedules the work items in it")
Problem: removing an iteration is the only operation in this spec that writes
an unbounded number of work items (project volumes are thousands, doc-001
NFR), it is irreversible by BR-8's own words, and it is one administrator
click away. The spec requires no confirmation and no impact preview ("N
элементов потеряют итерацию"), sets no time budget for the clearing while the
NFR section does budget reads, and says nothing about atomicity — whether a
failure halfway leaves half the items cleared and the iteration still active
— or about what happens to an item someone is editing concurrently
(doc-001 BR-11).
Required resolution: add to BR-8 (and a US-1 scenario) an explicit
confirmation showing the number of affected work items, state that the
clearing plus the deactivation is one transaction (or define the retry/repair
path if it is not), and add a volume/time line to the NFR section for the
clearing; state which BR-11 outcome a concurrent editor sees.
Status: resolved — BR-10 + US-14: a confirmation with the count, all-or-nothing, NFR § Bulk writes (5,000 items ≤ 5 s), a concurrent editor gets doc-001 BR-11's conflict.

### R-10
Severity: major (human)
Affects: § Roles, BR-4, BR-5, US-2 (all multi-team scenarios), US-4 (last
scenario), § Decomposition (`mod-007-iterations` size)
Problem: the spec's whole team layer is written for many teams per project
("a project with five iterations and two teams", "team A and team B",
"tens of teams per project"), but doc-001's MVP boundary gives each project
**exactly one default team** — "exactly one default team per project", Team
create "one default per project, MVP", Team delete "no, release 2 (a project
keeps its default team in MVP)", and doc-001 US-2 speaks only of "the
project's default team". Either doc-004 is quietly widening the MVP (more
teams per project than doc-001 granted, which also changes the size of
`mod-002`/`mod-007`), or half of US-2's acceptance criteria describes a
configuration MVP cannot produce and therefore cannot test.
Required resolution: the human rules on whether MVP has more than one team per
project. If not, US-2 keeps the per-team model (D-11 requires it) but its
criteria must be reachable with one team, and the multi-team ones must be
marked as post-MVP; if yes, doc-001's MVP boundary needs the amendment
recorded before `mod-007` is sized.
Status: resolved — E-5: several teams per project in MVP; the superseded doc-001 statements and the mod-002/mod-006 queue amendments are in spec § Amendments; mod-007 re-checked as one L.

### R-11
Severity: minor
Affects: US-4 ("The first and the last day count as covered")
Problem: the last line, "And the tie is broken by the next scenario", is not
an outcome and cannot be asserted.
Status: resolved
Resolution: fixed in place — the line now reads "And the one that finishes
today is current, its finish date being the earlier of the two (BR-7)", which
is what BR-7 actually produces for that data.

### R-12
Severity: minor
Affects: § Decomposition (`feat-NNN-iteration-planning-pane`,
`mod-NNN-sprint-views`)
Problem: the planning pane operates on the backlog surface but depended only
on `mod-007-iterations`; the sprint views need parent/child links (US-5 "each
of them can be expanded to its child Задача items", US-6) but did not depend
on `feat-001-work-item-links`.
Status: resolved
Resolution: fixed in place — deps now
`mod-007-iterations, mod-006-boards-backlogs` and
`mod-007-iterations, mod-006-boards-backlogs, feat-001-work-item-links,
feat-NNN-estimation-fields`.

### R-13
Severity: minor
Affects: § Decomposition, US-5 ("Scheduling from the product backlog",
"Removing work from a sprint")
Problem: the same capability — moving an item between the product backlog and
an iteration — was owned by two tasks at once (`mod-NNN-sprint-views` via
US-5, `feat-NNN-iteration-planning-pane` via its own row).
Status: resolved
Resolution: fixed in place — the pane row now names the two US-5 scenarios it
owns, and the sprint-views row reads "US-5 apart from the planning-pane
scenarios".

### R-14
Severity: minor
Affects: US-3
Problem: no criterion covered the empty case — a project with no iterations,
or a team with nothing selected — although it is the state of every project on
day one.
Status: resolved
Resolution: fixed in place — US-3 gained the scenario "The owning team has no
iterations to offer" (explicit empty state, not an error and not the project's
whole list).

### R-15
Severity: minor
Affects: § Non-functional requirements (Volumes)
Problem: doc-001's "5,000 items within 2 s" budget was applied to the
iteration *picker*, which lists tens of iterations, conflating two different
things.
Status: resolved
Resolution: fixed in place — the picker now has its own sentence (tens of
rows, no paging) and the 5,000-item budget is attached to a list/board/backlog
filtered by an iteration, «Текущая итерация» included.

### R-16
Severity: minor
Affects: US-1 ("An iteration is renamed and redated")
Problem: "shown everywhere the iteration appears" is not assertable.
Status: resolved
Resolution: fixed in place — the surfaces are now named (project list, every
team's selection, every picker and filter, every work item pointing at it).

### Checked and found sound

- **BR-8 as a reconciliation of doc-001 BR-6 with the Iteration row of
  doc-001's operation completeness** (brief point 1): the soft part follows
  BR-6/D-5 and the pointer clearing is doc-001's own instruction ("work items
  pointing at it become unassigned"), so the combination is a derivation, not
  a reopening — with the two exceptions raised as R-2 (reactivation, which
  doc-001 marks `n/a`) and R-9 (the unbounded destructive write).
- **The release-2/3 stories stay out of MVP** (brief point 3): see the
  coverage table above.
- **Literal `NNN` and slugs** (brief point 5): every new row uses
  `feat-NNN-…` / `mod-NNN-…` and is referenced by slug in the stories and in
  the release-2/3 entity table; `mod-007-iterations` is refined in a separate
  "already queued" table rather than re-proposed. Only its deps drifted from
  the queue line — R-4.
- **Sizes**: the new rows are L only for module founders (`mod-NNN-sprint-views`,
  `mod-NNN-iteration-metrics`, `mod-NNN-delivery-plans`), M otherwise, which
  matches doc-001's pattern. The one size worth re-checking is `mod-007`
  itself, under R-4.
- **Russian UI copy** is present for every user-visible string
  («Итерация с таким именем уже есть», «Дата окончания раньше даты начала»,
  «Нет текущей итерации», «Текущая итерация», «Ёмкость», «Дни отсутствия»,
  «Доска задач», «Диаграмма сгорания», «Скорость», «План поставки»,
  «По элементам»/«По людям», «Прогноз»), per AGENTS.md.
- **Concurrency** is addressed where doc-001 BR-11 applies to a single work
  item (US-3, last scenario); the gap is only the removal path (R-9).

## Questions only the human can answer (for gate-1)

Six are the spec's own open questions; four come out of this review.

1. **Current-iteration clock** (R-8, spec Q1): server UTC date, or the
   viewer's local date? MVP models no time zone.
2. **Which team resolves «Текущая итерация» on the project-wide work item
   list** (R-1): the project's team, an explicit team selector, or the filter
   is board/backlog-only in MVP?
3. **Is reactivating a removed iteration an MVP operation** (R-2, spec Q4),
   given doc-001 records Cancel/reverse = `n/a` for Iteration? If yes, does it
   restore team selections and defaults?
4. **Where does the iteration filter live** (R-4): inside `mod-007-iterations`
   (with an amended queue line and a departure from doc-001's parallel M4/M5
   order), inside `feat-003-work-item-search`, or in a new
   `feat-NNN-iteration-filter`?
5. **Does MVP have more than one team per project** (R-10), contrary to
   doc-001's "exactly one default team per project"?
6. **The backlog planning pane in MVP** (spec Q2): the work item form only, or
   drag-onto-iteration too?
7. **Flat iterations forever, or room for a Release > Sprint hierarchy**
   (spec Q3) — only decides whether `mod-007` keeps room for a parent.
8. **Estimation fields** (spec Q5): is `feat-NNN-estimation-fields` accepted
   here as the release-2 prerequisite under M3, or does doc-002 own that
   question?
9. **End of an iteration** (spec Q6): unfinished items simply stay pointed at
   it, or does MVP offer a manual "move the rest to the next iteration"?
10. **Name uniqueness with soft removal** (R-3): reserve deactivated names, or
    free them and refuse a colliding reactivation?

## Round 2 — requirements

Verdict: **needs-human-decision** — 0 blockers, 1 major (human), 8 minors
(all fixed in place by the reviewer). The round limit for this stage is
reached. Two small questions go to gate-1. One is new (R-17). The other is
the spec's own open question. Everything else is decided in the text.

Scope, per the stage rules, is a delta: the ten round-1 findings, the
human's E-1..E-10 (doc-004 `status.md` § Decisions), the rewritten spec and
US-1..US-4, the new US-11..US-14, the § Amendments table against doc-001
`spec.md`/`stories/US-2`, `US-9` and the queue lines in `docs/backlog.md`,
and the BA's own unasked decisions.

### Round-1 findings re-checked

| Finding | Verdict | Where it holds |
|---|---|---|
| R-1 | verified | BR-8's last sentence and BR-9 put «Текущая итерация» on team surfaces only. US-13 "The project list filters by a specific iteration only" |
| R-2 | verified | BR-10 "There is no reactivation", Operation completeness Cancel/reverse `n/a`, US-14 "A removed iteration cannot be brought back", § Out of scope |
| R-3 | verified | BR-2 numbers, BR-3 names unique among active siblings with removed names free, US-1 "A removed iteration's name is free again". Rename-onto-a-sibling was not covered: R-19 |
| R-4 | verified | the decomposition row `feat-NNN-iteration-filter` (M; mod-007, mod-006, feat-003) matches E-4. mod-007's deps `mod-003-work-item-core, doc-004` match the queue line `deps:mod-003,doc-004` |
| R-5 | verified | BR-7 enforces the selection on every write path, including for administrators. BPMN gate H checks active + project + selection. US-3 "…refused on write" |
| R-6 | verified | BR-7 + US-3 "A work item with no owning team cannot be scheduled"; US-13 lists such items on the project list |
| R-7 | verified | BR-8 names three keys: finish, start, number. The number is unique, so the order is total. US-4 has concrete winners. R-24 narrows the wording of one parenthetical |
| R-8 | verified | BR-8 "the server's date in UTC"; US-4 "The boundary days are covered, in UTC" |
| R-9 | verified | BR-10, NFR § Bulk writes (5,000 items, ≤ 5 s, all or nothing, BR-11 conflict), US-14. The stale count is R-22 |
| R-10 | verified | E-5 is applied throughout. § Amendments row E-5 and the mod-002/mod-006 wordings |
| R-11 | survived (superseded) | the old scenario was replaced by the concrete UTC boundary scenario, which is assertable |
| R-12 | survived | the pane (US-11) has deps mod-007 + mod-006. The sprint views also need feat-001 and doc-002's estimation slice |
| R-13 | survived (reshaped) | scheduling from the product backlog is now owned only by US-11. US-5 says it "adds no second scheduling mechanism". US-5 keeps removing an item from the sprint view, which is a different surface |
| R-14 | survived | US-3 "The owning team has no iterations to offer"; US-11 "A team with no iterations" |
| R-15 | survived | NFR § Volumes: the picker/pane is tens of rows and the filtered views stay within the 5,000 / 2 s budget |
| R-16 | survived | US-1 "An iteration is renamed and redated" names the surfaces |

### E-1..E-10 applied

| Decision | Applied in | Check |
|---|---|---|
| E-1 | BR-8, US-4, § Out of scope (no time zones) | verbatim |
| E-2 | BR-8, BR-9, US-13 | verbatim. BR-8 also counts the team iteration settings and the E-6 pane as team surfaces, which is consistent |
| E-3 | BR-10, Operation completeness, US-14, § Out of scope | verbatim |
| E-4 | § Decomposition, US-13 | verbatim, including the deps |
| E-5 | § Data model, BR-5, US-2, US-4, US-12, US-13, § Amendments | verbatim |
| E-6 | BR-13, US-11, § Decomposition, § Amendments | verbatim |
| E-7 | BR-1, BR-3, BR-5, BR-8, BR-9, US-1, § Amendments | verbatim. The move operation is the BA's addition (see below) |
| E-8 | BR-16, § Out of scope, the release-2 deps ("doc-002's estimation-fields slice") | verbatim |
| E-9 | BR-11, BR-12, US-12, § Amendments | verbatim |
| E-10 | BR-2 | "issued per project, never reused" holds word for word. The BA extends numbering to **every node at any level**. The human said «автонумерация спринта», so that extension is R-17 |

### § Amendments against doc-001

Every doc-001 statement that E-5/E-6/E-7/E-9 supersede is listed, with two
gaps. The E-7 row missed doc-001's Operation completeness Iteration Update,
which grants "dates" only; the tree move is new. The iteration dimension that
E-2/E-4 add to the M6 inline filter row was not recorded. Both are fixed in
place (R-25). The proposed queue wordings are consistent with the
decomposition:

- mod-002 and mod-006 change their summaries only, with size and deps
  unchanged.
- mod-007's wording covers exactly its row: tree, numbering, selection and
  default, pointer, UTC derivation and confirmed removal. It keeps the queued
  deps `mod-003,doc-004`. The pane is explicitly not in mod-006.

doc-001 US-1 "a default team … exists automatically" is not superseded. A
default team still comes with a project, and E-5 only allows more teams.

### The BA's own decisions (made without asking the human)

| Decision | Judgement |
|---|---|
| A parent filter includes its descendants (BR-9) | **Sound derivation.** It follows from E-7 plus ADO's "Under" semantics for iteration paths (research § Backlogs & sprints). It is the only reading in which a filter on a selected release is useful when the work sits in its sprints. The deliberate difference from the exact pane counts is stated in both BR-9 and BR-13 and tested in US-11/US-13 |
| Pane counts and «Перенести незавершённые» act only on items pointing exactly at the iteration (BR-12, BR-13) | **Sound derivation.** Counts that include descendants would double-count the sprints listed under a selected release. An inclusive bulk move would silently repoint work planned in child sprints, which is the riskier default for an irreversible bulk write. US-12 "Only items pointing exactly at the source move" tests it |
| An iteration with active children cannot be removed (BR-10) | **Sound derivation.** Without it the operation needs a cascade (§ Out of scope) or orphaned children, which breaks BR-1's "child of another *active* iteration". Refusing keeps each removal's blast radius to one node's pointers, the one R-9 bounded |
| Parent dates are independent of children (BR-4) | **Sound derivation.** doc-001 US-9 makes dates optional per iteration, and ADO does not bound child dates. Bounding would add an MVP validation nobody asked for. Consequence: BR-8's "a sprint beats its release" holds only on an equal finish. The wording is fixed (R-24) |
| Siblings ordered by start date (undated last), then number (BR-1) | **Sound derivation.** It is a display order with no business effect, and deterministic because numbers are unique. It is testable (US-1 "Children are ordered…") |
| An administrator can move a node in the tree in MVP (BR-1, US-1) | **Sound derivation, recorded as an amendment.** A tree without re-parenting leaves remove + recreate as the only fix for a misplaced node. That clears every pointer (BR-10) and cannot be undone (E-3). So the move is the non-destructive half of E-7, not a new capability. The Operation completeness change is now in § Amendments (R-25). If the human wants MVP smaller, this is the first cut (see sizing) |
| "Unfinished" = Proposed/InProgress/Resolved (BR-12) | **Sound derivation.** It is "every category except Completed and Removed" under doc-001 BR-2, and it matches velocity counting only Completed (BR-16, US-8). Resolved work is not done, and carrying it forward keeps it visible in the next sprint |

### Sizing and deps

- **mod-007 is still one L, at the upper edge.** Against doc-001's row it
  gained the tree with cycle-safe moves, the number sequence and a confirmed
  transactional removal. It lost nothing, because the filter, the pane and
  the bulk move are separate tasks. Each addition stays inside the ITERATION
  entity and its settings screen, so it is still "L solely as the module
  founder". If stage 3 of mod-007 finds it too big, the natural cut is the
  tree move (US-1's two move scenarios) as a follow-on `feat-…` (M,
  deps mod-007). That is a note for mod-007's own design, not a finding.
- **The three new M tasks have correct deps.** The filter depends on mod-007,
  mod-006 and feat-003, per E-4: it needs feat-003's filter bar and M4's team
  surfaces. The pane depends on mod-007 and mod-006: it is a backlog
  side-panel that reads M5. The move depends on the pane, since it starts
  from a pane entry, and gets mod-007/mod-006 transitively. Each is one story
  (US-13, US-11, US-12) and plausibly M.

### Rule → story coverage (renumbered rules)

| Rule | Covered by |
|---|---|
| BR-1 tree, move, order | US-1 |
| BR-2 numbering | US-1, US-4 (tie-break), US-14 |
| BR-3 names | US-1 |
| BR-4 dates | US-1, US-4 |
| BR-5 team selection | US-2, US-3, US-11, US-13 |
| BR-6 default | US-2, US-14 |
| BR-7 pointer write rule | US-3, US-11, US-14 |
| BR-8 current iteration | US-4, US-11, US-13 |
| BR-9 filter | US-13 |
| BR-10 removal | US-14 |
| BR-11 dates constrain nothing | US-3, US-12 (preamble) |
| BR-12 move unfinished | US-12 |
| BR-13 pane | US-11 |
| BR-14 permissions | US-1, US-2, US-3, US-11..US-14 |
| BR-15..17 (release 2/3) | US-7, US-8, US-10 |

No rule is left without a story, and no story introduces an unstated rule.

### R-17
Severity: major (human)
Affects: BR-2, BR-3, BR-8 (third key), US-1, spec § Open questions
Problem: The human's answer to R-3 was «автонумерация спринта в одном
проекте»: sprints are numbered. BR-2 numbers **every node at any level**, and
BR-3 names any node left unnamed «Спринт N». This is the E-10 reading the
orchestrator recorded ("numbers issued per project, never reused"), and the
text does not depart from it. The tree (E-7) makes the choice visible,
though. In the most common setup the administrator creates «Релиз 1» first
(it takes number 1) and then its first sprint with an empty name. That
sprint appears as «Спринт 2», and each later release makes the sprint names
skip again. Either the human accepts the gaps, or only sprint-like nodes
consume numbers. The model has no node kind, and any node can gain children
later, so the second option needs a rule the spec does not have (for
example, "only a node created with an empty name takes the next number").
The BA's open question (is the name editable) is the other half of the same
decision.
Required resolution: the human picks one. (a) One sequence for every node,
as written: releases consume numbers and sprint names may skip. (b) Only
nodes created with an empty name consume the next number. A named node
(«Релиз 1») takes none, and BR-8's third key becomes creation order. The
spec's open question on name editability is answered at the same time.
Status: resolved — by the human's E-11 (neither (a) nor (b): a node kind, only sprints numbered from a per-project sequence) and E-12 (the name is editable, the number fixed), applied in BR-1..BR-3, BR-8, BR-14, US-1, US-4, US-14; the spec's open question is closed

### R-18
Severity: minor
Affects: BR-2, BR-3, US-1
Problem: The number sequence had three untestable edges. Does a refused
creation burn a number? Do two concurrent creations get distinct numbers?
What happens when the generated «Спринт N» is already taken by a hand-named
sibling?
Status: resolved
Resolution: fixed in place. BR-2: "A refused creation issues no number, and
two creations at the same moment never receive the same one". BR-3: a
generated «Спринт N» that is taken is refused like any duplicate. US-1 gained
"A refused creation issues no number" and "Two iterations created at once
get different numbers".

### R-19
Severity: minor
Affects: BR-3, US-1 ("A duplicate name among siblings is refused")
Problem: Uniqueness was tested on creation and on move, but not on rename.
Rename is the easiest path to a duplicate.
Status: resolved
Resolution: fixed in place. BR-3 now says "on creation, rename and move
alike". The US-1 scenario covers "or renames another child of that parent to
it".

### R-20
Severity: minor
Affects: BR-1, US-1 ("An iteration is moved in the tree")
Problem: The text never said that a moved node takes its subtree with it,
and the scenario had no child to check it.
Status: resolved
Resolution: fixed in place. BR-1 says "together with its whole subtree". The
scenario now has a child «Неделя 1» whose path becomes «Релиз 2 / Спринт 2 /
Неделя 1».

### R-21
Severity: minor
Affects: US-1 ("Invalid dates are refused")
Problem: "a message that both dates are required" had no Russian copy. Every
other user-visible string in the spec has one (AGENTS.md, language rule).
Status: resolved
Resolution: fixed in place with «Укажите обе даты или ни одной».

### R-22
Severity: minor
Affects: BR-10, BR-12, US-12, US-14
Problem: Both bulk writes show a count and then act. The spec did not say
what happens when the count changes between the prompt and the confirmation.
Someone may schedule into the sprint meanwhile. The confirmed "6" (or "12")
would then be false, and a test cannot tell whether the operation acts on
the old set, acts on the new set, or refuses.
Status: resolved
Resolution: fixed in place with the safe reading for an irreversible bulk
write. The operation acts on the state at confirmation. If the number
differs from the one confirmed, nothing is written and the prompt is shown
again with the new number. New scenarios cover it: US-12 and US-14 each gain
"The count changed before the confirmation".

### R-23
Severity: minor
Affects: BR-13, US-11
Problem: The pane counts "the team's items of the viewed level" without
saying whether «Удалена» items count. They still point at the iteration
(BR-10 even clears them on removal), but no backlog shows them. So the count
would disagree with what the user can drag.
Status: resolved
Resolution: fixed in place. BR-13 now reads "(any state except «Удалена»)",
and the first US-11 scenario asserts that a deleted item is not counted.

### R-24
Severity: minor
Affects: BR-8
Problem: The parenthetical "a sprint beats the release that contains it"
reads as a general truth. Under BR-4 a child may end after its parent (US-1
"A parent's dates do not constrain its children"). In that case the release
finishes first and wins, so the parenthetical contradicts the rule it
illustrates.
Status: resolved
Resolution: fixed in place. It now reads "so a sprint beats a containing
release that finishes the same day". This is what US-4 "A sprint beats the
release that contains it" tests.

### R-25
Severity: minor
Affects: § Amendments to doc-001 and the queue
Problem: The table missed two doc-001 statements. First, Operation
completeness Iteration Update "admin (dates; …)" grants neither move (from
E-7) nor rename. Second, the M6 MVP row lists the inline filter dimensions as
"state/assignee/tag/area", and E-2/E-4 add iteration, on the project list
too.
Status: resolved
Resolution: fixed in place. The E-7 row now includes the Update column, with
move added and rename subject to the open question. A new "E-2, E-4" row
records the filter addition, and the intro says E-2/E-4 add to one statement.

### Checked and found sound

- **Contradictions between the MVP stories.** None found:
  - BR-5 ("nothing else" on team surfaces) and BR-9 (team filter = current +
    selection) agree.
  - The exact pane counts (BR-13) and the inclusive filter (BR-9) are both
    stated.
  - Every write path uses BR-7 and one refusal message: form (US-3), pane
    (US-11), bulk target (US-12) and a pick after removal (US-14).
- **Concurrency.** Every new write path names its doc-001 BR-11 outcome:
  pane drop (US-11), bulk move and removal against an open form (US-12,
  US-14), and a pick after removal (US-14).
- **Release 2/3 stories** still carry `shippable: no`. US-5 no longer owns
  pane scheduling. US-8 accounts for the MVP bulk move ("Work moved on by
  «Перенести незавершённые»…").
- **The new criteria are verifiable.** US-11..US-14 give concrete counts,
  messages and states. The failure-midway scenarios (US-12, US-14) need
  fault injection, which is normal for an all-or-nothing requirement.

## Questions only the human can answer (for gate-1, round 2)

1. **Which nodes take a number** (R-17, E-10): (a) every node, releases
   included, as written, so the first unnamed sprint under «Релиз 1» becomes
   «Спринт 2»; or (b) only nodes created with an empty name, so a named
   «Релиз 1» takes no number and sprint names run 1, 2, 3?
2. **Is an iteration's name editable** (spec § Open questions, BA's
   proposal: yes; the number stays fixed)? If no, US-1 "An iteration is
   renamed and redated", BR-14, the Roles table and Operation completeness
   drop renaming. Names are then either set once at creation or always the
   fixed «Спринт N», and the fixed name leaves releases unnamable.

## Delta check — E-11/E-12

Verdict: **approved-by-ai** — 0 blockers, 0 majors, 2 minors (both fixed in
place). No human-only question remains.

Scope, as instructed, is narrow: E-11/E-12 against BR-1, BR-2, BR-3, BR-8,
BR-14, the data model, Operation completeness, § Amendments, the mod-007
decomposition row, US-1, US-4 and US-14, plus a search of every other story
for "number", "kind", «Спринт» and "release" to find contradictions.

### E-11/E-12 applied

| Decision | Applied in | Check |
|---|---|---|
| E-11 | BR-2 (kind chosen at creation, a sprint takes the next per-project number at any level, a release takes and consumes none, never reissued), BR-1, BR-8, BR-14, Terms, data model, BPMN A/A1, Operation completeness, § Out of scope, § Amendments, mod-007 row and queue wording, US-1, US-4, US-14 | faithful. E-10's "never reused" and R-18's two edges (a refused creation issues no number, concurrent creations get distinct numbers) survive for sprints |
| E-12 | BR-3 (any iteration renamed by an admin; an empty sprint name shows «Спринт N»; rename changes neither kind nor number), Roles, BR-14, Operation completeness Update, § Amendments E-7 row, US-1 "A sprint's name is changed, and a cleared name falls back" | faithful. The number stays fixed across renames and moves (US-1 rename and move scenarios) |

R-17 and the spec's open question are closed. No release-2/3 story and none
of US-2, US-3, US-11, US-12 and US-13 depends on a numbering or naming
behaviour that E-11/E-12 changed. Their hand-named «Спринт 2», «Релиз 1»
etc. stay valid under BR-3.

### The BA's derived rules

| Rule | Judgement |
|---|---|
| Kind chosen at creation and immutable; a wrong kind is fixed by removal and re-creation (BR-2) | **Sound for MVP, with a known cost. Not a blocker.** The fix is destructive only when the mistaken node already carries pointers or children. BR-10 clears the pointers and refuses removal while children exist, so the children must first be moved out (BR-1). Round 2 used the same kind of argument to justify the tree move. The difference is frequency: a wrong kind is typically noticed at creation, before anything points at it. A kind-change operation would also be purely additive later. The BR-2 rationale ("orphan" a number) is weak, since removal orphans numbers anyway. The real cost of release→sprint is that number order would stop matching creation order, and BR-1 and BR-8 rely on that match. For the gate-1 summary: approving the spec accepts immutable kinds |
| No placement constraint between kinds (BR-1) | **Sound.** ADO has none. A constraint would be a new MVP validation nobody asked for. The human's reason for the kind was numbering. US-1 "Either kind may sit at any level" tests it |
| A release requires a name (BR-3) | **Sound.** A release has no number, so it has nothing to fall back to. A fixed «Релиз» fallback would collide among siblings at once. Rename was not tested: R-27 |
| An empty sprint name shows «Спринт N» and competes for sibling uniqueness under that name (BR-3) | **Sound.** Otherwise a hand-named «Спринт 7» and an unnamed sprint 7 would be shown identically under one parent, and pickers could not tell them apart by path. US-1 "A refused creation issues no number" tests creation. Rename and move follow from "on creation, rename and move alike" |
| Full tie: a sprint over a release, then creation order, and for sprints the lower number (BR-8) | **Sound.** Among nodes with identical dates the sprint is the finer grain, in the spirit of the "later start" key. Creation order is total when the system records it. For sprints the number is that order, since numbers are issued in sequence at creation. US-4 has concrete winners for both tie cases |

### R-26
Severity: minor
Affects: BR-1
Problem: BR-1 said the kind "drives numbering and naming only". BR-8's third
key ("a sprint over a release") also uses the kind. The rule contradicted
the one it sits next to, and a developer reading BR-1 alone would leave the
kind out of the current-iteration derivation.
Status: resolved
Resolution: fixed in place. It now reads "the kind drives numbering, naming
and one tie-break of BR-8 only, never structure".

### R-27
Severity: minor
Affects: BR-3, US-1 ("A release needs a name")
Problem: E-12 makes every name editable, and BR-3 requires a release's name.
Only creation was tested, though. Renaming a release to blank is the one
rename path that has to be refused, and no criterion covered it.
Status: resolved
Resolution: fixed in place. The scenario's When now reads "adds a release
with an empty or blank name, or renames an existing release to an empty or
blank name". The "A number is never issued twice" Given was tightened by one
line to keep US-1 at 149 lines.

### Checked and found sound

- **Line budgets.** spec.md is 250 lines (BR-1 edited within its row) and
  US-1 is 149 lines.
- **Verifiability.** The E-11/E-12 criteria in US-1, US-4 and US-14 assert
  concrete numbers, names and messages («Укажите название релиза», «Спринт 7»,
  numbers 3/5/7) and concrete winners.
- **Release-2/3 stories** (US-5..US-8) use "sprint" for any iteration opened
  in the sprint view ("the sprint view for that iteration"), regardless of
  kind. That matches ADO and contradicts nothing in MVP.
  `mod-NNN-sprint-views` may still decide whether a release gets a sprint
  view. That is a design note, not a finding.
