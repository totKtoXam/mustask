# API contract — feat-027-backlog-tree-editing

Fixed at stage 4a (2026-10-09), before the code. From now on it changes only
with a note in [status.md](status.md) and a Log line; once stage 6 has
started, a change goes to the orchestrator (04-backend.md § 4a.4). The plan
is the `## Plan` section of [status.md](status.md).

This task adds no route. It changes what mod-006's backlog read answers
(§ 1, § 2), adds one optional field to mod-003's create (§ 3) and — since the
code review's D-9 — one error code, `workitems.link.depth_exceeded` (§ 5): a
chain of parents holds at most 64 items, which every write of a link
refuses to exceed (§ 1, § 3, § 4). The drop and the menus reuse feat-001's
parent write (§ 4). § 7 is what changes in the merged contracts, recorded
there too.

*Changed after stage 4a, 2026-10-09 (review round 1a, D-9; status.md Log):
the depth bound in § 1–§ 5, § 6 and § 7; § 3 and § 4 worded for the inline
add-child row of D-7 instead of the dialog.*

## 0. Rules shared by every endpoint

- **Access, identifiers, not-found, error body** — exactly mod-006's (its
  api-contract § 0) for the read, mod-003's (§ 0) and feat-001's (§ 0) for
  the create: a Bearer token from realm `mustask` or 401 `http.unauthorized`;
  any signed-in user until mod-016 (D-3); mod-002's organization-context
  filter first.
- **Permission codes** (BR-T-17), none new. The read declares
  `workitems.workitem.read`, the create `workitems.workitem.write`; a create
  that carries `parentId` is a parent write, which mod-016 is to check
  against `workitems.link.write` as well — as feat-001 § 0.1 says of the
  save's `parent`. A drop is feat-001's parent write and declares
  `workitems.link.write`. Nothing checks any of them until mod-016.
- **Types and levels.** A type is recognised by `type.appearance`, never by
  its name (feat-022 § 0); a level by its rank in the organization's process
  (mod-001 `backlogLevels[].rank`, a higher rank is a higher level), never by
  its name.

## 1. What a backlog page holds (BR-T-4, BR-T-5, BR-T-6)

For team T of project P and the page's level L (`epics`, `features`,
`stories`, mod-006 § 0.2), the read gathers **the tree set**: the work items
that, at the moment of the read, satisfy mod-006 § 1's conditions for L **or
for any level ranked below L** — the «Задачи» level included, which still has
no backlog route of its own:

1. they belong to P, T being an active team of P;
2. their type is of L or of a level ranked below it;
3. their area resolves to T (mod-002 `resolvedOwner`);
4. their state is outside the Removed category («Удалена»).

From the tree set:

- **Nesting.** An item stands beneath its stored parent exactly when that
  parent is in the tree set and its level ranks the same as the item's or
  higher (BR-H-2). So beneath a row stand its direct children that are of
  team T and live, of any level below it or of its own, each with its own
  children, recursively, to the depth the hierarchy allows: **a chain of
  parents holds at most 64 items** (D-9, below). A child of another team or
  in «Удалена» stands beneath no row of T.
- **Top-level rows** are the items of L that stand beneath no row: no parent,
  or a parent of a higher level, of another team or in «Удалена». An item of
  L whose parent is of L, of T and live — a feature under a feature, a bug
  under a story — stands beneath that parent only (OQ-5), not also at the top.
- **An item of a level below L that stands beneath no row is not on the
  page**: it is a top-level row of its own level's backlog only.
- An item shows at most once on a page. (A stored cycle, which no write makes,
  would leave its items beneath one another and none at the top: they show
  nowhere.)
- **The order beneath a row** (BR-T-6): by level rank, highest first; within
  a level, that level's backlog order on team T (mod-006 § 1: placed items by
  their places, then unplaced ones by `number`). The «Задачи» level has no
  places before feat-023, so tasks stand by `number`.
- **The order of the top-level rows** is L's backlog order (mod-006 § 1)
  with the nested items left out; `order` numbers them 1..n, dense (BR-T-2).

**The depth bound** (D-9; review B-1, B-2). A chain of parents — a top item
and each step from a parent to its child, down to the deepest descendant —
holds **at most 64 items**, whatever their levels. So the tree of this
section is at most 64 rows deep (a top-level row is the first), on any page.
The bound is kept where links are written, not by the read: feat-001's
parent write, the form's save with a `parent` and the create with a
`parentId` (§ 3) each refuse a link that would make any chain longer — the
new parent's chain of ancestors, itself included, plus the height of the
moved item's own subtree, the item counted — with 409
`workitems.link.depth_exceeded` (§ 5), checked beside the cycle check under
the project's hierarchy lock. The read never refuses, cuts or pages a tree
for its depth. A chain stored before the bound that is longer than 64 is
not expected (no stand holds one): the read still maps it without recursion,
but the answer is written with a JSON depth derived from the bound, so the
page that shows such a chain may fail to be written until its links are
shortened.

