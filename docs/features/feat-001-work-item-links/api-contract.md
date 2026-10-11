# API contract — feat-001-work-item-links

Fixed at stage 4a (2026-10-06), before the code. From now on it changes only
with a note in [status.md](status.md) and a Log line; once stage 6 has
started, a change goes to the orchestrator (04-backend.md § 4a.4). The plan
is the `## Plan` section of [status.md](status.md).

The panel uses § 1–§ 5. § 3.1–§ 3.4 are built in backend pass A, § 4 in
backend pass B (D-9). § 7 is what changes in mod-003's and mod-006's
merged contracts, recorded there too.

## 0. Rules shared by every endpoint

### 0.1 Access

- **Authentication, guard, organization context** — exactly mod-003's
  (its api-contract § 0.1): a Bearer token from realm `mustask` or 401
  `http.unauthorized` first; any signed-in user until mod-016 (D-5,
  BR-H-18); mod-002's organization-context filter answers a malformed,
  unknown or deactivated organization with 404
  `organizations.organization.not_found` before anything else is read.
- **Permission codes** (BR-H-18). The parent write (§ 3.2) declares
  `workitems.link.write`, a new organization-scoped code
  (`OrganizationPermissionCatalog`): no token carries it, the realm import
  does not mention it, nothing checks it until mod-016. The form's save
  (§ 3.3) keeps `workitems.workitem.write`; a `parent` in it is a parent
  write, which mod-016 is to check against `workitems.link.write` as well.
  The reads (§ 3.1, § 3.4, § 4) declare `workitems.workitem.read`.

### 0.2 Identifiers

As mod-003 § 0.2: a work item is addressed in a path by its identifier
(`MSK-FEAT-6`); every id in a body or a query is a UUID, an untrusted
string accepted only in the canonical 8-4-4-4-12 form in either letter case
and not all zeros. A body id in any other form names nothing.

### 0.3 Not found, from the outside in

1. organization, project, work item of the path — mod-003 § 0.3, unchanged;
2. the new parent named in the body: `workitems.link.parent_not_found`
   (404, «Родитель не найден в этом проекте.») when `parentId` is malformed,
   names nothing, or names an item of another project or another
   organization. The answer is the same in all four cases: it never says
   whether the item exists elsewhere (BR-H-1).

### 0.4 «Удалена»

An item is **in «Удалена»** when its state's category is `Removed`
(`state.category.code`, mod-001). No route of this cut puts an item there
(mod-003 BR-M3-21; the deletion is mod-005's), but the server already
enforces BR-H-8 on that category (D-8, OQ-1): no link write touches such an
item, the picker never offers it, the form lists no such child, and the
backlog does not nest under it.

### 0.5 Error body

`application/problem+json`, as everywhere (mod-003 § 0.5): `errorCode`,
`detail` (the Russian text of § 5), and `params` only for
`workitems.link.cycle` and, since feat-027 D-9, `workitems.link.depth_exceeded`. Where the panel shows a link refusal is spec
§ Screens and copy: under «Родитель» on the form and in the dialog, as a
notice on «Убрать родителя».

## 1. The link

- **Storage.** A work item has at most one parent: one column of its row
  (`parent_id`). Children are the items whose parent it is. Parent and child
  are always items of one project (BR-H-1).
- **Levels** (BR-H-2). The parent's type is on the child's own level or a
  higher one, by mod-001's level rank (`backlogLevels[].rank`; the type's
  level is `workItemTypes[].backlogLevel`). With the seeded process:

  | Child | Allowed parent types |
  |---|---|
  | «Эпик» | «Эпик» |
  | «Фича» | «Эпик», «Фича» |
  | «Пользовательская история», «Баг» | «Эпик», «Фича», «Пользовательская история», «Баг» |
  | «Задача» | all five |

  The panel derives the helper «Родителем может быть: …» from the same
  process read (the types whose level's rank is the item's own or higher,
  in the process's display order), as it derives mod-003's editable states.
- **No self-link, no cycle; a chain holds at most 64 items.** A cycle is
  possible only inside one level. *Changed by feat-027 D-9: this was «no
  depth limit»; a chain of parents — from a top item down to its deepest
  descendant — now holds at most 64 items, and a link that would make any
  chain longer is refused with 409 `workitems.link.depth_exceeded` (§ 3.2,
  § 3.3, § 5).*
- **Deletion** (BR-H-8). A parent's deletion never removes its links; its
  children keep it as their stored parent. The form shows such a parent with
  its state; the backlog treats the child as unparented (§ 4).

## 2. Shapes

### 2.1 `WorkItemRef` — a parent, a child, a picker option

```json
{
  "id": "0199b0a2-…",
  "identifier": "MSK-FEAT-6",
  "type": { "id": "…", "key": "FEAT", "name": "Фича", "appearance": "feature" },
  "title": "Вход по ссылке",
  "state": { "id": "…", "name": "Новая", "category": { "code": "Proposed", "label": "Новые" } }
}
```

Every field means exactly what it means in mod-003's `WorkItem` (its
api-contract § 1.1).

