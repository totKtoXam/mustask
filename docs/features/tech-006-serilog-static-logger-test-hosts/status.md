# tech-006-serilog-static-logger-test-hosts

- type: tech
- size: M
- branch: tech/006-serilog-static-logger-test-hosts
- stage: 04-backend
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

## Notes

Summary (from the queue line or an argument): Program.cs calls UseSerilog(writeToProviders: true) without preserveStaticLogger, so disposing any test child host runs Log.CloseAndFlush() and every ILogger<T> created on the shared host afterwards binds to a SilentLogger: log-line assertions pass vacuously or fail by test order (bug-002 fixed one test on its own host). Set preserveStaticLogger: true, take the request logger from DI instead of Log.Logger, and make the integration tests' log capture per host
