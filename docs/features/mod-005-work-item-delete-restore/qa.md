# QA — mod-005-work-item-delete-restore

One QA pass over both layers (L with a UI, `05-frontend.md` § 7), clean context, on `mod/005-work-item-delete-restore` at `f8b6183b`. The plan was written from the spec, stories, api-contract, design and ADR-0030 and committed (`512889f6`) **before any implementation file was read**. Status: ✔ covered by a developer test, ➕ covered by a test this pass added (`DeleteRestorePurgeQaTests`, `WorkItemCard.deletion.keyboard.test.tsx`), ◐ partly, ✖ not covered. Classes are `Api.IntegrationTests/Features/WorkItems` unless named.

## Backend

### Coverage matrix

| # | Criterion | Check | Test (class) | St. |
|---|---|---|---|---|
| B1 | US-1 Delete | 200, own type's «Удалена», `deletion` {fromState, by, at}, `changedAt` = deletion; every non-Removed state, five types | DeleteWorkItemEndpointTests | ✔ |
| B2 | BR-DR-3 | nothing else changes: fields, assignee, area, team, parent, parent's `changedAt`, other items, places | DeleteWorkItem…, DeleteDoesNotCascade… | ✔ |
| B3 | BR-DR-11 | Delete x2 → 409 `…deleted`, first state held; 8 in parallel → one 200 | …AlreadyDeleted, RaceTests, QA EightParallel | ✔➕ |
| B4 | US-1 race | Save ∥ Delete both orders | DeleteRestorePurgeRaceTests | ✔ |
| B5 | US-1 roles | admin/member 200; viewer 403; non-member and instance admin 404 org; foreign, unknown, wrong type key 404; deactivated project **and organization** 404; body ignored | DeleteWorkItem…, QA (org, type key, junk body) | ✔➕ |
| B6 | BR-DR-5 | no job changes work items | RetentionTests | ✔ |
| B7 | US-2 list | no deleted row, count, any page; 1000 of 5000 deleted < 2 s | DeletedItemViews…, VolumeTests | ✔ |
| B8 | US-2 views | team and project backlogs, boards, three levels; order kept | DeletedItemViews…, SurfaceMatrixTests | ✔ |
| B9 | US-2 data kept | team, area, assignee, parent kept; area guard; team count and release | DeletedItemViews… | ✔ |
| B10 | US-2 address | full read for admin/member/viewer; foreign org 404; deactivated project hides, restored shows | DeletedItemViews… | ✔ |
| B11 | US-2/5 | deleted parent shown with state; picker, children and count, checklist skip deleted | DeletedItemViews…, RestoreDoesNotCascade… | ✔ |
| B12 | US-3 save | any field, assignee, team, area, state, parent, `{}` → 409, `changedAt` kept | DeletedItemIsReadOnly… | ✔ |
| B13 | US-3 order | 409 before 400; 404 before 409; viewer 403 | DeletedItemIsReadOnly… | ✔ |
| B14 | tech-012 m1 | live state + parent in one save → 409, no link | …LiveStateAndAParent | ✔ |
| B15 | US-3 moves | card move, tick → 409 (404 after purge); reorder naming it → `reorder_stale` | DeletedItemIsReadOnly…, Purge… | ✔ |
| B16 | US-3 links | parent route, creation beneath, live save naming it → `item_deleted`; deleted item's save naming a parent → `…deleted` | SurfaceMatrix (writes), DeleteDoesNotCascade… | ✔ |
| B17 | BR-DR-9 | team deletion releases a deleted item; project–team **unlink** | DeletedItemViews…, Race | ✔ / ✖ owed |
| B18 | US-4 | default → held state, `deletion` null, `changedAt` = restore, nothing else | RestoreWorkItemEndpointTests | ✔ |
| B19 | US-4 | chosen state; no body / `{}` / no marks → first; latest deletion remembered | RestoreWorkItem… | ✔ |
| B20 | US-4 refusal | other type, «Удалена», unknown, malformed, zero, foreign org → `state_not_allowed` | RestoreWorkItem… | ◐ upper-case GUID not pinned here |
| B21 | BR-DR-10 | live item → `not_deleted` whatever `stateId` | RestoreWorkItem… | ✔ |
| B22 | BR-DR-10 | team kept / gone; place kept / below placed | RestoreWorkItem… | ✔ |
| B23 | US-4 races | Restore ∥ Restore, Save, Purge, mapping (`parent_changed`) | RaceTests | ✔ |
| B24 | US-4 roles | member/admin; viewer 403; non-member 404; deactivated project | RestoreWorkItem… | ✔ |
| B25 | US-5 | children keep state, link, `changedAt`; tree top-level; leaf delete leaves parent | DeleteDoesNotCascade… | ✔ |
| B26 | US-5 | release / mapping; restore brings links back; chain; **cycle and depth count links through a deleted item** | RestoreDoesNotCascade…, SurfaceMatrix, QA | ✔➕ |
| B27 | US-6 | 204; 404 after; places gone; log line, none on rollback | PurgeWorkItemEndpointTests | ✔ |
| B28 | US-6 | children (live, deleted) unparented, `changedAt` = purge; own parent untouched; 5000 children < 2 s | PurgeWorkItem…, VolumeTests | ✔ |
| B29 | BR-DR-14 | number never reissued | …TheHighestNumber | ✔ |
| B30 | US-6 roles | admin; member/viewer 403; non-member 404; live → `not_deleted`; deactivated 404 | PurgeWorkItem… | ✔ |
| B31 | US-6 races | Purge ∥ Restore, Purge, release, team deletion, reorders; Delete ∥ reorders; 8 parallel purges; **storm** | RaceTests, QA | ✔➕ |
| B32 | BR-DR-16 | reference inventory, non-vacuous; step in the transaction, failure rolls back | WorkItemReferenceInventoryTests (Infra), PurgeWorkItem… | ✔ |
| B33 | ADR-0030 | parity of «deleted»; surface matrix; write inventory; writers rule | DeletedStateParityTests, SurfaceMatrix, WorkItemWriteInventoryTests | ✔ |
| B34 | DB | migration on a clean DB, rollback, re-apply; CHECK; FK | AddWorkItemDeletionMarksMigrationTests, WorkItemDeletionSchemaTests | ✔ |
| B35 | wire | `deletion.by`: former member, disabled, unknown user | QA DeletionBy | ➕ |
| B36 | codes | two codes ru+kk; access codes per role; 50 routes | WorkItemErrorCatalogTests, GetOrganizationAccess…, OrganizationAddressedRoutes | ✔ |

