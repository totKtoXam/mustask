# Review: tech-047-test-run-speedup

## Round 1

Reviewed: `git diff origin/main...HEAD` at f3c523e7 (21 files), against the
plan in `status.md`; `docs/conventions/backend.md` (tests). No suite was run
(a full run was in progress on the machine); xUnit 2.9.3 runner semantics
(DisableParallelization collections run after all parallel ones) and
`WebApplicationFactory.WithWebHostBuilder` (the child gets the parent's
`CreateHost` as its delegate) were taken from the frameworks' source.

Verdict: **changes-requested** — one major, prose-only (a delta read, no QA
re-run); the rest are minors.

What holds: every class that calls `Console.SetOut`/`ConsoleCapture`,
`Environment.SetEnvironmentVariable`, asserts on a capturing logger of a host it
builds, resets `DenyAllProbeHandlerExecuted`, builds a bare
`WebApplicationFactory<Program>`, or holds a `Stopwatch` budget is in
`SerialClasses` or is a `*VolumeTests` (swept: all `Console.Set*`,
`SetEnvironmentVariable`, `Log.Logger`, `Stopwatch|Elapsed`, `CapturingLogger`,
static settable members in both test projects). The three shard classes using
`StaticLoggerGuard` assert on no log output. `pg_stat_activity` probes filter
by `current_database()`; advisory locks are database-scoped. In
Infrastructure.Tests all five readers/writers of `ConnectionStrings__mustask`
name `ProcessEnvironmentCollection`. `_liveFactories` is touched only under the
lock; no await inside either lock. The template build clears its pool before
the first copy. No test, `[Fact]`, `Skip` or assertion line is added, removed
or changed by the diff (the 18 skips are the live-provider facts and the
pre-existing `WorkItemFilterQaTests` D-B1 skip).

### F1 — stale "runs sequentially" safety claims (direction 2)
Severity: major
Where: `tests/Mustask.Api.IntegrationTests/Features/UserDirectory/ConsoleCapture.cs:5-6`;
`Features/Session/SessionLogoutEndpointTests.cs:200-201` (not in the diff).
Essence: ConsoleCapture says capturing the console is "safe because every class
of `MustaskApiCollection` runs sequentially"; SessionLogout says every class
that builds a host from Program "is in MustaskApiCollection, which runs
sequentially". Both are now false — the collection runs as parallel shards — and
the safety now comes only from `ApiTestCollectionFactory.SerialClasses`. A
developer who writes a new ConsoleCapture test from its own doc puts the class
in a shard and gets other shards' log lines in the capture.
Fix: rewrite both sentences to say the class is safe because it is listed in
`ApiTestCollectionFactory.SerialClasses`, and that a new caller must be listed
there too.

### F2 — a dead entry in SerialClasses
Severity: minor
Where: `ApiTestCollectionFactory.cs:79`.
Essence: `PushSecretAuditQaTests` names no collection, so `Get` never consults
the set for it (`:124`); it is a unit test with a `RecordingLogger`, not a class
asserting "log entries of a host the test builds".
Fix: drop the line (and recount F3).

### F3 — status.md count
Severity: minor
Where: `status.md`, Log, "after" line: "25 process-wide-state classes".
Essence: `SerialClasses` has 24 entries, 23 of them effective (F2).
Fix: state the real number.

### F4 — refcount decremented for a factory that never counted itself
Severity: minor
Where: `MustaskApiFactory.cs:141-147` and `:175`.
Essence: if `CreateDatabaseFromTemplateAsync` throws, `InitializeAsync` never
increments, but xUnit still calls `DisposeAsync`, which decrements. With other
shards live the count can reach 0 and clear the Keycloak/push variables under
them, turning one fixture failure into a cascade that hides it.
Fix: a `_counted` flag set inside the increment, decrement only when set.

### F5 — unanchored reason for serialising template copies
Severity: minor
Where: `tests/Shared/SharedPostgres.cs:21-23`.
Essence: "two copies of one template at once are one such session for the
other" — PostgreSQL counts only backends connected *to* the source database;
the copying session is connected to `mustask`. The semaphore is harmless, the
reason is not shown.
Fix: soften to "one at a time, to be safe", or drop the semaphore.

