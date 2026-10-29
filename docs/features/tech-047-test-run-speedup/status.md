# tech-047-test-run-speedup

- type: tech
- size: M
- branch: tech/047-test-run-speedup
- stage: 04-backend
- epic: platform/ci
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

1. Baseline: time per bucket of `VERIFY_FULL=1 bash scripts/verify.sh` (Api.IntegrationTests, Infrastructure.Tests, the rest of dotnet, Vitest), containers started, the slowest test classes.
2. One PostgreSQL per test assembly, per-test isolation that fits the existing fixtures.
3. xUnit parallelism across collections where isolation allows.
4. Vitest pool/threads/isolate: a safe speed-up only.
5. Measure after; before/after in this Log and in the PR body. Test semantics unchanged, nothing deleted or skipped.

## Decisions

- E2E = NO (standing answer since 2026-10-12).

## Log

- 2026-10-29 [orchestrator] baseline on origin/main 2c6dbacb, this machine (22 logical cores), each project alone in turn (`dotnet test <project> --no-build`, then `npx vitest run`): Domain 33 s, Application 11 s, Shared 4 s, Contracts 7 s, Architecture 8 s; Infrastructure.Tests 532 s (440 green; 224 s of it is test time, the rest container starts and migrations — the assembly ran fully serial and 3 classes started a container per test); Api.IntegrationTests 1 715 s (3 094 tests in one serial collection, one container; 1 timing failure on main: WorkItemFilterVolumeTests 2 s budget); Vitest 1 969 s (5 067 tests on 4 workers; 2 failures on main, both 60 s timeouts: WorkItemFormPage.sections, WorkItemsPage.keyword). PostgreSQL containers started by the backend run: 48. Slowest Api classes: WorkItemKeywordEndpointTests 96 s, WorkItemAssigneeFilterEndpointTests 80 s, CreateChildEndpointTests 68 s, BoardChecklistsEndpointTests 62 s
- 2026-10-29 [orchestrator] route M (tech: dev → one QA pass → one code review → gate-3 → PR), gates: gate-3 only (no spec, no screens). Started on the human's «⭐ Сейчас, отдельно» (2026-10-29), economy mode lifted until 2026-10-30 00:00 UTC («можно везде включать maximum ultracode»); gate-3 stays the human's
- 2026-10-29 [orchestrator] after, same machine, each project alone: Infrastructure.Tests 532 s → 163 s (440 green; one container, a template copy per fixture, classes in parallel, the 5 environment-variable classes in one serial collection); Api.IntegrationTests 1 715 s → 649 s (3 076 passed, 18 skipped, 0 failed; 8 shards over copies of one migrated template, 23 process-wide-state classes and every `*VolumeTests` in a serial collection after them; 12 shards gave 654 s, so the default stays at min(cores/2, 8)); Vitest 1 969 s → 986 s (maxWorkers clamp 4 → 8; 5 060 passed, 1 failed — the WorkItemsPage.keyword 60 s timeout that also fails on main, green alone in 33 s). PostgreSQL containers started by the backend run: 48 → 2. A 4-core CI runner gets 2 shards and 3 Vitest workers (unchanged)
- 2026-10-29 [code-reviewer] review round 1 over origin/main...f3c523e7: changes-requested — 1 major open (F1, prose only: delta read, no QA), 8 minors; findings in review.md
- 2026-10-29 [orchestrator] review round 1 fixed: F1, F8 comments point at SerialClasses / per-shard database; F2 PushSecretAuditQaTests dropped from SerialClasses (names no collection); F3 count 23; F4 a factory uncounts only if it counted; F5, F6 comments softened; F7 AGENTS.md lists all six reasons (no guard test: the list is checked by review); F9 dead test reference removed
- 2026-10-29 [orchestrator] full verify on this machine (shared with other sessions running tests): Vitest green (5 061 passed); backend failures only under contention — DenyAllRuntimeTests once (green alone and in the next two runs), *VolumeTests budgets (1.2–3 s against 1–2 s) and, once, Npgsql timeouts in Infrastructure.Tests. Added: shard databases dropped when their factory is done, Infrastructure.Tests at most 4 parallel threads, test connection strings with 60 s open / 120 s command timeouts. CI on a clean runner is the arbiter for the budgets
- 2026-10-29 [qa-engineer] QA pass over origin/main...d6080e77 (infra only, E2E = NO): no blocker, no major, 2 minor doc debts (AGENTS.md omits the default shard count and what SHARDS=1 does to the serial classes; one over-long line); test counts unchanged (2148 test attributes before and after, Infrastructure 440), isolation audit clean, `MUSTASK_API_TEST_SHARDS=1` filtered run 296 passed; full suites and timing budgets left to CI; details in qa-report.md
