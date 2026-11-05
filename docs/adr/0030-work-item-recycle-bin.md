# ADR-0030: Delete a work item into its Removed state with the way back on its row, hide it by named entry points, and purge it in one locked transaction

- Status: accepted (2026-10-18, the human in chat at mod-005 gate-2, recorded by the agent per docs/adr/README.md Process 2) (amends ADR-0029 § 4: it places two matrix rows)
- Date: 2026-10-18
- Context: `mod-005-work-item-delete-restore`, stage 3 (design): [spec](../features/mod-005-work-item-delete-restore/spec.md) BR-DR-1..-18, [design](../features/mod-005-work-item-delete-restore/design.md) and stories US-1..US-6. Owed by mod-005 under doc-010 § ADR debt, row "Soft delete / recycle bin". Its sources are doc-001 D-5, BR-6, BR-10, BR-14 and § Deletion lifecycle, doc-002 BR-M3-5, BR-M3-9, BR-M3-21 and US-10, and doc-010 D-13 and PR-9. **It references [ADR-0026](0026-soft-deactivation-by-status.md)**, whose rule 9 sends the work item's recycle bin here, and it builds on ADR-0010, ADR-0011, ADR-0013, ADR-0027, ADR-0028 (rule 8's lock order) and ADR-0029 § 4. It goes to the human at mod-005's gate-2 with the design, and only the human sets `accepted` (`docs/adr/README.md`, Process 2).

## Problem

mod-003 refuses «Удалена» as a work item's state (BR-M3-21) and leaves removal to this line. The ground is ready: every type of the organization's process has exactly one state of the Removed category, and it is the last in the type's order (`SeededProcess`); feat-001, mod-006 and feat-032 already hide an item in that category from backlogs, boards, parent pickers and checklists. Each of those reads spells the category test for itself, and no code can put an item there yet. The decision they wait for has four parts: how an item gets into «Удалена» and back out; what every read and write does with it, and where that is enforced so a later line cannot forget it; the purge, the one operation ADR-0026 does not have; and what a later line must do for its own data.

The forces are these. The state is the item's user-visible lifecycle (doc-001 BR-10), so the bin cannot be a second status beside it. Restore must know the state the item held, and no history exists to ask (feat-015 is later). A deleted item is still read by its address, so ADR-0026's "removed means not found" does not fit it. Reads run at READ COMMITTED with targeted locks (ADR-0011), and ADR-0029 § 7 makes list reads caller-aware, so hiding cannot be one global filter. Deletion never cascades (BR-M3-9). No job may remove anything (BR-DR-5). A purge of an Эпик with 5,000 children takes 2 s, and a list with deleted items among 5,000 takes 2 s. Comments, attachments, tags and history will each add data keyed to an item, and none may outlive its purge (doc-010 PR-9, BR-DR-16).

## Options considered

### A. Where a deletion is recorded

#### Option A1: a flag (`deleted_at`) beside an unchanged state

- Pros: the held state is simply still there, and Restore clears one column.
- Cons: the row has two lifecycles. doc-001 BR-10 and BR-M3-21 already make «Удалена» the state, and every existing surface, board column and state filter reads the Removed category; with a flag, each of them shows a deleted item as live until it learns the flag, and flag and state can disagree. Rejected.

#### Option A2: the state «Удалена», with the held state read from history

- Cons: feat-015 and its ADR do not exist, so Restore would wait for a later line. Rejected.

#### Option A3: the state «Удалена», and the marks (held state, who, when) in a side table `work_item_deletions`, one row per deleted item

- Pros: `work_items` is untouched, and the table is a ready list of deleted items for feat-003.
- Cons: every item read left-joins a second table for three values that exist on a few rows. "Marks without the state" and "state without marks" are two states to police across tables, and a CHECK cannot span tables. Not chosen; it is D-1 (B).

#### Option A4: the state «Удалена», and the marks as three nullable columns of `work_items` (chosen)

- Pros: Delete and Restore are one-row writes (ADR-0026 rule 2). A CHECK keeps the marks all-or-none, a foreign key makes the held state a database fact, and the item read gets the marks with the row.
- Cons: three mostly-null columns on the widest table. The marks are memory, not truth (rule 1), so a row can be in the Removed category with no marks.

