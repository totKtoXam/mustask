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

<!-- S/M: implementation plan, 3-10 lines. L: see plan-backend.md / plan-frontend.md -->

## Log

- 2026-11-05 [orchestrator] route confirmed by the human (question form, 2026-10-12): M, tech, plan in status.md, one QA, one review round, gate-3; E2E no (paused 2026-10-12); weekly 33% at start; card moved to worktree slow-task-execution-fedb18
- 2026-11-05 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/tech-006-serilog-static-logger-test-hosts
- 2026-11-05 [new-task] size mismatch: queue has size:S, human confirmed M

## Notes

Summary (from the queue line or an argument): Program.cs calls UseSerilog(writeToProviders: true) without preserveStaticLogger, so disposing any test child host runs Log.CloseAndFlush() and every ILogger<T> created on the shared host afterwards binds to a SilentLogger: log-line assertions pass vacuously or fail by test order (bug-002 fixed one test on its own host). Set preserveStaticLogger: true, take the request logger from DI instead of Log.Logger, and make the integration tests' log capture per host