## 2. `GET …/teams/{teamId}/backlogs/{level}` — the answer

The route, `team`, `level`, the 404s and the order of answers are mod-006
§ 3.1's, unchanged. `items` holds the top-level rows of § 1 in their order,
each with the whole tree beneath it, at most 64 rows deep (§ 1): **the whole
tree is in the one answer**, nothing is collapsed or paged by the server, and
no expand ever needs a request (BR-T-17). What is shown expanded first — as
deep as fits the panel's 2 000 drawn rows (D-6 OQ-2 (d)) — is the panel's,
counted from this answer.

### 2.1 `BacklogRow` — a top-level row

```json
{
  "order": 1,
  "id": "0199b0a2-…",
  "identifier": "MSK-EPIC-1",
  "number": 1,
  "type": { "id": "…", "key": "EPIC", "name": "Эпик", "appearance": "epic" },
  "title": "Вход и регистрация",
  "assignee": null,
  "state": { "id": "…", "name": "Новая", "category": { "code": "Proposed", "label": "Новые" } },
  "priority": 1,
  "effort": 20,
  "businessValue": 300,
  "valueArea": "Business",
  "parent": null,
  "children": [
    {
      "id": "…", "identifier": "MSK-FEAT-8", "number": 8,
      "type": { "…": "…" }, "title": "Вход через SSO", "assignee": null, "state": { "…": "…" },
      "priority": 2, "effort": 2.5, "businessValue": null, "valueArea": "Business",
      "parent": { "id": "…", "identifier": "MSK-EPIC-1", "…": "…" },
      "children": [ "…" ]
    }
  ]
}
```

