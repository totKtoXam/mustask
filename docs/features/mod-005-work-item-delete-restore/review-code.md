# Code review, round 1 — mod-005-work-item-delete-restore

Stage 06, one round over the whole diff `ce7f055b..61a1d4e4` (HEAD `61a1d4e4a17000da85bd0d3c02bcf23e2d7f176c`, read on
a detached checkout). The reviewer did not build or run anything, because QA runs the suite at the same time. Input:
spec.md, stories US-1..US-6, design.md, api-contract.md, plan-backend.md, plan-frontend.md, ADR-0030,
`docs/conventions/backend.md` and `frontend.md`.

**Verdict: changes-requested.** One major finding (CR-1). It is a prose-only fix, so it gets a delta read and no QA
session (stage 06, loop economy). The four minor findings (CR-2..CR-5) are debts and owe no round.

## Round 1

### Findings

**CR-1 · major · docs** — `docs/conventions/backend.md:277-280` says "No source but `WorkItemSets` names the context's
`WorkItems` set (`WorkItemAccessRules` is the text rule), so there is no global filter to forget and no read that skips
the choice". The same diff contradicts this sentence:
- `src/backend/tests/Mustask.Architecture.Tests/WorkItemAccessRules.cs:28-40` allow-lists `BacklogMembership.cs` and
  `ProjectBacklogMembership.cs`, both of which name the set.
- The rule's own remarks (`:20-22`) call it "a text rule, not a proof".
- ADR-0030:77 says the memberships keep their own place.
- Raw-SQL reads of `work_items` are outside the rule altogether: `Infrastructure/Boards/BacklogRepository.cs:54,88`,
  `ProjectBacklogRepository.cs:53,88` and the locks in `WorkItemRepository.cs`. They choose by `ShownStateIds` or by id,
  and nothing executable makes them do so.

A reviewer who relies on "no read that skips the choice" will not check a new raw read. The sentence is contradicted in
its own diff, which makes it major (stage 06, Severity).

The same paragraph, at `:286-288`, says "A new write route has a row in `WorkItemWriteInventoryTests` … or the build
fails". That test is `[Trait("Category", "RequiresDocker")]` (`WorkItemWriteInventoryTests.cs:19`), and `verify.sh:676-697`
skips it when Docker is down. **Fix:** "Outside `WorkItemSets`, only the two backlog memberships name the set (each
pinned by `DeletedStateParityTests`); raw-SQL reads are not seen by the text rule and filter by the shown states or by
id themselves", and "… or `WorkItemWriteInventoryTests` (an integration test) fails".