### Added by this pass (`DeleteRestorePurgeQaTests`, 10 tests, green at first run)

| # | Scenario | Expectation |
|---|---|---|
| Q1 | T1 > T2 > T3, T2 deleted, T1 beneath T3 | 409 `workitems.link.cycle`; restore of T2 finds no cycle |
| Q2 | a 25-item subtree with a deleted member moved under a 40-chain; a 64-chain with a deleted ancestor, a task beneath its tail | 409 `depth_exceeded` both (BR-DR-10) |
| Q3 | organization deactivated: Delete, Restore, Purge | 404 `organizations.organization.not_found`, rows unchanged, as before after restore |
| Q4 | `MSK-US-<n>` for a task, three routes | 404 `workitem.not_found`, item intact |
| Q5 | deleter who left / was disabled in the directory / never known | `isMember` false / `isActive` false / `displayName` null, `isActive` false |
| Q6 | junk body (`{ not json`, `text/plain`) on Delete and Purge | ignored, 200 / 204 |
| Q7 | Restore with a malformed, non-string or non-JSON body | 400 `http.bad_request` / 415, item stays deleted (see D-1) |
| Q8 | 8 parallel Delete, Restore, Purge of one item | exactly one wins, the rest 409 `deleted` / `not_deleted` / 404 |
| Q9 | storm: 3 rounds x 60 mixed Delete / Restore / Save / move / Purge / mapping on 10 items | no 5xx, every refusal a named code; live ⇒ no mark, deleted ⇒ three marks |

## Frontend

### Coverage matrix

