# API contract — mod-005-work-item-delete-restore

Fixed at stage 4a (2026-10-18), before the code. From now on it changes only with a note in
[status.md](status.md) and a Log line; once stage 6 (frontend) has started, a change goes to the orchestrator
(04-backend.md § 4a.4). The plan is [plan-backend.md](plan-backend.md); the rules are [spec.md](spec.md) (BR-DR-n;
BR-M3-n are mod-003's, BR-H-n feat-001's, BR-OT-n mod-017's) and the model is
[ADR-0030](../../adr/0030-work-item-recycle-bin.md). Plan decisions D-Pn are the plan's § 1.

This task is **additive**. It adds three routes, one field on `WorkItem`, two error codes and two permission codes, and
one migration. It **changes four answers of merged routes** (the list stops returning deleted items; a save of a deleted
item is refused first; the read gains `deletion`; the order of answers gains one step), each restated in § 3. It removes
nothing. Merged contracts are not edited; § 3 and § 9 say what supersedes what.

## 0. Rules shared by every endpoint

Exactly the earlier contracts' ([mod-003](../mod-003-work-item-core/api-contract.md) § 0,
[feat-001](../feat-001-work-item-links/api-contract.md) § 0, [mod-016](../mod-016-organization-access/api-contract.md) § 0,
[mod-017](../mod-017-work-item-owning-team/api-contract.md) § 0), plus the rules below.

### 0.1 Access (BR-DR-17; ADR-0030 rule 9)

- **Authentication, organization context:** a Bearer token from realm `mustask`, or 401 `http.unauthorized` first. The
  scoped check runs before the body is read: a non-member of the organization of the address — the instance
  administrator included — gets 404 `organizations.organization.not_found` «Организация не найдена.»; a member whose
  organization role lacks the route's code gets 403 `organizations.access.forbidden` «Недостаточно прав».
