# feat-062-sight-in-work-views: API contract

The sight rule (doc-016 BR-TD-12..-16, `feat-041` api-contract) is applied to the
multi-project work item views, the sprint reads and the body ids that reach a team
or a sprint. **No route, request body, response field or status code is added or
removed.** What changes is what an existing answer contains for a caller who does
not see a project, and which of two refusals a hidden id gets. The administrator
(`organizations.organization.*` role admin) sees everything and is unchanged.

"Sees" is the one set form of feat-041 (`AccessSet`, `IOrganizationAccess`): a project
is seen when the caller holds a grant that reaches it, a department grant above it, or
the project is flagged visible to the organization. The narrowing is one value,
`VisibleProjects` (`All` for an administrator, otherwise the ids of the seen projects),
read once per request from the cached set. It is the project part of feat-061's `AccessSet.Sighted()`
(`CallerAccess.Sight`; `CallerAccess.Projects` derives it): one set form, one computation, no second helper.

## 1. Reads narrowed to the seen projects (BR-TD-17)

Only items of projects the caller sees take part, inside the same statement that
groups, counts and pages, so no total, page, cursor, count or "ещё" reveals a hidden
project. Equal in shape to the answer of the same data without the hidden project.

| Surface | Routes |
|---|---|
| feat-048 aggregates: groups, group rows, board cells, counts (organization and department level) | `GET /api/organizations/{o}/{backlogs\|boards}/{level}/groups`, `/rows`, `/cells`; `GET .../workitems/groups`, `/workitems/rows` |
| Flat list and keyword/selector filters | `GET .../workitems` |
| Assignee options | `GET .../workitems/assignee-options` |
| Team backlog and board, including the `projects` list (linked projects) and ranks | `GET .../teams/{t}/backlogs/{level}`, `GET .../teams/{t}/boards/{level}` |
| Sprint list counts | `GET .../teams/{t}/sprints` |
| Sprint card and close dialog counts | `GET .../sprints/{s}`, `GET .../sprints/{s}/close-preview` |
| A Closed sprint's outcome | `GET .../sprints/{s}/dynamics`, `/participants`, `/items` |

Directory-style lists that name projects (the department/project group lists of the
aggregates, the `projects` list of a team view) omit hidden projects.

Left untouched on purpose: `GET .../overview` and the selector offers (feat-061 owns
them).

## 2. Group addressed by a hidden project

`rows`/`cells` with `grouping=project&group=<hidden project id>` answer exactly what an
unknown project id answers: 404 `organizations.project.not_found`. A selector query id
that names a hidden project selects nothing, as an unknown id does (200; feat-041 R-5).

## 3. Team backlog reorder (feat-041 R-5 remainder)

`POST .../teams/{t}/backlogs/{level}/reorder`: when `itemId` or `afterItemId` names an
item of a project the caller does not see, the answer is the one an id that names nothing
gets: 409 `boards.backlog.reorder_stale`. Hidden items keep their places; a reorder by a caller
who sees them (the administrator) moves them as before. Visible reorders are unchanged
(204).

## 4. Body ids that reach a hidden team or sprint

| Route | Body id | Before | Now (hidden) |
|---|---|---|---|
| `POST .../projects/{p}/workitems` | `team.teamId` | 409 `organizations.team.not_in_project` for a visible project the team is not linked to, even when the team is hidden | 404 `organizations.team.not_found`, equal to an unknown team |
| `PATCH .../workitems/{id}` | `team.teamId` | same | same 404 |
| `PATCH .../workitems/{id}` | `sprint.sprintId` of a hidden team | 409 other-team | 404 `iterations.sprint.not_found`, equal to an unknown sprint |
| `POST .../sprints/{s}/close` | `destinationSprintId` of a hidden team | 409 `iterations.sprint.close_destination_invalid` | 404 `iterations.sprint.not_found` |
| `PUT .../projects/{p}/default-team` | `teamId` of a team the caller does not see | 409 `organizations.team.not_in_project` (unlinked), against 404 for an unknown team | 404 `organizations.team.not_found`, equal to an unknown team; reachable by the project and department managers (`project.write`) |
| `POST .../projects/{p}/teams/{t}/unlink` | `destination.teamId` of a team the caller does not see | 409 `organizations.team.not_in_project` | 404 `organizations.team.not_found`, equal to an unknown team; reachable by the managers (`project.link`, `team.link`) |

A visible team that is simply not linked to the project still answers 409 (control).

## 5. Sprint close and removal

`POST .../sprints/{s}/close` moves **all** items, hidden ones included; the response
(`SprintResponse`) carries no counts, so nothing is disclosed. `DELETE .../sprints/{s}`
refuses a sprint that holds items with the same generic refusal whether or not a hidden
item is among them (no counts in the answer). Both only gained tests.

## 6. Body ids audited and found to need no change

- `MoveTeam`: admin-only organization code (`team.create`, `OrganizationEndpoints.cs`); the default team moved to § 4 (the code is not admin-only).
- `MoveArea`, `CreateArea`, `CreateProject` place and body teams/projects: the 403 is
  identical for a hidden and an unknown id (role alone, link codes are admin-only).
- Parent ids (`SetWorkItemParent`, save, create): looked up inside the visible path
  project, so a hidden-project item is the same not-found as an unknown one.
- Assignee/user ids: values, not objects.
- Grants: the object is in the path and gated by feat-041.
- Link routes: `LinkProjectsToTeam`/`LinkTeamsToProject` done in feat-041 R-5; the default team and the unlink destination in § 4 of this contract.
- Item, sprint and team path ids: gated by feat-041's gate order.

## 7. Query shape

No statement is added to any read. The sight is a `WHERE project_id = ANY(@visible)`
inside each existing statement (a parameterized array; no-op for the administrator).
A non-admin request additionally pays the set's own fixed statements, once per request,
for five projects as for fifty. The existing pins (sprint dynamics 8, participants 8,
items 4, list 6, close 17) are unchanged and a test asserts a narrowed read takes the
same count; `AggregateViewStatementCountTests` asserts member = administrator + set.
