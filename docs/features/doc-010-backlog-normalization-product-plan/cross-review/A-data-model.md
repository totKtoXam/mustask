# Cross-review A — data model and ownership

Coverage: doc-010 status.md; AGENTS.md; stages/02-requirements-review.md; spec.md + status.md of doc-001..007; every story under doc-001..007/stories (70 files); doc-001 research/ado-boards.md; targeted: doc-006 review.md R-5/R-6, docs/backlog.md mod-001..012 lines.

### A-1
Severity: blocker
Affects: doc-002 § Types, levels and states, errata, US-1, US-3, US-10, US-11, W-2/W-7/W-9; doc-007 § MVP: the seeded process, US-3, US-5, P-1/P-2; doc-001 US-3, US-6, US-10; doc-003 BR-2/US-1 (column labels); STATE_DEFINITION
Problem: The seeded state names of all five types are defined twice, by two
human-approved decision sets of the same day, and disagree. M2 owns the
definition (doc-001 module map M2; doc-002 US-3:16 "M2's seed owns their
definition"), yet:
- Эпик/Фича/Пользовательская история, second InProgress state: doc-002
  spec.md:62 «Разрешена»→InProgress (W-9) vs doc-007 spec.md:123
  «Решена»→InProgress (P-1).
- Баг: doc-002 spec.md:65 «Новая»/«Активна»/«Разрешена»/«Закрыта» (W-7 "one
  label set for all five types ... whatever their gender") vs doc-007
  spec.md:124 «Новый»/«В работе»/«Исправлен»/«Закрыт» (P-2; spec.md:117
  "agreeing in gender with the masculine «Баг»").
- Задача: doc-002 spec.md:66 «Новая»/«Активна»/«Закрыта» (W-2, whose errata
  spec.md:73-77 supersedes doc-001 US-3) vs doc-007 spec.md:125
  «Новая»/«Выполняется»/«Готово». No P-decision covers doc-007's Задача row
  (P-1 names Эпик/Фича/История, P-2 names Баг) — it is the analyst's.

Stories inherit both versions. doc-002 US-1:35 puts a new «Баг» in «Новая»,
where the doc-007 seed gives «Новый». doc-007 US-3:21 moves «Готово» →
«Новая». doc-001 US-6:25 and US-10:17 use «Выполняется». mod-001 (first in the
queue) will seed doc-007's list; mod-003 will test doc-002's.

The choice has a cross-module consequence too:
- With one label set (W-7), a state filter by name spans all types (doc-002
  US-11:28 filters by «Активна»).
- With per-type names (P-2), the inline filter, doc-005's Статус clause and
  doc-003's column labels must work per (type, state).
Proposed resolution: Keep one state table, in doc-007 (the owner); doc-002 §
Types cites it instead of restating names. Amend the losing side's rows,
examples and errata, and doc-001 US-3/US-6/US-10 examples with them. Record
the superseded W-/P- decision in its task's status.md Log.
Decision: human — (1) Эпик/Фича/История, the resolved-but-in-progress state:
«Разрешена» (W-9) or «Решена» (P-1)? (2) Баг: the uniform label set (W-7:
«Новая»/«Активна»/«Разрешена»/«Закрыта») or its own gendered flow (P-2:
«Новый»/«В работе»/«Исправлен»/«Закрыт»)? Default for Задача, where no human
decision conflicts: W-2's «Новая»/«Активна»/«Закрыта»; doc-007's row is
amended.

### A-2
Severity: blocker
Affects: doc-001 § MVP boundary, M1 row, Operation completeness Team/Area, US-1, US-2; doc-006 TEAM, BR-M1-9, BR-M1-11, BR-M1-14, Operation completeness Team, US-5, US-6, US-7; doc-003 BR-13, US-2, US-3; doc-004 E-5/E-13, ER, US-2, US-12, US-13; doc-005 BR-6, BR-10; TEAM, AREA, PROJECT
Problem: TEAM cardinality per project is modelled two contradictory ways.
E-5 (several teams in MVP, kept by E-13) opened model questions that no spec
answers.
- One team: doc-001 spec.md:238 "exactly one default team per project";
  doc-006 spec.md:177 "PROJECT ||--o{ TEAM : has, exactly one in MVP", and
  BR-M1-9 (spec.md:212) "no action to create a second team". doc-003 BR-13
  (spec.md:69) "MVP ships exactly one team per project"; its US-2:64-65 and
  US-3:58 are built on it.
- Several teams: doc-004 spec.md:82 "PROJECT ||--o{ TEAM : has, several in
  MVP (M1, E-5)"; US-2:17 and US-12:44-48 need teams A and B.

doc-010 D-2 carries back only the doc-006 side (BR-M1-9/11, the Team row, the
M1 decomposition). It leaves out doc-001 (MVP table, Operation completeness,
US-1/US-2 "default team") and doc-003 (BR-13 and its "single-team-reachable
variants" in US-2/US-3).

E-5 also leaves five modelling gaps no spec closes:
- (a) Root-area owner: BR-M1-11 says "always owned by the project's team".
  Which team, once there are several? Can it change?
- (b) Default team: doc-001 says "default team", doc-006 "the project's
  team", doc-004 names none.
- (c) Default area of a new work item: doc-006 BR-M1-14 (spec.md:217) makes
  the root the default for everyone; a per-team default is "release 2". With
  several teams, an item created on team B's backlog gets Область = root. The
  root is owned by team A, so the item lands on A's backlog, not B's
  (doc-003 BR-5/BR-6).
- (d) Team lifecycle: the codes cover only "rename, membership" (doc-006
  spec.md:77), and TEAM names no uniqueness rule (spec.md:168 "one per
  project in MVP"). Team deletion "stays doc-006's to decide" (doc-004
  spec.md:202), while doc-005 BR-10 dates it "release 2+" and doc-005 BR-6
  already handles a deleted bound team.
- (e) Effects of a team deletion: on the areas it owns, its boards and
  BACKLOG_ENTRY rows, and its iteration selection and default.
Proposed resolution: Apply one amendment set:
- doc-001: MVP table, M1 row, Operation completeness Team, US-1/US-2.
- doc-006: TEAM {name, unique in project}; a create-team operation and its
  code; BR-M1-9 rewritten; BR-M1-11 and BR-M1-14 per the answers below;
  US-5/US-6/US-7.
- doc-003: BR-13 replaced by two-team rules; US-2/US-3 scenarios rewritten
  with a second team.
- doc-005: BR-10 aligned with the team-deletion answer.
Decision: human —
- (1) Team deletion in MVP: no (default — create and rename only, deletion
  in release 2), or yes, with owned areas reassigned first?
- (2) Default team: the team created with the project is the default team
  and owns the root area for good (default), or the root owner can be
  reassigned to any team of the project?
- (3) Per-team default area in MVP (A-4 also depends on this):
  - yes — each team has a default area among the areas it owns, used when an
    item is created from that team's surfaces (recommended: otherwise items
    do not land on the creating team's backlog);
  - no — items created anywhere land on the default team.

### A-3
Severity: blocker
Affects: doc-002 BR-5, US-4, Decomposition ("Two keys are required of upstream"); doc-006 PROJECT, BR-M1-1, US-5, Decomposition; doc-007 WORK_ITEM_TYPE, BR-M2-9, US-1, US-2; doc-001 ER PROJECT, glossary; doc-005 US-1, US-4; W-10, W-11; queue lines mod-001, mod-002
Problem: The work item identifier `{ProjectKey}-{WorkItemKey}-{Sequence}`
(doc-002 BR-5, spec.md:145) rests on two attributes that neither owning spec
models.
- Project key: doc-002 spec.md:245-248 requires it of mod-002/doc-006 — "the
  organization administrator sets the project key at project creation".
  doc-006 has no key anywhere. PROJECT is {name, status} (spec.md:163-166),
  and US-5:17-24 creates a project from a name only. doc-001's ER
  (spec.md:158-161) likewise has no key.
- Type key: doc-002 requires `EPIC`/`FEAT`/`US`/`TASK`/`BUG` "fixed in the
  seed" (W-11). doc-007's model has WORK_ITEM_TYPE {icon, colour} only
  (spec.md:51-54), and BR-M2-9 gives a stable identity but no key.

doc-002 spec.md:249 says "All four lines carry it". Neither the merged
doc-006/doc-007 nor the queue lines (docs/backlog.md:16-17) do. mod-001 and
mod-002 ship before mod-003. Built as specified, they leave every existing
project and type without its key, which means a later migration with
invented values.

Two related identity gaps:
- Organization-qualified addresses: project keys are unique only within an
  organization. doc-002 US-4:78-89 needs an address that "leads to one item,
  never to a namesake in another organization". ORGANIZATION has no stable
  key, only a renamable 1-100-character name (doc-006 BR-M1-1).
- The identifier as a field: doc-005 US-4 (lines 33-35, 47-50) imports by
  "existing work item id", and US-1's field set (lines 20-29) has no
  identifier field. Yet the identifier is the first column of doc-002 US-11
  and the first card field (doc-003 BR-14).
Proposed resolution:
- doc-006: PROJECT.key {2..10 Latin capitals/digits, starts with a letter,
  unique in the organization incl. deactivated, immutable, set by the org
  admin at creation}. Add it to the ER, BR-M1-1, US-5 (valid, duplicate, bad
  format, unchanged by rename), Operation completeness and the mod-002 row.
- doc-007: WORK_ITEM_TYPE {name, key}, keys fixed by the seed and shown on the
  read-only definition (US-2); add to the mod-001 row.
- doc-001: PROJECT {key}; glossary terms «Идентификатор» and «Ключ проекта».
- doc-005: «ID» (the identifier) joins the release-2 field set and the
  default columns. CSV import matches the full identifier within the target
  project.
- doc-002 US-4: an address carries the organization's internal id; the
  identifier resolves inside that organization.
Decision: default — apply W-10/W-11 to their owners as above; addresses carry
an internal organization id, with no user-visible organization key. The human
decides only if a readable organization slug in addresses is wanted.

### A-4
Severity: major
Affects: doc-001 BR-3, US-2, US-8; doc-003 BR-5, BR-13, BACKLOG_ENTRY, Operation completeness BacklogEntry, US-2; doc-004 BR-7, BPMN, US-3, US-13; doc-006 BR-M1-11, US-7, H-9; AREA, TEAM
Problem: The specs disagree on whether an area can resolve to no owning team.

doc-006 BR-M1-11 (spec.md:214, human-approved H-9) says the root "is always
owned by the project's team — its owner cannot be changed or cleared. So every
area resolves to an owning team".

Three specs build behaviour on the opposite:
- doc-001: US-2:45-46 "if no ancestor up to the root has an owner, the answer
  is undefined"; US-8:37-41.
- doc-003:
  - BR-5 (spec.md:61) "a work item whose Area has no owning team has no
    BACKLOG_ENTRY";
  - BR-13 (spec.md:69) uses "an Area's ownership toggling between ... and
    'unowned'" as its MVP variant;
  - BACKLOG_ENTRY (spec.md:89) "an Area becoming unowned removes the entry";
  - US-2:62-67 and US-2:87-92.
- doc-004:
  - BR-7 (spec.md:139) «У области элемента нет команды-владельца»;
  - BPMN spec.md:107;
  - US-3:55-61;
  - US-13:53-54 "including items with no owning team".

None of those scenarios has a reachable Given. doc-006 review R-6 found the
same dead branch in doc-006 US-7 and fixed it only there.
Proposed resolution:
- doc-001 BR-3 gains "every area resolves to exactly one owning team (root
  always owned, doc-006 BR-M1-11)".
- The unowned branches become re-owning branches:
  - doc-003: delete BR-5's second clause; rewrite BR-13's variant and US-2's
    two scenarios as "Area re-pointed to a node owned by another team"
    (reachable after E-5, A-2).
  - doc-004: delete BR-7's second refusal, the BPMN branch, the US-3
    scenario and the US-13 clause.
  - doc-001: rewrite the US-2 and US-8 scenarios.
- If A-2 admits team deletion, deletion first reassigns the team's areas, so
  the invariant holds.
Decision: default — keep BR-M1-11 (H-9) and amend the others as above.

### A-5
Severity: major
Affects: doc-002 (whole spec; US-2, US-5, US-6, US-7, US-8, US-9, US-10, US-11, US-13); doc-001 decomposition row doc-002 and ER; WORK_ITEM, COMMENT, ATTACHMENT, TAG, HISTORY_REVISION, WORK_ITEM_LINK, Drive connection
Problem: doc-001's decomposition (spec.md:344) asked doc-002 for
"WORK_ITEM/WORK_ITEM_LINK/COMMENT/ATTACHMENT/TAG/HISTORY_REVISION entities".
doc-002 has a field table and Operation completeness, but no logical data
model, unlike doc-003..007. So attributes that its stories and other modules
use have no owner:
- WorkItem:
  - the per-project sequence number (BR-5);
  - created by/at;
  - changed at: US-11:22-23 sorts "most recently changed first", and doc-005
    US-1:40 adds «Дата изменения» as "the time of the work item's latest
    history revision";
  - the state held before deletion, which Restore proposes (US-10:46-49) —
    stored, or read from history?;
  - the concurrency revision (BR-15).
- Comment: author, posted at, an edited flag with its time (US-6:44-45
  "marked as edited with the time of the edit"), and a deleted marker versus a
  removed row (US-6:57-59).
- Attachment: file name, size, uploader and upload time (US-7:33-34), and the
  Drive file reference. The uploader drives "removed only by whoever uploaded
  it" (spec.md:26).
- Tag: a stored project entity, or derived from use? US-8:36 keeps "its
  original spelling «Аудит»", but US-8:59-62 says an unused tag "stops being
  offered". Whether a later «аудит» brings back «Аудит» cannot be decided.
- HistoryRevision: its structure is unstated. It must hold one revision per
  save with per-field old/new values (US-2:34-35); entries on both items for
  a link add/remove (US-5:31); comment, attachment and delete/restore entries;
  and M5's pointer writes. mod-003 owes the history ADR and has no model to
  design against.
- Drive connection (spec.md:169): organization 1:0..1, connected and
  disconnected by/at, a credential reference.

doc-001's ER also has no USER edge for a comment's author, an attachment's
uploader or a revision's author.
Proposed resolution: Add a "Logical data model (M3)" section to doc-002 at
doc-003's depth: an ER plus an entity table (key attributes, stored vs
derived, uniqueness) covering the attributes above.
- Tag is a stored project entity; its canonical spelling is the first
  spelling, kept while unused.
- The restore default is derived from history, or stored — the spec says
  which.
- Created and changed are derived from revisions.
- USER edges for author and uploader.
Reflect the model in doc-001's ER (A-10).
Decision: default.

### A-6
Severity: major
Affects: doc-001 BR-6, US-1, Operation completeness; doc-006 BR-M1-1, Operation completeness (Membership, Area), US-2, US-5; doc-004 ITERATION.isActive, BR-10; doc-005 BR-9, BR-10, data-model note; doc-002 BR-12, BR-13; ORGANIZATION, PROJECT, AREA, ORG_MEMBERSHIP, ITERATION, COMMENT, ATTACHMENT, QUERY, QUERY_FOLDER, DASHBOARD, DASHBOARD_WIDGET
Problem: doc-001 BR-6 (spec.md:279) says "Deletion is always soft, never a
hard delete ... only an administrator purges a deleted object permanently, by
hand". The modules give each entity its own lifecycle, and no spec lists which
applies where.
- Purge:
  - doc-001 US-1:80-82 — a project "stays recoverable ... until an
    administrator purges it by hand";
  - doc-006 BR-M1-1 (spec.md:204) — "MVP has no purge" for organizations and
    projects;
  - doc-004 BR-10 (spec.md:142) — "an administrator purges by hand", yet
    there is no purge operation (Operation completeness, spec.md:155: "n/a").
- Hard deletes that break "always soft":
  - an Area leaf (doc-006 spec.md:231 "re-create it");
  - ORG_MEMBERSHIP removal (spec.md:227 "re-add instead");
  - comment text (doc-002 BR-12 "text disappears");
  - attachment removal (BR-13 "not undoable").
- One status, two causes: doc-005 spec.md:100-102 gives M6 objects "the same
  soft-delete status as PROJECT.status", and BR-10 deactivates them with
  their project, "restored together with it". A query its owner deleted
  before the project was deactivated comes back when the project is restored.
- Five names for one concept:
  - "archives (soft-deletes)" (doc-001 US-1:77);
  - "deactivated" (doc-006);
  - "deleted status" (doc-005 BR-9);
  - `isActive`/"removed" (doc-004);
  - the state «Удалена» (M3).
Proposed resolution: Rewrite doc-001 BR-6 as a principle plus a per-entity
table:
- Soft, with restore:
  - WorkItem via «Удалена», admin purge;
  - Organization/Project status active/deactivated, admin restore, no purge
    in MVP;
  - Query/Folder/Dashboard/Widget status deleted, restored by the class that
    may delete it, admin purge.
- Soft, without restore: Iteration, no purge in MVP.
- Removed outright, with a history or who/when trace: ORG_MEMBERSHIP, team
  membership, Area leaf, WorkItemLink, comment text, attachment link, tag on
  an item.

doc-001 US-1 drops "until an administrator purges it". doc-005 separates an
object's own deleted status from the status inherited from its project. The
glossary gets one Russian term per status.
Decision: default — the table above, which matches what each module already
specifies.

### A-7
Severity: major
Affects: doc-002 BR-8, US-5, Operation completeness WorkItemLink; doc-003 BR-10, US-3, Operation completeness BacklogEntry; WORK_ITEM_LINK
Problem: The Parent/Child link has two incompatible lifecycles for "give an
item a different parent".
- doc-002, the owner: BR-8 (spec.md:148) allows "at most one parent".
  US-5:33-39 refuses a different parent — "re-parenting is removing the old
  link and adding the new one" — and a link is never updated in place
  (spec.md:164).
- doc-003: BR-10 (spec.md:66) says the mapping action "creates or replaces
  the same Parent/Child link", and US-3:32-38 "Assigning a different parent
  replaces the existing link".

feat-004 writes "through M3's Parent/Child link" (doc-003 Roles), so it cannot
satisfy both. Nor does either spec say what history records for a replace.
doc-003's mapping offers only parents "one level up" (US-3:24, :34). That
narrows doc-002's "higher or equal" (W-1), and neither spec states it as M4's
own rule.
Proposed resolution: M3 gains an atomic "change parent" operation: remove and
add in one write, validated under BR-8, recorded as a link removal and a link
addition in every affected item's history. doc-002 US-5's "at most one parent"
scenario offers it from the form as well. doc-003 BR-10 cites it and states
the mapping's one-level-up offer as its own UI narrowing.
Decision: default — an atomic replace on both surfaces. The alternative: M4
also refuses and requires clear-then-assign.

### A-8
Severity: major
Affects: doc-006 BR-M1-13, NFR Concurrency, US-7; doc-004 BR-10, US-14; doc-002 BR-14, US-10; doc-003 BR-16, BR-17; AREA, ITERATION, WORK_ITEM («Удалена»)
Problem: No spec defines how M1/M5 structure changes treat work items in
«Удалена».
- Area deletion:
  - doc-006 BR-M1-13 (spec.md:216) deletes "a leaf with no work items in
    it", and does not say whether an item in «Удалена» counts.
  - If it does not count, the leaf goes. Restoring the deleted item — which
    "brings the item back exactly as it was" (doc-002 US-10:29), including
    its backlog position (doc-003 BR-17) — then points at a missing area,
    and purge is the only way out.
- Iteration removal:
  - doc-004 BR-10 (spec.md:142) clears "every pointer to it ... each clearing
    in that work item's history". «Удалена» items are included, whereas BR-12
    and BR-13 exclude them.
  - That is a write to an item doc-002 BR-14 (spec.md:154) makes read-only
    ("not edited ... while it is in «Удалена»").
  - US-14's confirmation count (lines 17-20) is silent on deleted items.
Proposed resolution:
- doc-006 BR-M1-13: "work items in it" includes items in «Удалена». The area
  is held until they are restored and moved, or purged; the refusal message
  and US-7 say so.
- doc-002 BR-14: "read-only for users; system reference clean-up (iteration
  removal, M5) still applies and is recorded in history".
