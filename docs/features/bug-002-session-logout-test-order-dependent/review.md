# bug-002-session-logout-test-order-dependent — review

## QA (light, S route)

Verdict: **pass**. Defects in this change: none (0 blocker, 0 major, 0 minor).
One out-of-scope finding about neighbouring tests is recorded below as F-1; it
is pre-existing, outside the diff, and is not a defect of this fix.

Scope: `git diff main...HEAD` — `SessionLogoutEndpointTests.cs` (+13/-1, test
code only) and this folder's `status.md`. No runtime file is touched.

### Plan coverage (status.md, Plan 1-4)

1. **Reproduce.** Done independently, on `main`'s version of the test file
   (`git checkout main -- <file>`, run, `git checkout HEAD -- <file>`):
   - class filter `FullyQualifiedName~SessionLogoutEndpointTests` → 6/7, the
     failure is `Logout_UnexpectedKeycloakError_Returns503_AndLogsErrorCodeWithoutSecret`
     on `Assert.Contains`: `Not found: "invalid_client"`, captured string starts
     with the request-log line `[ERR] HTTP POST /api/session/log...` — exit 1;
   - the same test alone → 1/1 green, exit 0. Order dependence confirmed.
2. **Shared state.** Cause as the dev recorded it (a lazily created
   `ILogger<T>` binds to the process-wide `Serilog.Log.Logger`, which
   `Log.CloseAndFlush()` of any disposed host replaces with a silent logger).
   Checked against the code: `Program.cs:66-69` (`UseSerilog(..., writeToProviders: true)`,
   no `preserveStaticLogger`) and `Program.cs:187` (the request-log line is
   pinned to the host's own logger — which is why that one line survives in
   the failing run, as seen above).
3. **The test proves the fix.** Fixed version, class filter ×3 → 7/7 each,
   exit 0 ×3. Before the fix: red (above). Whole `Mustask.Api.IntegrationTests`
   ×2 → 346/346 each, exit 0.
4. **verify.sh** → see "Commands run".

### Regression checks around the change

- **The secret assertion is not vacuous.** Temporarily dumped the captured
  `output` of the fixed test to a scratch file (reverted afterwards, tree
  clean). It holds 11 lines, among them the asserted one:
  `[ERR] Keycloak rejected a back-channel logout with 401 invalid_client — ...`,
  plus the `System.Net.Http.HttpClient` lines for the logout POST
  (`Sending HTTP request POST http://keycloak.invalid/.../logout`,
  `Received HTTP response headers ... - 401`), the `WRN` line with the
  `HttpRequestException` stack trace from `RefreshGrantClient.LogoutAsync`,
  and the request-log line. So `Assert.DoesNotContain(TestKeycloakClientSecret, output)`
  now runs over every line the logout path produces, including the HttpClient
  and `RefreshGrantClient` ones — the ones the silent logger used to swallow.
