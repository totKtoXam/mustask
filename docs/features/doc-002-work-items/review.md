# Review: doc-002-work-items

## Round 1 — requirements

Artifact: `spec.md` (251 lines at review start) and `stories/US-1..US-11`.
Normative references checked against: `doc-001-domain-map/spec.md` (M3
boundary, MVP table, ER model, BR-1..13, decomposition), its `status.md`
§ Decisions (D-1..D-12), its approved stories, `research/ado-boards.md`,
`screenshots/README.md`, and `docs/backlog.md` (the six queued lines).

Verdict: **needs-human-decision** — 1 blocker and 3 majors carry questions
only the human can answer; the other 13 findings are fixed in place.

### Rule → story coverage

Module rules (`spec.md` § Business rules) — all covered:

| Rule | Covered by |
|---|---|
| BR-1 type immutable | US-1 (type picker, non-editable afterwards) |
| BR-2 one project for item, area, tags, links | US-1, US-5, US-8 |
| BR-3 Заголовок trim / 255 | US-1 |
| BR-4 rich text safe subset | US-2, US-6 |
| BR-5 identifier | US-4 |
| BR-6 «Удалена» never in the status control | US-3 |
| BR-7 any → any transition | US-3 |
| BR-8 Parent/Child and its refusals | US-5 |
| BR-9 no cascade to children | US-5, US-10 |
| BR-10 history of everything | US-9 (and a history line in every story) |
| BR-11 tags project-scoped, case-insensitive | US-8 |
| BR-12 comment edit/delete rights | US-6 |
| BR-13 attachment add/remove only | US-7 |
| BR-14 deleted item read-only | US-2, US-6, US-7, US-10 |
| BR-15 what optimistic concurrency guards | US-2, US-8 |

doc-001 cross-module rules as they bear on M3:

| doc-001 rule | Covered by / disposition |
|---|---|
| BR-1 one project, one type | US-1 (via module BR-1/BR-2) |
| BR-2 state → category | US-3 |
| BR-3 area owns team membership of an item | US-1 (Область of own project), US-11 (sub-area inheritance); the board side is M4 |
| BR-4 history | US-9 |
| BR-5 permission codes from `ORG_MEMBERSHIP` | § Roles + the reader/outsider scenario in every story |
| BR-6 soft delete, indefinite retention | US-10 |
| BR-7 single iteration pointer | § Fields; the field ships with `mod-007-iterations`, out of this task's six lines by design |
| BR-8 tags project-scoped | US-8, US-11 |
| BR-9 current iteration | not M3 — M5, out of scope |
| BR-10 transitions, restore target | US-3, US-10 |
| BR-11 optimistic concurrency | US-2, US-3 |
| BR-12 stack rank owned by M4, read-only for M3, filters never reorder | **was uncovered — R-7**, now US-11; restore position stays M4's (§ Out of scope) |
| BR-13 assignee candidate set | US-2 |

### R-1
Severity: blocker
Affects: BR-8, US-5, § Out of scope, `feat-001-work-item-links`
Problem: BR-8 refuses a link unless "the parent's backlog level is strictly
higher than the child's", which forbids a Баг under a Пользовательская
история — the single most common parenting in a tracker after
Epic→Feature→Story. The spec presents it as a consequence of D-3, and it is
not: D-3 places Баг on the Истории level and says nothing about parenting.
The reference product does not enforce a level order either — research
§ Link types gives Parent/Child the tree topology (one parent, no cycles)
and nothing more, and ADO happily parents a Bug to a User Story. So MUSTASK
would ship a restriction that is neither decided nor borrowed, and US-5
turns it into a test ("The parent must sit on a higher backlog level") that
would then have to be rewritten together with the rule, the data and the
hierarchy view in release 2.
Required resolution: the human chooses — (a) keep BR-8 strict, accepting
that a bug is never a child of a story in MVP; or (b) relax the condition to
"the parent's level is higher **or equal**, and the parent is not the item
itself" (cycles and one-parent unchanged), which costs one comparison and
matches the reference. Recommendation: (b) — it removes a restriction, so
nothing built on (b) has to be undone if the human later prefers (a), while
the reverse is a data migration. Whichever is chosen, BR-8's own wording and
the § Out of scope line must stop attributing it to D-3.
Status: resolved
Resolution: the human chose (b), W-1. BR-8 now reads "higher than or equal
to" plus "not the item itself", sourced to W-1 and research § Link types; the
BPMN gateway matches; the § Out of scope bullet on same-level parenting is
gone. US-5 replaced the refusal with "A bug may be the child of a user
story", added same-type, lower-level-refused and self-parent scenarios, and
its cycle scenario now uses one level so only the cycle rule can refuse.

