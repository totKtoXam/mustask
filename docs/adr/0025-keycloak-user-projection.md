# ADR-0025: Project Keycloak users through a read-only event-listener provider that pushes signed, ordered state into MUSTASK

- Status: accepted (gate-3, 2026-10-04, delegated by the human 2026-10-01)
- Date: 2026-10-02
- Context: stage 4 (backend plan) of
  [mod-014-keycloak-user-projection](../features/mod-014-keycloak-user-projection/spec.md),
  which owes this record (spec § "ADR expected", items 1–9; doc-006 § Roles
  constraint 5; doc-010 D-9). Builds on
  [ADR-0018](0018-keycloak-as-identity-provider.md) (Keycloak owns identities),
  [ADR-0001](0001-use-net-aspire-for-local-orchestration.md) and
  [ADR-0023](0023-stand-only-keycloak-client-impersonates-the-e2e-fixture-user.md)
  (the stand; stand-only automation lives in `Mustask.AppHost`),
  [ADR-0011](0011-read-committed-with-pessimistic-locks.md) (pessimistic locks)
  and ADR-0004/0005/0006 (errors). It changes none of them. The *what* and the
  order of work are in the task's
  [plan-backend.md](../features/mod-014-keycloak-user-projection/plan-backend.md);
  the interface is in its
  [api-contract.md](../features/mod-014-keycloak-user-projection/api-contract.md).

## Problem

MUSTASK has to name people, find them by part of a name or e-mail and mark
them «неактивен», while Keycloak stays the only owner of identities and no
shipping code calls Keycloak's admin REST surface or holds a production
service account. doc-006 constraint 5 and doc-010 D-9 already fixed the
channel: a Keycloak event-listener provider pushes every change to a user of
realm `mustask` into a MUSTASK-owned projection, with a backfill, and
consumers read only the projection. What they left open, and what this record
decides, is how — nine questions, one per item of the spec's § "ADR expected".

The forces, all from the spec:

- **Keycloak never waits for MUSTASK** (BR-UP-7, NFR "Keycloak unaffected").
- **Order does not matter** (BR-UP-6): duplicates, reordering, concurrency and
  a replayed capture must all be harmless; a reported deletion is final
  (BR-UP-5); an absence yields to any newer push (BR-UP-8).
- **The provider only reads**, only realm `mustask`, only the projected fields
  (BR-UP-13), and calls no REST surface (BR-UP-9). Its in-process access is
  stronger than the admin REST surface ADR-0023 keeps out of production, so
  its own guard is the control (D-7).
- **The credential is not Keycloak's** (BR-UP-10): no client, token or
  service account, nothing in the repository or a log.
- **The stand** (NFR Stand, D-5, D-9, D-10, D-12): Keycloak's store persists
  and pairs one to one with the stand's `mustask` database; a realm-import edit
  never silently fails to appear; a reset wipes exactly that pair and never
  another database on an external server; no fixed-name container outlives the
  AppHost; a fresh clone needs no fourth secret and no tool beyond the .NET
  SDK, Node and Docker.
- **The read model is small and fixed** (D-2 (3)) so `mod-016` and `mod-003`
  build on it without reopening this task.

## Options considered

### 1. The provider as a deployable

- **A. Java sources in the repository, built and unit-tested inside a
  multi-stage Docker build that produces the stand's Keycloak image (chosen).**
  Keycloak providers are JVM code; Docker is already a stand prerequisite, so
  the JDK and Maven live in a builder image, never on the developer's machine.
  The stand runs exactly the jar the build tested, against exactly the Keycloak
  it runs on.
- **B. A prebuilt jar committed to the repository.** No toolchain at all on
  the stand, but a binary nobody reviews, rebuilt by hand on every Keycloak
  upgrade; the provider's guard (item 8) would test sources that are not what
  runs. Rejected.
- **C. A local JDK and Maven.** A fourth tool on every machine, against the
  NFR. Rejected.

### 2. Event sources

- **A. Admin events for creations and edits, user events for self-service
  edits, Keycloak's user-removal provider event for deletions (chosen).** Each
  source carries the user id and runs inside the transaction that made the
  change, so the provider can read the whole state in-process.
- **B. Admin events only.** Misses everything a user does to themselves —
  their own profile (US-2), a permanent lockout. Rejected.
- **C. No events, only periodic reconciliation.** Simplest, but the freshness
  NFR (10 s) would become the period. Kept as the safety net (item 5), not
  as the channel.

### 3. The push channel

- **A. HTTPS POST/PUT of JSON to `Mustask.Api`, each request signed with
  HMAC-SHA256 over a timestamp, the method, the target and the body's hash,
  with a shared secret (chosen).** The secret never travels; a captured
  request cannot be altered or moved to another operation; a stale timestamp
  bounds replay, and BR-UP-6 makes a replay inside the window harmless.
- **B. A static bearer secret in a header.** The secret crosses the wire on
  every request — in clear on the stand's plain HTTP — and a captured request
  can be replayed with a forged body. Rejected.
- **C. A Keycloak client-credentials token.** Needs a client with a service
  account in the realm import — exactly what BR-UP-10 and ADR-0023's allowlist
  forbid. Rejected.
- **D. Mutual TLS.** Sound in production, but certificates on a dev stand are
  a third credential store and do not replace the application-level proof the
  stand's plain HTTP needs. Rejected for now; TLS still wraps option A outside
  the stand.

### 4. Delivery relative to Keycloak's transaction

