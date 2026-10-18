# Plan — backend (mod-005-work-item-delete-restore)

Stage 4a, deep pass (L). No production code yet. It covers the backend half of **US-1..US-6** and fixes one
interface, [api-contract.md](api-contract.md). The three dialogs, the deleted card and the menu belong to stage 6
(`plan-frontend.md`, not yet written); the wire shapes they need are in the contract's § 1.

Inputs: [spec.md](spec.md) (BR-DR-1..-18, § Surfaces and writes), the stories
[US-1](stories/US-1-delete-from-the-card.md)..[US-6](stories/US-6-purge.md), [design.md](design.md) § 12,
[review.md](review.md), **[ADR-0030](../../adr/0030-work-item-recycle-bin.md) (accepted 2026-10-18)** and the ADRs it
stands on: 0026 (rules 2-5, 9), 0027 (rule 4), 0028 (rule 8's lock order), 0029 (§ 4, § 7, § 8), 0010, 0011, 0012,
0013; `docs/conventions/backend.md`, `src/backend/AGENTS.md`. The code was read, not assumed (branch tip `ecf441f0`,
main at `ce7f055b`): `WorkItem`, `WorkItemHierarchy`, `WorkItemTypeStates`, `SaveWorkItemHandler`,
`SetWorkItemParentHandler`, `CreateWorkItemHandler`, `GetWorkItemHandler`, `WorkItemRepository`, `WorkItemReadModel`,
`WorkItemRefs`, `WorkItemTeamOwnership`, `WorkItemAreaUsage`, `WorkItemFilterPredicate`, `BacklogMembership`,
`ProjectBacklogMembership`, `BacklogReadModel`, `BacklogRepository`, `ProjectBacklogRepository`,
`TeamBacklogPlaces`, `ReorderProjectBacklogHandler`, `PermissionCatalog`, `OrganizationRoleBundles`,
`WorkItemEndpoints`, `OrganizationAddressedRoutes`, `OrganizationAccessInventoryTests`,
`ProcessStateWritesRules`, the merged migrations, the error catalog and the test helpers (`CommitGate`, `PgLocks`,
`LinksTestData`, the statement-count tests).

- **ADR:** none new. ADR-0030 settles the model; this plan fixes what it leaves to the plan (the signature of the
  step contract, the routes, the wire shape, the test shapes) and adds two text rules and one widening of a test
  that the ADR names (D-P4, D-P5). Nothing here edits an accepted ADR.
- **Lanes: no** (04-backend.md § 4c). Three use cases, but they share the domain change, the migration, the read
  entry points and the permission codes; the work is deep, not wide. One sequential line of green commits (§ 10).
- **E2E:** the standing answer is NO (paused 2026-10-12). Not asked, not run; the specs the change breaks are listed
  in § 9.6 and not edited.
- **No code, comment, test name, log line or commit names the reference tracker.**
- **feat-028** (teams of the organization) is in flight and not in main. This plan builds on main as it is; the
  project–team unlink does not exist here, so BR-DR-9's unlink test is owed by whichever of the two merges second
  (§ 11).

## 1. Decisions of the plan

Decisions where the spec or the ADR leave room. None changes a rule. Those marked **for the human** are low-stakes
and the recommended option is taken; they go to the report.

| # | Decision | Why |
|---|---|---|
| D-P1 | **Routes: three `POST` sub-commands** under the item: `…/workitems/{identifier}/delete`, `/restore`, `/purge`. `DELETE …/{identifier}` is not used. Delete and Restore answer 200 `WorkItem`, Purge 204. **For the human.** (A, taken) POST sub-commands, as the project's `deactivate` and `restore` are. (B) `DELETE` for Delete and `DELETE …?permanent=true` for Purge: terser, but one `DELETE` verb would mean two different things on the same resource and a client that misses the query would purge. | `restore` already exists as a POST sub-command on the project and the organization (mod-002); "delete" here is a state change, not the removal of the resource, and only Purge removes it. The panel's mutations return the item for Delete and Restore (design § 12). |
| D-P2 | **The deletion marks go out as one nullable object `deletion` on `WorkItem`**: `{ fromState, by, at }`, `by` = `{ id, displayName, isActive, isMember }`, `null` for a live item and for an item in «Удалена» with no marks. **For the human.** (A, taken) as design § 12 asks ("held state, deleter as a user reference with its marks, time"). (B) three flat fields: no gain, and the three are all-or-none by the CHECK. | One object cannot be half-present, as the CHECK keeps the columns. `isActive` and `isMember` are the two marks of BR-13 the card needs (design § 4); feat-017 later adds the same pair to `assignee` with one helper. |
| D-P3 | The deletion marks are read **in the item's own statement**: a left join of `state_definitions` on `deleted_from_state_id`, and the deleter looked up in the same directory call as the assignee. The list's statement carries the same join (50 rows, all null). **No statement is added**: `ItemGetStatements = 7` and `ListStatements = 7` stay pinned. | NFR "fixed number of queries"; the pinned counts are decisions (`OwningTeamStatementCountTests`). |
| D-P4 | **The write inventory takes every write route of the organization-addressed set**, not only the routes that carry an item identifier or name item ids: each of the ~25 POST/PUT/PATCH/DELETE routes needs a row, and an unrelated route's row is "names no work item". **For the human.** (A, taken) the fail-closed shape of `OrganizationAccessInventoryTests`: nothing to decide which body "names work items". (B) only `{identifier}` routes plus a hand list: a new bulk route whose body names item ids passes unseen. | ADR-0030 rule 3 asks for the routes that carry an identifier or name item ids in the body. A body cannot be inspected for that without a marker nobody would forget to leave off, so the test takes the superset. Cost: about twenty "names no work item" rows in one block. |
| D-P5 | **One more text rule: reads go through the named entry points.** In `Mustask.Infrastructure`, the context's `WorkItems` set and `Set<WorkItem>()` are named only in `WorkItemSets.cs` (and the two membership classes, listed with their reason). ADR-0030 rule 2 leaves a forgotten entry point to the surface matrix test alone; this makes the forgetting a build failure for the common shape. | A second line of defence at the price of one architecture test, in the family of `ProcessStateWritesRules`. It is a text rule and says so. |
| D-P6 | **A partial index** `ix_work_items_deleted_from_state_id ON work_items (deleted_from_state_id) WHERE deleted_from_state_id IS NOT NULL`, declared in the configuration, so EF adds no conventional full index for the new foreign key. | ADR-0030 adds "no new index" for the list's predicate, and this is not one: it serves the key's own restrict check and release 3's "does a deleted item remember this state". A full index would carry a null per live row. |
| D-P7 | The purge removes the item with **EF `Remove` + `SaveChanges` of the one tracked, locked row**, and its children's link with one raw set-based `UPDATE`. | ADR-0030 rule 5 step 5 says `DELETE`; the effect is the same row. One tracked delete keeps the `xmin` check and keeps `DELETE FROM work_items` out of the code, so the writers rule (§ 6.4) has two raw `UPDATE`s to allow (the team release and this one). |
| D-P8 | A restore's held state that is not allowed by the type any more (cannot happen in the MVP) **falls back to the type's first state**, never a 500. | BR-DR-10 "stays a state of the type"; the foreign key and release 3 keep it so, the fallback keeps a defect from becoming a refusal the user cannot act on. A domain test pins the fallback. |
| D-P9 | The purge reads the item **once without a lock before the transaction** (for the early `not_deleted` and for the pre-flights), then decides again under the locks. | ADR-0030 rule 5. Pre-flights may do I/O, which is forbidden inside a transaction (backend.md, EF Core). |

## 2. Affected slices

| Area | Change |
|---|---|
| `Mustask.Domain/WorkItems` | `WorkItem`: three marks, `IsDeleted`, `EnsureLive`, `EnsureDeleted`, `Delete`, `RestoreTarget`, `Restore`; `Save` calls `EnsureLive` first. `WorkItemTypeStates`: `RemovedStateIds`, `RemovedStateId`, `IsRemoved`, `FirstStateId`; `Allows` reads from them. `WorkItemErrorCodes`: two codes. |
| `Mustask.Domain/Identity` | `PermissionCatalog`: `workitems.workitem.delete`, `workitems.workitem.purge`; `OrganizationRoleBundles`: the member bundle gets `…delete`, the admin bundle gets both (it is the whole organization set). |
| `Mustask.Application/Abstractions/WorkItems` | `IWorkItemRepository` (purge locks and writes), `IWorkItemReadModel` (`WorkItemView.Deletion`, `WorkItemHead.StateId`, the two view records), **new** `IWorkItemPurgeStep`, `WorkItemPurgeRequest`, `WorkItemPurge`, `PurgeStepCoversAttribute`, **new** `ProcessWorkItemTypes` (the item's type as `WorkItemTypeStates`, moved out of `SaveWorkItemHandler`); `ProcessHierarchy.Of` builds each type's removed ids from the same `WorkItemTypeStates`. |
| `Mustask.Application/Abstractions/Boards`, `Features/Boards/ReorderProjectBacklog` | `IProjectBacklogRepository.LockAsync` takes the scope (the member lock); the handler passes the scope it already builds. |
| `Mustask.Application/Features/WorkItems` | **new slices** `DeleteWorkItem/`, `RestoreWorkItem/`, `PurgeWorkItem/`; `SaveWorkItem/` places `EnsureLive`. Nothing else: `SetWorkItemParent`, `CreateWorkItem`, `GetWorkItem*` keep their text. |
| `Mustask.Infrastructure/WorkItems` | `WorkItemConfiguration` and `WorkItemsConstraints` (columns, key, CHECK, partial index); **new** `WorkItemSets`; `WorkItemReadModel` (entry points, the item's `Deletion`, the list on `Live`), `WorkItemRefs` (the removed-ids subquery moves into `WorkItemSets`), `WorkItemRepository` (purge members; `InAnyState`), `WorkItemTeamOwnership` and `WorkItemAreaUsage` (`InAnyState`); one migration. |
| `Mustask.Infrastructure/Boards` | `BacklogReadModel` (`Live` for checklists and live parents), `ProjectBacklogRepository` (member lock, locking read), comments of the two memberships. |
| `Mustask.Api/Features/WorkItems` | `WorkItemEndpoints`: three routes. `Mustask.Application/DependencyInjection.cs`: three handlers. |
| `contracts/errors`, `docs/error-codes.md` | `workitems.json` +2 codes; the reference regenerated. |
| `docs` | `conventions/backend.md` § "Soft removal", the index line of ADR-0029 ("amended by ADR-0030"). |

## 3. Domain (`Mustask.Domain`)

```csharp
// WorkItem — three nullable properties, private setters, written only by Delete and Restore (ADR-0030 rule 1)
public Guid? DeletedFromStateId { get; private set; }     // the state held
public string? DeletedBy { get; private set; }            // the Keycloak sub, no key
public DateTimeOffset? DeletedAt { get; private set; }    // not ChangedAt: a clean-up moves ChangedAt of a deleted item

public bool IsDeleted(WorkItemTypeStates type);            // type.IsRemoved(StateId); RequireOwnType first
public void EnsureLive(WorkItemTypeStates type);           // Conflict workitems.workitem.deleted
public void EnsureDeleted(WorkItemTypeStates type);        // Conflict workitems.workitem.not_deleted
public void Delete(WorkItemTypeStates type, string deletedBy, DateTimeOffset now);
public Guid RestoreTarget(WorkItemTypeStates type);        // the held state if the type allows it, else the first (D-P8)
public void Restore(WorkItemTypeStates type, Guid? chosenStateId, DateTimeOffset now);
```

- **`Delete`**: `EnsureLive`; then, in one block, `DeletedFromStateId = StateId; StateId = type.RemovedStateId;
  DeletedBy = deletedBy; DeletedAt = now; ChangedAt = now`. Nothing else is assigned (BR-DR-3). `deletedBy` is
  non-empty and at most `WorkItemLimits.UserIdMaxLength` (`ArgumentException` otherwise: the caller is the token).
- **`Restore`**, in the order BR-DR-10 fixes: `EnsureDeleted` (`not_deleted`); then, when a state is chosen and
  `!type.Allows(chosen)`, `state_not_allowed`; then `StateId = chosen ?? RestoreTarget(type)`, the three marks to
  `null`, `ChangedAt = now`. A refusal throws before anything is assigned. `Guid.Empty` stands for a sent value that
  named nothing (as `SeenId()` does) and is never allowed.
- **`Save`** calls `EnsureLive(type)` right after its argument and own-type guards, before `CheckFields`, the state and
  the assignee are looked at. This closes tech-012 m1 at the aggregate: the stored state is judged before any value is
  applied. `WorkItemHierarchy.Write` and `CreateChild` keep their `ItemDeleted` checks (the link routes' code, BR-DR-8).
- **`WorkItemTypeStates`** gains `RemovedStateIds` (the states with category `Removed`, from `States`), `RemovedStateId`
  (`Single`; `InvalidOperationException` when the type does not have exactly one: "Delete relies on one Removed state
  per type", ADR-0030 rule 1; the constructor does not enforce it, many domain tests build types without one),
  `IsRemoved(Guid)` and `FirstStateId` (the state at `StateDefinition.InitialPosition`, which `InitialStateId` already
  is: one is the other). `Allows(stateId)` = in the type's states and not `IsRemoved`.
- **One in-memory list of removed ids** (ADR-0030 rule 2): `ProcessHierarchy.Of` builds each `HierarchyType` from the
  `WorkItemTypeStates` of the same type (`ProcessWorkItemTypes.StatesOf(typeRow)`), so `HierarchyTypes.IsDeleted` and
  `WorkItemTypeStates.IsRemoved` read one list. `HierarchyType.RemovedStateIds` stays a record field.
- **Error codes** (`WorkItemErrorCodes`, `[ErrorCodes]`): `WorkItemDeleted = "workitems.workitem.deleted"`,
  `WorkItemNotDeleted = "workitems.workitem.not_deleted"`. Entries in `contracts/errors/workitems.json` (409, no
  params, `kkNeedsReview`), texts in the contract's § 4.
- **Permission codes** (`PermissionCatalog`): `WorkItemsWorkItemDelete`, `WorkItemsWorkItemPurge`, both
  `PermissionScope.Organization`, descriptions «Удаление и восстановление рабочих элементов проекта» and
  «Окончательное удаление рабочих элементов проекта». Bundles: member += `…delete`; admin holds the whole set
  (`Admin` is built from `OrganizationCodes`); viewer none. ADR-0029's rows are placed by ADR-0030 rule 9; mod-019
  applies the scoped columns.
- **Domain tests** are § 9.1.

## 4. Database

### 4.1 Entity configuration (`WorkItemConfiguration`, `WorkItemsConstraints`)

- `DeletedFromStateId` (uuid, null), `DeletedBy` (`HasMaxLength(WorkItemLimits.UserIdMaxLength)`, null), `DeletedAt`
  (`timestamptz`, null).
- `HasOne<StateDefinition>().WithMany().HasForeignKey(i => i.DeletedFromStateId).OnDelete(Restrict)` — a second key
  to the same principal as `StateId`; constraint `fk_work_items_state_definitions_deleted_from_state_id` (the naming
  convention's). This is the database's half of doc-001 BR-14 (b): a release-3 state removal is refused while an item
  remembers the state.
- `HasCheckConstraint("ck_work_items_deletion_marks", "(deleted_from_state_id IS NULL AND deleted_by IS NULL AND
  deleted_at IS NULL) OR (deleted_from_state_id IS NOT NULL AND deleted_by IS NOT NULL AND deleted_at IS NOT NULL)")`.
- The partial index of D-P6, named in `WorkItemsConstraints`.
- Constants added to `WorkItemsConstraints`: `DeletionMarksCheck`, `DeletedFromStateKey`, `DeletedFromStateIndex`.

### 4.2 Migration `AddWorkItemDeletionMarks` (additive, reversible)

Generated by `dotnet ef migrations add AddWorkItemDeletionMarks --project src/backend/Mustask.Infrastructure`
(runbook: `docs/runbooks/local-migrations.md`). `Up`: three `AddColumn`, the key, the index, the CHECK. `Down`: the
reverse, in reverse order. **No data moves and nothing is dropped** (BR-DR-18): nothing is in «Удалена» before this
ships. A stand that holds an item written into «Удалена» by hand is an item with no marks: deleted by its category,
restorable to its type's first state (BR-DR-10). The model has no pending changes afterwards (the test of the same
family as `AddProjectBacklogOrderMigrationTests.Model_HasNoPendingChanges_AfterTheMigrations`).

**Rebase note** (backend.md, "Parallel branches with migrations"): feat-028 is in flight and may add a migration
touching `work_items`. If main brings a migration before the PR, this one is deleted and regenerated on the fresh
snapshot; it is never edited after it lands.

### 4.3 What is not stored

No tombstone, no copy (BR-DR-15); `work_item_counters` is never decremented (BR-DR-14); no job (BR-DR-5).

## 5. Endpoints

All three are organization-addressed, behind `RequiresOrganizationPermission`, tag `WorkItems`, and carry
`ProducesProblem` for 401, 403, 404 and 409 (Purge: also 204). Full shapes are in the contract.

| Route | Code | Slice | Answer |
|---|---|---|---|
| `POST …/projects/{projectId}/workitems/{identifier}/delete` | `workitems.workitem.delete` | `DeleteWorkItem` | 200 `WorkItem` |
| `POST …/workitems/{identifier}/restore` (body `{ "stateId"? }`) | `workitems.workitem.delete` | `RestoreWorkItem` | 200 `WorkItem` |
| `POST …/workitems/{identifier}/purge` | `workitems.workitem.purge` | `PurgeWorkItem` | 204 |

- `OrganizationAddressedRoutes.All` gets three rows **right after the item routes** (the mod-003 block), not at the end:
  `Memberships => All.Skip(All.Count - 10)` takes the last ten rows. The count in
  `TheScopedRoutes_AreExactlyTheListedOnes…` goes 47 → 50 (49 at the commit of Restore, 48 at Delete).
- Each code arrives **with its first route in the same commit**: `EveryOrganizationCode_IsDeclaredByAtLeastOneRoute`
  fails for a code with no route.
- `WorkItemsOpenApiTests.ContractOperations` gets the three operations.
- DI: three scoped handlers in `Mustask.Application/DependencyInjection.cs`; `IEnumerable<IWorkItemPurgeStep>` is
  empty (no step is registered in this line).

## 6. Reads: the named entry points (ADR-0030 rule 2)

### 6.1 `Mustask.Infrastructure/WorkItems/WorkItemSets.cs` (internal, static)

```csharp
IQueryable<Guid> RemovedStateIds(MustaskDbContext c);        // the one LINQ copy: StateDefinitions where Category == Removed
IQueryable<WorkItem> InAnyState(MustaskDbContext c);          // c.WorkItems
IQueryable<WorkItem> Live(MustaskDbContext c);                // InAnyState where !RemovedStateIds.Contains(StateId)
IQueryable<WorkItem> Deleted(MustaskDbContext c);             // InAnyState where  RemovedStateIds.Contains(StateId)
```

The queries are not `AsNoTracking`: the caller adds it. `Deleted` has no caller in this line (feat-003's); it is
tested (§ 9.3) so that the pair `Live`/`Deleted` is proven a partition. The backlog and board memberships keep their
own single place (`BacklogMembership`, `ProjectBacklogMembership`, with the scope's `ShownStateIds`) and stay inside the
parity test.

### 6.2 Every read, moved

| Read | Today | After |
|---|---|---|
| `WorkItemReadModel.GetPageAsync` (list, its `count`) | every state | **`Live`** (the one behaviour change of the list; the team filter composes on it; same statements) |
| `FindParentCandidatesAsync` | joins `state_definitions` for the category | **`Live`**; the state join goes (the Ref rows join the state for the view anyway) |
| `FindAsync`, `GetAsync`, `FindHeadAsync`, `GetRefAsync` (the card by address, a write's answer, a parent's ref) | `context.WorkItems` | **`InAnyState`**; `FindAsync`/`GetAsync` also read the item's `Deletion` (D-P3) |
| `LinksAsync` (the card's «Родитель», «Дочерние элементы») | one statement, an inline `!removed.Contains` | one statement: `InAnyState` where id is the parent, **`Concat`** `Live` where the item is the parent. If EF cannot translate a set operation under `RefRows`' joins, the fallback is one `InAnyState` query with the predicate sourced from `WorkItemSets.RemovedStateIds`, with a comment saying why. Either way `ItemGetStatements` stays 7 |
| `BacklogReadModel.ReadChecklistsAsync` | joins state, `!= Removed` | **`Live`**; the state join stays for the view only |
| `BacklogReadModel.FindLiveParentsAsync` | `!removed.Contains` via `WorkItemRefs.RemovedStateIds` | **`Live`** |
| `BacklogMembership.Members/TreeMembers`, `ProjectBacklogMembership.Members/TreeMembers` | `ShownStateIds` | unchanged; listed in D-P5's allow-list and in the parity test |
| `BacklogRepository.LockAsync/ReadEntriesAsync` (raw SQL) | `state_id = ANY(shown)` | unchanged; in the parity test |
| `WorkItemTeamOwnership.CountOwnedAsync` | any state | **`InAnyState`** (BR-OT-12) |
| `WorkItemAreaUsage.IsInUseAsync` | any state | **`InAnyState`** (BR-M3-24, until mod-018) |
| `WorkItemRepository`: `FindCreatedByRequestAsync`, `LockAsync`'s load, `LockLinkEndAsync`'s load | `context.WorkItems` | **`InAnyState`**; `AncestryAsync` and `SubtreeHeightAsync` (raw SQL) count stored links through a deleted item, by design (BR-DR-10) |
| `WorkItemRefs.RemovedStateIds` | the subquery | deleted; its two callers use `WorkItemSets` |

### 6.3 Parity and the surface matrix

Tests in § 9.3. The parity test pins the three expressions of "deleted" equal (in memory, LINQ, raw SQL and
memberships); the surface matrix test reads every surface of the spec's table.

### 6.4 The two text rules (`Mustask.Architecture.Tests/WorkItemAccessRules.cs`)

In the shape of `ProcessStateWritesRules`: comment lines dropped, patterns on the text, a not-vacuous test with a
planted violation for each pattern.

1. **Reads (D-P5).** `using` and `namespace` lines are dropped with the comments (a namespace is `…Features.WorkItems`).
   Outside `Mustask.Infrastructure/WorkItems/WorkItemSets.cs`, the allow-listed `Boards/BacklogMembership.cs`,
   `Boards/ProjectBacklogMembership.cs`, `MustaskDbContext.cs` and the migrations, no source names the context's set
   (`\b\w*[cC]ontext\.WorkItems\b`) or `Set<WorkItem>\s*\(`. (`WorkItemRepository.Add` becomes `context.Add(item)`.)
2. **Writes (ADR-0030 rule 2).** Outside `Mustask.Infrastructure/WorkItems/` and the migrations, nothing names
   `UPDATE work_items`, `DELETE FROM work_items` or `INSERT INTO work_items`; the set's own writes
   (`ExecuteUpdate`, `ExecuteDelete`, `Remove`, `Add`) cannot occur there, because they must name the set and rule 1
   forbids that. Inside, a raw `UPDATE work_items` statement (up to its closing quotes) names neither `state_id` nor
   `deleted_`: only the aggregate writes them. Today's raw writers inside are the team release and the purge's
   `parent_id = NULL`.

## 7. Handlers, locks, answers

Common: the address is resolved as `SaveWorkItemHandler` does, from the outside in (organization → project reachable
and of the organization → identifier well-formed with the project's key → item → type key), every miss the level's
not-found. The route's code is checked before the handler (ADR-0027 rule 4); a non-member gets «Организация не найдена.»
there. Time from `IClock`, the actor from `ICurrentUser.SubjectId`.

### 7.1 `DeleteWorkItem`

One transaction. **Locks: the item `FOR UPDATE` and nothing else** (`IWorkItemRepository.LockAsync(projectId, number)`,
then the tracked load): no team, no hierarchy, no other item, no place (ADR-0030 rule 4). Steps: address → process →
`LockAsync` (null → 404) → type from `ProcessWorkItemTypes` (key differs → 404) → `item.Delete(type, subject, now)`
(409 `…deleted`) → `SaveAsync` → `GetAsync`. A refusal throws before the flush and the transaction rolls back.

### 7.2 `RestoreWorkItem`

Same lock and steps; the body's `stateId` string maps to `null` (absent), the parsed id, or `Guid.Empty` (not an id).
`item.Restore(type, chosen, now)`: `not_deleted`, then `state_not_allowed`, then the write. **No hierarchy check**
(BR-DR-10): nothing is read of a parent, a child or a place. The backlog places were never touched, so an item comes
back where it stood unless a reorder dropped its place (BR-DR-10, ADR-0030 rule 4).

### 7.3 `PurgeWorkItem`

```
 1  address (as above), process
 2  head = IWorkItemReadModel.FindHeadAsync(projectId, number)           -- no lock; null or wrong type key: 404
 3  if the head's state is not Removed: 409 not_deleted                  -- early; the transaction decides again
 4  foreach step: await step.PreflightAsync(request)                     -- outside the transaction; may refuse, may do I/O
 5  tx:
    a  LockHierarchyAsync(projectId)                                     -- ADR-0028 rule 8 step 5, as every link writer
    b  ids = LockForPurgeAsync(projectId, head.Id)                       -- the item and its direct children, deleted ones too,
                                                                           ONE statement ORDER BY id FOR NO KEY UPDATE
    c  item = FindLockedAsync(...) ?? 404; item.EnsureDeleted(type)      -- 409 not_deleted: a restore got there first
    d  foreach step: await step.PurgeAsync(purge)                        -- inside, own tables only
    e  teamPlaces.DropForItemAsync(item.Id)                              -- before the delete: the place's SQL-only owner key
    f  ReleaseChildrenAsync(projectId, item.Id, now)                     -- UPDATE parent_id = NULL, changed_at = now WHERE parent_id = :id
    g  RemoveAsync(item) + SaveChanges                                   -- the project place goes by its cascading key
 6  after the commit, never inside: one log line (user, organization, project, identifier; no title)
 -> 204
```

- **Why this order** (ADR-0030 rule 5): the hierarchy lock stabilizes the children and keeps every cycle or depth
  check from reading a link being removed; one ordered statement is the order every other multi-item locker keeps
  (`WorkItemTeamOwnership.LockOwnedAsync` is the model), so a purge and a team's deletion or unlink never deadlock;
  `FOR NO KEY UPDATE` is a parent write's link-end mode, so a reorder's `FOR KEY SHARE` is never waited on.
- **The one stronger lock.** The final delete takes the item's row at `FOR UPDATE` strength, an upgrade from `NO KEY
  UPDATE`; it waits only for `KEY SHARE` holders of that very row. None can exist: a reorder holds `KEY SHARE` on
  members, and a deleted item is no member (its locking read excludes it), and a reorder that was a holder when the
  Delete ran made the Delete wait for its commit (`FOR UPDATE` conflicts with `KEY SHARE`). A creation or a mapping
  naming the item as parent holds the hierarchy lock the purge holds. § 8.2 has the argument as a test.
- **`LockForPurgeAsync`** reads `SELECT id AS "Value" FROM work_items WHERE project_id = :p AND (id = :id OR parent_id =
  :id) ORDER BY id FOR NO KEY UPDATE`; the item's own id is taken out of the answer, the rest are `ChildIds`. The
  children are a stable set under the hierarchy lock.
- **The step contract** (D-3 A, rule 7), in `Mustask.Application/Abstractions/WorkItems`:

  ```csharp
  public interface IWorkItemPurgeStep
  {
      /// Before the transaction. May refuse (a ConflictException) and may do I/O; sees no child ids.
      Task PreflightAsync(WorkItemPurgeRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

      /// Inside the transaction, after the item and its children are locked and before the item is removed. Touches only its
      /// own tables, locking in ADR-0028 rule 8's order; does no external I/O (an outbox intent instead, ADR-0013).
      Task PurgeAsync(WorkItemPurge purge, CancellationToken cancellationToken);
  }

  public sealed record WorkItemPurgeRequest(Guid OrganizationId, Guid ProjectId, Guid WorkItemId, string Identifier, string ActorId, DateTimeOffset At);
  public sealed record WorkItemPurge(WorkItemPurgeRequest Request, IReadOnlyList<Guid> ChildIds);   // ids and identifier, never content

  [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
  public sealed class PurgeStepCoversAttribute(Type entity) : Attribute { public Type Entity { get; } = entity; }
  ```

  The step class is implemented in Infrastructure by the owning line, registered `AddScoped<IWorkItemPurgeStep, …>`,
  collected as `IEnumerable<IWorkItemPurgeStep>` in registration order, and marked `[PurgeStepCovers(typeof(Comment))]`
  for every entity it handles, which is what the reference check (§ 9.3) reads without a container.
- **The log line**: `ILogger<PurgeWorkItemHandler>`, structured, e.g. `Work item {Identifier} purged by {UserId} in
  project {ProjectId} of organization {OrganizationId}`. Never the title or any content. A rolled-back purge logs
  nothing.
- **Accepted** (ADR-0030): the counter row is not decremented; the creation-request key goes with the row, so a replay
  of an old creation after a purge creates a new item.
- **Idempotence**: a second purge, a restore or a save after a purge finds no row: 404 `workitems.workitem.not_found`.

### 7.4 `SaveWorkItem` (and what does not change)

`item.EnsureLive(type)` is placed **right after the item is locked and its type is resolved, before `ValidateValues`**
(`SaveWorkItemHandler`, between steps 1 and 2). The class remarks' "order of answers" gain the refusal as step 1a. The
locks before it are unchanged (team, hierarchy), so a save of a deleted item that names a team or a parent locks them
first and is then refused; harmless, and the 403 for the second code stays first of everything. `WorkItem.Save` calls
`EnsureLive` again (the domain does not rely on the handler). A form save of a deleted item answers `…deleted`
whatever it carries, `{}` and a named parent included; a live item's save that names a deleted parent keeps
`workitems.link.item_deleted` from `WorkItemHierarchy.Write`. **tech-012 m1 is closed** (its queue line keeps m4):
`status.md` Decisions gets the line when the code lands.

`SetWorkItemParentHandler`, `CreateWorkItemHandler` (a deleted parent → `item_deleted`; a replay answers the item as
stored, deleted or not) and the team deletion are unchanged in text.

### 7.5 Project backlog reorder: the member lock (ADR-0030, "the finding")

`ProjectBacklogRepository.ReadEntriesAsync` is a plain read today, and `SaveOrderAsync` then writes a place for every
member. Until now an item could not disappear in between; with Delete and Purge it can, and the place's key fails with a
server error. The reorder takes the team reorder's lock (rule 8 step 5, then 6), and the order of the repository
becomes: project `FOR SHARE` → reachable? → **members `FOR KEY SHARE` in one statement `ORDER BY id`** → order row
`FOR UPDATE` → (handler) members read **in a locking statement** → write.

- `IProjectBacklogRepository.LockAsync(Guid organizationId, ProjectBacklogScope scope, CancellationToken)`; the handler
  already builds the scope before the transaction. The member statement: `SELECT 1 FROM work_items WHERE project_id =
  :p AND type_id = ANY(:types) AND state_id = ANY(:shown) ORDER BY id FOR KEY SHARE`.
- `ReadEntriesAsync` becomes a raw locking read of the same membership (the reorder scope carries no team filter),
  with `created_at` for the order's tie-break: `SELECT item.id, item.number, item.created_at, CASE WHEN
  place.project_id = :p AND place.backlog_level_id = :l THEN place.position END FROM work_items item LEFT JOIN
  project_backlog_places place ON place.work_item_id = item.id WHERE … ORDER BY item.id FOR KEY SHARE OF item`. A test
  pins it to `ProjectBacklogMembership.Members` (the parity test; the team repository has the same pin).
- **Backstop:** `SaveOrderAsync` maps a `PostgresException` 23503 on `fk_project_backlog_places_work_items_work_item_id`
  to 409 `boards.backlog.reorder_stale` (the raw statements throw the `PostgresException` itself, not a
  `DbUpdateException`; the helper gets an overload). The locks make it unreachable.
- Cost: `ProjectBacklogStatementCountTests.ReorderStatements` goes 12 → 13 (the one lock statement), pinned on
  purpose; the 5,000-item reorder stays within 1 s (NFR of feat-039; the existing volume test).

## 8. Races (ADR-0011 echelon 3, ADR-0028 rule 8)

### 8.1 One order of locks

Each write takes only what it needs, in ADR-0028 rule 8's order: (2) team rows → (5) the project's number and
hierarchy rows, then work items **by id** → (6) order rows.

| Writer | Locks, in order |
|---|---|
| Save | team `FOR SHARE` (if named) → hierarchy `FOR UPDATE` (if a parent) → item `FOR UPDATE` → area `FOR SHARE` → new and stored parent `FOR NO KEY UPDATE` |
| **Delete, Restore** | item `FOR UPDATE` |
| **Purge** | hierarchy `FOR UPDATE` → the item and its children by id, one statement, `FOR NO KEY UPDATE` → (rows it changes: the item's team place, the children's `parent_id`, the item) |
| Parent write | hierarchy → item → new and stored parent |
| Creation (with a parent) | team → number → hierarchy → parent `FOR NO KEY UPDATE` |
| Team deletion | team `FOR NO KEY UPDATE` → the owned items by id `FOR UPDATE` → place rows |
| Team reorder | team `FOR SHARE` → members `FOR KEY SHARE` by id → order row → members again |
| **Project reorder** | project `FOR SHARE` → members `FOR KEY SHARE` by id → order row → members again |

### 8.2 The races, and why each ends in one order

A request that locks second re-reads under its lock and decides on what it finds. "Both orders" means a test per
order (§ 9.5). The first block is ADR-0030 rule 6; the rest are the pairs it does not spell out.

| Pair | Outcome | Why |
|---|---|---|
| Delete ∥ Delete | the second answers 409 `…deleted`; the held state is the first's | the item lock; `Delete` judges the tracked row after it |
| Delete ∥ Save of the item | save first: the delete records the state the save left; delete first: the save answers 409 `…deleted` | same lock; `EnsureLive` is the save's first act under it |
| Restore ∥ Restore; Restore ∥ Save | the second restore 409 `…not_deleted`; a save decided second lands on the restored item | same lock |
| Restore ∥ Purge | restore first: the purge answers 409 `…not_deleted` (step 5c); purge first: the restore finds no row, 404 | the purge's locking statement and re-read |
| Purge ∥ Purge | the second answers 404 | same |
| Delete ∥ Purge | purge before the Delete commits: 409 `…not_deleted` (its early read, step 3, sees the committed live state); after: it proceeds | step 3 is a plain read; READ COMMITTED |
| Restore ∥ a child's mapping or release | one after the other; after a restore a mapping that saw no parent answers 409 `workitems.link.parent_changed` (BR-H-4) | the child's lock and the hierarchy lock |
| Purge ∥ a link write of the project (a child's release, a mapping, a save with a parent, a creation beneath) | one after the other; a release that comes second finds the child unparented; a mapping or creation naming the purged item answers 404 `workitems.link.parent_not_found` | the hierarchy lock |
| Purge ∥ a team's deletion or unlink | the item and its children are locked by id in one order; the child ends unowned and unparented either way | rule 8 (both lock ascending ids, so the lowest id they share is taken first by one of them) |
| Delete ∥ a team's deletion releasing the item | the team deletion's `LockOwnedAsync` waits on the Delete's row; it then releases the now-deleted item and moves `changed_at`, the marks untouched | the item lock |
| Delete ∥ a team reorder | reorder first: the Delete waits for its `KEY SHARE`, and the place the reorder wrote is left unread (a deleted item is no member); delete first: the reorder's locking read no longer finds the item | the member lock (mod-017) |
| Delete ∥ a project reorder | as above, **now that the project reorder takes the same lock** (§ 7.5) | the member lock |
| Purge ∥ a project or team reorder | a purge needs a prior Delete, so the item is no member; a reorder that began earlier made the Delete wait. The reorder's drop of the item's stale place and the purge's drop of it queue on the place row; the reorder holds no lock the purge needs | place row, no cycle |
| Delete ∥ a creation beneath it, or a mapping naming it as parent | the creation holds the parent `FOR NO KEY UPDATE`, the Delete `FOR UPDATE` on the same row: the child is created first and kept under the deleted item (BR-DR-4), or the write answers `workitems.link.item_deleted` | the item lock |
| Save of a child ∥ Purge of its parent | the child's save (no parent in it) takes only the child; the purge waits for it, then clears the link; the save never sees a half state | the child's row in the purge's statement |
| Any write ∥ the project's deactivation | the write only reads the status (ADR-0026 rule 8): a request checked before the deactivation committed is ordered before it | unchanged |

**Deadlock reading.** The purge acquires: hierarchy, then rows in ascending id inside one statement, then the rows it
changes (all already locked, but the item's team place and the project place rows). A save or a creation never waits on
a lock the purge holds while holding one the purge needs: a save holds its item, the purge waits for it, and the save
asks next only for an area (`FOR SHARE` on `areas`) or parents behind the hierarchy lock the purge holds (it would
wait first for the hierarchy, holding nothing the purge wants). A reorder holds `KEY SHARE`, which `NO KEY UPDATE` never
waits on, and the purge's one stronger lock is on a row no reorder can hold (§ 7.3). The earlier mod-017 deletion
orders items by id; the purge does too.

**Owed:** the project–team unlink (feat-028) is a clean-up of the same kind as the team deletion and is tested on a
deleted item by whichever line merges second (BR-DR-9); `Unlink_ReassignsADeletedItemToo_StillDeleted` is the name
reserved for it.

## 9. Tests

Tests are written with the code. Each carries a comment naming the story and the scenario (`// US-3 · "A save of any
field is refused"`), as the repository's tests do. Backend-only scenarios are here; scenarios that are the card's, the
dialogs' or the host views' are Vitest and MSW tests in `plan-frontend.md` and are marked **FE** below.

### 9.1 Domain (`Mustask.Domain.Tests`)

`WorkItemDeletionTests` (new): `Delete_LiveItem_MovesToTheRemovedState_RecordsHeldStateWhoAndWhen_ChangedAtIsTheDeletion`;
`Delete_FromEveryNonRemovedState_RemembersIt`; `Delete_ChangesNothingElse` (every other property compared);
`Delete_AlreadyDeleted_ThrowsDeleted_NothingChanged`; `Delete_WrongType_Throws`;
`Delete_TwiceWithARestoreBetween_RemembersTheLatestState`; `Restore_ToTheHeldState_ClearsAllThreeMarks_MovesChangedAt`;
`Restore_ToAChosenState_ForgetsTheHeldOne`; `Restore_ItemWithNoMarks_GoesToTheFirstState`;
`Restore_HeldStateNotAllowedAnyMore_GoesToTheFirstState` (D-P8); `Restore_NotDeleted_ThrowsNotDeleted_BeforeStateNotAllowed`
(BR-DR-10 order, even for «Удалена» and a foreign state); `Restore_StateOfAnotherTypeRemovedOrUnknown_StateNotAllowed_Unchanged`;
`Restore_NamingNothing_GuidEmpty_IsStateNotAllowed`; `Marks_AreAllOrNone_AfterEveryTransition`;
`EnsureLive_AndEnsureDeleted_AreEachOthersNegation`; `Save_OnADeletedItem_Throws_BeforeAnyFieldStateOrAssigneeIsLooked_At`
(tech-012 m1: a live state sent with the edit does not slip past); `Save_OnADeletedItem_EmptyEdit_StillThrows`.

`WorkItemTypeStatesTests` (additions): `RemovedStateId_IsTheOneRemovedState_AndThrowsWithNoneOrTwo`;
`IsRemoved_AgreesWith_Allows_ForEveryState`; `HierarchyTypes_IsDeleted_AgreesWith_TypeStates_IsRemoved` (the in-memory
copy of the definition; `ProcessHierarchy.Of` over a seeded process).
`OrganizationRoleBundlesTests` and `PermissionCatalogTests`: the two codes, scope Organization, the matrix rows (admin
both, member `…delete` only, viewer neither), no instance code.

### 9.2 Application (`Mustask.Application.Tests`, fakes)

`PurgeWorkItemHandlerTests` (the step contract, with fake steps, a fake repository and `FakeUnitOfWork`):
`Handle_RunsPreflightsBeforeTheTransaction_ThenStepsInsideIt_InRegistrationOrder_WithTheChildIds`;
`Handle_ThePreflightRefuses_NothingIsLockedOrRemoved`; `Handle_AStepFails_NoRemovalIsCalled_NoLogLine`;
`Handle_LocksTheHierarchyFirst_ThenTheItemAndChildren_ThenStepsDropTheTeamPlaceReleaseChildrenRemove` (call order);
`Handle_LiveHead_Is409NotDeleted_BeforeAnyPreflight`; `Handle_NoRowUnderTheLock_Is404`; `Handle_ARestoreGotThere_Is409NotDeleted_NothingRemoved`;
`Handle_NoStepsRegistered_IsFine`; `Handle_LogsOnceAfterTheCommit_WithoutTheTitle` (logger double).
`DeleteWorkItemHandlerTests` / `RestoreWorkItemHandlerTests` (short): the order of answers (path not-found before the
409), the actor and the clock reach the domain, a malformed `stateId` is `state_not_allowed` only after `not_deleted`.

### 9.3 Infrastructure (`Mustask.Infrastructure.Tests`)

- `AddWorkItemDeletionMarksMigrationTests` (Docker): additive (rows, places, counters as they were), rollback drops
  exactly the three columns, the key, the CHECK and the index, applies again; the model has no pending changes.
- `WorkItemDeletionSchemaTests` (Docker): the CHECK refuses one or two marks set and accepts none or all three; the key
  refuses a held state that is not a state; `ix_work_items_deleted_from_state_id` is partial (`pg_indexes.indexdef`);
  `deleted_by` takes 255 and refuses 256; the marks round-trip through EF; a state definition that an item remembers
  cannot be deleted (Restrict).
- **`DeletedStateParityTests`** (Docker) — the three expressions of "deleted" agree (ADR-0030 rule 2). A process with an
  item in **every state of every type** (the seeded five and the test-case type that `BoardChecklistsQaTests` adds),
  written by SQL. The expected deleted set is read from the in-memory process (`WorkItemTypeStates.IsRemoved`). Asserted
  equal to it: `HierarchyTypes.IsDeleted` over the loaded entities; `WorkItemSets.Deleted` and the complement of `Live`;
  `Live` ∪ `Deleted` = `InAnyState` and `Live` ∩ `Deleted` = ∅; the list's page and `count`; `FindParentCandidatesAsync`;
  `FindLiveParentsAsync`; `ReadChecklistsAsync`; the team memberships (`Members`, `TreeMembers`) and the project ones for
  every level; and the raw locking reads of `BacklogRepository` and `ProjectBacklogRepository` (the latter pinned to
  `ProjectBacklogMembership.Members` for the project scope).
- **`WorkItemReferenceInventoryTests`** (model-only, no Docker; the family of `CodeUniqueIndexModelTests`) — BR-DR-16.
  From `MustaskDbContext.Model`: every foreign key whose principal type is `WorkItem`; every property named `WorkItemId`
  or ending in it that has no such key; every entity a `[PurgeStepCovers]` class names (found by scanning the
  Infrastructure assembly, no container). Each must be exactly one of **Cascade** (`DeleteBehavior.Cascade`),
  **ClearedByPurge** (today `WorkItem.ParentId` alone, whose key is `Restrict`: a forgotten clear breaks the delete),
  **CoveredByStep** (a step declares the entity), **Kept** (listed in the test with its reason; none today). It fails on
  an unlisted reference, on a stale entry, and on a `Restrict` key that is neither cleared nor covered, and it carries a
  guard against passing vacuously: today's three references (`BacklogPlace.WorkItemId`, `ProjectBacklogPlace.WorkItemId`,
  `WorkItem.ParentId`) must be found. A second case plants an extra entity with a `WorkItemId` and no key, and one with a
  `Restrict` key, and expects the failures. **Limit, stated in the test:** the model does not see
  `fk_backlog_places_work_items_owner` (SQL-only, `NO ACTION`; EF cannot declare it, mod-017) nor a reference inside a JSON
  column or a store outside the database; the first is pinned by the purge test of a team-placed item (§ 9.4), the others
  are seen only through a step that declares them.
- `DeletionStatementCountTests` (Docker; the pattern of `OwningTeamStatementCountTests`): Delete, Restore and Purge take
  the same number of statements for a project of 50 items as for one of 5,000, and for a purge of 0 as of 5,000
  children; the numbers are the first run's and pinned. `ItemGetStatements` (7) and `ListStatements` (7) stay as they
  are (D-P3). `ProjectBacklogStatementCountTests.ReorderStatements` 12 → 13.
- `WorkItemSetsTests` (Docker): `Deleted` returns exactly the items in «Удалена» across two organizations' processes (the
  category, never the name).

### 9.4 API integration (`Mustask.Api.IntegrationTests`, Testcontainers)

Scenario → test. **FE** = Vitest/MSW in the frontend stage; the backend half of a mixed scenario is named.

**US-1 · Delete** — `DeleteWorkItemEndpointTests`

| Scenario | Test |
|---|---|
| «Удалить» in «⋮», the confirmation texts, buttons, Escape | **FE** (the served codes: `GetOrganizationAccessEndpointTests` lists `…delete` for admin and member, not viewer) |
| Confirming moves the item into «Удалена»; the card stays, read-only; who and when; «Изменён»; the held state | `Delete_LiveItem_Answers200WithTheDeletedItem_StateRemoved_DeletionMarksSet_ChangedAtIsTheDeletion`; **FE** for the card |
| A deletion changes nothing else (fields, assignee, area, team, priority, parent; the parent's «Изменён») | `Delete_ChangesNothingElse_AndTheParentsChangedAtIsUnchanged` |
| The hosts show the item gone once they read again | `DeletedItemViewsEndpointTests.List_…` (US-2); **FE** for the host re-read |
| An item is deleted from any non-Removed state, each into its own type's «Удалена» | `Delete_FromEveryNonRemovedState_OfEveryType_RemembersTheStateHeld` (theory over the five types) |
| Nobody else's work is touched (both organizations) | `Delete_TouchesNoOtherItemProjectTeamAreaOrMember` |
| Deleting something already deleted | `Delete_AlreadyDeleted_Is409Deleted_TheHeldStateIsTheFirstDeletions` |
| A save and a deletion end in one order | `Races.Delete_Versus_Save_BothOrders_NoServerError` (§ 9.5) |
| A failed delete leaves the card as it was | **FE** (no answer, 5xx); 409/404/403 answers: the tests of this block |
| Unsaved edits do not survive a deletion; one confirmation sends one request | **FE** |
| A reader and a non-member cannot delete | `Delete_AViewer_Is403_AndChangesNothing`; `Delete_ANonMember_AnotherOrganizationsAdministrator_AndTheInstanceAdministrator_Is404OrganizationNotFound` |
| Unknown, foreign or deactivated addresses | `Delete_UnknownForeignMalformedOrDeactivated_Is404TheLevelsCode_NothingChanges_AndTheRestoredProjectKeepsItLive`; the same address set for Restore and Purge in their classes; containment across organizations for every new route is also `OrganizationContainmentTests` (§ 9.5) |
| Retention has no clock | `RetentionTests.NoHostedService_WritesWorkItems` (the registered `IHostedService`s equal a listed set that touches no work item, so a new one forces a decision) and `Delete_ThenALongTime_ItemIsStillDeletedUnchanged` (the test's clock moved a year) |

**US-2 · Hidden everywhere, found by address** — `DeletedItemViewsEndpointTests`, and the matrix below

| Scenario | Test |
|---|---|
| The list neither shows nor counts a deleted item, whatever the page | `List_ADeletedItem_IsNeitherListedNorCounted_OnEveryPage` (8 items, 1 deleted: 7 rows and `count` 7; 120 items paged) |
| Team backlogs and boards at every level lose the item; others keep their order | `TeamBacklogAndBoard_AtEveryLevel_LoseTheItem_OthersKeepTheirOrder`; `ProjectBacklogAndBoard_…` |
| A deleted item keeps what it carried; its area is not deletable | `Delete_KeepsTeamAreaAssigneeParent_AndTheAreaIsStillNotDeletable_AreaNotEmpty` |
| A deleted item is still counted among a team's items; the team deletion releases it | `OwnedItemCount_CountsADeletedItem`; `DeleteTeam_ReleasesADeletedItemToo_StillDeleted_ChangedAtMoves` |
| Found by its address (a reader too; a foreign identifier is an unknown one) | `Get_ADeletedItem_ReadsInFull_WithItsDeletion_ForAMemberAndAViewer`; `Get_AForeignIdentifier_IsNotFound_LikeAnUnknownOne` |
| A live child leads to its deleted parent | `Get_ALiveChild_ShowsItsDeletedParent_WithStateRemoved` |
| Pickers and the form's lists skip a deleted item | `ParentCandidates_SkipADeletedItem_WhateverIsTyped`; `Get_Children_ListsLiveOnly_AndTheCountIgnoresDeletedOnes` |
| A board card's checklist skips a deleted child; restored, it returns in creation order | `Checklist_SkipsADeletedChild_RestoredItReturnsInCreationOrder` |
| Another user's open view lags | **FE** (no push); the stale writes are US-3's |
| A deactivated project hides its deleted items; restored, they read as before | `Get_ADeletedItem_UnderADeactivatedProject_Is404_RestoredProjectReadsItAsBefore` |
| Nothing leaks across organizations | `OrganizationContainmentTests` (the three new rows) and `Get_ADeletedItemOfAnotherOrganization_IsOrganizationNotFound` |

**`DeletedItemSurfaceMatrixTests` (the cross-cutting one).** One test seeds the world of spec § Common data plus a deleted
task **D** with a live parent (the story S), a live child **C** and a deleted child **DC**, and a live task **L**
under S. It reads **every surface of the spec's § Surfaces and writes table that exists** and asserts each is present or
absent exactly as the table says, one assertion block per table row, each commented with the row: the list and its
`count`; the card by address; the team backlog and board of each level and the project's (rows, cards, column counts, the
order of the others); the backlog tree (C is a top-level row, its «Родитель» cell «Нет»; D is in no row); the «Родитель»
read on C (D, state «Удалена»); the parent picker; «Дочерние элементы» of D's parent and of D; S's board card checklist
and its counter; the team's owned count and the area guard (D counted); the deactivated project (404, then back). A
surface added by a later line adds a row; a surface with no row is what review looks for.

**US-3 · A deleted item takes no write** — `DeletedItemIsReadOnlyEndpointTests`

| Scenario | Test |
|---|---|
| The card of a deleted item is read-only; a reader sees it without the way back | **FE**; backend: `Get_ADeletedItem_StateIsRemoved_DeletionCarriesFromStateByAndAt` and the reader's 200 |
| A save of any field is refused (title, assignee, team, a field of the type); the item and «Изменён» are as they were | `Patch_ADeletedItem_AnyField_Is409Deleted_ItemUnchanged` (theory over the four) |
| The refusal comes before the value checks; an unknown identifier is 404 before the 409 | `Patch_ADeletedItem_With300CharTitle_Is409NotAnd400`; `Patch_UnknownIdentifier_Is404BeforeTheRefusal`; `Patch_ADeletedItem_NamingAnUnknownAreaTeamParentOrState_Is409Deleted` |
| A live state sent with a parent does not slip past (tech-012 m1) | `Patch_ADeletedItem_LiveStateAndAParent_Is409Deleted_NoLinkNoChangedAtMoves` |
| No state write brings the item back without Restore | `Patch_ADeletedItem_AStateAlone_Is409Deleted_StaysDeleted`; `Patch_ADeletedItem_EmptyBody_Is409Deleted` |
| A stale board card cannot be moved; a stale checklist row cannot be ticked | `Patch_State_OnADeletedBoardCard_Is409Deleted` and `…_OnADeletedChecklistChild_…` (a card move and a tick are this save); **FE** for the notice and the re-read |
| A stale reorder naming a deleted item is refused | `Reorder_TeamAndProject_NamingADeletedItemAsMovedOrAnchor_Is409ReorderStale_OrderUnchanged` |
| Link writes keep their own refusal; a form save of the deleted item answers `…deleted` | `SetParent_Deleted_ParentOrChild_SetReplaceClear_Is409ItemDeleted`; `Patch_LiveItem_NamingADeletedParent_Is409ItemDeleted`; `Create_BeneathADeletedParent_Is409ItemDeleted` (existing `CreateChildEndpointTests`, re-pointed); `Patch_ADeletedItem_NamingAParent_Is409Deleted_NotItemDeleted` |
| A draft is not saved after the deletion | `Patch_AfterAnotherUserDeleted_Is409Deleted_TheTitleStoredIsTheOldOne` (**FE** for the card turning read-only) |
| A reader's write answers as on a live item | `Patch_ADeletedItem_AsAViewer_Is403_TellsNothingOfTheDeletion` |
| System clean-up reaches a deleted item | `DeleteTeam_ReleasesADeletedItem_StaysDeleted_OnlyTheOwnerAndChangedAtMove` |
| Restore and Purge are the writes it takes | `WorkItemWriteInventoryTests` (below) |

**`WorkItemWriteInventoryTests` (ADR-0030 rule 3, D-P4).** Enumerates every write route (POST/PUT/PATCH/DELETE) of
`OrganizationAddressedRoutes.All` from the live endpoint data source and requires a row for each in a table beside the
test, one of: **Refuses a deleted item** (with the code and the name of the test that proves it); **Acts on one**
(Restore, Purge); **Clean-up** (the team deletion; the unlink is added by feat-028); **Names no work item** (with a reason).
A route with no row fails `scripts/verify.sh`, as `OrganizationAccessInventoryTests` fails one with no code; a canary
asserts the scan reaches `workitems`, `backlogs` and `teams` routes, and a second test plants a row for a route that does
not exist and one for a route with no row. The proving tests it names must exist in the assembly (reflection on method
names), so a row cannot lie.

**US-4 · Restore** — `RestoreWorkItemEndpointTests`

| Scenario | Test |
|---|---|
| The proposal is the held state; the options are the type's states without «Удалена» | **FE**; backend: `Get_ADeletedItem_FromStateIsTheHeldState` |
| Accepting the proposal returns the item as it was; back in the list, on the backlog and board, in its column | `Restore_NoBody_ReturnsTheItemToTheHeldState_BackOnEverySurface_MarksCleared_ChangedAtIsTheRestore` |
| Any other state of the type may be chosen; the held state is forgotten | `Restore_ToAChosenState_AnyOfTheTypes_ForgetsTheHeldOne` |
| The choice is limited to the item's type | `Restore_StateOfAnotherTypeRemovedOrNotAnId_Is409StateNotAllowed_StaysDeleted` (a Задача has no «Разрешена») |
| A restore changes nothing but the state (parent's «Изменён», children included) | `Restore_ChangesNothingButTheState_AndTheMarks` |
| The item returns to its owner as stored; an owner deleted meanwhile leaves it «Без команды» | `Restore_ReturnsToItsOwnersViews`; `Restore_AfterItsTeamWasDeleted_IsOnNoTeamsViews_OnTheProjects` |
| The backlog place follows mod-006 | `Restore_BacklogNotReordered_ReturnsWhereItStood`; `Restore_BacklogReorderedMeanwhile_ReturnsBelowEveryPlacedItem` |
| A second deletion remembers the latest state | `Delete_Restore_Move_Delete_RemembersTheLatestState` |
| Restoring something not deleted is refused, even naming «Удалена» or a foreign state | `Restore_ALiveItem_Is409NotDeleted_EvenWithAForeignOrRemovedState_StateNotApplied` |
| A failed restore; double activation | **FE** |
| A restore and a save end in one order; a restore and a purge end in one | § 9.5 |
| Retention has no clock | `Restore_AfterAYear_NothingLost` (the test clock) |
| An item with no remembered state has a way back | `Restore_AnItemPutInRemovedWithNoMarks_GoesToTheFirstStateOfItsType` (set directly: `LinksTestData.PutInRemovedWithNoMarksAsync`) |
| Who may restore | `Restore_AViewer_Is403`; `Restore_ANonMember_Is404OrganizationNotFound`; `Restore_AMember_AndTheAdministrator_Can` |
| A deactivated project is not reached | `Restore_UnderADeactivatedProject_Is404ProjectNotFound_RestorableOnceTheProjectIs` |

**US-5 · No cascade** — `DeleteDoesNotCascadeEndpointTests` (BR-DR-18: the set-up is the real Delete)

| Scenario | Test |
|---|---|
| Deleting a parent keeps every child, its state and its link; no child's «Изменён» moves | `Delete_AParent_KeepsEveryChild_StateLinkAndChangedAt` |
| A child of a deleted parent is unparented in the backlogs | `ProjectBacklog_ChildrenOfADeletedParent_AreTopLevelRows_ParentCellNone` (re-run of `BacklogTreeEndpointTests`) |
| The child's form shows the deleted parent and lets go of it | `Patch_NoParent_OverADeletedParent_ClearsIt_TheDeletedParentUntouched` (existing, now on a real Delete) |
| The mapping replaces the link to a deleted parent | `SetParent_ChildOfADeletedParent_KeepsTheLink_UntilAWriteThatSawNoneReplacesIt` (existing, re-pointed) |
| A deleted item is not offered as a parent | `ParentCandidates_SkipADeletedItem_WhateverIsTyped` |
| No link is written to or from a deleted item | `SetParent_ADeletedParent…`, `…ADeletedChild…`, `Create_BeneathADeletedParent…` (existing, re-pointed) |
| A deleted child leaves its parent's form, checklist and counter | `Get_Children_ListsLiveOnly…`; the parent's «Изменён» and links unchanged |
| Restoring the parent brings its kept links back; a released link stays released | `Restore_TheParent_BringsItsKeptChildrenBack_NestsThemInTheBacklog_AReleasedLinkStaysReleased` |
| Restoring a child does not restore its parent | `Restore_AChildOfADeletedParent_StaysUnparentedInViews_ParentStaysDeleted` |
| Deleting a chain deletes nothing below it; each restores alone in any order | `Delete_AChain_DeletesNothingBelow_EachRestoresAlone_InAnyOrder` |
| A deletion leaves the item's own parent as it was | `Delete_ALeaf_LeavesItsParentsChangedAtStateAndOtherChildren`; the checklist counts one fewer |

**US-6 · Purge** — `PurgeWorkItemEndpointTests`

| Scenario | Test |
|---|---|
| «Удалить навсегда» offered to an administrator on a deleted card only | **FE**; backend: `GetOrganizationAccess…` lists `…purge` for admin only |
| The confirmation says it cannot be undone | **FE** |
| Confirming removes the item; 404 as if it never was; on no list; its places gone, the others keep their order; no stored data references it | `Purge_ADeletedItem_Is204_ItsAddressIs404_NoRowNoPlaceInEitherOrder_OthersKeepTheirOrder` (reads both place tables and `work_items` by SQL) |
| Failure and double activation | **FE** |
| The children of a purged item survive without it (also a deleted child; the purged item's own parent untouched) | `Purge_AParent_ChildrenLoseTheLink_KeepStateAndTeam_ChangedAtIsThePurge_ADeletedChildStaysDeleted_TheOwnParentIsUntouched` |
| The number is never issued again | `Purge_TheHighestNumber_NextCreationsGetThe24And25_NoItemEverHasTheNumberAgain_UnderAnyTypeKey` |
| Only a deleted item can be purged | `Purge_ALiveItem_Is409NotDeleted_NothingRemoved` |
| Only an administrator | `Purge_AMemberAndAViewer_Are403_ItemUntouched`; `Purge_ANonMember_AndTheInstanceAdministrator_Are404OrganizationNotFound`; `Purge_AnAdministrator_IsNotRefused` |
| A purge cannot be undone | `AfterAPurge_RestoreSaveAndPurge_Answer404NotFound` |
| A purge and a restore; a purge and a child's release; a purge and a team's deletion | § 9.5 |
| Other users' open views meet the purge as an unknown item | `AfterAPurge_ACardMoveAndATick_Answer404NotFound` |
| A purge leaves no trace: no tombstone; one log line without the title; no audit | `Purge_LeavesNoCopy_AndLogsOneLine_UserOrganizationProjectIdentifier_NeverTheTitle`; `Purge_RolledBack_LogsNothing` (a throwing step registered through `WithWebHostBuilder`) |
| A big purge is quick | `PurgeVolumeTests.Purge_AnEpicWith5000Children_WithinTwoSeconds_EveryChildLiveAndUnparented` |
| Data added later cannot be forgotten | `WorkItemReferenceInventoryTests` (§ 9.3) |
| A deactivated project cannot be purged from | `Purge_UnderADeactivatedProject_Is404ProjectNotFound_ItemUntouched` |
| The step contract end to end | `Purge_RunsARegisteredStep_InsideTheTransaction_AndItsFailureRollsEverythingBack`; `Purge_APreflightRefusal_Is409_NothingRemoved_NoStepRan` (the same factory) |
| A team-placed item purges (the SQL-only owner key) | `Purge_AnItemWithATeamAndAProjectPlace_DropsBothAndTheItem` |

### 9.5 Cross-cutting and concurrency

- **Route inventory** (existing, updated): `OrganizationAddressedRoutes.All` + 3 rows (§ 5);
  `OrganizationAccessInventoryTests` runs each route for each role; `OrganizationContainmentTests` iterates the rows, so
  the three new routes get the foreign-id containment cases without a new test; `WorkItemsOpenApiTests` + 3.
- **Access**: `GetOrganizationAccessEndpointTests`, `GetOrganizationAccessHandlerTests`, `WorkItemsAccessEndpointTests`
  list the two codes (member: `…delete`; admin: both).
- **`DeleteRestorePurgeRaceTests`** (`CommitGate` and `PgLocks`, as `DeleteOwnedItemsEndpointTests`): one test per row of
  § 8.2, **both orders** where the table says so, each asserting the stated outcome, no 5xx and no half state:
  `Delete_Versus_Delete`, `Delete_Versus_Save`, `Restore_Versus_Restore`, `Restore_Versus_Save` (decided second lands on
  the restored item), `Restore_Versus_Purge`, `Purge_Versus_Purge`, `Delete_Versus_Purge`,
  `Restore_Versus_AChildsMapping_Answers_ParentChanged`, `Purge_Versus_AChildsRelease_AMapping_ACreationBeneath`,
  `Purge_Versus_TeamDeletion_ChildEndsUnownedAndUnparented`, `Delete_Versus_TeamDeletion`, `Delete_Versus_TeamReorder`,
  `Delete_Versus_ProjectReorder` (**fails on today's plain read**: the red test that proves § 7.5), `Purge_Versus_ProjectReorder`,
  `Delete_Versus_CreationBeneath_And_MappingNamingItAsParent`, `AChildsSave_Versus_ParentPurge`. The purge's stronger lock
  (§ 7.3) has its own test: a reorder holds `KEY SHARE` on a sibling while the purge runs, and neither waits on the other.
- **`DeletedItemSurfaceMatrixTests`**, **`WorkItemWriteInventoryTests`**: § 9.4.
- **Volume** (`DeleteRestorePurgeVolumeTests`, NFR): Delete and Restore each within 1 s on a project of 5,000 items; the
  purge of 5,000 children within 2 s (after a warm-up, as `List_5000Items_…` does); the list's first and a later page
  within 2 s with 1,000 of the 5,000 deleted, and the count 4,000.
- **Architecture** (`WorkItemAccessRules`, § 6.4) and `RulesAreNotVacuous` entries.
- **Contracts** (`Mustask.Contracts.Tests`): the two codes have ru and kk, `docs/error-codes.md` is regenerated
  (`MUSTASK_UPDATE_ERROR_CODES=1 dotnet test src/backend/tests/Mustask.Contracts.Tests`).

### 9.6 Tests retired, rewritten and re-pointed — by name, with the rule

**Nothing is retired outright.** No test asserts a rule this line removes: the mod-003 list text "every item in every
state" (BR-M3-29) is not asserted by any test (an item could not be put there), and feat-001's US-7 tests are re-run, not
dropped. What changes:

| Test or helper | Change | Rule |
|---|---|---|
| `LinksTestData.DeleteAsync` (24 call sites in 14 files: `BacklogTreeEndpointTests`, `BoardChecklistsEndpointTests`, `CreateChildEndpointTests`, `GetProjectBacklogEndpointTests`, `OwnedViewsEndpointTests`, `ProjectBoardsQaTests`, `ReorderProjectBacklogEndpointTests`, `GetTeamOwnedItemCountEndpointTests`, `GetParentCandidatesEndpointTests`, `GetWorkItemLinksEndpointTests`, `HierarchyDepthEndpointTests`, `SaveWorkItemParentEndpointTests`, `SetWorkItemParentEndpointTests`, and `DeleteOwnedItemsEndpointTests`) | **re-pointed** from `UPDATE work_items SET state_id` to the real `POST …/delete` as the organization's administrator; no call site's text changes | BR-DR-18, feat-001 D-8 OQ-1 |
| new `LinksTestData.PutInRemovedWithNoMarksAsync` | the old direct write, kept for the tests of an item with no marks (the restore of an unmarked item; `BoardChecklistsQaTests.Get_ARemovedTestCase_IsNotInTheChecklist`, which changes the type in the same statement) | BR-DR-10, BR-DR-18 |
| `OrganizationAccessInventoryTests.TheScopedRoutes_AreExactlyTheListedOnes_EachWithItsListedCode` | `Assert.Equal(47, …)` → 50 | mod-016 BR-M1-33 |
| `WorkItemsOpenApiTests.OpenApiDocument_WorkItemsOperations_AreExactlyTheContracts` | three operations added | the contract |
| `PermissionCatalogTests`, `OrganizationRoleBundlesTests`, `GetOrganizationAccessEndpointTests`, `GetOrganizationAccessHandlerTests`, `WorkItemsAccessEndpointTests` | the two codes and the member's and admin's lists | ADR-0030 rule 9 |
| `ProjectBacklogStatementCountTests.ReorderStatements` | 12 → 13 | § 7.5 |
| `OwningTeamStatementCountTests` constants | **unchanged** (`ItemGetStatements`, `ListStatements`): they are the proof of D-P3 | NFR |
| doc comments of `GetWorkItemsEndpointTests` ("every item of the project"), `LinksTestData.DeleteAsync` ("straight in the table"), `BoardChecklistsQaTests` | reworded; no behaviour | BR-DR-6 |

### 9.7 E2E specs the change breaks (listed, not edited; the standing answer is NO)

Only the frontend landing breaks specs; the backend alone changes no wire shape they read except the served codes.

| Spec | Why | Fix when E2E is next asked |
|---|---|---|
| `src/frontend/e2e/specs/27-work-item-card.spec.ts`, test `27.5` (lines ~1641-1648) | asserts the card's «Ещё действия» menu has `['Копировать ссылку']` only; a member or administrator now also has «Удалить» | the expected list becomes the fixture user's menu per role |
| `24-work-item-hierarchy.spec.ts` (header, "no item can be deleted through the panel") and `26-backlog-tree-editing.spec.ts` (header, "can be put in «Удалена» through the API before mod-005") | comments only; the premise they state stops being true | reword; US-7 / create-refused-under-a-gone-parent cases may be added |
| `29-role-views.spec.ts` (the viewer's card and menus) | not broken: a viewer's menu keeps «Копировать ссылку» only | none |

## 10. Order of work (green at every commit)

Each commit builds, its tests are green, and a code arrives with its first route. Subject `feat(work-items): <what>
(mod-005)`. Files staged by name. No lanes.

| # | Commit | Content |
|---|---|---|
| 1 | deletion marks and the type's removed states in the domain | § 3 domain (marks, `EnsureLive/Deleted`, `Delete`, `Restore`, `WorkItemTypeStates`), the two error codes, `workitems.json`, `docs/error-codes.md`; § 9.1 |
| 2 | deletion marks in the database | § 4: configuration, constants, migration; § 9.3 migration and schema tests |
| 3 | named read entry points | § 6: `WorkItemSets`, every read moved, the list on `Live`, `ProcessWorkItemTypes`, `WorkItemView.Deletion` and the deleter lookup, `WorkItemHead.StateId`; the parity test, `WorkItemSetsTests`, the two text rules, the reference inventory (model); pinned counts unchanged |
| 4 | a deleted item takes no save | § 7.4: `EnsureLive` in the handler and in `Save`; `DeletedItemIsReadOnlyEndpointTests` (the item put there by SQL, as no Delete exists yet); the tech-012 m1 scenario |
| 5 | project backlog reorder locks its members | § 7.5 and its tests; `ReorderStatements` 13; the race `Delete_Versus_ProjectReorder` is written red here and turns green with commit 6 |
| 6 | delete a work item | `…delete` code, bundles, the `DeleteWorkItem` slice, the route, the inventory rows, OpenAPI list; `LinksTestData.DeleteAsync` re-pointed; US-1, US-2, US-5 tests |
| 7 | restore a work item | the `RestoreWorkItem` slice and route; US-4 tests |
| 8 | purge a work item | `…purge` code, repository members, step contract and attribute, the slice and route; US-6 tests; the reference inventory made non-vacuous |
| 9 | cross-cutting proofs | surface matrix, write inventory, races, volume, statement counts, retention; § 9.5 |
| 10 | recycle-bin conventions | `backend.md` § "Soft removal" paragraph (a work item's deletion is its state, reads choose an entry point, the purge and its reference check, pointing at ADR-0030); ADR-0029's index line "amended by ADR-0030"; this plan's "As built"; `status.md` `stage: 05-frontend` and the Decisions line for tech-012 m1 |

Before the PR: rebase on main; if main brought migrations, regenerate commit 2's (§ 4.2). `bash scripts/verify.sh` is the
definition of green; `dotnet test Mustask.sln` (Docker running) before commit 9.

## 11. Risks, MR notes, and what is owed

- **EF and the `Concat` under `RefRows`' joins** (§ 6.2): if it will not translate, the documented fallback keeps one
  statement.
- **Two keys from `work_items` to `state_definitions`** in one entity (`StateId`, `DeletedFromStateId`): EF needs both
  declared with their own `HasOne<StateDefinition>()`; the snapshot's constraint names are checked by the schema test.
- **The re-pointed `LinksTestData.DeleteAsync`** now writes through the endpoint, so a test that deleted in the same
  second as a read of `changed_at` sees the real deletion time; none depends on equality with the old time, and the
  helpers that compare `StoredAsync` before and after read after the deletion.
- **feat-028 in flight**: both lines touch `OrganizationAddressedRoutes`, `WorkItemTeamOwnership` and possibly
  `work_items`. Whichever merges second rebases; the unlink test on a deleted item is owed by it (BR-DR-9).
- **Pinned counts** move only where stated (§ 9.6).
- **Queue amendments** (spec § Decomposition) are the orchestrator's: feat-015, feat-003, tech-012 (m1 closes here),
  feat-002, mod-004, feat-016.
- **MR notes:** one additive migration, `AddWorkItemDeletionMarks`, no table emptied; two permission codes; two error
  codes; the project reorder gains a lock and a statement.
- **For the human** (recommended option taken, nothing blocks): D-P1 the route shape, D-P2 the shape of the deletion
  marks, D-P4 the widened write inventory (with D-P5's read rule).

## 12. As built — deviations from this plan

_Not yet._
