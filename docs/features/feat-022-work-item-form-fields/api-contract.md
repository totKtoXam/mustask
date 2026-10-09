# API contract — feat-022-work-item-form-fields

Fixed at stage 4a (2026-10-09), before the code. From now on it changes only
with a note in [status.md](status.md) and a Log line; once stage 6 has
started, a change goes to the orchestrator (04-backend.md § 4a.4). The plan
is the `## Plan` section of [status.md](status.md).

This task adds no route. It adds fields to mod-003's `WorkItem` (read,
create, save) and `priority` to mod-006's backlog row and board card. § 7 is
what changes in mod-003's and mod-006's merged contracts, recorded there too.

## 0. Rules shared by every endpoint

- **Access, identifiers, not-found, error body** — exactly mod-003's (its
  api-contract § 0.1–§ 0.3, § 0.5) and feat-001's: a Bearer token from realm
  `mustask` or 401 `http.unauthorized`; any signed-in user until mod-016
  (D-3, mod-003 BR-M3-27); mod-002's organization-context filter first. No
  new permission code: the fields are read under `workitems.workitem.read`
  and saved under `workitems.workitem.write` (spec § Roles and access).
- **How a type is recognised.** By its kind, the appearance token of the
  organization's process (`type.appearance`: `epic`, `feature`, `story`,
  `task`, `bug`; mod-001), which the process holds fixed and unique per
  type. Never by its Russian name (`type.name`) and never by the key of the
  identifier (`type.key`). The panel picks a type's sections and fields from
  § 1 by `type.appearance` too.

## 1. The fields per type

Every field is new. «Story» is «Пользовательская история». ✓ — the type has
the field; blank — it has not.

| JSON name | Поле | epic | feature | story | task | bug | JSON type | Allowed values | Default at creation |
|---|---|:-:|:-:|:-:|:-:|:-:|---|---|---|
| `priority` | «Приоритет» | ✓ | ✓ | ✓ | ✓ | ✓ | integer | `1`, `2`, `3`, `4` (1 the highest) | `2` |
| `risk` | «Риск» | ✓ | ✓ | ✓ | | | integer or `null` | `1` «1 — Высокий», `2` «2 — Средний», `3` «3 — Низкий» | `null` |
| `storyPoints` | «Оценка в пунктах» | | | ✓ | | ✓ | number or `null` | 0..9 999, at most 2 digits after the point | `null` |
| `effort` | «Трудоёмкость» | ✓ | ✓ | | | | number or `null` | 0..9 999, at most 2 digits | `null` |
| `businessValue` | «Бизнес-ценность» | ✓ | ✓ | | | | integer or `null` | 0..9 999 | `null` |
| `timeCriticality` | «Критичность по времени» | ✓ | ✓ | | | | number or `null` | 0..9 999, at most 2 digits | `null` |
| `startDate` | «Дата начала» | ✓ | ✓ | | | | string `YYYY-MM-DD` or `null` | `2000-01-01`..`2099-12-31` | `null` |
| `targetDate` | «Целевая дата» | ✓ | ✓ | | | | string `YYYY-MM-DD` or `null` | as `startDate`, and not earlier than it when both are set | `null` |
| `severity` | «Серьёзность» | | | | | ✓ | integer | `1` «1 — Критическая», `2` «2 — Высокая», `3` «3 — Средняя», `4` «4 — Низкая» | `3` |
| `activity` | «Вид работ» | | | | ✓ | ✓ | string or `null` | `Deployment` «Развёртывание», `Design` «Проектирование», `Development` «Разработка», `Documentation` «Документирование», `Requirements` «Требования», `Testing` «Тестирование» | `null` |
| `originalEstimate` | «Исходная оценка», hours | | | | ✓ | ✓ | number or `null` | 0..9 999, at most 2 digits | `null` |
| `remainingWork` | «Оставшаяся работа», hours | | | | ✓ | ✓ | number or `null` | 0..9 999, at most 2 digits | `null` |
| `completedWork` | «Выполненная работа», hours | | | | ✓ | ✓ | number or `null` | 0..9 999, at most 2 digits | `null` |
| `valueArea` | «Вид ценности» | ✓ | ✓ | ✓ | | | string | `Business` «Бизнес», `Architectural` «Архитектура» | `Business` |
| `acceptanceCriteria` | «Критерии приёмки» | | | ✓ | | | string | plain text, 0..100 000 UTF-16 code units | `""` |
| `reproSteps` | «Шаги воспроизведения» | | | | | ✓ | string | plain text, 0..100 000 UTF-16 code units | `""` |
| `systemInfo` | «Сведения о системе» | | | | | ✓ | string | plain text, 0..100 000 UTF-16 code units | `""` |