- **Two new organization codes**, checked on the organization role alone until `mod-019-scoped-access` (ADR-0029 "What the
  delivery lines implement"); belonging to a team grants nothing:

  | Code | Declared by | «Администратор» | «Участник» | «Читатель» |
  |---|---|---|---|---|
  | `workitems.workitem.delete` | Delete, Restore | yes | yes | no |
  | `workitems.workitem.purge` | Purge | yes | no | no |

  Both are served by `GET …/access` in the role's list (mod-016 § 1). Reading a deleted item needs
  `workitems.workitem.read` as for any item. The scoped columns of ADR-0030's two matrix rows are applied by mod-019.
- **A deactivated project or organization** answers the container's not-found for every operation below, deleted items
  included, and nothing changes (ADR-0026 rules 3 and 5).

### 0.2 Identifiers

As mod-003 § 0.2: the item is addressed by its identifier (`MSK-US-12`) under the project's id; a state id in a body is an
untrusted string accepted only in the canonical 8-4-4-4-12 form in either letter case and not all zeros. A string in any
other form names nothing; a `stateId` that is not a JSON string is a malformed body (§ 0.4).

### 0.3 Not found, from the outside in

mod-003 § 0.3, unchanged: organization → project → work item. A purged item, another project's or organization's
identifier, an unknown number and a type key that differs from the item's type all answer
404 `workitems.workitem.not_found` «Рабочий элемент не найден.» alike (BR-DR-14, BR-DR-17).

### 0.4 The order of answers

For Delete, Restore and Purge, the first of:

1. 401 `http.unauthorized`;
2. 404 `organizations.organization.not_found` (a non-member);
3. 403 `organizations.access.forbidden` (the route's code);
4. 404 `organizations.project.not_found`;
5. 404 `workitems.workitem.not_found`;
6. the operation's own 409s, in the order its section gives.

Delete and Purge take no body and never read one: a body of any type or shape is ignored. Restore's optional body is
bound by the framework, as every JSON body of the platform is (mod-017 § 0.2): after step 3, which the scoped check
decides before any body is read, and **before** steps 4 and 5. A body that is not JSON of the documented shape
(`{ "stateId": <string or null> }`) therefore answers 400 `http.bad_request` — 415 `http.request_failed` when the body is
not JSON at all — for any address, an unknown item included, and changes nothing. A well-formed body whose `stateId`
names nothing is not that: it reaches step 6 as 409 `state_not_allowed`. For the save, § 3.4 gives the order.

### 0.5 Error body

`application/problem+json`, as everywhere (mod-003 § 0.5). The two new codes carry no `params`.

## 1. Shapes

### 1.1 `WorkItem` gains `deletion` (mod-003 § 1.1)

Every route that answers `WorkItem` answers it: the read, the create, the save, Delete and Restore.

```json
{
  "id": "0199b0a2-…",
  "identifier": "MSK-US-12",
  "state": { "id": "…", "name": "Удалена", "category": { "code": "Removed", "label": "Удалённые" } },
  "changedAt": "2026-10-18T09:14:00.123456+00:00",
  "deletion": {
    "fromState": { "id": "…", "name": "Активна", "category": { "code": "InProgress", "label": "В работе" } },
    "by": { "id": "5d1e8f3a-…", "displayName": "Анна Смирнова", "isActive": true, "isMember": true },
    "at": "2026-10-18T09:14:00.123456+00:00"
  }
}
```

| Field | Meaning |
|---|---|
| `state` | unchanged. **An item is deleted exactly when `state.category.code` is `Removed`** (BR-DR-1, BR-DR-18): the category decides, never the name and never `deletion` |
| `deletion` | `null` for a live item, and for an item in «Удалена» that has no marks (one put there outside Delete, BR-DR-10). Otherwise the record of the latest deletion (BR-DR-2); Restore clears it, a later Delete records anew |
| `deletion.fromState` | the state the item held when it was deleted, as `state` is shaped: the restore's preset (BR-DR-10). A state of the item's type |
| `deletion.by` | who deleted it, as a user reference: `id` is the Keycloak subject; `displayName` is `null` when the user directory does not know the id (the panel reads «Неизвестный пользователь»); `isActive` is the directory's flag (`false` for an unknown, disabled or deleted user); `isMember` is whether that user is **now** a member of the item's organization, in any role, from the membership alone (as `assignee.isMember`, mod-016 § 3.3). The marks «неактивен» and «не в организации» are drawn from the two flags (BR-13, BR-DR-7); no e-mail is served |
| `deletion.at` | when it was deleted, UTC with its offset (mod-003 § 0.6). It is **not** `changedAt`: a team's deletion that releases the deleted item moves `changedAt` and leaves `deletion.at` |

`changedAt` takes the deletion's time at Delete and the restore's time at Restore (BR-DR-2). Nothing else of the item
changes at either (BR-DR-3, BR-DR-10).

`WorkItemListItem` (mod-003 § 1.2), `WorkItemRef` (feat-001 § 2.1), the backlog and board rows (mod-006, feat-027,
feat-039) and the checklist rows (feat-032) **do not change**: a deleted item is on none of them (§ 3.2), except as a live
child's stored parent, where `WorkItemRef.state` reads «Удалена» as it did (feat-001 BR-H-8).

## 2. Routes

The base is `/api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}` (`…/{identifier}` below).
OpenAPI tag `WorkItems`. All three are organization-addressed and carry the scoped check; the route inventory lists them.

### 2.1 `POST …/{identifier}/delete` — delete (US-1; BR-DR-1..-5, -8, -11)

No body (a body is ignored). Moves the item into its type's one state of the Removed category, «Удалена»
(BR-DR-1) and records on the same row the state it held, who deleted it and when (BR-DR-2). **Nothing else changes**
(BR-DR-3): not the title, fields, assignee, area, owning team, parent, children, backlog places or any other item, and
no child is moved, deleted or re-stated (BR-DR-4). «Изменён» takes the deletion's time. An item in any state outside
«Удалена» is deletable. No revision check: it is a separate write and is never blocked by a stale form (BR-DR-11).

- **200** `WorkItem` — the item as stored: `state` «Удалена», `deletion` set.
- **401**, **403** `organizations.access.forbidden` (code `workitems.workitem.delete`).
- **404** organization, project, work item (§ 0.3).
- **409** `workitems.workitem.deleted` — the item is already in «Удалена» (a second Delete, or a Delete decided after
  another user's), decided on the **stored** state under the item's lock. The held state is the first deletion's.

### 2.2 `POST …/{identifier}/restore` — restore (US-4; BR-DR-10, -11)

```json
{ "stateId": "0199b0a3-…" }
```

| Field | Meaning |
|---|---|
| `stateId` | optional. A state of the item's own type other than its «Удалена». Absent, `null` or `{}`, or no body: the state held at deletion (`deletion.fromState`), else — for an item with no marks — the type's first state |

The state becomes the chosen one, else the held one, else the first; the three marks are cleared; «Изменён» takes the
restore's time; nothing else changes. The item is back on every surface it qualifies for as stored (BR-DR-10): its
owning team (none if that team was deleted meanwhile, BR-OT-12), its kept links, and its backlog place as mod-006 gives
an item that comes back (where it stood unless a reorder meanwhile dropped it, else below every placed item). **No hierarchy
check**: a restore is never refused for a cycle or a depth. Restoring a child restores no parent, and a parent restored
brings back the links its children kept (BR-DR-4).

- **200** `WorkItem` — the item as stored, live, `deletion: null`.
- **400** `http.bad_request` (**415** `http.request_failed` for a body that is not JSON) — the body is not JSON of the
  shape above, e.g. `{ not json` or `{"stateId":5}`. It comes from the framework's binding, after the scoped check and
  before the not-founds of the address (§ 0.4); nothing changes.
- **401**, **403** `organizations.access.forbidden` (code `workitems.workitem.delete`).
- **404** organization, project, work item (§ 0.3).
- **409**, in this order: `workitems.workitem.not_deleted` — the item is not in «Удалена», **whatever `stateId` names**;
  then `workitems.workitem.state_not_allowed` — `stateId` is malformed, unknown, a state of another type or organization,
  or the type's «Удалена». A refusal changes nothing.

### 2.3 `POST …/{identifier}/purge` — remove for good (US-6; BR-DR-12..-15)

No body. Only of an item in «Удалена». In one transaction the item and everything keyed to it that exists when this ships
go: its team backlog place, its project backlog place and the item itself. Its **children survive**, deleted ones
included: each loses its parent link and its «Изменён» takes the purge's time; a deleted child stays deleted (BR-DR-13).
The purged item's own parent is not touched. **The number is never issued again** under any type key (BR-DR-14). Nothing
of the item is kept: no tombstone, no copy; one operational log line (user, organization, project, identifier, and the
deleter's Keycloak subject and the deletion's time, which go with the row; never the title) is written after the commit
(BR-DR-15). Data that later lines key to a work item leaves in the same transaction,
by a cascading key or a purge step (ADR-0030 rule 7; BR-DR-16).

- **204** — removed. Idempotent in effect, not in answer: a repeat finds no row.
- **401**, **403** `organizations.access.forbidden` (code `workitems.workitem.purge`: «Администратор» only).
- **404** organization, project, work item (§ 0.3) — also for an item purged a moment ago, and for a Restore, a save or a
  Purge that arrives after it.
- **409** `workitems.workitem.not_deleted` — the item is outside «Удалена» (a live item is never purged), or a restore got
  there first. A pre-flight of a later line's purge step may add its own 409 or 5xx (mod-004's, while the Drive is
  unreachable); this line registers none.

### 2.4 What the client sees of the races (BR-DR-11)

Each pair ends in one order and no server error; the plan's § 8.2 has the locks. Delete then Delete: the second 409
`…deleted`. Save then Delete: the delete records the state the save left; Delete then save: 409 `…deleted`. Restore then
Restore, or a Purge of a live item: 409 `…not_deleted`. Restore then Purge: 409 `…not_deleted`; Purge then Restore: 404.
After a restore, a parent write that saw no parent answers 409 `workitems.link.parent_changed` (feat-001 BR-H-4).

## 3. What changes in merged contracts (restated here)

### 3.1 mod-003 § 0.4 — the order of answers gains one step

For the save, between the path's not-found and the 400: **409 `workitems.workitem.deleted`**. The order is now: 1. the
not-found of § 0.3 for the path; 2. **the item is in «Удалена»: 409 `workitems.workitem.deleted`**; 3. 400
`workitems.validation_failed`; 4. the not-found of a body reference (area, team, parent); 5. the 409s: the state, the
assignee, the parent's. mod-016 § 4.2's 403 for the second code (`workitems.link.write`, when `parent` is sent) and
mod-017 § 0.4's team step keep their places; the new step is decided on the item as stored, under its lock, before any
value is applied (BR-DR-8).

### 3.2 mod-003 § 2.1 — the list stops returning deleted items

Replaces "every item of the project in every state (BR-M3-29)": `GET …/workitems` returns the project's items **outside
«Удалена»**, and `count` counts the same set, whatever the page. Order, paging, the team filter (mod-017 § 3) and the 50
rows a page are unchanged. There is no parameter that lists deleted items in this line (spec OQ-2): feat-003's «Удалена»
filter is the way in. The same set — live items only — is what the team and project backlogs and boards, the backlog tree,
the parent picker, «Дочерние элементы» and a board card's checklist already return (mod-006, feat-001, feat-027, feat-032,
feat-039); a team's owned-items counts — the unlink's count and the deletion's counts (feat-028 § 1.5, which replaced mod-017
§ 2.9) — and the area deletion guard (mod-003 § 6) count **every** state.

