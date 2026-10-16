# API contract — feat-032-board-card-checklists

One additive change to one read: **`GET …/teams/{teamId}/boards/{level}`**
(mod-006 api-contract § 3.3) answers each card with its checklist: the card's
children in the field `checklist` (BR-C-1, BR-C-10). Nothing the board answered before changes, no route is
added, no error code is added. The state change and the creation beneath a card
are existing routes (§ 3). The backlog's read is not touched.

## 0. Rules

Those of mod-006 api-contract § 0 stand unchanged: any signed-in user
(`workitems.workitem.read` declared, nothing checks it until mod-016), the same
not-found answers from the outside in, the same error body. This task adds no
route, so mod-002's inventory test needs no row.

## 1. What a card's `checklist` holds (BR-C-1, BR-C-3)

For a card whose item is `P`, `checklist` holds every work item `C` such that:

1. `C`'s stored parent is `P` (a **direct** child; grandchildren are not in it);
2. `C` is in the same project as `P`;
3. `C`'s state is outside the Removed category («Удалена» of the seeded
   process);
4. `C`'s type has the appearance `task`, `bug` or `testcase` (recognised by the
   token, never by name; a child of another kind — a story under a feature, a
   feature under an epic — is not in it).

And nothing else narrows it:

- **Whatever `C`'s Область.** Unlike the backlog's tree (feat-027 BR-T-4), which
  shows a child only when it is on the team's surfaces, a child of another
  team is in its parent's `checklist` (D-4, OQ-4). A story can read three
  children here and show two beneath it on the backlog, by design.
- **Whatever `C`'s state otherwise**: a «Разрешена» bug and a «Закрыта» test case
  are in it; whether a child is *done* is the panel's reading of
  `state.category.code == "Completed"` (BR-C-3), not a filter.
- **Whatever the card's own type**: the answer does not apply BR-C-9's "which
  card offers a task"; the panel decides what to draw from the card's type.
  A card whose item has no such child answers `[]`, never an absent field.
- A child that is itself a card of the same board (a bug under a story, on the
  «Истории» board) stays a card of its own **and** is in its parent's
  `checklist` (BR-C-1).

Order: by the child's `number`, ascending — creation order, **for every kind**
(BR-C-5; unlike feat-027 BR-T-6, a bug child is not in the bug backlog's order
here). The panel groups the list by `type.appearance`, which keeps this order
inside each group.

The read is the board's read, not another one per card: one more fixed-count
read for all the cards of the answer at once (the child rows in one statement,
their assignees through mod-014's directory in batches of its per-read limit),
whatever the number of cards or children (BR-C-10, NFR "Volumes and speed").

## 2. Shapes

### 2.1 `BoardCard` — `BacklogItem` plus `checklist`

A card in `columns[].items` is mod-006's `BacklogItem` (api-contract § 2.3: `order`,
`id`, `identifier`, `number`, `type`, `title`, `assignee`, `state`, feat-022's
`priority`) **plus one field**:

```json
{
  "order": 2,
  "id": "0199b0a2-…",
  "identifier": "MSK-US-2",
  "number": 2,
  "type": { "id": "…", "key": "US", "name": "Пользовательская история", "appearance": "story" },
  "title": "Повторная отправка письма",
  "assignee": { "id": "…", "displayName": "Анна Смирнова", "email": "anna@example.com" },
  "state": { "id": "…", "name": "Активна", "category": { "code": "InProgress", "label": "В работе" } },
  "priority": 2,
  "checklist": [
    {
      "id": "0199b0a3-…",
      "identifier": "MSK-TASK-3",
      "number": 3,
      "type": { "id": "…", "key": "TASK", "name": "Задача", "appearance": "task" },
      "title": "be",
      "assignee": { "id": "…", "displayName": "Анна Смирнова", "email": "anna@example.com" },
      "state": { "id": "…", "name": "Закрыта", "category": { "code": "Completed", "label": "Завершённые" } }
    },
    {
      "id": "0199b0a4-…",
      "identifier": "MSK-TASK-4",
      "number": 4,
      "type": { "id": "…", "key": "TASK", "name": "Задача", "appearance": "task" },
      "title": "fe",
      "assignee": null,
      "state": { "id": "…", "name": "Новая", "category": { "code": "Proposed", "label": "Новые" } }
    }
  ]
}
```

| Field | Meaning |
|---|---|
| `checklist` | **new.** The card's checklist children, § 1; an array, `[]` for a card with none, never absent or `null`. Only on a **board card**: the backlog's rows (mod-006 § 2.3, feat-027 §§ 2.1–2.2) are unchanged, and their own `children` (the backlog tree) is another field of another read. |