### R-2
Severity: major
Affects: § Types levels and states, US-3, `mod-001-process-seed`
Problem: the state table calls itself a mirror of the reference product's
Agile process, and for four of the five types it is. For «Задача» it is not:
Agile's Task has New / Active / Closed / Removed, i.e. «Новая» / «Активна» /
«Закрыта» / «Удалена», while the spec carries «Новая» / «Выполняется» /
«Готово» — the Basic process's To Do / Doing / Done. The names come from
doc-001's approved US-3, so the spec is consistent with the umbrella and
wrong about its own provenance. The cost of leaving it undecided is not
cosmetic: `mod-001-process-seed` writes these names into seed data and they
become user-visible copy that later changes only by a migration, and a user
moving between a story and its child task meets two vocabularies for the
same category (InProgress is «Активна» above and «Выполняется» below).
Required resolution: the human confirms one of — (a) keep the doc-001 US-3
names for «Задача» («Новая»/«Выполняется»/«Готово») and accept the mixed
vocabulary; or (b) align «Задача» with Agile («Новая»/«Активна»/«Закрыта»),
which makes one vocabulary across all five types and requires a one-line
correction to doc-001's US-3 wording (already merged, so an errata note in
this spec suffices — the umbrella is not reopened for a decision, only for
an inherited typo-level name).
Status: resolved
Resolution: the human chose (b), W-2. The «Задача» row now reads «Новая» /
«Активна» / «Закрыта» / «Удалена»; the divergence paragraph is replaced by an
errata note naming doc-001 US-3 as superseded on this point (and its US-6 /
US-10 «Выполняется» examples); US-3, US-10 and US-11 use «Активна»; US-3
gained a one-label-set scenario (W-7).

### R-3
Severity: major
Affects: § Fields (Исполнитель), US-2, doc-001 BR-13
Problem: the assignee is "every member of the item's organization", and
nothing anywhere says what happens to work items assigned to a user whose
`ORG_MEMBERSHIP` is later removed (doc-001 Operation completeness gives an
org admin exactly that power). Three behaviours are possible — keep the
stored assignee and show it as inactive, clear the field silently, or refuse
the membership removal while items are assigned — and they differ in
visible behaviour (the assignee filter of US-11, the list column, history)
and in what `mod-003-work-item-core` has to build. doc-001 set a precedent
for the neighbouring case: deleting an iteration leaves its work items
unassigned. Leaving this open means each of `mod-002` and `mod-003`
inventing a different answer.
Required resolution: the human decides. Recommendation: the work item keeps
the stored assignee, which is shown as a former member and is not offered to
new assignments; nothing is rewritten in history. Then state it as a rule in
§ Fields and as a criterion in US-2.
Status: resolved
Resolution: the human chose the recommendation, W-3. § Fields (Исполнитель)
states the rule; US-2 gained "An assignee who left the organization stays on
the item" and "A former member is never offered for a new assignment";
US-11 gained "Items of a former member can still be found" (filter and list
column), and `feat-003`'s row mentions former members in the assignee filter.

### R-4
Severity: major
Affects: § Decomposition, `mod-003-work-item-core`, `docs/backlog.md`
Problem: the refinement puts the work item list surface into
`mod-003-work-item-core`, which the queued line does not mention (it says
WorkItem CRUD, history, identifier scheme). The line is already `L` and
already owes the history/audit ADR, and now also carries the create dialog,
the whole form, tags and a list. Nothing else can take the list — `feat-003`
depends on `mod-006` (boards), so routing the list there would block the
only way of seeing a work item behind the whole of M4 — but a silent scope
growth on an `L` line is how an `L` becomes two.
Required resolution: the human (or the orchestrator on a standing approval)
confirms `mod-003` stays `L` with the list included, and the queue line's
`note:` records the growth; or the list becomes its own `S`/`M` line
depending only on `mod-003`. Recommendation: keep it in `mod-003` — a list
with fixed columns, a fixed sort and no filters is small — and update the
queue note. Every other size in the table matches its queued line exactly
(`feat-001` M, `feat-002` M, `mod-004` L, `mod-005` L, `feat-003` M), and no
story falls between the six lines: US-1..US-4, US-8, US-9 and the list half
of US-11 → `mod-003`; US-5 → `feat-001`; US-6 → `feat-002`; US-7 →
`mod-004`; US-10 → `mod-005`; the filter half of US-11 → `feat-003`.
Status: resolved
Resolution: the human confirmed the recommendation, W-4: `mod-003` keeps the
list surface at size `L`. § Decomposition's note to the orchestrator says the
queue line's `note:` must record the growth; the queue edit itself is the
orchestrator's.