### B. How a deleted item is hidden

#### Option B1: a global EF Core query filter on `WorkItem`

- Pros: a forgotten condition cannot leak a deleted row.
- Cons: ADR-0026 rejected it (option 2) for the same reasons, and they are stronger here. Half the reads need deleted rows: the card by address, Restore, Purge, the locks, the clean-ups, a team's owned count "in any state" (BR-OT-12), and feat-003's «Удалена» filter. Each would opt out with `IgnoreQueryFilters`, which drops every other filter too. "Deleted" is a category on another table, so the filter would be a subquery in each query. The raw-SQL lock reads bypass it anyway. ADR-0029 § 7's caller-aware reads cannot be a global filter, so there would be two mechanisms for one job. Not chosen; it is D-2 (B).

#### Option B2: named entry points, one place per read model, and tests that fail when one is forgotten (chosen)

- Pros: the same rule as ADR-0026 rule 4, so one mechanism for every removable thing. A read states its intent by name.
- Cons: a read that forgets to choose leaks until a test finds it. The surface matrix test below exists for that.

### C. How a purge reaches the data keyed to the item

#### Option C1: asynchronous: mark the item "to purge" and let a job remove it and its data

- Cons: the item is in limbo, no job may remove anything (BR-DR-5), and a purge would end with the item gone while data remains. Rejected.

#### Option C2: each line removes its own data in its own handler after the purge, through events and the outbox

- Cons: no atomicity with the item, and a window in which the data outlives it, which BR-DR-16 forbids. It is right only for a store outside the database (ADR-0013), and there it is a step's job (rule 7).

#### Option C3: foreign keys with cascade by default, steps for what a cascade cannot do, and a build check that sees every reference (chosen)

- Pros: the default costs no code and is atomic with the purge. A forgotten reference breaks the build, and a key left at `Restrict` breaks the purge loudly.
- Cons: a reference outside the EF model (a JSON column, an external store) is seen only through a step that declares it.

## Decision

A4, B2 and C3, with these rules.

**1. A deletion is a state, with the way back on the row.**

- **Delete** sets the state to the type's one state of the Removed category and writes three marks in the same row: `deleted_from_state_id` (uuid, the state held), `deleted_by` (varchar of `WorkItemLimits.UserIdMaxLength`, the Keycloak subject as `created_by` is, no key) and `deleted_at` (timestamptz). «Изменён» takes the deletion's time. Nothing else on the row changes: type, fields, assignee, area, owning team, parent and children stay (BR-DR-3). **Restore** sets the state to the chosen one, else the held one, else the type's first; it clears the three marks and moves «Изменён». Nothing else changes.
- The columns are added with `ck_work_items_deletion_marks` (all three null, or all three set) and a foreign key `deleted_from_state_id` onto `state_definitions`, `Restrict`. That key is the database's half of doc-001 BR-14 (b): a release-3 state removal is refused while an item remembers the state as well as while one is in it (doc-007 BR-M2-6), with no code to forget. The migration is additive and reversible. Nothing is in «Удалена» before this ships, so no data moves (BR-DR-18).
- **Deleted is decided by the category, never by the marks.** An item is deleted when its state's category is Removed (doc-001 BR-2, BR-DR-18). An item put there outside Delete has no marks, is deleted, and is restorable to its type's first state (BR-DR-10). The marks cannot be written on a live item: the setters are private and Delete and Restore write them with the state. `deleted_at` is not `changed_at`: a team's clean-up moves `changed_at` of a deleted item (BR-DR-9), and the card shows the deletion's own time.
- Delete relies on one Removed state per type, which the seeded process guarantees. Release 3 must keep the Removed category fixed per type: no state moves into or out of it, and none is removed while an item is in it or remembers it (owed row in the spec).

**2. One definition of "deleted", and one named place in every read.**

