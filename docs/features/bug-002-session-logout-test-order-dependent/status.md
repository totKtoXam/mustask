# bug-002-session-logout-test-order-dependent

- type: bugfix
- size: S
- branch: bug/002-session-logout-test-order-dependent
- stage: complete
- epic: platform/tests
- deps: -
- gates: gate-3

## Gates

gate-3: approved · delegated by human 2026-10-04 (D-2) · local squash via scripts/finish-task.sh

## Plan

1. Reproduce: `dotnet test tests/Mustask.Api.IntegrationTests --filter FullyQualifiedName~SessionLogoutEndpointTests` — `Logout_UnexpectedKeycloakError_Returns503_AndLogsErrorCodeWithoutSecret` fails on `Assert.Contains` (the expected log line is not in the captured output) when the class runs together; the same test passes alone. `scripts/verify.sh` on mod/001-process-seed hit it twice in a row on 2026-10-04.
2. Find the shared state between the tests of that class (and the neighbouring Session tests): the suspect is log capture bound to a static logger or a sink shared across test hosts, so the line is written to another host's sink or to none. Bind the capture to the test's own host, or scope whatever is static per test.
3. The test proves the fix: the class run together fails before the change and passes after; the whole `Mustask.Api.IntegrationTests` project is run twice more to see it stays green.
4. `bash scripts/verify.sh` green, the command and the exit code into `## Log`.

## Decisions

- D-1 Route: bugfix, S, backend tests only (`tests/Mustask.Api.IntegrationTests`), no runtime code expected to change; gates: gate-3 only (s-route.md). QA in the light S form, then code review (stage 8) — the size is no reason for a weak review. E2E: not applicable, no UI and no runtime change.
- D-2 Delegation (human, 2026-10-04, chosen in chat: «Чиню сам сейчас»): the task is added to the 2026-10-01 series — the orchestrator decides gate-3 and merges via scripts/finish-task.sh. Reason: the order-dependent failure makes `scripts/verify.sh` red on branches that touch no backend code, and finish-task.sh refuses to merge on a red verify, so the whole series stalls on it.
- D-3 Gate-3 (delegated, D-2): the root cause stays in Program.cs and is not fixed here — the task's scope is the shared state between tests (the queue line), and the runtime change is a separate S tech task (tech-006-serilog-static-logger-test-hosts, queued in main) that also names the TokenLeakTests whose secret-leak guards pass vacuously while the static logger is silent. Review's out-of-scope note (the fake's invalid_client body carries no secret, so the guard cannot catch a raw-body log regression) is handed to tech-006 as well.

## Log

- 2026-10-04 [orchestrator] gate-3 approved (delegated, D-2); stage: complete; squash into main via scripts/finish-task.sh
- 2026-10-04 [orchestrator] gate-3 summary: fix is test code only (the logout runs on the test's own child host, so the lazily created RefreshGrantClient logger binds to a live Log.Logger); QA light pass, review approve with two prose minors CR-1/CR-2 fixed in ee9afcb; the root (Program.cs UseSerilog without preserveStaticLogger, TokenLeakTests' DoesNotContain guards degrading) is queued as tech-006 (main)
- 2026-10-04 [review] code review round 1: approve, 0 blocker/major open, 2 minor (CR-1, CR-2: the test comment's and QA's reason for "nothing else builds or disposes a host" is overstated), details in review.md; diagnosis re-verified on main's test file (class 6/7 red, exit 1), fixed class 4x 7/7; `bash scripts/verify.sh` exit 0, `RESULT: green`
- 2026-10-04 [qa] light QA (s-route step 3): pass, 0 blocker/major/minor, details in review.md. Reproduced on main's test file: class filter 6/7 red (exit 1), the single test green; fixed: class 3x 7/7 and whole `Mustask.Api.IntegrationTests` 2x 346/346, all exit 0; capture of the fixed test verified live (holds the `invalid_client` line and the HttpClient lines, so the secret assertion is not vacuous); `bash scripts/verify.sh` exit 0, `RESULT: green`. E2E question answered by D-1 (not applicable). Out of scope, for the Program.cs follow-up (review.md F-1): six `TokenLeakTests` session tests assert `DoesNotContain` only on shared-host output; four of them captured the request-log line and nothing else in a full run
- 2026-10-04 [dev] fixed in 7019ff9, test code only (`SessionLogoutEndpointTests`), runtime code NOT changed. Cause: `UseSerilog` without `preserveStaticLogger` makes every `ILogger<T>` bind to the process-wide `Serilog.Log.Logger` when it is created, and disposing any host runs `Log.CloseAndFlush()`, which swaps that static for a `SilentLogger`. `RefreshGrantClient` is a lazy singleton, so on the shared host its logger was bound at the first logout/token request: after `Logout_DiscoveryUnavailable` (own child host, disposed first in this class) it got the silent logger and the `invalid_client` line vanished; alone, or when an earlier class had already resolved it, the line survived (the request-log line survived either way: Program.cs pins it). Fix: the test logs out on a host of its own (`WithWebHostBuilder`), so the logger binds to that host's `Log.Logger`. Proof: class run 3x green (7/7); before the fix 3x red (6/7, same assert) and single test green; fix reverted again: red; whole `Mustask.Api.IntegrationTests` 2x green (346/346). `bash scripts/verify.sh` exit 0, `RESULT: green`. Open: same hazard remains for any other test that asserts on a log line from a lazily created `ILogger<T>` on the shared host; real root is Program.cs `UseSerilog` (orchestrator to decide a follow-up)
- 2026-10-04 [orchestrator] plan and route recorded (D-1), delegation extended by the human to this task (D-2); queue line removed from main (49cd732)
- 2026-10-04 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/bug-002-session-logout-test-order-dependent

## Notes

Summary (from the queue line or an argument): SessionLogoutEndpointTests.Logout_UnexpectedKeycloakError_Returns503_AndLogsErrorCodeWithoutSecret fails when its class runs together and passes alone (order-dependent, seen on main e6f78a5); find the shared state and isolate it