### 3.3 mod-003 § 2.3 / feat-001 § 3.1 — the read gains `deletion`

`GET …/{identifier}` answers a deleted item **in full**, read-only for the client (BR-DR-7), with `state` in the Removed
category and `deletion` as § 1.1 shapes it. It needs `workitems.workitem.read` alone. Everything else is unchanged.

### 3.4 mod-003 § 2.4 and feat-001 § 3.3 — the save refuses a deleted item first

`PATCH …/{identifier}` on an item in «Удалена» answers **409 `workitems.workitem.deleted`** «Рабочий элемент удалён и
доступен только для чтения.» whatever the body carries — any field, a state (live or not), the team, the assignee, the
area, a parent, or nothing (`{}`) — and changes nothing, `changedAt` included. The order of answers (the full list, with
mod-016's and mod-017's steps):

1. 401; 2. 404 organization (a non-member); 3. 403 (`workitems.workitem.write`; first of all, `workitems.link.write` when
`parent` is sent); 4. 404 project; 5. 404 work item; 6. **409 `workitems.workitem.deleted`**; 7. 400 values (every field
reason at once); 8. 404 area, then team; 9. 404 `workitems.link.parent_not_found`; 10. 409 `state_not_allowed`, then
`assignee_unavailable`, then the parent's 409s (feat-001 § 3.2, checks 3-8).

