# Cross-review D — behaviour, rules, states and seams

Coverage: doc-010 status.md; stages/02-requirements-review.md; AGENTS.md; doc-001 spec.md, status.md, stories US-1..US-10, research/ado-boards.md; doc-002 spec.md, status.md, stories US-1..US-13; doc-003 spec.md, status.md, stories US-1..US-3; doc-004 spec.md, status.md, stories US-1..US-14; doc-005 spec.md, status.md, stories US-1..US-5; doc-006 spec.md, status.md, stories US-1..US-7; doc-007 spec.md, status.md, stories US-1..US-5; docs/backlog.md (queue lines mod-001..mod-013, feat-001..feat-012).

Finding ids D-n belong to this file; the umbrella's human decisions are cited as "doc-001 D-n", and each module's decisions by their own ids (W-, DD-, E-, D-13..16, H-, P-).

### D-1
Severity: blocker
Affects: doc-001 US-3 sc.2-3, US-4 sc.1, US-6 sc.2, US-10 sc.1; doc-002 § Types, levels and states (table and Errata), US-1 sc.2-3, US-3 (all), US-10 sc.5, US-11 sc.2; doc-007 § MVP: the seeded process (state table), US-3 sc.1, US-5 sc.1; doc-003 BR-2 (drag target = first state in the category); decisions doc-002 W-2/W-7/W-9 vs doc-007 P-1/P-2
Problem: the five types have three incompatible state-name lists. Categories and order agree, names do not.
- Эпик/Фича/Пользовательская история, InProgress state #3: doc-002 spec:62 «Разрешена» (W-9) vs doc-007 spec:123 «Решена» (P-1).
- Баг: doc-002 spec:65 «Новая»/«Активна»/«Разрешена»/«Закрыта» (W-7: "one set of state labels for all five types, regardless of grammatical gender") vs doc-007 spec:124 «Новый»/«В работе»/«Исправлен»/«Закрыт» (P-2: "agreeing in gender with the masculine «Баг»").
- Задача: doc-002 spec:66 «Новая»/«Активна»/«Закрыта» (W-2, with an errata that supersedes doc-001 US-3) vs doc-007 spec:125 «Новая»/«Выполняется»/«Готово», which is doc-001 US-3:28-29 again. No P-decision backs doc-007's Задача names. Its note "mirrors ADO's Task" is about skipping Resolved, and the names themselves are not Agile ones (research: Agile Task = New/Active/Closed/Removed).
M2 owns the definition (doc-002 § Types: "M2's seed (`doc-007`, `mod-001-process-seed`, which own the definition)"). So mod-001 would seed doc-007's names while mod-003's acceptance tests (doc-002 US-1/US-3/US-10/US-11) assert doc-002's. The target of a Баг drag into the Истории InProgress column is «Активна» or «В работе» depending on which spec is read. doc-007 US-5 uses a Story transition («Активна»→«Закрыта») with the bug reason «Исправлено». Two human decisions contradict each other (W-7 vs P-2, W-9 vs P-1 on spelling), so a reviewer cannot pick one.
Proposed resolution: keep one table, in doc-007 (the owner), laid out as § State tables below. doc-002 § Types, levels and states replaces its table with a reference plus the Resolved-on-Баг consequence, and its Errata paragraph is deleted. The examples in doc-001 US-3/US-4/US-6/US-10, doc-002 US-1/US-3/US-10/US-11 and doc-007 US-3/US-5 are amended to the chosen names. Under every option Задача takes W-2's «Новая»/«Активна»/«Закрыта»/«Удалена», because W-2 is the only explicit human decision on Задача.
Decision: human — which label set wins for Эпик/Фича/История/Баг:
- (A) doc-002's uniform set (W-7/W-9). Pro: one label per state across types, no gender forms. Con: «Разрешена» is InProgress on История and Resolved on Баг, so the Истории board shows two cards with the same label in two columns (doc-002 US-3 itself flags this), and a query "Статус = Разрешена" mixes categories. Supersedes P-1/P-2.
- (B, recommended) doc-007's P-1/P-2 for Эпик/Фича/История/Баг, plus W-2 for Задача. Pro: no label maps to two categories, it is the owning module's decision, and the bug flow reads idiomatically. Con: gendered labels, and the list's state filter shows more distinct names. Supersedes W-7 and W-9's spelling.
- (C) doc-007 verbatim, including Задача «Выполняется»/«Готово». Not recommended: it overrides the explicit W-2 with no human decision behind it.

