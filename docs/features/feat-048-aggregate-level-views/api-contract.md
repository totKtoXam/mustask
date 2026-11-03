# feat-048 — API contract

Status: written by the backend stage with the code (2026-11-03). Spec: `spec.md` (BR-AV-1..12, US-11); design: `design.md` § 6;
cursor rules: ADR-0036. All routes are under `/api/organizations/{organizationId}`, behind the organization code
`workitems.workitem.read` (`GET` only), and answer 404 `organizations.organization.not_found` to a non-member. Values of the query
string reach the handler as strings; a selector or filter value that names nothing selects nothing and is never an error.

## 1. Query parameters (shared)

| Parameter | Meaning |
|---|---|
| `departmentId` (repeated) | Departments whose projects are in scope. |
| `projectId` (repeated) | Projects in scope. |
| `teamId` (repeated) | A team id, or `none` for items owned by no team. |
| `state` (repeated), `assigneeId` (repeated, id or `none`), `search` | feat-003 filters and keyword rules, across the organization. The keyword may spell an identifier with its project key. |
| `grouping` | `none` (default), `department`, `project`, `team`. A grouping the selectors do not leave open (BR-AV-2: a chosen project is not offered `project` or `department`, a chosen team not `team`, both chosen only `none`) is 400 `boards.validation_failed`, field `grouping`, reason `boards.invalid_grouping`. An unknown name is the same 400. |
| `group` (group reads) | The group's id, or `none` for «Без департамента» / «Без команды». Ignored when `grouping=none`. Malformed: 400 field `group`, reason `boards.invalid_parameter`. |
| `limit` (group reads) | 1..50, default 50. Else 400 field `limit`, reason `boards.invalid_parameter`. |
| `cursor` (group reads) | Opaque, forward only, issued for one request: a cursor that is malformed or was issued for another view, level, grouping, group, column or filter set is 400 field `cursor`, reason `boards.invalid_cursor`. |

## 2. Routes

1. `GET /backlogs/{level}/groups`, `GET /boards/{level}/groups`, `GET /workitems/groups` — the grouped counts.
   `{ grouping, groups: [{ kind, id|null, key?, name|null, count, total, columns? }] }`. Order (BR-AV-4): the group of no department / no
   team first, then by name, ties by id; empty groups absent. `count` follows the selectors and filters, `total` does not. `columns`
   (boards only) is `[{ key, count }]` for every column of the level, key `<Category>-<index>`. Unknown level: 404 `boards.level.not_found`.
2. `GET /backlogs/{level}/rows?grouping=&group=&limit=&cursor=` — a page of one group: `{ rows: [...], nextCursor }`. By project: the project's
   rank order; every other grouping: change time descending, number descending, project key. Rows are flat (the tree of feat-027 is not
   paged by a cursor); each carries `project`, `team`, `parent` and `changedAt`. No total, no page number. A by-project group of a project
   that is not active: 404 `organizations.project.not_found`.
3. `GET /boards/{level}/cells?grouping=&group=&column=&limit=&cursor=` — a page of one cell: `{ cards: [...], nextCursor }`; `column` is a
   key of the groups read, else 400 field `column`, reason `boards.invalid_parameter`. Cards carry their checklist (feat-032).
4. `GET /workitems/rows?grouping=&group=&limit=&cursor=` — as 2 for «Рабочие элементы»; always change-time order, even for a by-project group (BR-AV-8).
5. `GET /workitems?page=` — the organization-wide list: ADR-0015 envelope `{ count, data }`, 50 a page, change time descending, number
   descending, project key. `page` < 1: 400 `workitems.validation_failed`, `workitems.invalid_page`. A deleted item is listed only when a chosen `state` names its own.
6. `GET /workitems/assignee-options` — `[{ id, displayName, email, isActive, isMember }]`, as the project's.

Routes 2, 3 and 4 are read-limited per API process: 6 concurrent a user (Keycloak subject) and 40 in all, a queue of 6 / 20, a wait of at
most 3 s; past that **429** `http.too_many_requests` with `Retry-After: 2`.

## 3. Errors added

`boards.invalid_cursor`, `boards.invalid_grouping`, `boards.invalid_parameter` (400, in `boards.validation_failed`) and
`http.too_many_requests` (429) — `contracts/errors/`, `docs/error-codes.md`.

## 4. Departures from the design table (recorded in `status.md`)

- By-project backlog groups are flat rows, not feat-027 nested trees (a tree cannot be cursor-paged without per-page recursion).
- The cell read takes `column` (the key from the groups read), not `stateId`.
- `/workitems` routes have no `{level}` segment: `/workitems/groups`, `/workitems/rows`.
- A migration (`AggregateViewIndexes`) adds the change-time indexes of D-5 and replaces `ix_work_items_owning_team_id_type_id`.