- doc-004 BR-10/US-14: deleted items' pointers are cleared too and counted in
  the confirmation.
Decision: default.

### A-9
Severity: major
Affects: doc-004 E-8, BR-15, BR-16, US-7, US-8, US-9, Decomposition; doc-002 § Out of scope, Decomposition; doc-007 FIELD_DEFINITION; queue mod-011, mod-012
Problem: Release-2 planning rests on fields no spec owns.
- doc-004 assigns them to doc-002:
  - E-8 (status.md:55) gives estimation fields to doc-002;
  - BR-16 (spec.md:148) "read[s] estimation fields owned by doc-002";
  - the decomposition (spec.md:236-238) depends on "doc-002's
    estimation-fields slice".
- doc-002 does not own them: spec.md:199-201 lists them only as release-2 out
  of scope (Story Points, Effort, Activity, Original/Remaining/Completed Work),
  and its decomposition has no such row.
- The queue records the hole: docs/backlog.md:38, :40 "not yet queued".

Capacity (BR-15, US-7:18) also needs the Activity value list, which has no
owner at all — an M0 dictionary, the M2 seed, or an M3 constant.
Proposed resolution:
- doc-002 gains a release-2 section and a row
  `feat-NNN-work-item-estimation-fields`: the fields, the types carrying them,
  their rules and where they appear. The Activity list gets an owner.
