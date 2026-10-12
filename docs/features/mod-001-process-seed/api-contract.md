# API contract — mod-001-process-seed

Fixed at stage 4a (2026-10-02). From now on it changes only with a note in
[status.md](status.md) and a Log line. Once stage 6 has started, a change
goes to the orchestrator (04-backend.md § 4a.4). The plan behind it is
[plan-backend.md](plan-backend.md).

It has two consumers. The frontend uses § 1, the read the screen makes.
`mod-002` uses § 2, the in-process seed it calls when it creates an
organization. Section 3 lists what this task registers, and § 4 the
Development stand's data.

## 1. HTTP — read an organization's process definition

`GET /api/organizations/{organizationId}/process`

### 1.1 Request

- **Authentication:** `Authorization: Bearer <access token>` from realm
  `mustask`, as for every other API call. The guard checks sign-in only
  (`AuthenticatedOnly`).
- **Declared code:** `process.definition.read`, which is organization-scoped
  (BR-M2-10). The endpoint declares the code and nothing checks it. No
  token carries it, and the client must not require it (design DD-6).
  `mod-016` retrofits the check: a non-member then gets § 1.3's not-found
  answer, not a 403.
- **Path parameter `organizationId`:** an untrusted string. The server
  accepts only the canonical UUID form `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`
  (hex digits, either case) that is not all zeros. Any other value gets the
  same answer as an unknown identifier (§ 1.3). The client sends the value
  from its address URL-encoded and does not validate it (design DD-4).
- No query string and no body.

### 1.2 Response 200 — `ProcessDefinitionResponse`

`Content-Type: application/json`, camelCase.

| Field | Type | Meaning |
|---|---|---|
| `id` | uuid | The process's identity (`ProcessId`). |
| `backlogLevels` | array of `BacklogLevel` | The four levels, ordered by `rank` descending. |
| `backlogLevels[].id` | uuid | The level's stable identity (BR-M2-9). |
| `backlogLevels[].name` | string | Russian name: «Эпики», «Фичи», «Истории», «Задачи». |
| `backlogLevels[].rank` | integer | Total order of the levels; a higher rank is a higher level (BR-M2-4). |
| `backlogLevels[].kind` | string | `portfolio`, `requirement` or `task`. |
| `workItemTypes` | array of `WorkItemType` | The five types in display order (§ 1.4). The client renders them in the order received and does not sort (design R-25). |
| `workItemTypes[].id` | uuid | The type's stable identity. |
| `workItemTypes[].key` | string | `EPIC`, `FEAT`, `US`, `BUG` or `TASK`. Latin, fixed, unique in the process. |
| `workItemTypes[].name` | string | Russian name. |
| `workItemTypes[].appearance` | string | The appearance token: `epic`, `feature`, `story`, `task` or `bug`. Each type has a different one (D-6). |
| `workItemTypes[].backlogLevel` | object | The level hosting the type: `{ "id", "name" }`, equal to one entry of `backlogLevels`. |
| `workItemTypes[].states` | array of `State` | The type's states, ordered by `order` ascending. |
| `states[].id` | uuid | The state's own identity. Two equal labels on two types are two states (BR-M2-9). |
| `states[].name` | string | Russian label. |
| `states[].order` | integer | 1-based and dense; «Удалена» is always last. |
| `states[].isInitial` | boolean | `true` for exactly one state, the first (BR-M2-2). |
| `states[].category` | object | `{ "code", "label" }`. |
| `states[].category.code` | string | `Proposed`, `InProgress`, `Resolved`, `Completed` or `Removed`. It is never shown. In layout B the client uses it only to look up the category's theme colour. |
| `states[].category.label` | string | The Russian label: «Новые», «В работе», «Решённые», «Завершённые» or «Удалённые». The client shows this and holds no code-to-label map of its own (design R-26). |

The response never carries the organization's name, any audit stamp, or an
icon or colour. Every value is always present, and nothing is `null`.

Example, abbreviated to two of the five types; § 1.4 has the full content:

```json
{
  "id": "0199a3f2-5b7c-7d10-9a4e-3c2b1a0f9e8d",
  "backlogLevels": [
    { "id": "<uuid>", "name": "Эпики", "rank": 4, "kind": "portfolio" },
    { "id": "<uuid>", "name": "Фичи", "rank": 3, "kind": "portfolio" },
    { "id": "<uuid>", "name": "Истории", "rank": 2, "kind": "requirement" },
    { "id": "<uuid>", "name": "Задачи", "rank": 1, "kind": "task" }
  ],
  "workItemTypes": [
    {
      "id": "<uuid>",
      "key": "EPIC",
      "name": "Эпик",
      "appearance": "epic",
      "backlogLevel": { "id": "<uuid>", "name": "Эпики" },
      "states": [
        { "id": "<uuid>", "name": "Новая", "order": 1, "isInitial": true, "category": { "code": "Proposed", "label": "Новые" } },
        { "id": "<uuid>", "name": "Активна", "order": 2, "isInitial": false, "category": { "code": "InProgress", "label": "В работе" } },
        { "id": "<uuid>", "name": "Разрешена", "order": 3, "isInitial": false, "category": { "code": "InProgress", "label": "В работе" } },
        { "id": "<uuid>", "name": "Закрыта", "order": 4, "isInitial": false, "category": { "code": "Completed", "label": "Завершённые" } },
        { "id": "<uuid>", "name": "Удалена", "order": 5, "isInitial": false, "category": { "code": "Removed", "label": "Удалённые" } }
      ]
    },
    {
      "id": "<uuid>",
      "key": "BUG",
      "name": "Баг",
      "appearance": "bug",
      "backlogLevel": { "id": "<uuid>", "name": "Истории" },
      "states": [
        { "id": "<uuid>", "name": "Новая", "order": 1, "isInitial": true, "category": { "code": "Proposed", "label": "Новые" } },
        { "id": "<uuid>", "name": "Активна", "order": 2, "isInitial": false, "category": { "code": "InProgress", "label": "В работе" } },
        { "id": "<uuid>", "name": "Разрешена", "order": 3, "isInitial": false, "category": { "code": "Resolved", "label": "Решённые" } },
        { "id": "<uuid>", "name": "Закрыта", "order": 4, "isInitial": false, "category": { "code": "Completed", "label": "Завершённые" } },
        { "id": "<uuid>", "name": "Удалена", "order": 5, "isInitial": false, "category": { "code": "Removed", "label": "Удалённые" } }
      ]
    }
  ]
}
```

> **Superseded in part by bug-004 D-3 (2026-10-12), one name, one meaning:**
> the first type of this example, `EPIC`, now reads `"Разрешена"` with
> `"category": { "code": "Resolved", "label": "Решённые" }` — as `BUG`'s
> already does — and so do `FEAT` and `US`. The shape of the answer is
> unchanged; only the category of those three states is. The lines above are
> left as written.

### 1.3 Errors

Every error body is `application/problem+json`, with `status`, `detail`
(Russian text from the catalog), `errorCode` and `traceId`. The client
branches on `errorCode` only, never on the status alone or on the text
(frontend.md "Named values").

| Status | `errorCode` | When | Body |
|---|---|---|---|
| 404 | `organizations.organization.not_found` | `organizationId` is malformed (§ 1.1) or no process exists for it. After `mod-002`, also for an organization that does not exist or is deactivated (its context filter, mod-002 D-g). After `mod-016`, also for a non-member. All of these are the same answer, and none can be told apart from another (BR-M2-11). | `detail` «Организация не найдена.»; no part of any process |
| 401 | `http.unauthorized` | No access token, or an invalid or expired one (platform). | No part of any process |
| 500 | `http.internal_error` | The read failed for any other reason, for example the database is unreachable or its schema is not migrated. It never carries the not-found code (BR-M2-11). | No part of any process |
| 503 | `http.service_unavailable` | The realm is not ready yet (the platform gate, before authentication). | — |

The read never answers 400 or 403. A `404` with any other code, such as
`http.not_found` for a mistyped route, is a failed read, not «Организация
не найдена» (design § States).

### 1.4 Content and order guarantees