### D-2
Severity: blocker
Affects: doc-002 BR-5, US-4, § Decomposition ("Two keys are required of upstream"); doc-006 BR-M1-1, ER PROJECT, US-5, Operation completeness (Project); doc-007 § MVP: the seeded process, BR-M2-9, US-1, US-2; queue lines mod-001, mod-002 (docs/backlog.md:16-17)
Problem: every identifier is `{ProjectKey}-{WorkItemKey}-{Sequence}` (doc-002 BR-5, spec:145). doc-002 spec:244-249 states that the project key (W-10: set by the org admin at creation, 2..10 Latin capitals and digits starting with a letter, unique in the organization including deactivated projects, never changed) is required of mod-002/doc-006. It also states that the type keys EPIC/FEAT/US/TASK/BUG (W-11) are required of mod-001/doc-007, and that "All four lines carry it". A grep of doc-006, doc-007 and doc-001 finds no key of either kind. doc-006 US-5 "A project is created ready to use" creates a project from a name alone. The queue lines mod-001 and mod-002 do not mention keys either. mod-001 → mod-002 → mod-003 is the critical path. If mod-002 ships keyless projects, a founded L slice needs a form change and a data backfill (a required, unique, immutable key for existing projects) before mod-003 can issue its first identifier.
Proposed resolution: doc-006 adds `key` to PROJECT in the ER. BR-M1-1 is extended with the key's format, its org-wide uniqueness (deactivated projects included), its immutability, and survival across renames. US-5 gains four scenarios: key required at creation, format refused with a Russian message, a duplicate refused even against a deactivated project, and a rename that keeps the key. Operation completeness Project Update reads "rename; the key never". doc-007 adds a «Ключ» column (EPIC/FEAT/US/TASK/BUG) to the seed table, BR-M2-9 makes the key part of the stable, non-editable identity, and US-1/US-2 show it. Queue lines mod-001 and mod-002 are re-worded during doc-010's normalization.
Decision: default — W-10 and W-11 are already human decisions; this is carry-over only.

### D-3
Severity: major
Affects: doc-003 BR-3, BR-12, BR-19, US-1 sc. "A drag always lands even while a form save is pending", US-2 (reorder); doc-001 BR-10; doc-002 BR-14, US-2 sc. "A save arrives for a work item deleted meanwhile"; doc-007 BR-M2-3, US-3 sc.3
Problem: doc-003 BR-19 (spec:75) says a drag "lands immediately regardless of any form open … with no check" and "is never rejected". Its only refusals are permission and BR-18. On the other side, doc-001 BR-10 and doc-007 BR-M2-3 say «Удалена» is "left only via Restore", and doc-002 BR-14 makes a deleted item read-only; doc-002 US-2 refuses a form save that arrives after a delete. There is no live push (doc-003 NFR "Refresh model"). A member whose board still shows a card that was deleted meanwhile can drag it, and BR-19 then writes the column's state over «Удалена». That is a restore without Restore: no restore entry in history, and the retained BACKLOG_ENTRY path (BR-16/BR-17) is undefined. A reorder of the deleted item's hidden entry has the same gap. A pane drop of the same item fails (doc-004 US-11 is revision-checked), so the surfaces also disagree with each other.
Proposed resolution: doc-003 BR-19 gains: "the no-check rule covers concurrent writes to a live item only; a card move or a reorder of an item in «Удалена», or of a purged item, is refused, nothing changes, and the mover receives the stale-move notice and a refresh". US-1 and US-2 each get a scenario mirroring doc-002 US-2's "save arrives for a work item deleted meanwhile". Promote the invariant into doc-001 BR-10: "no operation other than Restore changes the state of an item in «Удалена», whatever its concurrency policy".
Decision: default.