| # | Screen / criterion | Check | Test | St. |
|---|---|---|---|---|
| F1 | design § 2, § 9 | «⋮» per role and card: copy + «Удалить» (`…delete`); reader copy only; deleted: «Удалить навсегда» (`…purge`), member copy only | WorkItemCard.delete / .deleted | ✔ |
| F2 | § 3 | texts (children, draft), neutral primary, Cancel/Esc/Закрыть keep the draft, busy, one request; Enter on the frame confirms nothing | WorkItemCard.delete, QA keys | ✔➕ |
| F3 | § 3 done | notice, draft dropped, card read-only in place, focus, host re-read | WorkItemCard.delete, WorkItemCardDialog.deletion, WorkItemFormPage.deletion | ✔ |
| F4 | § 4 | read-only fields, no Save group / undo, «Восстановить», notice per role, deletion line with `(неактивен)`, `(не в организации)`, unknown user, none without marks | WorkItemCard.deleted, deletion.test | ✔ |
| F5 | § 5 | preset held / first, list without «Удалена», type's states, hint, deleted-parent sentence, process loading and failed, state sent, focus on title; Enter on the closed list submits nothing | WorkItemCard.restore, QA keys | ✔➕ |
| F6 | § 6 | text, title wraps, error primary, focus «Отмена» (Enter cancels), dialog closes / page `replace` | WorkItemCard.purge, host tests, QA keys | ✔➕ |
| F7 | § 8 | no answer, 5xx, `deleted`, `not_deleted`, `state_not_allowed`, 404, 403, lost code | the three dialog tests, deletionRefusals.test | ✔ |
| F8 | § 8 writes | save loses (D-5), move / tick `deleted` and 404, reorder, `item_deleted` | WorkItemCard.deleted, BoardPage.deleted | ✔ |
| F9 | § 4 mode | follows the category on open, «Обновить», re-read, both ways | WorkItemCard.deleted | ✔ |
| F10 | § 7 | list shows no deleted; deleted parent opens; «Статус» of a live card never offers «Удалена» | WorkItemsPage.deleted, WorkItemCard.deleted, process.ts | ✔ |
| F11 | § 11 | dialogs modal, never `alertdialog`; notice `status`; restore field labelled | QA keys, WorkItemCard.deleted | ✔➕ |
| F12 | contract | paths, no body on delete / purge, `{stateId}`; `deletion`; codes; error codes vs catalog | workItemsApi.test, errorCodes.workitems.test, organizationRoles.test | ✔ |
| F13 | § 10 | widths down to 640 px | none; not claimed | ✖ |
| F14 | E2E | standing NO (2026-10-12): not asked, not run | - | recorded |

Reconciled against design.md by reading the code: every string of § 2–§ 6 equals `cardTexts.ts`; `CardActions`, `RestoreWorkItemDialog` and the card draw the states of § 4, § 5, § 8; the roles of § 9 are the served codes.

## Runs

| What | Result |
|---|---|
| `dotnet test Api.IntegrationTests --filter DeleteRestorePurgeQaTests` | 10 / 10, 13 s |
| one temporary probe of Restore's body (reverted, not committed) | see D-1 |
| `npx vitest run WorkItemCard.deletion.keyboard.test.tsx` | 3 / 3 |
| `BASE=ce7f055b bash scripts/verify.sh` at `f8b6183b`, once | **RESULT green**. dotnet: Shared 53, Application 734, Architecture 86, Contracts 246, Domain 698, Infrastructure 360, Api.IntegrationTests 2483 passed, 17 skipped (live-Keycloak tests behind their switches). Frontend: lint, build, vitest affected 2149 in 162 files, source-reading guards 590 in 13 files (1 skipped) |
| migrations on a clean database | every integration run migrates a fresh Testcontainers database; `AddWorkItemDeletionMarksMigrationTests` rolls back and applies again |

E2E: **not asked, not run** (standing answer NO since 2026-10-12). **Specs the change breaks, not edited:** `27-work-item-card` 27.5 asserts «Ещё действия» holds «Копировать ссылку» only (now «Удалить» follows for a member and an administrator); `27-organization-members` 27.9 asserts the served bundles (`MEMBER_CODES` lacks `workitems.workitem.delete`, `ADMIN_CODES` lacks it and `…purge`). Found by reading `e2e/specs` for the card's menu and the served codes; no other spec reads either.

## Defects

None is a blocker or a major. Both are minor and go to gate-3 as debts.