| Field | Meaning |
|---|---|
| `order` | «Порядок»: the row's 1-based position among the **top-level rows** (§ 1), dense. A nested row has no `order` member at all (BR-T-2). |
| `id`, `identifier`, `number`, `type`, `title`, `assignee`, `state` | mod-006 § 2.3, unchanged |
| `priority` | feat-022 § 5, unchanged: «Приоритет», an integer 1..4 |
| `effort` | **new.** «Трудоёмкость» as feat-022 § 1 and § 2 answer it on the item: a number in its shortest form (`2.5`, `20`), or `null` — empty, or a type without the field (only an epic and a feature have it) |
| `businessValue` | **new.** «Бизнес-ценность»: an integer, or `null` — empty, or a type without it (only an epic and a feature have it) |
| `valueArea` | **new.** «Вид ценности»: the list key `Business` or `Architectural` (the panel's labels «Бизнес», «Архитектура»), never `null` on an epic, a feature or a story; `null` on a task and a bug, which do not have it |
| `parent` | feat-001 § 4, unchanged: the stored parent as a `WorkItemRef`, whatever its level or team, or `null` when there is none or it is in «Удалена». On a nested row it is always the row it stands beneath. |
| `children` | `BacklogChild[]` (§ 2.2): the row's children by § 1, in § 1's order; `[]` when none, so a row has a chevron exactly when `children` is not empty |

### 2.2 `BacklogChild` — a nested row

Every field of § 2.1 except `order`, with the same meanings, and `children`
of its own, recursively.

### 2.3 What does not change

- The board (mod-006 § 3.3): a card keeps mod-006 § 2.3's fields and
  feat-022's `priority`; it gains none of § 2.1's new fields, no `parent` and
  no `children`; its `order` stays the item's position in the whole backlog.
- The reorder (mod-006 § 3.2) is unchanged: it orders the whole backlog of
  its level, nested items of that level included. The panel names only
  top-level rows in it (BR-T-13); since § 1 keeps the top-level rows in the
  backlog's order, «after a top-level row» lands where the page shows it.

**Speed** (spec § NFR): with 5 000 items on each of a team's four levels,
each one under an item of the level above, the «Эпики» read (20 000 rows in
the tree) and the «Истории» read are each served within 2 s. The read is a
fixed number of statements whatever the depth: the area tree, the tree set in
one statement, the assignees, and the parents outside the tree set — never
one per level or per row.

## 3. `POST …/projects/{projectId}/workitems` — create with a parent (BR-T-10, OQ-4)

mod-003 § 2.2 (with mod-006's `teamId`), with one more optional field:

```json
{ "requestId": "0199b0a3-…", "typeId": "…", "title": "Повторная отправка письма", "parentId": "0199b0a1-…" }
```

| Field | Meaning |
|---|---|
| `parentId` | optional. The id of the item the new one is created beneath — the row whose add-child action the user used: the row's «+» and its inline entry row in the tree, where Enter creates (D-7 (2), (3); the dialog of D-6 OQ-3 (a) is superseded). Absent or `null`: no parent, exactly as before. |

**Why `parentId`, flat.** It is the very member of feat-001 § 2.3's
`ParentChange`, the body of `PUT …/parent` — same name, same meaning, same
reading of a malformed value (a parent that names nothing) — so the panel
writes a parent the same way whether it creates or re-parents. Its partner
`seenParentId` is not there: it guards a **stored** parent against a stale
view (BR-H-4), and a new item has none to be stale against. The form's save
nests the pair under `parent` only because its body is a partial update in
which `null` means "unchanged" (feat-022 § 3.2, "Why a wrapper"); the create
has no "unchanged", and its other references are flat (`typeId`, `teamId`),
so a flat `parentId` keeps both conventions.

**With `parentId`**, in one transaction — all or none:

- the item takes **the parent's area as stored at that moment**, instead of
  the root or `teamId`'s default area; so it comes onto the parent's team's
  backlog. With `teamId` sent too, the team is still checked (404 below), but
  the area is the parent's;
- it is created beneath the parent: its `parent` is the parent, its
  `changedAt` its creation time; the parent's `changedAt` moves to the same
  time (BR-H-5) and nothing else of the parent changes;
- everything else is mod-003's and feat-022's: the type's initial state, no
  assignee, an empty description, feat-022's defaults, the next number. The
  item is unplaced (mod-006 § 1), so it is the last of its parent's children
  of its level and the last row of its own backlog.

- **201** `WorkItem` (mod-003 § 1.1) with `parent` the parent's `WorkItemRef`.
- **200** `WorkItem` — a replay (below).

**The order of answers** — mod-003 § 2.2's, the parent's checks last:

1. the path's not-found: organization, project;
2. 400 `workitems.validation_failed` — `requestId`, `title`, unchanged
   (`parentId` adds no field reason);
3. 404 `workitems.workitem.type_not_found`;
4. **the replay**: the project already has the item this `requestId` created
   → 200 with that item as it is now, whatever its parent or the named one
   has become since. Nothing is checked, linked or moved again;
5. 404 `organizations.team.not_found` — `teamId` sent and not an active team
   of the project (mod-006 § 7);
6. 404 `workitems.link.parent_not_found` — `parentId` malformed, naming
   nothing, or an item of another project or organization: one answer for
   all four (BR-H-1);
7. 409 `workitems.link.item_deleted` — the parent is in «Удалена» (BR-H-8);
8. 409 `workitems.link.parent_level_not_allowed` — the parent's level ranks
   below the new item's (BR-H-2; by the seeded process a feature under a
   story is refused, a task or a bug under a story, a bug under a bug are
   allowed);
9. 409 `workitems.link.depth_exceeded` — the parent's chain of ancestors,
   itself included, already holds 64 items, so the new item would be the
   65th (D-9, § 1). `params.limit` is 64 (number).

Then the item is created. A refusal creates nothing and consumes no number.
The parent write's other checks cannot fail for an item that does not exist
yet: it is not in «Удалена», has no stored parent to have changed
(`parent_changed`), is not its own parent (`self_parent`) and has no
descendants (`cycle`) — its own subtree is itself alone, so the depth check
is the parent's chain plus one.

**Idempotency** (mod-003 BR-M3-26): one `requestId`, one item, created and
linked once. The panel's inline entry row (D-7 (3)) keeps the `requestId` of
its submission until the create is answered or the row is cancelled, so a
double Enter or a resend after a lost answer gets 200 and the one item with
its parent; the body of a replay is not compared with the first, so a replay
naming another parent still answers the first item.

**Concurrency** (spec § NFR): the create takes the parent write's ordering
(feat-001 § 3.2) after its own — the number's lock, the team's when `teamId`
is sent, then **the project's hierarchy lock**, then the parent locked like a
parent write's link end, and read — with its chain of ancestors, for the
depth check — under that lock. So a parent write, a save
or a deletion of the parent that committed first is seen — an area moved
meanwhile is the area taken, «Удалена» is `item_deleted`, a chain deepened
meanwhile is `depth_exceeded` — and one that comes later waits for the
creation. The parent's area needs no lock of its own: an
area that holds an item cannot be deleted (mod-003 § 6), and the parent's
lock keeps the parent in it until the commit.

## 4. Writes the panel reuses

| Need | Route |
|---|---|
| A drop onto a row (US-5, BR-T-11) | feat-001 § 3.2 `PUT …/workitems/{identifier}/parent`: `{ "parentId": "<the target row's id>", "seenParentId": "<the dragged row's parent.id, or null>" }`; then the backlog read again. A descendant the page does not show (on another team) is refused by the server with `workitems.link.cycle` and its `path`; a drop that would make a chain longer than 64 items — the target's chain of ancestors plus the dragged row's own subtree as stored: every link counts whatever its state, so items on another team, items in «Удалена» and anything stored beneath them count although the page does not show them — with `workitems.link.depth_exceeded` (§ 1, § 5). |
| «Изменить родителя…» on any row (US-6, BR-T-14) | feat-001 § 3.4 without `levelAbove` (or `false`) — every type BR-H-2 allows — then § 3.2 (the same refusals, `depth_exceeded` included) |
| «Убрать родителя» on any row | feat-001 § 3.2 with `"parentId": null` and the row's `parent.id` seen (never refused for depth: no chain gets longer) |
| A drop between top-level blocks (BR-T-13) | mod-006 § 3.2, unchanged, naming top-level rows only |
| The row's «+» and its inline entry row (US-4, D-7 (2), (3)) | § 3, `parentId` the row's id, no `teamId` |
| «Новый рабочий элемент» (BR-T-16) | mod-003 § 2.2 with `teamId`, no `parentId`, unchanged |

## 5. Error codes

One new, module `workitems`, `contracts/errors/workitems.json` (Kazakh text
with `kkNeedsReview`), decided by D-9:

| Code | Status | Params | ru |
|---|---|---|---|
| `workitems.link.depth_exceeded` | 409 | `limit` (`number`, 64) | «Слишком глубокая вложенность: в цепочке родителей может быть не больше {limit} элементов.» — with `limit` filled, «…не больше 64 элементов.» |

It is answered by the three writes that make a link — feat-001's parent write
(`PUT …/parent`, § 3.2 there), the form's save with a `parent` (§ 3.3 there)
and the create with a `parentId` (§ 3 here) — after the cycle check and the
level check of each (the create has no cycle check; it comes after the
parent's level), under the project's hierarchy lock. The limit is a
parameter, not text: the one constant of the server and the text cannot
drift apart.

The create with a parent also answers feat-001's
`workitems.link.parent_not_found` (404), `workitems.link.item_deleted` (409)
and `workitems.link.parent_level_not_allowed` (409), beside mod-003's create
codes and mod-002's `organizations.team.not_found`; their texts are
`contracts/errors/workitems.json`'s, unchanged. The drop answers every code
of feat-001 § 3.2, `depth_exceeded` now among them.

## 6. Summary of routes

| Method | Route | Code declared | Story | Change |
|---|---|---|---|---|
| GET | `/api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/backlogs/{level}` | `workitems.workitem.read` | US-1, US-2, US-3 | the tree at full depth, three values per row, `order` over top-level rows |
| POST | `/api/organizations/{organizationId}/projects/{projectId}/workitems` | `workitems.workitem.write` (+ `workitems.link.write` with `parentId`, mod-016) | US-4 | optional `parentId`; 409 added to its answers (`item_deleted`, `parent_level_not_allowed`, `depth_exceeded`) |
| PUT | `/api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/parent` | `workitems.link.write` | US-5, US-6 | 409 `depth_exceeded` added (D-9) |
| PATCH | `/api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}` | `workitems.workitem.write` (+ `workitems.link.write` for `parent`, mod-016) | US-5, US-6 | with a `parent`: 409 `depth_exceeded` added (D-9) |

## 7. Changes to merged contracts

- **mod-006** (recorded in its api-contract and Log): § 3.1's rows nest the
  tree at full depth by § 1 — every level below the page's, the «Задачи»
  level included, and same-level children beneath their parent only —
  superseding feat-001 § 4's "the level directly below"; `order` numbers the
  top-level rows only, dense (unchanged for a backlog with no same-level
  link); every row, nested ones included, gains `effort`, `businessValue`
  and `valueArea`. The board, the reorder and membership are unchanged. The
  tree is at most 64 rows deep (§ 1, D-9). § 7's create from a team's
  surface — the item "takes that team's default area" — yields to the
  parent's area when the same request names a `parentId` (§ 3).
- **mod-003** (recorded in its api-contract and Log): § 2.2's create gains
  the optional `parentId` (§ 3 here) and its 409s — `item_deleted`,
  `parent_level_not_allowed` and, by D-9, `depth_exceeded`; its order of
  answers puts the parent's checks after the team's; with `parentId` the
  item's area is the parent's, over `teamId`'s default, and the create
  answers that parent (not `null`). § 2.4's save with a `parent` gains
  `depth_exceeded` among its 409s. A request without `parentId` behaves
  exactly as before.
- **feat-001** (recorded in its api-contract and Log): § 4's `children` and
  its "nests under no row" paragraph are superseded on the backlog read by
  § 1 here; the parent write (§ 3.2) and the form's save (§ 3.3) gain the
  check and the 409 `depth_exceeded` after the cycle check, and § 1's "no
  depth limit" is superseded by the bound of 64 items per chain (D-9); the
  picker is unchanged.