- mod-011/mod-012 depend on that row.
- doc-007's FIELD_DEFINITION note ("MVP: fixed system fields owned by
  doc-002") gains "release 2 adds the estimation fields, still M3-fixed".
Decision: human — which estimation fields ship in release 2?
- the ADO Agile set: Story Points on Пользовательская история and Баг;
  Original Estimate, Remaining Work, Completed Work and Activity on Задача
  and Баг (recommended);
- Story Points only;
- Remaining Work only.

Default for the Activity list: a fixed M3 list with Russian labels, like
Приоритет (doc-001 NFR: the dictionary pattern covers only M0-owned lists).

### A-10
Severity: major
Affects: doc-001 § Cross-module logical data model, ownership table, ER; doc-002 Operation completeness (Drive connection), Decomposition; doc-003 Data model; doc-004 § Amendments, Logical data model; doc-005 Logical data model; doc-006 Entities and invariants; doc-007 Logical model
Problem: doc-001's ER and ownership table are the cross-module reference, but
the module refinements never flowed back. doc-004 spec.md:197-198 says as much:
"doc-001 is not edited here ... this table is the record".

Every doc-001 ER edge was checked against the module that owns its endpoints:
- Consistent: DICTIONARY–ENTRY; ORGANIZATION–PROJECT/PROCESS/ORG_MEMBERSHIP;
  USER–ORG_MEMBERSHIP; PROJECT–AREA; AREA–AREA; both TEAM–ITERATION edges;
  PROCESS–TYPE; TYPE–STATE; TYPE–LEVEL; PROJECT/TYPE/STATE–WORK_ITEM;
  WORK_ITEM–AREA/ITERATION/USER/LINK/COMMENT/ATTACHMENT/TAG/HISTORY;
  PROJECT–TAG; TEAM/LEVEL–BOARD; BOARD–COLUMN/SWIMLANE;
  TEAM/LEVEL/WORK_ITEM–BACKLOG_ENTRY.
