# ADR-0036: Page the aggregate views' group reads by a cursor, and limit those reads per user and per process

- Status: proposed
- Date: 2026-11-03
- Context: `feat-048-aggregate-level-views` ([design](../features/feat-048-aggregate-level-views/design.md) § 7), owed by `feat-043`'s design § 12 and BR-NV-20 (Q-13 A). **Deviates from ADR-0014 and ADR-0015 for these reads only.** Accepted only by the human (`docs/adr/README.md`, rule 2).

## Problem

The organization's and the department's «Бэклог», «Канбан» and «Рабочие элементы» open one group per project, department or team (the user's choice), or one stream, and a group is read when it opens (up to 10 restored at once), then page by page. Two things are not settled by the page envelope of ADR-0014/0015 (`page`, `pageSize`, `total`, Gridify):

1. **Paging.** A group is a live, reorderable list. With page numbers a reorder by someone else between two pages shows a row twice or loses one, and `total` is a count the grouped read already gives. ADR-0014 itself foresees the case ("cursor-based pagination … a deviation with a new ADR").
2. **Load.** `AddNpgsqlDbContext` sets only `DisableRetry`, so Npgsql's default `Maximum Pool Size` 100 applies and nothing limits concurrent requests. The panel's cap of 4 reads in flight per tab is a promise a second tab multiplies and a script ignores; many open groups on a busy moment can take the whole pool and starve every other request.

## Options considered

### Option 1: Keep the page envelope, no limiter

- Pros: no new shape; nothing to build server-side.
- Cons: duplicate or lost rows under concurrent reorder; a `COUNT` on every page; the pool is guarded by a browser habit only.

### Option 2: Cursor on the group reads, a concurrency limiter on them

- Pros: stable pages that name a position, not a row; a read costs the page, not the count; the pool has a ceiling the server enforces; ordinary use (two tabs) is not refused.
- Cons: a second paging shape in the API (scoped, below); in-memory limiter state per process.

### Option 3: Cursor only, rely on the pool's own queueing

- Pros: smaller.
- Cons: Npgsql waits for a connection up to its timeout, so a burst turns into slow and then failing requests for everybody, with no signal to the client.

## Decision

Option 2.

1. **Scope.** The cursor and the limiter apply to **the group read and the cell read of every grouping** (including «Без группировки», read as one group) of the aggregate views and their «Показать ещё», and to nothing else. The flat «Рабочие элементы» keeps ADR-0015's envelope; the grouped count read and the keyword read are outside (the latter an accepted risk, recorded in feat-048 OQ-2).
2. **The cursor and the grouping.** The read takes `grouping` (`none | department | project | team`, the user's choice of feat-048 BR-AV-1), `group` (the group's id, or `none` for «Без команды» and «Без департамента»), `limit` (1..50, default 50) and `cursor`; the answer is `{ rows | cards, nextCursor }`, with `nextCursor` absent at the end and **no total, no page number**. The cursor is opaque to the client (base64url of a small structure), **forward only**, and names a **position in the group's order**, not a row, so it never fails because its row moved or was deleted. The order has **two keys**: `rank` (the project's rank key and id) when `grouping=project`, and `changed` (change time, number, project key; BR-NV-12's order) for the department, team, «Без группировки» stream and every other group. It carries a fingerprint of the view, level, filter parameters, **grouping and group**. A malformed cursor, or one whose fingerprint differs from the request's, is the existing validation 400 (`boards.validation_failed`) with the field `cursor` and the reason `boards.invalid_cursor`; the panel reloads the group on exactly that. A `grouping` the level does not offer is the same 400 with the field `grouping`. The cursor is not signed: it grants nothing, since the caller's checks decide what is read. Whether a change-time group meets its 2 s at 1 000 000 items is a matter of indexes, decided in feat-048 D-5, not here.
3. **The limiter.** A server-side concurrency limiter on those two reads, chained: **per user** (the Keycloak subject, across organizations, tabs and devices) **6** in flight with a wait queue of **6**; **global**, per API process, **40** in flight with a queue of **20**; a read waits at most **3 s** for a place. When the queue is full or the wait ran out the answer is **429** `http.too_many_requests` with `Retry-After: 2`. The state is per process, since the pool it protects is per process. It runs after authentication, so the subject is known; the slot is held until the read releases its connection. The built-in concurrency limiter has no wait timeout, so the wait is a linked cancellation of 3 s over the permit acquisition, with `TimeProvider` so tests drive it by a fake clock.
4. **The panel** keeps its cap of 4 per tab, retries a 429 by `Retry-After` (at most 3 tries, releasing its slot while it waits) and then offers «Повторить» (feat-048 OQ-1, OQ-4).
5. **Why these numbers.** 40 of the pool's 100 leaves 60 for everything else; 6 per user admits two tabs (8 reads) with two waiting briefly, so a 429 stays abnormal; a user cannot take more than 6 of 40, so seven users can saturate the global limit, which is the point of a queue and a 429 rather than of a bigger number.

## Consequences

- Easier: stable, cheap deep pages; a ceiling under the pool; a defined, retryable refusal.
- Harder: two paging shapes (the envelope for lists, the cursor for group reads), documented here; the limiter's state is in memory, so a second API replica has its own limits and its own pool (acceptable, the pool is per process); the BFF (ADR-0022) must pass `Retry-After`.
- Locked in: the cursor is opaque, so the client never builds one. The numbers are constants of `AggregateReadLimiter`: changing them is a code change, not an ADR amendment; changing the key (subject) or the scope (per process) is.
- Owed: the error-catalogue entries `http.too_many_requests` (429) and `boards.invalid_cursor` (400) in `contracts/errors/` and `docs/error-codes.md`; the api-contract of feat-048; a `docs/conventions/backend.md` pointer when this is accepted. ADR-0014 is already superseded; ADR-0015 gets no change (lists keep it).