### D-4
Severity: major
Affects: doc-001 BR-11, US-4 sc.4, US-8 sc.3; doc-002 BR-15, US-3 sc. "A state change is a normal field change"; doc-003 BR-12, BR-19 (DD-2); doc-004 US-3 last sc., US-11 sc. "A drop on an item someone else changed meanwhile", US-12/US-14, NFR Bulk writes; doc-005 BR-8, NFR Concurrency; doc-006 NFR Concurrency
Problem: the concurrency policy is stated six times with different content, and nowhere as a whole.
- doc-002 BR-15 (spec:155) guards "the work item's own field set", Статус included, and US-3 says a state change conflicts "exactly as for any other field". doc-003 BR-19 (human DD-2) says a state written by a drag is never checked. doc-002 does not mention the exception, so mod-003 will build one always-checked state write that mod-006 cannot use.
- doc-001 BR-11 says "two users dragging/reordering the same board or backlog — … last write wins". doc-004 US-11 has a drag from the same backlog onto the planning pane "rejected as a conflict". Two drags on one screen follow opposite policies, and BR-11's wording covers both of them.
- Four writes have no stated policy: the backlog mapping action (doc-003 BR-10: two members assigning different parents at once — does doc-002 BR-8's "at most one parent" survive?), a CSV update row (doc-005 BR-8: which revision does a row check?), iteration administration and team selection (doc-004), and a comment edit.
Proposed resolution: replace doc-001 BR-11 with an operation → policy table:
- form save (fields incl. Статус, Теги, type fields, Итерация): revision check, the loser reloads;
- card move between columns: last write wins, no check, but it advances the revision so a later form save conflicts; refused on «Удалена» (D-3);
- backlog/in-column reorder: last write wins plus the stale-move notice (D-13);
- pane drop: revision check, because it writes the pointer;
- mapping action: link write; the one-parent invariant is enforced atomically and the loser is refused;
- comment/attachment/link add: separate write, never blocked by a stale field revision;
- bulk move and iteration removal: count-confirmed, all or nothing, advancing every touched revision;
- CSV update row (release 3): last write wins per row, with history;
- M1 check-then-act invariants: one of two conflicting changes is refused (doc-006);
- M6 shared objects: revision check (doc-005).
doc-002 BR-15, doc-003 BR-12/BR-19 and doc-004 US-11 cite the table instead of restating it.
Decision: default. The only cell not derivable from existing text is the CSV update row; the default is last write wins per row, and the human may instead prefer an optional revision column in the file.

### D-5
Severity: major
Affects: doc-006 BR-M1-11, US-7 sc. "An area with no explicit owner inherits the nearest owned ancestor"; doc-001 BR-3, US-2 sc.5 (last clause), US-8 sc.4; doc-003 BR-5, BR-13, § Data model (BACKLOG_ENTRY), US-2 sc. "A work item's Area is re-pointed to an unowned node" and sc. "A work item with no team owner has no backlog position"; doc-004 BR-7, BPMN node H, US-3 sc. "A work item with no owning team cannot be scheduled", US-13 sc. "The project list filters by a specific iteration only"
Problem: doc-006 BR-M1-11 (spec:214) says the root area "is always owned by the project's team — its owner cannot be changed or cleared. So every area resolves to an owning team", and US-7:50-52 repeats it. Every other module specifies and tests the opposite branch:
- doc-001 US-2:45-46: "if no ancestor up to the root has an owner … the work item is not shown on any team's board";
- doc-003 BR-13 says MVP exercises "an Area's ownership toggling between 'owned by the project's one team' and 'unowned'";
- doc-003 US-2:62-67 re-points an item "to a node with no owning team";
- doc-004 BR-7 defines the message «У области элемента нет команды-владельца», and US-3:55-61 tests it;
- doc-004 US-13 includes "items with no owning team".
Those Givens cannot be set up and their Thens cannot be observed, so mod-006 and mod-007 would build and test a dead branch.
Proposed resolution: keep BR-M1-11; it stays true with several teams if the root belongs to the project's first team (D-6). State the invariant once, in doc-001 BR-3: "every area resolves to exactly one owning team". Delete the unreachable scenarios: doc-001 US-2 last clause, doc-001 US-8 sc.4, the two doc-003 US-2 scenarios, doc-004 US-3 sc. "no owning team", and the "items with no owning team" clause of doc-004 US-13. Reduce the "no owning team" clauses of doc-003 BR-5 and doc-004 BR-7 to a defensive one-line invariant with no UI message, and drop «У области элемента нет команды-владельца». Replace doc-003 US-2's re-point scenario with the multi-team one: the item's Area is re-owned from team A to team B, so it leaves A's backlog and is appended to B's (BR-6).
Decision: default.

### D-6
Severity: major
Affects: doc-010 D-2; doc-004 E-5, E-13, § Amendments; doc-006 BR-M1-9, BR-M1-11, Operation completeness (Team), US-6 sc. "MVP offers no second team and no team deletion"; doc-003 BR-5, BR-6, BR-11, BR-13, Operation completeness (Board), US-2 sc. re-point, US-3 sc. "A child's linked parent is not one level above it"; doc-005 BR-6, BR-10, mod-009 seeding; doc-001 MVP table M1/M4 rows
Problem: doc-010 D-2 routes E-5 (several teams per project) into doc-006 BR-M1-9/11 only. Several consequences are left unreconciled.
- doc-003 BR-13 (spec:69) still fixes "exactly one team per project" and demotes the cross-team behaviour of BR-5/BR-6/BR-11 to "single-team-reachable variants". The Givens of US-2 and US-3 say "there is no second team" / "reachable in MVP without a second team". With E-5, two behaviours become MVP with no scenario: the cross-team re-scope (rank not carried over, appended at the bottom — § Data model, spec:89) and the cross-team parent (BR-11).
- Team creation: doc-003 Operation completeness Board says boards are "seeded … when the team itself is created (M1, `doc-006`)", but doc-006 has no such hook (US-5 creates team, root area and process only). Teams that exist before mod-006 ships would get no board. doc-005 solves the same problem for dashboards ("for new teams and for teams existing when it ships"); doc-003 does not.
- Team deletion: doc-004 § Amendments says "team deletion stays doc-006's to decide", doc-006 allows none, and doc-005 BR-6/BR-10 assume it exists. No spec says what happens to the team's boards and entries, iteration selection and default, owned areas or bound queries.
- The root area's owner is undefined once a project has several teams.
Proposed resolution: extend the E-5 carry-back.
- doc-003: delete BR-13; BR-5/BR-6/BR-11 stand as MVP behaviour; rewrite US-2's re-point scenario for two teams (D-5); keep US-3's "not one level above" scenario and add a cross-team parent scenario.
- doc-006, teams: an org admin creates teams (name unique in the project) and renames them.
- doc-006, root area: BR-M1-11 reads "owned by the project's first team"; child areas may be owned by any team of the project.
- doc-003, boards: add "M4 guarantees one board and one backlog per (team, level) for every team, including teams that existed before M4 shipped". Whether that is lazy creation or a backfill is a design matter.
Decision: human — is team deletion in MVP?
- (a, recommended) No. Teams are created and renamed only; deletion and its cascade are analysed in release 2. Pro: smallest critical path, and doc-005's "release 2+" assumption holds. Con: a team created by mistake stays; it can only be renamed or emptied.
- (b) Yes, as a soft delete. Its areas are re-owned to the project's first team, its boards and entries are retained and hidden, its iteration selection and default are dropped, and its bound queries stop running (doc-005 BR-6). Pro: complete. Con: puts a cascade across M1, M4 and M5 onto the critical path.

### D-7
Severity: major
Affects: doc-001 BR-6, NFR Audit/retention, Operation completeness (Area, Iteration, Comment, Attachment, Tag), US-1 sc. "archives (soft-deletes) a project"; doc-002 BR-12, BR-13, US-8 sc.6, US-10; doc-004 BR-10, Operation completeness (Iteration), US-14; doc-005 BR-9, BR-10, D-13; doc-006 BR-M1-1, BR-M1-13, Operation completeness (Organization, Project, Area, memberships), US-2, US-5, US-7
Problem: doc-001 BR-6 (spec:279) says "Deletion is always soft, never a hard delete … only an administrator purges a deleted object permanently, by hand", and US-1:80-82 promises a project "stays recoverable indefinitely until an administrator purges it by hand". The modules implement six different lifecycles:

| Object | Deleted as | Restore | Purge | Afterwards |
|---|---|---|---|---|
| Work item | state «Удалена» | member/admin | admin (doc-002) | number never reused |
| Organization, project | status deactivated | admin | none in MVP (doc-006 BR-M1-1), which contradicts doc-001 US-1 | name reserved |
| Iteration | "soft" deactivation (doc-004 BR-10) | never (E-3) | "an administrator purges by hand", but Operation completeness has no purge | name freed, sprint number reserved |
| M6 query/folder/dashboard/widget (r2/r3) | deleted status | whoever may delete (doc-005 BR-9) | admin | — |
| Area leaf | gone ("re-create it", doc-006) | none | — | — |
| Comment text, attachment, tag, team and org membership | gone (doc-002 BR-12/BR-13, US-8; doc-006 "re-add") | none | — | — |

As a result, BR-6's "never a hard delete" is false for areas, comment text, attachments and memberships. Its purge promise is false for organizations and projects and has no operation behind it for iterations. An iteration's "soft" removal is invisible and irreversible, so its softness is only a storage property. Only mod-005 owes the soft-delete ADR (doc-001 § Decomposition), yet mod-002, mod-007 and mod-008 each build a variant.
Proposed resolution: rewrite doc-001 BR-6 as the matrix above, one row per object class with who deletes, restores and purges and what stays reserved. The work-item row stays normative from doc-001 D-5. Correct doc-001 US-1 to "no purge in MVP (doc-006 BR-M1-1)". Drop "an administrator purges by hand" from doc-004 BR-10. Record that mod-005's soft-delete ADR covers the work-item recycle bin only, and that org/project deactivation is designed by mod-002.
Decision: default.

### D-8
Severity: major
Affects: doc-001 BR-6, US-6 sc.1; doc-002 BR-8, BR-9, BR-14, US-5, US-8 sc. "A tag nobody uses stops being offered", US-11 sc. "Deleted items are reached through the state filter"; doc-003 BR-10, BR-16, BR-17, US-3; doc-004 BR-10, BR-12, BR-13, US-14; doc-005 BR-4, BR-8, US-4; doc-006 BR-M1-13; doc-007 BR-M2-6
Problem: each module decided on its own what an item in «Удалена» counts as, and some of those decisions collide.
- Iteration removal clears the pointer of "every work item" pointing at the iteration (doc-004 BR-10, spec:142), which includes read-only deleted items (doc-002 BR-14). The confirmation count in US-14 does not say whether they are counted, while the pane count excludes them (BR-13) and the bulk move leaves them in place (BR-12).
- doc-006 BR-M1-13 allows deleting "a leaf with no work items" without saying whether deleted items count. If they do not, a later Restore puts the item into an area that no longer exists.
- Tags: doc-002 US-8 names removal and purge as the ways a tag stops being offered. It does not say whether a tag used only by a deleted item is still suggested.
- Queries and CSV (r2/r3): doc-005 BR-4 returns "every work item the … role already grants read access to". It does not say whether deleted items appear without an explicit «Удалена» clause, or whether a CSV update row may write a deleted item (US-4 vs doc-002 BR-14).
- Parent link deadlock: a live child of a deleted parent is "treated as unparented" (doc-002 BR-9). Yet it cannot take a new parent (BR-8 allows one parent), it cannot drop the old link (BR-14 forbids re-linking a deleted item), and the mapping action is offered only for "a child with no parent yet" (doc-003 US-3). It stays stuck until the parent is restored or purged.
- An area re-owned while its item is deleted: is the retained BACKLOG_ENTRY (doc-003 BR-16) re-scoped?
- Release 3: may a custom state be removed while a deleted item remembers it as its restore target (doc-007 BR-M2-6 "references")?
Proposed resolution: add one participation rule to doc-001 next to BR-6.
- (a) Visibility: an item in «Удалена» is excluded from every list, board, backlog, pane count, bulk move, tag suggestion, query result and export, unless a filter or clause names «Удалена» explicitly (generalises doc-002 US-11; doc-005 BR-4 amended).
- (b) References: it still holds its area, iteration, tags, state and restore-target state for referential rules. Deleting an area that holds it is refused ("work items, deleted ones included"), and a release-3 state removal is refused while any item is in that state or remembers it as its restore target.
- (c) Writes: only Restore, Purge and system bulk writes change it. Iteration removal clears its pointer, counts it in the confirmation and records history. A CSV row that targets it is rejected. The link between a live child and a deleted parent may be removed from the child's side, recorded in both histories, and the mapping action treats such a child as parentless (it replaces the link, D-9).
- (d) If its Area is re-owned while it is deleted, the retained entry is re-scoped on Restore and appended at the bottom of the new team's backlog.
Decision: default.

### D-9
Severity: major
Affects: doc-002 BR-8, BR-10, W-1, US-5 sc. "A work item has at most one parent"; doc-003 BR-10, BR-11, US-3 sc. "Assigning a parent from the backlog view links the item" and sc. "Assigning a different parent replaces the existing link"
Problem: the same act, giving an item a different parent, has opposite outcomes in two modules.
- doc-002 US-5:33-39: picking a different parent "is refused, and the message names the existing parent … re-parenting is removing the old link and adding the new one".
- doc-003 BR-10 (spec:66) and US-3:32-38: the mapping action "creates or replaces" the link and ends with exactly one link, to the new parent.
doc-003 writes "through M3's Parent/Child link" (doc-003 § Roles), but M3 has no replace operation, and M3's history rule (BR-10) does not say what a replace records on the three items involved. Separately, doc-003 US-3 offers mapping only to "a prospective parent one level up", while W-1 allows same-level parents (a Баг under a История, a Задача under a Задача). The backlog view can therefore neither create nor show those links, and does not say so.
Proposed resolution: doc-002 BR-8 gains a named operation, "replace parent": an atomic remove plus add, refused under the same conditions as add, recorded as a removal on the old parent, an addition on the new parent and one revision on the child. The backlog mapping uses it, and the form keeps "refuse and name the existing parent" (the reference product splits it the same way: its form refuses a second parent, its mapping pane re-parents). doc-003 BR-10 cites the operation. doc-003 US-3 states explicitly that mapping targets only the level directly above and that same-level links are made on the form.
Decision: default.

### D-10
Severity: major
Affects: doc-001 BR-7, BR-9, US-9 sc.3-6; doc-003 § Out of scope ("Iteration-scoped views of a board or backlog"); doc-004 BR-7, BR-8, BR-9, § Amendments, US-11, US-13; doc-005 BR-4, BR-6, US-1 § Query language, sc. "The @CurrentIteration macro needs a team"
Problem: "iteration filter" and "current iteration" mean different things in doc-001, M4, M5 and M6.
- Subtree vs exact match. doc-004 BR-9 matches items pointing at an iteration "or at any of its descendants" (US-13). doc-005 US-1 offers only "Итерация =" for @CurrentIteration, and adds no Under for Итерация even in release 3. doc-001 US-9:40/47/55 shows "only work items assigned to" the iteration. For a current iteration that has children, one team gets three different result sets.
- Scope. doc-004 BR-8 says the current iteration "exists only on team surfaces … never on the project-wide work item list". doc-005 BR-6 puts it into project-wide queries bound to a team, whose results are not narrowed to that team (BR-4), while doc-004 US-13 narrows the team board to team-owned items. No spec says whether a team-bound query returns other teams' items.
- Derivation. doc-005 BR-6 cites doc-001 BR-9 only. The UTC clock and the four tie-breaks live in doc-004 BR-8 alone, so M6 could implement doc-001's subset.
- doc-003 § Out of scope (spec:130-134) says "`doc-001` BR-7 is read by M5's planning surfaces, not by M4's MVP views". That contradicts doc-001 BR-7 ("boards, backlogs and planning all read that one pointer") and doc-004's MVP filter (US-13) and planning pane (US-11), both of which sit on M4's surfaces.
- doc-001 US-9 sc.3 assigns an iteration without the team-selection precondition that doc-004 BR-7 makes mandatory. doc-004 § Amendments does not list US-9 sc.3-6.
Proposed resolution:
- Move the full derivation (UTC, tie-breaks) into doc-001 BR-9. doc-004 BR-8 keeps only "derived on read; on team surfaces and in team-bound queries".
- Add one cross-module rule to doc-001: "an iteration condition matches the iteration and its descendants". doc-005 US-1 gives Итерация that semantics, @CurrentIteration included.
- doc-005 BR-4 states that a team-bound @CurrentIteration does not narrow results to the team's areas.
- Replace doc-003's out-of-scope sentence with "M4's MVP views read the pointer only through the iteration filter (feat-008) and the planning pane (feat-009)".
- Amend doc-001 US-9 sc.3-6 (team-selection Given, descendants) and list them in doc-004 § Amendments.
Decision: default — subtree semantics everywhere; doc-004 BR-9 is the only analysed, gate-approved rule.

### D-11
Severity: major
Affects: doc-001 Glossary («Статус / категория статуса»), BR-2; doc-003 BR-2, US-1; doc-007 § MVP: the seeded process, US-2 sc.1 and sc.4
Problem: doc-003 BR-2 (spec:58) defers column headers to M2: "Column wording is owned by M2/`doc-007`, not invented here". doc-007 defines no wording at all. Columns are per category, so under any option of D-1 the Истории InProgress column holds История «Активна» next to a Баг state, and no single state name can head it. doc-007 US-2 also shows "each state's category" to every role on the read-only process screen. Proposed/InProgress/Resolved/Completed/Removed would therefore reach the UI in English, against AGENTS.md's Russian-UI rule.
Proposed resolution: doc-007 adds a category table with Russian labels. Board column headers use it (doc-003 BR-2 cites it), so does the process screen, and doc-001's Glossary gets the row. Proposed labels: «Предложено», «Выполняется», «На проверке», «Завершено», «Удалено». Resolved deliberately avoids «Решено», which is too close to the История state «Решена» that sits in the InProgress column. If D-1 picks option (C), InProgress needs a label other than «Выполняется», e.g. «В процессе».
Decision: default — the labels are UI copy and the human may object to the wording.

### D-12
Severity: major
Affects: doc-001 BR-4, NFR Audit/retention, Operation completeness (Comment), US-5 sc.2; doc-002 BR-10, NFR Audit and retention, Operation completeness (Comment), US-10 sc. "An administrator purges permanently" and "A purged parent leaves its children consistent"; doc-003 (drag, reorder, mapping); doc-004 NFR Audit, Operation completeness (Iteration); doc-005 NFR Audit; doc-006 BR-M1-15, BR-M1-16, NFR Audit
Problem: doc-001 NFR (spec:314-316) promises that "every change, delete and restore is attributable to a user and a timestamp". The modules deliver less.
- M1 keeps who/when on memberships only; deactivating or restoring an organization or project is unrecorded until release 2 (doc-006 NFR, BR-M1-15).
- Iteration create, rename, redate, move and remove, and team selection, are unrecorded: doc-004 audits pointer changes only.
- M6 has no audit (doc-005).
- A purge destroys the item's history with no tombstone (doc-002 NFR). The purge itself is unattributable, and US-10 does not say that the surviving children's link removal is recorded.
- doc-003 never mentions history: it is silent on reorder, and a drag is recorded only by implication ("the same write").
- doc-001 BR-4's list lacks create and tag add/remove, which doc-002 BR-10 adds.
- Whether history keeps a comment's old text: doc-001 Operation completeness says "edit history stands", doc-002 Operation completeness says "the earlier text is not restorable".
Proposed resolution: scope doc-001 BR-4 and its NFR explicitly to work items, and merge doc-002 BR-10's additions into BR-4 (create, tag add/remove, one save = one revision). Then add a list of what MVP does not audit: M1 beyond membership who/when, M2, the M4 order (a reorder is not a history event), M5 iteration administration, and M6. Around it, state that:
- a card move is a state revision authored by the mover;
- a purge records "parent purged: <identifier>" in each surviving child's history, while the purge itself stays unrecorded until release 2's audit log (accepted, and said so);
- a comment edit records that the text changed, not the old text (doc-002 wins).
Decision: default.

### D-13
Severity: minor
Affects: doc-001 BR-11, US-8 sc.3; doc-003 BR-12, NFR "Refresh model", BPMN, US-1 sc. "Two members drag the same card at once", US-2 sc. "Two members reorder the same backlog at once"
Problem: the scenarios say "the last write wins, and the other member sees a one-time notice and their view refreshes" (doc-001 US-8:33-35, doc-003 US-2:58-60). "The other" reads as the member whose move was overtaken. doc-003's NFR (spec:102-111), however, gives the notice only to the later mover, "attached when the view it was made from was already stale", and the earlier mover sees nothing until they interact. When the two drags move different items, both apply, so "last write wins" decides nothing. No spec gives the notice a Russian text. As written, the Then cannot be tested.
Proposed resolution: rewrite the scenarios.
- Different items: "both moves apply in arrival order; the member whose drag arrived second receives with the response the notice «Порядок изменён другим участником — список обновлён» (default wording) and a refreshed view; the first member sees the combined order at their next interaction".
- Same card: "the second drag's column is final and the second mover receives the notice".
doc-003 records the text.
Decision: default.

### D-14
Severity: minor
Affects: doc-001 US-5 sc. "The author edits their own comment"; doc-002 BR-12, US-6 sc. "The author edits their own comment"
Problem: doc-001 US-5:24-26 says "the edit shows in history, not in the comment itself", while doc-002 US-6:44-45 says "the comment is marked as edited with the time of the edit". The two Gherkin outcomes contradict each other.
Proposed resolution: amend doc-001 US-5 to doc-002's outcome: an "edited" marker with the edit time on the comment, plus a history entry.
Decision: default.

### D-15
Severity: minor
Affects: doc-006 BR-M1-17, US-3 sc. "A member whose Keycloak account is disabled is shown as inactive" and sc. "An administrator removes a member"; doc-002 § Fields (Исполнитель), W-3, US-2 sc.3-4, US-11 sc. "Items of a former member can still be found"; doc-001 BR-13
Problem: doc-006 BR-M1-17 (spec:219) has M3 render «неактивен» and leaves "whether it remains an assignee candidate" to doc-002, "alongside BR-13". doc-002 does not mention inactive accounts: its candidate set is "every current member", which includes them. Its own "former member" mark (W-3) has no Russian text. One field can carry two marks, and nothing says which wins (for example, an inactive account that is later removed from the organization).
Proposed resolution: doc-002 § Fields, Исполнитель: inactive members are not offered for a new assignment, as in doc-006's member and team pickers. Marks: «неактивен» (membership kept, account disabled) and «не в организации» (membership removed; default wording); the second wins when both apply. US-2 gets a scenario for the inactive case.
Decision: default.

### D-16
Severity: minor
Affects: doc-001 D-11, BR-9; doc-004 BR-6, § Terms, US-2 sc. "The team's default is set"; doc-002 § Fields (Итерация)
Problem: in the reference product, a team's default iteration is where new work items land (research § Team settings: "default iteration macro @CurrentIteration"). In MUSTASK it only decides the current iteration when no dates do (doc-001 BR-9), and a new item gets no iteration (doc-002 § Fields: default none). The label is the same and the behaviour differs, and no MUSTASK spec tells a reader who knows ADO that new items do not land there.
Proposed resolution: doc-004 BR-6 adds "the team's default iteration is never assigned to new work items; it only decides «Текущая итерация» when no dates do", with a scenario in US-2. The label stays, as doc-001 D-11 names it.
Decision: default.

### D-17
Severity: minor
Affects: doc-002 § Fields (Статус), BPMN node C, US-1 sc. "Each type is created in its own type's initial state"; doc-007 BR-M2-2; doc-005 BR-8, US-4 sc. "Importing new work items from CSV"
Problem: the initial state is defined two ways. doc-002 says "the type's Proposed state"; doc-007 BR-M2-2 says "the first is the type's default initial state". They coincide today and diverge once release 3 lets a custom Proposed state be added. Neither spec says whether Статус can be chosen before the first save, although a CSV import creates items "through the same validation path as a manual create" (doc-005 BR-8), and its file may carry a Статус column.
Proposed resolution: doc-002 § Fields, Статус, cites BR-M2-2 ("the type's first state") and adds: "a work item is always created in its type's first state; the create form shows Статус read-only; a CSV row that names another state for a new item is rejected with the reason".
Decision: default.

### D-18
Severity: minor
Affects: doc-001 Glossary, US-1 sc. "An organization administrator archives (soft-deletes) a project"; doc-002 US-2 (form header «Тип»), US-11 (list columns); doc-005 US-1 field table, US-4 sc. "Importing updates to existing work items from CSV"; doc-004 BR-10, US-14; doc-006 US-2, US-5
Problem: the glossary and UI copy drift across modules.
- One concept has four verbs: a work item is «Удалить» (→ «Удалена»); an iteration is "removed" (message «Сначала удалите…»); a project or organization is "deactivated" («…: он деактивирован»), while doc-001 US-1 calls the same act "archives". No spec gives Russian action labels for org/project deactivate and restore, or for iteration removal.
- Field labels differ: doc-002 uses «Тип» and «Теги» on the form and in the list, while doc-005's clause builder uses «Тип рабочего элемента» and «Тег».
- doc-005 US-4 identifies rows by "existing work item ids", but the user-visible identifier is `MSK-BUG-42` (doc-002 BR-5), and its number is unique only within a project.
- doc-001's Glossary defines «Корзина» although no such screen exists (doc-001 D-5; doc-002 § Out of scope), which invites a UI label.
- Related strings are handled elsewhere: category labels in D-11, the notice text in D-13, the assignee marks in D-15.
Proposed resolution: doc-001's Glossary gains «Удалить»/«Восстановить»/«Удалить навсегда» (work item), «Деактивировать»/«Восстановить» (organization, project) and «Удалить итерацию». doc-001 US-1 "archives" becomes "deactivates". doc-005 uses M3's labels «Тип» and «Теги». CSV rows are identified by the full identifier. The «Корзина» row is annotated "term only, never a UI label".
Decision: default.

### D-19
Severity: minor
Affects: every business-rule table: doc-001 BR-1..13, doc-002 BR-1..17, doc-003 BR-1..19, doc-004 BR-1..17, doc-005 BR-1..12, doc-006 BR-M1-1..17, doc-007 BR-M2-1..9; decision ids doc-005 D-13..D-16, doc-010 D-1..D-3
Problem: doc-001 and doc-002..005 share one unprefixed BR-n namespace, while doc-006 (BR-M1-n) and doc-007 (BR-M2-n) are prefixed. Citations collide.
- "BR-3" means team ownership in doc-001, title trimming in doc-002, the card column in doc-003, the iteration name in doc-004 and shared queries in doc-005.
- doc-001 US-8 cites the stale-move rule as BR-11, while doc-003 US-2 cites the same rule as BR-12, which in doc-001 is stack rank.
- Decision ids collide too: doc-005 continues the umbrella's D-series (D-13..D-16), and doc-010's own D-1..D-3 share ids with doc-001's D-1..D-3.
Proposed resolution: one scheme.
- Unprefixed BR-n is reserved for doc-001's cross-module rules.
- Module rules become BR-M<k>-n, with k the module number and n kept: doc-006 = M1 and doc-007 = M2 as today; doc-002 = M3, doc-003 = M4, doc-004 = M5 and doc-005 = M6 gain the prefix (BR-3 of doc-002 becomes BR-M3-3).
- A deleted rule leaves a gap, numbers are never reused, and a citation of another module's rule always uses the prefixed form. Decision ids are cited with their task id outside their own task.
Pure duplicates, to delete in favour of the doc-001 rule:
- doc-002 BR-6 and BR-7 (→ BR-10);
- doc-003 BR-7 (→ BR-12) and BR-12 (→ BR-11);
- doc-003 BR-13 (superseded by E-5, D-6);
- doc-004 BR-14 (→ § Roles);
- doc-006 BR-M1-7's first sentence (→ BR-5; the Area→Project→Organization resolution chain stays);
- doc-007 BR-M2-3 (→ BR-10).
Rules to merge into doc-001 and then delete:
- doc-002 BR-10 → BR-4 (D-12);
- doc-004 BR-8 → BR-9 (D-10);
- doc-002 BR-15 and doc-003 BR-19 → the BR-11 table (D-4);
- doc-002 BR-14 → BR-10's "only Restore leaves «Удалена»" clause (D-3);
- doc-005 BR-9 → the BR-6 matrix (D-7);
- doc-007 BR-M2-2 keeps only "states are ordered; the first is the initial state".
Decision: default.

## State tables

Per-type states after D-1 under the recommended option (B), each mapped to its category. Order is the seed order, and the first state is the initial state (BR-M2-2, D-17). Transitions are unchanged: any non-Removed state may move to any other with no reason (doc-001 BR-10, doc-001 D-6); «Удалена» is entered only by Delete and left only by Restore (D-3), and Restore defaults to the pre-delete state. Cells marked ✎ change against the spec named in the column.

| Type (level) | # | State | Category | vs doc-002 | vs doc-007 | vs doc-001 US-3 |
|---|---|---|---|---|---|---|
| Эпик, Фича, Пользовательская история (Эпики, Фичи, Истории) | 1 | «Новая» | Proposed | — | — | n/a |
| | 2 | «Активна» | InProgress | — | — | n/a |
| | 3 | «Решена» | InProgress | ✎ was «Разрешена» | — | n/a |
| | 4 | «Закрыта» | Completed | — | — | n/a |
| | 5 | «Удалена» | Removed | — | — | n/a |
| Баг (Истории) | 1 | «Новый» | Proposed | ✎ was «Новая» | — | n/a |
| | 2 | «В работе» | InProgress | ✎ was «Активна» | — | n/a |
| | 3 | «Исправлен» | Resolved | ✎ was «Разрешена» | — | n/a |
| | 4 | «Закрыт» | Completed | ✎ was «Закрыта» | — | n/a |
| | 5 | «Удалена» | Removed | — | — | n/a |
| Задача (Задачи) | 1 | «Новая» | Proposed | — | — | — |
| | 2 | «Активна» | InProgress | — | ✎ was «Выполняется» | ✎ was «Выполняется» |
| | 3 | «Закрыта» | Completed | — | ✎ was «Готово» | ✎ was «Готово» |
| | 4 | «Удалена» | Removed | — | — | — |

Under option (A), Баг takes «Новая»/«Активна»/«Разрешена» (Resolved)/«Закрыта»/«Удалена», and Эпик/Фича/История #3 is «Разрешена» (InProgress). The ✎ marks then move to the doc-007 column. Задача is the same under (A) and (B).

Category labels (D-11, new everywhere): Proposed «Предложено», InProgress «Выполняется», Resolved «На проверке», Completed «Завершено», Removed «Удалено» (never a column).

Board columns per level (doc-003 BR-2/DD-1) after D-1 (B) and D-11:

| Level | Columns (header, in order) | State a drop writes, per type |
|---|---|---|
| Эпики | Предложено / Выполняется / Завершено | Эпик: «Новая» / «Активна» / «Закрыта»; a card in «Решена» shows under Выполняется, and «Решена» is set on the form only |
| Фичи | Предложено / Выполняется / Завершено | Фича: as Эпик |
| Истории | Предложено / Выполняется / ✎ На проверке / Завершено | История: «Новая» / «Активна» / no drop target (BR-18) / «Закрыта»; Баг: ✎ «Новый» / ✎ «В работе» / ✎ «Исправлен» / ✎ «Закрыт» |
| Задачи | no board in MVP; the release-2 taskboard would use Предложено / Выполняется / Завершено | Задача: «Новая» / «Активна» / «Закрыта» |

All column headers are new text (✎ on every header; doc-003 had none). A card in «Удалена» is on no board and no backlog (doc-003 BR-16). Drags and reorders of such a card are refused (D-3).

## Summary

Blockers 2, majors 10, minors 7 (19 findings). Human decisions: D-1 (which state-label set) and D-6 (team deletion in MVP); everything else has a default.

1. D-1: three incompatible per-type state lists (doc-001 US-3, doc-002, doc-007), each backed by conflicting human decisions (W-2/W-7/W-9 vs P-1/P-2), so mod-001 cannot be seeded until one set is chosen.
2. D-2: the project and type keys that every work item identifier needs are missing from doc-006, doc-007 and the mod-001/mod-002 queue lines, which puts rework and a data backfill on the critical path.
3. D-6 (with D-5): E-5 (several teams per project) was carried into doc-006 only. doc-003 still assumes one team per project, the team create/delete cascade across M4/M5/M6 is undefined, and the "no owning team" branch that three modules test cannot occur.