The field is named `checklist`, not `children`, on purpose: feat-027's
api-contract § 2.3 and its test `GetBoard_CardsCarryNoLinksAndNoPlanningValues`
fix that a board card carries no `parent`, `children`, `effort`, `businessValue`
or `valueArea`, and `children` on the backlog means the whole tree of the team's
surfaces, a different thing. The spec's "children" (BR-C-1, BR-C-10) is this list.

### 2.2 `ChecklistChild` — one entry of `checklist`

| Field | Meaning |
|---|---|
| `id` | the child's id |
| `identifier`, `number`, `type`, `title`, `state` | exactly as mod-003's `WorkItem` (its api-contract § 1.1) and as a card's: `type` is `{ id, key, name, appearance }` with `appearance` one of `task`, `bug`, `testcase`; `state` is `{ id, name, category: { code, label } }`, `category.code` never `Removed` |
| `assignee` | as a card's: `null` for «Не назначен»; `{ id, displayName, email }`, with `displayName` and `email` both `null` for «Неизвестный пользователь» (a name the directory does not know) |

A child carries **no** `order`, `priority`, parent, Область or planning value:
the checklist row shows none of them. A child that is a card of this board has
its own, in its column.

## 3. What does not change: the writes the panel reuses

| Need | Route |
|---|---|
| A row's checkbox, tick or untick (US-2, BR-C-7) | mod-003's `PATCH /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}` with `{ "stateId": "<state>" }` where `<state>` is the first state of the child's type in the Completed category (tick) or its initial state (untick) in the process's order, exactly as the card move (mod-006 api-contract § 6). It writes the state and `changedAt` and nothing else; last write wins; a state of another type is 409 `workitems.workitem.state_not_allowed`. The 200 body is the item; the panel reads the board again. The type's states come from the process definition read (`GET /api/organizations/{organizationId}/process`, mod-001), whose type rows carry `appearance` and `states` with `order`, `isInitial` and `category`. |
| «Добавить задачу» (US-3, BR-C-9) | feat-027's `POST /api/organizations/{organizationId}/projects/{projectId}/workitems` with `parentId` the card's item (feat-027 api-contract § 3): one task beneath it, the parent's Область as stored, the type's initial state, `requestId` for the replay. Its refusals (`workitems.link.parent_not_found`, `workitems.link.item_deleted`, field errors) are feat-027's. |

No new write endpoint: `boards.*` gains no permission code, and the two reused
routes declare their own (`workitems.workitem.write`, `workitems.link.write`,
BR-C-7, BR-C-9).

## 4. The `testcase` appearance token (BR-C-11, D-3)

`appearance` gains a sixth value, **`testcase`**, in every place the five were
listed: the process definition's type rows (`GET /api/organizations/{organizationId}/process`), mod-003's `type`,
the board's `level.types[]` and `checklist[].type`. It is the stored spelling too
(`work_item_types.appearance`, `varchar(16)`, a converted string: no migration).
**No type of that appearance exists** in the seeded process: feat-031 adds the
type, a child of a story on the «Задачи» level, and must use this spelling. Until
then `checklist` never holds a `testcase` entry outside a test's fixture process.

The fields a test case has on its form, and that it can be created at all, are
feat-031's: this task makes the token readable and nothing more.

## 5. Error codes

None added. The read answers the board's 401 and 404s (mod-006 § 3.3, § 0.3).

## 6. Summary

| Method | Route | Change |
|---|---|---|
| GET | `/api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/boards/{level}` | each card in `columns[].items` gains `checklist` (§ 2) |

## 7. Changes to merged contracts

Additive on mod-006 api-contract § 2.3/§ 3.3 (the board card gains `checklist`)
and mod-001's list of appearance tokens (a sixth, `testcase`). Owed at gate-3,
a superseded-in-part note on both (status.md, spec "Decomposition"). Done at
code review round 1 (2026-10-12): mod-006 api-contract § 2.3 and § 3.3, spec
BR-M4-14 and the card row; mod-001 api-contract, spec, design (two places);
mod-015 spec BR-4, US-1, US-10, design (two places); mod-003 api-contract § 1.1.
ADR-0024 § 3 is not edited: it lets the set grow by a change to `app/theme`
(adr/README rule 3). feat-031 owns adding `testcase` to mod-001's token set and
the `WorkItemField` support (spec BR-C-11).
