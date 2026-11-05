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

**Resolution (fixed, prose only).** `docs/conventions/backend.md`, "Reads choose an entry point": the sentence now says that
outside `WorkItemSets` only the two backlog memberships name the set (each pinned by `DeletedStateParityTests`), that
`WorkItemAccessRules` checks this by the set's name (a text rule, not a proof), that there is no global filter, and that a
raw-SQL read of `work_items` is not seen by the rule: it filters by the shown states or by id itself and the reviewer checks
it by hand. The write-inventory sentence now ends "… or that test fails; it is an integration test (`RequiresDocker`), so it
runs where Docker runs and `scripts/verify.sh` skips it when Docker is down". `WorkItemReferenceInventoryTests` ("fails the
build") was checked: it is model-only, without Docker, so that sentence stands. No code, no test (nothing to run). Needs a
delta read, no QA.

**CR-2 · minor · backend** — the spec NFR at `spec.md:139` asks for "each pair of Delete, Restore, Purge and a save, in
both orders". `DeleteRestorePurgeRaceTests.cs` has no test for:
- Delete ∥ Restore (either order);
- Restore ∥ Save with the save first (only restore-first exists, at `:155`);
- Purge ∥ Save of the item itself (`:320` is a child's save).

The plan's table (`plan-backend.md:358-375`) narrowed the set without saying so. Every pair is ordered by the item's
`FOR UPDATE` and the re-read (`WorkItem.cs:291-305`; `PurgeWorkItemHandler.cs:94-101`), and the code reads correct to
the reviewer. **Fix:** add the four tests in the CommitGate pattern of `:110`/`:126`, or narrow the NFR with the
reason.

**Resolution (fixed, with one reason recorded).** Five tests in `DeleteRestorePurgeRaceTests`, green:
`Delete_Versus_Restore_DeleteFirst_…` and `…_RestoreFirst_…` (the second request decides on what it finds under its own lock:
restored to the held state; deleted again holding the state the restore set), `Purge_Versus_Save_PurgeFirst_TheSaveAnswers404`,
`Restore_Versus_Save_SaveFirst_…` and `Purge_Versus_Save_SaveFirst_…`. The CommitGate cannot hold a request that is refused:
a save of a deleted item is refused under the lock before it writes, so it never reaches the gate. Those two "save first"
orders are therefore pinned in sequence (the save answers 409 `…deleted`; the restore or the purge that follows finds the item
untouched), which is all that pair can do; the reason is in the test comments and in `plan-backend.md` § 13. The NFR stands as
written.

**CR-3 · minor · frontend** — the claim of the feat-029 test at `WorkItemCard.actions.test.tsx:180`, "holds «Копировать
ссылку» and nothing else", is one that mod-005 changes: design § 2, D-4, and `CardActions.tsx:90-97` add «Удалить» and
«Удалить навсегда». The test still passes only because `openCard()` renders without `canDelete`. The test was neither
reworded nor retired with a rule id. The scenario at `feat-029…/stories/US-5-refresh-undo-copy-link.md:67-70` ("nothing
else — no «Удалить»") is the same stale claim, and E2E 27.5 encodes it. **Fix:** rename the test to "…for a reader
without `workitems.workitem.delete`" with a `// mod-005 D-4` note, and add a P-10 note to feat-029's `status.md`
like the five notes this diff left in other features (feat-029 got none).

**Resolution (fixed).** The test is renamed "holds «Копировать ссылку» and nothing else for a reader without
workitems.workitem.delete, and …" with a `// mod-005 D-4` comment (`WorkItemCard.actions.test.tsx`; the body is unchanged,
`openCard()` renders without `canDelete`). `docs/features/feat-029-work-item-card-reference/status.md` has a P-10 note at the
top of its Log: US-5's scenario and BR-C-11 now hold for a reader only. The story file and E2E 27.5 are not edited (a merged
story is amended by the note; E2E has the standing NO and is listed in H-2). Vitest of the file was run (see the report).

