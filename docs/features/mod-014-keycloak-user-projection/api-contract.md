# API contract — mod-014-keycloak-user-projection

Fixed at stage 4a, 2026-10-02. From here on it changes only with a note in
`status.md` and a Log line (`04-backend.md` § 4a.4); the read model of Part A
is D-2 (3)'s fixed interface — `mod-016-organization-access` and
`mod-003-work-item-core` build on it without reopening this task.

The decisions behind it are
[ADR-0025](../../adr/0025-keycloak-user-projection.md); the requirements are
BR-UP-11 and BR-UP-14..17 for Part A, BR-UP-5..10 for Part B
([spec.md](spec.md)).

There are two surfaces and no third:

| Part | Surface | Who calls it |
|---|---|---|
| A | **The read model** — an in-process interface, `IUserDirectory` | slices of other MUSTASK modules (`mod-016`, `mod-003`) |
| B | **The push channel** — four HTTP routes under `/api/user-directory/feed` | the Keycloak provider `mustask-user-projection` only |

No person calls either: this task exposes no read to people and registers no
permission code (BR-UP-12); each consumer offers what it reads under its own
code.

---

## Part A — The read model (`IUserDirectory`)

### A.1 Types

Namespace `Mustask.Application.Abstractions.UserDirectory`
(`src/backend/Mustask.Application/Abstractions/UserDirectory/`). Registered
**scoped** in DI (it uses the request's `DbContext`); not safe for concurrent
calls within one scope, like every EF-backed service here.

```csharp
public interface IUserDirectory
{
    Task<UserSearchResult> SearchAsync(UserSearch search, CancellationToken cancellationToken);

    Task<UserLookupResult> LookupAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken);
}

/// BR-UP-14. AmongIds = null: the whole projection.
public sealed record UserSearch(
    string Text,
    bool IncludeInactive = false,
    IReadOnlyCollection<string>? AmongIds = null);

/// BR-UP-11: exactly these four values — nothing else about a user is offered.
public sealed record DirectoryUser(
    string Id,
    string DisplayName,
    string Email,
    bool IsActive);

/// BR-UP-14/15: at most 20 users, in DirectoryUserOrder; MoreMatched when more than 20 matched.
public sealed record UserSearchResult(IReadOnlyList<DirectoryUser> Users, bool MoreMatched)
{
    /// BR-UP-17: merges the answers of one search split into batches of ids.
    public static UserSearchResult Merge(IEnumerable<UserSearchResult> batches);
}

/// BR-UP-16/17: every distinct requested id is in exactly one of the two.
public sealed record UserLookupResult(
    IReadOnlyDictionary<string, DirectoryUser> Known,
    IReadOnlyCollection<string> Unknown);

/// BR-UP-15: the order of a search answer, offered for merging.
public static class DirectoryUserOrder
{
    public static IComparer<DirectoryUser> Comparer { get; }
}
```

### A.2 The values of a user

| Value | Meaning |
|---|---|
| `Id` | the Keycloak user id — the `sub` of that user's tokens; opaque text, never reused by Keycloak |
| `DisplayName` | first name and last name, each trimmed, empty ones dropped, joined by one space; when both are empty, the username (BR-UP-3). Never empty for a held user |
| `Email` | the e-mail as Keycloak holds it, letter case kept; `""` when the user has none. Not unique across the projection |
| `IsActive` | `true` exactly while the account is enabled and neither deleted nor absent (BR-UP-5). `false` covers disabled, deleted and absent alike — a consumer cannot and need not tell them apart |

After a deletion or an absence the user keeps the name and e-mail last
received, indefinitely (BR-UP-5). A user Keycloak has not pushed yet is not in
the projection at all: a lookup answers it in `Unknown` (BR-UP-17), a search
never finds it — even while that user is signed in (BR-UP-1).

### A.3 `SearchAsync`

| Rule | Behaviour |
|---|---|
| Text | trimmed of white space. Fewer than **2** characters (Unicode code points) after trimming: answers `([], false)` without reading anything — never the whole directory. More than **100**: refused, `ValidationException` (A.6). Exactly 100: accepted |
| Matching | the text is a substring of the display name or of the e-mail, compared after the same normalisation on both sides: Unicode NFC, lower case, `ё` → `е`, control characters removed, a Unicode noncharacter read as U+FFFD (the projection holds none — B.3 — and the provider writes U+FFFD in its place, so a text copied from Keycloak finds the user; a search text holding one is answered, never an error). The username is not searched; a user with no first or last name is found through the display name that falls back to it |
| `IncludeInactive` | `false` (default): only active users. `true`: inactive users too, `IsActive = false` |
| `AmongIds` | `null`: the whole projection. A collection: only those ids — at most **500** entries, a repeated id counting toward the 500 and changing nothing else; more than 500: refused (A.6), nothing answered; empty: answers `([], false)`; an id never held matches nobody. Ids are not checked for shape |
| Size | at most **20** users. `MoreMatched` is `true` exactly when more than 20 matched |
| Order | `DirectoryUserOrder` (A.5) |
| Null | `search` or `search.Text` null → `ArgumentNullException` (a programming error, not a refusal) |

### A.4 `LookupAsync`

| Rule | Behaviour |
|---|---|
| Size | at most **500** entries, a repeated id counting toward the 500; more: refused (A.6), nothing answered partially. Empty: `({}, [])`, not an error |
| Answer | each distinct id once — in `Known` with its user (active or inactive) or in `Unknown`. A lookup never fails because of an unknown id. The order of `Unknown` carries no meaning; consumers match by id |
| Ids | opaque text, compared ordinally (case-sensitive), not checked for shape; `""` or an over-long id is simply unknown |
| Null | `ids` null → `ArgumentNullException` |

### A.5 Order and merging

`DirectoryUserOrder.Comparer` orders by `DisplayName` under the platform ICU
collation for culture `ru` at primary strength — letter case and diacritics
ignored (so `ё` equals `е`), punctuation, symbols and spaces significant and
ranked before digits, digits before Cyrillic (Russian alphabet order),
Cyrillic before Latin — and, between display names that compare equal, by
`Id` in Unicode code-point order. It is total: a repeated search answers the
same users in the same order. A search answer is already in this order: the
projection sorts by a key computed with the same collation.

**Above 500 ids** (BR-UP-17) a consumer splits:

- a lookup into batches of at most 500 ids; the answers add up — the union of
  `Known` and of `Unknown`;
- a restricted search into one `SearchAsync` per batch, the same text and
  `IncludeInactive`, and `UserSearchResult.Merge` over the answers: users
  de-duplicated by id, ordered by `DirectoryUserOrder.Comparer`, the first 20
  kept; `MoreMatched` is `true` if any batch said so or more than 20 came back.

### A.6 Refusals

Both are `Mustask.Shared.Exceptions.ValidationException` (HTTP 400 if a
consumer lets it reach its own endpoint) with the top-level code
`user_directory.validation_failed` and one field entry:

| Call | Field key | Reason code | Params |
|---|---|---|---|
| `SearchAsync`, text over 100 characters | `text` | `user_directory.value_too_long` | `limit`: 100 |
| `SearchAsync`, `AmongIds` over 500 entries | `amongIds` | `user_directory.too_many_ids` | `limit`: 500 |
| `LookupAsync`, over 500 entries | `ids` | `user_directory.too_many_ids` | `limit`: 500 |

A consumer that takes these values from its own request should validate them
at its own boundary, under its own field names and codes; these refusals are
the read model's backstop. Texts: Part C.

### A.7 Guarantees

- **No call to Keycloak**, ever: both reads answer from MUSTASK's database, so
  they work while Keycloak is unreachable (doc-006 NFR Member search).
- **As last received:** the projection lags Keycloak by the freshness bounds
  of the spec (10 s with both sides up; 5 minutes after MUSTASK returns; one
  reconciliation period for a lost change) and is never refreshed by a read.
- **Time:** at 10 000 users, a search and a 500-id lookup each answer within
  500 ms, also while a reconciliation is being applied.
- **Read-only:** nothing in this interface writes; the projection has no write
  path for MUSTASK code (BR-UP-1).
- **The status authorizes nothing** (BR-UP-12): `IsActive = false` is
  information for display and for the consumer's own rules; no part of
  MUSTASK's authentication consults it.

### A.8 How the consumers use it

| Consumer | Typical use |
|---|---|
| `mod-016-organization-access` | member search — `SearchAsync` with the person's text, defaults; members of an organization — `AmongIds` (split and merge above 500); the «неактивен» marking and the active-administrator count of BR-M1-4 — `LookupAsync`, deciding itself how an `Unknown` administrator counts |
| `mod-003-work-item-core` | the assignee picker — `SearchAsync`; rendering an assignee and its «неактивен» marking — `LookupAsync` |

Consumers inject `IUserDirectory` into their slice handlers; they never read
the projection's tables, its EF entities or its store interfaces.

---

## Part B — The push channel

### B.1 Common rules

| Item | Rule |
|---|---|
| Base path | `/api/user-directory/feed` on `Mustask.Api` |
| Authorization category | `.Public(...)` with respect to the Bearer scheme (ADR-0022's precedent); authenticated by the signature below. Outside the realm-readiness gate. Not in the OpenAPI document |
| Transport | HTTP on the stand; HTTPS everywhere else (the provider refuses `http` unless its option `require-https` is `false`) |
| Request body | `application/json`, UTF-8, at most **4 MiB**; property names camelCase; unknown properties ignored |
| Success body | `application/json` (B.4) |
| Error body | `application/problem+json` with `errorCode` and, for validation, `errors` (ADR-0004/0006); codes in Part C |
| Size of a batch | at most **500** entries per request |
| Credential | a shared secret, at least 32 characters: MUSTASK `UserDirectory:Push:Secret` (required at startup) and optionally `UserDirectory:Push:PreviousSecret` (accepted too, for rotation); the provider's SPI option `push-secret`. Environment variables / user secrets only |

### B.2 The signature

Every request carries two headers (names case-insensitive):

| Header | Value |
|---|---|
| `Mustask-Push-Timestamp` | the request time, Unix seconds, a decimal integer |
| `Mustask-Push-Signature` | `v1=` followed by 64 lowercase hex digits |

The signature is HMAC-SHA256, keyed by the secret's UTF-8 bytes, over the
UTF-8 bytes of the canonical string — five lines joined by `\n` (LF), no
trailing newline:

```
v1
<the Mustask-Push-Timestamp header value, exactly as sent>
<the HTTP method, upper case>
<target: the request path below "/api/user-directory/feed/", exactly as sent>
<lowercase hex SHA-256 of the raw body bytes>
```

MUSTASK accepts a request when the timestamp is within **300 s** of its own
clock (either direction) and the signature matches under `Secret` or
`PreviousSecret` (constant-time comparison). Otherwise — a header missing or
malformed, a wrong signature, a stale timestamp, a Bearer token instead — it
answers **401 `user_directory.push_unauthorized`**, says nothing about which
check failed, changes nothing, and logs the refusal with its reason and the
route, never the body, a name, an e-mail or the secret. The checks that need no
body — both headers present and well-formed, the timestamp inside the window —
come first, so such a request is refused 401 before its body is read, whatever
its size. Otherwise the body is read (bounded by 4 MiB) and verified before it
is parsed: a body over 4 MiB that arrives with well-formed headers and a
timestamp inside the window is 400 `user_directory.push_malformed`, its
signature not yet checked.

Worked example (also the first entry of the shared test vectors in
`deploy/keycloak/providers/user-projection/src/test/resources/push-signature-vectors.json`,
read by both the provider's tests and the C# tests; the secret is an example,
never a real one):

| Input | Value |
|---|---|
| secret | `example-only-not-a-real-secret-0123456789` |
| timestamp | `1790000000` |
| method, target | `POST`, `changes` |
| body (one line, 207 bytes) | `{"changes":[{"id":"7c9e6679-7425-40de-944b-e07fc1f90ae7","moment":1790000000000000,"username":"a.smirnova","firstName":"Анна","lastName":"Смирнова","email":"anna.s@example.test","enabled":true}]}` |
| body SHA-256 | `6757a7b88fb4a27ecfdc1a054e876d717d467db92bcba6f5617e5adb9628a4e9` |
| `Mustask-Push-Signature` | `v1=d4f1bd083b09951e40e80e0d65f0b7348dff08c2e89a23022e27cb9606155dce` |

### B.3 Operations

| # | Method and route | Target in the signature | Purpose |
|---|---|---|---|
| B-1 | `POST /api/user-directory/feed/changes` | `changes` | apply changes captured from Keycloak events |
| B-2 | `PUT /api/user-directory/feed/snapshots/{snapshotId}` | `snapshots/{snapshotId}` | begin a reconciliation snapshot |
| B-3 | `PUT /api/user-directory/feed/snapshots/{snapshotId}/pages/{pageNumber}` | `snapshots/{snapshotId}/pages/{pageNumber}` | apply one page of a snapshot |
| B-4 | `PUT /api/user-directory/feed/snapshots/{snapshotId}/completion` | `snapshots/{snapshotId}/completion` | complete a snapshot: prove it whole, mark absences |

`{snapshotId}` is a UUID (route constraint `guid`; the provider sends it in
lowercase), `{pageNumber}` an integer (route constraint `int`). A path that
fails a constraint matches no route: 404 `http.not_found`.

#### The user entry

The element of `changes` (B-1) and of `entries` (B-3): one user's whole
projected state as of a moment, or that user's deletion.

| Field | Type | Rule |
|---|---|---|
| `id` | string | required, 1–255 characters |
| `moment` | integer (int64) | required; the ordering marker — microseconds since the Unix epoch on the provider's clock, strictly increasing within one provider process; > 0 and at most one hour ahead of MUSTASK's clock |
| `username` | string | required, 1–255 characters |
| `firstName` | string or null | at most 255 characters; null means empty |
| `lastName` | string or null | at most 255 characters; null means empty |
| `email` | string or null | at most 255 characters; null means none |
| `enabled` | boolean | required unless `deleted` is true; ignored when it is |
| `deleted` | boolean | optional, default false. True only in B-1: a reported deletion, carrying the last state Keycloak held (so `username` is still required). In B-3 it is invalid |

None of the five text fields (`id`, `username`, `firstName`, `lastName`, `email`)
may contain U+0000 — PostgreSQL text cannot hold it — or a Unicode noncharacter:
U+FDD0..U+FDEF and the last two code points of every plane (U+FFFE, U+FFFF,
U+1FFFE, … U+10FFFF), sixty-six values the standard reserves for a program's own
use and no name or e-mail needs (.NET's normaliser throws on U+FFFE, the one of
them that made MUSTASK's derivation of its search columns fail). A text that
does is refused `user_directory.value_invalid` (field key the path of that
field); when the text is also over the limit it is `user_directory.value_too_long`
instead — the length is checked first, and a field carries one code. The
provider drops U+0000 and replaces an unpaired UTF-16 surrogate and a
noncharacter with U+FFFD before it sends, so it never sends any of them (the
rule "the provider never sends what MUSTASK refuses"); a surrogate that does
arrive is not a text MUSTASK's reader can take and is 400
`user_directory.push_malformed`.

Within one B-1 request an id may appear more than once; each entry is applied
and the newest moment wins. Within one B-3 page an id appears at most once.

#### B-1 `POST .../changes`

Request: `{ "changes": [ <user entry>, … ] }` — 1 to 500 entries.

Response 200: `FeedCounts` (B.4). Errors: 400, 401.

#### B-2 `PUT .../snapshots/{snapshotId}`

Request: `{ "enumerationStart": <int64> }` — the marker taken before the
provider listed the realm's users (> 0, at most one hour ahead).

| Situation | Answer |
|---|---|
| a new id, `enumerationStart` greater than that of every snapshot begun so far | 200 `SnapshotState` with `state: "open"`; every open snapshot with a smaller `enumerationStart` becomes `superseded` |
| the same id again with the same `enumerationStart` | 200 `SnapshotState`, its current state — idempotent |
| the same id with another `enumerationStart`, or a new id not newer than the newest begun | 409 `user_directory.snapshot_conflict`, nothing changes |

#### B-3 `PUT .../snapshots/{snapshotId}/pages/{pageNumber}`

Request: `{ "entries": [ <user entry>, … ] }` — 1 to 500 entries,
`deleted` false or absent. Pages are numbered from 0.

| Situation | Answer |
|---|---|
| an open snapshot, a page number not applied before | entries applied as pushes, each user row stamped with this snapshot; 200 `FeedCounts` of the page |
| the same page number again | 200 with the counts stored the first time; not applied again |
| unknown snapshot | 404 `user_directory.snapshot_not_found` |
| snapshot completed or superseded | 409 `user_directory.snapshot_not_open` |
| `pageNumber` negative | 400 `user_directory.validation_failed`, field `pageNumber`, `user_directory.value_invalid` |

#### B-4 `PUT .../snapshots/{snapshotId}/completion`

Request: `{ "pageCount": <int>, "entryCount": <int> }` — both ≥ 0; a realm
with no users completes with `0, 0`.

| Situation | Answer |
|---|---|
| open, pages `0..pageCount-1` all applied, no other page applied, and exactly `entryCount` distinct users stamped with this snapshot | absences marked (below); state `completed`; 200 `SnapshotCounts` |
| already completed | 200 with the stored counts — idempotent, nothing marked again |
| superseded | 409 `user_directory.snapshot_not_open` |
| counts do not match what arrived | 409 `user_directory.snapshot_incomplete` (`expected`, `received` entries); stays open, nobody marked |
| unknown snapshot | 404 `user_directory.snapshot_not_found` |

### B.4 Response bodies

`FeedCounts` (B-1, B-3):

```json
{ "received": 3, "added": 1, "changed": 1, "unchanged": 1, "heldAsDeleted": 0 }
```

| Count | An entry counts here when |
|---|---|
| `received` | always — the entries in the request |
| `added` | its id was not held and now is |
| `changed` | a projected value (username, first or last name, e-mail) or the status (enabled, deleted, absent) differs from the state held — not merely a newer moment |
| `unchanged` | a duplicate, an older moment, or a newer moment with the same values and status |
| `heldAsDeleted` | a state entry for an id held as deleted, ignored (BR-UP-5; the accepted risk of a Keycloak restored from an old backup) |

`added + changed + unchanged + heldAsDeleted = received`.

`SnapshotState` (B-2):

```json
{ "snapshotId": "3f2a…", "enumerationStart": 1790000000000000, "state": "open" }
```

`state` is `open`, `completed` or `superseded`.

`SnapshotCounts` (B-4): the sums of the snapshot's pages, plus the outcome of
completion.

```json
{ "received": 200, "added": 0, "changed": 1, "unchanged": 199, "heldAsDeleted": 0,
  "markedAbsent": 0, "pageCount": 1, "entryCount": 200 }
```

`markedAbsent` counts the users newly marked absent by this completion.

### B.5 Effects (normative)

For one id, MUSTASK holds at most one record: the projected fields, the status
(present, absent or deleted, plus enabled) and the moment of the state held.

| Incoming | Held: nothing | Held: present or absent | Held: deleted |
|---|---|---|---|
| state entry, moment > held | added | replaced; an absent user becomes present (BR-UP-8) | ignored — `heldAsDeleted` |
| state entry, moment ≤ held | — | ignored — `unchanged` | ignored — `heldAsDeleted` |
| deletion entry (B-1) | added, deleted, with the carried last state | deleted for good; name and e-mail kept — the carried ones if its moment is newer than the held one | `unchanged` |

- A snapshot entry is a state entry, and stores its newer moment even when its
  values are unchanged.
- **Absence**, at a successful B-4 only: every held user that is present (not
  deleted, not already absent), not stamped with this snapshot, and whose held
  moment is older than the snapshot's `enumerationStart`, becomes absent with
  moment `enumerationStart` — inactive, name and e-mail kept.
- **Concurrency:** requests touching the same id are applied one after the
  other in MUSTASK (ADR-0025 § 6); the outcome is the same as any sequential
  order of the same entries — the newest moment wins, a deletion wins over any
  state.
- **Replay:** within the 300 s window a replayed request is harmless by the
  rules above; outside it, refused (401).

### B.6 Observability

MUSTASK logs, never with a name, an e-mail or the secret, and naming users by
id only: each refusal (reason, route); each B-1 with its counts, the number of
deletions it applied apart from them, and — when there were any — the ids of the
users whose deletion it applied (a deletion is final, BR-UP-5; an id already held
as deleted is not among them); the configured `UserDirectory:Push:PreviousSecret`
at startup, and a request accepted under it at most once an hour, by key and
never by value; each
completed snapshot with `received`, `added`, `changed`, `markedAbsent` and
`heldAsDeleted` (the NFR's "deleted") — the provider logs the same counts
from the B-4 answer on Keycloak's side.

---

## Part C — Error codes

Module `user_directory`; registry rules ADR-0005; texts in
`contracts/errors/user_directory.json` (Russian and Kazakh, the Kazakh marked
for review), declared in `UserDirectoryErrorCodes`
(`Mustask.Domain/UserDirectory`).

| Code | HTTP | Params | Where | Text (ru) |
|---|---|---|---|---|
| `user_directory.validation_failed` | 400 | — | top-level code of every validation refusal, A.6 and B | Проверьте правильность переданных данных. |
| `user_directory.value_required` | 400 | — | field reason: a required value missing or empty | Обязательное значение. |
| `user_directory.value_too_long` | 400 | `limit` number | field reason: over a length limit | Превышена максимальная длина: {limit} символов. |
| `user_directory.value_invalid` | 400 | — | field reason: a moment out of range, `deleted` in a snapshot page, an id repeated in a page, a negative page number or count, U+0000 or a Unicode noncharacter (B.3) in a text field (`id`, `username`, `firstName`, `lastName`, `email`) — `value_too_long` instead when the text is also over the limit | Недопустимое значение. |
| `user_directory.too_many_entries` | 400 | `limit` number | field reason (`changes`, `entries`): over 500 entries | Слишком много записей в одном запросе: не больше {limit}. |
| `user_directory.too_many_ids` | 400 | `limit` number | field reason (`amongIds`, `ids`): over 500 ids | Слишком много идентификаторов в одном запросе: не больше {limit}. |
| `user_directory.push_malformed` | 400 | — | B: the body is not JSON of the declared shape, or over 4 MiB | Запрос канала пользователей не разобран: тело повреждено или слишком велико. |
| `user_directory.push_unauthorized` | 401 | — | B: the signature or timestamp does not hold | Запрос канала пользователей отклонён: подпись отсутствует, неверна или устарела. |
| `user_directory.snapshot_not_found` | 404 | — | B-3, B-4 | Сверка пользователей не найдена. |
| `user_directory.snapshot_not_open` | 409 | — | B-3, B-4: completed or superseded | Сверка пользователей уже завершена или заменена более новой. |
| `user_directory.snapshot_conflict` | 409 | — | B-2 | Сверка с этим идентификатором уже начата с другим моментом или устарела. |
| `user_directory.snapshot_incomplete` | 409 | `expected` number, `received` number | B-4 | Сверка пользователей не завершена: получено записей — {received} из {expected}. |

Field keys of B validation errors are JSON paths into the request:
`changes`, `changes[3].email`, `entries[0].id`, `enumerationStart`,
`pageNumber`, `pageCount`, `entryCount`. Field keys of A.6 are `text`,
`amongIds`, `ids`.

Platform codes may also appear unchanged: `http.not_found` (a route
constraint not met), `http.method_not_allowed`, `http.internal_error`.

---

## Part D — Named values

| Value | Number | Owner | Source |
|---|---|---|---|
| shortest search text | 2 characters | `UserDirectoryLimits` (Domain) | BR-UP-14, D-8 |
| longest search text | 100 characters | `UserDirectoryLimits` | BR-UP-14 |
| search answer size | 20 users | `UserDirectoryLimits` | BR-UP-14, D-8 |
| ids per search restriction or lookup | 500 | `UserDirectoryLimits` | BR-UP-14, BR-UP-16, D-8 |
| entries per push request | 500 | `UserDirectoryLimits` | ADR-0025 § 4, § 5 |
| length of id, username, names, e-mail | 255 characters | `UserDirectoryLimits` | Keycloak's own column sizes |
| push body | 4 MiB | push binder (Api) | ADR-0025 § 3 |
| timestamp window | 300 s | push verifier (Api) | ADR-0025 § 3 |
| moment ahead of MUSTASK's clock | 1 hour | `UserDirectoryLimits` | this contract |
| secret length | ≥ 32 characters | push options (Api), provider options | ADR-0025 § 3 |
| reconciliation period | 60 minutes, configurable (≥ 1) | the provider's option `reconcile-period-minutes` | BR-UP-8, D-8 |

The limits are part of this contract: changing one is a contract change.

---

## Part E — Stability

- **Fixed for consumers (D-2 (3)):** the types and members of A.1, the four
  values, the rules of A.3–A.6 and the codes they throw. An addition (a new
  optional member, a new read) is a new task with a note here; a removal or a
  change of meaning is not made.
- **Fixed between the provider and MUSTASK:** routes, headers, the canonical
  string, the bodies and the codes of Part B. Both sides ship from this
  repository together; a change touches both in one task, with a note here.
- **Not offered:** any HTTP read, any event or notification to consumers,
  usernames, roles, groups, memberships, timestamps or any other user
  attribute (BR-UP-2), a way to tell disabled, deleted and absent apart, and
  any write.