So `epic` and `feature` have 8 fields, `story` 5, `task` 5, `bug` 9 (spec
§ The field table, D-6). The list keys are case-sensitive and spelled
exactly as above; the Russian labels are the panel's.

**Always set** (BR-M3-31): `priority` on every type, `severity` on a bug,
`valueArea` on an epic, a feature and a story. They are never `null` on a
type that has them; a save that empties one stores its default (§ 3.2).

**Numbers** (BR-M3-32, BR-M3-33): JSON numbers. Empty is `null`, never `0`;
`0` is a value of its own. Responses write a number in its shortest form,
with no trailing zeros (`2.5`, not `2.50`; `8`, not `8.00`); the panel shows
it with «,». Nothing is rounded, clamped, converted or summed. Hours are the
unit of the three estimates; `storyPoints` is points; `effort`,
`businessValue` and `timeCriticality` have no unit.

**Dates** (BR-M3-35, OQ-2 (a)): a calendar date with no time and no time
zone, ISO 8601 `YYYY-MM-DD`, in requests and responses alike.

**Texts** (BR-M3-36): stored and answered exactly as sent, line breaks and
markup characters kept, as mod-003's `description`. Empty is `""`, never
`null`, on a type that has the field.

## 2. The read: `WorkItem` gains 17 fields

mod-003 § 1.1's `WorkItem` (with feat-001's `parent` and `children`) carries
**all 17 fields of § 1 on every type**, in every route that answers it: the
read (mod-003 § 2.3), the create (§ 2.2) and the save (§ 2.4).

```json
{
  "id": "…", "identifier": "MSK-BUG-4", "type": { "id": "…", "key": "BUG", "name": "Баг", "appearance": "bug" },
  "title": "Падение при входе", "description": "", "…": "mod-003's and feat-001's fields",
  "priority": 2,
  "risk": null,
  "storyPoints": 1.5,
  "effort": null,
  "businessValue": null,
  "timeCriticality": null,
  "startDate": null,
  "targetDate": null,
  "severity": 3,
  "activity": "Development",
  "originalEstimate": 8,
  "remainingWork": 5.5,
  "completedWork": null,
  "valueArea": null,
  "acceptanceCriteria": null,
  "reproSteps": "1. Открыть…",
  "systemInfo": ""
}
```

| Value | Meaning |
|---|---|
| a field the type does not have (§ 1, blank) | always `null` |
| `priority`; `severity` on a bug; `valueArea` on an epic, feature, story | always a value |
| a text field the type has | a string, `""` when empty |
| any other field the type has | its value, or `null` when empty («Не указан», «Не оценено», «Не задано») |