- The definition is "the item's state is in the Removed category of its type". It has three expressions that must agree: in memory (`WorkItemTypeStates.IsRemoved`, from which `Allows` and `HierarchyTypes.IsDeleted` read, one list of removed state ids built from the process); in LINQ (`WorkItemSets`, below); and in the locking raw SQL and the memberships of the backlogs, which take the scope's shown-state ids. A parity test over a process with an item in every state of every type pins the three equal.
- **No global query filter** (ADR-0026 rule 4, and ADR-0029 § 7). `Mustask.Infrastructure/WorkItems/WorkItemSets` offers three entry points, and a read model that returns or counts a set of items composes on one of them: `Live` is the default (lists, pickers, children, checklists, counts, queries); `Deleted` is for a filter that names «Удалена» (feat-003); `InAnyState` is for the card by address, a live child's stored parent, a team's owned count, Restore, Purge, the clean-ups and the locks. The backlog and board memberships keep their own single place (`BacklogMembership`, `ProjectBacklogMembership`) and stay inside the parity test. The list (`GetPageAsync`) reads every state today and moves onto `Live` in this line; the sites that spell the category test move onto the entry points where this line touches them.
- **The surface matrix test.** One integration test seeds a deleted item with a live parent, a live child and a deleted child, and reads every surface of spec § Surfaces and writes that exists. Each is present or absent exactly as that table says. A surface added later adds a row, and a surface with no row is what review looks for. This is what makes a forgotten surface fail.
- **The writers of `work_items`.** An architecture rule of the `ProcessStateWritesRules` kind (a text rule, not a proof): outside `Mustask.Infrastructure/WorkItems/` and the migrations, nothing writes `work_items` by raw SQL, `ExecuteUpdate`/`ExecuteDelete` or `Remove`. Inside, a raw `UPDATE work_items` names neither `state_id` nor `deleted_*`, which only the aggregate writes. Today the one raw writer is the team release; the purge adds two statements.

**3. The aggregate decides, the handler orders, an inventory classifies (BR-DR-8).**

- The refusal is a domain rule: `WorkItem.EnsureLive(type)` throws `ConflictException(workitems.workitem.deleted)`, decided on the **stored** state under the item's `FOR UPDATE` lock, before anything is applied. A save that sends a live state together with a parent therefore cannot slip past (tech-012 m1).
- Two methods change an item: `WorkItem.Save` and `WorkItemHierarchy.Write` (with `CreateChild`). `Save` calls `EnsureLive` first of all. `Write` and `CreateChild` keep their `workitems.link.item_deleted` checks, which are the link routes' code. A handler written later that forgets the early call below still cannot change a deleted item; it answers at another position.
- The handler places the call, because the answer order is part of the contract: the path's not-founds, then the refusal, then the body (values 400, area, team, parent, state, assignee). `SaveWorkItemHandler` calls `EnsureLive` right after the item is locked and its type resolved, before `ValidateValues`. A form save of a deleted item answers `…deleted` whatever it names, a parent included; the parent route and a live item's save that names a deleted parent keep `…link.item_deleted`.
- **The write inventory.** An integration test enumerates the endpoints whose route carries a work item identifier or whose body names work item ids, and requires each one to be classified in a table beside it: refuses a deleted item (with its code and the test that proves it), or acts on one (Restore, Purge). A new route with no row fails `verify`, as `OrganizationAccessInventoryTests` fails a route with no code. Writes that name other items in the body (a reorder, a mapping, later a sprint assignment or a bulk move) have rows too.
- **System clean-up** (BR-DR-9) is the third kind of writer: set-based SQL in `Infrastructure/WorkItems/` that changes one reference and `changed_at` of items in any state. It is not a user write, takes no `EnsureLive`, and sits in the inventory as such. A team's deletion and a project–team unlink are the two today; ADR-0029 § 6 "Consequence writes" governs their rights.

**4. Delete and Restore are one-row writes.**