### R-5
Severity: major
Affects: § Types levels and states, OQ-5, `doc-003`
Problem: two statements about the Resolved category were imprecise enough to
mislead `doc-003`. (1) "the Resolved **category** has one user, Баг" is true
but stops before the consequence: with board columns driven by categories
(doc-001, M4 MVP row) the Истории level holds two types with different
category sets, so that board gets a Resolved column no Пользовательская
история can ever enter, and a story in «Разрешена» and a bug in «Разрешена»
— identical labels — stand in different columns. (2) OQ-5 tells the human
that dropping «Разрешена» from Эпик and Фича "gives those boards four
columns instead of five". That is wrong under the same category rule:
«Активна» and «Разрешена» are both InProgress and share one column either
way, so the answer changes no board column at all. A human question whose
stated cost is false is worse than no question.
Required resolution: state the Истории consequence in full and hand
`doc-003` the column-model problem; restate OQ-5's real trade-off.
Status: resolved
Resolution: fixed in place — the paragraph under the state table now spells
out the Истории column collision and names `doc-003` as its owner, and
OQ-5's cost sentence is replaced by the correct one (no column changes;
what changes is the picker and the seed).

### R-6
Severity: major
Affects: BR-15, § Fields (Теги), US-8, US-2
Problem: BR-15 listed a tag among the writes that are "separate" from the
work item's field set and therefore not guarded by optimistic concurrency,
while § Fields lists Теги as a form field with a default and a limit, and
US-2 describes the form as one save. Both cannot be built: either the chip
commits immediately on its own or it commits with Save under the revision
check. `mod-003` would have to guess, and the guess shows up as a user-
visible difference (an unsaved chip surviving "leave without saving").
Required resolution: pick one and say it in all three places.
Status: resolved
Resolution: fixed in place — Теги are part of the guarded field set (matches
§ Fields, US-2 and the reference product, where tags are saved with the
item). BR-15 now excludes tags from the separate-write list, the § Fields
row says so, and US-8 gained a one-line preamble. If the human prefers
immediate chip writes, this is the one fix in this round to reverse.

### R-7
Severity: major
Affects: US-11, doc-001 BR-12
Problem: doc-001 BR-12 — the backlog order is M4's, M3's lists read it and
never write it, and a filter hides non-matching items but never changes the
relative order of the rest — had no story and no criterion anywhere in
doc-002, although `feat-003-work-item-search` is exactly the code that could
break it. The neighbouring case (does a restored item regain its backlog
position?) was also unanswered.
Required resolution: a criterion in US-11 for the order-preserving filter,
and an explicit statement that the restore position belongs to `doc-003`.
Status: resolved
Resolution: fixed in place — US-11 gained the scenario "A filter hides items
but never reorders them" (citing doc-001 BR-12), and § Out of scope now says
`BACKLOG_ENTRY` is M4's, including whether a restored item regains its
position.

### R-8
Severity: major
Affects: US-1, US-4, BR-5
Problem: US-1 required that a refused create consume no identifier, while
US-4 and BR-5 allow gaps and explicitly do not require consecutive numbers.
The US-1 criterion is both a contradiction and unbuildable as written with a
database sequence (a rolled-back insert burns its number), so it would have
been either quietly dropped in implementation or paid for with a slower
issuing scheme nobody decided on.
Required resolution: drop the "no identifier is consumed" clause and keep
"no work item is created".
Status: resolved
Resolution: fixed in place in US-1, with a pointer to BR-5/US-4 on gaps;
BR-5 now states the gap allowance itself instead of implying consecutive
numbering.

### R-9
Severity: major
Affects: US-5
Problem: "Then either the action is refused with the existing parent named,
or the team member is asked to confirm replacing it" is not a criterion —
it is two criteria joined by "or", so no test can fail it, and the second
branch contradicts the same story's closing scenario ("re-parenting is
remove plus add", "no way to edit a link in place").
Required resolution: pick the behaviour consistent with the rest of the
story.
Status: resolved
Resolution: fixed in place — the link is refused with the existing parent
named, and the criterion now states that re-parenting is remove plus add.