### 2.2 `WorkItem` — mod-003 § 1.1 gains two fields

| Field | Meaning |
|---|---|
| `parent` | `WorkItemRef` of the stored parent, or `null` for «Нет родителя». A parent in «Удалена» is answered too, with that state (BR-H-8, US-7). |
| `children` | `WorkItemRef[]`: the item's direct children, those in «Удалена» excepted (BR-H-7), by level rank descending, then by `number` ascending. `[]` when there is none. Never paged. |

Every route that answers `WorkItem` answers them: the read (§ 3.1), the
create (mod-003 § 2.2: `null` and `[]` — *changed by feat-027: a create that
names a `parentId` answers that parent as a `WorkItemRef`, not `null`*) and
the save (§ 3.3).
`WorkItemListItem` (mod-003 § 1.2, the project's list) does not change.

### 2.3 `ParentChange` — one parent write

```json
{ "parentId": "0199b0a3-…", "seenParentId": "0199b0a1-…" }
```

| Field | Meaning |
|---|---|
| `parentId` | the new parent's id; `null` or absent clears the parent («Нет родителя», «Убрать родителя») |
| `seenParentId` | the id of the parent the user saw when they chose — the form's loaded `parent.id`, the backlog row's `parent.id` — or `null` / absent when they saw none. A malformed value names nothing and never matches. |

## 3. Routes of the work item

The base is `/api/organizations/{organizationId}/projects/{projectId}/workitems`
(`…` below).

### 3.1 `GET …/workitems/{identifier}` — one item (US-1, US-3)

mod-003 § 2.3 unchanged, answering § 2.2's `WorkItem` with `parent` and
`children`.

### 3.2 `PUT …/workitems/{identifier}/parent` — the parent write (US-1, US-2, US-5, US-7)

Body: `ParentChange` (§ 2.3). A missing body is `{}`: clear, having seen
none.

One write sets a parent, **replaces** one — the old link removed and the new
one added in the same write, never two parents and never none in between —
or clears it (BR-H-3). The checks, in this order; the first that fails
answers, and nothing changes:

1. the path's not-found (§ 0.3, step 1);
2. the new parent's not-found: `workitems.link.parent_not_found` (404);
3. `workitems.link.item_deleted` (409): the item, or the new parent, is in
   «Удалена» — whether the write sets, replaces or clears (BR-H-8);
4. `workitems.link.parent_changed` (409): the stored parent differs from
   `seenParentId` (BR-H-4). A write that saw none also matches a stored
   parent in «Удалена», so a child of a deleted parent can be given a parent
   or none from a view that showed «Нет»;
5. `workitems.link.self_parent` (409): `parentId` is the item's own id;
6. `workitems.link.parent_level_not_allowed` (409): the new parent's level
   is below the item's (§ 1);
7. `workitems.link.cycle` (409): the item is the new parent itself or one of
   its ancestors. `params`: `parent` — the new parent's identifier, `item` —
   the item's identifier, `path` — the chain from the item down to the new
   parent, identifiers joined by « → » («MSK-TASK-10 → MSK-TASK-11 →
   MSK-TASK-12»), all `string`;
8. `workitems.link.depth_exceeded` (409), added by feat-027 D-9: the new
   parent's chain of ancestors, itself included, plus the height of the
   item's own subtree (the item counted, 1 for an item with no children) is
   more than 64. `params`: `limit` — 64, a `number`. Not checked when the
   write clears the parent or names the stored parent again: no chain gets
   longer.

Then the write is accepted. A `parentId` equal to the stored parent changes
nothing, `changedAt` included. Otherwise the stored parent becomes
`parentId` (or none), and `changedAt` moves to the write's time on the item,
on the parent it removes unless that one is in «Удалена» (which stays
exactly as it was), and on the parent it adds (BR-H-5). No other field, no
state and no backlog place changes.

- **200** `{ "parent": WorkItemRef | null }` — the item's parent as stored
  after the write.
- **404** organization, project, work item (§ 0.3); `workitems.link.parent_not_found`.
- **409** the six codes above (the sixth, `depth_exceeded`, by feat-027 D-9).

**Concurrency** (spec § NFR). Parent writes of one project run one after
another: each takes the project's hierarchy lock before it reads anything it
decides on. So of two writes for one child made from the same view, the
later is refused with `parent_changed`; of two writes that would together
close a cycle, the later is refused with `cycle`. A parent write and a form
save of other fields of the same item run one after the other and both take
effect; a parent write and a backlog reorder of the same item both take
effect.

### 3.3 `PATCH …/workitems/{identifier}` — the form's save gains `parent` (US-1, US-2)

mod-003 § 2.4, with one more optional field:

```json
{ "title": "Письмо со ссылкой на вход", "parent": { "parentId": "…", "seenParentId": null } }
```

| Field | Meaning |
|---|---|
| `parent` | `ParentChange` (§ 2.3): the form's «Родитель» when it differs from the loaded value. `"parent": null` or no `parent` leaves the parent unchanged, whoever changed it meanwhile (US-1 «A save of other fields leaves the parent alone»). |

The parent is written exactly as § 3.2 writes it, in the same transaction as
the other fields: all of them or none (BR-H-6, mod-003 BR-M3-25). Choosing
another parent where one is stored replaces it (OQ-2).

**The order of answers** (mod-003 § 0.4, extended):

1. the path's not-found;
2. 400 `workitems.validation_failed`, every field reason at once;
3. the body's not-found: the area (`organizations.area.not_found`), then the
   parent (`workitems.link.parent_not_found`);
4. 409: the state, the assignee (mod-003), then § 3.2's checks 3–8 in their
   order (8 added by feat-027 D-9).

- **200** `WorkItem` (§ 2.2) as stored after the save, `parent` and
  `children` included.
- Every other answer of mod-003 § 2.4 unchanged; plus the 404 and the six
  409s of § 3.2.

### 3.4 `GET …/workitems/{identifier}/parent-candidates?text=ссылк&levelAbove=false` — the «Родитель» picker (US-1, US-2, US-5, US-7)

The candidates for **this** item's parent: items of the same project, never
the item itself, never an item in «Удалена», of a type BR-H-2 allows for
it (§ 1). Its descendants are offered: a cycle is refused by the write,
with its path (US-2).

| Query | Meaning |
|---|---|
| `text` | optional, absent is `""`. Trimmed; empty matches every candidate. Otherwise a candidate matches when its identifier or its «Заголовок» contains the text, ignoring letter case (`feat-8` finds MSK-FEAT-8, `ссылк` «Вход по ссылке»). A text longer than 255 characters matches nothing. |
| `levelAbove` | optional, `true` or `false`, default `false`. `true` narrows the candidates to the types of the level directly above the item's own — the lowest level ranked higher (an «Эпик» for a «Фича»; a «Фича» for a story or a bug; none for an epic): the backlog's dialog «Изменить родителя» (BR-H-14). Any other value fails binding: 400 `http.bad_request`. |

- **200** `{ "items": [ WorkItemRef, … ], "moreMatched": false }` — at most
  20, most recently changed first (`changedAt` descending), then by `number`
  descending; `moreMatched` is `true` when more than 20 matched, which the
  panel reads «Показаны первые 20. Уточните запрос.». The first option
  «Нет родителя» is the panel's, not an item.
- **404** organization, project, work item (§ 0.3).

## 4. The team's backlog with its tree (US-4, US-5, US-7) — backend pass B

`GET /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/backlogs/{level}`
(mod-006 § 3.1): the same items in the same order, the same `team` and
`level`; every element of `items` gains two fields.

```json
{
  "order": 1, "id": "…", "identifier": "MSK-EPIC-1", "number": 1,
  "type": { … }, "title": "Вход и регистрация", "assignee": null, "state": { … },
  "parent": null,
  "children": [
    {
      "id": "…", "identifier": "MSK-FEAT-8", "number": 8,
      "type": { … }, "title": "Вход через SSO", "assignee": null, "state": { … },
      "parent": { "id": "…", "identifier": "MSK-EPIC-1", … },
      "children": [ … ]
    }
  ]
}
```

| Field | Meaning |
|---|---|
| `parent` | `WorkItemRef` (§ 2.1) of the stored parent — whatever its level or team (BR-H-13) — or `null` when there is none **or it is in «Удалена»** (BR-H-8: the cell reads «Нет»). |
| `children` | the row's children on **the same team's backlog of the level directly below**: `epics` → `features` → `stories`; a `stories` row has none in this cut (OQ-5). They are the items of that backlog (mod-006 § 1: same team, its level's types, not in «Удалена») whose stored parent is this row, in **that backlog's own order** (mod-006 § 1, BR-H-11). Each is a nested row: mod-006's `BacklogItem` **without `order`** (a nested row has no «Порядок», BR-H-11), plus `parent` and `children` of its own, recursively. `[]` when there is none, so a row has an expand control exactly when `children` is not empty (BR-H-12). |

So «Эпики» answers up to three tiers, «Фичи» two, «Истории» one. An item
whose parent is on its own level, two levels up, on another team's backlog,
or in «Удалена» nests under no row; it is a top-level row of its own level's
backlog only (BR-H-10). Rows are not paged and nothing is collapsed by the
server: what is expanded is the panel's (BR-H-12).

**Superseded on the backlog read by feat-027** (its api-contract § 1, § 2):
`children` hold the tree at full depth — every level below the row's, the
«Задачи» level included, of the same team and not in «Удалена», by level then
that level's backlog order — and an item whose parent is on its own level and
team stands beneath that parent only, so it is not a top-level row. A parent
two levels up or more nests its child too. `parent` is unchanged. The tree is
at most 64 rows deep, the bound of § 1 (feat-027 D-9).

The board (mod-006 § 3.3) does not change: a card has no `parent` and no
`children` (BR-H-17).

**The mapping** (US-5) is § 3.2 from a top-level row of «Фичи» or «Истории»:
«Изменить родителя…» searches with § 3.4 and `levelAbove=true` and writes
`{ "parentId": <chosen>, "seenParentId": <row's parent.id or null> }`;
«Убрать родителя» writes `{ "parentId": null, "seenParentId": <row's parent.id> }`.
Then the panel reads the backlog again.

## 5. Error codes

New, module `workitems`, `contracts/errors/workitems.json` (Kazakh texts
with `kkNeedsReview`):

| Code | Status | Params | ru |
|---|---|---|---|
| `workitems.link.parent_not_found` | 404 | — | «Родитель не найден в этом проекте.» |
| `workitems.link.parent_level_not_allowed` | 409 | — | «Родителем может быть только рабочий элемент того же или более высокого уровня.» |
| `workitems.link.self_parent` | 409 | — | «Рабочий элемент не может быть родителем самого себя.» |
| `workitems.link.cycle` | 409 | `parent`, `item`, `path` (`string`) | «Нельзя выбрать родителем {parent}: он вложен в {item} ({path}).» |
| `workitems.link.parent_changed` | 409 | — | «Родитель уже изменён другим участником. Данные обновлены — проверьте и повторите.» |
| `workitems.link.item_deleted` | 409 | — | «Удалённый рабочий элемент нельзя связывать.» |
| `workitems.link.depth_exceeded` | 409 | `limit` (`number`) | «Слишком глубокая вложенность: в цепочке родителей может быть не больше {limit} элементов.» — added by feat-027 D-9 |

No panel-defect code is added: a malformed `parentId` is a parent that names
nothing, a malformed `seenParentId` a parent that never matches, and a
`levelAbove` that is not a boolean fails binding with the platform's 400.

Reused, unchanged: mod-003's `workitems.workitem.not_found` and every code of
its save; mod-002's `organizations.organization.not_found`,
`organizations.project.not_found`, `organizations.team.not_found`; mod-006's
`boards.level.not_found` (§ 4).

## 6. Summary of routes

| Method | Route | Code declared | Story | Pass |
|---|---|---|---|---|
| GET | `/api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}` | `workitems.workitem.read` | US-1, US-3, US-7 | A (shape) |
| PUT | `/api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/parent` | `workitems.link.write` | US-1, US-2, US-5, US-7 | A (new) |
| PATCH | `/api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}` | `workitems.workitem.write` (+ `workitems.link.write` for `parent`, mod-016) | US-1, US-2 | A (field) |
| GET | `/api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/parent-candidates` | `workitems.workitem.read` | US-1, US-2, US-5, US-7 | A (new) |
| GET | `/api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/backlogs/{level}` | `workitems.workitem.read` | US-4, US-5, US-7 | B (shape) |

The two new routes are organization-addressed and carry mod-002's filter;
its inventory test lists them. OpenAPI tag `WorkItems`.

## 7. Changes to merged contracts (additive)

- **mod-003** (recorded in its api-contract and Log): § 1.1 `WorkItem` gains
  `parent` and `children` (§ 2.2 here); § 2.4 `PATCH` gains the optional
  `parent` (§ 3.3 here), its order of answers extended. A request without
  `parent` behaves exactly as before.
- **mod-006** (recorded in its api-contract and Log in pass B): § 3.1's
  backlog rows gain `parent` and `children` (§ 4 here). Order, `order`,
  membership, the reorder and the board are unchanged.