**CR-4 · minor · backend** — ADR-0030:79 says that outside `Infrastructure/WorkItems/` nothing writes `work_items` by
raw SQL, "`ExecuteUpdate`/`ExecuteDelete` or `Remove`". The write rule's patterns
(`WorkItemAccessRules.cs:53-58`) cover only raw `UPDATE`/`DELETE`/`INSERT` text. So
`WorkItemSets.Live(context)…ExecuteUpdateAsync(…)` or `context.Remove(item)` in Boards would pass. No violation exists
today: the only `ExecuteDelete` is `UserDirectory/DirectorySnapshotStore.cs:43`. **Fix:** add the two shapes to
`WritePatterns` with a `RulesAreNotVacuous` sample each. The ADR is accepted and stays as it is.

**Resolution (fixed).** `WorkItemAccessRules.WritePatterns` gains "bulk update or delete of work items" (an
`ExecuteUpdate`/`ExecuteDelete` in the same statement as a `WorkItemSets.Live|Deleted|InAnyState(` call, the context's
`WorkItems` set or `Set<WorkItem>()`) and "tracked remove through the context" (`…context.Remove(` / `RemoveRange(`; the
entity cannot be told by text, so it is flagged for any entity outside `Infrastructure/WorkItems/`, and the other slices
remove through their own set, `context.Areas.Remove`). `Work_item_access_rules_are_not_vacuous` has a flagged sample of each
shape and of each variant, and samples that must not be flagged (another entity's bulk write, `context.Areas.Remove`, a read
followed by another statement's bulk write, the sub-slice itself). The real sources have no violation. Ran
`Mustask.Architecture.Tests` (`WorkItemAccessRules`, 3 of 3). The ADR stays as it is.

**CR-5 · minor · backend** — the doc comment at `Mustask.Domain/WorkItems/WorkItemTypeStates.cs:50-52` says
`RemovedStateIds` is the list "that `IsRemoved`, `Allows` and `HierarchyTypes.IsDeleted` all read". But `IsRemoved`
(`:76-77`) re-filters `States` itself, and `Allows` goes through `IsRemoved`. The two are equal today, and the parity
test would catch a divergence. **Fix:** `IsRemoved(id) => RemovedStateIds.Contains(id)`, or reword the comment.

**Resolution (fixed).** `RemovedStateIds` and `IsRemoved` now share one private predicate, `InRemovedCategory`, so they
cannot disagree (not `RemovedStateIds.Contains`, which would build a list on every call of a method the hot paths use). The
doc comment says what each reads: `Allows` through `IsRemoved`; `HierarchyTypes.IsDeleted` the list. The new theory
`WorkItemTypeStatesTests.RemovedStateIds_AndIsRemoved_AreOneDefinition_ForEveryState` pins them equal for a type with none,
one and two Removed states, and for a stranger id. Ran `WorkItemTypeStatesTests` (8 of 8).

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

**Resolution of the "For the human" items.** All three are unchanged on purpose.
- **H-1: left, option A (keep), for the human.** The orchestrator's rule is to go on with the recommended option; nothing
  was built for B. If the human picks B, the change is one mark in `lib/deletion.ts` and its test.
- **H-2: left.** E2E has the standing NO (2026-10-12): nothing was run or edited. The two specs that break are 27.5 and 27.9
  (and the stale comments of 22-27); the feat-029 note of CR-3 records 27.5's premise. One E2E queue line for both is the
  orchestrator's call when E2E resumes.
- **Owed (BR-DR-9): left.** feat-028 is not in main; the unlink test belongs to whichever merges second.

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

## Delta read 1

A clean session read the fix-round diff `8067f009..267ab14a` on `mod/005-work-item-delete-restore`, HEAD `267ab14a`. Its
scope was CR-1 and the prose and rules next to it. The CR-1 fix is `37cb9e80`, prose only, in `docs/conventions/backend.md`.
Tests run on the anchors, all green:
- `WorkItemAccessRules`: 3 of 3 (Architecture.Tests).
- `DeletedStateParityTests` + `WorkItemReferenceInventoryTests`: 12 of 12 (Infrastructure.Tests, Docker).
- `WorkItemWriteInventoryTests`: 4 of 4 (Api.IntegrationTests, Docker).

**Verdict: approve.** CR-1 is resolved. The only new finding is CR-6, a minor, so it is a debt and gets no round.

### CR-1 — resolved

- **Who names the set.** `backend.md:277-281` now names `WorkItemSets` and the two memberships as the only sources outside
  it that name the set. That is true: a grep of `context.WorkItems` / `Set<WorkItem>(` outside the tests and migrations finds
  exactly `WorkItemSets.cs`, `BacklogMembership.cs`, `ProjectBacklogMembership.cs` and the `DbSet` in `MustaskDbContext.cs:58`.
- **The memberships are pinned.** "each pinned by `DeletedStateParityTests`" → `DeletedStateParityTests.cs:155,189`, which
  covers both memberships.
- **The text rule.** "a text rule and not a proof" → `WorkItemAccessRules.cs:20-22`. The false guarantee "no read that skips
  the choice" is gone. A repo grep finds that wording nowhere else, except in this file's own record of CR-1.
- **The write inventory.** The sentence now ends "or that test fails; it is an integration test (`RequiresDocker`)…". Anchors:
  - `WorkItemWriteInventoryTests.cs:19` carries the trait;
  - when Docker is down, `verify.sh:682-697` drops the `*IntegrationTests` projects (`:685`) and runs the rest with
    `Category!=RequiresDocker`.
- **The reference inventory.** "`WorkItemReferenceInventoryTests` fails the build" stands. The test has no trait and uses the
  design-time `MustaskDbContextFactory` (`:231`).
- **Nothing nearby broke.**
  - CR-4 widened `WritePatterns` with the bulk-write and tracked-remove shapes. No real source trips them (the run above).
  - CR-5's shared `InRemovedCategory` predicate leaves the parity tests green.
  - ADR-0030:76-79 agrees with the new sentence.

### Finding

**CR-6 · minor · docs** — two overstatements are left next to CR-1.
- **(a) The raw-read sentence.** `docs/conventions/backend.md:280-282` says a raw-SQL read of `work_items` "filters by the
  shown states or by id itself". Four raw reads do neither, by design: they read in any state, by a reference key.
  - `WorkItemRepository.cs:219` (the purge lock, `parent_id = {id}`);
  - `:291-296` (`SubtreeHeightAsync`, joined by `parent_id`);
  - `WorkItemTeamOwnership.cs:19` (the release lock, `owning_team_id`).

  `plan-backend.md:191` says the same of the subtree read. Nothing misleads a reviewer, because the same sentence orders a
  check by hand. **Fix:** "…it filters by the shown states, or reads in any state by id or by a reference (parent, owning
  team) as the locks and clean-ups do".