- Divergent, covered by other findings: PROJECT–TEAM (A-2), TEAM–AREA
  resolution (A-4), the meaning of USER–TEAM (A-16).
- Divergent, covered only here:
  - ITERATION {name, startDate, finishDate} (spec.md:167-171) vs doc-004's
    kind, number, optional name, isActive and ITERATION–ITERATION parent
    (doc-004 spec.md:69-78).
  - `BOARD_COLUMN }o--|| STATE_DEFINITION` (spec.md:209) vs doc-003
    spec.md:87: the column stores (board, category), and the per-type state is
    derived.
  - `PROJECT }o--|| PROCESS` (spec.md:181), drawn as stored with the
    invariant "must be its own organization's". doc-006's ER drops it. With
    one process per organization forever (doc-007 P-4), it is derived through
    ORGANIZATION.

Entities absent from the ownership table (spec.md:218-226):
- QueryFolder (doc-005 spec.md:84-88);
- Dashboard's team/project levels (D-15);
- the Drive connection (doc-002 spec.md:169). M3 owns it, but it lives in M1's
  organization settings. doc-002 spec.md:254-255 claims "doc-006 only notes
  that it will exist"; doc-006 never mentions it.
- release-3 FieldDefinition/FormLayout/TransitionRule (doc-007
  spec.md:59-61);
