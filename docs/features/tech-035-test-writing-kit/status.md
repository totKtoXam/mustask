# tech-035-test-writing-kit

- type: tech
- size: M
- branch: tech/035-test-writing-kit
- stage: complete
- epic: platform/tests
- deps: -
- gates: gate-3

## Gates

gate-3: approved 2026-11-09

## Plan

1. No endpoints, migrations, contract or runtime code: test projects and docs only.
2. `src/backend/tests/Shared/Kit/` (TestData: Creator, Now, UniqueName, Name; RepositoryLayout) is compiled into every test project through `src/backend/tests/Directory.Build.props`.
3. `Mustask.Api.IntegrationTests/Kit/`: ApiClient (default token, `.As`, `.Anonymous`), ProblemAssert.Problem/Validation (text from the real catalog), ResponseFields.AssertExactly, World (roles), Race.TwoAsync.
4. `docs/conventions/testing.md`: layer ownership, the kit, anti-patterns; backend and frontend parts.
5. Frontend: one renderPage(path, {role, data}), `src/test/api.ts` (apiUrl, problem, serveProblem, recordRequests, hold), fixture factories with one id scheme.
6. One reference test per layer moves onto the kit; KitAssertionsTests prove the assertions fail when they should.
7. No mass migration (phase 2), no new dependency, no new project.

## Log

- 2026-11-09 [orchestrator] started from the parent orchestrator session as a lane parallel to mod-019. The human confirmed «⭐ Подтвердить M»: tech, M, Dev (backend → frontend) → one QA pass → one code review (Opus) → gate-3 → PR. Size grounds: block 0 — test infrastructure behaviour changes; block 1 clean (no migration, no new project, no ADR, no auth/secrets, no external exchange, no contract change); 2.1 not S (>5 files, backend + frontend test layers + docs); 2.3 → M. Block 3: no regulatory basis, no migration, nothing beyond the line. E2E: the standing answer is NO (part split off to tech-036).
- 2026-11-09 [orchestrator] binding: this session's worktree `.claude/worktrees/unruffled-banach-65f877`, its branch renamed to `tech/035-test-writing-kit` and reset to `origin/main` `d6fb8e0f`; new-task.sh was not run (it makes a worktree this session cannot use).
- 2026-11-09 [orchestrator] queue take: PR #100.
- 2026-11-09 [backend-developer] stage 4 done (a2086ae3, 67e5fbe6, f3957318): kit, testing.md sections 1–2, four reference tests, 16 KitAssertionsTests; build green, affected suites green (full Api/Infrastructure suites not run). The developer created `src/backend/tests/Directory.Build.props` itself to keep the kit commit buildable; the orchestrator reviewed and accepts it.
- 2026-11-09 [frontend-developer] frontend stage done (8d9232f3, 51d12637, 8e59da70): `src/test/` api.ts, fixtures.ts, renderPage.tsx, 16 kit tests; reference tests: page `routes.access.test.tsx`, component `DefaultLanguageDialog.test.tsx` (unit layer needs no helper); testing.md section 3. tsc and ESLint clean; full Vitest 5602 passed, 1 load timeout (`BacklogPage.qa.test.tsx`, passes alone).
- 2026-11-09 [qa-engineer] single QA pass on dfe77af6 (49658f18, `qa.md`): ready, 2 minors (ProblemAssert skips params when none expected; Race/World/ApiClient lack own failure tests) and an open question for gate-3 (`AggregateViewVolumeTests` 2 s budget fails on this machine, untouched by the diff). E2E: no (standing answer). Minor 1 goes into the review fix pass.
- 2026-11-09 [code-reviewer] round 1 (Opus, be9197f3): changes requested, 3 major (testing.md example 403 not 400; ProblemAssert params guarantee; testing.md unlinked) and 2 minor (Creator divergence; problemText unfilled placeholder).
- 2026-11-09 [task-executor] F1–F5 fixed (5efef878): build green, kit tests 30/30 backend, 576 frontend.
- 2026-11-09 [code-reviewer] delta read (b9f82574): approve, F1–F5 closed. Carried to gate-3: QA minor 2 (Race/World/ApiClient lack own failure tests, phase 2) and `AggregateViewVolumeTests` 2 s budget on this machine (untouched by the diff).
- 2026-11-09 [orchestrator] gate-3: the human chose «⭐ Одобрить, открыть PR»; Race/World/ApiClient failure tests carried to phase 2.

## Notes

Summary (from the queue line): Test-writing kit, phase 1, for new tests only: docs/conventions/testing.md (which layer owns which check, the helpers, the anti-patterns); backend - one API client with a default token, problem and validation assertions that read the text from the error catalog, a response-fields assertion instead of the 22 PropertyNames copies, a composable world builder with roles, a two-request race helper, shared Name/Now/Creator/RepositoryLayout through tests/Directory.Build.props; frontend - one renderPage(path, {role, data}), src/test/api.ts (apiUrl, problem, serveProblem, recordRequests, hold), fixture factories with one id scheme. One reference test per layer moves onto the kit; no mass migration (phase 2), no new dependency, no new project
