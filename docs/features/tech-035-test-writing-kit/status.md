# tech-035-test-writing-kit

- type: tech
- size: M
- branch: tech/035-test-writing-kit
- stage: 04-backend
- epic: platform/tests
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

<!-- M: the backend developer writes the plan at stage 4 -->

## Log

- 2026-11-09 [orchestrator] started from the parent orchestrator session as a lane parallel to mod-019. The human confirmed «⭐ Подтвердить M»: tech, M, Dev (backend → frontend) → one QA pass → one code review (Opus) → gate-3 → PR. Size grounds: block 0 — test infrastructure behaviour changes; block 1 clean (no migration, no new project, no ADR, no auth/secrets, no external exchange, no contract change); 2.1 not S (>5 files, backend + frontend test layers + docs); 2.3 → M. Block 3: no regulatory basis, no migration, nothing beyond the line. E2E: the standing answer is NO (part split off to tech-036).
- 2026-11-09 [orchestrator] binding: this session's worktree `.claude/worktrees/unruffled-banach-65f877`, its branch renamed to `tech/035-test-writing-kit` and reset to `origin/main` `d6fb8e0f`; new-task.sh was not run (it makes a worktree this session cannot use).

## Notes

Summary (from the queue line): Test-writing kit, phase 1, for new tests only: docs/conventions/testing.md (which layer owns which check, the helpers, the anti-patterns); backend - one API client with a default token, problem and validation assertions that read the text from the error catalog, a response-fields assertion instead of the 22 PropertyNames copies, a composable world builder with roles, a two-request race helper, shared Name/Now/Creator/RepositoryLayout through tests/Directory.Build.props; frontend - one renderPage(path, {role, data}), src/test/api.ts (apiUrl, problem, serveProblem, recordRequests, hold), fixture factories with one id scheme. One reference test per layer moves onto the kit; no mass migration (phase 2), no new dependency, no new project