- **(b) The repository comment.** `WorkItemRepository.cs:239` says "The context's set is named in WorkItemSets alone".
  `backend.md:277` now names the memberships too. **Fix:** "…in WorkItemSets and the two backlog memberships alone".

### Still open, for the human (none blocks)

- **H-1 / QA D-2.** An unknown deleter shows no «(неактивен)». Option A (keep) is taken.
- **S-1.** The purge log names the deleter and the deletion's time. Option A is taken; the human confirms it.
- **H-2: E2E, standing NO; nothing was run or edited.** Two specs break:
  - `27-work-item-card.spec.ts` 27.5;
  - `27-organization-members.spec.ts` 27.9 (`MEMBER_CODES` and `ADMIN_CODES`).

  The premise comments in 22-27 are stale.
- **BR-DR-9.** `Unlink_ReassignsADeletedItemToo_StillDeleted` is owed by whichever of mod-005 and feat-028 merges second.
- **CR-6** is a minor debt. It fits a docs-only line, or a fold-in when either file is next touched.

### Behavioural claims pass (delta)

- **Direction 1, the prose the CR-1 fix adds.** Every claim is anchored above, except the one CR-6 (a) records:
  - backend.md:277-279 → grep + `DeletedStateParityTests.cs:155,189`;
  - :279-280 → `WorkItemAccessRules.cs:20-22`;
  - :280-282 → **CR-6 (a)**;
  - :289-293 → `WorkItemWriteInventoryTests.cs:19` and `verify.sh:682-697`.
