# tech-006-serilog-static-logger-test-hosts

- type: tech
- size: M
- branch: tech/006-serilog-static-logger-test-hosts
- stage: 06-code-review
- epic: platform/tests
- deps: bug-002
- gates: gate-3

## Gates

gate-3: pending

## Plan


1. Affected: `src/backend/Mustask.Api/Program.cs`; test code in `src/backend/tests/Mustask.Api.IntegrationTests` (`StaticLoggerGuard` and its 5 call sites, `SessionLogoutEndpointTests` workaround and comment, comments in `MustaskApiFactory`/`ApiTestCollectionFactory`, new `StaticLoggerRegressionTests`). No endpoints change, so no `api-contract.md` change.
2. Fix: `UseSerilog(..., preserveStaticLogger: true)`. The host logger is no longer assigned to the static `Log.Logger` and its dispose no longer runs `Log.CloseAndFlush()` on the shared logger.
3. `UseSerilogRequestLogging` gets its logger from DI (`app.Services.GetRequiredService<Serilog.ILogger>()`) instead of the captured static `Log.Logger`.
4. Log capture in tests is already per host (Console.Out redirect + each host's own logger). With the static no longer shared, `StaticLoggerGuard` is dead: remove it and its uses. The bug-002 child host in `SessionLogoutEndpointTests` is no longer needed: the test goes back to the shared host and still asserts `invalid_client` (a positive assertion, so it fails if the logger is silent).
5. Regression test: build and dispose a child host, then resolve a lazily created `ILogger<T>` on the shared host and assert its line reaches the captured console. It fails on the old code (static logger closed by the child's dispose).
6. Run: `Mustask.Api.IntegrationTests` classes `StaticLoggerRegressionTests`, `SessionLogoutEndpointTests`, `TokenLeakTests`, `LoggingTests`, `ProductionLoggingTests`, `ApiExplorerTests`, `ErrorHandlingTests` and the former guard users, plus `Mustask.Architecture.Tests`. No E2E; Testcontainers only.
<!-- S/M: implementation plan, 3-10 lines. L: see plan-backend.md / plan-frontend.md -->

## Log

- 2026-11-05 [orchestrator] route confirmed by the human (question form, 2026-10-12): M, tech, plan in status.md, one QA, one review round, gate-3; E2E no (paused 2026-10-12); weekly 33% at start; card moved to worktree slow-task-execution-fedb18
- 2026-11-05 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-006-serilog-static-logger-test-hosts
- 2026-11-05 [new-task] size mismatch: queue has size:S, human confirmed M
- 2026-11-05 [dev] stage 4 done: `UseSerilog(..., preserveStaticLogger: true)`, request logging takes `Serilog.ILogger` from DI, `StaticLoggerGuard` and its 6 uses removed, bug-002 own-host workaround in `SessionLogoutEndpointTests` reverted to the shared host, regression class `StaticLoggerRegressionTests` added (fails on the old Program.cs only when the shared host is built first: see fix round 1). Ran 128 integration tests of the affected classes + 82 architecture tests, green. Not done: positive log assertions in `TokenLeakTests` (still DoesNotContain-only, now over live loggers); `SerialClasses` entries kept. stage: 05-backend-qa

- 2026-11-05 [qa] backend QA done: positive controls (request path in the capture) added to 7 TokenLeakTests asserts, ErrorHandlingTests/PurgeWorkItemEndpointTests left SerialClasses and the AGENTS.md rule reworded, regression test confirmed to fail on the old Program.cs; 81+44 integration, 82 architecture green; no blocker/major. stage: 06-code-review
- 2026-11-05 [review] round 1 done (one pass incl. security): changes-requested, 3 major open (CR-1..CR-3 in review.md), 0 minor
- 2026-11-05 [dev] fix round 1 (review CR-1..CR-3): regression test builds the shared host first and fails on the old wiring alone and in order (probed: preserveStaticLogger false + request logger on Log.Logger); the sibling-host test could not fail on the old wiring and was dropped; TokenLeakTests control comments reworded; qa.md prose corrected. stage unchanged

- 2026-11-05 [review] round 2 (delta) done: approve, 0 blocker/major open, 3 minor (CR-4..CR-6, debts)

## Notes

Summary (from the queue line or an argument): Program.cs calls UseSerilog(writeToProviders: true) without preserveStaticLogger, so disposing any test child host runs Log.CloseAndFlush() and every ILogger<T> created on the shared host afterwards binds to a SilentLogger: log-line assertions pass vacuously or fail by test order (bug-002 fixed one test on its own host). Set preserveStaticLogger: true, take the request logger from DI instead of Log.Logger, and make the integration tests' log capture per host
