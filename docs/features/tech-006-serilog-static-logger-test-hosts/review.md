# Review: tech-006-serilog-static-logger-test-hosts

## Round 1

Diff `ceaa1646..8b645bfa` (14 files). Input: status.md Notes/Plan/Log, qa.md,
bug-002 review.md F-1, `docs/conventions/backend.md`. Security covered in this
pass (economy mode). No endpoint, auth or contract change.

Verdict: **changes-requested**. Open: 0 blocker, 3 major (CR-1..CR-3), 0 minor.

The runtime fix is right. `preserveStaticLogger: true` (`Program.cs:80`) plus
the DI request logger (`Program.cs:202, 224`) leave nothing in production that
reads the static logger. A grep of `src/backend/**/*.cs` for `Log.Logger`,
`Log.<level>`, `CloseAndFlush`, `CreateBootstrapLogger` and `using static Serilog`
finds only comments. There is no bootstrap logger and no `try/catch Log.Fatal`
around startup. Taking `ErrorHandlingTests` and `PurgeWorkItemEndpointTests` out
of `SerialClasses` is sound: both capture through a `CapturingLoggerProvider` in
their own child host's DI, with no console capture, no environment variables and
no shared static. The defects are in the tests that are meant to pin the fix.

Run by the reviewer (Testcontainers):
- new class plus `SessionLogoutEndpointTests` and `TokenLeakTests` on HEAD → 18/18;
- `preserveStaticLogger: false` (temporary), each `StaticLoggerRegressionTests`
  test alone → **both green on the old wiring**;
- the same, with `_ = _factory.Services;` added as the first line of test 1 →
  red (`Assert.Contains() Failure`);
- tree restored, `git status` clean.

### Findings

**CR-1 — `src/backend/tests/Mustask.Api.IntegrationTests/StaticLoggerRegressionTests.cs:24-31` — major.**
`SharedHostLogger_CreatedAfterChildHostDisposed_StillWritesToConsole` fails on
the old wiring only if the shared host already exists. `MustaskApiFactory.InitializeAsync`
(`MustaskApiFactory.cs:132`) builds no host. When this class runs first in its
collection, or alone, the child is built and disposed first. `_factory.Services`
at `:39` then builds the shared host, which sets the static logger to a live
one, so the test passes. The regression guard is therefore order-dependent, the
same hazard the task removes. This also makes two prose claims false:
`qa.md:15` ("fails on the old code regardless of order") and the dev Log line
("fails on the old Program.cs").
Fix: make `_ = _factory.Services;` (or a request on `_factory.CreateClient()`)
the first statement of both tests, so the shared host is up before the child
exists. Verified above: with that line the test fails on the old wiring when it
runs alone.

**CR-2 — `StaticLoggerRegressionTests.cs:57-61` — major.**
`ChildHostDispose_DoesNotSilenceTheLoggerOfALiveSiblingHost` claims to pin "the
other half of the old bug (a disposed host closed the logger that a live host
was still writing through)". It cannot reproduce that case. On the old wiring
the child's dispose runs `CloseAndFlush` on whatever the static logger is at
that moment, which is the child's own logger. The shared host's logger is never
closed. The test was green on the old wiring alone, and with the shared host
built first it would pass as well. The live-logger case needs a second host
built after the one that is disposed.
Fix: build child A, then child B. Create the logger from B's `ILoggerFactory`,
dispose A, and assert that B's line arrives. On the old wiring the static logger
is B's at the dispose, so this goes red. The other fix is to drop the test and
its claim.

**CR-3 — `src/backend/tests/Mustask.Api.IntegrationTests/TokenLeakTests.cs:123, 194, 274, 347, 406, 447, 481` — major (prose claim).**
"Positive control: the request-log line must be there, or the DoesNotContain
checks below scan a silent logger." The request-log line goes through
`options.Logger`, which is pinned to the host (`Program.cs:202, 224`, and before
this task `Log.Logger` was captured at build). It was never the silenced line:
bug-002 review F-1 recorded the exposed runs as "request-log line only". So the
control stays green under the exact failure it names (a silent lazily created
`ILogger<T>`), and a reader relies on a guard that does not exist. `qa.md:19`
("a silent logger now fails the test instead of passing it") makes the same
claim.
Fix: either assert on a line written through a lazily created `ILogger<T>` of
the shared host, where one is reliably emitted (as `SessionLogoutEndpointTests`
does with `invalid_client`), or reword both places along these lines: "proves
the console capture is live; a silenced `ILogger<T>` is caught by
`StaticLoggerRegressionTests`". The reword is only true once CR-1 is fixed. A
prose-only fix needs a delta read and no QA.

### Behavioural claims checked

- `Program.cs:77-79` (without the flag the static is assigned on Build and closed on dispose) → matches the package behaviour, reproduced (old wiring + prebuilt shared host → red).
- `Program.cs:199-201` (request line through this host's DI logger, static untouched) → `Program.cs:80, 202, 224`.
- `SessionLogoutEndpointTests.cs:193-195` (no own host needed) → `Program.cs:80`; HEAD run green, positive `invalid_client` assertion intact.
- `StaticLoggerRegressionTests.cs:6-10` (pins the fix; runs alone) → `ApiTestCollectionFactory.cs:55`; "pins" → CR-1.
- `StaticLoggerRegressionTests.cs:26` ("on the old wiring this closed the static") → true, but only matters with the shared host prebuilt → CR-1.
- `StaticLoggerRegressionTests.cs:59-60` ("the other half of the old bug") → CR-2.
- `StaticLoggerRegressionTests.cs:33-34` (async swap under 10 ms, 500 ms wait) → bug-002 measurement; plausible, not re-measured.
- `TokenLeakTests.cs` positive-control comments ×7 → CR-3.
- `ApiTestCollectionFactory.cs:36-49` reason list (no log-entries reason) → `ErrorHandlingTests.cs:444-447`, `PurgeWorkItemEndpointTests.cs:286-290` (per-host DI provider).
- `src/backend/AGENTS.md:21-24` (serial rule without "asserts on log entries") → same anchors.
- `MustaskApiFactory.cs:186-190` (lock keeps shards off each other's DB) → unchanged code; the dropped Serilog clause is now moot (`Program.cs:80`).
- `qa.md:15, 19`, dev Log line "fails on the old Program.cs" → CR-1, CR-3.
- Direction 2: changed behaviour "host no longer sets or closes the static `Log.Logger`". Grepped `Log.Logger`, `Log\.(level|CloseAndFlush|ForContext)`, `preserveStaticLogger`, `StaticLoggerGuard`, `CloseAndFlush`, `static logger`, "log entries of a host" over `src/backend/**/*.cs`, `docs/`, `.claude/` and the three `AGENTS.md`. Hits outside this diff are only in merged history artifacts (bug-002, mod-001, mod-002, tech-047 review/qa/status, plan-backend; `docs/backlog.md:14` is this task's own queue line). They are records, so not a finding. No convention, runbook or `AGENTS.md` describes the old behaviour. Not swept: none.