- Each takes the item `FOR UPDATE` (`IWorkItemRepository.LockAsync`), loads it tracked, decides on what it reads under the lock (Delete: `deleted`; Restore: `not_deleted`, then `state_not_allowed`, BR-DR-10), writes the row and flushes in `ExecuteInTransactionAsync`. Neither takes a revision check, being a separate write (doc-001 BR-11), and neither takes the hierarchy lock or any other item's: nothing is cascaded, so no child, link or place is read or written.
- The backlog places are not touched. A deleted item is a member of no backlog, so its place is simply not read, which mod-006 already tolerates; the next reorder of that backlog drops it. Restore therefore finds the item where it stood if nobody reordered, and unplaced below the placed items if somebody did (BR-DR-10).
- Restore takes no hierarchy check. No write adds a link to or from a deleted item, and the cycle and depth checks count stored links through it.
- No event is published (ADR-0026 rule 7): other modules read the category.

**5. The purge: a hard delete in one transaction, by an administrator, of a deleted item only.**

`workitems.workitem.purge` is checked by the route. The handler resolves the address from the outside in and reads the item without a lock (a live one answers 409 `not_deleted` here already). It then runs the steps' pre-flights (rule 7), and then, in `ExecuteInTransactionAsync`:

1. Locks the project's hierarchy `FOR UPDATE`, as every link-changing write does (ADR-0028 rule 8 step 5, feat-001). The item's children are then a stable set, and no cycle or depth check reads a link being removed.
2. Locks the item and its direct children, deleted children included, in **one statement** `ORDER BY id FOR NO KEY UPDATE`. This is the order every other locker of several items keeps (ADR-0028 rule 8; `IWorkItemTeamOwnership.LockOwnedAsync` is the model), so a purge and a team's deletion or unlink never deadlock. The mode is a parent write's link-end mode, so a reorder's `FOR KEY SHARE` checks are never waited on. It then re-reads the item tracked: no row is 404 `workitems.workitem.not_found`, and a state outside Removed is 409 `workitems.workitem.not_deleted`, with nothing removed.
3. Runs the registered purge steps (rule 7) and drops the item's team place through `ITeamBacklogPlaces.DropForItemAsync`. The place's second key onto the item's owner is checked at the end of a statement, exactly as for a change of owner (BR-OT-6), so the drop comes first rather than depending on the order in which two keys are checked.
4. `UPDATE work_items SET parent_id = NULL, changed_at = :now WHERE project_id = :p AND parent_id = :id`: one set-based statement for 5,000 children (BR-DR-13). They keep their state, owner and places; a deleted child stays deleted. The purged item's own parent is not touched, and neither is its «Изменён».
5. `DELETE FROM work_items WHERE id = :id AND project_id = :p`. The key `(project_id, parent_id)` is `Restrict`, so a forgotten step 4 fails the delete instead of leaving a dangling parent. The project place goes with the row by its cascading key; the other items keep their positions, and a gap in positions orders as well as a dense run (the next reorder renumbers).

After the commit, and never inside the transaction, the handler writes one structured log line with the user, organization, project and identifier, and never the title or any content. A rolled-back purge therefore logs nothing. That is the whole trace (BR-DR-15, spec OQ-3 A). No tombstone and no copy remain, and release 2's audit log takes over this call site. Two consequences are accepted. The project's `work_item_counters` row is never decremented, so the number is never issued again under any type key (BR-DR-14); the unique index on (project, number) does not guard that, the counter does. And the item's creation-request key (BR-M3-26) goes with the row, so a replay of an old creation after a purge creates a new item: the key defends a double submit, not a replay across days.

**Idempotence.** The effect is idempotent and the answer is not, and there is no key. A second purge, and a restore or a save after a purge, find no row and answer 404 `workitems.workitem.not_found`. A purge of a live item answers 409 `not_deleted`. A retry never brings anything back, and the panel treats a 404 on Purge as "gone, as the user wanted" (design § 8).

**6. Races.** Each ends in one order and no server error. A request that locks second re-reads under its lock and decides on what it finds.