**CR-2 · minor · backend** — the spec NFR at `spec.md:139` asks for "each pair of Delete, Restore, Purge and a save, in
both orders". `DeleteRestorePurgeRaceTests.cs` has no test for:
- Delete ∥ Restore (either order);
- Restore ∥ Save with the save first (only restore-first exists, at `:155`);
- Purge ∥ Save of the item itself (`:320` is a child's save).

The plan's table (`plan-backend.md:358-375`) narrowed the set without saying so. Every pair is ordered by the item's
`FOR UPDATE` and the re-read (`WorkItem.cs:291-305`; `PurgeWorkItemHandler.cs:94-101`), and the code reads correct to
the reviewer. **Fix:** add the four tests in the CommitGate pattern of `:110`/`:126`, or narrow the NFR with the
reason.

**CR-3 · minor · frontend** — the claim of the feat-029 test at `WorkItemCard.actions.test.tsx:180`, "holds «Копировать
ссылку» and nothing else", is one that mod-005 changes: design § 2, D-4, and `CardActions.tsx:90-97` add «Удалить» and
«Удалить навсегда». The test still passes only because `openCard()` renders without `canDelete`. The test was neither
reworded nor retired with a rule id. The scenario at `feat-029…/stories/US-5-refresh-undo-copy-link.md:67-70` ("nothing
else — no «Удалить»") is the same stale claim, and E2E 27.5 encodes it. **Fix:** rename the test to "…for a reader
without `workitems.workitem.delete`" with a `// mod-005 D-4` note, and add a P-10 note to feat-029's `status.md`
like the five notes this diff left in other features (feat-029 got none).

**CR-4 · minor · backend** — ADR-0030:79 says that outside `Infrastructure/WorkItems/` nothing writes `work_items` by
raw SQL, "`ExecuteUpdate`/`ExecuteDelete` or `Remove`". The write rule's patterns
(`WorkItemAccessRules.cs:53-58`) cover only raw `UPDATE`/`DELETE`/`INSERT` text. So
`WorkItemSets.Live(context)…ExecuteUpdateAsync(…)` or `context.Remove(item)` in Boards would pass. No violation exists
today: the only `ExecuteDelete` is `UserDirectory/DirectorySnapshotStore.cs:43`. **Fix:** add the two shapes to
`WritePatterns` with a `RulesAreNotVacuous` sample each. The ADR is accepted and stays as it is.

**CR-5 · minor · backend** — the doc comment at `Mustask.Domain/WorkItems/WorkItemTypeStates.cs:50-52` says
`RemovedStateIds` is the list "that `IsRemoved`, `Allows` and `HierarchyTypes.IsDeleted` all read". But `IsRemoved`
(`:76-77`) re-filters `States` itself, and `Allows` goes through `IsRemoved`. The two are equal today, and the parity
test would catch a divergence. **Fix:** `IsRemoved(id) => RemovedStateIds.Contains(id)`, or reword the comment.

### Checked, no finding

- **Spec:** BR-DR-1..18 against the handlers, the read model and the card.
  - Delete moves the item to the one Removed state and records the held state, who and when (`WorkItem.cs:318-340`).
  - Restore follows D-P8: the held state, the chosen one, or the first state (`:344-399`).
  - The order of answers matches api-contract § 2-§ 5.
  - No cascade (BR-DR-4): children keep their links and count as unparented through `HierarchyTypes.IsDeleted`.
  - tech-012 m1 is closed by `SaveWorkItemHandler.cs:134`: `EnsureLive` runs after the lock and type key, before
    `ValidateValues`.
- **Concurrency:** the purge's order (`PurgeWorkItemHandler.cs:64-123`, `WorkItemRepository.cs:214-236`) matches
  ADR-0030 rule 5 and ADR-0028 rule 8. The modes are compatible: `NO KEY UPDATE` against a reorder's `KEY SHARE`, and
  `parent_id` is not a key column. A 23503 on `fk_project_backlog_places_work_items_work_item_id` maps to
  `reorder_stale` (`ProjectBacklogMemberLockTests.cs:122`). No lock cycle was found against a team deletion, a
  reorder, a save or a creation beneath.
- **Security:** the route codes are pinned by the inventories (`OrganizationAddressedRoutes.cs:80-84`, 47→50 routes).
  `…delete` is held by members and admins, `…purge` by admins only. The address resolves from the outside in, so a
  foreign id gets the container's not-found (`OrganizationContainmentTests` iterates the rows). No personal data goes
  into the purge log line.
- **Migration** `20261018001247_AddWorkItemDeletionMarks`: additive. It adds three nullable columns, an FK with
  `Restrict`, the all-or-none CHECK and a partial index, and `Down` reverses it. No backfill is needed: no item was in
  «Удалена» with a way back.
- **Reads:** every list, picker, children, checklist and count read is on `Live`. The card, the owned count and the
  locks are on `InAnyState`. The parity tests cover each family (`DeletedStateParityTests.cs:36-189`). There is no
  global filter, as ADR-0030 D-2 requires.
- **N+1:** the deleter joins the batched user read (`AssigneesAsync`), and `DeletionRow` is one join. The purge clears
  children with one set-based UPDATE.
- **Renamed tests:** all seven in the diff are count renames (three→four member writes, 17→19 codes, 26→28 contract
  codes). No test is removed. `LinksTestData.DeleteAsync` now goes through the real route.
  `PutInRemovedWithNoMarksAsync` keeps its direct write for BR-DR-10's no-marks case, which is named as such.
- **Architecture:** the slices follow the Dictionaries reference. `IWorkItemPurgeStep` plus `[PurgeStepCovers]` and
  `WorkItemReferenceInventoryTests` (model-only) are the BR-DR-16 hook. On the frontend, `isDeleted` is decided by the
  category (`lib/deletion.ts:14`), the busy state uses `useIsMutating` (`WorkItemCard.tsx:179`), and the purge
  navigates with `replace` (`WorkItemFormPage.tsx:142`).

### For the human

- **H-1. Does an unknown deleter get «(неактивен)»?** design.md:113 leaves this open, and plan-frontend.md:56 flags
  it. The contract marks a user the directory does not know `isActive: false`. Today the line reads «Неизвестный
  пользователь» without «(неактивен)», but with «(не в организации)» when the membership says so
  (`lib/deletion.ts:25-36`).
  - **A (recommended): keep it.** For an unknown user, the inactive flag only repeats what the name already says.
  - B: add the mark. Nothing else changes.
  - The reviewer proceeds on A.
- **H-2. E2E (the standing answer is NO; nothing was run or edited).** The change breaks two specs:
  - `src/frontend/e2e/specs/27-work-item-card.spec.ts` 27.5 (`:1648` expects `['Копировать ссылку']`; the menu now
    adds «Удалить»);
  - `27-organization-members.spec.ts` 27.9 (`:131` `MEMBER_CODES` and `:133` `ADMIN_CODES` lack `…delete` and
    `…purge`).
  - In 22-27, the comment premises "work items have no deletion before mod-005, so the stand is left as it was found"
    are now stale. They are comments only and break nothing.
  - Recommendation: queue one E2E line for both specs when E2E resumes.
- **Owed:** the project–team unlink test on a deleted item (BR-DR-9, `Unlink_ReassignsADeletedItemToo_StillDeleted`).
  It belongs to whichever of mod-005 and feat-028 merges second.

### Behavioural claims pass

Direction 1, the prose the diff adds:
- backend.md:273-276, "Reads choose an entry point" with Live, Deleted and InAnyState →
  `Infrastructure/WorkItems/WorkItemSets.cs` and `DeletedStateParityTests.cs:57`.
- backend.md:277-280, "No source but WorkItemSets … no read that skips the choice" → **CR-1**.
- backend.md:280-283, the three expressions pinned equal → `DeletedStateParityTests.cs:36,57,155,189`.
- backend.md:283, "SurfaceMatrix reads every surface once" → `DeletedItemSurfaceMatrixTests.cs:56-145`. The list, card,
  backlogs, boards, pickers, children, checklists, owned count and deactivation are all read.
- backend.md:284-286, "EnsureLive … on the stored state, under the lock, before any value" → `WorkItem.cs:291`,
  `SaveWorkItemHandler.cs:134`.
- backend.md:286-288, "… or the build fails" → **CR-1**. It is an integration test that needs Docker.
- backend.md:289-292, a cascade by default, else an `IWorkItemPurgeStep` marked → `WorkItemReferenceInventoryTests.cs:45,54,68`.
- ADR-0030:76, three expressions → the parity tests above. ADR-0030:77 → `WorkItemAccessRules.cs:28-40`.
- ADR-0030:79, the writers rule → **CR-4**. Its sub-claim that a raw UPDATE inside never names `state_id`/`deleted_*` →
  `WorkItemAccessRules.cs:187-192`.
- ADR-0030:83, the refusal under the lock → `SaveWorkItemHandler.cs:134`.
- ADR-0030:98 and rule 5, the purge order → `PurgeWorkItemHandler.cs:64-123`.
- ADR-0030 rule 6, the member lock on the project reorder → `ProjectBacklogRepository.cs:50-57,76-93` and
  `ProjectBacklogMemberLockTests.cs:30,90`.
- `ProjectBacklogRepository.cs:79-81`, "A test pins it to the membership" → `DeletedStateParityTests.cs:189`.
- `WorkItemTypeStates.cs:50-52` → **CR-5**.
- `CardActions.tsx:34-35` (the menu by codes, after a divider) → `CardActions.tsx:90-97`.
- `lib/deletion.ts:20-23` (the unknown user's marks) → `:28-35`.
- spec.md:139, NFR concurrency → **CR-2**.
- plan-backend.md § 12 "As built", the pinned statement counts → `DeletionStatementCountTests.cs:38-40` and
  `ProjectBacklogStatementCountTests.cs:92`. They were read, not re-run.

Direction 2, the behaviour the diff changes and the prose outside the diff that describes it:
- **The list hides deleted items.** Grepped "every state" / BR-M3-29 → `mod-003…/api-contract.md:154`. That contract
  sentence is superseded by the P-10 note this diff left in `mod-003…/status.md`.
- **A save of a deleted item is refused first.** Grepped "tech-012 m1" → ADR-0030:83 and spec:6 only.
- **The card read gains `deletion`.** Grepped `deletion` in the older contracts: additive, covered by the mod-003 P-10
  note.
- **The card menu.** Grepped «Ещё действия» and «Копировать ссылку» in docs/.claude:
  - feat-029 spec BR-C-11: anchored ("«Удалить» joins with mod-005").
  - feat-029 US-5:67-70 → **CR-3**.
  - feat-029 design.md:271 and :580: superseded by mod-005 design.md:20.
  - tech-015 design: kit only, not affected.
  - E2E 27.5 → H-2.
- **The role bundles.** Grepped `workitems.link.write` and `workitem.delete`:
  - ADR-0027:121 is open-ended ("the write codes later modules mark").
  - ADR-0029:116 is amended by ADR-0030 and its index line.
  - mod-016's spec matrix is that line's record.
  - E2E 27.9 `MEMBER_CODES` → H-2.
- **The project reorder's member lock and `reorder_stale`.** Grepped `reorder_stale`: the feat-039 and mod-006 P-10
  notes cover it, and there is no wire change.
- **The «Удалена» premise in E2E comments.** Grepped `mod-005` in `e2e/`: 22-27 → H-2.
- **Not swept:** doc-001..doc-012 analyses (they are older reasoning, not behaviour prose) and `docs/backlog.md`.