- release-2/3 TeamCapacity/DayOff/TeamWorkingDays/IterationSnapshot/
  DeliveryPlan/DeliveryPlanTeam (doc-004 spec.md:89-91);
- team membership, which doc-006 treats as an entity of its own (Operation
  completeness).
Proposed resolution:
- Amend doc-001's ER to these shapes, with PROJECT–PROCESS drawn as derived.
  The ER also takes A-3's keys and A-5's USER edges.
- Amend the ownership table: every entity above, with its release.
- doc-006: M1's organization settings name the Drive-connection section that
  mod-004 adds.
Decision: default.

### A-11
Severity: minor
Affects: doc-007 Logical model, BR-M2-2, BR-M2-4; doc-002 BR-8, US-5; doc-003 BR-1, BR-2, BR-11; BACKLOG_LEVEL, STATE_DEFINITION, WORK_ITEM_TYPE
Problem: doc-007's ER shows attributes only for WORK_ITEM_TYPE {icon, colour}
(spec.md:51-54). Other modules compute with M2 attributes that M2 never
models:
- A total order of backlog levels:
  - doc-002 BR-8 "the parent's backlog level is higher than or equal to";
  - doc-002 US-5:13 "Эпики → Фичи → Истории → Задачи";
  - doc-003 BR-11 "exactly one level below";
  - ADO ranks the levels 40/30/20/10 (research :25).