| Pair | Outcome | Why |
|---|---|---|
| Delete ∥ Delete | the second answers 409 `…deleted`; the held state is the first's | the item lock, a re-read under it |
| Delete ∥ Save of the item | save first: the delete records the state the save left; delete first: the save answers 409 `…deleted` | the same lock; `EnsureLive` is the save's first act under it |
| Restore ∥ Restore; Restore ∥ Save | the second restore answers 409 `…not_deleted`; a save decided second lands on the restored item | the same lock |
| Restore ∥ Purge | restore first: the purge answers 409 `…not_deleted`; purge first: the restore finds no row, 404 | the purge's locking statement and re-read |
| Purge ∥ Purge | the second answers 404 | the same |
| Restore ∥ a child's mapping or release | one after the other; after a restore, a mapping that saw no parent (the parent was deleted) answers 409 `workitems.link.parent_changed` (BR-H-4) | the child's lock, and the hierarchy lock for a write that carries a parent |
| Purge ∥ a link write of the project (a child's release, a mapping, a save with a parent, a creation beneath) | one after the other; a release that comes second finds the child unparented and has nothing to release; a mapping or a creation that names the purged item as parent answers 404 `workitems.link.parent_not_found` | the hierarchy lock |
| Purge ∥ a team's deletion or unlink | the item and its children are locked by id in the one order; the child ends unowned and unparented either way | ADR-0028 rule 8 |
| Delete or Purge ∥ a team reorder | a deleted item is no member. Reorder first: the Delete waits on its `FOR KEY SHARE` and the place the reorder wrote is left unread; delete first: the reorder's locking read no longer finds the item a member | the reorder's member lock |
| Delete or Purge ∥ the project backlog's reorder | as above, **once the project reorder takes the same member lock** | see below |
| Delete ∥ a creation beneath it, or a mapping naming it as parent | the creation or mapping holds the parent `FOR NO KEY UPDATE`, the Delete `FOR UPDATE` on the same row: the child is created first and kept under the deleted item (BR-DR-4), or the write answers `…link.item_deleted` | the item lock |

The project backlog's reorder (feat-039) reads its members without locking them (`ProjectBacklogRepository.ReadEntriesAsync` is a plain read), and then writes a place for every one. Until now an item could not disappear, so the place's key onto the item could not fail. With a purge it can: the item leaves between the read and the write, and the key fails with a server error. This line gives that reorder the member lock the team reorder already has: the members `FOR KEY SHARE` in id order, before the order row's lock, with the members read again in a locking statement. A Delete then waits for the reorder, or the reorder's locking read no longer finds the item a member, and a purge needs a Delete first. A 23503 on the place's key is mapped to `boards.backlog.reorder_stale` as a backstop, as `WorkItemRepository.SaveAsync` maps the owner key (the locks make it unreachable).

**7. The data of lines to come (BR-DR-16).** A line that gives a work item data does three things.

1. Its writes refuse a deleted item and have rows in rule 3's inventory.
2. Its surfaces choose an entry point of rule 2 and add a row to the surface matrix.
3. Its data leaves with the purge in one of three ways:
   - **A cascading key.** A row that cannot exist without the item (a comment, an attachment's row, a tag's link, a history entry) carries a foreign key to `work_items` with `ON DELETE CASCADE`. This is the default.
   - **A purge step.** A row that must be written on survivors, such as history's "parent purged" note on each child, or an effect outside the database (the files of an attachment), is handled by `IWorkItemPurgeStep`: an interface in `Mustask.Application/Abstractions/WorkItems`, implemented in Infrastructure by the owning line and collected from DI. A step:
     - runs inside the purge's transaction after rule 5 step 2 and before step 5;
     - touches only its own tables, locking in ADR-0028 rule 8's order;
     - does no external I/O: an external effect is an outbox intent written here (ADR-0013);
     - receives the item's id and identifier, the children's ids, the actor and the time, and never content;
     - may add a pre-flight that runs before the transaction, may refuse, and may do I/O. mod-004's refusal while the Drive is unreachable (doc-002 US-10) is one. That is safe because an item's data is frozen while it is deleted (rule 3), so what the pre-flight saw is what the step meets; a restore in between is covered by the outbox.
     - The signature is the plan's, not this ADR's.
   - **Kept.** The data stays by design, listed with its reason in the check.
- **The check** is `WorkItemReferenceInventoryTests`, a model test over `MustaskDbContext` in the family of the `Code` unique-index test (backend.md), which needs no database. It enumerates from the EF model every foreign key whose principal type is `WorkItem`, every property named `WorkItemId` or ending in it that has no such key, and every type a registered step declares it covers. Each must be exactly one of: cascading, cleared by the purge statement (today `parent_id` alone), covered by a step, or kept, listed in the test with a reason. The test also fails on a stale entry and carries a guard against passing vacuously: today's three references (the two place tables and `parent_id`) must be found. Every table is an EF entity (ADR-0009), so a new table cannot hide from the model. The limit is stated: a reference inside a JSON or text column, or in a store outside the database, is seen only through the step that declares it.

**8. Reachability, and ADR-0026.**

| | ADR-0026 (organization, project, team) | This ADR (work item) |
|---|---|---|
| removal is | a status column on the object | the item's state (Removed category) with marks |
| a removal or restore | one row (rule 2) | one row (rule 4) |
| reachability | computed by containment, never stored (rule 3) | the same; a deleted item changes nothing for its children (no cascade) |
| addressed read | not found (rule 5) | read in full, read-only (BR-DR-7) |
| lists and pickers | hidden by an explicit read (rule 4) | hidden by an explicit entry point (rule 2) |
| a write | not found | 409 `workitems.workitem.deleted`, except Restore, Purge and clean-up |
| restore | an organization or project, by the administrator; a team never | a member or administrator, to the held or a chosen state |
| purge | never (doc-010 D-13) | the organization administrator; the MVP's one purge |
| reserved afterwards | names and keys (rule 6) | the number, by the sequence |
| other modules | read the status (rule 7) | read the category |

The two combine: the container first, the item within. An item of a deactivated project or organization answers the container's not-found for every operation, Delete, Restore and Purge included, whatever its state. Its state is unchanged, and when the project is restored so is the item (ADR-0026 rules 3 and 9). Within a reachable project the order is the path's not-founds, the route's code (before the handler's nested not-founds until mod-019, ADR-0027 rule 4; after it, ADR-0029 § 8), the stored-state refusal, and then the body. Hidden projects (feat-041) gate a deleted item as any item.