### R-10
Severity: major
Affects: § Decomposition, § Roles, § Fields, all eleven stories
Problem: every implementation line was referenced as `mod-NNN-…` /
`feat-NNN-…`, and the table justified it with `features/numbering.md`. That
reads the rule backwards: the literal `NNN` is for a task that has no number
yet, and these six were numbered when their lines entered the queue
(`mod-003`, `feat-001`, `feat-002`, `mod-004`, `mod-005`, `feat-003`).
Anyone opening `mod-003-work-item-core` later would have to re-derive which
paragraph of this spec is their scope by matching slugs, and `deps:` in the
queue cannot be checked against slugs at all.
Required resolution: name each line by its queued id, and its deps by the
short ids of the queued lines.
Status: resolved
Resolution: fixed in place across `spec.md` and the `shippable:` line of
every story. The one genuinely unnumbered id, the conditional
`feat-NNN-work-item-type-fields` under OQ-1, keeps `NNN` and now says the
orchestrator would have to queue it.

### R-11
Severity: minor
Affects: § Roles
Problem: the Roles table gave the organization administrator "deleting any
comment … plus purging" but not removing another member's attachment, which
§ Operation completeness and US-7 both grant; and it assigned the
registration of every permission code — comment moderation included — to
`mod-003-work-item-core`, although comments ship in `feat-002`, attachments
in `mod-004` and purge in `mod-005`.
Required resolution: complete the admin row, note the member's own-attachment
limit, and split code registration by slice.
Status: resolved
Resolution: fixed in place in § Roles (table rows and the paragraph under it).

### R-12
Severity: minor
Affects: US-2, US-4
Problem: the form's region list omitted Тип, although US-1 requires the type
to be visible and read-only; and the list implied that a work item form
arrives complete, while «Связи», «Обсуждение», «Вложения» and Итерация come
with `feat-001`, `feat-002`, `mod-004` and `mod-007`. US-4 likewise required
the identifier on the board card, which `mod-003` cannot deliver — the card
is M4's.
Required resolution: add Тип; say which slice brings which region; qualify
the board card.
Status: resolved
Resolution: fixed in place — US-2's preamble now lists Тип and the
slice-by-slice arrival of the regions, and US-4's first scenario points the
board card at `mod-006` / `doc-003`.

### R-13
Severity: minor
Affects: BR-4, BR-5
Problem: two rules cited sources that do not support them. BR-4 (the safe
rich-text subset) cited "AGENTS.md § Hard rules; D-8" — AGENTS.md's hard
rules are about secrets, branches and language, and D-8 is about attachments
and the area tree. BR-5 cited "research § How the project uses Boards",
which is an aggregate count of the analysed project and says nothing about
identifiers. A rule whose cited source does not contain it is a rule the
next session deletes or rewrites at will.
Required resolution: cite what actually carries the rule.
Status: resolved
Resolution: fixed in place — BR-4 now cites research § Fields (Описание is
html) plus D-8 for the images-are-attachments clause; BR-5 cites OQ-2 and
research § Other capabilities (move to another project).

