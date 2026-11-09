# QA — tech-035-test-writing-kit

Single QA pass (backend + frontend), HEAD `dfe77af6`, branch `tech/035-test-writing-kit`. Type tech, test infrastructure
and docs only. **E2E: no (standing answer)** — skipped by the human's decision; nothing under `src/frontend/e2e/` touched.

## Check list (built from the queue-line scope, before reading the code)

| Scope item | Result |
|---|---|
| `testing.md`: layer ownership, helpers, anti-patterns (backend + frontend) | present; helper tables match the code; the four backend and two frontend reference tests named in it exist |
| TestData Creator/Now/UniqueName/Name, RepositoryLayout via `tests/Directory.Build.props` | exist, compile into every test project; Architecture/Contracts/Shared suites green |
| ApiClient (default token, `.As`, `.Anonymous`), ProblemAssert (catalog text), ResponseFields, World (roles), Race | exist; used by the Api reference test, which passes |
| Frontend `renderPage`, `api.ts` (apiUrl, problem, serveProblem, recordRequests, hold), fixture factories, one id scheme | exist; 16 kit tests green |
| One reference test per layer moved onto the kit | Api, Application, Domain, Infrastructure, page, component: all six moved, diffs small |
| No mass migration | 4 backend + 2 frontend existing test files touched, one test each |
| No new dependency / project / runtime code | `git diff` touches no csproj, sln, package.json, nor runtime code |
| Kit tests fail when they should | backend: 16 `KitAssertionsTests` assert failures (wrong status/code/text/type, extra and missing field); frontend: mutation run, `renderPage` role override disabled -> reference page test fails, restored |

## Runs

- `bash scripts/verify.sh`: build, lint (0 errors, 3 pre-existing warnings), frontend build, Shared 53, Architecture 82,
  Application 845, Contracts 226, Domain 892, Infrastructure 465 all green. Two red items, both load artefacts and not
  the kit (see below).
- Full `Mustask.Api.IntegrationTests` (once, Testcontainers): 3198 passed, 3 skipped, 2 failed (first run, under load).
- Frontend: `tsc -b` clean; ESLint on `src/test` and the two moved tests clean; Vitest for `src/test` + `routes.access` +
  `DefaultLanguageDialog`: 570 passed, 1 skipped (pre-existing `it.skipIf` in `kitGuard.test.ts`).
- Hypothesis re-runs (load, not ritual): verify's frontend steps timed out (`vitest-worker fetch`, `setup.ts` hook 10s)
  while the backend suite ran beside them; the three red guard files (`colourLiterals`, `e2eSessionGuard`,
  `mockRegistry`) pass alone. `DeleteRestorePurgeVolumeTests` passes alone (3/3).

## Defects

### Backend

1. **Minor** — `src/backend/tests/Mustask.Api.IntegrationTests/Kit/ProblemAssert.cs:25-30` (`Problem`): with no parameters
   the method returns before looking at `params`, so an answer that carries unexpected `params` passes, although testing.md
   says «exactly those `params`». Also a number/date value is checked by type only (only strings by value). Repro: call
   `Problem` on a body with `"params": {"x": {...}}` and no expected parameters — no failure. Not covered by
   `KitAssertionsTests`.
2. **Minor** — `Kit/Race.cs`, `Kit/World.cs`, `Kit/ApiClient.cs` have no own failure-mode test (the section 6 claim
   «the kit's tests show the helpers fail when they should» holds for the assertions and `TestData`, not for these three).
   They are exercised only through the one reference race test. Carry to phase 2.
3. **Not a kit defect, open question for gate-3** — `AggregateViewVolumeTests.EveryGrouping_OfAMillionItemsInTwoHundredProjects_IsReadWithin2s`
   (Features/Boards) fails on this machine on the full run and alone (2 s budget, 3.5 min test); the file and everything it
   reads are untouched by the diff (no runtime change). Not run on `main` to confirm; treat as environmental/pre-existing.

### Frontend

No blocker, major or minor found. `hold` is covered only by its own kit test (no reference test uses it), as the plan says.

## Verdict

No blocker or major open. Ready for code review, with two minors (1, 2) and the volume-test note (3) carried to gate-3.