**9. Permission codes (amends ADR-0029 § 4).** Two codes are registered and checked on the organization role alone until `mod-019-scoped-access` (ADR-0029 "What the delivery lines implement"); belonging to a team grants nothing. They are placed in the matrix here, and the accepted ADR-0029 text is not edited (`docs/adr/README.md`, Process 3):

| Code | Scope | Org A M V | Area Ч И У | Project Ч И У | Team П У | Covers |
|---|---|---|---|---|---|---|
| `workitems.workitem.delete` | P | + ° – | – + + | – + + | – – | Delete and Restore |
| `workitems.workitem.purge` | P | + – – | – – – | – – – | – – | Purge |

`…delete` mirrors `workitems.workitem.write` (spec OQ-1 A). If the human decides OQ-1 (B) at gate-1, this row changes before this ADR is accepted. `…purge` is the administrator's alone: no scoped role holds it, so mod-019 has nothing to scope there. ADR-0029's bullet "`workitem.delete` maps to no code" is thereby fulfilled. Reading a deleted item needs `workitems.workitem.read` as for any item. A team's deletion and an unlink reach deleted items under their own code (ADR-0029 § 6, "Consequence writes").

**10. The pattern for the lines that come.** Only the first rule is specific to work items, whose lifecycle is a state of a process. The others are what later lines copy.

- The marks on the row, all-or-none, with the held state if there is one; the category or the status decides, not the marks.
- Named entry points per aggregate (`Live`, `Deleted`, `InAnyState`) in place of a global filter, and a matrix test over its surfaces.
- The aggregate refuses, the handler places the refusal, an inventory classifies every write.
- The purge: one transaction, ordered locks, cascade first, steps for the rest, the reference check, the log after the commit, no tombstone.
- A number or name reserved by an index or a sequence that no removal rewinds.