- **Nothing builds a host between sign-in and the logout.** Every class that
  touches `WebApplicationFactory` / `WithWebHostBuilder` carries
  `[Collection(MustaskApiCollection.Name)]` (grep over the whole test project,
  `ProductionCorsTests`'s own `new WebApplicationFactory<Program>()` included),
  and a collection runs sequentially; no test class outside the collection
  builds a Serilog-configured (Program) host — the slim probe hosts in
  `DictionaryListQueryNormalizationTests` and `GridifyQueryBindingTests` never
  touch `Serilog.Log.Logger`. Between `SignInAsync` and the request on
  `logoutClient` the test itself builds exactly one host (its own). This check
  covered host builds, not asynchronous disposals — see CR-1 in the code review
  below for the timing margin.
- **Cross-host cookie.** The sign-in cookie, made on the shared host, is
  accepted by the child host (the logout reaches Keycloak's fake and gets the
  401 `invalid_client`) — the same pattern `Logout_DiscoveryUnavailable_...`
  already relies on.
- **Other Session tests and `TokenLeakTests`**: green in the class runs and in
  the four whole-project runs (346/346 each).
- **Side effect of the fix:** one more host is built and disposed in the run,
  so one more `Log.CloseAndFlush()` swaps the static for a silent logger. The
  class already had that (`Logout_DiscoveryUnavailable_...`) and so do at
  least ten other classes; the net change in exposure is nil in practice (see F-1 — the
  window is opened by any child-host dispose, not by this one).

### F-1 (out of scope, pre-existing, for the follow-up tech task) — other tests exposed to the same silent-logger window

Not a defect of this diff. The dev's open point is confirmed; the list below is
for the queue line. `Program.cs` is not touched here.

Classification rule: a log line is safe if it is written by (a) the request
logging middleware (pinned to the host's own logger, `Program.cs:187`),
(b) a logger created while the test's own child host is the static one
(startup lines, per-test child hosts, no other host built in between), or
(c) an eager singleton of the shared host (created at its start, before any
child host exists). It is exposed if it is written through an `ILogger<T>` of
the **shared** host created lazily — `RefreshGrantClient`, the
`IHttpClientFactory` handler loggers, per-request auth handlers, endpoint
`ILogger<T>` parameters — after any child host has been disposed.

Empirical check: temporarily dumped `output` in every `TokenLeakTests` capture
and ran the whole project once (reverted, tree clean). Captured output per test,
in this run's order:

| Test (`TokenLeakTests`) | Host | What the capture held |
|---|---|---|
| `Token_SuccessfulRenewal_DoesNotLeakRefreshOrIdTokenOrClientSecret` | shared | request-log line only |
| `Callback_Failed_DoesNotLeakAuthorizationCodeOrClientSecret` | shared | request-log line only |
| `Callback_SuccessfulExchange_DoesNotLeakCodeRefreshTokenOrClientSecret` | shared | request-log line only |
| `Logout_Success_DoesNotLeakRefreshTokenOrClientSecret` | shared | request-log line only |
| `Token_InvalidGrant_DoesNotLeakRefreshTokenOrClientSecret` | shared | request-log line + the `session.expired` business-error line; no HttpClient / `RefreshGrantClient` line |
| `Logout_KeycloakUnavailable_DoesNotLeakRefreshTokenOrClientSecret` | shared | the endpoint's `WRN` + stack trace + request-log line; no `System.Net.Http.HttpClient` line, which a live logger writes (`Start processing HTTP request ...`, as in the fixed test) |
| `Unauthorized_...` / `Forbidden_TokenDoesNotLeakIntoResponseOrLogs` | shared | request-log line only; JwtBearer lines sit under the `Microsoft.AspNetCore: Warning` override, so a live logger would add nothing — weak for a different reason, not this hazard |
| `ServiceUnavailable_TokenDoesNotLeakIntoResponseOrLogs` | own child host | full startup + realm-readiness `ERR` + request-log lines — live, not exposed |

Exposed — `DoesNotContain`-only, so they pass vacuously whenever the window is
open (they never assert that the output contains anything):

- `TokenLeakTests`: `Token_SuccessfulRenewal_...`, `Callback_Failed_...`,
  `Callback_SuccessfulExchange_...`, `Logout_Success_...` (observed
  request-line-only in the run above), and, partially, `Token_InvalidGrant_...`
  and `Logout_KeycloakUnavailable_...`. The six are the ones that go through
  `RefreshGrantClient`, the HttpClient pipeline or the OIDC handler on the
  shared host. These are the secret/token-leak guards; with the window open the
  only log text they scan is the request-log line, which carries a path and no
  query, so they cannot fail on a leak through the lines that matter.
  The order that decides which are blind is xUnit's, and is not part of the
  test code — it can differ per run.

Not exposed (checked):

- Positive `Assert.Contains` on a log line: `ApiExplorerTests` (4 tests,
  startup lines on a child host of the test's own), `LoggingTests` and
  `ProductionLoggingTests` (request-log line, pinned), `RealmReadinessTests.ForeignIssuerInBackground_AfterReady_...`
  (`RealmReadinessMonitor` is an eager hosted service of the shared host,
  bound at its start), `ErrorHandlingTests` (2 tests with a
  `CapturingLoggerProvider` on a child host of their own).
- `LoggingTests.GetHealth_RequestCompletes_IsNotWrittenToConsole` and
  `ProductionLoggingTests.GetHealth_ProductionRequestCompletes_IsNotWrittenToConsole`
  (`DoesNotContain("responded")`): the request-log line is pinned, and the
  sibling test in the same class asserts it is written, so they are not vacuous.
- `BusinessExceptionHandlerTests`, `GlobalExceptionHandlerTests`,
  `MalformedRequestExceptionHandlerTests`, `OperationCanceledExceptionHandlerTests`,
  `RefreshGrantClientTests` use test doubles for the logger, not `Serilog.Log`.

Suggested queue line: the root is `Program.cs` `UseSerilog(...)` without
`preserveStaticLogger: true` (and the ambient `Log.Logger` that
`Log.CloseAndFlush()` silences on every host dispose); the tests named above
are the ones whose result depends on it.

### E2E

Answered by status.md D-1: not applicable (no UI, no runtime change). Not asked
again; no Playwright spec written or run.

### Commands run

All from `src/backend` unless noted; Testcontainers brought up by the tests
themselves, no stand started.

- `git checkout main -- .../SessionLogoutEndpointTests.cs`, then
  `dotnet test tests/Mustask.Api.IntegrationTests --filter "FullyQualifiedName~SessionLogoutEndpointTests"`
  → 6/7, `Logout_UnexpectedKeycloakError_...` FAIL, exit 1
- same file, `--filter "FullyQualifiedName~SessionLogoutEndpointTests.Logout_UnexpectedKeycloakError"`
  → 1/1, exit 0
- `git checkout HEAD -- <file>` (tree clean afterwards)
- class filter on the fixed file, ×3 → 7/7, exit 0 ×3
- class filter with a temporary dump of the captured output (reverted) → 7/7,
  exit 0; the dump holds the `invalid_client` line
- whole `tests/Mustask.Api.IntegrationTests`, ×2 → 346/346, 346/346 (run 1
  exit 0; run 2 the `Пройден!` line, the batch ended with exit 0)
- whole project once more with the temporary `TokenLeakTests` dump (reverted) →
  346/346, exit 0
- `bash scripts/verify.sh` (repository root, on the committed fix) →
  `RESULT: green`, exit 0; it ran the backend build and every test project
  (Shared 53, Domain 38, Application 39, Architecture 68, Contracts 48,
  Api.IntegrationTests 346, Infrastructure 74), all passed

## Code review — round 1

Side: backend (test code only). Scope: `git diff main...bug/002-session-logout-test-order-dependent`
(merge base c48b565): `SessionLogoutEndpointTests.cs` +13/-1, this folder's
`status.md` and `review.md`. No runtime file is touched.

Verdict: **approve**. Open: 0 blocker, 0 major, 2 minor (CR-1, CR-2).

The diagnosis holds and the fix is correct and minimal: the test now logs out
on a child host it builds itself (`SessionLogoutEndpointTests.cs:202-203, 217`), so
`RefreshGrantClient`'s lazily created `ILogger<T>` binds to that host's live
logger. The client secret check (`:230`) now really runs over the logger's output:
the positive `Assert.Contains("invalid_client", ...)` at `:229` runs on the same
capture, and `invalid_client` can come only from `RefreshGrantClient.LogUnexpectedFailure`
(`RefreshGrantClient.cs:147, 194-198`). The endpoint's warning carries an
`HttpRequestException` whose message has no error code (`:149-152`), the
HttpClient lines carry a URI and a status, and the request-log line carries a path
and a status. A silent logger makes `:229` fail, so while the test is green the
capture includes that logger's output. Plan 1-4 is covered (see "Commands run"
below).

### Findings

**CR-1 — `src/backend/tests/Mustask.Api.IntegrationTests/Features/Session/SessionLogoutEndpointTests.cs:199-201` — minor.**
The comment's last sentence gives the wrong reason for the invariant the fix
depends on: "nothing else builds or disposes a host before the request below,
because every host-building class is in MustaskApiCollection and a collection
runs sequentially". Both halves are wrong:
(a) Two classes outside the collection do build hosts:
`DictionaryListQueryNormalizationTests.cs:189-196` and
`GridifyQueryBindingTests.cs:45-47` (`WebApplication.CreateSlimBuilder()` +
TestServer). They run in parallel with the collection. They are harmless only
because they never call `UseSerilog`, so they never touch `Serilog.Log.Logger`.
(b) Running sequentially does not order host *disposal*. A
`WebApplicationFactory` host runs its `Log.CloseAndFlush()` asynchronously,
after `Dispose()` has returned. This round measured it with a temporary probe
(deleted afterwards): 15 cycles of building a child and disposing it. In 11 of
them `Log.Logger` was still the child's logger right after `Dispose()`, and it
became `Logger.None` 0.05-8.6 ms later. So an earlier test's dispose (for example
`Logout_DiscoveryUnavailable_...`'s) can land after this test has started.
Collection ordering does not stop it from landing between this host's build and
the request. The margin does: `SignInAsync`'s two in-process round trips run
before `:202`. If it did land there, it would silence the static *and* dispose
this host's Serilog logger (`CloseAndFlush` exchanges whatever is current).
Nothing fails today: the class ran 4x green and the whole project ran green in this round. It is
minor because the sentence overstates and no action in this task changes: the
queued tech-006 (main, abeb23a) removes the dependency on the static logger
(`preserveStaticLogger: true`). A reader who copies the pattern into a test that
builds its host first thing does inherit a millisecond-scale race.
Proposed fix: replace the sentence with the narrower true statement, along
these lines. "Only hosts built from Program call UseSerilog, and every class that
builds one is in MustaskApiCollection, which runs sequentially. The slim probe
hosts outside it never touch Serilog.Log.Logger. A host swaps the static
asynchronously, shortly after Dispose() returns. The sign-in above gives an
earlier test's dispose time to land before this host is built. tech-006 removes
the dependency."
Status: resolved — the comment now carries the narrower statement above (`[orchestrator]` commit, see status.md Log).

**CR-2 — `docs/features/bug-002-session-logout-test-order-dependent/review.md:46-53` (QA section) — minor.**
"no test class outside the collection builds a host or references Serilog"
contradicts the same two files as CR-1(a). The "builds a host" half is false;
the "Serilog" half holds. "Nothing builds a host between sign-in and the logout
... The comment in the test states this correctly" inherits CR-1(b): the QA
check covered builds, not asynchronous disposals. Same root as CR-1. Nothing
anyone would do differs.
Proposed fix: narrow the sentence to "no test class outside the collection
builds a Serilog-configured (Program) host", and drop the endorsement of the
comment, or fix it together with CR-1.
Status: resolved together with CR-1 — the QA sentence is narrowed to Serilog-configured hosts and no longer endorses the comment's timing claim.

Out of scope, not a finding against this diff (pre-existing, for tech-006 or the
human): the fake's `InvalidClient` body
(`KeycloakTokenEndpointResponder.cs:73-74`) carries no secret. So
`DoesNotContain(TestKeycloakClientSecret, ...)` at `:230` catches only code that
logs the secret itself. It does not catch a regression to logging Keycloak's raw
body, which is the risk `RefreshGrantClient.cs:191-192` names. A fake whose
`error_description` echoes the secret would make the guard cover that path too.

Conventions (`docs/conventions/backend.md` § Tests, § Miscellaneous; `git.md`
§ Commits): integration test through WebApplicationFactory with Testcontainers,
the external Keycloak stubbed. Test name unchanged, English comment, no secret
literal added, and the build passes with warnings as errors. No convention
finding.

### Behavioural claims in prose — pass

Direction 1 (prose the diff adds):

- Test `:193` "the line ... is written through RefreshGrantClient's ILogger<T>"
  → `RefreshGrantClient.cs:147, 194-198`. No other source of `invalid_client`
  in the capture (see the verdict paragraph).
- Test `:193-194` "Serilog binds an ILogger<T> to ... Serilog.Log.Logger at the
  moment it is created" → `Program.cs:66-69` (no `preserveStaticLogger`).
  Probe: a logger created on the shared host after a child host was disposed
  reports `IsEnabled(Error) == false`, and the factory is
  `Serilog.Extensions.Logging.SerilogLoggerFactory`.
- Test `:195` "this lazily-resolved singleton, the first logout/token request"
  → `SessionAuthenticationSetup.cs:80` (`AddSingleton<RefreshGrantClient>`).
  Its only consumers are `SessionEndpoints.cs:97` (token) and `:229` (logout).
- Test `:195-196` "every host disposed anywhere in the run replaces that static
  with a silent logger" → probe, 15/15 ended at `Logger.None`. The swap is
  asynchronous to `Dispose()`; the timing part is CR-1.
- Test `:196-198` "swallowed or not depending on whether an earlier test
  (Logout_DiscoveryUnavailable_...) had disposed a host by then" → this round,
  on main's version of the test: the class was red 6/7; the pair
  DiscoveryUnavailable + target was red 1/2; the class without
  DiscoveryUnavailable was green 6/6.
- Test `:199` "a host of this test's own ... its loggers bind to its own
  Log.Logger" → `:202-203, 217`. Probe: building a child moves `Log.Logger` to
  the child's logger.
- Test `:199-201` "nothing else builds or disposes a host before the request
  below, because ..." → **CR-1**.
- status.md Plan 1 (fails together, passes alone) → reproduced: 6/7,
  `Not found: "invalid_client"`, exit 1. "verify.sh on mod/001 hit it twice" is
  a historical record and was not re-checked.
- status.md D-1 "no runtime code" → numstat: only the test file and this folder.
- status.md D-2 "finish-task.sh refuses to merge on a red verify" →
  `scripts/finish-task.sh:184`.
- status.md `[dev]` cause, mechanism and fix → `Program.cs:66-69`, the probe,
  `SessionAuthenticationSetup.cs:80`, and the runs above. "the request-log line
  survived either way: Program.cs pins it" → `Program.cs:187, 209`, and the red
  capture starts `"[.. ERR] HTTP POST /api/session/log"`. "runtime code NOT
  changed" → numstat. "when an earlier class had already resolved it, the line
  survived" follows from binding at creation (probe) and was not run separately.
  The proof counts were partly re-run (class 4x green; whole project 1x 346/346
  via verify.sh).
- status.md `[qa]` → the repro and fixed runs as above. "capture verified live"
  → `:229`. The HttpClient lines in it → `appsettings.json:30-35` (Default
  Information, no `System.Net.Http` override). "six TokenLeakTests ... assert
  DoesNotContain only on shared-host output" → output assertions are
  DoesNotContain only (`TokenLeakTests.cs:126, 194, 271, 341, 397, 435`), and the
  only child host is at `:68` (`ServiceUnavailable_...`). "four of them captured
  the request-log line and nothing else" is a QA run observation that depends on
  run order and was not re-run.
- review.md QA `:9-10` (+13/-1, no runtime file) → numstat 13/1.
  `:14-20` → same assert, and the same capture start in this round's red run.
  `:21-27` → `Program.cs:66-69, 187`. `:28-30` → partly re-run.
- review.md QA `:35-45` "capture holds 11 lines ... DoesNotContain now runs over
  every line the logout path produces" → liveness anchored at `:229`, the
  HttpClient lines at `appsettings.json:30-35`. The 11-line count was not re-run.
- review.md QA `:46-53` → **CR-2**.
- review.md QA `:54-57` cross-host cookie → the 503 at `:228` and the
  `invalid_client` at `:229` require the back-channel call, and that call needs
  the cookie's refresh token (`SessionEndpoints.cs:235-237, 264-266`).
- review.md QA `:58-59` → verify.sh in this round: 346/346.
- review.md QA `:60-64` "one more CloseAndFlush ... at least ten other classes"
  → 14 test files build `WithWebHostBuilder`/`WebApplicationFactory` hosts, 13
  of them other than this one. "nil in practice" is hedged judgement, and the
  async timing adds the same ms-scale exposure every existing child host has.
- review.md F-1 `:66-129` → the classification rule follows the mechanism
  above. The exposed list is anchored structurally (the TokenLeakTests lines
  above). The per-test table is a QA run observation, order-dependent by its own
  statement (`:107-108`), and was not re-run. Of the "Not exposed" list, only
  the request-log pinning (`Program.cs:187, 209`) was re-checked. The rest of
  that list (ApiExplorerTests, RealmReadinessTests, ErrorHandlingTests and the
  handler unit tests) was not re-verified in this round. It is a named gap that
  belongs to tech-006.

Direction 2 (behaviour the diff changes: which host
`Logout_UnexpectedKeycloakError_Returns503_AndLogsErrorCodeWithoutSecret`
logs out on; test behaviour only, no runtime behaviour changes):

- Grepped `Logout_UnexpectedKeycloakError`, `SessionLogoutEndpointTests`,
  `preserveStaticLogger`, `CloseAndFlush`, `SilentLogger`, `bug-002` and
  `session-logout-test-order` over `docs/`, `.claude/`, `AGENTS.md`,
  `CLAUDE.md`, `src/backend/AGENTS.md`, `src/frontend/AGENTS.md` and `scripts/`,
  excluding this task's folder. One hit: `docs/backlog.md:15` on the branch,
  the queue line describing the bug. It is stale only because the branch
  predates 49cd732, which took the line off main. The diff does not touch
  `docs/backlog.md`, so the squash keeps main's copy, and no action is needed.
  Main's tech-006 line (abeb23a, read with `git show main:docs/backlog.md`)
  describes the `Program.cs` behaviour consistently with this round's probe,
  and it points at F-1 above.
  Not swept: `Program.cs:166-186`'s comment on the static logger. It describes
  behaviour this diff does not change, and it belongs to tech-006.

### Commands run (this round)

From the repository root. Testcontainers were started by the tests; no stand
was started; no background process was left running.

- `git checkout main -- .../SessionLogoutEndpointTests.cs`, then
  `dotnet test src/backend/tests/Mustask.Api.IntegrationTests --filter "FullyQualifiedName~SessionLogoutEndpointTests"`
  → 6/7, `Logout_UnexpectedKeycloakError_...` FAIL on `Assert.Contains`,
  `Not found: "invalid_client"`, exit 1.
- Same file, filter `...SessionLogoutEndpointTests&FullyQualifiedName!~Logout_DiscoveryUnavailable`
  → 6/6, exit 0.
- Same file, filter for the pair DiscoveryUnavailable + UnexpectedKeycloakError
  → 1/2, the target FAIL, exit 1.
- `git checkout HEAD -- <file>`; tree clean.
- Fixed file, class filter, 4 runs → 7/7, exit 0 each.
- A temporary probe test (`Serilog.Log.Logger` identity around building and
  disposing a child host; 3 runs; deleted, tree clean) → the results quoted in
  CR-1 and the pass above.
- A mutation run (a secret written into the asserted log line) was not
  performed in this round. Liveness rests on `:229`, as argued above.
- `bash scripts/verify.sh` → `RESULT: green`, exit 0. Backend build with 0
  warnings, then Domain 38, Shared 53, Architecture 68, Application 39,
  Contracts 48, Api.IntegrationTests 346, Infrastructure 74, all passed. No
  frontend bucket ran, because no frontend files changed.