- A level kind that says which levels get a board (doc-003 BR-1). It matters
  once doc-007's release-3 custom portfolio levels exist (spec.md:183-187).
- State order and default: given only as a table, yet doc-003 BR-2 relies on
  "M2's seed order".
Proposed resolution: doc-007's model gains:
- BACKLOG_LEVEL {name, rank, kind: portfolio|requirement|task};
- STATE_DEFINITION {name, category, order};
- WORK_ITEM_TYPE {name, key (A-3), icon, colour}.

BR-M2-4 adds: "levels are totally ordered by rank; M3 BR-8 and M4 BR-11 read
the rank; M4 BR-1 reads the kind".
Decision: default.

### A-12
Severity: minor
Affects: doc-001 glossary; doc-003 BR-2, US-1; doc-007 US-2; state categories
Problem: Board columns are per category (doc-003 BR-2), and their wording "is
owned by M2/doc-007, not invented here" (spec.md:58). doc-007 defines no
column or category wording, and its read-only screen shows "each state's
category" (US-2:18-20) with no Russian name. doc-001's glossary has only
«Статус / категория статуса» (spec.md:139).

A column cannot borrow a state name: one level mixes types whose state in that
category differs. Under P-2 that is the case A-1 describes; under W-7 it is
doc-002 US-3:20-26, with two «Разрешена» cards in two columns.
Proposed resolution: doc-007 owns Russian labels for the five categories. M4
uses them as board column headers, M2 on the process-definition screen, and
doc-001's glossary lists them. Proposal: «Новые», «В работе», «Решённые»,
«Завершённые», «Удалённые» (Removed is never shown as a column).
Decision: default — the wording can be changed at gate-1.