This closes tech-012 m1: a save that sends a live state together with a parent can no longer link an item in «Удалена»,
because the stored state is judged before anything is applied. **What stays**: a form save of a **live** item that names a
deleted parent, and the parent route (`PUT …/{identifier}/parent`) for a deleted item, a deleted parent or a deleted child,
answer 409 `workitems.link.item_deleted` as before (feat-001 § 3.2 check 3); a creation beneath a deleted parent (feat-027)
the same. feat-001 § 3.3 is changed only in that a save of a **deleted** item that names a parent answers `…deleted` at
step 6 and no longer `…item_deleted` at step 10. A card move between board columns and a checklist tick are this save
with a `stateId` (feat-032); a deleted card answers 409 `…deleted`, a purged one 404.

### 3.5 feat-001 § 0.4 — "No route of this cut puts an item there"

Superseded: `POST …/delete` is the only route that puts an item in «Удалена», and `POST …/restore` the only one that takes
it out (BR-DR-1). The server still judges "deleted" by the state's category (D-8).

### 3.6 mod-006 and feat-039 — a reorder naming a deleted item

Unchanged on the wire and restated: `POST …/backlogs/{level}/reorder` (a team's and the project's) naming a deleted item
as the moved item or as the anchor answers 409 `boards.backlog.reorder_stale` and the order does not change (a deleted
item is a member of no backlog). **No wire change**, but the project backlog's reorder now locks its members as the team's
does (plan § 7.5): it cannot fail with a server error when a member is deleted or purged meanwhile.

### 3.7 mod-017 — clean-up still reaches a deleted item

A team's deletion releases the items it owns **in any state** (BR-OT-12): a deleted item becomes «Без команды» and stays
deleted, `changedAt` takes the deletion of the team, `deletion` is untouched (BR-DR-9). The same holds for feat-028's
project–team unlink (`POST …/projects/{projectId}/teams/{teamId}/unlink`), tested at the merge
(`Unlink_ReassignsADeletedItemToo_StillDeleted`): the item goes to the destination (a team, or «Без команды»), stays deleted with
its state and its three marks as they were, and a Restore afterwards brings it back owned by that destination (BR-DR-10).

