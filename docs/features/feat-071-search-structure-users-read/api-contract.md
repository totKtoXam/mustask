# feat-071 — API contract: search for structure and users

Fixed at gate-1 with [plan-backend.md](plan-backend.md). After the frontend stage starts a change goes through the orchestrator; before it, with a note in `status.md` and a Log line. Rules: [feat-065 spec](../feat-065-global-search/spec.md) BR-GS-3..-10, -17; [ADR-0044](../../adr/0044-trigram-quick-search-full-text-page-and-search-limiter.md).

## POST /api/organizations/{organizationId}/search

A read sent as POST so the typed text is in the body, never in the request line the API logs (BR-GS-10, design § 3.4). It changes nothing; it is not idempotency-keyed and not audited. Auth: any signed-in member of the organization; what is returned is filtered by sight (feat-041, ADR-0040). There is no other permission; the users group needs `organizations.membership.read` and is simply absent without it.

Request body (`application/json`):

```json
{ "query": "ал", "expand": null }
```

| Field | Type | Rule |
|---|---|---|
| `query` | string | required; trimmed by the server; 2..100 characters after trimming (Unicode code points) |
| `expand` | string or null | null: the first read, up to 5 rows in every group. One of `projects`, `teams`, `departments`, `areas`, `users`: only that group, up to 20 rows, `hasMore` always false (no further paging, BR-GS-9). Any other value: 400 |

Response 200:

```json
{
  "groups": [
    {
      "kind": "projects",
      "hasMore": true,
      "rows": [
        { "id": "…", "name": "Альфа", "key": "ALP", "departmentName": "Логистика",
          "color": "teal", "similar": false }
      ]
    }
  ]
}
```

- `groups`: only non-empty groups, ordered by the best row's tier, ties in the order `projects`, `teams`, `departments`, `areas`, `users` (BR-GS-8). Empty array when nothing is visible and matches. (`workitems` is added by `feat-NNN-search-work-items-read` as a new group kind before `projects` in the tie order; nothing else here changes.)
- `hasMore`: true when more than the group's limit of visible rows match (5, or 20 on expansion; always false on expansion). It is computed from visible rows only. **No total, count or "hidden" field exists anywhere in the response** (BR-GS-5).
- Rows are ordered by tier, then name (Russian collation), then id. A row with `similar: true` is a typo-tolerant match (BR-GS-17), always after every `similar: false` row of its group; the client shows it under «Похожие» and does not emphasise it. Emphasis for the other rows is done by the client from the typed text.
- Row fields (one flat DTO; absent fields are omitted, not null, except `color`):

| Kind | Fields |
|---|---|
| `projects` | `id`, `name`, `key`, `departmentId`, `departmentName`, `color`, `similar` |
| `teams` | `id`, `name`, `departmentId`, `departmentName`, `color`, `similar` |
| `departments` | `id`, `name`, `color`, `similar` |
| `areas` (sub-areas) | `id`, `name`, `projectId`, `projectName`, `departmentName`, `similar` |
| `users` | `id` (the user's subject id), `displayName`, `email`, `inactive` (true for a disabled account, shown «(неактивен)»), `similar` |

`color` is the object's palette key (doc-016 BR-TD-20) or null. The server returns ids and names, never URLs; the client builds the addresses (ADR-0032). `email` and the whole users group exist only for a caller holding `organizations.membership.read`.

Sight and hiding: a project, team, department or sub-area the caller does not see, a deactivated or deleted one, and the sub-areas of a deactivated project are in no group; a former member is not found. The body for a hidden name and for a name that exists nowhere is identical (hidden ≡ nonexistent).

Errors (envelope ADR-0007; codes in `contracts/errors/search.json`):

| Status | Code | When |
|---|---|---|
| 400 | `search.validation_failed` with field `query` and reason `search.query_too_short` | fewer than 2 characters after trimming (also empty or missing) |
| 400 | `search.validation_failed` with field `query` and reason `search.query_too_long` | more than 100 characters |
| 400 | `search.validation_failed` with field `expand` and reason `search.invalid_group` | `expand` is not one of the five kinds |
| 401 | (the platform's) | not signed in |
| 404 | `organizations.organization.not_found` | not an active member of the organization (an instance administrator without membership included); same body as a nonexistent organization |
| 429 | `http.too_many_requests` | the limiter refused the read; header `Retry-After: 1` |

The error body never echoes `query`.

### 429 behaviour

A separate per-user and per-process concurrency limiter (not ADR-0036's group-read limiter): per user (Keycloak subject, across organizations and tabs) 3 reads in flight with a wait queue of 3; per API process 24 in flight, queue 12; a read waits at most 1 s for a place; a full queue or an expired wait answers 429 with `Retry-After: 1` and an empty body that the status-code handler turns into `http.too_many_requests`. Clients retry by `Retry-After`, up to 3 tries (BR-GS-15, design § 3.4), then show the failure message. The cancelled request releases its place.

## Reserved, not part of this contract

The all-organizations read (US-8, BR-GS-19) is a separate route without an organization in the path, `POST /api/search`, with the same request and response plus an `organizationName` on every row; it belongs to `feat-NNN-search-all-organizations`. The read model of this task already takes a list of organizations so that route is no new read.