- **Direction 2.** The fix changes no behaviour. The rule CR-4 widened was grepped as `ExecuteUpdate`, `Remove` and "writers
  of": the hits are ADR-0030:79 (now anchored by `WorkItemAccessRules.cs` `WritePatterns`) and `WorkItemAccessRules.cs:12-15`
  (consistent). The grep for "named in WorkItemSets" found **CR-6 (b)**.
- **Not swept (outside this read's charge).** The prose of the other fixes: CR-2, CR-3, CR-5, QA D-1 (api-contract
  § 0.2, § 0.4, § 2.2) and S-1 (BR-DR-15, US-6, api-contract § 2.3). Their tests were not run here. The S-1
  handler diff was glanced at: it logs subject ids and a time, with no title, content or name.

## Delta read 2 — merging main (feat-028)

A clean session read a detached `16ac90d3`: the merge `2aaed5b6` (parents `301b0011`, `175aa1db`) by `--remerge-diff`, the
integration commits `2aaed5b6..16ac90d3`, and every auto-merged shared file against both parents. Nothing was built or run (QA
runs at the same time). Input: spec BR-DR-8..11, plan-backend § 14, api-contract § 3.2, § 3.7, § 5, feat-028 plan § 7, ADR-0028
rule 8, ADR-0030 rules 4–6.

**Verdict: approve.** Three new minors (DR2-1..DR2-3), debts; 0 blocker, 0 major. CR-6 stays a debt.

### Checked and sound

- **Conflicts.** Both intents kept: 23 codes, «Администратор» 23 / «Участник» 11 / «Читатель» 7, in the three matrices and
  `organizationRoles.ts`; 59 routes, mod-005's three after the item routes, so `Memberships` (`OrganizationAddressedRoutes.cs:129`)
  is still the last ten; both P-10 notes.
- **Auto-merged files.** Each is main's text plus mod-005's lines. `SaveWorkItemHandler` keeps A-9 (`:115`, `:287` "linked" =
  the lock's answer) and BR-DR-8 (`EnsureLive` `:137`, after the item's lock, before values, area, team, parents).
- **BR-DR-9, "counted in any state".** The counts `WorkItemTeamOwnership.cs:16-17,31` read `InAnyState`. The release (`:50`,
  `:66-71`, `TeamBacklogPlaces.cs:25-31`) filters by project and owner and leaves state and marks. Both swaps keep main's behaviour,
  since main read `context.WorkItems`. BR-DR-10: Restore writes no owner.
- **Concurrency.** Main's only items-then-project-row path was the clean-ups' `ClearDefaultTeamAsync`. It now runs before
  `LockOwnedAsync` (`UnlinkTeamHandler.cs:171`, `DeleteTeamHandler.cs:88`).
  - No writer now holds items and then asks the project row for more than `KEY SHARE`, which `NO KEY UPDATE` admits.
  - The project reorder takes the row first (`ProjectBacklogRepository.cs:38`).
  - The cycle tests (`DeleteRestorePurgeRaceTests.cs:802-877`) are not vacuous: PostgreSQL grants `FOR SHARE` past a queued
    `NO KEY UPDATE`, so the old order reaches `40P01`.
  - Every pair of § 14.3 was traced to the SQL. No other path to `23001`, `23503` or `40P01` beyond the carried one.
  - The new races in which the item is still deleted when the clean-up runs fail if a clean-up skipped deleted items: the
    link's `DELETE` meets the owner key.