`WorkItemListItem` (mod-003 § 1.2, the project's list) does not change
(BR-M3-40): no new column.

## 3. The writes

### 3.1 `POST …/workitems` — create (mod-003 § 2.2)

The body does not change: the dialog asks only «Тип» and «Заголовок»
(BR-M3-40). The new item takes § 1's defaults — `priority` 2, on a bug
`severity` 3, on an epic, a feature or a story `valueArea` `Business`, the
type's text fields `""`, everything else `null` (BR-M3-37) — and the answer
(201, or 200 on a replay) is § 2's `WorkItem`. A member of § 1's names in a
create body is ignored, as any member mod-003's create does not know.

### 3.2 `PATCH …/workitems/{identifier}` — the form's save (mod-003 § 2.4)

mod-003 § 2.4 with feat-001's `parent`, plus § 1's 17 optional fields.
Exactly the fields sent change, all of them or none (BR-M3-25, BR-M3-38).

```json
{
  "storyPoints": { "value": 5 },
  "risk": { "value": null },
  "startDate": { "value": "2026-11-01" },
  "priority": { "value": 1 },
  "acceptanceCriteria": "Вход без пароля"
}
```

**How a field is sent, emptied or left alone.**

| Field | Sets | Empties | Leaves unchanged |
|---|---|---|---|
| `acceptanceCriteria`, `reproSteps`, `systemInfo` | `"<text>"` | `""` | absent, or `null` |
| every other field of § 1 | `{ "value": <value> }` | `{ "value": null }`, or `{}` | absent, or `null` |

Emptying `priority` stores `2`, emptying a bug's `severity` stores `3`,
emptying `valueArea` stores `Business` (BR-M3-31). Emptying an optional
field is a change (BR-M3-38).

**Why a wrapper.** mod-003's save has one rule for every member of its body:
absent or `null` is "unchanged" (§ 2.4). Clearing is always explicit — an
object with a `null` inside, as `"assignee": { "userId": null }` and
feat-001's `"parent": { "parentId": null }` already do — and a text is
emptied with `""`, as `description` is. The new fields keep that one rule
instead of adding a second one in which `"risk": null` would clear while
`"assignee": null` in the same body leaves the assignee alone. So a client
that writes an unchanged field as `null`, or a defect that does, never
wipes a stored value; and the server needs no special binding to tell an
absent member from a `null` one.

A value equal to the stored one changes nothing — numbers compare as
numbers (`2.50` against a stored `2.5` is no change), and re-sending the
current value, or emptying an already empty field, changes nothing.
`changedAt` moves only when at least one value changes (BR-M3-28). No field
changes another, and a state change changes none of them (BR-M3-34).

**Checks** — every field reason at once, under 400
`workitems.validation_failed` (mod-003 § 0.5), `errors` keyed by § 1's JSON
names, one reason per field, the first that applies:

1. `workitems.workitem.field_not_on_type` — the field is present and not
   `null` (a wrapper, `{ "value": null }` and `{}` included; a text, `""`
   included) on an item whose type does not have it (BR-M3-30). Nothing else
   is checked on that field.
2. The value (BR-M3-31, BR-M3-32, BR-M3-35, BR-M3-36):

   | Field | Reason |
   |---|---|
   | `priority`, `risk`, `severity`, `activity`, `valueArea` | `workitems.workitem.value_not_in_list` — not one of § 1's keys |
   | `storyPoints`, `effort`, `timeCriticality`, `originalEstimate`, `remainingWork`, `completedWork` | `workitems.workitem.number_out_of_range` — below 0 or above 9 999; then `workitems.workitem.number_too_precise` — more than two digits after the point |
   | `businessValue` | `workitems.workitem.number_out_of_range`; then `workitems.workitem.integer_required` — a fraction |
   | `startDate`, `targetDate` | `workitems.workitem.date_out_of_range` — before 2000-01-01 or after 2099-12-31 |
   | `acceptanceCriteria`, `reproSteps`, `systemInfo` | `workitems.workitem.text_too_long` — more than 100 000 UTF-16 code units |

3. `workitems.workitem.target_before_start`, on `targetDate` — after the
   save both dates would be set and `targetDate` would be earlier than
   `startDate`, the stored values standing for the date not sent (BR-M3-35,
   OQ-3 (a)). Reported on `targetDate` also when the save sent only
   `startDate`; not checked when either date has a reason of its own.

A value of the wrong JSON type fails binding before any check, with the
platform's 400 `http.bad_request` (as mod-003's `page`): a string that is
not a number where a number belongs (a numeric string such as `"5"` is read
as the number — the platform's default), a fraction for `priority`, `risk`
or `severity`, a date not
in `YYYY-MM-DD` form or not a calendar date, a wrapper that is not an
object, a text that is not a string. The panel never sends one.

**The order of answers** (mod-003 § 0.4 with feat-001 § 3.3): the path's
not-found; this 400, mod-003's and § 3.2's field reasons together; the
area's, then the parent's 404; the 409s. Every check runs on the item as
stored under the save's lock (mod-003 § 2.4), so two saves of one item run
one after the other and the second checks its dates against what the first
stored.

- **200** `WorkItem` (§ 2) as stored after the save.
- **400** `workitems.validation_failed` — mod-003's reasons, plus `errors`
  of § 1's names with the reasons above.
- Every other answer of mod-003 § 2.4 and feat-001 § 3.3, unchanged.

## 4. Existing items (BR-M3-37)

The migration `AddWorkItemFormFields` gives every item that exists when it
runs the values a new item of its type takes (§ 3.1): `priority` 2; on a bug
`severity` 3, `reproSteps` and `systemInfo` `""`; on an epic, a feature and
a story `valueArea` `Business`; on a story `acceptanceCriteria` `""`; every
other field `null`. The type is recognised by its appearance token (§ 0).
Nothing else of the item changes: not `changedAt`, so not the project's list
order (mod-003 BR-M3-29), and not its place on any backlog.

## 5. The backlog and the board gain `priority` (US-6, OQ-1 (a))

mod-006 § 2.3's `BacklogItem` gains one field: `priority`, the item's
«Приоритет» (§ 1, an integer 1..4).

```json
{
  "order": 1, "id": "…", "identifier": "MSK-US-12", "number": 12,
  "type": { … }, "title": "Вход по ссылке из письма", "assignee": null, "state": { … },
  "priority": 1,
  "parent": null, "children": [ … ]
}
```

- `GET …/teams/{teamId}/backlogs/{level}` (mod-006 § 3.1): every top-level
  row and every nested row of feat-001 § 4 carries `priority`.
- `GET …/teams/{teamId}/boards/{level}` (mod-006 § 3.3): every card carries
  `priority`.

The order, `order`, membership, the reorder and every other field are
unchanged; a reorder or a card move changes no item's `priority` (US-6).

## 6. Error codes

New, module `workitems`, `contracts/errors/workitems.json`, all field
reasons under 400 `workitems.validation_failed`, with no parameters; Kazakh
texts with `kkNeedsReview`:

| Code | Status | Params | ru | Shown at (spec § Error codes) |
|---|---|---|---|---|
| `workitems.workitem.number_out_of_range` | 400 (field reason) | — | «Допустимо значение от 0 до 9 999.» | the number field |
| `workitems.workitem.number_too_precise` | 400 (field reason) | — | «Не более двух знаков после запятой.» | the number field |
| `workitems.workitem.integer_required` | 400 (field reason) | — | «Введите целое число.» | «Бизнес-ценность» |
| `workitems.workitem.value_not_in_list` | 400 (field reason) | — | «Выберите значение из списка.» | the list field |
| `workitems.workitem.date_out_of_range` | 400 (field reason) | — | «Укажите дату с 2000 по 2099 год.» | the date field |
| `workitems.workitem.target_before_start` | 400 (field reason) | — | «Целевая дата не может быть раньше даты начала.» | «Целевая дата» |
| `workitems.workitem.text_too_long` | 400 (field reason) | — | «Не более 100 000 символов.» | «Критерии приёмки», «Шаги воспроизведения», «Сведения о системе» |
| `workitems.workitem.field_not_on_type` | 400 (field reason) | — | «Это поле недоступно для рабочего элемента этого типа.» | the error `Alert` above the band |

Reused, unchanged: every code of mod-003's save and feat-001's parent write,
and the platform's `http.bad_request`.

## 7. Changes to merged contracts (additive)

- **mod-003** (recorded in its api-contract and Log): § 1.1 `WorkItem` gains
  § 1's 17 fields (§ 2 here); § 2.2's create answers them with the defaults
  and its body does not change (§ 3.1); § 2.4's `PATCH` gains the 17
  optional fields and their reasons (§ 3.2). A request without them behaves
  exactly as before; § 1.2 `WorkItemListItem` does not change.
- **mod-006** (recorded in its api-contract and Log): § 2.3 `BacklogItem` —
  the backlog's rows, nested rows included, and the board's cards — gains
  `priority` (§ 5 here). Nothing else changes.
