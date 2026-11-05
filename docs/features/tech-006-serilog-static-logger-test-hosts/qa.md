# QA — tech-006-serilog-static-logger-test-hosts

## Backend

No spec; the requirement is the Notes summary. No endpoint changes, so no api-contract check.

### Test plan (written before reading the implementation)

| Requirement | Check |
|---|---|
| A disposed child host must not silence loggers of the shared host created after it | `StaticLoggerRegressionTests` (1 test, after fix round 1); must fail on the old wiring |
| Request logging does not depend on the static logger | Request-log line present in captures (`LoggingTests`, `TokenLeakTests`, `SessionLogoutEndpointTests`) |
| Log capture is per host | Classes using `CapturingLoggerProvider` (`ErrorHandlingTests`, `PurgeWorkItemEndpointTests`) need no serialization |
| Log-line assertions are real | `DoesNotContain`-only leak guards need a positive control |
| Order independence | Affected classes pass; regression test fails on the old code when the shared host is built first (fix round 1) |

### Decisions on the points the developer left open

1. `TokenLeakTests` (6 session tests + the shared `AssertNoLeakAsync`): added a positive control to each, `Assert.Contains(<request path>, output)` before the `DoesNotContain` loops. The request-log line carries the path, so the control proves only that the console capture is live; it is pinned to the host and was never the silenced line (fix round 1: a silenced `ILogger<T>` is caught by `StaticLoggerRegressionTests`, not by these controls). Cheap; the HttpClient-pipeline line was not asserted (it sits under a log-level override that varies, so it would be brittle).
2. `SerialClasses`: the rule "asserts on log entries of a host it builds" is no longer true. `ErrorHandlingTests` and `PurgeWorkItemEndpointTests` capture through a `CapturingLoggerProvider` registered in their own host's DI: no console, no environment, no static. Removed both from `SerialClasses` and the phrase from `src/backend/AGENTS.md` (root `AGENTS.md` never carried it). Both classes pass (44 tests with the other filtered classes' run below).
3. Regression test spot check: temporarily set `preserveStaticLogger: false` and the request logger back to `Log.Logger`: the `StaticLoggerRegressionTests` tests fail (at the time two; one after fix round 1); restored (tree has no Program.cs change).

### What ran

| Command (filter on `Mustask.Api.IntegrationTests`) | Result |
|---|---|
| `TokenLeakTests` + `StaticLoggerRegressionTests`, after the positive controls | 11/11 |
| `TokenLeakTests`, `SessionLogoutEndpointTests`, `LoggingTests`, `ErrorHandlingTests`, `PurgeWorkItemEndpointTests`, `ApiExplorerTests`, `StaticLoggerRegressionTests` | 81/81 |
| Same two regression+TokenLeak classes on the old Program.cs (temporary) | 2 failed (the regression tests), 9 passed |
| `ErrorHandlingTests` + `PurgeWorkItemEndpointTests`, after leaving `SerialClasses` | 44/44 |
| `Mustask.Architecture.Tests` | 82/82 |

Not run: full `verify.sh` (CI), E2E (standing answer NO).

### Defects

- None blocker or major.
- Minor (fixed here): `TokenLeakTests` were vacuous on a silent logger (positive controls added); stale `SerialClasses` entries and AGENTS.md wording.
- Minor (left open, debt): the two `ErrorHandlingTests`/`PurgeWorkItem` classes now run in shards; a timing flake from that was not seen in one run, only the full CI shard run exercises it.

### Verdict

No open blocker or major. Backend QA passed.

### Fix round 1 (developer, after code review)

Review CR-1..CR-3 were right about the prose above. The regression test passed on the old wiring when it ran first or alone, because the shared host did not exist yet when the child was disposed; the remaining test now builds the shared host first. The second test (a live sibling host) could not fail on the old wiring even with an A/B host pair, so it was dropped with its claim. The `TokenLeakTests` positive controls only prove that the console capture is live. Probes on the old wiring: the remaining test fails alone and in class order; `Logout_UnexpectedKeycloakError_...` in `SessionLogoutEndpointTests` goes red with it (developer's probe, not reproduced by review).
