# Plan — backend (mod-014-keycloak-user-projection)

Stories: **US-1..US-6**, all of them — the task is backend-only (D-1).
The *why* is [ADR-0025](../../adr/0025-keycloak-user-projection.md)
(proposed; answers § "ADR expected" 1–9 of [spec.md](spec.md)); the interface
is [api-contract.md](api-contract.md) (fixed); this file is the *what* and the
*order*. ADRs the implementation relies on: 0001 (Aspire), 0002 (explicit
migrations), 0004/0005/0006 (errors and the catalog), 0009 (EF Core), 0010
(xmin), 0011 (read committed, pessimistic locks), 0012 (no EF retry), 0018
(Keycloak), 0022 (`.Public` routes with their own scheme), 0023 (stand-only
automation in `Mustask.AppHost`, its guards), 0025.

## 1. Identifiers this plan fixes

| Name | Value |
|---|---|
| Provider id (Keycloak event listener) | `mustask-user-projection` |
| Provider sources | `deploy/keycloak/providers/user-projection/`, Java package `mustask.keycloak.userprojection` |
| Provider SPI options | `endpoint` (required), `push-secret` (required, ≥ 32), `require-https` (default `true`), `reconcile-period-minutes` (default `60`, ≥ 1) |
| Their environment variables on the stand | `KC_SPI_EVENTS_LISTENER__MUSTASK_USER_PROJECTION__ENDPOINT`, `…__PUSH_SECRET`, `…__REQUIRE_HTTPS`, `…__RECONCILE_PERIOD_MINUTES` — the Keycloak 26.4 spelling is confirmed on the first live start (§ 13), set in one place in `AppHost.cs` |
| Keycloak image build | `deploy/keycloak/Dockerfile`, context `deploy/keycloak`, build args `KEYCLOAK_IMAGE`, `KEYCLOAK_TAG` (default `26.4`), stages `keycloak` → `provider` → final |
| Mustask.Api configuration | `UserDirectory:Push:Secret` (required at start, ≥ 32), `UserDirectory:Push:PreviousSecret` (optional) |
| AppHost configuration | `Keycloak:Image`, `Keycloak:Tag` (unchanged keys, now build args); `Keycloak:UserProjection:ReconcilePeriodMinutes` (optional, test use) |
| AppHost secret parameter | `user-projection-push-secret` — generated per AppHost process, never human-set |
| Push routes | `/api/user-directory/feed/changes`, `…/snapshots/{snapshotId}`, `…/snapshots/{snapshotId}/pages/{pageNumber}`, `…/snapshots/{snapshotId}/completion` |
| Error module | `user_directory` (`contracts/errors/user_directory.json`, `UserDirectoryErrorCodes`) |
| Tables | `directory_users`, `directory_snapshots`, `directory_snapshot_pages` |
| Migration | `AddUserDirectory` |
| Keycloak store volume | container mode `<app>-<hash>-keycloak-data` (Aspire's generator, as `-postgres-data`); external mode `mustask-keycloak-<16 hex>` (§ 9.2) |
| Drift health check key | `keycloak-realm-drift` |
| Reset | `scripts/stand-reset.sh`, `scripts/stand-reset.ps1`; AppHost flag `--stand-reset` (`--yes` skips the prompt) |
| verify.sh bucket | `provider` |
| Test trait for provider-related .NET tests | `Category=KeycloakProvider`; live ones also need `MUSTASK_KEYCLOAK_PROVIDER_TESTS=1` |
| Shared signature vectors | `deploy/keycloak/providers/user-projection/src/test/resources/push-signature-vectors.json` |

## 2. Files touched

New, MUSTASK side (`src/backend/`):

- `Mustask.Domain/UserDirectory/` — `ProjectedUser.cs`, `ProjectedUserLifecycle.cs`,
  `UserPush.cs`, `PushOutcome.cs`, `FeedTally.cs`, `ProjectionBatch.cs`,
  `DisplayNameRule.cs`, `DirectorySearchText.cs`, `DirectoryCollation.cs`,
  `DirectorySnapshot.cs`, `DirectorySnapshotState.cs`, `DirectorySnapshotPage.cs`,
  `UserDirectoryLimits.cs`, `UserDirectoryErrorCodes.cs`.
- `Mustask.Application/Abstractions/UserDirectory/` — `IUserDirectory.cs` (with
  `UserSearch`, `DirectoryUser`, `UserSearchResult`, `UserLookupResult`),
  `DirectoryUserOrder.cs`, `IProjectedUserStore.cs`, `IDirectorySnapshotStore.cs`.
- `Mustask.Application/Features/UserDirectory/ApplyUserChanges/`,
  `…/BeginSnapshot/`, `…/ApplySnapshotPage/`, `…/CompleteSnapshot/` — command
  with its validator, handler, result, response, one folder each.
- `Mustask.Infrastructure/UserDirectory/` — `Configurations/ProjectedUserConfiguration.cs`,
  `Configurations/DirectorySnapshotConfiguration.cs`,
  `Configurations/DirectorySnapshotPageConfiguration.cs`,
  `UserDirectoryReadModel.cs`, `ProjectedUserStore.cs`, `DirectorySnapshotStore.cs`,
  `UserDirectoryLocks.cs` (the advisory-lock SQL, one place).
- `Mustask.Infrastructure/Migrations/<timestamp>_AddUserDirectory.cs` (+ Designer, snapshot update).
- `Mustask.Api/Features/UserDirectory/` — `UserDirectoryEndpoints.cs`,
  `SignedPushBody.cs`, `PushSignatureVerifier.cs`, `PushChannelOptions.cs`,
  `PushRefusal.cs`.
- `contracts/errors/user_directory.json`; `docs/error-codes.md` regenerated.

New, provider and stand:

- `deploy/keycloak/Dockerfile`, `deploy/keycloak/.dockerignore`.
- `deploy/keycloak/providers/user-projection/pom.xml`, `src/main/java/…`,
  `src/main/resources/META-INF/services/org.keycloak.events.EventListenerProviderFactory`,
  `src/test/java/…`, `src/test/resources/push-signature-vectors.json` (§ 8).
- `src/backend/Mustask.AppHost/Provisioning/` (compiled into
  `Mustask.Contracts.Tests` by its existing `Compile` glob, so Aspire-free):
  `KeycloakStandDriftCheck.cs`, `KeycloakRealmImport.cs`, `StandStoreKey.cs`,
  `UserProjectionPushSecret.cs`, `UserProjectionIdentifiers.cs`.
- `src/backend/Mustask.AppHost/Stand/` (AppHost only — Aspire, Docker CLI,
  Npgsql): `KeycloakDriftHealthCheck.cs`, `StandReset.cs`.
- `scripts/stand-reset.sh` (executable), `scripts/stand-reset.ps1`.

Changed:

- `Mustask.Infrastructure/MustaskDbContext.cs` (three `DbSet`s),
  `Mustask.Infrastructure/DependencyInjection.cs`,
  `Mustask.Application/DependencyInjection.cs`, `Mustask.Api/Program.cs`
  (options, the collation start check, `MapUserDirectoryEndpoints`).
- `src/backend/Mustask.AppHost/AppHost.cs` (§ 9), `Mustask.AppHost.csproj`
  (Npgsql reference — § 12).
- `deploy/keycloak/realms/mustask-realm.json` — `"eventsListeners": ["jboss-logging", "mustask-user-projection"]`.
- `scripts/verify.sh` — the `provider` bucket (§ 9.6).
- `scripts/keycloak-seed.sh` — header (the store persists now) and the
  admin-token failure message (a changed password needs a reset).
- `tests/Mustask.Api.IntegrationTests/MustaskApiFactory.cs` (the push secret),
  `…/Features/Authorization/EndpointCategoryInventoryTests.cs` (four public routes).
- `tests/Mustask.Contracts.Tests/UserProjectionStandTests.cs`, new (the
  listener in the realm import, no push credential in it, the feed's stand
  address); `KeycloakRealmRolesTests.cs` stays unchanged.
- `src/backend/AGENTS.md` (the stand: image build, persistent store, drift
  check, reset, provider tests), `docs/runbooks/local-migrations.md` (after a
  reset), `docs/adr/README.md` (index line).
- `.gitignore` — `deploy/keycloak/providers/*/target/`.

Tests: § 10.

## 3. Domain (`Mustask.Domain/UserDirectory`)

- **`ProjectedUser`** — the aggregate. Fields: `Id`, `Username`, `FirstName`,
  `LastName`, `Email` (`""` for none), `Enabled`, `Lifecycle`
  (`Present | Absent | Deleted`, stored as a string), `Moment` (`long`), the
  derived `DisplayName`, `SearchName`, `SearchEmail`, `SortKey` (`byte[]`),
  `SortKeyVersion`, `IsActive`, plus `LastSnapshotId` (`Guid?`) and
  `ChangedAt` (`IClock`, diagnostics only).
  - `static Create(UserPush, now)` → `Added`.
  - `Apply(UserPush, now)` → `PushOutcome`: deleted held → `HeldAsDeleted`
    (a deletion push on a deleted user → `Unchanged`); `push.Moment <= Moment`
    → `Unchanged`; otherwise the state is replaced — `Changed` if a projected
    field, `Enabled` or `Lifecycle` differs, else `Unchanged` with the moment
    still advanced (ADR-0025 § 6). A deletion push makes the user `Deleted`
    for good; its carried profile replaces the held one only when its moment
    is newer.
  - `StampSnapshot(snapshotId)`.
  - `MarkAbsent(enumerationStart, now)` → `true` only for a `Present` user
    with `Moment < enumerationStart`; sets `Absent`, `Moment = enumerationStart`.
  - Derived fields are recomputed whenever the profile changes or
    `SortKeyVersion` differs from `DirectoryCollation.Version`.
- **`UserPush`** — one entry of Part B: id, moment, username, first and last
  name, e-mail, enabled, deleted. Built by the slices from validated input.
- **`ProjectionBatch.Apply(pushes, held, now, snapshotId?)`** — the one place
  both apply paths use: orders each id's pushes by moment, applies them,
  creates the missing users, stamps the snapshot if given, returns the new
  users and a `FeedTally` (received, added, changed, unchanged,
  heldAsDeleted). One push counts once even if a later push in the same batch
  touches the same user again.
- **`DisplayNameRule.Compose(first, last, username)`** — BR-UP-3.
- **`DirectorySearchText`** — `Normalize(value)` (a noncharacter read as
  U+FFFD, then NFC — a text NFC cannot take goes on uncomposed —
  `ToLowerInvariant`, `ё` → `е`, control characters removed) for both sides, and
  `Parse(text)` → *too short* (< 2 code points after `Trim`), *too long*
  (> 100) or a LIKE pattern with `%`, `_` and `\` escaped.
- **`DirectoryCollation`** — `CultureInfo.GetCultureInfo("ru").CompareInfo`
  with one fixed `CompareOptions` value (case- and diacritic-insensitive,
  `IgnoreCase | IgnoreNonSpace`, with or without `StringSort` — whichever
  passes BR-UP-15's unit tests; § 13); `SortKey(displayName)`,
  `Compare(a, b)`, `Version` (the `CompareInfo.Version` string),
  `EnsureSupported()` (throws unless `ё`=`е`, `я` < `a` and `-a` < `1` < `а`
  hold — invariant globalization fails here).
- **`DirectorySnapshot`** — `Id`, `EnumerationStart`, `State`
  (`Open | Completed | Superseded`), `BegunAt`, `CompletedAt`, the expected
  `PageCount`/`EntryCount` and the totals stored at completion.
  `Supersede()`; `Complete(pageCount, entryCount, appliedPages, stampedUsers,
  tally, markedAbsent, now)` — throws `ConflictException`
  `user_directory.snapshot_incomplete` (`expected`, `received`) on a mismatch,
  `user_directory.snapshot_not_open` when superseded; idempotent when completed.
- **`DirectorySnapshotPage`** — snapshot id, page number, the page's tally.
- **`UserDirectoryLimits`**, **`UserDirectoryErrorCodes`** (`[ErrorCodes]`) —
  the values and codes of `api-contract.md` Parts C and D.

Domain has no dependency beyond `Mustask.Shared` and the BCL
(`System.Globalization`), as now.

## 4. Database: migration `AddUserDirectory`

Additive, three new tables, reversible (`Down` drops them); no extension, no
collation object, no seed. xmin comes from the ADR-0010 convention.

| Table | Columns | Keys and indexes |
|---|---|---|
| `directory_users` | `id` varchar(255), `username` varchar(255), `first_name`, `last_name`, `email` varchar(255) not null default `''`, `enabled` bool, `lifecycle` varchar(16), `moment` bigint, `display_name` varchar(511), `search_name` text, `search_email` text, `sort_key` bytea, `sort_key_version` varchar(64), `is_active` bool, `last_snapshot_id` uuid null, `changed_at` timestamptz | PK `id`; index `(is_active, sort_key)` |
| `directory_snapshots` | `id` uuid, `enumeration_start` bigint, `state` varchar(16), `begun_at`, `completed_at` timestamptz, `page_count`, `entry_count` int null, `received`, `added`, `changed`, `unchanged`, `held_as_deleted`, `marked_absent` int | PK `id`; index `enumeration_start` |
| `directory_snapshot_pages` | `snapshot_id` uuid, `page_number` int, `received`, `added`, `changed`, `unchanged`, `held_as_deleted` int, `applied_at` timestamptz | PK `(snapshot_id, page_number)`, FK → `directory_snapshots` cascade |

Search scans `directory_users` (10 000 rows) with two `LIKE`s on the
pre-normalised columns; the volume test (§ 10) is the proof that no trigram
index is needed at this size (ADR-0025, "revisit when"). Snapshot rows are
pruned at each `BeginSnapshot` to the 20 newest; their pages go with them.

## 5. Application: the four push use cases

Every write runs in `IUnitOfWork.ExecuteInTransactionAsync` (READ COMMITTED,
ADR-0011) and **takes per-user advisory locks** — `pg_advisory_xact_lock(
hashtextextended('user_directory.user:' || id, 0))`, for the ids it will
write, in one call, the keys in ascending order of the lock key (the hash,
not the id) — the one order `UserDirectoryLocks.LockUsersAsync` uses for
every lock site — and then locks the held rows `FOR UPDATE` in ordinal id
order. A new lock site goes through the same method, never a second call
in the same transaction. Snapshot
operations first take one more lock, `'user_directory.snapshot'`, so begins,
pages and completions never interleave; `ApplyUserChanges` never takes it, so
the lock order is always snapshot → users and there is no cycle. The SQL lives
in `UserDirectoryLocks` (Infrastructure), behind `IProjectedUserStore` /
`IDirectorySnapshotStore`.

| Use case (folder) | Flow |
|---|---|
| `ApplyUserChanges` (the reference use case) | validate (Part B limits, field paths `changes[i].…`) → lock and load the distinct ids → `ProjectionBatch.Apply` → add new users → save → log the tally → `FeedCountsResponse` |
| `BeginSnapshot` | snapshot lock → same id: same start → current state, other start → `snapshot_conflict`; new id: `enumerationStart` not above the newest begun → `snapshot_conflict`; else supersede the open ones, add, prune to 20 → `SnapshotStateResponse` |
| `ApplySnapshotPage` | validate (no `deleted`, no repeated id, `pageNumber ≥ 0`) → snapshot lock → not found / not open → 404 / 409 → page already stored → its counts → else lock and load users, `ProjectionBatch.Apply` with the snapshot id, store the page → `FeedCountsResponse` |
| `CompleteSnapshot` | snapshot lock → not found → 404; completed → stored counts; superseded → 409 → applied page numbers must be exactly `0..pageCount-1` and users stamped with the snapshot exactly `entryCount`, else `snapshot_incomplete` → select the absence candidates' ids (`lifecycle = 'present'`, not stamped, `moment < enumerationStart`) and up to 1 000 ids whose `sort_key_version` is not the current collation's (code review F5: deleted and absent users are never rewritten by a page), lock both sets in one call, reload `FOR UPDATE`, `MarkAbsent` each candidate (the domain re-checks), `RefreshSortKey` every row loaded (derived values only) → complete with the page totals → save → log → `SnapshotCountsResponse` |

Each slice owns its response type (the reference slice's habit of a response
per slice), even where two look alike. Handlers are registered scoped in
`Mustask.Application/DependencyInjection.cs`.

Logging (NFR Logging; ADR-0025 § 4): one Information line per changes request
and per completed snapshot with the counts; users named only by id — at Debug,
except that a changes request that applied deletions names their ids in a second
Information line (a deletion is final; security pass S5, api-contract.md B.6);
never a name, an e-mail, a DTO's `ToString()` or the secret.

## 6. The read model (`UserDirectoryReadModel : IUserDirectory`)

- `SearchAsync`: refusals first (text > 100, `AmongIds` > 500 → A.6); then
  short-circuits (text < 2, empty `AmongIds` → `([], false)`); then one
  query — `AsNoTracking`, `EF.Functions.Like` on `search_name` or
  `search_email` with escape `\`, `is_active` unless `IncludeInactive`,
  `id = ANY(@ids)` when restricted, `ORDER BY sort_key, id COLLATE "C"`,
  `Take(21)`, projected with one static `Project` into `DirectoryUser`.
  `MoreMatched` = 21 came back.
- `LookupAsync`: refusal over 500; distinct ids; one `id = ANY(@ids)` query;
  `Known` from the rows, `Unknown` the rest.
- `DirectoryUserOrder.Comparer` and `UserSearchResult.Merge` sit in
  `Abstractions/UserDirectory` and use `DirectoryCollation` (Domain) — the
  same comparison the stored `sort_key` encodes; `id COLLATE "C"` and the
  comparer's code-point comparison agree on UTF-8.
- No `HttpClient`, no Keycloak type anywhere under `UserDirectory` (an
  architecture test, § 10).

## 7. Api: the routes and the signed body

- `UserDirectoryEndpoints.MapUserDirectoryEndpoints` maps the four routes of
  Part B (`{snapshotId:guid}`, `{pageNumber:int}`), each
  `.Public("authenticated by the push channel's HMAC signature (ADR-0025) in SignedPushBody, not by Bearer")`,
  `.ExcludeFromDescription()`, and `.ProducesProblem(...)` for 400, 401, 404,
  409 as applicable.
- `SignedPushBody<T>` — a parameter type with a static `BindAsync(HttpContext)`:
  resolves `PushSignatureVerifier` from `RequestServices`, checks the envelope
  first (both headers well-formed, the timestamp inside the window; refusal →
  `UnauthorizedException` `user_directory.push_unauthorized` before any of the
  body is read — security pass S3), then reads the raw body into memory up to
  4 MiB (more → `user_directory.push_malformed`), verifies the signature
  (refusal → the same 401, logged by reason), and only then deserialises with
  `System.Text.Json` (failure → `push_malformed`). The envelope needs no
  secret, so the 4 MiB read is bounded, not authenticated (security pass S6;
  ADR-0025 § 3). Every handler of the
  four routes takes its body only as `SignedPushBody<TRequest>` — a test
  asserts it, so a fifth route cannot forget the check.
- `PushSignatureVerifier` — the canonical string of `api-contract.md` B.2,
  `HMACSHA256`, `CryptographicOperations.FixedTimeEquals`, `IClock` for the
  300 s window, `Secret` then `PreviousSecret`.
- `PushChannelOptions.FromConfiguration(configuration)` — read once in
  `Program.cs`, before `Build()`, the `KeycloakOptions` pattern: a missing or
  short `UserDirectory:Push:Secret` fails the start naming the key, never the
  value.
- `Program.cs` also calls `DirectoryCollation.EnsureSupported()` before
  `Build()`.

## 8. The provider (`deploy/keycloak/providers/user-projection`)

Maven, Java 21, artifact `mustask-user-projection.jar`. Dependencies:
`org.keycloak:keycloak-server-spi`, `keycloak-server-spi-private`,
`keycloak-core` and Jackson, all `provided`, version `${keycloak.version}`
passed by the Dockerfile; tests — JUnit 5, Mockito, ArchUnit, and the JDK's
`com.sun.net.httpserver` as the stub receiver (no extra dependency).

| Class | Role |
|---|---|
| `UserProjectionListenerFactory` | `EventListenerProviderFactory`, id `mustask-user-projection`; `init` reads `FeedOptions` (invalid → ERROR log, inert); `postInit` registers `UserRemovalListener` and starts `FeedSender`, `Reconciler`, `ListenerEnablementWatch`; `close` stops them |
| `UserProjectionListener` | `onEvent(Event)` for `REGISTER`, `UPDATE_PROFILE`, `UPDATE_EMAIL`, `IDENTITY_PROVIDER_FIRST_LOGIN`, `USER_DISABLED_BY_PERMANENT_LOCKOUT`; `onEvent(AdminEvent, …)` for resource type `USER`, `CREATE`/`UPDATE`, path exactly `users/<id>`; realm ≠ `mustask` → ignored |
| `UserRemovalListener` | the `UserModel.UserRemovedEvent` provider event → a deletion with the last state |
| `ChangeCapture` | reads the user through `ProjectedUserReader` in the event's session, enlists an after-completion transaction: on commit, `MomentClock.next()` and `PendingChanges.put` |
| `ProjectedRealm` | the only caller of `getRealmByName`, with the constant `mustask` |
| `ProjectedUserReader` | `UserModel` → `UserState` (BR-UP-2 fields only), every string made well-formed — an unpaired surrogate and a Unicode noncharacter (U+FDD0..U+FDEF, the last two code points of every plane) become U+FFFD, U+0000 is dropped (security pass S1; QA defect D-5), so it never sends a character api-contract.md B.3 refuses (a username or id that cleans to the empty string is still sent and refused `value_required` — reachable only through an unvalidated import, QA re-check 3 O-2); `isServiceAccount` |
| `MomentClock` | microseconds, `max(now, last + 1)` |
| `PendingChanges` | map by id, newest wins, deletion dominates, cap 50 000 |
| `FeedSender` | batches ≤ 500, 200 ms fill window, timeouts 2 s / 10 s, backoff 1 → 60 s, 400 → drop + ERROR, 401 → retry + ERROR, first success after a failure → `deliveryRecovered(mark)` (the mark taken at the outage's first failure; see `Reconciler`) |
| `Reconciler` | single-flight; triggers: start + 30 s, recovery (dropped when a run begun since delivery started failing has completed or is under way — code review F1), period, re-enable, the first sighting of the listener enabled when a run was skipped meanwhile (F6); id list in one transaction, batches of 500 read by id in their own transactions, pages, completion; logs MUSTASK's counts |
| `ListenerEnablementWatch` | on the reconciler's scheduler, first 2 s after start, every 2 s while the state is unknown (up to 5 minutes), then each minute (code review F13): is `mustask-user-projection` in realm `mustask`'s `eventsListeners`; while not, nothing is pushed; a missing realm is unknown until the state is known, not enabled after; a transition to enabled → `Reconciler.request()`; the first check that reads the realm, if it finds it enabled → `Reconciler.requestIfSkipped()` |
| `PushClient`, `PushSigner` | `java.net.http.HttpClient`; the canonical string of B.2; refuses `http` unless `require-https=false` |
| `FeedOptions`, `FeedJson`, `UserEntry` | configuration, Jackson mapping of Part B, the entry record |

**The read-only guard** (`ReadOnlyGuardTest`, ArchUnit over
`mustask.keycloak.userprojection` main classes): every method or constructor
call into `org.keycloak..` must be on this allowlist —
`Config$Scope.get/getInt/getBoolean`;
`KeycloakSession.users/realms/getTransactionManager/getKeycloakSessionFactory`;
`KeycloakSessionFactory.register`; `KeycloakTransactionManager.enlistAfterCompletion`;
`AbstractKeycloakTransaction.<init>`; `KeycloakModelUtils.runJobInTransaction`;
`RealmProvider.getRealmByName` (from `ProjectedRealm` only);
`KeycloakSession.getContext` (an accessor) and `KeycloakContext.setRealm` (the one
setter, from `ProjectedRealm` only — live finding of 2026-10-02: Keycloak 26.4's user lookups in a
background session fail with "Session not bound to a realm", so
`ProjectedRealm.bind` binds the provider's own short-lived session to realm
`mustask`; session state, nothing written to the model);
`RealmModel.getId/getName/getEventsListenersStream`;
`UserProvider.getUserById/searchForUserStream`;
`UserModel.getId/getUsername/getFirstName/getLastName/getEmail/isEnabled/getServiceAccountClientLink`;
`UserModel$UserRemovedEvent.getRealm/getUser/getKeycloakSession`;
`Event.getType/getRealmId/getUserId`;
`AdminEvent.getResourceType/getOperationType/getResourcePath/getRealmId`;
enum accessors of `EventType`, `ResourceType`, `OperationType`. A violation
names the caller and the member. `ReadOnlyGuardIsNotVacuousTest` runs the same
rule over `guardfixtures` test classes calling `UserModel.setEnabled`,
`UserModel.credentialManager()`, `RealmProvider.getRealmsStream()`,
`RealmProvider.getRealmByName` and `KeycloakContext.setRealm` (the last two
from a class other than `ProjectedRealm`) and asserts five named failures.

**Dockerfile** (BuildKit syntax 1): stage `keycloak` =
`${KEYCLOAK_IMAGE}:${KEYCLOAK_TAG}`; stage `provider` =
`maven:3.9-eclipse-temurin-21`, copies only
`/opt/keycloak/lib/lib/main/org.keycloak.keycloak-server-spi-*.jar` from
`keycloak` to read the version, then `pom.xml` and `src`, then
`mvn -B -Dkeycloak.version=<that> verify` with `/root/.m2` as a cache mount;
final stage = `keycloak` + the jar in `/opt/keycloak/providers/` +
`/opt/keycloak/data/h2` created as the image's user (the volume's ownership).
`docker build --target provider deploy/keycloak` is the bucket's check.

## 9. The stand (`Mustask.AppHost`), the realm import and the scripts

### 9.1 Keycloak from the Dockerfile

`builder.AddContainer("keycloak", image, tag)` becomes
`builder.AddDockerfile("keycloak", "../../../deploy/keycloak")` with
`.WithBuildArg("KEYCLOAK_IMAGE", …)` and `.WithBuildArg("KEYCLOAK_TAG", …)`;
the rest of the chain (ADR-0023's container name, the endpoint, the two bind
mounts, `start-dev --import-realm`, the environment, the `/realms/mustask`
health check) stays. The comment block that explains "no volume, H2 in the
writable layer" is rewritten for the persistent store.

### 9.2 The store

- `.WithVolume(storeVolume, "/opt/keycloak/data/h2")`.
- Container mode: `storeVolume = VolumeNameGenerator.Generate(keycloak, "data")`,
  and the PostgreSQL volume is given its name explicitly through the same
  generator (`WithDataVolume(VolumeNameGenerator.Generate(postgres, "data"))`) —
  the name Aspire already uses, so existing stands keep their data — still
  inside the container branch (`AppHostExternalDatabaseRules`).
- External mode: `storeVolume = StandStoreKey.ForExternalDatabase(externalDb)`:
  `DbConnectionStringBuilder`, `Host`/`Server`, `Port` (default 5432),
  `Database`; host lower-cased, `localhost`/`127.0.0.1`/`::1`/`[::1]` folded to
  `localhost`; `mustask-keycloak-` + the first 16 hex of SHA-256 of
  `host|port|database`. No password enters the key.
- A startup line next to `Database:` — `Keycloak store: volume <name> (pairs
  with <the stand's container database | host:port/database>)`.

### 9.3 The feed's wiring

- `var pushSecret = new UserProjectionPushSecret();` (32 random bytes,
  base64url; one per AppHost process, never logged) →
  `builder.AddParameter("user-projection-push-secret", pushSecret.Value, secret: true)`.
- Keycloak: endpoint = `UserProjectionIdentifiers.FeedEndpoint(apiPort)`,
  `http://host.docker.internal:{Ports:Api}`, with the runtime argument
  `--add-host=host.docker.internal:host-gateway` (amended 2026-10-04 for QA
  D-1: `api.GetEndpoint("http")` resolves for a container to Aspire's tunnel,
  which loses the api after a `keycloak` restart followed by an api restart —
  § 13, assumption 4's fallback), the secret parameter,
  `require-https=false`, and the period when
  `Keycloak:UserProjection:ReconcilePeriodMinutes` is set.
- Api: `UserDirectory__Push__Secret` = the same parameter.

### 9.4 The drift check

- `KeycloakStandDriftCheck` (Provisioning, HTTP only, unit-tested): input — an
  `HttpClient` on `http://localhost:{Ports:Keycloak}`, the admin credential,
  the panel client secret, the expected panel and API URLs, and
  `KeycloakRealmImport` parsed from every `*.json` under `deploy/keycloak/realms`.
  It reads `GET /admin/realms/mustask`, `/roles?briefRepresentation=false`
  with each composite role's `/composites`, `/clients?clientId=…`, each
  declared client's `/roles` and `/client-secret`, `/groups` with
  `/role-mappings`. Output: findings, each with a message that names what
  differs and the remedy — `scripts/stand-reset.sh` (PowerShell
  `scripts/stand-reset.ps1`), or restoring the old value for the admin
  password, the panel secret and the ports. Values of secrets never appear.
  Comparison rules: ADR-0025 § 7 (built-in realm roles and undeclared clients
  aside; groups one way).
- `KeycloakDriftHealthCheck` (Stand, AppHost only): `IHealthCheck` registered
  as `keycloak-realm-drift` and attached with `keycloak.WithHealthCheck(...)`;
  unhealthy until the realm answers, then the check's result; logs each new
  finding once at Error through the AppHost logger; caches a pass.

### 9.5 The reset

- `AppHost.cs` strips `--stand-reset` and `--yes` from `args` before
  `DistributedApplication.CreateBuilder`, declares the resources as for a
  start (so the volume names and the mode come from the same code), and at the
  end calls `StandReset.RunAsync(...)` instead of `builder.Build().Run()`.
- `StandReset` follows ADR-0025 § 7: Docker present; refuse while any
  container mounts a volume of the pair (`docker ps -a --filter volume=…`);
  show the plan, confirm unless `--yes` (no `--yes` and no terminal →
  refuse); container mode `docker volume rm` both; external mode Npgsql to the
  `postgres` maintenance database, plain `DROP DATABASE`, `CREATE DATABASE`,
  then the Keycloak volume; never `postgres`/`template0`/`template1`; delete
  `.e2e-stand/keycloak-e2e-client.json`; print the next steps.
- `scripts/stand-reset.sh`: header (what it wipes, what it refuses, the next
  steps), `cd` to the repository root, `exec dotnet run --project
  src/backend/Mustask.AppHost -- --stand-reset "$@"`. `scripts/stand-reset.ps1`:
  the `invoke-sh.ps1` wrapper of the other scripts.

### 9.6 `scripts/verify.sh`

- `classify`: `deploy/keycloak/providers/*|deploy/keycloak/Dockerfile|deploy/keycloak/.dockerignore) echo provider ;;`
  before the other `deploy/keycloak/*` lines.
- `need_provider=1` runs, in order: Docker reachable, else **fail**
  ("Docker is required to verify the Keycloak provider"); `docker build
  --target provider deploy/keycloak`; the backend build if the dotnet bucket
  did not already; `dotnet test` of `Mustask.Contracts.Tests` and
  `Mustask.Api.IntegrationTests` with `--filter Category=KeycloakProvider` and
  `MUSTASK_KEYCLOAK_PROVIDER_TESTS=1`.
- `--list` shows the bucket; the header comment lists it.

### 9.7 Realm import and seed

- `eventsListeners` declared (realm-import edit — the first start after this
  task creates the store anyway).
- `scripts/keycloak-seed.sh`: the "no volume" paragraph rewritten; its
  admin-token failure says the password is the one the store was created with
  and names the reset.

## 10. Tests per story

Levels: **D** Domain unit, **A** Application unit, **I** Api integration
(Testcontainers PostgreSQL, the push routes through `PushTestClient`, which
signs with the factory's test secret; the read model resolved from the
factory's services), **C** Contracts (no Docker), **R** Architecture,
**J** provider (Java, in the image build), **L** opt-in live (the built image
in Testcontainers, the provider pushing to a signature-checking stub
receiver), **Q** QA on the live stand (D-13). Every test names its story and
scenario in a comment.

### US-1 — the read model

| Scenario | Tests |
|---|---|
| Search by part of a name, exactly four values | I `UserDirectoryReadModelTests`; A `DirectoryUserShapeTests` (four properties, by reflection) |
| E-mail, case ignored | I |
| Username not searched | I |
| `ё` = `е` | D `DirectorySearchTextTests`; I |
| Fewer than 2 characters | D; I (empty, `MoreMatched` false) |
| Over 100 refused, 100 accepted | D; I (code and field) |
| Inactive only when asked | I |
| Capped at 20, more matched | I |
| Cyrillic before Latin, case and `ё` ignored | D `DirectoryCollationTests` (the scenario's six names); I (same order from SQL) |
| Equal names by id, stable | I (25 namesakes, twice); A `DirectoryUserOrderTests` |
| Restricted to ids, repeats, never-held ids | I |
| Empty restriction | I |
| Over 500 ids refused, 500 answered | I |
| Shown by username | D `DisplayNameRuleTests`; I |
| Lookup known, inactive, deleted, unknown | I |
| Lookup repeated once, empty list | I |
| Lookup over 500 refused | I |
| Keycloak unreachable | I (realm not ready, reads answer); R `UserDirectoryBoundaryRules` (no `HttpClient`); Q (container stopped) |
| 10 000 users within 500 ms | I `UserDirectoryVolumeTests`; Q |
| (merge, BR-UP-17) | A `UserSearchResultMergeTests` |

### US-2 — created and updated

| Scenario | Tests |
|---|---|
| Created user becomes findable; id = `sub` | J (admin `CREATE` → entry); L (create → signed push with the fields); I (push → search); Q (`sub` comparison) |
| Name change followed | J; L; I |
| E-mail change followed | J; L; I |
| Username rename moves the display name | J; I; L (the test realm enables `editUsernameAllowed`, § 13) |
| The user's own profile change | J (`UPDATE_PROFILE`, `UPDATE_EMAIL`); Q (fixture user through ADR-0023 impersonation) |
| Changes outside the projected fields | J (role, group, reset-password events → nothing queued); L |
| Created disabled → inactive | J; L; I |
| Service account never projected | J (`ProjectedUserReader`, `Reconciler`); L (`mustask-e2e`-like client in the test realm); Q |
| Another realm never projected | J; L (a master-realm user) |

### US-3 — account status

| Scenario | Tests |
|---|---|
| Disabled → inactive, nameable | L; I |
| Enabled again → active, same id | L; I |
| Deleted → inactive for good, name kept | J (`UserRemovalListener`); L; I |
| A reported deletion is final (later push, after a complete reconciliation) | D `ProjectedUserTests`; I |
| New user with a deleted user's username and e-mail | I; L |
| Temporary lockout is not inactivity | J (`LOGIN_ERROR` and lockout types ignored); Q (wrong passwords typed by the suite, never the real one) |
| The status authorizes nothing | I (a token for an inactive id, same outcome); R (`Authentication`/`Authorization` do not reference the read model) |

### US-4 — backfill and reconciliation

| Scenario | Tests |
|---|---|
| Existing users loaded into an empty projection | J `ReconcilerTest`; L (200 users, 5 disabled, a service account); I (a 200-entry snapshot) |
| The stand comes up projected | Q |
| Second start: same ids, 0 counts | I (a repeated snapshot counts 0 added/changed/absent/deleted); Q |
| Import differs from the live realm → start fails, reset recovers, other database untouched | C `KeycloakStandDriftCheckTests`; Q (external database with a second database) |
| Two checkouts take turns on one external database | C `StandStoreKeyTests`; Q |
| Backfill in-process only | J `ReadOnlyGuardTest`; C `KeycloakRealmRolesTests` and R `StandOnlyKeycloakClientRules` unchanged and green |
| Lost deletion caught | I (snapshot without the user → absent, inactive, name kept); Q |
| Lost change caught | I; Q |
| Created during a reconciliation stays active | D; I (push with a moment after `enumerationStart`, then completion) |
| An absence yields to a newer push, not to an older | D; I |
| Repeats on its period | J (scheduler with a fake clock); Q (`Keycloak:UserProjection:ReconcilePeriodMinutes` = 2) |
| Interrupted snapshot marks nobody | I (pages without completion; completion with wrong counts → 409); J (a cut connection → no completion sent) |
| Reads during a 10 000-user reconciliation | I `UserDirectoryVolumeTests` |
| Unchanged realm → 0 counts | I |
| Provider tests gate verification | Q (break a provider test, `scripts/verify.sh` fails; `--list` shows the bucket) |

### US-5 — outages, ordering, concurrency

| Scenario | Tests |
|---|---|
| Keycloak does not wait for a stopped MUSTASK; failure logged by id | J (no I/O in `onEvent`); L (receiver stopped; median of 5 with and without the listener); Q |
| … nor for a slow one | L (the stub answers after 30 s) |
| Catch-up after MUSTASK returns | J (recovery → reconciliation); L; Q |
| Statuses stay as last received | I; Q |
| Same push twice | I (second answer counts `unchanged`) |
| Older after newer | D; I |
| Older enabled after newer disabled | D; I |
| Replayed captured push | I (the same signed request resent inside the window) |
| Concurrent pushes, 50 times | I `PushConcurrencyTests` (two requests released together) |
| Deletion concurrent with enabled, 50 times | I `PushConcurrencyTests` |
| Disabled provider freezes; re-enabled reconciles | J `ListenerEnablementWatchTest`; L |

### US-6 — the channel's security

| Scenario | Tests |
|---|---|
| No credential → refused, logged without name or e-mail | I `PushChannelAuthenticationTests`, `UserDirectoryLoggingTests` |
| Wrong credential | I |
| A Keycloak access token is not a credential (ordinary user, every code) | I |
| No MUSTASK user can write | I `UserDirectoryWriteSurfaceTests` (each route, every code); I `EndpointCategoryInventoryTests` (exactly four new routes) |
| A sign-in does not fill the projection | I `UserDirectoryWriteSurfaceTests` (calls with a token for an unpushed id → still unknown); R (only the three apply slices use `IProjectedUserStore`) |
| Provider with a wrong credential fails loudly, recovers | J (401 → ERROR, retried); L; Q |
| Credential only from environment / user secrets | R `UserProjectionPushSecretPlacementRules`; C (no secret in the realm import); J (`FeedOptions` reads `Config.Scope` only) |
| Never printed | I (log capture over accepted, refused and failed pushes and a snapshot); J (log capture); Q (grep of the three logs) |
| A fresh clone needs no new secret | R (the parameter is generated, no `AddParameter` without a value added); Q |
| No Keycloak client, no service account | C `KeycloakRealmRolesTests`, R `StandOnlyKeycloakClientRules` — unchanged |
| The read-only guard passes | J `ReadOnlyGuardTest` |
| The guard catches a write and a credential read | J `ReadOnlyGuardIsNotVacuousTest` |

Also: C `PushSignatureVectorsTests` (the C# verifier against the shared
vectors; trait `KeycloakProvider`), J `PushSignerTest` (the same file);
C `KeycloakProviderImageTests` (the Dockerfile's default tag equals
`AppHost.cs`'s; the jar lands in `/opt/keycloak/providers/`);
C `UserProjectionPushSecretTests`; Infrastructure `ProjectedUserModelTests`
(table and column names, lengths, string enums, xmin); the existing
`MigrationsTests` (apply on a clean database, roll back) extended if it does
not already cover a second migration; R `RulesAreNotVacuous` for each new
rule; I a test that every push route's handler binds `SignedPushBody<T>`.

## 11. Order of work

Small commits, build and tests green after each (`04-backend.md` § 4b).

1. Domain rules and their tests (§ 3).
2. EF configuration, `DbSet`s, the migration, model and migration tests.
3. The read model: abstractions, `UserDirectoryReadModel`, DI, the catalog
   entries of A.6, `docs/error-codes.md`; US-1 tests (seeding rows through
   the store, before the push route exists).
4. **The reference use case, end to end**: `ApplyUserChanges`, options,
   verifier, `SignedPushBody`, `UserDirectoryEndpoints` with this route, the
   remaining catalog entries, the inventory update; US-5 and US-6 MUSTASK-side
   tests.
5. `BeginSnapshot`, `ApplySnapshotPage`, `CompleteSnapshot` and their routes;
   US-4 MUSTASK-side tests and the volume tests.
6. The provider: `pom.xml`, sources, Java tests, the guard, the shared
   vectors, the Dockerfile, `.dockerignore`, `.gitignore`, the `provider`
   bucket of `verify.sh`.
7. The stand: Keycloak from the Dockerfile, the store volume, the feed's
   wiring and generated secret, the realm import's listener, the startup
   line, Contracts and Architecture tests, `src/backend/AGENTS.md`, the seed
   script.
8. The drift check and its tests.
9. The reset: AppHost mode, the two scripts, docs (`AGENTS.md`, the runbook).
10. The opt-in live tests.
11. A live stand run of § 13's checklist; findings fixed; `[dev]` Log line.

## 12. Lanes, and what goes through the orchestrator

**No lanes** (`04-backend.md` § 4c). The trigger is four or more use cases
with disjoint write zones; there are four push use cases, but three of them
are thin over the same core — `ProjectionBatch`, the stores and their lock
discipline, the signed binder — which phase 1 would have to finish first,
leaving each lane a handler of a few dozen lines. The bulk of the task is not
use cases at all: the Java provider, the stand and the scripts, which do not
fit a lane's three-path zone and share the stand and `verify.sh` for
verification. One session, sequentially, in the order of § 11.

Outside this task's write zone, for the orchestrator:

- `Directory.Packages.props`: `PackageVersion` for `Npgsql` (the reset's
  external-database drop and create in `Mustask.AppHost`) and for
  `Testcontainers` (the live tests' `ImageFromDockerfileBuilder`), each
  pinned to the version already resolved transitively — through
  `Aspire.Hosting.PostgreSQL` 13.5.1 and `Testcontainers.PostgreSql` 4.14.0 —
  so nothing that loads today changes. Needed before steps 9 and 10.
- `Mustask.sln`: no change (no new .NET project; the Java project is not in
  the solution).
- `status.md` `deps` (review round 3, R-21): tech-002 is already in this
  branch's base (`e6f78a5`), so the external-database scenarios are
  buildable.
- `tech-004-stand-docs-drift` (queued) also edits the stand's documentation:
  whichever lands second rebases its `src/backend/AGENTS.md` paragraph.

## 13. Live verification, and what the first live run must confirm

At QA, on the stand (D-13): US-1 (Keycloak stopped; volume), US-2 (`sub`;
own profile through the fixture), US-3 (lockout), US-4 (stand projected;
second start; drift with an external database and a second database on the
server; two checkouts; lost deletion and change; period of 2 minutes),
US-5 (timing, catch-up), US-6 (wrong credential; logs grepped; fresh clone).

Assumptions this plan rests on, each confirmed on the first live start (step
11) or by the test named, before anything is built on top of it:

1. The Keycloak 26.4 spelling of the SPI options' environment variables.
2. Admin events reach a listener whatever `adminEventsEnabled` says; the user
   event types of ADR-0025 § 2 fire as listed in 26.4.
3. `UserModel.UserRemovedEvent` carries a readable model for every removal
   path. If it does not, deletions fall back to the admin `DELETE` event with
   the held name kept, and `username` on a deletion entry becomes optional —
   a contract change, noted in `status.md` before it is made.
4. `api.GetEndpoint("http")` resolves for the Keycloak container to an
   address that reaches `Mustask.Api` on Windows; otherwise
   `http://host.docker.internal:{Ports:Api}` explicitly.
5. `start-dev`'s database lives under `/opt/keycloak/data/h2` and the volume
   initialised from the image's directory is writable by Keycloak.
6. `VolumeNameGenerator.Generate(postgres, "data")` equals the name
   `WithDataVolume()` gives today (an existing stand keeps its database).
7. DCP builds the Dockerfile with BuildKit (the Maven cache mount).
8. The `CompareOptions` value of `DirectoryCollation` that yields BR-UP-15
   (`DirectoryCollationTests` decides).
9. The realm import keeps `editUsernameAllowed` off, so on the stand an
   administrator cannot rename a username and US-2's rename scenario is
   proven at the push level and in the live tests' own realm. Turning the
   flag on in the import is a realm decision this plan does not take.

**Outcome of the first live run (dev, 2026-10-02, external database
`mustask_mod014`).** 1 confirmed: `KC_SPI_EVENTS_LISTENER__MUSTASK_USER_PROJECTION__*`
reaches the provider. 2 confirmed for admin events (create, update, disable,
delete via admin REST all pushed; a role mapping pushed nothing). 3 confirmed:
the removal event carries the last state (deleted users keep name and e-mail).
4 confirmed: `api.GetEndpoint("http")` resolves for the container to
`http://aspire.dev.internal:5000` (Aspire's tunnel) and reaches `Mustask.Api`
— but QA D-1 (2026-10-04) found the tunnel cannot rebind after a `keycloak`
restart followed by an api restart, so the fallback is now the wiring (§ 9.3).
5 and 7 confirmed: DCP built the Dockerfile with its cache mount, the volume
mounted on `data/h2` is writable, users and ids survived the stand restarts (a second start reconciled 3 users: 0 added, 0 changed).
6 not exercised (external-database mode has no PostgreSQL volume). One
finding fixed: background sessions must be bound to the realm (§ 8, the
allowlist). Also seen live: the drift check failing a start on an added realm
role, `scripts/stand-reset.sh --yes` dropping and recreating only
`mustask_mod014` (every other database on the server intact) and the next
start passing; a lost deletion marked absent and a lost change healed by the
start reconciliation; a 2-minute period honoured; Keycloak's admin create
taking the same ~0.22 s with `Mustask.Api` stopped as with it running.