### F6 — overstatements in the factory's doc
Severity: minor
Where: `ApiTestCollectionFactory.cs:23` "shards share nothing but the server";
`:30-31` "1 runs the shared collection as one, as before".
Essence: shards share the process — Serilog's static logger and the console
(the doc's own next paragraph). With 1 shard the serial classes still get a
collection and a factory of their own.
Fix: "share nothing but the server and the process"; "1 runs the shards as
one collection; the serial classes still run after it".

### F7 — AGENTS.md rule is narrower than the list and unenforced
Severity: minor
Where: `src/backend/AGENTS.md:18-24`.
Essence: it names four reasons; `SerialClasses` has six (missing: a bare
`WebApplicationFactory<Program>`, a static flag another class resets). Nothing
checks membership — the rule is prose only.
Fix: name all six or point at the `SerialClasses` doc; optionally an inventory
test that fails when a class using `ConsoleCapture`/`Console.SetOut`/
`SetEnvironmentVariable`/`Stopwatch` is missing from the set.

### F8 — "one database" wording outside the diff
Severity: minor
Where: `Features/UserDirectory/UserDirectoryTestData.cs:10-11`,
`Features/Organizations/PgLocks.cs:8`.
Essence: "runs sequentially on one database" / "the integration collection runs
sequentially" — still true per shard, no longer of the collection.
Fix: "each shard runs sequentially on a database of its own".

### F9 — cites a test that does not exist
Severity: minor
Where: `MustaskApiFactory.cs:81` ("see
ProgramDoesNotAutoMigrateTests").
Essence: no such class anywhere in `src/` (carried over from the old comment).
Fix: drop the reference or point at ADR-0002.

### Behavioural claims checked
- AGENTS.md "each of the two starts one container per run" → `SharedPostgres.cs:19` (Lazy) + no other `PostgreSqlBuilder` in tests (Keycloak containers are opt-in, Category=KeycloakProvider bucket).
- AGENTS.md "gives every fixture a database of its own" → `SharedPostgres.cs` CreateDatabase*/fixtures; Xmin/Keyless/Migrations per test.
- AGENTS.md `MUSTASK_API_TEST_SHARDS` overrides → `ApiTestCollectionFactory.cs:143-146`.
- AGENTS.md "a `*VolumeTests` name goes there by itself" → `ApiTestCollectionFactory.cs:131`.
- AGENTS.md list of serial reasons → F7.
- AGENTS.md Infrastructure rule → the 5 `[Collection(ProcessEnvironmentCollection.Name)]` classes; unenforced (F7).
- ApiTestCollectionFactory "each shard … its own MustaskApiFactory and database" → `:107-112` + `MustaskApiCollection` ICollectionFixture + `MustaskApiFactory.InitializeAsync`.
- "runs alone after the shards" → `DisableParallelization = true` (`MustaskApiCollection.cs:34`), xUnit 2.9.3 runner.
- "every other class … a collection of its own" → `:124-127` (`CollectionPerClassTestCollectionFactory`, which also honours other named collections).
- "shards share nothing but the server", "1 … as before" → F6.
- "a class must land in the same shard on every run" → FNV-1a `:150-159`.
- "12 shards … 654 s against 649 s" → status.md Log (measurement).
- MustaskApiCollection "builds every host under one process-wide lock with its shard's connection string" → `MustaskApiFactory.cs:197-204`.
- MustaskApiFactory "each one a test derives with WithWebHostBuilder comes back here" → ASP.NET Core `WithWebHostBuilderCore` passes `CreateHost`; anchored to framework source.
- "keeps two builds from swapping Log.Logger" → lock spans `base.CreateHost`, which returns after Program reaches `app.Run()` past `Program.cs:214`.
- "first live factory sets … last one clears" → `_liveFactories` under lock; failure path F4.
- SharedPostgres "two copies … one such session" → F5.
- SharedPostgres "every connection it opened is cleared from the pool" → `SharedPostgres.cs:80` `ClearPool` on the template's connection string.
- SharedPostgres "resource reaper removes the container" → Testcontainers default; ProcessExit fallback `SharedPostgres.cs:108`.
- Infrastructure AssemblyInfo/ProcessEnvironmentCollection "runs alone, after the parallel ones" → `DisableParallelization = true`.
- ProcessDatabaseFixture "tests of one collection run one after another" → xUnit collection semantics.
- vite.config.ts "4-core CI runner still gets three" → `min(8, 4-1)`; 1 969 → 986 s → status.md Log.
- status.md "25 process-wide-state classes" → F3; "48 → 2 containers", "5 environment-variable classes" → anchored (one Lazy server per assembly; five attributes).
- Direction 2: behaviours changed — the Api collection's sequential run, one container per fixture, the Infrastructure assembly's serial run, Vitest worker cap. Grepped `runs sequentially|run serially|whole collection|one container|shared container|single collection` in both test projects, and `MustaskApiFactory|MustaskApiCollection|Testcontainers|ICollectionFixture|maxWorkers|DisableTestParallelization` under `docs/conventions`, `docs/ai-workflow`, `docs/runbooks`, `docs/adr`, `.claude/agents`, `.claude/commands`, both `AGENTS.md`. Hits: `ConsoleCapture.cs`, `SessionLogoutEndpointTests.cs` (F1); `UserDirectoryTestData.cs`, `PgLocks.cs` (F8); `CreateLanguageEndpointTests.cs:17`, `GetDeactivatedOrganizationsEndpointTests.cs:10`, `OrganizationsTestData.cs:15`, `KeycloakTestRealm.cs:350`, `DatabaseHealthCheckTests.cs:14-20` — "shared by the whole collection, never emptied" still holds within a shard (weaker, not wrong); docs/ hits mention Testcontainers generically, none stale. Not swept: test-name comments outside these patterns.
