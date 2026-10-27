# API contract — feat-003-work-item-search

Fixed at stage 4a (2026-10-18), before the code. From now on it changes only with a note in [status.md](status.md) and a Log line; once the frontend stage has started, a change goes to the orchestrator (04-backend.md § 4a.4). The plan is [plan-backend.md](plan-backend.md). Rules BR-WS-n are [spec.md](spec.md)'s.

The panel uses all of it. It restates, and so supersedes in part, mod-003 § 1.2 and § 2.1, mod-017 § 2.1 and § 3, and mod-005 § 3.2 (§ 7). Everything not said here is as those contracts say.

## 0. Rules shared by the routes

- **Access.** Both routes sit under `/api/organizations/{organizationId}/projects/{projectId}/workitems` and declare `workitems.workitem.read` (ADR-0029 § 7: a read inside one project needs only the gate; ADR-0027 for the check). All three roles hold it; a non-member of the organization, an instance administrator included, gets 404 `organizations.organization.not_found`. No permission code is added.
- **Not found, from the outside in** (mod-003 § 0.3): organization, then project (malformed, unknown, another organization's or deactivated: 404 `organizations.project.not_found`). A filter value is never part of this chain.
- **Ids** are untrusted strings (mod-003 § 0.2); responses write ids as stored.
- **Time and text**: mod-003 § 0.6.

## 1. Shapes

### 1.1 `WorkItemListItem` — a row of the list (restates mod-003 § 1.2, mod-017 § 1.2)

`id`, `identifier`, `number`, `type`, `title`, `assignee`, `state`, `area`, `team`, `changedAt` as before. **`assignee` gains `isActive`** (OQ-2 A):

```json
"assignee": { "id": "5d1e8f3a-…", "displayName": "Пётр Ли", "email": "petr@alfa.test", "isActive": false, "isMember": true }
```

| Field | Meaning |
|---|---|
| `isActive` | the user directory's flag (mod-014 BR-UP-11): `false` for a disabled or deleted account, and for an id the directory does not know. The panel adds «(неактивен)» only when `displayName` is not `null`. |
| `isMember` | as mod-016 BR-M1-26: the assignee holds a membership in the organization now; `false` is «(не в организации)». |
| the rest | `displayName` and `email` are both `null` for an id the directory does not know («Неизвестный пользователь»), `email` is `""` for a known user without one. |

The card's `assignee` (mod-003 § 1.1) is **unchanged**: `{ id, displayName, email, isMember }`, until feat-017. A row in `Удалена` reads like any row: its `state` is the Removed state with category `Removed`, its `changedAt` is its last change; the list carries no `deletion` (OQ-3 A).

### 1.2 `AssigneeFilterOption` — an option of the «Исполнитель» filter

```json
{ "id": "5d1e8f3a-…", "displayName": "Иван Котов", "email": "ivan@alfa.test", "isActive": true, "isMember": false }
```

`displayName` and `email` are `null` and `isActive` is `false` for an id the directory does not know; `email` is `""` for a known user without one; `isMember` is the membership alone (a member the directory does not know is still a member). The panel's text is `userNameWithMarks` (mod-005, BR-WS-7): the name, then «(не в организации)» when `isMember` is false, else «(неактивен)» for a known inactive user (the membership mark prevails); an unknown user reads «Неизвестный пользователь» with no «(неактивен)», and still «(не в организации)» when the membership ended.

## 2. Routes

### 2.1 `GET …/workitems` — the project's list (US-1..US-3; BR-WS-1..-4, -9..-13)

mod-003 § 2.1 with mod-005 § 3.2 and mod-017 § 2.1, plus three parameters. Unknown parameters are ignored.

| Query | Shape | Meaning |
|---|---|---|
| `page` | optional, 1-based | as before; below 1 is 400 `workitems.validation_failed` with `errors.page: workitems.invalid_page`; a page past the last answers an empty `data` and the right `count` |
| `teamId` | repeated | mod-017 § 3, unchanged: a team's id or `none` |
| `state` | repeated, one per chosen name | a state name **as stored**, compared ordinally, letter case included: `новая` names no state. One name matches every item whose state, on its own type, bears it («Разрешена»: a story and a bug). |
| `assigneeId` | repeated | a user id as stored (ordinal), or the literal `none` (`NONE` names nothing) for the items with no assignee |
| `search` | one value | the keyword (§ 3.3). Given more than once, the first non-blank value counts |

- **Combination** (BR-WS-2): an item is listed when it matches every dimension that is set; within `state`, `assigneeId` and `teamId` any value matches. A dimension with no value sets no condition.
- **«Удалена»** (BR-WS-4, ADR-0030 rule 2): with no `state` the list holds live items only, as in mod-005 § 3.2. With `state` set it also reaches the deleted items: an item is listed when its state's name is among the chosen, whether live or in the Removed category, and "deleted" is the category, never a name written in code. The deleted items that match every other dimension appear beside the live items the other chosen names match, in the one order. No `state` value names a deleted item unless it is the name of a Removed-category state.
- **Blank values are left out** (BR-WS-11): an empty or whitespace-only `state`, `assigneeId` or `search` value is dropped; if none is left the dimension is not set. (`teamId` keeps mod-017's server reading: a blank value is a set filter that names no team.)
- **A value that names nothing** (BR-WS-10) selects nothing, alike for every kind, and is never an error: a state name that is no state of the process (or in another letter case); a user id that is malformed, unknown, another organization's, a member assigned nothing in the project, or a person assigned only deleted items while no chosen `state` names a Removed state. Status, body and `count` are the same for each; beside a valid value it adds nothing. A value holding U+0000 or longer than 255 characters names nothing as well.
- Order, 50 a page, `count` = the number of matching items (BR-WS-12); one read shows an item once.
- **200** `{ "count": 3, "data": [ WorkItemListItem, … ] }` (ADR-0015). **400** `workitems.validation_failed` (`page`). **401**, **403** `organizations.access.forbidden` «Недостаточно прав» for a role without `workitems.workitem.read`. **404** organization, project (§ 0). A filter value never answers an error; a request line past 8 KB (some 150 values) fails as any read does (accepted, spec § Option sizes).

### 2.2 `GET …/workitems/assignee-options?state=…` — the options of «Исполнитель» (US-2; BR-WS-6, -7)

Who can be chosen in the filter: the people assigned to the project's items.

| Query | Meaning |
|---|---|
| `state` | repeated, the same as the list's: the **chosen** states, which decide whether the people of deleted items are offered. Nothing else is taken |

- The options are the distinct assignees of the project's items **outside «Удалена»**, and also of its **deleted** items whose state's name is among `state` (so "while «Статус» names «Удалена»" is decided by the server; the panel sends the names it already holds). Members, former members and disabled accounts alike; nobody assigned nothing, no assignee of another project or organization. Filters other than `state` do not narrow it; no option carries a count.
- **Order** (mod-016 § 0.5, mod-014 BR-UP-15): known users by display name (the `ru` collation at primary strength), equal names by id in code-point order; then the users the directory does not know, by id. «Не назначен» is not in the answer: the panel puts its constant last, with the value `none`.
- **200** a JSON array of `AssigneeFilterOption` (an unpaged list, as `…/teams`), `[]` when nobody is assigned. **401**, **403** as § 2.1. **404** organization, project. A `state` value that names nothing adds nobody; no error.

Access row for mod-016's inventory: `GET /api/organizations/{organizationId}/projects/{projectId}/workitems/assignee-options`, `workitems.workitem.read`. It is **not** the picker of the card (`…/workitems/assignee-candidates`, active members, `workitems.workitem.write`, mod-016 BR-M1-26).

## 3. The shared filter specification — three more dimensions (BR-WS-16)

mod-017 § 3 founded it with the team and feat-040 adds the project. One meaning per dimension wherever it appears: `Mustask.Domain/WorkItems/WorkItemFilter` (parsed values), `Mustask.Infrastructure/WorkItems/WorkItemFilterPredicate` (the one predicate), `WorkItemSets` (the entry points). Only the project's list takes the three dimensions in this line; feat-013 and feat-043 bring them to the views and the cross-project list.

### 3.1 State

`state`, repeated; the name as stored (§ 2.1). The predicate is "the item's state is a state definition with a chosen name"; the entry points choose which items it can reach (live only; live and deleted when `state` is set). A name has one category on every type of a process (`StateNameRule`), so a name and its category never disagree.

### 3.2 Assignee

`assigneeId`, repeated; an item matches when its assignee is any chosen id, or when it has none and `none` is chosen. Ids are compared as stored; nothing is looked up in the directory, so a former member's items are found by id.

### 3.3 Keyword

`search`, trimmed (every Unicode white space) at both ends; blank sets no filter. An item matches when:

- (a) its title contains the keyword as one piece of text, ignoring letter case, **«ё» equal to «е»** (both sides lower-cased by the database in the `ru` ICU collation, then «ё» replaced by «е»). Every character is literal: `%`, `_` and `\` find only themselves.
- (b) the keyword is its identifier, in any letter case: `MSK-BUG-42` or `msk-bug-42`. Its project key must be the addressed project's, its type key the item's type's, its number the item's. A part (`MSK-BUG-4`, `BUG-42`), an identifier with another type's key, another project's identifier (`WRH-US-1` of the same organization, `OTH-US-1` of another) and `MSK-BUG-042` match no identifier; they still match a title that contains them.
- (c) the keyword is its number, in digits as the identifier writes it: `42`, not `042`. The number is unique in the project.

An item matching by more than one is listed once. A keyword longer than 255 UTF-16 units after trimming, one holding a line break (`\n`, `\r`, `\v`, `\f`, U+0085, U+2028, U+2029) or U+0000, matches nothing; digits past `int` are text for (a) only. None is an error. «Описание», other fields and comments are not searched; words are not reduced to stems. A deleted item matches only while a chosen `state` reaches it (§ 2.1).

## 4. Error codes

None is added; `docs/error-codes.md` and `contracts/errors/*.json` do not change. The routes answer the registry's: `organizations.organization.not_found`, `organizations.project.not_found`, `organizations.access.forbidden`, `workitems.validation_failed` with `workitems.invalid_page`, and the platform's `http.unauthorized` and `http.bad_request` (a `page` that is not an integer).

## 5. Summary of routes

| Route | Code | Change |
|---|---|---|
| `GET …/workitems` | `workitems.workitem.read` | `state`, `assigneeId`, `search`; the row's `assignee.isActive` |
| `GET …/workitems/assignee-options` | `workitems.workitem.read` | new |

The scoped-route inventory grows by one (59 to 60); the published API description lists the new operation under the tag `WorkItems`.

## 6. Query shape and speed (spec § NFR)

- **The list** keeps its statements: the project, the count, the page (the filters are composed into those two), the area nodes, the assignees' lookup and the memberships: a filter adds none, and a request with all four dimensions takes the number a plain one does, for 50 or 5 000 items (the `UNION ALL` of live and deleted items is part of the same statements). At 100 000 items in the organization, 5 000 in the project, 500 of them deleted: the first page and the count within 2 s, unfiltered, with each dimension alone and with all four, «Удалена» among them.
- **The options** take the project, one statement for the ids, one for the memberships, and mod-014's lookups, one per 500 distinct ids (BR-UP-17): ⌈people / 500⌉. Nothing else grows with the people. With 2 000 people assigned: within 2 s.
- Statement-count tests pin both; a statement added is a decision.

## 7. What this contract supersedes in earlier ones (P-10)

| Contract | Sections |
|---|---|
| [mod-003](../mod-003-work-item-core/api-contract.md) | § 1.2 (the row's `assignee` gains `isActive`, changing mod-003 D-2's cut for the list), § 2.1 ("There is no `filter`": three parameters) |
| [mod-017](../mod-017-work-item-owning-team/api-contract.md) | § 2.1, § 3 (the shared specification gains three dimensions; the team's meaning is unchanged) |
| [mod-005](../mod-005-work-item-delete-restore/api-contract.md) | § 3.2 ("no parameter lists deleted items in this line": `state` does) |
| [mod-016](../mod-016-organization-access/api-contract.md) | § 4.2 (a row: `assignee-options`, `workitems.workitem.read`); BR-M1-26 "the marks are feat-017's" (the list's column takes them here, OQ-2 A) |