- **A. Capture the state inside the transaction, enqueue it after the commit
  into an in-memory queue coalesced by user id, deliver from the provider's
  own threads with timeouts, retry with backoff, and reconcile after a failure
  (chosen).** The request thread does no I/O and never waits; a rolled-back
  change is never pushed; memory is bounded by the number of users.
- **B. Push synchronously in `onEvent`.** Puts MUSTASK's latency and outages
  into every Keycloak operation. Rejected by BR-UP-7.
- **C. A durable outbox table in Keycloak's store.** Survives a Keycloak
  restart, but needs a custom JPA entity provider writing into Keycloak's
  model — the provider would no longer only read (BR-UP-13). Rejected: what a
  restart loses, the start reconciliation heals (BR-UP-7, BR-UP-8).

### 5. Reconciliation

- **A. Read the realm's user ids in one statement, then read the users by id
  in batches, each entry stamped before its read; send a snapshot (begin,
  numbered pages, completion with counts) that MUSTASK can prove complete
  (chosen).**
- **B. Offset pagination over the user list.** A deletion during the walk
  shifts every later page by one, so a live user is skipped — and a skipped
  user is marked absent. Rejected.
- **C. MUSTASK pulls.** Breaks BR-UP-9 (MUSTASK never calls Keycloak).
  Rejected.

### 6. The ordering marker

- **A. The provider's own clock in microseconds, strictly increasing within
  one provider process, read after the commit for an event and before the
  read for a snapshot entry (chosen).**
- **B. Keycloak's event time.** It is set before the commit, so a snapshot
  read between the event and the commit carries the old state with a *later*
  time, and the real change is then ignored as older. Rejected.
- **C. MUSTASK's receive time.** Reordering in flight is exactly what BR-UP-6
  must survive. Rejected.
- **D. A per-user version in Keycloak.** Keycloak keeps none; adding one is a
  write to its model. Rejected (BR-UP-13).

### 7. The stand's persistent store, the drift check and the reset

Store placement:

- **A. Keycloak's own development database in a named Docker volume, the
  volume's name derived from the database the stand uses (chosen).** Keycloak
  stays self-contained; no fixed-name container survives (D-12 allows a named
  volume); the pairing is a naming rule.
