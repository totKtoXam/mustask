# API contract — feat-028-org-level-teams

Fixed at stage 4a (2026-10-18), before the code. From now on it changes only with
a note in [status.md](status.md) and a Log line; once stage 6 (frontend) has
started, a change goes to the orchestrator (04-backend.md § 4a.4). The plan is
[plan-backend.md](plan-backend.md); the rules are [spec.md](spec.md) (BR-OL-n;
BR-MX-n are doc-012's) and the decisions marked D-n are the plan's § 1.

This task is **breaking** (doc-012 § Decomposition). It adds 16 organization-addressed
routes, removes 8, and changes the code or the meaning of the two team routes that keep
their path (§ 4); it registers **four permission codes** and narrows a fifth (§ 0.1);
it adds **two error codes**, retires one and changes one text (§ 3); it adds **one
migration** that drops every team (§ 5). Earlier contracts it supersedes are named in
§ 7; everything not named there stands.

## 0. Rules shared by every endpoint

Exactly the earlier contracts' ([mod-002](../mod-002-organizations-projects-teams/api-contract.md) § 0,
[mod-016](../mod-016-organization-access/api-contract.md) § 0,
[mod-017](../mod-017-work-item-owning-team/api-contract.md) § 0), plus the rules below.
Every route is under `/api/organizations/{organizationId}` (`…` below).

### 0.1 Access (BR-OL-5; ADR-0029 § 4, "What the delivery lines implement")

The scoped check (ADR-0027) runs before the body is read: 401, then 404
`organizations.organization.not_found` for a person who is not a member (an instance
administrator included), then 403 `organizations.access.forbidden` for a role without
the route's code. **Four codes are new**, and `organizations.team.write` is narrowed;
until `mod-019-scoped-access` each is checked against the organization role alone, so
**only «Администратор» holds the four**, and no «Участник» or «Читатель» loses a right
(`team.write` was hers alone).

| Code | Held by | Meaning now |
|---|---|---|
| **new** `organizations.team.create` | admin | create a team |
| **new** `organizations.team.delete` | admin | delete a team; the deletion's per-project counts |
| **new** `organizations.team.link` | admin | the team's end of a link or an unlink |
| **new** `organizations.project.link` | admin | the project's end of a link or an unlink |
| `organizations.team.write` *(narrowed)* | admin | a team's name, description and members (it no longer creates or deletes) |
| `organizations.project.write` | admin | create, rename, **describe** a project; **name the default team** (mod-017 checked it with `team.write`) |
| `organizations.team.read` | admin, member, viewer | every read of a team, its projects, its members, the project's linked teams |

`GET …/access` lists the four new codes for «Администратор» and for no one else, with no
change of shape.

**A link joins two ends, so it needs both link codes (D-7).** The route declares the
code of the end whose address it is; the handler asks the other from
`IOrganizationAccess`, **first of all, before any id is looked at** (as `workitems.link.write`
is asked on a save with a parent), so a role without it learns nothing and nothing changes:

| Route | Declares | The handler also asks |
|---|---|---|
| `POST …/projects/{projectId}/teams` (link teams), `POST …/projects/{projectId}/teams/{teamId}/unlink`, `GET …/unlink-count` | `organizations.project.link` | `organizations.team.link` |
| `POST …/teams/{teamId}/projects` (link projects) | `organizations.team.link` | `organizations.project.link` |
| `POST …/teams` **with a non-empty `projectIds`** | `organizations.team.create` | `team.link` and `project.link` |
| `POST …/teams` **with a non-empty `memberIds`** | `organizations.team.create` | `organizations.team.write` |
| `POST …/projects` **with a non-empty `teamIds`** | `organizations.project.write` | `team.link` and `project.link` |

A team's deletion needs no code of the projects it reaches (ADR-0029 § 6, consequence
writes). The route inventory (`OrganizationAddressedRoutes`) lists every route below
with its declared code (§ 4); the old project-nested team routes are not in it.

### 0.2 Identifiers, bodies and lists

Ids are UUIDs, written lowercase in responses; in a path, a body or a query an id is
an untrusted string, accepted only in the canonical 8-4-4-4-12 form (either letter
case, not all zeros); anything else answers as an id that names nothing (mod-002
§ 0.2). A body field that holds an id is a JSON string or `null`; a number or an
object there fails binding with 400 `http.bad_request`. A list field
(`memberIds`, `projectIds`, `teamIds`) is an array of such strings, **absent or `null`
means empty, duplicates collapse**, and its size is bounded only by the organization's
own volumes (200 projects, 2 000 members); every list is checked in a number of
statements that does not grow with its length.

