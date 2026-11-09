# API contract: scoped access (mod-019)

Fixed at stage 04 (backend plan), 2026-11-09. Reads: spec BR-SA-1..19, stories US-1..US-8, design D-1..D-13 and OQ-D1/OQ-D2 as answered, ADR-0029 as amended by ADR-0038 (`proposed`). This file is the interface the frontend reads. A change after this point is a commit of its own and a line in `status.md`; once the frontend stage has started it is escalated to the orchestrator. JSON is camelCase; ids are GUID strings (a user's id is the Keycloak subject string); errors are ProblemDetails with `errorCode` (registry `docs/error-codes.md`; texts in `contracts/errors/organizations.json`). Conventions of mod-016 §§ 0.4-0.5 (`UserRef`, display-name order, 2-character minimum, 20 results, `moreMatched`) and mod-018 § 1 apply unchanged.

## 0. The model in six lines

- A **grant** is one role held by one active organization membership on one **department**, **project** or **team**. The role is `reader`, `member` or `manager` in every scope (UI «Читатель», «Участник», «Менеджер»). One grant per member and object; changing the role replaces it. A project node and a sub-area hold no grant (OQ-1 A).
- Grants are written under the object's own address: `/areas/{areaId}`, `/projects/{projectId}`, `/teams/{teamId}`. The grant's key in the path is the **grantee's user id**.
- The check is the one of ADR-0029 § 6 on the addressed object: the organization role's bundle plus the bundles of the grants that reach it. A route that is not yet scoped (§ 8) keeps checking the organization role alone.
- A grantee is never the giver (BR-SA-19); the actor may remove their own grant.
- «Наследовать доступ» is a switch on project nodes and sub-areas (§ 3); a department's value is stored and never served.
- Codes are served per object (§ 5); the organization answer keeps the organization-scope codes only.

## 1. Shapes

```
GrantRole   "reader" | "member" | "manager"
GrantRow    { user: UserRef, role: GrantRole,
              origin: "held" | "department",            // "held" on every row except a project's inherited ones
              department: { id, name } | null,          // set when origin = "department"
              givenBy: UserRef, givenAt: datetime }   // always a UserRef; a giver the projection does not know has state "unknown"
GrantList   { items: GrantRow[],                         // by display name, then role strongest first, then origin "held" first
              inheritance: { departmentId, departmentName, inheritsAccess: bool } | null }   // projects only: null for a top-level project
GrantCandidates { items: UserRef[], moreMatched: bool }
SwitchImpact { holderCount: int,                         // distinct users who stop being reached; administrators and holders of a project grant that covers every code the switch takes from them excluded (OQ-12)
               callerIncluded: bool,                     // the caller is among them
               givenGrants: [{ user: UserRef, role: GrantRole, givenBy: UserRef }],   // project grants on the owning project given by those users, at most 50
               givenGrantsTotal: int }                   // the count behind the list (the list is cut at 50)
```

`UserRef` is mod-016's (`{ id, displayName, email, state: "active"|"inactive"|"unknown" }`).

## 2. Grants

| Route | Declared code, on the object | Body → Answer |
|---|---|---|
| `GET /areas/{areaId}/grants` | `organizations.area.read` | → 200 `GrantList`. 404 `area.not_found` |
| `POST /areas/{areaId}/grants` | `organizations.area.grant` | `{ userId, role }` → 201 `GrantRow` |
| `PUT /areas/{areaId}/grants/{userId}` | `organizations.area.grant` | `{ role }` → 200 `GrantRow` |
| `DELETE /areas/{areaId}/grants/{userId}` | `organizations.area.grant` | → 204 |
| `GET /areas/{areaId}/grant-candidates?text=` | `organizations.area.grant` | → 200 `GrantCandidates` |
| `GET /projects/{projectId}/grants` | `organizations.project.read` | → 200 `GrantList`, **inherited rows included** (`origin: "department"`, one per department grant that reaches the project's node, only while the project node's switch is on). 404 `project.not_found` |
| `POST /projects/{projectId}/grants` | `organizations.project.grant` | as above |
| `PUT /projects/{projectId}/grants/{userId}` | `organizations.project.grant` | as above; only a **held** grant is changed (an inherited one is changed on the department) |
| `DELETE /projects/{projectId}/grants/{userId}` | `organizations.project.grant` | → 204 |
| `GET /projects/{projectId}/grant-candidates?text=` | `organizations.project.grant` | → 200 `GrantCandidates` |
| `GET /teams/{teamId}/grants` | `organizations.team.read` | → 200 `GrantList` (`inheritance: null`). 404 `team.not_found` |
| `POST`, `PUT`, `DELETE`, `GET …/grant-candidates` on `/teams/{teamId}/grants…` | `organizations.team.grant` | as above |

- **Order of answers** (mod-018 § 1 with the grant's own steps): 401; 404 `organizations.organization.not_found`; 403 `organizations.access.forbidden` for the declared code on the addressed object (the organization admin holds every code); 404 of the address, outside in (`area.not_found` / `project.not_found` / `team.not_found`); **409 `organizations.grant.scope_not_allowed`** when the addressed area is a project node or a sub-area (BR-SA-4; a department only holds grants) — on GET grants and grant-candidates as well as on the writes; 400 (`role`, every reason at once; `userId` blank → `membership.user_invalid`); 400 `organizations.grant.self_grant` when `userId` is the caller (POST and PUT; BR-SA-19); 404 `organizations.membership.not_found` («Участник не найден.», the grantee is not an active member of this organization, any cause alike); then the write.
- **POST** on a user who already holds a grant on the object replaces nothing: 409 `organizations.grant.changed`. **PUT** and **DELETE** on a grant that is not there: 409 `organizations.grant.changed`. A PUT to the same role is a 200 that changes nothing and keeps the giver and time.
- `GrantRow.givenBy` / `givenAt` record the last giver or changer and the time. `userId` of another organization's member, a removed member and an unknown id all answer 404 `membership.not_found`.
- **Concurrency.** The write takes the organization membership `FOR KEY SHARE` through the composite key: a membership removed first fails the insert and answers `membership.not_found`; one removed after cascades the row away (BR-SA-3). Two managers removing each other's grants may both succeed (BR-SA-2).
- **Grant candidates** (OQ-D2, the object-aware search): active members of this organization whose display name or e-mail matches `text` (short text is the directory search's own rule, the same as the member candidates of mod-016: no 400 of its own), **excluding** the caller and everyone who already holds a grant on this object; 20 results, `moreMatched`. Held-only: a department grant that reaches a project does not exclude its holder (the user may also be given a project grant).
- **Lifecycle** (BR-SA-3): removing a membership ends its grants in the same statement (cascade); deleting a department ends its grants (cascade); deleting a team ends its grants in the same transaction as the team's other ends; deactivating a project keeps them.

## 3. «Наследовать доступ» (US-2)

| Route | Declared code | Body → Answer |
|---|---|---|
| `PUT /areas/{areaId}/inherits-access` | `organizations.area.write` on the node | `{ value: bool }` → 200 `{ inheritsAccess: bool }`. Second code on the node's **parent**: `organizations.area.write` (a department parent or, for a sub-area, its parent), `organizations.area.create` for a top-level project node (BR-SA-7), both after the 403 of the first and before the 404 of nothing else. 409 `organizations.area.type_not_allowed` for a department. The decision for the current parent is taken on the **locked** row (BR-SA-13); turning it on again is a no-op 200 |
| `GET /areas/{areaId}/inherits-access/impact` | `organizations.area.write` on the node (+ the parent's, as above) | → 200 `SwitchImpact`: what turning the switch **off** would do now, read once when the dialog opens and not re-judged at confirm. For a project node: holders of department grants that reach the node and stop reaching it, a project-grant holder spared only when that grant covers every code the switch takes from them (OQ-12). For a sub-area: holders of grants whose area codes reach it through its parent and stop; `givenGrants` is the owning project's held grants given by those users (OQ-D1). A department or an already-off node: 409 `organizations.area.type_not_allowed` / `holderCount: 0` |

Turning it off ends the actor's own inherited «Менеджер» there, and takes effect on the next request. **Where the state is read:** `AreaPage` and `AreaNode` gain `inheritsAccess: bool | null` (null for a department: never shown) and `AreaPage` gains `department: { id, name } | null` (the department above a project node; null at the top level); `GET /projects/{id}` gains `department: { id, name } | null` and `inheritsAccess: bool | null` (the project node's value; null when there is no department).

## 4. Links under grants (US-7, OQ-D2)

- Creating a project–team link or a department–team link needs the end's link code on **both** ends (`project.link` + `team.link`; `area.link` + `team.link`). Removing a project–team link needs both; removing a department–team link needs either (BR-SA-15). The addressed end's 403 comes before the other end's 404.
- **Per-row flag.** Every link table row gains `canUnlink: bool` — whether the caller holds what the unlink needs: rows of `GET /projects/{id}/teams` and `GET /projects/{id}/team-links`, `GET /teams/{id}/projects`, `GET /areas/{id}/teams`, `GET /teams/{id}/areas`. The row never decides a request: the unlink route does.
- **Filtered offers.** `GET /projects`, `GET /teams` and the area tree `GET /areas` gain the optional query **`linkableFor=team|project|area`**: the answer keeps only the objects on which the caller holds the link code of the *other* end's counterpart (`linkableFor=project` on `GET /teams` keeps teams with `team.link`; `linkableFor=team` on `GET /projects` keeps projects with `project.link`, on `GET /areas` keeps departments with `area.link`). An unknown value is a 400 `organizations.validation_failed`. No matrix of every object is served.

## 5. Codes served per object (US-4, BR-SA-11)

- `GET /organizations/{id}/access` (existing) keeps `{ role, codes }` with the **organization-scope codes only**.
- `GET /areas/{id}`, `GET /projects/{id}`, `GET /teams/{id}` gain **`codes: string[]`**: the caller's effective codes on that object, in the catalog's order.
- A multi-project read (`GET /projects`, the team's and department's project lists) gains `codes` on each project row **once per project**, never per work item.
- `GET /areas/writable-ids` (a sibling of the tree, declared `organizations.area.read`, because the tree is a bare array the panel already consumes) returns `Guid[]`: the ids of the nodes where the caller holds `organizations.area.write`, once, computed as a set (the read's statement count and 2 s budget stay as pinned).

## 6. Creating a project (BR-SA-17)

`POST /projects` keeps its path and body. The place decides: `organizations.area.write` on the department named by `parentAreaId`, `organizations.area.create` at the top level, **decided at step 0**, before any lookup. The route is on the inventory's exception list: "the addressed object is the place, carried in the body (mod-018 api-contract § 4); decided at step 0, before any lookup". A non-admin gets 403 for any place they lack the code on (unknown, deleted, foreign and non-department places included). The administrator gets 404 `area.not_found` for an unknown place and 409 `area.type_not_allowed` for a project node or sub-area. `organizations.project.create` is not registered. The creator reaches the new project through the department grant.

## 7. Error codes

New (texts in Russian in `docs/error-codes.md`, Kazakh in the catalog, both `kkNeedsReview`):

| Code | Status | Where | Text |
|---|---|---|---|
| `organizations.grant.self_grant` | 400 (field `userId`) | POST, PUT grants | Нельзя выдать доступ самому себе. |
| `organizations.grant.role_invalid` | 400 (field `role`) | POST, PUT grants | Выберите роль: читатель, участник или менеджер |
| `organizations.grant.scope_not_allowed` | 409 | grants on a project node or a sub-area | Эту роль нельзя выдать на этом объекте. |
| `organizations.grant.changed` | 409 | POST on a held grant; PUT, DELETE on a missing one | Доступ уже изменён или отозван. Список обновлён. |

Existing and reused: `organizations.membership.not_found`, `membership.user_invalid`, `organizations.access.forbidden`, `organizations.validation_failed`, `organizations.area.not_found`, `.type_not_allowed`, `.root_is_fixed`, `organizations.project.not_found`, `organizations.team.not_found`, `organizations.organization.not_found`.

## 8. What is scoped in which delivery step

The grants take effect on a route when that route declares its resource scope. The plan (`plan-backend.md` § 3) lists the order; until a route is moved it checks the organization role alone and a grant changes nothing there (US-1 "shippable: no"). The routes of §§ 2-3 declare their scope from the first step; the rest of the organization routes follow in the route-policy step, with `boards.projectbacklog.reorder`, `organizations.visibility.write` staying with feat-041.

## 9. The role-bundle contract

`contracts/access/role-bundles.json` carries, machine-readable: the instance table (the four client roles of `mustask-api` and the codes each gives, ADR-0033), the organization bundles, the scoped bundles per grant scope and role, and the catalog with each code's source and resource scope. `Mustask.Contracts.Tests` compares it with the domain. The panel imports it at build time in place of `src/frontend/src/test/organizationRoles.ts` and `src/frontend/src/shared/api/tokenClaims.ts` (stage 6).

## 10. Addendum after backend QA D-3 (2026-11-09, OQ-11)

`GET /api/organizations/{organizationId}/workitems/assignee-candidates?text=&projectId=` takes one new **optional** query parameter, `projectId` (a project id of the organization). Backward compatible: without it the answer is as before (the organization role's `workitems.workitem.write`, otherwise 403 `organizations.access.forbidden`). With it, the code is held on that project through the organization role or a grant that reaches the project. An id that names no project of the organization is judged by the organization role alone. The route declares the organization code in the inventory's decided-in-handler list (the object is a query value). Answers are unchanged: 200 `{ users, moreMatched }`, 400, 401, 403, 404. The frontend passes the project of the item form.

Also, no contract change but a fixed order (BR-SA-9, US-7): on the link and unlink routes the second code is judged after the ids of the body or address are found (404 first, then 403): `POST /projects/{id}/teams`, `POST /teams/{id}/projects`, `POST /teams/{id}/areas`, `POST /areas/{id}/teams`, `POST /projects/{id}/teams/{t}/unlink`, `GET /projects/{id}/teams/{t}/unlink-count`.