### A-13
Severity: minor
Affects: doc-006 BR-M1-17, H-8, USER.status; doc-002 § Fields Исполнитель, W-3, US-2, US-11; ORG_MEMBERSHIP, USER
Problem: A user's standing towards an organization has three states, split
across two specs:
- member;
- member with a disabled account, marked «неактивен» (doc-006 BR-M1-17);
- former member, whose membership was removed (doc-002 W-3).

doc-006 BR-M1-17 (spec.md:219) hands over one question: "whether it remains an
assignee candidate is for doc-002-work-items to state". doc-002 never answers
it. Its "candidates = every current member" (spec.md:90) would include
disabled accounts, and neither its spec nor its stories mention «неактивен».
W-3's "former member" marking has no Russian label (US-2:49-50, US-11:43). A
user who is both removed and disabled would carry two competing markings.
Proposed resolution: In doc-002 § Fields, Исполнитель:
- an inactive account is not offered as a candidate (as H-8 rules for the
  member search and the team picker);
- an inactive account already assigned stays, shown «неактивен»;
- the former-member marking gets a label (proposal: «не в организации»), and
  it prevails over «неактивен».

US-2/US-11 gain the scenario.
Decision: default.

### A-14
Severity: minor
Affects: doc-007 § MVP: the seeded process, FIELD_DEFINITION, US-2; doc-005 US-1 field set, BR-12; doc-001 US-4, M3 MVP row; doc-002 § Fields
Problem: The MVP field set is restated in several specs, and each copy
differs.
- doc-007 spec.md:137-139 lists "every type's one fixed layout
  (Заголовок/Описание/Статус/Исполнитель/Область/Приоритет/Теги, owned by
  doc-002/M3)". It misses Итерация (MVP, mod-007) and «Критерии
  приёмки»/«Шаги воспроизведения» (W-5, feat-007). Which type carries which
  field is therefore an M3 constant that the process-definition screen
  (US-2) does not show.
- doc-005 US-1:14-18 grounds its field set on doc-001 because "doc-002 has not
  shipped", which is now stale.
  - It has no identifier field.
  - Its labels differ from doc-002's: «Тег» (US-1:29) vs «Теги» (doc-002
    spec.md:93); «Тип рабочего элемента» (US-1:23) vs «Тип» (spec.md:86).
- doc-005 BR-12's default columns leave out the identifier, which leads doc-002
  US-11 and doc-003 BR-14.
Proposed resolution: doc-002 § Fields is the single catalogue, with field
names equal to UI labels.
- doc-007 cites it and states that type-specific fields stay M3-fixed until
  release-3 FIELD_DEFINITION.
- doc-005 US-1 re-grounds on doc-002: labels «Тип»/«Теги», and the identifier
  («ID») in the field set and the default columns.
Decision: default.

### A-15
Severity: minor
Affects: doc-005 US-1 query language, BR-5; doc-002 US-11; doc-004 BR-9; doc-001 release-2 plan; Область and Итерация as query fields
Problem: The two tree-valued fields compare differently on the two filter
surfaces.
- The inline filter includes descendants: Область (doc-002 US-11:47-51) and
  Итерация (doc-004 BR-9, spec.md:141).
- The release-2 query offers Область "=, In" and Итерация "=" only (doc-005
  US-1:26-27). "Under" arrives in release 3, for Область alone (US-1:39), and
  never for Итерация.

doc-001's release-2 plan (spec.md:249-250) says inline filters "graduate into
personal saved queries", but a saved query cannot reproduce the filter it
graduates from.
Proposed resolution: In doc-005, release 2 offers "Under" on Область and
Итерация, with the inline filter's subtree semantics; "=" stays an exact
match.
Decision: default.

### A-16
Severity: minor
Affects: doc-006 § Roles (last paragraph), US-6; doc-003 Roles; doc-001 BR-3; USER–TEAM (team membership)
Problem: Specs give the USER–TEAM relation different meanings. This overlaps
dimension B.
- doc-006 spec.md:83-84: team membership "decides which board and backlog a
  user sees (BR-3)". US-6:9-10 adds "so that the team's boards ... show the
  right people's work".
