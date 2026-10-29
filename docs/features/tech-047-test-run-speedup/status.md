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

- 2026-10-29 [orchestrator] route M (tech: dev → one QA pass → one code review → gate-3 → PR), gates: gate-3 only (no spec, no screens). Started on the human's «⭐ Сейчас, отдельно» (2026-10-29), economy mode lifted until 2026-10-30 00:00 UTC («можно везде включать maximum ultracode»); gate-3 stays the human's