#### D-1: Restore binds its body before the address is resolved
- Severity: minor · Layer: docs (the contract wording), or backend if the contract stands
- Steps: `POST …/workitems/MSK-TASK-999/restore` with the body `{ not json` or `{"stateId":5}`; the same on a live item.
- Expected / Actual: api-contract § 0.4 says "None of the three reads a body before step 6" and § 0.2 says a value in another form names nothing (409 `state_not_allowed` for a bad `stateId`). Actual: 400 `http.bad_request` (415 `http.request_failed` for a non-JSON body) for an unknown item as well as a live one, before any not-found. No data changes. It is the platform's uniform malformed-request answer; the cheapest fix is to say so in § 0.4 and § 2.2.
- Status: fixed (contract wording)
- Resolution: `api-contract.md` § 0.4, § 0.2 and § 2.2 now say that Restore's optional body is bound by the framework after the scoped check and before the address's not-founds (400 `http.bad_request`; 415 `http.request_failed` for a non-JSON body; a well-formed body naming nothing still reaches 409 `state_not_allowed`); Delete and Purge never read a body. No code changed (the panel sends a string or nothing); note in `plan-backend.md` § 13. Pinned by `DeleteRestorePurgeQaTests.Restore_AMalformedBody_IsAnsweredAfterTheScopedCheck_BeforeTheAddress_AndChangesNothing` (viewer 403 and non-member 404 before the body; unknown, live and deleted item 400/415; the 404 and 409s of a well-formed body; the item stays deleted).

#### D-2: an unknown deleter reads «Неизвестный пользователь» with no «(неактивен)»
- Severity: minor · Layer: design · **for the human**; recommended: keep (A)
- Steps: delete as a member the directory never knew, open the deleted card.
- Expected / Actual: the contract flags the user `isActive: false`; design § 4 says "a user who is inactive … gets the mark" but also takes the name from the «Исполнитель» rule, which has no mark for an inactive or unknown user (`assigneeLabel`: "No mark for an inactive user"). The code follows the assignee rule (`userNameWithMarks`; the developer flagged it in the Log). Options: (A) keep, the name already says what is known; (B) add «(неактивен)» for the unknown too.
- Status: left, option A (keep), for the human
- Resolution: nothing built; the orchestrator's rule is to go on with the recommended option. If the human picks B, the change is one mark in `lib/deletion.ts` (`userNameWithMarks`) and its test.

## Carried, not defects