- **Migrations.** The Designer's model equals the snapshot (the method header aside). The snapshot is `OrganizationLevelTeams`
  plus the three columns, the index, the check and the key. `Up`/`Down` untouched, ordered after `20261017200252`: in effect the
  regeneration `backend.md:179-183` asks for.
- **Pins.** No statement-count constant moved against main but mod-005's own `ReorderStatements` 12 → 13. The three Infrastructure
  edits are seeds (`FirstTeam`).
- **Inventories.** The write table equals the inventory's writes, with proofs checked by reflection.
  `EveryReadRoute_SaysWhetherItReadsWorkItems_AndInWhichStates` covers every GET. feat-028 adds no key onto `work_items`.
- **Main moved since.** `git merge-tree` of `16ac90d3` with today's `origin/main` (`12c26469`, queue files only) is clean.

### Findings

**DR2-1 · minor · tests** — No pin tells `InAnyState` from `Live` at `BacklogRepository.cs:141`, the merge's choice (§ 14.2,
BR-DR-10). `RestoreWorkItemEndpointTests.cs:240` deletes `middle`. Under `Live` its stale place 2 survives and ties with `first`'s
new 2, `first` wins on `CreatedAt` (`StackRank.cs:79-82`), and the expected rows come out either way. **Fix:** delete `first`
(its stale 1 then beats `last`'s new 1), or assert `TeamPlacesOfAsync(middle) == 0` after the reorder.

**DR2-2 · minor · docs** — The project row moved before the items, and three places still describe the old order:
- (a) feat-028 `plan-backend.md:322` says "Project rows. Taken last, one at a time by every writer". That is false now for the
  unlink and the deletion, and the project reorder never took it last.
- (b) `DeleteTeamHandler.cs:80-82` lists "the settings cleared" after the reassignment, against the lines below it.
- (c) ADR-0028 rule 8 (`0028-matrix-organization-model.md:144`) has no project row in its one order. The place the merge fixed
  lives only in plans (mod-005 § 14.3, feat-028 § 7.1). `UnlinkTeamHandler.cs:52` puts mod-007's step "between the link locks and
  the owned-item lock" — the gap the project row now shares.

It is minor because the bold § 7.1 amendment and both handlers' remarks give the right order to anyone extending these writers.
**Fix:** reword (a) and (b). For (c), a line for the human at gate-3: amend ADR-0028 rule 8, which is accepted and not edited in
place.
**Resolution (backend-developer, with the QD-1 fix):** (b) fixed — the remark in `DeleteTeamHandler` step 5 now lists the order the code
runs in (links, settings, hierarchy locks, owned items, places, reassignment, links, memberships). (a) and (c) left: (a) is
feat-028's own plan, and (c) is the line for the human. QD-1 settles one more place in that order: ADR-0028 rule 8 step 5 puts the
project's number and hierarchy lock rows "beside" the work items by id, and the clean-ups now take the hierarchy row before the items,
as the purge does (plan-backend § 14.3). For the human, with (c): the rule's text should say so, and where the project row sits.

**DR2-3 · minor · tests** — BR-DR-11 names a clean-up "releasing it or its children". The child shape is raced against a team's
deletion only (`DeleteRestorePurgeRaceTests.cs:424,438`); the unlink is raced on the purged item itself (`:733,748`). The two share
`LockOwnedAsync`/`ReassignOwnedAsync`, so the risk is low. **Fix:** an unlink twin of `:424`/`:438`, with a destination team.
**Resolution (backend-developer):** already covered, by the merge QA's `DeleteRestorePurgeFeat028QaTests.M10_…` (the purge of a deleted
parent whose three children the team owns, against the unlink and against the deletion, both orders, the parent's id below and above
theirs: 8 cases, `e0076300`), which was written after this read; no test added here.

### Still open, for the human (none blocks)