**Descriptions (BR-OL-3).** A team's or a project's `description` is `string | null`.
It is stored **exactly as sent**, spaces and line breaks included, and is at most
**2 000 characters** (UTF-16 code units, as the database's `char_length` counts). The
**empty string, `null` and an absent field are all "no description"** and answer
`null`. Over 2 000: 400 `organizations.validation_failed` with the field reason
`organizations.description_length` under `description`.

### 0.3 How a team named in a write is answered (BR-OL-6)

For an item's owning team (a create, a save) and the project's default team
(`PUT …/default-team`); the unlink's destination follows the same table (§ 2.4). The first
row that applies:

| The named team | Answer |
|---|---|
| a string that is not an id; no such team; a team of **another organization**; a **deleted** team | 404 `organizations.team.not_found` «Команда не найдена.» — identical status and body for each |
| an active team of the organization that is **not linked to this project** (a team linked to no project included) | 409 `organizations.team.not_in_project` «Команда не участвует в проекте.» |
| an active team **linked to this project** | accepted |

mod-017's row "a team of a deactivated project → 404" is gone: a team has no project of
its own. A refusal stores nothing of the request and consumes no number. The address's
not-founds and the 403 come before it (§ 0.5). **A team in the path of
`…/projects/{projectId}/teams/{teamId}/…` is not "named in a write"**: the team views (§ 2.5)
and `unlink-count` answer **404 `team.not_found`** for an active team of the organization that is
not linked to the project, the same as for a deleted one (BR-OL-11); the unlink itself treats an
unlinked pair as a success (§ 2.4).

### 0.4 Not found, from the outside in

| Level | `errorCode` | `detail` |
|---|---|---|
| organization | `organizations.organization.not_found` | «Организация не найдена.» |
| project | `organizations.project.not_found` | «Проект не найден.» |
| team | `organizations.team.not_found` | «Команда не найдена.» |

A **team's address is the organization's**: `…/teams/{teamId}`. A deleted team's
address, and one of another organization, answers `team.not_found` for every route
under it (its members, its projects, its counts). A project that is deactivated, of
another organization or unknown answers `project.not_found`, in a path and in a body
alike. No route answers "deactivated" or "deleted" as such (ADR-0026 rule 5).

### 0.5 The order of answers (BR-OL-14; mod-016 § 0.1)

401, organization 404, **403 for the route's code, then 403 for the second code of
§ 0.1**, then the address from the outside in (project 404, team 404), then 400, then
the not-founds of the ids a body names (each with its own code, project before team
when both), then the 409s. The order of the 409s on a create is: a member
(`member_not_in_organization`), then the name (`name_taken`). Special cases are in each
route. `feat-041-project-visibility` moves this to BR-MX-24 (ADR-0029 § 8).

### 0.6 Error body

`application/problem+json` as everywhere (mod-002 § 0.5). `errors` appears only with
`organizations.validation_failed`.

## 1. Shapes

### 1.1 `Team` (replaces mod-017 § 1.3 for the single team)

```json
{ "id": "…", "name": "MARS", "description": "Платформа и инструменты" }
```

`description` is `null` when there is none. **Removed:** `projectId`, `defaultMark`,
`isMember` — they were a project's view of a team and live on `ProjectTeam` (§ 1.3).
The answer of a create, a read, a rename and a description write.

### 1.2 `TeamListItem` and `TeamProject`

```json
{ "id": "…", "name": "MARS", "description": null,
  "projects": [ { "id": "…", "name": "Alpha" }, { "id": "…", "name": "Beta" } ] }
```

`TeamListItem` (the organization's list) carries the team's **active projects** only, by
name (Russian collation, letter case not deciding), then id; a team whose only projects
are deactivated has `[]`. `TeamProject` (a team's page): `{ "id", "name", "key" }`, the
same projects in the same order.

### 1.3 `ProjectTeam` (a project's linked team; replaces mod-017 § 1.3 for the project's list)

```json
{ "id": "…", "name": "MARS", "defaultMark": "named", "isMember": true }
```

| Field | Meaning |
|---|---|
| `defaultMark` | `"named"`: the project's settings name this team; `"onlyTeam"`: no team is named and this is the project's only linked team; `null`: neither (BR-OL-7). At most one team carries a mark. |
| `isMember` | the **caller** is in the team (mod-016 § 3.3); in this list only |

**Removed:** `projectId`. The list is **by name** (Russian collation, then id); no team
comes first. A team linked to the project but deleted never appears (deleting removes the
links).

### 1.4 `Project` (mod-002 § 2: `GET`, the create, the rename and the restore answers)

`{ "id", "organizationId", "name", "key", "description" }` — **`description` is new**
(`string | null`, § 0.2). The list items (`GET …/projects`, the deactivated list) are
unchanged and carry no description.

### 1.5 `DeletionCounts` and `UnlinkCount`

```json
{ "projects": [ { "projectId": "…", "name": "Alpha", "active": true, "count": 5 },
                { "projectId": "…", "name": "Delta", "active": false, "count": 3 } ] }
```
```json
{ "count": 7 }
```

`DeletionCounts` lists **every project the team is linked to, deactivated ones
included, zeros included**, by project name then id; `count` is the number of items the
team owns in that project **in any state** (Removed included) when the read runs.
`UnlinkCount` is the same number for one project. An item given to the team after a
count was read is not in it and is reassigned all the same.

## 2. Routes

### 2.1 The organization's teams (US-1, US-5; BR-OL-1, -3, -9, -12)

**`GET …/teams` — the active teams** · `organizations.team.read`
200 `TeamListItem[]`, by name (Russian collation, then id), no paging; an organization
with 200 teams lists them within 2 s in a fixed number of statements. **No team** is the
first state after the upgrade (BR-OL-13): `[]`. Errors: 401, 404 organization.

**`POST …/teams` — create a team** · `organizations.team.create` (+ § 0.1 seconds)

```json
{ "name": "MARS", "description": "…", "memberIds": ["u-1", "u-2"], "projectIds": ["0199…"] }
```

`description`, `memberIds` and `projectIds` are optional. In **one operation**: the team;
its members (each an **active member of this organization**, known to the user
directory and active there — `member_not_in_organization` otherwise, as an add on the
team's page); and its links, each recording the caller and the time. **Creating a team
creates no project, no area and no membership of the caller's** (BR-OL-2). On any
refusal nothing is created and the name stays free.

- **201** `Team`, `Location: …/teams/{id}`.
- **400** `organizations.validation_failed`: `name` (`organizations.name_length`: 1–100
  characters after trimming), `description` (`organizations.description_length`), every
  reason at once.
- **403** (route code; then § 0.1 seconds). **404** organization; **`organizations.project.not_found`**
  for a `projectIds` entry that is not an **active project of this organization**
  (malformed, unknown, foreign, deactivated — alike).
- **409** `organizations.team.member_not_in_organization` (a `memberIds` entry that is not an
  active member of this organization; the user may be in another organization, unknown, or
  disabled); **`organizations.team.name_taken`** — the name, compared ignoring letter case and
  surrounding spaces, is held by an **active team of this organization** (a deleted team's name
  is free; another organization may share a name). Two creations of one name at once end with
  one team and one 409.

**`GET …/teams/{teamId}` — one team** · `organizations.team.read` → 200 `Team`. **404**
organization, team (a deleted one included).

**`PUT …/teams/{teamId}/name` — rename** · `organizations.team.write`
Body `{ "name" }`. 200 `Team`. 400 `name`; 404 team; 409 `team.name_taken` (a team's own
name, in another letter case, never conflicts with itself). What the team owns, its
people and its links do not change.

**`PUT …/teams/{teamId}/description` — describe** · `organizations.team.write`
Body `{ "description": "…" | null }`; the empty string, `null` and `{}` clear it (§ 0.2).
200 `Team`. 400 `description`; 404 team.

**`GET …/teams/{teamId}/deletion-counts` — what a deletion would reassign** · `organizations.team.delete`
Read **when the confirmation opens**. 200 `DeletionCounts`; an empty `projects` for a
team linked to none. 404 team (deleted included). The number of statements is the same
for 2 projects as for 50.

**`DELETE …/teams/{teamId}` — delete a team** · `organizations.team.delete`
No body, **204**. In one operation over **every project the team is linked to,
deactivated ones included** (BR-OL-9):

1. every item the team owns, in any state, gets **no owning team** («Без команды») and
   shows the operation's time as `changedAt`; its project, identifier, assignee,
   estimates, state, parent, children, «Область» and project-backlog place are untouched;
2. the team's backlog places are deleted, so no item stands placed on a backlog of a
   team that does not own it;
3. every project's default-team setting that named the team is cleared;
4. every link of the team is removed, and its memberships end (mod-016 BR-M1-36; the people
   stay in the organization);
5. the team is deleted for good: it leaves every list, picker and filter, its name is
   free, and every request that addresses it answers `team.not_found`. No restore.

All or nothing; 5 000 owned items over three projects complete within 5 s. **There is no
409:** the last-team guard is retired (BR-OL-10). A write that names the team at the
same time (a save, a create, a default naming, a link, an unlink of it or naming it
as the destination, a reorder, a member add) either commits first and is taken with the
rest, or is refused with 404 `team.not_found`; a **child created beneath one of its
items** is never refused for the team and ends reassigned or on the project's default
(plan § 7.3). Two deletions that would leave a project with no team are both done.
Errors: 401, 403, 404 organization, team.

### 2.2 A team's projects (US-3; BR-OL-4, -5)

**`GET …/teams/{teamId}/projects` — the team's active projects** · `organizations.team.read`
200 `TeamProject[]` (§ 1.2); `[]` when none. 404 organization, team.

**`POST …/teams/{teamId}/projects` — link projects to the team** · `organizations.team.link` (+ `project.link`)
Body `{ "projectIds": ["…"] }`. **204.** Each pair is linked **once**: linking a pair that
is already linked changes nothing and the link keeps who made it and when (BR-OL-4); two
requests that link one unlinked pair at once leave one link and no 5xx. **All or
nothing**: if any entry is not an active project of this organization the answer is
404 `project.not_found` and nothing is linked. An absent, `null` or empty list is a no-op
204 (D-14). Linking the team to a project moves no item and sets no default.
Order: 401, 404 org, 403, 403 (second code), 404 team, 404 project. Errors: 401, 403, 404.

### 2.3 A team's members (mod-016 § 2, **the project leaves the address**)

Each is mod-016 § 2.1–§ 2.4 unchanged in behaviour and error table, at the team's address:

| Route | Code |
|---|---|
| `GET …/teams/{teamId}/members` | `organizations.team.read` |
| `GET …/teams/{teamId}/member-candidates?text=` | `organizations.team.write` (the search covers the organization's active members) |
| `POST …/teams/{teamId}/members` `{ "userId" }` → 201 `UserRef` | `organizations.team.write` |
| `DELETE …/teams/{teamId}/members/{userId}` → 204 | `organizations.team.write` |

Errors as before: 400 `userId`; 404 organization, team, and `team.member_not_found` on a
removal; 409 `team.member_not_in_organization`, `team.member_exists`. A person removed
from the organization leaves every team of that organization and no other (mod-016
BR-M1-36); a team deleted while an add ran leaves no membership behind.

### 2.4 The project's end (US-2, US-3, US-4; BR-OL-4..-8, -10)

**`GET …/projects/{projectId}/teams` — the project's linked teams** · `organizations.team.read`
200 `ProjectTeam[]` (§ 1.3), by name. `[]` for a project with no team — **the state of
every project right after the upgrade**. 404 organization, project (deactivated too).
In a fixed number of statements whatever the number of teams.

**`POST …/projects/{projectId}/teams` — link teams to the project** · `organizations.project.link` (+ `team.link`)
Body `{ "teamIds": ["…"] }`. **204**, with the rules of § 2.2 (once; all or nothing; an
absent or empty list is a no-op) and the not-found of a team entry (`team.not_found`: not
an active team of this organization). A project that gets its first team has that team as
its default by inference (BR-OL-7); nothing is named. Order: 401, 404 org, 403, 403,
404 project, 404 team. **The path is the old "add a team" route and its meaning
changes** (§ 4): an old body `{ "name" }` reads as an empty list, so it answers 204 and
creates nothing (D-14); the panel is its only client.

**`POST …/projects/{projectId}/teams/{teamId}/unlink` — take a team off a project (SC-09)** · `organizations.project.link` (+ `team.link`)

```json
{ "destination": { "teamId": "0199b0a2-…" } }     another team linked to the project
{ "destination": { "teamId": null } }             «Без команды»
```

The `destination` object **and its `teamId` key must be present** (D-8); an absent
`destination`, `null`, or `{}` is 400. With nothing to move the panel still names
«Без команды». **204.** Order (BR-OL-8, BR-OL-14): 401, 404 org, 403, 403, **404 project,
404 team** (the team of the address must be an active team of the organization — a deleted,
unknown or foreign one is `team.not_found`; one not linked to this project is *not* a
not-found here), **400** `destination_required`, **404 `team.not_found` for a destination
that is not an active team of this organization** (malformed, unknown, foreign, deleted), then,
under the locks (plan § 7):

- **The pair is not linked: 204 and nothing changes**, whatever team the destination names
  (BR-OL-4: unlinking an unlinked pair succeeds).
- **409 `organizations.team.not_in_project`** — the destination is the unlinked team itself,
  or is an active team that is not linked to the project when the unlink runs.
- Otherwise, **in one operation, all or nothing** (a failure at any step leaves the link,
  the items, their places and the setting as they were):
  1. every item of this project that the team owns, **in any state, Removed included**, gets
     the destination as owning team, or none for `null`; items of **other projects** are not
     touched;
  2. their places on the unlinked team's backlog are deleted, so each one outside the Removed
     category stands **unplaced below every placed item** on the destination team's backlog of
     its level, and on no team backlog with `null`; the project backlog place is untouched;
  3. their sprint pointers into the unlinked team's sprints are cleared — **owed by
     `mod-007-iterations`**, which adds its step here;
  4. each shows the operation's time as `changedAt`;
  5. the project's default-team setting is cleared if it named the team;
  6. the link is removed.

  Project, identifier, assignee, state, estimates, parent and children, «Область» and the
  project backlog's place are untouched; no item is deleted. **Linking the team again moves
  nothing back.** A write that names the team at the same time (a save, a create, a default
  naming) commits first and is reassigned (the naming cleared) with the rest, or comes after
  and is refused `team.not_in_project`; a reorder of the team's backlog at this project's
  address commits first and has its places deleted with the rest, or comes after and is refused
  `team.not_found`; an unlink that meets the deletion of the unlinked team or of its destination
  ends as § 2.1 says (the later is refused `team.not_found`); two unlinks that name each other
  end with one applied and the other refused `team.not_in_project`.
- Errors: 400 `organizations.validation_failed` (`destination`: `organizations.team.destination_required`);
  401; 403; 404 organization, project, team; 409.

**`GET …/projects/{projectId}/teams/{teamId}/unlink-count` — what an unlink would reassign** · `organizations.project.link` (+ `team.link`)
200 `UnlinkCount`: the items the team owns in this project now, in any state. Read when the
confirmation opens. Errors: 401, 403, 404 organization, project, team — **a team that is
not linked to this project is `team.not_found`** (§ 0.3).

**`PUT …/projects/{projectId}/default-team` — name the project's default team** · `organizations.project.write` *(was `team.write`)*
Body `{ "teamId": "…" }` names it; `{ "teamId": null }`, `{}` and no body clear it (mod-017
§ 2.8, D-7 there). The team is judged as § 0.3: an active team linked to the project, else 404 /
409. Naming the current default again changes nothing. **204.** The setting is cleared by the unlink and
the deletion of the team it names, and survives a deactivated project.

**`PUT …/projects/{projectId}/description` — describe a project** · `organizations.project.write`
Body `{ "description": "…" | null }` (§ 0.2). 200 `Project`. 400 `description`; 404 organization,
project (deactivated too). Survives a deactivation and a restore; a rename changes neither the
description nor the links.

**`POST …/projects` — create a project** · `organizations.project.write` (+ link codes when `teamIds` is non-empty)
Body `{ "name", "key", "description"?, "teamIds"? }`. **Creating a project creates no team and
no team membership — the creator's included** (BR-OL-2); only its root area comes with it.
`teamIds` links each chosen team in the same operation (each recording the caller and the time);
two teams chosen name no default. 201 `Project`. Errors: 400 `name`, `key`, `description` (every
reason at once); 404 organization, **`team.not_found`** for an entry that is not an active team of
this organization; 409 as mod-002 § 2.3 (name, key). On any refusal nothing is created and the name
and key stay free. `GET …/projects/{projectId}`, `PUT …/name` and `POST …/restore` answer `Project`
with `description`.

### 2.5 Team views at a project's address (BR-OL-11; kept until `feat-040-team-surfaces-across-projects`)

`GET …/projects/{projectId}/teams/{teamId}/backlogs/{level}`, `POST …/backlogs/{level}/reorder`
and `GET …/boards/{level}` are mod-006's and mod-017 § 2.5's, with one change: **the team must
be an active team of the organization that is linked to the project**; otherwise
`team.not_found` (404), for an unlinked, a deleted, a foreign and an unknown team alike, and a
reorder for Vera (a «Читатель») is 403 first. They hold the items **the team owns in that
project**. The team's order is one per level, and **a reorder in one project's view writes the
places of that view's items only**: another project's places and relative order never move, and
the places the reorder drops for items that left the view are those of this project (OQ-3 A,
D-13). A reorder that meets an unlink or a deletion of the team ends in one order (plan § 7.3);
`409 boards.backlog.reorder_stale` is unchanged.

### 2.6 Routes that no longer exist (BR-OL-12; ADR-0028 rule 7)

The platform answers each as a path that does not exist (404, no body of ours), and none
changes anything; **there is no alias**:

`GET …/projects/{projectId}/teams/{teamId}`, `PUT …/projects/{projectId}/teams/{teamId}/name`,
`DELETE …/projects/{projectId}/teams/{teamId}`, `GET …/projects/{projectId}/teams/{teamId}/owned-items/count`,
and `GET`, `POST`, `DELETE` under `…/projects/{projectId}/teams/{teamId}/members` and
`…/member-candidates`. (`POST …/projects/{projectId}/teams` remains with another meaning, § 2.4.)

## 3. Error codes

From `contracts/errors/organizations.json` (Russian and Kazakh mandatory; the new Kazakh
texts carry `kkNeedsReview`). `docs/error-codes.md` is regenerated.

| Code | Status | Text | Use here |
|---|---|---|---|
| **new** `organizations.description_length` | 400 (field reason `description`) | «Описание должно содержать не более 2000 символов» / kk «Сипаттама 2000 таңбадан аспауы керек» | a team's or a project's description over 2 000 characters |
| **new** `organizations.team.destination_required` | 400 (field reason `destination`) | «Выберите команду или «Без команды»» / kk «Команданы немесе «Командасыз» таңдаңыз» | an unlink without `destination` |
| `organizations.team.name_taken` | 409 | **text changes** to «Команда с таким названием уже есть в организации» / kk «Ұйымда мұндай атауы бар команда бар» | an active team of the organization holds the name (ADR-0005: the code and its meaning stay) |
| `organizations.team.not_in_project` | 409 | «Команда не участвует в проекте.» | an owning team, a default team or an unlink destination that is active but not linked (§ 0.3, § 2.4) |
| `organizations.team.not_found` | 404 | «Команда не найдена.» | § 0.3, § 0.4; a team named in a body; a team under a project it is not linked to |
| `organizations.project.not_found` | 404 | «Проект не найден.» | a project in a path or in a body that is not an active project of the organization |
| `organizations.team.member_not_in_organization`, `.member_exists`, `.member_not_found` | 409, 409, 404 | as mod-016 | members, at creation and on a team's page |
| `organizations.validation_failed`, `organizations.name_length` | 400 | as before | fields |
| `organizations.access.forbidden` | 403 | «Недостаточно прав» | a role lacking the route's code or a second code (§ 0.1) |
| `boards.backlog.reorder_stale` | 409 | unchanged | a reorder that names an item that left the view |
| **deprecated** `organizations.team.last_team_cannot_be_deleted` | - | - | retired with BR-OL-10: per ADR-0005 rule 3 the entry is marked `"deprecated": true` and stays taken, its C# constant is removed (as mod-017 D-5) |

Unchanged and reused: `organizations.organization.not_found`, `workitems.*`, `boards.level.not_found`.

## 4. Summary of routes

Organization-addressed routes: **47 → 55** (eight removed, sixteen added, two kept with another
meaning). `Code` is the declared code; "+" is a second code the handler asks (§ 0.1).

| Method | Route | Code | Story | Change |
|---|---|---|---|---|
| **GET** | **`…/teams`** | `team.read` | US-1 | **new** |
| **POST** | **`…/teams`** | `team.create` (+ link codes, + `team.write`) | US-1 | **new** (replaces the project-nested add) |
| **GET** | **`…/teams/{teamId}`** | `team.read` | US-1 | **moved** from the project |
| **PUT** | **`…/teams/{teamId}/name`** | `team.write` | US-1 | **moved** |
| **PUT** | **`…/teams/{teamId}/description`** | `team.write` | US-1 | **new** |
| **DELETE** | **`…/teams/{teamId}`** | `team.delete` | US-5 | **moved**, per-project reassignment, no guard |
| **GET** | **`…/teams/{teamId}/deletion-counts`** | `team.delete` | US-5 | **new** (replaces `owned-items/count` as the deletion's count) |
| **GET** | **`…/teams/{teamId}/projects`** | `team.read` | US-1, US-3 | **new** |
| **POST** | **`…/teams/{teamId}/projects`** | `team.link` (+ `project.link`) | US-3 | **new** |
| GET, POST, DELETE | `…/teams/{teamId}/members`, `…/member-candidates`, `…/members/{userId}` | as before | US-1 | **moved** (4 routes) |
| GET | `…/projects/{projectId}/teams` | `team.read` | US-2, US-3 | **changed**: linked teams, `ProjectTeam` |
| **POST** | `…/projects/{projectId}/teams` | `project.link` (+ `team.link`) | US-3 | **changed meaning**: link teams (was: add a team) |
| **POST** | **`…/projects/{projectId}/teams/{teamId}/unlink`** | `project.link` (+ `team.link`) | US-4 | **new** |
| **GET** | **`…/projects/{projectId}/teams/{teamId}/unlink-count`** | `project.link` (+ `team.link`) | US-4 | **new** (replaces the project-nested `owned-items/count`) |
| PUT | `…/projects/{projectId}/default-team` | `project.write` | US-3 | **changed**: code, "linked" |
| **PUT** | **`…/projects/{projectId}/description`** | `project.write` | US-2 | **new** |
| POST | `…/projects` | `project.write` (+ link codes) | US-2 | **changed**: no team, `description`, `teamIds` |
| GET, PUT, POST | `…/projects/{projectId}`, `…/name`, `…/restore` | as before | US-2 | `description` in the answer |
| GET, POST | `…/projects/{projectId}/teams/{teamId}/backlogs…`, `…/boards…` | as before | US-6 | **linked** check, per-project reorder |
| ~~GET~~ | ~~`…/projects/{p}/teams/{t}`~~ | | | **removed** |
| ~~PUT~~ | ~~`…/projects/{p}/teams/{t}/name`~~ | | | **removed** |
| ~~GET~~ | ~~`…/projects/{p}/teams/{t}/owned-items/count`~~ | | | **removed** |
| ~~DELETE~~ | ~~`…/projects/{p}/teams/{t}`~~ | | | **removed** |
| ~~GET, POST, DELETE~~ | ~~`…/projects/{p}/teams/{t}/members`, `…/member-candidates`, `…/members/{u}`~~ | | | **removed** (4 routes) |

The count, as keys of the inventory (`METHOD template`): **8 out** — `GET`, `PUT …/name` and
`DELETE` of the project-nested team, its `GET …/owned-items/count`, and its four member routes;
**16 in** — nine at `…/teams` and `…/teams/{teamId}` (the list, the create, the read, the rename,
the description, the deletion, the deletion's counts, the team's projects read and link), the four
member routes at the team's address, and the project's `unlink`, `unlink-count` and
`PUT …/description`; **2 kept** at their path with another code — `GET` and `POST …/projects/{projectId}/teams`.
47 − 8 + 16 = 55, pinned by `OrganizationAddressedRoutes.All` and `OrganizationAccessInventoryTests`.

OpenAPI tags are unchanged (`Organizations`, `WorkItems`, `Boards`).

## 5. Storage and migration (BR-OL-13; ADR-0028 rules 4, 5, 12)

**One migration on top of the merged ones, editing none: `OrganizationLevelTeams`**
(plan D-1). The human waived preservation (doc-012 § Existing data) and chose to use it
in full (OQ-1 C): the upgrade drops every team. It is atomic: the drop and the reshaping
commit together or not at all.

| Step | What |
|---|---|
| drop | **every team** (any status, every organization and project), their `team_memberships`, **all** `backlog_places` and `backlog_orders` (the team places and the team order rows), and every `projects.default_team_id` |
| clear | `work_items.owning_team_id` becomes `NULL` for every item that had one; `changed_at` is **not** written |
| reshape | `teams`: `project_id` and its keys and indexes go; `organization_id` (FK, `NOT NULL`), alternate key `(organization_id, id)`, `description`, unique index `(organization_id, normalized_name)` where `status = 'Active'`; `projects`: `description`, alternate key `(organization_id, id)`; **new `project_teams`**; the item's owner key and the project's default key now reference `project_teams (project_id, team_id)`; `team_memberships` gains the key `(organization_id, team_id)` → `teams` |

`project_teams` (the link; BR-OL-4): `organization_id`, `project_id`, `team_id`, `linked_by`,
`linked_at`; primary key `(project_id, team_id)` — **one link per pair is the key**; keys
`(organization_id, project_id)` → `projects` and `(organization_id, team_id)` → `teams`, so
**the database refuses a link across organizations**; `Restrict` everywhere, so a link that
an item or a setting references is never removed under it (the unlink reassigns first);
index `(team_id, project_id)` for the team → projects direction. Details and names: plan § 4.

**Data dropped, named in the MR:** the teams (deleted ones too), their memberships, the team
backlog places and the team order rows, the projects' default-team settings, and the owning team
of every item that had one — **remapped: none**. Organizations, organization memberships and
roles, projects (deactivated too), areas, work items (their number, identifier, project,
assignee, state, estimates, parent and children, «Область», `changed_at`) and the project
backlogs' places and orders are as before. The migration logs, per kind, how many it dropped
(`RAISE NOTICE`): teams, memberships, team places, order rows, default settings, items made
«Без команды». `Down` restores the **shape** (`teams.project_id`, the old keys) and **empties
the teams again**, because a team of the organization has no one project to go back to;
it recreates no dropped row.

## 6. In-process contracts that change (ADR-0028 rules 7, 8; BR-OL-1; mod-017 S-2)

Not HTTP. All at their module's root namespace, scoped, no access check, no event.

**`IOrganizationStructure`** (`Mustask.Application.Features.Organizations`): **every by-id
read takes the organization and answers `null` for an object of another one**, so no caller
compares organizations itself.

```csharp
Task<OrganizationInfo?> FindOrganizationAsync(Guid organizationId, CancellationToken ct);              // unchanged
Task<ProjectInfo?> FindProjectAsync(Guid organizationId, Guid projectId, CancellationToken ct);        // was (projectId)
Task<TeamInfo?> FindTeamAsync(Guid organizationId, Guid teamId, CancellationToken ct);                 // was (teamId)
Task<ProjectTeamInfo?> FindProjectTeamAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken ct); // new
Task<IReadOnlyList<TeamInfo>> ListLinkedTeamsAsync(Guid organizationId, Guid projectId, CancellationToken ct);  // replaces ListActiveTeamsAsync(projectId)
Task<IReadOnlyList<LinkedProjectInfo>> ListLinkedProjectsAsync(Guid organizationId, Guid teamId, CancellationToken ct); // new

public sealed record TeamInfo(Guid Id, Guid OrganizationId, string Name, TeamStatus Status, bool IsReachable); // ProjectId removed; IsReachable = the team and its organization are active
public sealed record ProjectTeamInfo(TeamInfo Team, bool IsLinked);                    // null when the team is unknown, foreign or of another organization
public sealed record LinkedProjectInfo(Guid Id, string Name, string Key, bool IsReachable);
// ProjectInfo is unchanged: DefaultTeamId is resolved over the project's LINKED active teams (the named link, else the only link, else null)
```

`ListLinkedTeamsAsync` is the project's active linked teams by name (letter case not deciding),
then id. `ListLinkedProjectsAsync` is the team's linked projects that are reachable, by name.

**`IWorkItemTeamOwnership`** (`Mustask.Application.Features.WorkItems`) — the work items module's
write contract for an unlink and a deletion, joining the caller's transaction and locking its rows in
rule 8's order:

```csharp
Task<int> CountOwnedAsync(Guid projectId, Guid teamId, CancellationToken ct);                                  // any state
Task<IReadOnlyList<ProjectCount>> CountOwnedByProjectAsync(Guid teamId, IReadOnlyCollection<Guid> projectIds, CancellationToken ct); // one GROUP BY statement
Task LockOwnedAsync(IReadOnlyCollection<Guid> projectIds, Guid teamId, CancellationToken ct);                  // FOR UPDATE, by id
Task<int> ReassignOwnedAsync(IReadOnlyCollection<Guid> projectIds, Guid teamId, Guid? destinationTeamId, DateTimeOffset now, CancellationToken ct); // owner := destination | NULL, changed_at := now
```

`ReleaseOwnedAsync` is replaced by `ReassignOwnedAsync` (a deletion passes every linked project and `null`).
**`ITeamBacklogPlaces`** (`Mustask.Application.Features.Boards`) gains
`DropForTeamInProjectAsync(teamId, projectId)` beside `DropForItemAsync` and `DropForTeamAsync`.

## 7. Earlier contracts this one supersedes (P-10; each owner gets a note in its `status.md`)

- **mod-002** `api-contract.md` § 0.3–§ 0.4 (a team is no level under a project; the answer order of
  a team write), § 3 (all of it: the team routes and their shapes), § 6 (`team.name_taken`'s text; the
  retired guard code), § 7 (`IOrganizationStructure`); **mod-016** § 2 (the team-member routes lose the
  project), § 3.3 (`isMember` stays on the project's linked teams), § 4.1 rows 15–18 and 20 (rows 21–26
  are area routes and stay); **mod-017** BR-OT-2, -4, -11, -12, -15 and § 0.1 (the codes of the default
  naming, the deletion and its count), § 0.2 ("a team of another or of a deactivated project" is "not
  linked"), § 0.4 (the deletion's 409), § 1.3 (`Team`), §§ 2.7–2.9 (the owned-items count becomes
  per project; the default-team route is `project.write`'s), § 7 (`IOrganizationStructure`,
  `IWorkItemTeamOwnership`); **feat-039** BR-PB-9 ("once feat-028 merges it has none" is delivered);
  **mod-006** § 3.1–§ 3.3 (the team views' address check is "linked").

**mod-016 § 4.1, the rows this line changes** (row 19 went with mod-017; rows 21–26 are area routes and stay):

| Row | Was | Now |
|---|---|---|
| 15 | `GET …/projects/{p}/teams` · `team.read`, each `Team` with `isMember` | same path and code; the project's **linked** teams, shape `ProjectTeam` (§ 1.3) |
| 16 | `GET …/teams/{t}` (project-nested) · `team.read` | **moved** to `GET …/teams/{teamId}`, shape `Team` (§ 2.1) |
| 17 | `POST …/projects/{p}/teams` · `team.write` (add a team) | same path, **another meaning and code**: link teams to the project · `project.link` (+ `team.link`); the create is `POST …/teams` · `team.create` |
| 18 | `PUT …/teams/{t}/name` (project-nested) · `team.write` | **moved** to `PUT …/teams/{teamId}/name` · `team.write` |
| 20 | `DELETE …/teams/{t}` (project-nested) · `team.write` | **moved** to `DELETE …/teams/{teamId}` · `team.delete`; reassigns per project, no guard |

mod-016 § 2 (the four team-member routes) moves to `…/teams/{teamId}/members…` (§ 2.3), and its § 3.3 `isMember`
stays on the project's linked teams.