Every seeded process answers with exactly this content (doc-007 § "MVP: the
seeded process", D-6). Only the ids differ from one process to another, and
they never change for a given process.

| # | `key` | `name` | `appearance` | `backlogLevel.name` | `states`: order, name → `category.code` / `label` |
|---|---|---|---|---|---|
| 1 | EPIC | Эпик | epic | Эпики | 1 Новая → Proposed / Новые; 2 Активна → InProgress / В работе; 3 Разрешена → InProgress / В работе; 4 Закрыта → Completed / Завершённые; 5 Удалена → Removed / Удалённые |
| 2 | FEAT | Фича | feature | Фичи | as EPIC |
| 3 | US | Пользовательская история | story | Истории | as EPIC |
| 4 | BUG | Баг | bug | Истории | 1 Новая → Proposed / Новые; 2 Активна → InProgress / В работе; 3 Разрешена → Resolved / Решённые; 4 Закрыта → Completed / Завершённые; 5 Удалена → Removed / Удалённые |
| 5 | TASK | Задача | task | Задачи | 1 Новая → Proposed / Новые; 2 Активна → InProgress / В работе; 3 Закрыта → Completed / Завершённые; 4 Удалена → Removed / Удалённые |

> **Superseded in part by bug-004 D-3 (2026-10-12):** rows 1–3 read
> `3 Разрешена → Resolved / Решённые`, as row 4 does, and "as EPIC" carries
> that to `FEAT` and `US`. A state name has one category on every type of the
> process (`StateNameRule`, tested against the seed). The rows are left as
> written.

| `backlogLevels` # | `name` | `rank` | `kind` |
|---|---|---|---|
| 1 | Эпики | 4 | portfolio |
| 2 | Фичи | 3 | portfolio |
| 3 | Истории | 2 | requirement |
| 4 | Задачи | 1 | task |

- `workItemTypes` are ordered by their level's rank descending, then by the
  seed's position, which yields the order above (design DD-2).
- `states` are ordered by `order`. The first is `isInitial: true`, and
  `Removed` is last.
- The answer has five types, 24 states and four levels. An answer with no
  types is not an empty process. The client treats it as a failed read
  (design DD-5).

## 2. In-process — the seed seam (for `mod-002`)

Not HTTP. No request seeds a process, and no endpoint wraps the seed
(BR-M2-13).

```csharp
namespace Mustask.Application.Features.Process;

public interface IProcessSeed
{
    Task<ProcessId> SeedAsync(Guid organizationId, CancellationToken cancellationToken);
}

// Mustask.Domain.Process
public readonly record struct ProcessId(Guid Value);
```

- **Registration:** scoped. Resolve it from the same DI scope as the
  `IUnitOfWork` it runs under, because both must share one `MustaskDbContext`.
- **Input:** `organizationId` is an opaque key (doc-007 R-5). Any non-empty
  `Guid` is well-formed, and `mod-002` passes its new organization's id.
  `Guid.Empty` throws `ArgumentException`, and nothing is created (D-10,
  BR-M2-9).
- **Result:** the `ProcessId` of the organization's complete process. The
  caller records it as the organization's process; M2 records nothing about
  the organization (BR-M2-1). `mod-002`'s `Organization.ProcessId` property
  needs no EF configuration, because the `ProcessId` converter is
  registered model-wide.
- **Failure:** any exception. When the seed throws, nothing of a process
  remains for that identifier, and the caller receives no identity
  (BR-M2-8). The seed has already rolled back its own savepoint by the
  time the exception reaches the caller.
- **Idempotent (BR-M2-9):** a call for an identifier that already has a
  process creates nothing and returns that process's id. Two racing calls
  leave one process. The later call waits on the database until the
  earlier transaction ends. It then returns the winner's id if the winner
  committed, and creates the process itself if the winner rolled back.
- **Transaction:** the seed joins the caller's transaction and never begins,
  commits or rolls it back (D-10). It uses a savepoint inside it and
  flushes its own rows before it returns. Its rows commit or roll back with
  the caller's operation. Until the caller commits, no other session sees
  the process.

The caller's obligations:

1. Call `SeedAsync` inside `IUnitOfWork.ExecuteInTransactionAsync`. Without
   an ambient transaction it throws `InvalidOperationException` and writes
   nothing.
2. Call it before tracking any change of your own (for example, seed first,
   then create the `Organization` with the returned id). If the context has
   pending changes, it throws `InvalidOperationException` and writes
   nothing. This keeps a failure of the caller's own rows from surfacing as
   a failed seed.
3. Treat an exception as a failed seed. Let it leave
   `ExecuteInTransactionAsync`, so the organization's creation fails as a
   whole, and never make the organization visible without its process
   (BR-M2-8).
4. Seed only while creating an organization. No person-facing request seeds
   (BR-M2-13). The Development stand (§ 4) is the one other caller, until
   `mod-002` removes it (mod-002 D-h).

## 3. Registrations (no endpoint)

| What | Value | Where | Owner afterwards |
|---|---|---|---|
| Error code | `organizations.organization.not_found`: 404, no params, ru «Организация не найдена.», kk «Ұйым табылмады.» (`kkNeedsReview`) | `contracts/errors/organizations.json`, `Mustask.Domain/Organizations/OrganizationErrorCodes.cs`, `docs/error-codes.md` (generated) | `mod-002` (M1) owns the file and adds its project and team codes (D-7, mod-002 D-k); `mod-016` reuses the code for a non-member |
| Permission code | `process.definition.read`, organization-scoped | `Mustask.Domain/Identity/OrganizationPermissionCatalog.cs`, outside `PermissionCatalog.AllCodes`; not in the realm import, no role or group grants it, and no token carries it | `mod-016` folds it into its scoped catalog (D-8) |

The frontend keeps the error code as a constant in `shared/api`, matching
this file one to one. It keeps the permission code as a named constant of
the slice that no guard reads (design DD-6).

## 4. Development stand data

The API started in the Development environment seeds one process at start,
through § 2, for the fixed demonstration organization id
**`d0000000-0000-4000-8000-000000000001`** (D-4). The screen opens it at
`/organizations/d0000000-0000-4000-8000-000000000001/settings/process`.
`src/backend/AGENTS.md` documents the same id. No other environment seeds
at start.

If the database lacks a migration this build ships, the stand skips the
seed and logs a warning (event `DemonstrationSeedSkipped`). A read for the
id then fails with 500, or answers 404 once the schema is migrated and
before the next start. If the seed itself fails, the stand logs the error
(event `DemonstrationSeedFailed`) and starts without the process, and the
next start tries again. When the process is in place, whether created now or
already there, it logs an Information event, `DemonstrationSeedReady`. The
three event names are the only log surface of the stand's seed, and
`src/backend/AGENTS.md` names them for a developer reading the log.
`mod-002` removes the stand's seed when real organization creation lands
(mod-002 D-h).