### 3.8 mod-016 § 4.2 and ADR-0029 § 4 — the codes and the inventory

Three rows join the route inventory, after the item routes (§ 5). ADR-0030 rule 9 places the two matrix rows:

| Code | Scope | Org A M V | Area Ч И У | Project Ч И У | Team П У |
|---|---|---|---|---|---|
| `workitems.workitem.delete` | P | + ° – | – + + | – + + | – – |
| `workitems.workitem.purge` | P | + – – | – – – | – – – | – – |

## 4. Error codes

New, module `workitems`, `contracts/errors/workitems.json` (ADR-0005; a Kazakh text, `kkNeedsReview`), `docs/error-codes.md`
regenerated:

| Code | Status | Params | ru | kk |
|---|---|---|---|---|
| `workitems.workitem.deleted` | 409 | — | «Рабочий элемент удалён и доступен только для чтения.» | «Жұмыс элементі жойылған және тек оқуға арналған.» |
| `workitems.workitem.not_deleted` | 409 | — | «Рабочий элемент не удалён.» | «Жұмыс элементі жойылмаған.» |

Where the panel shows them is design § 8: `…deleted` closes the dialog and turns the card read-only; `…not_deleted` closes
it and turns the card live; both are the board's existing notice on a move.

Reused, unchanged: `workitems.workitem.not_found`, `workitems.workitem.state_not_allowed` (Restore),
`workitems.link.item_deleted`, `workitems.link.parent_changed`, `workitems.link.parent_not_found`,
`boards.backlog.reorder_stale`, `organizations.organization.not_found`, `organizations.project.not_found`,
`organizations.access.forbidden`.

## 5. Summary of routes

| Method | Route | Code declared | Story |
|---|---|---|---|
| POST | `/api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/delete` | `workitems.workitem.delete` | US-1, US-5 |
| POST | `/api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/restore` | `workitems.workitem.delete` | US-4, US-5 |
| POST | `/api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/purge` | `workitems.workitem.purge` | US-6 |

Changed routes (§ 3): `GET …/workitems` (US-2), `GET …/workitems/{identifier}` (US-2, US-3), `PATCH …/workitems/{identifier}`
(US-3). The route inventory (`OrganizationAddressedRoutes`) gains three rows, after the item routes: 47 → 50 on the base of this
line, 56 → 59 with feat-028 merged (plan-backend § 14).

## 6. Storage and migration (ADR-0030 rule 1)

One additive, reversible migration, `AddWorkItemDeletionMarks`: three nullable columns on `work_items` —
`deleted_from_state_id` (uuid), `deleted_by` (varchar(255), the Keycloak subject, no key), `deleted_at` (timestamptz);
`ck_work_items_deletion_marks` (all three null, or all three set); a foreign key `deleted_from_state_id` →
`state_definitions` (`Restrict`: a state a deleted item remembers cannot be removed); a partial index on the column.
Nothing is in «Удалена» before this ships, so **no data moves and no table is emptied** (BR-DR-18).

## 7. In-process contracts that change

- `Mustask.Application.Abstractions.WorkItems.IWorkItemPurgeStep` (new): how a later line's data leaves with a purge,
  signature in the plan's § 7.3. This line registers none; it tests the contract with a fake step.
- `IWorkItemRepository` gains the purge's locks and writes (plan § 7.3); `IWorkItemReadModel.WorkItemView` gains
  `Deletion`, `WorkItemHead` gains `StateId`.
- `IProjectBacklogRepository.LockAsync` takes the `ProjectBacklogScope` (plan § 7.5).
- Infrastructure's `WorkItemSets` (`Live`, `Deleted`, `InAnyState`) is internal; every item read composes on one of them.

## 8. Query shape and speed (spec § NFR)

- Delete and Restore each within 1 s on a project of 5,000 items: one lock, one `UPDATE`.
- A purge of an item with 5,000 children within 2 s: one hierarchy lock, one ordered locking statement, one set-based
  `UPDATE`, one `DELETE`.
- The list answers within 2 s for 5,000 items with deleted ones among them: `NOT IN` over the few Removed states, in the
  same two statements as before. The statement counts of the list and of the item read are unchanged.
