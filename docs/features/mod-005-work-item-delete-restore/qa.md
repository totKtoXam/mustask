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
- Status: open

#### D-2: an unknown deleter reads «Неизвестный пользователь» with no «(неактивен)»
- Severity: minor · Layer: design · **for the human**; recommended: keep (A)
- Steps: delete as a member the directory never knew, open the deleted card.
- Expected / Actual: the contract flags the user `isActive: false`; design § 4 says "a user who is inactive … gets the mark" but also takes the name from the «Исполнитель» rule, which has no mark for an inactive or unknown user (`assigneeLabel`: "No mark for an inactive user"). The code follows the assignee rule (`userNameWithMarks`; the developer flagged it in the Log). Options: (A) keep, the name already says what is known; (B) add «(неактивен)» for the unknown too.
- Status: open (decision)

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