- **E2E breaks** above (27.5, 27.9): into the gate-3 summary and the MR's "Deliberately left open" with the standing NO.
- **BR-DR-9 unlink on a deleted item** (`Unlink_ReassignsADeletedItemToo_StillDeleted`) is owed by whichever of mod-005 and feat-028 merges second; feat-028 is not in main, so B17 is half.
- **Replay of a creation request** after the item was deleted answers 200 with the deleted item (BR-M3-26: the original result); after a purge the replay creates a new item (ADR-0030 rule 5 accepts it). Not in the spec; no action.
- **Rolling the migration back** keeps items in «Удалена» without marks (restorable to the first state); additive and reversible as designed.
- Upper-case GUID `stateId` on Restore is `AddressedId`'s (mod-003 tests), not pinned on this route (B20). Narrow widths (F13) are manual only.
- `scripts/verify.sh` notes a Log line over 300 characters in `status.md` (the orchestrator's file).
- Security read (no separate session): the scoped code runs before the body; a non-member gets the organization's not-found, the instance administrator included; the purge log line holds user, organization, project and identifier, never the title; `deletion.by.id` is the Keycloak subject the contract defines, as `assignee.id` is.

## Verdict

blockers: 0 · majors: 0 · minors: 2 (D-1, D-2) · result: **pass**

## QA — merging main (feat-028)

A second, narrower pass: `origin/main` at `175aa1db` (feat-028, teams of the organization) merged into `mod/005-work-item-delete-restore`, tip `16ac90d3`. Clean context. **The plan below is written from mod-005's spec and api-contract and feat-028's spec, stories and api-contract (BR-OL-8/-9, SC-09, doc-012 BR-MX-12) before any resolution code, merge diff or new test was read.** It covers only where the two lines meet; the rest of mod-005 was passed above and the rest of feat-028 in its own `qa.md`.

### Plan (where the lines meet)

| # | Meeting point | Check (expected) |
|---|---|---|
| M1 | BR-DR-9 x BR-OL-8: unlink, naming a team | a deleted item owned by MARS is owned by the destination afterwards and **stays** «Удалена»: state, `deletion` {fromState, by, at} untouched, no place on any backlog; `changedAt` = the unlink's time (BR-OL-8 (4)); live twins reassigned alike; the pair is unlinked. The reserved name is `Unlink_ReassignsADeletedItemToo_StillDeleted` |
| M2 | same, destination «Без команды» | the deleted item owns no team, stays deleted; on no team backlog; the project backlog holds neither |
| M3 | BR-DR-9 x BR-OL-9: team deletion | deleted items the team owns, in every linked project, **deactivated ones too**, become «Без команды», stay deleted, marks kept; items of other teams, their parents and children untouched |
| M4 | the any-state row: counts | `unlink-count` and `deletion-counts` count live and deleted alike (e.g. 3 live + 2 deleted = 5), per project, zeros kept, deactivated project listed; Delete of a live owned item, Restore, Purge move the count by 0 / 0 / -1; the count equals what the operation then reassigns; other projects' items never counted |
| M5 | BR-DR-10: Restore after a reassignment | after an unlink, Restore gives state = held (or chosen), `deletion` null, `changedAt` = restore, owner = the unlink's destination, back on the **destination's** backlog unplaced below every placed item (none with «Без команды»), on the project backlog as mod-006 says; after a team deletion: owner none |
| M6 | a deleted item and a later unlink/deletion | Delete after an unlink, then unlink the destination: the deleted item moves again; a deleted item never needs the destination to still be linked at Restore (it is always linked or none) |
| M7 | BR-DR-8 x BR-OL-6: the order of answers on a deleted item | form save naming an unlinked, deleted, foreign, malformed or unknown team: 409 `workitems.workitem.deleted`, not `team.not_found` / `team.not_in_project`; the address's 404s come before; a create beneath a deleted parent: `link.item_deleted`; a live item naming a deleted parent likewise |
| M8 | BR-OL-11 x BR-DR-6: team views at a project's address | team backlog and board show no deleted item, count none, even though the team owns it; an unlinked team: `team.not_found`; a reorder naming a deleted item: `boards.backlog.reorder_stale`; the project views as before |
| M9 | BR-DR-11 x BR-OL-8/-9: pairs, each in both orders | {Delete, Restore, Purge, Save} x {unlink, team deletion} on one item and on a parent with owned children: one order wins, 0 5xx, 0 deadlock (40P01), the final state is consistent: no item owned by an unlinked or deleted team, live => no marks, deleted => all three marks, a purged item gone with its count reduced |
| M10 | the purge of children x a release | Purge of a deleted parent whose children a team owns, against that team's deletion or unlink: both lock work items by id (ADR-0028 § 6 rule 8); children unparented and reassigned, no deadlock |
| M11 | storm | a mixed round of Delete / Restore / Purge / Save / unlink / team deletion / reorder over a few items: no 5xx, every refusal a named code, invariants of M9 |
| M12 | access: all six codes | `GET …/access`: administrator holds the four feat-028 codes (`team.create`, `team.delete`, `team.link`, `project.link`) and `workitems.workitem.delete`, `…purge`; «Участник» holds `…delete` only of the six; «Читатель» none; the organization codes count 23 (17 + 4 + 2), none twice; the route inventory holds every route of both lines with its declared code; a team's deletion and an unlink need no code of the items they reach |
| M13 | ADR-0029 § 4 matrix and catalogs | the two ADR-0030 rows are placed beside feat-028's four; the permission catalog, role bundles, the served `GET …/access` and the panel's fixtures agree (one list) |
| M14 | migrations | `OrganizationLevelTeams` (20261017) before `AddWorkItemDeletionMarks` (20261018): a clean database migrates to the head, no pending model change in the snapshot, the second rolls back and re-applies; the upgrade's drop of owning teams reaches items in the Removed category and moves no `changedAt` |
| M15 | error catalogs | `docs/error-codes.md`, `contracts/errors/*.json`, the panel's `errorCodes.ts` hold the codes of both lines (`workitem.deleted`, `not_deleted`, `team.not_in_project`, `destination_required`, …), none lost in the merge |
| F1 | the unlink and deletion dialogs | the sentence states the server's number as given (any state, deleted included); no client-side "live only" filter; per-project counts incl. deactivated; unchanged copy |
| F2 | a deleted card x owning team | the read-only card shows «Команда» as stored (a name, or «Без команды»); the team control is a value; Restore sends no team and shows no team control; after Restore the card shows the stored owner |
| F3 | the served codes in the panel | the panel's role fixtures hold the 23 codes; «Участник» shows «Удалить» and no team-management control; the administrator both |
| F4 | E2E | standing NO: list the specs the merge breaks by reading `src/frontend/e2e/specs` for the served codes and the card's menu |

### Checked against the developers' tests

✔ a developer test covers it · ➕ a test this pass added (`DeleteRestorePurgeFeat028QaTests`, 17 cases) · ◐ partly · ✖ not covered. Classes are `Api.IntegrationTests/Features/WorkItems` unless named.

| # | Covered by | St. |
|---|---|---|
| M1, M2 | `DeletedItemViewsEndpointTests.Unlink_ReassignsADeletedItemToo_StillDeleted` (theory: a team / «Без команды») — the reserved name is there: owner, state and the three marks, `changedAt` moves, the team place goes, Restore after | ✔ |
| M3 | `DeleteTeam_ReleasesADeletedItemToo_StillDeleted_ChangedAtMoves` (a live project) · ➕ `M3_…`: a deleted item of a **deactivated** project released and restorable with no owner; a deleted parent released while its child of another team and another team's deleted item stay equal to the byte | ✔➕ |
| M4 | `OwnedItemCount_CountsADeletedItem`, `SurfaceMatrix` row 7 (one project, one deleted) · ➕ `M4_…`: Delete +0, Restore +0, Purge -1; a second project, the deactivated one listed with its deleted item; the number is what the destination receives | ◐➕ |
| M5, M6 | the unlink theory (Restore lands below the destination's placed items; «Без команды» on the project's lists only), `Restore_AfterItsTeamWasDeleted…`, `SurfaceMatrix` (unlink, then deletion, over one deleted item), the two Unlink x Restore races | ✔ |
| M7 | `Patch_ADeletedItem_…("team")` with a linked and with an unknown team · ➕ `M7_…`: a team linked to no project (would be 409 `not_in_project`), a deleted and a foreign team (404) on a deleted item all answer 409 `…deleted`; the same bodies on a live item get the team's own refusals | ◐➕ |
| M8 | `SurfaceMatrix` (team board and backlog), `Reorder_…NamingADeletedItem…` · ➕ the destination's backlog after the unlink in M4 | ✔➕ |
| M9 | eight races, both orders: Unlink x Delete / Restore / Purge, TeamDeletion x Restore · ➕ TeamDeletion x Delete and x Purge (4), Unlink x a save of a deleted item naming the team (1). **A parent write against the clean-up is in neither line's suite, and it fails: QD-1** | ◐➕ ✖ |
| M10 | none · ➕ `M10_…` (8 cases): Purge of a deleted parent with three owned children (one deleted) x unlink / deletion, both orders, the parent's id below and above its children's | ➕ |
| M11 | mod-005's storm has no clean-up · ➕ `M11_Storm…`: 3 rounds x 50 writes and one clean-up each, 9 stories, 3 teams | ➕ |
| M12 | `PermissionCatalogTests`, `OrganizationRoleBundlesTests` (23 codes x 3 roles; the six cells: administrator 6, «Участник» `…delete` alone, «Читатель» none), `GetOrganizationAccessHandlerTests` (23 / 11 / 7), `OrganizationAccessInventoryTests` (59 routes), frontend `organizationRoles.test.ts` (reads the three contracts) | ✔ |
| M13 | read: ADR-0029 § 4 holds feat-028's four rows, ADR-0030 rule 9 the two others | ✔ |
| M14 | `AddWorkItemDeletionMarksMigrationTests` (clean database, back to `OrganizationLevelTeams`, again, no pending model change), `OrganizationLevelTeamsMigrationTests`; read: the upgrade's `UPDATE work_items SET owning_team_id = NULL` has no state filter, and nothing is in «Удалена» before the second migration | ✔ |
| M15 | read: the registries differ from main by two additive entries per file; both catalog test classes green | ✔ |
| F1, F2 | `TeamsPage` / `OrganizationTeamsPage` tests (the server's number as given, no work item read), `WorkItemCard.deleted` / `.restore` (the owner as stored: a destination, «Без команды»; the request names no team) | ✔ |
| F3 | `organizationRoles.test.ts`, `testMembership.test.ts` (the three item routes' codes) | ✔ |

### Runs

| What | Result |
|---|---|
| `dotnet test Api.IntegrationTests --filter DeleteRestorePurgeFeat028QaTests`, first run | 17 / 17 (green at first run) |
| a scratch probe of QD-1, three variants (deleted parent x unlink, deleted parent x team deletion, live parent x unlink); not kept | 3 / 3 reproduce |
| `BASE=origin/main bash scripts/verify.sh` at `e0076300`, once | **RESULT green.** dotnet: Shared 53, Contracts 248, Architecture 86, Application 779, Domain 733, Infrastructure 412, Api.IntegrationTests 2851 passed, 17 skipped (live-Keycloak switches). Frontend: lint, build, vitest affected 2269 in 165 files, source-reading guards 590 in 13 (1 skipped). Docs checks pass |
| the developers' ten new race tests + the 17 of this pass, 10 times (a hypothesis about rare orders, not a ritual) | 10 / 10 runs, 27 / 27 each, 17-20 s a run |

E2E: **not asked, not run** (standing NO since 2026-10-12). **Specs the merge breaks, not edited:** nothing the meeting adds to the union of the two lines. `27-organization-members` 27.9 (the served bundles lack six codes: feat-028's four, mod-005's two); `27-work-item-card` 27.5 (the card's «Ещё действия» now holds «Удалить» for a member and an administrator) and `:135` (the project's team); and feat-028's own list (`feat-028…/plan-backend.md` § 11: `19-shell-in-a-browser`, `21-organizations`, `23-backlogs-boards`, `24`, `25`, `26`, `27-organization-members:401`, `28-team-members`, `29-role-views` and the support files).

### Defects

#### QD-1: a parent write meets an unlink or a team deletion in a lock-order cycle, and the clean-up answers 500
- Severity: **major** · Layer: backend · BR-DR-11, NFR Concurrency ("each write of another item that BR-DR-11 names"); also found, not fixed, by the developer (`plan-backend.md` § 14.3 "Carried")
- Steps (all three variants reproduce every time): in one project the team «Платформа» owns a story P (created first, so the lower id) and a task C. Variant A: P is deleted. 1) A side session holds `SELECT 1 FROM areas WHERE id = <the API area> FOR UPDATE`. 2) `PATCH …/workitems/C` with `{ areaId: <that area>, parent: { parentId: P, seenParentId: null } }` takes the hierarchy lock and C's row and blocks on the area. 3) `POST …/teams/{platform}/unlink` (destination «Мастаск»), or `DELETE …/teams/{platform}`, locks P by id and blocks on C. 4) The side session commits.
- Expected / Actual: the save is refused 409 `workitems.link.item_deleted` (P live: it saves, 200) and the clean-up succeeds, 204 (BR-DR-11: one order, no 5xx, no deadlock). Actual: the save answers as expected and the unlink / the team's deletion answers **500 `http.internal_error`** (`40P01`), all three variants. Cause: the clean-ups lock the owned items `ORDER BY id` and take no hierarchy lock; a parent write takes the hierarchy lock, then the child, then the parents, not by id. The deleted-parent variant needs this line's data, but the live one is main's alone (feat-028 x feat-001); neither suite has the pair. A purge is safe (it takes the hierarchy lock first), and Delete / Restore hold one row.
- Fix (either): `UnlinkTeamHandler` and `DeleteTeamHandler` take `LockHierarchyAsync` for each affected project before `LockOwnedAsync` (the tier between the project row and the items in `plan-backend.md` § 14.3, as the purge does; two statements more each, `UnlinkStatements` / `DeletionStatements` 11 -> 13), pinned by a race test of the steps above; or the parent write locks child and parents in one statement by id. A human may instead accept it as a debt with a bug line and BR-DR-11 worded for the pair: that is a decision, not a QA call.
- Status: open

### Verdict (merge pass)

blockers: 0 · majors: 1 (QD-1) · minors: 0 · result: **defects**. Everything else in the plan is covered and green: the unlink and the team's deletion reach a deleted item in any state, the counts agree, the access inventory holds all six codes, the migrations chain and the registries merged without loss.