- **Carried, main's own.** § 14.3 records a deadlock: a parent write locks the child (`WorkItemRepository.cs:76`) and then the
  parent (`:217-220`), while a clean-up locks owned items by id. The result can be `40P01`, a 500. Read here, not re-run;
  mod-005 adds no instance. A bug line for the orchestrator.
- **E2E, standing NO; nothing was run or edited.**
  - `27-organization-members.spec.ts` 27.9: `MEMBER_CODES`/`ADMIN_CODES` (`:131-133`) lack six codes.
  - `27-work-item-card.spec.ts` 27.5.
  - feat-028's project-nested helpers leave 19, 21.2, 23–26, 27-work-item-card `:135`, 28 and 29 stale, as main already does.
- **As before:** H-1/D-2 and S-1 (taken as A); CR-6. CR-6 (a) now also covers `TeamBacklogPlaces.cs:25-31`.

### Behavioural claims pass (delta)

- **Direction 1, anchored:**
  - backend.md:274-276 → `WorkItemTeamOwnership.cs:16-17,31`;
  - api-contract :198-199 → the same plus `DeletedItemViewsEndpointTests.cs:161`;
  - api-contract :242-244 → `UnlinkTeamHandler.cs:171-175`, `WorkItemTeamOwnership.cs:50,66-71`, `DeletedItemViewsEndpointTests.cs:210`;
  - api-contract :283 → `OrganizationAccessInventoryTests.cs:237`;
  - § 14.1 → the four matrix tests;
  - § 14.2 rows → the cited code; the `BacklogRepository` row's pin → **DR2-1**; "A-9 intact" → `SaveWorkItemHandler.cs:115,137,287`,
    `CreateWorkItemHandler.cs:150,155`;
  - § 14.3 → the repositories' SQL; the cycle → `DeleteRestorePurgeRaceTests.cs:843,861`; "Carried" → the code's shape only;
  - § 14.4 → the diff against main; § 14.5 → the tests exist; § 14.6 → `27-organization-members.spec.ts:131-133`;
  - feat-028 plan :254-257 and :276 → the handlers;
  - the handlers' remarks → the code, except **DR2-2 (b)**;
  - `WorkItemsConstraints.cs:46-47` → `OrganizationLevelTeamsSchemaTests.cs:143`.
- **Direction 2.**
  - The clean-ups' order: grepped `ClearDefaultTeam`, "project row" and "setting is cleared" in docs/ and the handlers.
    feat-028 plan :252-257 and :263-264 are amended by :276; :322 → **DR2-2 (a)**. feat-028 api-contract :393, BR-OL-7/-8 and
    BR-MX-11/-12 list effects, not an order. ADR-0028 :123 and :144-149 → **DR2-2 (c)**.
  - The counts' state: grepped "owned count" and "owned-items". backend.md, `WorkItemSets.cs:15` and ADR-0030 are consistent.
  - The owed test: grepped "merges second". ADR-0030:187, BR-DR-9 and plan :30/:386/:675 are obligations, now met.
    `qa.md:118` ("B17 is half") is QA's to refresh.
  - **Not swept:** feat-028's E2E prose and the doc-012 analyses.

### Fix re-check

A clean session read a detached `07a6d87f`: the fix `559bf10c` and the prose `fe02c444`, `07a6d87f`. Nothing was built or run.
**Verdict: approve.** QD-1 is closed. Two new minors (DR2-4, DR2-5) are debts.