### R-14
Severity: minor
Affects: BR-5, OQ-2
Problem: BR-5 stated as settled ("unique within the organization", "survives
a move between projects") exactly what OQ-2 still asks the human. A
normative rule must not presume the answer to an open question — if the
human picks the instance-wide alternative, BR-5 silently becomes false.
Required resolution: phrase BR-5 so it holds for whichever scheme is chosen.
Status: resolved
Resolution: fixed in place — BR-5 now says "unique within the scope OQ-2
fixes (recommended: the organization)" and marks the move-survival as a
property of the recommended scope. US-4 already handled this correctly.

### R-15
Severity: minor
Affects: US-1, US-2, US-11
Problem: three edge cases from the review checklist had no criterion:
resubmission (a double-clicked or retried create producing two work items),
an integration-style mid-flight failure of a different kind — a save
arriving for a work item another user deleted meanwhile (BR-14 says the item
is read-only, but nothing said what the writer sees), and the empty list of
a project that has no work items at all, as distinct from a filter that
matched nothing.
Required resolution: one criterion each.
Status: resolved
Resolution: fixed in place — US-1 gained "Submitting the create form twice
creates one work item", US-2 gained "A save arrives for a work item deleted
meanwhile", US-11's empty-result scenario now distinguishes the empty
project.

### R-16
Severity: minor
Affects: § Non-functional requirements
Problem: "the list returns within 2 s", "opening a form and saving it each
complete within 1 s" — with no measurement basis, both are untestable: a
cold instance under ten concurrent writers fails them, a warm one passes,
and QA would have had to invent the conditions.
Required resolution: name the basis.
Status: resolved
Resolution: fixed in place — the budgets are stated as server response times
on a warm instance, one request at a time. If the human wants a concurrency
figure instead, that is a change to doc-001's NFR, not to this spec alone.

### R-17
Severity: minor
Affects: none of the artifacts under review — a note for the orchestrator
Problem: `docs/backlog.md` still carries the `doc-002-work-items` line in
§ Next, although the task's branch exists. Per `docs/features/numbering.md`
the line is removed by a separate commit to `main` when the task starts, so
the queue currently shows a task that is already claimed.
Required resolution: the orchestrator removes the line (the queue is outside
this reviewer's write zone).
Status: open
Resolution: not applied — `docs/backlog.md` is the orchestrator's file.

## Human-only questions for gate-1

1. **OQ-1 — per-type fields.** Common field set for all five types, or add
   «Критерии приёмки» to Пользовательская история and «Шаги воспроизведения»
   to Баг? (BA recommends adding the two; that adds one queued `M` line.)
2. **OQ-2 — identifier scheme.** Per-organization sequence shown bare
   (recommended), per-project with a key, or instance-wide?
3. **OQ-3 — one state-label set regardless of type's gender** (recommended)
   or per-type labels?
4. **OQ-4 — attachment limits.** ≤ 25 MB per file, ≤ 50 per item, no
   extension filter, no virus scan (recommended) — confirm or replace.
5. **OQ-5 — «Разрешена» on Эпик and Фича.** Keep (recommended) or drop. Note
   the corrected trade-off (R-5): it changes no board column either way.
6. **R-1 — may a Баг be a child of a Пользовательская история?** BR-8's
   "strictly higher level" forbids it; neither D-3 nor the reference product
   requires that. Reviewer recommends relaxing to "higher or equal".
7. **R-2 — «Задача» state names.** Keep doc-001 US-3's
   «Новая»/«Выполняется»/«Готово» (Basic-style, zero change) or align with
   the Agile vocabulary the other four types use,
   «Новая»/«Активна»/«Закрыта»?
8. **R-3 — an assignee who leaves the organization.** Keep the stored
   assignee as a former member (recommended), clear the field, or refuse the
   membership removal?
9. **R-4 — `mod-003-work-item-core` scope.** Confirm it stays `L` with the
   work item list surface added (recommended, plus a queue `note:`), or
   split the list into its own line.

## Round 2 — requirements

Delta check over the rework `8a10cb7..aad9ac6` against the human's answers
W-1..W-9 (`status.md` § Decisions). This session had no shell, so the delta
was read from the files at `aad9ac6`: every section and story the rework
names (§ Types levels and states, § Fields, BPMN, BR-5/8/16, § Operation
completeness, NFR, § Out of scope, § Decomposition, § Open questions;
US-2, US-3, US-4, US-5, US-7, US-11 and the new US-12), plus a search of
`spec.md` and every story for leftovers of the old decisions.

Verdict: **needs-human-decision** — OQ-6, OQ-7, OQ-8 and R-23 are for the
human; the other six new findings are fixed in place.

### W-1..W-9 applied

| Answer | Where | Result |
|---|---|---|
| W-1 higher-or-equal parent | BR-8, BPMN gateway L, US-5 (bug under story, same type, lower refused, self, one-level cycle) | applied; no "strictly" left anywhere |
| W-2 «Задача» Agile names | state table, errata note, US-3 | applied; «Выполняется»/«Готово» survive only inside the errata and US-3's pointer to it |
| W-3 former-member assignee | § Fields, US-2 (two scenarios), US-11, `feat-003` row | applied |
| W-4 list stays in `mod-003`, L | § Decomposition row and orchestrator note | applied |
| W-5 two type fields, own M line | § Fields, BR-4, US-12, `feat-NNN-work-item-type-fields` (M, deps `mod-003`) | applied |
| W-6 `{ProjectKey}-{WorkItemKey}-{Sequence}`, per-project sequence | BR-5, BPMN node E, US-4, US-11 (number alone), `mod-003` row | applied; no bare-number or per-organization identifier left; one weaker wording in US-10 — R-19 |
| W-7 one label set | § Types intro, US-3 | applied |
| W-8 Google Drive, no MUSTASK limits | BR-16, BPMN T, NFR, US-7, `mod-004` row | applied; no 25 MB / 50-file limit left; purge vs Drive — R-18; out-of-scope attribution — R-23 |
| W-9 «Разрешена» on Эпик/Фича | state table, US-3 | applied |

Round 1's open findings: R-1..R-4 are genuinely resolved — each Resolution
line matches the text at `aad9ac6`. R-17 is unchanged and outside the
artifact (the orchestrator's queue).

Judgment calls of the business-analyst, checked:

- **A former member who rejoins is a normal member** (US-2): consistent with
  W-3, which ties the mark to the membership, not to the person — accepted.
- **US-12 as a new story**: one story per the new queue line, `depends-on`
  US-2, criteria testable and consistent with US-2, US-9, US-11 and BR-4/15 —
  accepted.
- **Extension filtering and virus scanning out of scope "under W-8"**: W-8
  answers size and count only — R-23.
- **`spec.md` at 262 lines** (265 after this round's fixes) against the ~250
  index budget: not a finding. The excess is the three open questions, which
  shrink to a line once gate-1 answers them; no story content leaked back
  into the index and the feature is not too big for one task.

### R-18
Severity: major
Affects: § Operation completeness (Attachment), § Decomposition (`mod-004`), US-10, BR-16
Problem: US-10 says a purge leaves the item's attachments "gone for good",
and the NFR says purge destroys the item. Yet the Attachment row and the
`mod-004` row left "whether the file in Drive is kept or dropped … on purge"
to the implementation line. With files now in an external store (W-8),
keeping them would leave purged data in a third party's storage while the
user is told it is gone. An implementation task would be making a data
retention decision, and it would contradict an approved criterion.
Required resolution: a purge deletes the files from Drive, and a purge never
ends half-done. Only the removal of a single attachment, which users cannot
see either way, stays with `mod-004`.
Status: resolved
Resolution: fixed in place. The § Operation completeness Attachment row and
the `mod-004` decomposition row now say that a purge deletes the item's files
from Drive. US-10's purge scenario gained "the attachment files in Google
Drive included" and "a purge never ends with the item gone while its files
remain in Drive".

### R-19
Severity: minor
Affects: US-10, BR-5, US-4
Problem: US-10 said the purged item's "identifier is never issued again".
Under W-6, that still allows the same number under another type key: after
MSK-BUG-42 is purged, MSK-TASK-42 would be a new identifier. BR-5 and US-4
both forbid that ("under any type key"). A test written from US-10 alone
would pass an implementation that breaks BR-5.
Required resolution: state the rule on the number.
Status: resolved
Resolution: fixed in place. US-10 now reads "its number is never issued
again in the project, under any type key (BR-5, US-4)".

### R-20
Severity: minor
Affects: US-4, OQ-6
Problem: W-6 makes the number unique within a project, and OQ-6's
recommendation makes project keys unique within an organization. Two
organizations can therefore both have `MSK-BUG-42`, which OQ-6 says itself.
US-4's direct-address criterion only covered a stranger being refused. It
did not say what happens when the stranger's own organization has a namesake.
An address built from the identifier alone would send that user to their own
item, and the criterion would still pass.
Required resolution: the address resolves to exactly one item, never to a
namesake in another organization.
Status: resolved
Resolution: fixed in place. US-4's last scenario now covers the namesake
case, conditioned on OQ-6, so it holds whatever key scope the human picks.

### R-21
Severity: minor
Affects: US-5, BR-8, `doc-003`
Problem: W-1 allows a parent and a child on the same level (a bug under a
story, a task under a task). The rework removed the out-of-scope line but
never handed the consequence to M4. A backlog of one level now holds a
parent and its child side by side, and the reference product does not nest
same-level items in its backlog tree. Round 1 handed the Resolved-column
problem to `doc-003` explicitly (R-5). This one had no such handoff, so
`doc-003` would find it only by rereading this spec.
Required resolution: a consequence note for `doc-003`, as in US-3.
Status: resolved
Resolution: fixed in place. US-5's preamble gained a "Consequence for
`doc-003`" paragraph.

### R-22
Severity: minor
Affects: § Open questions, OQ-8
Problem: OQ-8 was not well posed for the human. (1) "so leaving an
organization loses its files" reads as data loss, when the meaning is loss
of access for the user who leaves. (2) The recommendation's main cost was
missing. One instance-owned Drive with no MUSTASK limits (BR-16) means one
shared quota, and one organization can exhaust it for every other; US-7's
quota-exhausted scenario is exactly that failure. (3) The recommendation
named "a service account" as the owner. Google's Drive API guidance is that
a bare service account has no storage quota of its own, so this in practice
means a Google Workspace shared drive, which is a purchasing and ownership
fact the human should see before choosing.
Required resolution: restate the recommendation and its costs.
Status: resolved
Resolution: fixed in place. OQ-8 now says "loses access", names the shared
quota, and states the shared-drive requirement as likely, to be confirmed by
`mod-004`'s ADR (the reviewer could not verify Google's current terms from
this session).

### R-23
Severity: minor
Affects: § Out of scope, BR-16, US-7
Problem: § Out of scope drops extension filtering and MUSTASK's own virus
scanning and cites BR-16, whose only source is W-8. W-8 answers size and
count limits and nothing more. Round 1's OQ-4 did recommend no extension
filter and no virus scan, and the human replaced OQ-4's limits without
addressing that half. So the exclusion may reflect the human's intent, but
nothing on record says so. This is a security posture for files that any
organization member can upload and every reader downloads, so an analyst's
inference is not enough.
Required resolution: the human confirms, yes or no, that MVP has no
extension filter and no virus scan of MUSTASK's own. Download-only access
with no inline rendering (US-7) is the mitigation already in the spec. If
the answer is yes, record it as a W-line and cite it next to BR-16.
Status: resolved
Resolution: the human confirmed (W-13, status.md § Decisions): no extension
filter and no virus scan in MVP. § Out of scope, BR-16 and US-7's preamble
now cite W-13.

### R-24
Severity: minor
Affects: § Decomposition (orchestrator note on W-6), OQ-6, OQ-7
Problem: the note gave the key scope to `mod-002` and `mod-001` ("both lines
pick up that scope"), while OQ-6 and OQ-7 name `doc-006` and `doc-007` as
owners too. Those two analysis lines are the ones that would carry an
unanswered question, and their queue summaries would not mention it.
Required resolution: name all four lines.
Status: resolved
Resolution: fixed in place. The note now says "all four lines pick up that
scope".

## Human-only questions for gate-1 — round 2

None of these blocks doc-002's criteria, which hold for any answer. OQ-6 and
OQ-7 may also be deferred to their owners (`doc-006`, `doc-007`) instead of
being answered now.

1. **OQ-6 — the project key.** Set by the organization administrator at
   project creation; 2..10 Latin capitals and digits, starting with a letter;
   unique within the organization, deleted projects included; immutable in
   MVP. Recommended: accept. If the key may change, BR-5's "never changes"
   has to go.
2. **OQ-7 — the type keys.** `EPIC` / `FEAT` / `US` / `TASK` / `BUG`, fixed
   in the seed. Recommended: accept. `STORY` instead of `US` is the one real
   alternative, clearer out of context.
3. **OQ-8 — whose Google Drive.** One instance-owned Drive (likely a Google
   Workspace shared drive), never shared to users, every download through
   MUSTASK's permission check. Recommended: accept, knowing that all
   organizations share one quota. Alternative: each uploader's own Drive.
4. **R-23 — no extension filter, no virus scan in MVP.** Recommended:
   confirm. It is an internal tool, attachments are download-only and never
   rendered inline; revisit with release 2 previews.

## Round 3 — requirements (delta)

A narrow check of the rework `ea1fa08..4f3bbec` against the human's answers
W-10..W-13 (`status.md` § Decisions). This session had no shell. The delta
was read from the files at `4f3bbec`: § Roles, BR-5, BR-16, BR-17, the BPMN
attach and purge branches, § Operation completeness, NFR, § Out of scope,
§ Decomposition (the `mod-004` row and the key notes), § Open questions,
US-4, US-7, US-10 and the new US-13. `spec.md` and every story were also
searched for leftovers of the round-2 recommendation (one instance-owned
Drive, a service account, a shared drive, one shared quota).

Verdict: **approved-by-ai**. The four new findings are fixed in place, and
none needs a decision from the human. No question is open.

### W-10..W-13 applied

| Answer | Where | Result |
|---|---|---|
| W-10 project key: admin sets at creation, 2..10 Latin capitals/digits starting with a letter, unique in the organization incl. deleted, immutable | BR-5, US-4 preamble and namesake scenario, § Decomposition key note, NFR localization | applied. R-20's scenario is no longer conditional and now cites W-10 |
| W-11 `EPIC`/`FEAT`/`US`/`TASK`/`BUG`, fixed in the seed | BR-5, US-4, key note | applied |
| W-12 Drive per organization | § Roles, BR-16, new BR-17, BPMN T0/T2/R0/R1, Operation completeness (new Drive-connection row, Attachment row), NFR, § Out of scope, `mod-004` row and note, US-7, US-10, US-13 | applied. No instance-wide Drive, service account, shared drive or shared quota is left. One consequence of the old model survived in US-13 (R-25) |
| W-13 no extension filter, no virus scan | BR-16, § Out of scope, US-7 preamble and the 1 GB "any extension" scenario | applied |

W-1..W-9 are not undone. The BR-8, state table, errata, § Fields, BR-4 and
US-12 text is unchanged by this rework, and BR-5 still carries W-6's scheme
and gap rule.

### The business-analyst's assumptions

1. **The organization administrator connects the Drive.** Accepted as a
   requirement. doc-001 § Roles gives `admin` every org-scoped code, and every
   other org-wide setting (membership, projects, areas) is the admin's. A
   member-level storage switch would contradict that model. The "assumed"
   hedge in § Roles and US-13 now cites doc-001 § Roles as the source.
2. **The setting lives in `mod-004`, not `mod-002`.** Accepted. This is a
   decomposition call, and the reason given holds: the Drive integration and
   its secrets serve only attachments. `mod-004` reaches `mod-002`'s
   organization settings through `mod-003`, and the D-1/D-9 code registration
   is split to match (§ Roles paragraph).
3. **Purge is refused while the item has attachments and the Drive is
   unreachable.** Accepted as a requirement. It is the direct reading of
   R-18's approved criterion ("a purge never ends with the item gone while its
   files remain in Drive"), and the item stays in «Удалена», recoverable, so
   doc-001 BR-6 holds. It is also the reversible choice: relaxing it later
   needs no data migration. It had one defect, a possible dead end, fixed as
   R-28.

`spec.md` is 263 lines, the same after this round's edits, against the ~250
budget. This is not a finding. Round 2 expected the excess to shrink once the
questions closed. It did not, because W-12 added real normative content
(BR-17 and the Drive-connection row) in place of the closed questions. No
story content leaked into the index.

### R-25
Severity: major
Affects: US-13, BR-16, US-7
Problem: US-13's scenario "Files are not shared to users inside Drive"
required that "only the access MUSTASK itself uses can reach" a file, and
that a removed member "loses access to the organization's files". Both were
written for the instance-owned Drive of the round-2 recommendation. Under
W-12 the Drive belongs to the organization. Its owner and its Google
Workspace administrators reach every file directly, and that owner may be the
organization administrator who just lost membership. MUSTASK can neither
prevent this nor test it, so the criterion could never pass, and it promised
a security property the product cannot deliver. The US-13 preamble ("holds
that organization's attachment files and nothing else") and US-7's scenario
title ("reachable only through MUSTASK's permissions") claimed the same
thing.
Required resolution: limit the promise to what MUSTASK controls. It adds no
share inside Drive, and every download it serves is permission-checked.
Direct access to the Drive is the organization's matter.
Status: resolved
Resolution: fixed in place. BR-16 now says that direct access to the Drive
is the organization's business, outside MUSTASK's control. US-13's preamble
says the same, and the scenario is renamed "MUSTASK shares no file inside
Drive". It now tests that MUSTASK adds no share, and that a removed member
can no longer download through MUSTASK. US-7's scenario title is now
"MUSTASK serves files only through its permissions".

### R-26
Severity: minor
Affects: US-13, § Operation completeness (Drive connection)
Problem: US-13's failed-connect scenario ended "with no Drive, or with the
Drive it had before", which implies replacing a connected Drive in place.
§ Operation completeness says Update is "no: disconnect and connect again".
The disconnect dialog also promised that downloads fail only "while no Drive
is connected", which implies that connecting any Drive brings them back. BR-17
leaves that recovery to `mod-004`'s ADR.
Required resolution: align the story with the Operation completeness row,
and make no promise about reconnection.
Status: resolved
Resolution: fixed in place. The failed-connect scenario starts from an
organization with no Drive and ends with no Drive. The preamble says that
one Drive is connected at a time and is never swapped in place. The
disconnect dialog now says downloads fail "once it is disconnected".

### R-27
Severity: minor
Affects: US-7
Problem: two scenarios overlap. "Existing attachments after the Drive is
disconnected or broken" (access stopped working → "unavailable, ask your
administrator") and "Google Drive is unavailable" (Drive "returns an error" →
"temporarily unavailable, retry later"). An authorization error from Drive
matches both, so a test cannot tell which message is required.
Required resolution: separate the two cases by their cause.
Status: resolved
Resolution: fixed in place. The "unavailable" scenario now covers only
valid access with Drive not responding or returning a server-side error.
Broken access belongs to the disconnected-or-broken scenario.

### R-28
Severity: minor
Affects: BR-17, US-7, US-10
Problem: the spec did not say whether an attachment can be removed while the
Drive is unreachable. Together with the purge refusal (assumption 3), an item
whose Drive is gone for good had no stated way to be purged. `mod-004` would
have to guess whether removal is refused or allowed, and the guess decides
whether a dead end exists.
Required resolution: state the removal behaviour and the exit.
Status: resolved
Resolution: fixed in place. BR-17 now says that removing an attachment never
waits on the Drive, and what becomes of the file stays `mod-004`'s call
(BR-13). US-7's disconnected scenario says the same. US-10's refused-purge
scenario gives the exit: restore, remove the attachments, delete, purge.
This is consistent with R-18's criterion, which covers the attachments an
item has when it is purged. It is also consistent with § Operation
completeness, which already left a removed attachment's file to `mod-004`.
If the human wants removal refused while the Drive is down too, this is the
fix to reverse. The purge would then stay blocked until the Drive returns.