mod-008's queries and folders (release 2) and mod-009's dashboards (release 3) have a deleted status of their own (ADR-0026 rule 9), not a process state. They take the last four with the status in the place of the category, and their results take the `Live` entry point for the items they hold, so a query excludes a deleted item unless a clause names «Удалена» (doc-001 BR-14 (a)). Their purge codes mirror `workitems.workitem.purge` (doc-010 D-13). mod-004 stores rows by cascade, files by a step with an outbox intent, and its pre-flight. mod-007's sprint removal is a clean-up of rule 3's third kind that reaches deleted items.

**Convention.** When this ADR is accepted, mod-005's backend stage adds to § "Soft removal" of `docs/conventions/backend.md` a short paragraph that points here: a work item's deletion is its state, reads choose an entry point, the purge and its reference check. ADR-0029's index line then gets "amended by ADR-0030", as ADR-0027's got it from ADR-0029.

## Consequences

- **Simpler:** Delete and Restore are one-row writes, and a restore is exact because the held state is on the row. "Hidden" and "refused" each have one definition and a test that fails when a surface or a route is forgotten. A removed state that a deleted item remembers is refused by a key, with no code. A later line meets a checklist and a failing build instead of a convention.
- **Harder:** `work_items` gains three columns, a CHECK and a key. Every item read model must pick an entry point, and the list gains a filter (mod-003 api-contract § 2.1 changes; the predicate is a subquery on the few Removed states, and no new index is added). `SaveWorkItemHandler` answers the refusal before the values (mod-003 § 0.4 and feat-001 § 3.3 restated). The purge is the one write that locks many rows and the project's hierarchy. The project backlog's reorder gains a member lock, in feat-039's code. A step interface and a model test have to be kept current.
- **Locked in for the long term:** deleted is the Removed category and nothing else. The marks are all-or-none and keep their column names. The purge keeps its lock order. There is no tombstone, so a purge can never be audited back in time: release 2's audit log records from its own start.
- **Owed elsewhere:** the release-3 process-customization line keeps the Removed category fixed per type (rule 1). feat-015 adds the delete and restore entries and the children's note, through a step. feat-003 lists deleted items through `Deleted`. mod-004, feat-002 and feat-016 follow rule 7. mod-007 clears the sprint pointer of deleted items. feat-021's token is not taken by the three writes. Of this line and feat-028, the one that merges second tests the unlink on a deleted item. mod-019 applies the scoped columns of the two rows.
- **Revisit when:** a type may have several states of the Removed category. A retention clock or an undo window for a purge is wanted (BR-DR-5 rules it out now). A store outside the database cannot take its deletion as an intent. The hiding predicate shows in profiling at the NFR volumes. An audit trail of purges is wanted before release 2, which would need an event record under feat-015's ADR.

## Decisions for the human at gate-2

- [x] **D-1 Where the deletion's marks live.** (A, recommended) Three nullable columns on `work_items`, with the all-or-none CHECK and the held-state key (rule 1): one-row writes, no join, and the state-removal guard is a database fact. (B) A side table `work_item_deletions`: `work_items` stays as it is, and the table is a ready list for feat-003, but every read joins it, and "marks without the state" and the reverse must be policed without a CHECK. decided by the human in chat on 2026-10-18: A («⭐ Принять, все A»).
- [x] **D-2 How "hidden by default" is made safe.** (A, recommended) Named entry points per read model, the parity test and the surface matrix test (rule 2): the same mechanism as ADR-0026 rule 4, and it works with ADR-0029's caller-aware reads. (B) A global EF query filter on `WorkItem` with `IgnoreQueryFilters` at the explicit reads: a forgotten condition cannot leak, but it is a work-item-only exception to the accepted ADR-0026 rule 4, every explicit read also drops any filter added later, and ADR-0029's reads need a second mechanism anyway. decided by the human in chat on 2026-10-18: A («⭐ Принять, все A»).
- [x] **D-3 How much of the purge's hook is built now.** (A, recommended) The cascade default, the `IWorkItemPurgeStep` contract (with a fake step in the purge handler's tests) and the reference check, all now (rule 7): the first line with data only implements a step. (B) The cascade default and the check now, and the step contract with the first line that needs one: less code now, but that line designs the contract under this ADR's obligations with no precedent in the code. decided by the human in chat on 2026-10-18: A («⭐ Принять, все A»).