- doc-003 Roles (spec.md:26) lets any reader "open any of their
  organization's team boards and backlogs".
- doc-001 BR-3 routes items by Area, not by people.

Taken at their word, the specs give team membership no MVP reader. Its
consumers are release 2/3: capacity (doc-004 US-7) and "In Group" (doc-005
US-1:38).
Proposed resolution: doc-006 states that in MVP team membership drives no
visibility and no routing. It is used only for display, and for a "my teams"
default navigation if that is wanted. doc-006 names its release-2/3 consumers
and rewords US-6's "so that".
Decision: default.

### A-17
Severity: minor
Affects: doc-003 Operation completeness Board/BacklogEntry, BR-6; doc-005 mod-NNN-dashboards-widgets; BOARD, BOARD_COLUMN, SWIMLANE, BACKLOG_ENTRY
Problem: Two M4 records are created by events that happen before M4 exists.
- doc-003 seeds a team's boards "when the team itself is created (M1,
  doc-006)" (spec.md:95).
- It creates a BACKLOG_ENTRY when an item "becomes visible" (spec.md:98).

In the module order, mod-002 creates teams and mod-003 creates items before
mod-006 ships (mod-006 deps: mod-003, mod-005). M1 cannot call M4, which
depends on it. doc-003 does not say:
- (a) what triggers seeding for teams created after M4 ships;
- (b) how teams that predate mod-006 get their boards;
- (c) what initial stack rank items that predate mod-006 get.

doc-005 answers (b) for dashboards ("for new teams and for teams existing
when it ships", spec.md:229); doc-003 does not.
Proposed resolution: In doc-003, M4 creates boards and entries itself —
reacting to team or item creation, or on first read (a design choice). When
M4 ships:
- every existing team gets its boards;
- every existing item gets an entry, ordered by identifier number ascending
  (creation order);
- deleted items get hidden entries (BR-16).
Decision: default.

### A-18
Severity: minor
Affects: doc-004 release-3 entities, BR-17, US-10; DeliveryPlan
Problem: DeliveryPlan stores "which teams and levels a timeline shows"
(spec.md:91), but its owning scope is not given. US-10:44-48 lets a plan hold
"teams of projects inside one organization", so a plan spans projects, while
every other M5 entity is project-owned. Nor do the specs say who may create,
edit or read a plan; ADO has plan permissions (research :89).
Proposed resolution: doc-004 states whether DeliveryPlan belongs to an
organization (cross-project) or to a project, in which case US-10 is limited
to one project's teams. Add its Operation completeness row.
Decision: default — organization-owned; member and admin write, every role
reads.

### A-19
Severity: minor
Affects: doc-003 US-3; doc-004 US-5, US-12; doc-001 glossary; doc-004 § Terms; doc-005 § Glossary
Problem: D-12 fixes the Russian type and level names, and some stories use
other names:
- doc-003 US-3:17 "a Feature with linked Stories"; US-3:56-60 "a Story linked
  directly to an Epic, skipping Features ... that Epic's row";
- doc-004 US-12:33 "an unfinished team A История" (a short form not in the
  glossary); US-5:21 "the requirement level".

doc-001's glossary keeps «Порядок бэклога («Порядок»)» (spec.md:145), which no
module uses. It lacks the terms the modules added: doc-004 § Terms, doc-005 §
Glossary, identifier and keys (A-3), category labels (A-12).
Proposed resolution: Stories use «Эпик»/«Фича»/«Пользовательская
история»/«Задача»/«Баг» and «Эпики»/«Фичи»/«Истории»/«Задачи» in Gherkin.
doc-001's glossary absorbs the module glossaries and the new terms. Either the
glossary drops «Порядок», or doc-003 uses it for the order term.
Decision: default.

## Summary

Blocker 3 (A-1..A-3) · major 7 (A-4..A-10) · minor 9 (A-11..A-19) · total 19.
Human decisions: A-1 (two), A-2 (three), A-9 (one); everything else has a
default.

Most consequential:
1. A-1 — doc-002 and doc-007 seed different state names for all five types,
   backed by conflicting human decisions (W-2/W-7/W-9 vs P-1/P-2). mod-001,
   first in the queue, cannot start.
2. A-2 — "several teams in MVP" (E-5) is carried back only into doc-006.
   doc-001/doc-003 still model one team, and the root-area owner, the default
   team, the per-team default area and team deletion are undefined.
3. A-3 — the identifier's project key and type keys (W-10/W-11) are missing
   from their owners doc-006/doc-007 and from the mod-001/mod-002 lines, which
   ship before mod-003.