- **B. Keycloak's tables in a schema of the stand's own `mustask` database.**
  Pairs by construction and makes the reset one database drop — but in
  external-database mode the Keycloak container has to reach the developer's
  PostgreSQL through the Docker host gateway (listen addresses, `pg_hba`,
  Linux's missing `host.docker.internal`), and Keycloak's ninety tables move
  into MUSTASK's application database, where shipping code could be tempted
  to read them. Rejected: it trades a naming rule for a networking dependency
  on a machine we do not control.
- **C. A Keycloak container with a fixed name and a persistent lifetime.**
  Outlives the AppHost and blocks the next branch's stand on the same ports.
  Rejected by D-12.

Drift check: **in `Mustask.AppHost`, as a health check of the `keycloak`
resource, reading the live realm through the admin REST surface (chosen)**,
against a check inside the provider (it would have to read realm roles and
clients — beyond BR-UP-13's fields) or inside `Mustask.Api` (shipping code on
the admin REST surface, against ADR-0023).

Reset: **a mode of `Mustask.AppHost` (`--stand-reset`) behind
`scripts/stand-reset.sh` and its `.ps1` wrapper (chosen)**, against a pure
shell script (it would have to recompute the volume names Aspire derives from
the AppHost's path and re-parse the connection string — a second copy of the
AppHost's own decisions, free to drift).

### 8. The provider's power

- **A. An allowlist test over the provider's compiled calls into Keycloak
  (ArchUnit): every call from the provider's classes to an `org.keycloak`
  type must be on a named list of reads (chosen).** It sees calls, not text,
  so formatting, aliases and imports do not hide one.
- **B. A source-text scan.** Walked around by an alias or a line break.
  Rejected as the guard; ADR-0023's own admin-REST scan may additionally cover
  the provider's sources, harmlessly (D-7).
- **C. Review only.** Kept as the first line, not enough alone (D-7).

### 9. The sub-slice and how consumers read it

- **A. An explicit Application interface, `IUserDirectory`, in
  `Mustask.Application/Abstractions/UserDirectory`, implemented in
  Infrastructure; the push channel's use cases as slices under
  `Features/UserDirectory` (chosen).** Consumers are other modules' slices in
  the same process; the backend conventions route cross-module communication
  through explicit interfaces, and BR-UP-12 forbids exposing the read to
  people.
- **B. HTTP read endpoints.** A read surface for people that BR-UP-12 rules
  out, and a network hop between modules of one process. Rejected.
- **C. Consumers query the projection's table.** Every consumer would bind to
  columns, and D-2 (3)'s fixed model would be fixed nowhere. Rejected.

## Decision

### 1. The provider is a Java project built inside the stand's Keycloak image

- Sources: `deploy/keycloak/providers/user-projection/` (Maven, Java 21),
  provider id `mustask-user-projection`. Keycloak's SPI artifacts are
  `provided` dependencies; the only other runtime dependency is what Keycloak
  itself ships (Jackson); JUnit, Mockito and ArchUnit are test-only.
- Build: `deploy/keycloak/Dockerfile`, multi-stage, BuildKit. Its first stage
  is the Keycloak image itself (`ARG KEYCLOAK_IMAGE`, `ARG KEYCLOAK_TAG`); the
  builder stage (`maven:3.9-eclipse-temurin-21`) reads the exact Keycloak
  version from that image's own `keycloak-server-spi` jar name and compiles
  and tests the provider against that version (`mvn verify`: compile, unit
  tests, the read-only guard); a `test` target stops there; the final stage
  copies the jar into `/opt/keycloak/providers/` of the same Keycloak image.
  Maven's repository is a BuildKit cache mount, so a sources-only change
  rebuilds without the network.
- The stand gets it by building, not pulling: `AppHost.cs` declares Keycloak
  with `AddDockerfile` instead of `AddContainer`, passing `Keycloak:Image` and
  `Keycloak:Tag` as the two build args. The Dockerfile's `ARG KEYCLOAK_TAG`
  default equals `AppHost.cs`'s default (`26.4`), held by a contract test.
- **A tag the provider was not built against cannot happen**: the provider is
  rebuilt against whatever `Keycloak:Tag` names. An incompatible SPI fails the
  image build, and Aspire shows the failed build as the `keycloak` resource's
  error — the stand does not start Keycloak at all. A compatible compile with
  changed runtime behaviour is what the opt-in live tests catch (below); a tag
  set only in a developer's own user secrets is not a repository change and
  meets them only when that developer runs them.
- **Network** is needed by the first image build on a machine (base images,
  Maven Central), after a Keycloak tag change (new base image and SPI
  artifacts), after a dependency or plugin change in `pom.xml`, and after the
  BuildKit cache is pruned — not after a change to the provider's sources only.
- **`scripts/verify.sh` gets a `provider` bucket** for
  `deploy/keycloak/providers/**` and `deploy/keycloak/Dockerfile`. It runs
  `docker build --target provider` — the stage that compiles and tests, which
  the Dockerfile also names `test`; the same compile, unit tests and guard the
  stand's build runs — and the .NET tests tagged `KeycloakProvider`: the shared
  signature vectors, and the live tests that start the built image with a
  stub receiver (opt-in, enabled by the bucket). **Docker is a prerequisite of
  that bucket**: without it the bucket fails instead of printing "not
  checked", because a provider change verified by nothing is exactly what the
  NFR forbids.
- **A Keycloak upgrade re-verifies** by construction: bumping the Dockerfile's
  default tag (which the contract test forces together with `AppHost.cs`'s)
  lands in the `provider` bucket, which rebuilds against the new SPI and reruns
  the unit and live tests; the live-stand checklist of `plan-backend.md`
  closes it at QA.

### 2. Event sources, and what relies on reconciliation

The provider pushes a user's whole state on:

| Source | Carries |
|---|---|
| Admin event, resource type `USER`, operation `CREATE` or `UPDATE`, resource path exactly `users/<id>` | creation; any console or admin-REST edit, including disable and enable |
| User events `REGISTER`, `UPDATE_PROFILE`, `UPDATE_EMAIL`, `IDENTITY_PROVIDER_FIRST_LOGIN`, `USER_DISABLED_BY_PERMANENT_LOCKOUT` | self-registration and brokered creation (both off in today's realm), the user's own profile edits, a permanent lockout (off today: `permanentLockout: false`) |
| Keycloak's user-removal provider event (`UserModel.UserRemovedEvent`, published inside the removing transaction with the user's model) | deletion, by any path — console, admin REST, self-service account deletion — with the last state |

Everything else is ignored, by construction rather than by a list of
exclusions: role and group mappings, credential resets and required actions
are admin events of other resource types or operations, or of a path under
`users/<id>/`; sign-ins, a temporary brute-force lockout (the account stays
enabled) and their errors are user events of other types. A whole-state push
for an unrelated change would anyway apply as "unchanged" (BR-UP-4).

**Realm settings:** the provider needs only its id in the realm's
`eventsListeners`. `eventsEnabled` and `adminEventsEnabled` govern Keycloak's
own event store — the audit trail the spec keeps (NFR Audit) — not delivery to
listeners. The provider ignores any event whose realm is not `mustask`, so
enabling it in another realm projects nothing (US-2).

**No event at all, so only reconciliation (item 5) carries them:** user
federation synchronisation (none configured today), `--import-realm` and
`kc.sh import`, a partial realm import (any per-resource events it emits are
not relied on), changes made by another provider or script in-process, direct
database edits, and a Keycloak restored from a backup. Each is healed by the
next reconciliation — at most the reconciliation period.

### 3. The push channel

- **Transport:** JSON over HTTP(S) to four routes under
  `/api/user-directory/feed` of `Mustask.Api` (one for changes, three for a
  snapshot). The routes are `.Public(...)` with respect to the Bearer scheme —
  the precedent is ADR-0022's session routes — and are authenticated by their
  own scheme: the request body reaches a handler only through a custom-bound
  `SignedPushBody<T>` parameter, which first checks what needs no body — both
  headers present and well-formed, the timestamp inside its window — then
  reads the raw body (bounded), verifies the signature and only then
  deserialises. A request with a header missing or malformed, or a timestamp
  outside the window, is refused 401 before its body is read (security pass
  S3). Forming those headers needs no secret, so any caller can still have up
  to 4 MiB read before the signature refuses it: the limit bounds the cost, it
  does not authenticate (security pass S6). The routes have no rate limit of
  their own, and none is decided here; one, or a body limit at an ingress, is a
  deployment decision. There is no way to take the body without verifying it.
  Being anonymous to the Bearer scheme also keeps the routes out of the
  realm-readiness gate, which is about Keycloak's JWKS, not this channel.
- **Scheme:** headers `Mustask-Push-Timestamp` (Unix seconds) and
  `Mustask-Push-Signature: v1=<hex>`, the HMAC-SHA256, keyed by the secret's
  UTF-8 bytes, of `v1\n<timestamp>\n<METHOD>\n<target>\n<hex SHA-256 of the
  body>`, where `<target>` is the path below the feed base. A missing or
  malformed header, a wrong signature or a timestamp more than 300 s from
  MUSTASK's clock is one refusal, 401 `user_directory.push_unauthorized` —
  the response does not say which. A Bearer token, a Keycloak token of any
  user, is simply not a signature (BR-UP-10). The exact canonical form and a
  set of test vectors shared by the Java and the C# tests are in
  `api-contract.md`.
- **Replay:** outside the 300 s window, refused. Inside it, harmless by
  BR-UP-6: a replayed change is a duplicate or older; snapshot pages and
  completion are idempotent by snapshot id and page number; a replayed
  "begin" of an older snapshot does not supersede a newer one. No nonce store
  is kept.
- **Where the secret lives:** MUSTASK — configuration
  `UserDirectory:Push:Secret` (environment variable / user secrets), required
  at startup (≥ 32 characters, the start fails naming the key otherwise, the
  pattern of `KeycloakOptions`); the provider — its SPI option `push-secret`,
  from the environment of the Keycloak process. Never in the realm import, a
  file in the repository or a log line, on either side. On the stand,
  `Mustask.AppHost` generates a fresh value per AppHost process (the shape of
  ADR-0023's `KeycloakStandClientSecret`) and hands it to both sides as a
  secret parameter, which the dashboard masks — so a fresh clone sets no
  fourth secret.
- **Rotation without losing changes:** MUSTASK also accepts
  `UserDirectory:Push:PreviousSecret`. Set the new secret as MUSTASK's
  `Secret` and the old one as `PreviousSecret`, restart MUSTASK; change the
  provider's `push-secret`, restart Keycloak; remove `PreviousSecret`. Nothing
  is refused in between, and anything lost to a misstep is retried and then
  healed by the start reconciliation. On the stand both sides restart with the
  AppHost, so the question does not arise. `PreviousSecret` never expires, so
  the log is the operator's signal that the rotation is not finished
  (security pass S4): MUSTASK says at startup, at Information, that the key is
  configured, and logs a Warning when a request is accepted under it — at most
  once an hour — naming the key and never a value. A provider that has moved
  to the new secret raises nothing; the warning stops, and the key can go.
- **TLS outside the stand:** the provider refuses a non-`https` endpoint
  unless its option `require-https` is `false`, which only the stand sets. The
  signature proves origin and integrity; TLS is what keeps names and e-mails
  confidential in transit.

### 4. Delivery

- In `onEvent` (and in the removal listener) the provider reads the user's
  projected fields from Keycloak's model in the same session — no I/O — and
  enlists an after-completion task on the session's transaction manager. Only
  on commit does that task take a marker (item 6) and put the captured state
  into the pending queue; on rollback nothing is queued.
- The pending queue is a map keyed by user id holding the newest captured
  state (a deletion supersedes a state), so it never holds more entries than
  the realm has users. A hard cap of 50 000 entries guards against a bug: on
  overflow the queue is dropped, logged, and a reconciliation is scheduled.
- A sender thread of the provider's own drains it in batches of up to 500,
  waiting at most 200 ms to fill a batch, with a 2 s connect and a 10 s
  request timeout. On a 2xx the batch is done. On a refusal (401), a 5xx, a
  timeout or a connection failure, the batch goes back into the queue (newer
  states win the merge) and the sender backs off 1 s, doubling to 60 s; each
  failure is logged at WARN on Keycloak's side with the count and up to 20
  user ids — never a name, an e-mail or the secret — and a 401 at ERROR,
  naming the credential and clock checks to make. A 400 means MUSTASK cannot
  take what was sent: a contract bug, or input that Keycloak accepted and
  MUSTASK cannot parse or refuses — an unpaired UTF-16 surrogate or a Unicode
  noncharacter in a name or the e-mail, which Keycloak's account REST takes
  from the user themselves (security pass S1, S8; QA defect D-5: U+FFFE made
  MUSTASK's normaliser throw, a 500 the provider retries without end).
  The provider prevents the second kind: where it reads a user's projected
  fields — the one place the event, the removal and the reconciliation paths
  share — it makes every string well-formed before anything is queued (an
  unpaired surrogate and a noncharacter — U+FDD0..U+FDEF and the last two code
  points of every plane — become U+FFFD, and U+0000 is dropped, which
  PostgreSQL text cannot hold). MUSTASK refuses what the provider replaces
  (U+0000 and a noncharacter are `value_invalid`, `value_too_long` first when
  the text is also over the limit) and its own derivation is total, so no text
  that reaches it is a 500: "the provider never sends what MUSTASK refuses"
  holds for B.3's character rule (a username or id that cleans to the empty
  string is still sent and refused `value_required`; only an unvalidated
  import can produce one — QA re-check 3, O-2), and the search text of a consumer,
  which no provider sends, reads a noncharacter as U+FFFD, the character the
  provider writes in its place. A batch refused with a 400 is dropped and
  logged at ERROR with the error codes from the response; a reconciliation page
  refused with a 400 is not dropped but fails its run (§ 5), and is logged at
  ERROR with the ids of the users on that page — ids only — so the operator
  has a lead.
- The first success after a failure requests a reconciliation, which heals
  whatever the outage lost (BR-UP-8, "when delivery recovers after a
  failure") — unless a run begun since delivery started failing has already
  completed or is under way, which heals it as well (item 5).
- **BR-UP-7 holds** because no Keycloak request thread ever touches the
  network or waits on the sender's I/O: after the commit it only puts the
  captured state into the pending queue. The queue's monitor is shared — the
  request thread takes it to put, the sender to take a batch of at most 500 —
  but each holds it for in-memory map work only: never across a send, and
  the sender's wait for entries releases it. A slow or refusing
  MUSTASK only grows the coalescing queue and delays its own catch-up. A
  Keycloak restart loses the queue; the start reconciliation heals it.
- The provider's factory owns the two threads (sender and reconciler, daemon,
  named `mustask-user-projection-*`) and stops them in `close()`. Missing or
  invalid options make the provider log an ERROR at start and stay inert:
  a misconfigured feed never stops Keycloak from starting.

### 5. Reconciliation

- **When:** 30 s after Keycloak starts; after delivery recovers from a
  failure; every `reconcile-period-minutes` (option, default 60, minimum 1);
  and when the listener is enabled again in realm `mustask` after being
  disabled. While the listener is not enabled in the realm — or not yet known
  to be, before a check has read the realm — nothing is pushed and no
  reconciliation runs (US-5, "a disabled provider freezes the projection");
  changes are still captured and held while the state is unknown; while the
  listener is known not to be enabled none are captured (what was already
  queued is held), and the reconciliation that follows re-enablement catches
  up. The enablement is first checked 2 s after start and every 2 s while it
  is unknown — realm `mustask` not imported yet, or its listeners unreadable
  — for up to 5 minutes, then each minute; once known, each minute. Keycloak
  imports the realm before it
  answers, so the state is known within about 2 s of readiness and a change
  made as Keycloak becomes ready is held for seconds: NFR Freshness's 10 s
  holds from readiness on (code review F13; the live fixture measures it). A
  missing realm while the state is unknown is no answer yet, not "not
  enabled": its arrival is the first sighting, not a re-enablement adding a
  run beside the start run. Once the state is known, a realm gone counts as
  not enabled, and its return with the listener as a re-enablement. A run
  skipped while the state was still unknown is made up by the first check
  that finds the listener enabled, so checks failing past the start run
  delay the start reconciliation by seconds (or, after the quick checks, a
  minute), not by a period. One reconciliation runs at a time; triggers
  during a run coalesce into one more run. A run that fails is retried a
  minute later, and at most
  one retry is ever pending: any run that starts — a period, a recovery, the
  retry itself — cancels it first. Delivery's recovery asks for a run only
  when no run begun since delivery started failing has completed or is under
  way: the sender hands the reconciler a mark taken at the outage's first
  failure, and a run begun after it read the realm after the outage began
  (should one under way fail, it schedules the retry itself). An outage of any
  length therefore costs one attempt a minute (plus the periods), and its end
  one reconciliation — whichever comes first, the retry or the recovery.
- **How it enumerates, in-process:** in one short transaction
  (`KeycloakModelUtils.runJobInTransaction`) it takes the marker `E` — the
  enumeration start — and then, in one statement, the ids of every user of
  realm `mustask` that is not a service account
  (`UserProvider.searchForUserStream` with `UserModel.INCLUDE_SERVICE_ACCOUNT`
  off). Then, in batches of 500 ids, each in its own transaction, it takes a
  marker and reads each user by id, skipping service accounts (checked again
  on the model) and users gone since the id list (their removal is pushed as
  a change).
- **The snapshot:** `PUT .../snapshots/{id}` with `E` (the id is a fresh UUID
  per run), `PUT .../snapshots/{id}/pages/{n}` for pages 0, 1, … of up to 500
  entries, and `PUT .../snapshots/{id}/completion` with the page and entry
  counts. MUSTASK applies each page as pushes and remembers, on each user row,
  the last snapshot that carried it. **Completeness is proved on MUSTASK's
  side**: completion is accepted only if pages `0..pageCount-1` all arrived
  and exactly `entryCount` distinct users carry this snapshot. Only then are
  the held users that are neither deleted nor absent, not carried by it and
  whose state's marker is older than `E`, marked absent as of `E`. Anything
  less — a dropped page, a cut connection, a provider restart, a newer
  snapshot begun meanwhile — completes nothing and marks nobody absent
  (BR-UP-8). Only a snapshot with a newer `E` supersedes an open one.
- **Why the id list is correct:** every user committed before `E` is in the
  statement's result, because the statement starts after `E`; a user created
  after `E` carries a marker after `E` and is never marked absent; a user read
  as missing after the list was taken is skipped, and its removal event pushes
  the deletion.
- **10 000 users:** one id statement, 20 read batches and 20 pages; MUSTASK
  applies a page in one short transaction. Reads never wait on it: they take
  no locks under PostgreSQL's MVCC (NFR Volumes).
- **Several Keycloak nodes** (out of scope, the stand runs one): each node
  pushes the changes it commits, so the change feed works unchanged. The
  reconciliation would have to run on one node at a time — Keycloak's
  `ClusterProvider.executeIfNotExecuted` is the tool, not used today; MUSTASK's
  supersession only keeps two concurrent snapshots from marking anyone absent
  wrongly. What breaks is item 6: markers from different nodes' clocks are not
  comparable. A cluster needs a cluster-coherent marker (a database sequence or
  a hybrid logical clock) — the condition under which this record is
  revisited.

### 6. Ordering

- **The marker** is an integer, microseconds since the Unix epoch, from the
  provider's `MomentClock`: `max(now, last + 1)`, so strictly increasing within
  one provider process — two markers of one process are never equal.
- **Its clock** is the Keycloak node's wall clock. Only markers are compared
  with markers, so MUSTASK's clock does not enter into ordering (it enters only
  the 300 s signature window). A wall clock stepping back across a Keycloak
  restart makes the pushes after it look older for that long, and they are
  ignored. A reconciliation run before the clock catches up heals none of
  them either: its entries are older than the held markers, and its begin is
  refused while its enumeration start is not above the newest snapshot
  begun. The first reconciliation after the clock has passed the held
  markers heals them — the step back costs its own length plus at most one
  period.
- **An event's marker is read after its transaction commits; a snapshot
  entry's before the read that produced it.** Under READ COMMITTED this makes
  "newer marker" mean "newer committed state": if the commit happened before
  the read, the entry already shows it; if after, the event's marker is the
  later one.
- **Ties:** none within one process. MUSTASK applies a push only if its marker
  is strictly greater than the held one; an equal marker is a duplicate.
- **A snapshot entry** is a push like any other. Even when its values equal
  the held ones, its newer marker is stored — otherwise a delayed push from
  between the old marker and the snapshot's read could revert the user.
- **An absence** is held with marker `E`: any push newer than `E` replaces it,
  any older one is ignored (US-4).
- **A deletion** is final whatever its marker: no state push after it, and no
  absence, changes the record (BR-UP-5).
- **Concurrent application** in MUSTASK is serialised per user: a request's
  transaction takes `pg_advisory_xact_lock` on a key per user id it touches —
  `hashtextextended` of the prefixed id — in one call, the keys in ascending
  order of the key itself (not of the id): the one order
  `UserDirectoryLocks` uses for every lock site, so two requests that share
  users take the shared keys in the same order and never wait on each other
  in a cycle. The lock also covers ids not held yet. It then locks the held
  rows `FOR UPDATE` in id order, lets the domain rule decide each push and
  writes. A lock site added later takes its keys through the same method, in
  one call per transaction. The completion takes the same per-user locks —
  in one call — for its absence candidates and for the users whose sort key
  it re-keys (item 9) and re-reads them `FOR UPDATE`, so it cannot
  interleave with a push to the same user; snapshot operations also take one
  snapshot-wide lock first, which pushes of changes never take, so the lock
  order never forms a cycle. The newest marker wins and a deletion
  wins over any push whatever the
  arrival order — the concurrent scenarios of US-5 are this rule, run 50 times.
- **A known limit:** two Keycloak transactions editing one user at the same
  instant each capture their own view. If Keycloak's update writes only the
  changed columns, the projection shows the later commit's view until the
  next reconciliation. Accepted.

### 7. Enabling the listener; the stand's store, drift check and reset

**Enabling.** The realm import declares
`"eventsListeners": ["jboss-logging", "mustask-user-projection"]`. A realm the
import does not overwrite — any existing production realm, or a persistent
stand store created before the listener was declared — gets it by an operator
step (realm settings, events, event listeners; or the admin REST call that
updates the realm), never by MUSTASK code. On the stand the drift check below
refuses a store without it.

**The store.** Keycloak keeps its development database (`start-dev`'s file
database under `/opt/keycloak/data/h2`) in a named Docker volume, created by
the image build with the Keycloak user's ownership. The volume's name pairs it
with the stand's database:

- container mode — Aspire's own volume name for the `keycloak` resource,
  `<app>-<hash>-keycloak-data`, from the same generator and the same AppHost
  path hash as the PostgreSQL volume `<app>-<hash>-postgres-data`: one pair per
  checkout;
- external-database mode (tech-002) —
  `mustask-keycloak-<first 16 hex of SHA-256 of host|port|database>`, read from
  `ConnectionStrings:mustask` with the loopback spellings (`localhost`,
  `127.0.0.1`, `::1`) folded into one: every checkout on that database shares
  the store, and stands on different databases never do. The pairing is per
  machine, as Docker volumes are.

The Keycloak container keeps ADR-0023's port-based name and Aspire's session
lifetime, so it is removed when the AppHost stops; only the volume outlives
the run (D-12). Two branches' stands take turns on the same ports exactly as
today.

**The feed's address on the stand.** The provider reaches `Mustask.Api` at
`http://host.docker.internal:{Ports:Api}` — the api's own port on the Docker
host — and the container gets `--add-host=host.docker.internal:host-gateway`
so a Linux engine resolves that name as Docker Desktop does. It does not go
through `api.GetEndpoint("http")`, which for a container is Aspire's
container tunnel: once the `keycloak` resource has restarted, the next api
restart leaves the tunnel unable to bind its listener again, and the
provider never reaches the api until the AppHost restarts (QA D-1). This is
the Docker host gateway over which store option B was rejected, and it does
not reopen B: the far end here is our own api, which the stand already
binds non-proxied on `0.0.0.0` for access from the local network, so the
route asks nothing of a machine we do not control; option B's far end is a
developer's PostgreSQL, whose listen addresses and `pg_hba` are theirs.
Verified on Docker Desktop for Windows. A Linux engine whose host firewall
filters the bridge would block the feed; that shows as delivery failures in
Keycloak's log, never as a silent gap, and the reconciliation catches up once
the route works.

**The start drift check.** A health check of the `keycloak` resource in
`Mustask.AppHost` — stand-only code that is never deployed, the placement
ADR-0023 established — signs in with the master administrator credential the
AppHost already holds and compares the live realm with the realm-import files:

- realm roles: every declared one present with the same composites, and no
  live one undeclared, Keycloak's built-ins (`offline_access`,
  `uma_authorization`, `default-roles-mustask`) aside;
- the roles of every client the import declares (`mustask-panel`,
  `mustask-api`), in both directions; clients the import does not declare —
  Keycloak's own and ADR-0023's `mustask-e2e` — are not compared;
- the listener: `mustask-user-projection` enabled in the live realm exactly
  when the import declares it;
- **groups: compared, one way.** Every declared group must exist at its path
  with exactly its declared realm and client role mappings; a group the import
  does not declare is ignored. mod-002 grants its right "by the realm role or
  group" (BR-M1-2), so a declared group's mapping is configuration the import
  owns — but groups are also the organisation structure a realm administrator
  edits, and a stand that refused to start after someone created a group in
  the console would force a reset, which on a shared external database drops
  every branch's data;
- the values Keycloak takes only when the store is created: the
  administrator password (the master token request is refused), the
  `mustask-panel` client secret (compared with the parameter, never printed),
  and the client URLs the import fills from the stand's ports
  (`MUSTASK_PANEL_URL`, `MUSTASK_API_URL`: `rootUrl`, `redirectUris`,
  `post.logout.redirect.uris`).

The check reads through the admin REST surface; that is allowed because
`Mustask.AppHost` is not shipping code — ADR-0023's scan of shipping projects
is unchanged and does not see it. On any finding the resource stays
**unhealthy**: the dashboard shows the finding and the AppHost's console logs
it at error, each naming the remedy — `scripts/stand-reset.sh` (PowerShell:
`scripts/stand-reset.ps1`), or, for a changed secret or port, restoring the
old value. Because Aspire raises `ResourceReadyEvent` only on a healthy
resource, ADR-0023's provisioning does not run and the E2E stand file is not
written: the stand does not come up ready (US-4). A passing result is cached
for the AppHost process.

**This is how a developer learns that a changed administrator password,
`mustask-panel` secret or stand port needs a reset** — at the start, by name,
instead of as a failed provisioning or a sign-in that ends in
`unauthorized_client` or `Invalid parameter: redirect_uri`.
`scripts/keycloak-seed.sh`'s own admin-token failure message says the same.

**The reset.** `scripts/stand-reset.sh` (and `scripts/stand-reset.ps1`
through `invoke-sh.ps1`) runs `Mustask.AppHost` with `--stand-reset`, which
resolves the mode, the volume names and the database exactly as a stand start
would, then:

1. **refuses while a stand runs** — any container, running or stopped, that
   mounts either volume of the pair is named, with "stop the AppHost first";
2. shows what it will wipe — mode, volume names, the database's host, port and
   name, never the password — and asks for confirmation unless `--yes`;
3. container mode: removes the PostgreSQL and the Keycloak volumes of this
   checkout; external mode: connects to that server's `postgres` maintenance
   database with the same credentials, runs `DROP DATABASE` on the named
   database — plain, without `FORCE`, so PostgreSQL itself refuses while any
   session is connected, which is reported as "a stand or another client is
   connected" — then `CREATE DATABASE` with the same name, and removes the
   Keycloak volume keyed to it. It refuses to touch `postgres`, `template0`
   and `template1`, and never names any other database;
4. deletes the stale E2E stand file and prints the next steps: start the
   stand, `dotnet ef database update`, `scripts/keycloak-seed.sh`. The first
   reconciliation into the empty projection is the backfill.

**ADR-0023's provisioning and the seed on a surviving realm.** Both were
written to be re-run: the provisioning resolves `mustask-e2e` and
`e2e-fixture` by natural key and creates or updates them, sets the realm's
stand-only `accessTokenLifespan` and gives the client this run's fresh secret,
so it converges on a realm that already has them. `scripts/keycloak-seed.sh`
creates or updates its users, so it is needed after a reset, not after every
start; its header comment changes accordingly. The fixture user is projected
like anyone else; `mustask-e2e`'s service-account user never is (BR-UP-2).

### 8. The provider's power

- **The rule (BR-UP-13):** the provider writes nothing to Keycloak's model;
  reads realm `mustask` only; reads of a user only id, username, first name,
  last name, e-mail, enabled and the service-account link.
- **The guard, a second line after review (D-7):** an ArchUnit test in the
  provider's own build imports the provider's compiled classes and requires
  every call they make into an `org.keycloak` type to be on a named
  allowlist (the full list is in `plan-backend.md`) — `UserModel`'s getters
  above and `getServiceAccountClientLink`, the event objects' getters, the
  realm's id, name and event-listener list, `UserProvider.getUserById` and
  `searchForUserStream`, the session's accessors for those providers, its
  context (`getContext`) and its transaction manager's
  `enlistAfterCompletion`, `KeycloakModelUtils.runJobInTransaction`, the
  session factory's `register`, `Config.Scope`'s getters, and
  `RealmProvider.getRealmByName`, which only one class, `ProjectedRealm`, may
  call, with the constant `mustask`.
- **One setter is on the list, and only for that same class:**
  `KeycloakContext.setRealm`, which `ProjectedRealm.bind` calls to bind the
  provider's own background session (`runJobInTransaction`) to realm
  `mustask` — the realm it has just looked up by that name. Keycloak 26.4's
  user lookups refuse a session bound to no realm (found on the live stand,
  2026-10-02). It sets the session's state for the duration of that one
  short-lived session and writes nothing to Keycloak's model; called from any
  other class it fails the guard. Apart from it, a setter,
  `credentialManager()`, `getAttributes()`, a realm or model write, a realm
  enumeration — anything not listed — fails the test with the calling class
  and the called member. A companion test runs the same rule over fixture
  classes that call `UserModel.setEnabled`, `credentialManager()`,
  `RealmProvider.getRealmsStream`, and `getRealmByName` and `setRealm` from a
  class other than `ProjectedRealm`, and asserts it fails on each of the five,
  so the rule cannot pass vacuously.
- **What the guard is not:** like ADR-0023's scans, it is a net for the
  innocent addition, not a defence against a determined author — reflection or
  a cast to an implementation class defeats any static rule. Review of the
  provider's sources stays the first line.
- **ADR-0023's guards are unchanged:** the realm import still declares only
  `mustask-panel` and `mustask-api`, neither with a service account, and no
  shipping .NET source calls `/admin/realms/`. Extending that scan to the
  provider's Java sources is harmless and not the control; the plan does not
  do it.

### 9. The sub-slice and the read model

- **Layout**, as the reference Dictionaries slice: `Mustask.Domain/UserDirectory`
  (the rules: the projected user, apply and absence, display name, search
  normalisation, the collation, limits, error codes),
  `Mustask.Application/Abstractions/UserDirectory` (the read model's
  interface and DTOs, the stores' interfaces),
  `Mustask.Application/Features/UserDirectory/<UseCase>` (the four push use
  cases), `Mustask.Infrastructure/UserDirectory` (EF configuration, the read
  model, the stores) and `Mustask.Api/Features/UserDirectory` (the routes and
  the signed-body binder). One migration of its own; no
  `Features/Organizations` (D-11); nothing of M1's skeleton (D-6).
- **How consumers read it:** a `mod-016` or `mod-003` slice injects
  `IUserDirectory` and calls `SearchAsync` or `LookupAsync`; it never touches
  the table or the stores. A user in either answer is exactly id, display
  name, e-mail and active — four values (BR-UP-11). The shape, the limits
  and the error codes are fixed in `api-contract.md`.
- **The order offered for merging** (BR-UP-15, BR-UP-17): the projection
  stores, per user, a sort key — the bytes of the platform ICU collation for
  culture `ru` at primary strength (case and diacritics ignored, so `ё` equals
  `е`; punctuation and symbols significant) — and orders a search in SQL by
  that key, then by id in code-point order. The same collation is offered to
  consumers in-process as `DirectoryUserOrder.Comparer`, and the whole merge
  rule of BR-UP-17 as `UserSearchResult.Merge`, so no consumer reimplements
  the collation and the merged order is the SQL order by construction. The
  collation lives in .NET, not in PostgreSQL, so an external server needs
  neither ICU nor a collation object, and the key travels nowhere: the user
  stays four values.
- **The collation is a runtime dependency:** `Mustask.Api` checks at startup
  that ICU is present and behaves (`ё` equals `е`, Cyrillic before Latin) and
  refuses to start otherwise — invariant-globalization runtimes, such as the
  chiseled container images without ICU, are unsuitable. A runtime with a
  different ICU version changes keys; each stored key carries its collation
  version. A user's next write recomputes a stale key, and a reconciliation's
  pages rewrite the present users it carries. Deleted and absent users are
  never pushed again, so each snapshot's completion also re-keys up to 1 000
  rows of any lifecycle whose key is of another version, locked together
  with its absence candidates; that changes derived values only — no
  lifecycle, marker or change time. After an ICU change the order is the new
  version's for present users from the first reconciliation, and for the
  rest once enough completions have run, one per 1 000 of them; until then an
  inactive user can sit out of order in an `IncludeInactive` search.

## Consequences

**What gets easier.** Consumers get names and statuses with no call to
Keycloak and no permission of their own to design; the stand keeps its users
and ids across restarts, so M1 data created on it stays meaningful; a
realm-import edit, a changed secret or a changed port is announced at the
start instead of discovered as a broken sign-in; a fresh clone still needs
only three secrets.

**What gets harder.**

- **A second language and toolchain** in the repository — Java and Maven,
  confined to `deploy/keycloak/providers/` and to a Docker builder image.
  Changing the provider needs Docker running (verify.sh refuses otherwise),
  and the first stand start on a machine, or after a Keycloak upgrade, builds
  an image and needs the network.
- **The stand is no longer reset by a restart.** Switching between branches
  whose realm imports differ needs `scripts/stand-reset.sh`; on a shared
  external database that reset drops the shared `mustask` database, so every
  branch's stand data goes with it (the cost review round 3 named; accepted
  at gate-1 under D-9 and D-10).
- **Keycloak SPI coupling:** the event types, the removal provider event, the
  after-completion hook and the user-search parameter are Keycloak internals,
  not a published API. The live tests and the QA checklist are what notice a
  change; a Keycloak major that removes one of them supersedes this record.
- **A stale projection is possible for up to one reconciliation period**
  after a change no event carries (item 2) or a push that was lost, and a
  user's sign-in does not shorten it (BR-UP-1).

**Locked in.** The push channel's operations, headers and signature, and the
read model's four values, are contracts with the provider and with `mod-016`
and `mod-003`; changing either is a new task with a contract note. Any future
Keycloak provider follows this shape: Java under `deploy/keycloak/providers/`,
built and tested in the stand's image, guarded by its own allowlist.

**Revisit when:** Keycloak runs on more than one node (item 6's marker); the
projection must serve far beyond 10 000 users (a search index, item 9); a
deployment pipeline appears (TLS termination, secret distribution, the
operator's listener step); or a Keycloak upgrade removes an event source this
record relies on.
