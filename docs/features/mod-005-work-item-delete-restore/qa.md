# QA — mod-005-work-item-delete-restore

One pass over both layers (`M`/`L` with a UI, `05-frontend.md` § 7), clean context. The plan below was written from the spec, the stories, the api-contract, the design and ADR-0030 **before any implementation file was read**; the Status column is filled after.

## Backend

### Coverage matrix (plan)

| # | Criterion | Check | Type | Status |
|---|---|---|---|---|
| B1 | US-1 Delete | 200, state «Удалена» of the item's own type, `deletion` {fromState, by, at}, `changedAt` = deletion time; any non-Removed state, all five types | integ | ☐ |
| B2 | US-1 / BR-DR-3 | nothing else changes: title, fields, assignee, area, team, priority, parent; the parent's and other items' `changedAt`; backlog places | integ | ☐ |
| B3 | US-1 / BR-DR-11 | Delete twice: 409 `…deleted`, the held state is the first's; Delete in parallel x N: one 200, rest 409, no 5xx | integ | ☐ |
| B4 | US-1 race | Save ∥ Delete both orders: save-first → held state is the saved one; delete-first → save 409; no 5xx | integ | ☐ |
| B5 | US-1 roles | Delete: admin, member 200; viewer 403 (item untouched); non-member and instance admin 404 org; foreign/unknown item 404; deactivated project/org 404 and unchanged; a body is ignored | integ | ☐ |
| B6 | US-1 retention | no scheduled/background job changes work items | arch/integ | ☐ |
| B7 | US-2 list | `GET workitems` neither returns nor counts a deleted item, any page; order/paging/team filter unchanged | integ | ☐ |
| B8 | US-2 views | team and project backlogs, boards, three levels: no row, card, column count, place; others keep order | integ | ☐ |
| B9 | US-2 carried data | deleted item keeps team/area/assignee/parent; area deletion guard counts it (`area.not_empty`); team deletion counts and releases it, it stays deleted | integ | ☐ |
| B10 | US-2 address | `GET …/{id}` returns a deleted item in full to admin, member, viewer; foreign org 404; deactivated project hides it, reactivated shows it with its marks | integ | ☐ |
| B11 | US-2/US-5 children | live child shows the deleted parent (`state` Удалена); parent picker, «Дочерние элементы» and count, checklist and counter skip a deleted item; restored it returns in creation order | integ | ☐ |
| B12 | US-3 save | PATCH of a deleted item with any field, assignee, team, area, state (live or not), parent, `{}` → 409 `…deleted`, `changedAt` unchanged | integ | ☐ |
| B13 | US-3 order | 409 before 400 (300-char title); 404 before 409; 403 for a viewer (not 409); 403 before the project's 404 | integ | ☐ |
| B14 | US-3 tech-012 m1 | live state + parent in one save → 409; no new child, parent's `changedAt` unchanged | integ | ☐ |
| B15 | US-3 moves | card move (state save) and checklist tick → 409; after a purge 404; reorder (team and project level) naming it as moved or anchor → 409 `reorder_stale`, order unchanged | integ | ☐ |
| B16 | US-3 links | parent route set/replace/clear on a deleted item, a deleted parent, creation beneath a deleted parent, a live save naming a deleted parent → `link.item_deleted`; a deleted item's save naming a parent → `…deleted` | integ | ☐ |
| B17 | US-3 clean-up | team deletion releases a deleted item: «Без команды», still deleted, `changedAt` moves, `deletion` unchanged (unlink on a deleted item: owed, feat-028 not in main) | integ | ☐ |
| B18 | US-4 Restore | default → held state; `deletion` null; `changedAt` = restore time; nothing else (team, parent, fields, parent's `changedAt`, children) changes | integ | ☐ |
| B19 | US-4 chosen | chosen state of the type; held state forgotten; `stateId` absent / null / `{}` / no body → held; no marks → first state; second deletion remembers the latest state | integ | ☐ |
| B20 | US-4 refusal | `state_not_allowed` for another type's state, «Удалена», unknown, malformed, all-zero, another organization's state; uppercase GUID accepted; item stays deleted | integ | ☐ |
| B21 | US-4 order | live item → `not_deleted` whatever `stateId` names (before `state_not_allowed`) | integ | ☐ |
| B22 | US-4 place | owner team kept; team deleted meanwhile → none; backlog place kept if nobody reordered, below every placed item otherwise | integ | ☐ |
| B23 | US-4 races | Restore ∥ Restore, Restore ∥ Save (both orders), Restore ∥ Purge, Restore ∥ a child's mapping (`parent_changed`) | integ | ☐ |
| B24 | US-4 roles | member, admin OK; viewer 403; non-member 404; deactivated project 404 and then restorable after reactivation | integ | ☐ |
| B25 | US-5 no cascade | Delete parent: children keep state, parent link, `changedAt`; backlog tree shows them top-level, «Родитель» none; Delete a child: parent untouched | integ | ☐ |
| B26 | US-5 links | child releases/replaces the link to a deleted parent (form save, mapping), parent unchanged incl. `changedAt`; Restore parent brings kept links back, released stay released; restoring a child restores no parent; chain deletion, any restore order | integ | ☐ |
| B27 | US-6 Purge | 204; address, restore, save, second purge → 404; places gone, others keep order; log line (user, org, project, identifier; no title), none on rollback | integ | ☐ |
| B28 | US-6 children | children (live and deleted) lose the parent link, keep state/owner/places, `changedAt` = purge time; the purged item's own parent untouched incl. `changedAt`; 5,000 children < 2 s | integ | ☐ |
| B29 | US-6 number | the number is never reissued, under any type key | integ | ☐ |
| B30 | US-6 roles | admin 204; member 403, viewer 403 (item intact); non-member and instance admin 404 org; live item 409 `not_deleted`; deactivated project 404 | integ | ☐ |
| B31 | US-6 races | Purge ∥ Restore, ∥ Purge, ∥ child release, ∥ team deletion, ∥ project and team reorder, Delete ∥ reorder: one order, no 5xx | integ | ☐ |
| B32 | BR-DR-16 | reference inventory fails on an unlisted reference and is not vacuous; a fake purge step runs inside the transaction and a throwing step rolls everything back | unit/integ | ☐ |
| B33 | ADR-0030 r2/r3 | parity of the three expressions of «deleted»; surface matrix; write inventory; writers rule | integ/arch | ☐ |
| B34 | DB | migration applies on a clean DB and repeatedly, reverts; CHECK rejects partial marks; FK `Restrict` refuses removing a remembered state | migration | ☐ |
| B35 | wire | `deletion.by` flags: unknown user (`displayName` null, `isActive` false), former member (`isMember` false); no e-mail | integ | ☐ |
| B36 | codes | the two error codes in `contracts/errors/workitems.json` with ru + kk, `docs/error-codes.md` regenerated; `GET …/access` serves both codes per role; route inventory 50 rows | integ | ☐ |

### Additional scenarios

Boundaries and hidden surfaces: every other read of `work_items` outside the matrix (counts, searches, project/org summaries, exports, "my" views) must choose an entry point; a missed one is a defect (B33 only covers what the matrix lists). A purge of an item that is a team backlog member; Delete of an item sitting on both places. Double-cased `Content-Type` and a malformed JSON body on the body-less routes.

### Runs

(filled after the runs)

### Defects

(filled after the runs)

## Frontend

### Coverage matrix (plan)

| # | Screen / criterion | Check | Type | Status |
|---|---|---|---|---|
| F1 | design § 2, § 9 menu | live card: «Копировать ссылку» + «Удалить» per `…delete`; reader: copy only; deleted: copy + «Удалить навсегда» per `…purge` only (member sees copy only); divider only between drawn groups | component | ☐ |
| F2 | design § 3 Delete dialog | title, three-part text (children count, draft), buttons, neutral primary; Cancel/Esc/Закрыть change nothing and keep the draft; busy; double activation sends one request | component | ☐ |
| F3 | design § 3 done | notice, draft dropped, card re-read and read-only in the same host, focus on «Ещё действия», host views re-read | component | ☐ |
| F4 | design § 4 deleted card | fields read-only, no Save group, no «Отменить все изменения»; «Восстановить» for `…delete` only; notice text per role; deletion line with name, `formatDateTime`, «(неактивен)», «(не в организации)», «Неизвестный пользователь»; no line without marks | component | ☐ |
| F5 | design § 5 Restore dialog | preset held state or first state; options of the type without the Removed category; hint only while held; deleted-parent sentence; process loading and failed; sends the state shown; done notice, card editable, focus on title | component | ☐ |
| F6 | design § 6 Purge dialog | text with title and children count, error-colour primary, focus on «Отмена»; dialog host closes, page navigates with `replace`; notice | component | ☐ |
| F7 | design § 8 matrix | 5xx / no answer, `deleted`, `not_deleted`, `state_not_allowed`, 404 x3, 403 for each of the three dialogs | component | ☐ |
| F8 | design § 8 writes | Save loses to a deletion (also with a live state + parent) → notice with server text, card read-only, draft dropped; move/tick `deleted` / `not_found`; reorder `reorder_stale`; `link.item_deleted` placements | component | ☐ |
| F9 | design § 4 mode | mode follows the category of the read both ways (open, «Обновить», re-read after refusal); «Данные обновлены.» not "остались на месте" | component | ☐ |
| F10 | design § 7 | live child shows the deleted parent with «Удалена» and its link opens the deleted card; no control lists deleted items; the live card's state list never offers «Удалена» | component | ☐ |
| F11 | design § 11 a11y | dialogs `dialog` modal (not `alertdialog`), names; deleted notice `status`; restore field `combobox` labelled | component | ☐ |
| F12 | contract | API layer: paths, methods, no body on delete/purge, `{stateId}` on restore; `deletion` type; access codes; error codes in `errorCodes.ts` match the catalog | unit | ☐ |
| F13 | design § 10 | narrow widths to 640 px | manual (not claimed) | ☐ |
| F14 | E2E | standing NO (paused 2026-10-12): not asked, not run; the specs the change breaks are listed | - | ☐ |

### Runs

(filled after the runs)

### Defects

(filled after the runs)