- **QD-1 (major): closed.** `UnlinkTeamHandler.cs:178-180` and `DeleteTeamHandler.cs:95-97` take the hierarchy after the
  project rows and before `LockOwnedAsync`. `WorkItemHierarchyLocks.cs:30-40` is one insert and one `FOR UPDATE`, both ordered by
  project id, and every taker uses it (`WorkItemRepository.cs:96-97`). Checked for a new cycle: no hierarchy holder asks, after the
  hierarchy, for a row a clean-up holds before it. The save takes its link first (`SaveWorkItemHandler.cs:115`, then `:120`); the
  creation its links and number (`CreateWorkItemHandler.cs:150-158`, then `:192`). The parent route (`SetWorkItemParentHandler.cs:68`)
  and the purge (`PurgeWorkItemHandler.cs:91`) lock only items after it, and no `IWorkItemPurgeStep` is registered. Their later FK
  checks onto links and project rows are `KEY SHARE`, which the clean-ups' `NO KEY UPDATE` admits (`ProjectTeamRepository.cs:29,37-48`,
  `ProjectRepository.cs:57-60`). Pinned by `DeleteRestorePurgeRaceTests.cs:961` (QA's steps, four variants), `:1023`, `:1047`,
  `TeamStatementCountTests.cs:258,273,291` and the call order (`DeleteTeamHandlerTests.cs:183`, `UnlinkTeamHandlerTests.cs:304`).
  Not racing the parent route alone is sound: it takes the hierarchy before the item, and `:1023` shows both clean-ups wait for
  that row. The "Carried" item under "Still open" is this one: no bug line is owed.
- **DR2-2: (b) closed** (`DeleteTeamHandler.cs:83-86` follows `:87-100`). (a) and (c) stay open for the human, and both now also miss
  the hierarchy tier: feat-028 `plan-backend.md:247` (5a: "create, a parent write"), `:276` ("the rest … as written above"),
  and ADR-0028 `:144` (the unlink and deletion bullets).
- **DR2-3: closed.** `DeleteRestorePurgeFeat028QaTests.cs:378` (M10) races the children shape against the unlink and the deletion,
  in both orders and both id orders, through a commit gate.
- **DR2-1:** untouched, still a debt.

**DR2-4 · minor · docs** — prose the fix made stale.
- (a) `plan-backend.md:787` says "The purge's hierarchy lock is not the clean-up's", and its meeting point is "the item rows". Both
  now meet at the hierarchy row first.
- (b) `:835` credits "2 projects … equal to 50 projects" to `OwningTeamStatementCountTests`, which seeds one project. The pin is
  `TeamStatementCountTests.cs:221-233`.
- (c) The remark at `OwningTeamStatementCountTests.cs:162-163` lists the settings after the reassignment and has no hierarchy step.

**Fix:** reword all three.

**DR2-5 · minor · contract** — `IWorkItemTeamOwnership.LockHierarchiesAsync` (`IWorkItemTeamOwnership.cs:36`) is a new in-process
method, but mod-005 `api-contract.md` § 7 (`:293-300`) does not list it. feat-028 `api-contract.md:584-589` and `plan-backend.md:224`
list the old four methods. qa.md's resolution and the Log say "no contract change", which is true for the wire only. It is minor
because the interface's own doc ("Before `LockOwnedAsync`, always") and the handler tests carry the rule to anyone who extends it.
**Fix:** add a § 7 bullet with a status.md note, and say "no wire change".

**Claims.**
- **Direction 1.** Each claim was traced to the code or tests above: the remarks of `IWorkItemRepository.cs:79-84`,
  `IWorkItemTeamOwnership.cs:28-36`, `WorkItemHierarchyLocks.cs:5-18` and both handlers; § 14.3 rows `:788-789` and "the order
  closes both"; § 14.4 pins; § 14.5; qa.md QD-1; the DR2-2 and DR2-3 resolutions. The exceptions are DR2-4 (b) and DR2-5. The
  runs in § 14.4 were not re-run.
- **Direction 2.** Grepped "hierarchy lock", `LockHierarchy`, `work_item_hierarchy_locks` and `LockOwnedAsync` in `docs/adr`,
  `docs/conventions`, feat-028, mod-005 spec, contract and design, and `src/backend/AGENTS.md`. ADR-0030 `:99-101` and `:121` still
  hold. The other hits are DR2-2 and DR2-5. **Not swept:** feat-001 and mod-017 docs.
