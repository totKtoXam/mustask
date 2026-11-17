# tech-074 — code review

## Round 1

Diff `origin/main...HEAD` at 4e189aae. I ignored the TEMPORARY `Program.cs`
touch (4e189aae), as the brief said to. I did not run the full `verify.sh`
(economy mode). I checked by reading, and I compared the result against the
old `ci.yml` on `origin/main`.

### Verdict

**approve.** No blocker or major findings. Four minors and one nit are
debts. One condition belongs to gate-3, not to this round: the backend
coverage merge has not run in CI yet (R1-1). The run that 4e189aae triggers
should show green "Backend coverage floors" in the
`verify.sh (change-proportionate)` job before that commit is reverted.

### Findings

- **R1-1 (minor) `.github/workflows/ci.yml:282-296`, `scripts/ci-coverage-gate.sh:36-48`: the backend merge path is proven only by reading.**
  I traced the layout. `upload-artifact` of `$RUNNER_TEMP/coverage/backend`
  gives `<Project>/<guid>/coverage.cobertura.xml`. `download-artifact` with
  `pattern:` gives `coverage/coverage-backend-<group>/...`. After the `mv`,
  the regroup makes it `<Project>/coverage-backend-<group>/...`.
  `coverage-gate.mjs` then classifies by the top folder name and ORs the hits
  per `file:line`. The groups that matched no tests in a project add only
  zero-hit lines of the same instrumented modules, so the denominators do not
  change. That matches the old single run. A wrong layout fails loud (the
  `mv` glob, "no integration-test coverage reports"), not green. Fix: before
  reverting 4e189aae, record that run's id and the floors' output in the Log.
- **R1-2 (minor) `scripts/tests/` (missing): `ci-coverage-gate.sh` has no case script.**
  Its regroup and its "no reports" exits were checked once by hand in QA.
  `verify.sh` gives it only the generic shell checks, so a later edit is
  verified by nothing until a CI run. Fix: add a small
  `scripts/tests/ci-coverage-gate-cases.sh`. It would build two fake job
  folders with tiny cobertura files: a unit project, an IntegrationTests
  project, and one zero-hit duplicate. It would assert the floors' verdict
  and exits 1 and 2. Then hook it into verify.sh's shell bucket the way the
  other case scripts are keyed.
- **R1-3 (minor) `scripts/verify.sh:77-79`: "keep the raw coverage there instead of checking the floors" overstates.**
  For the frontend, `VERIFY_COVERAGE_OUT` only takes effect together with
  `VERIFY_SHARD` (`verify.sh` shard branch). A full frontend run with it set
  still checks the floor and keeps nothing (the `VERIFY_FULL=1` branch never
  reads it). CI never combines them, so nothing breaks. Fix: say "the backend
  reports, and a shard's blob".
- **R1-4 (minor) `docs/ai-workflow/README.md:118-120` (outside the diff, direction 2): "CI runs `VERIFY_FULL=1 sh scripts/verify.sh` on every pull request — the required check on `main`".**
  The required check no longer runs verify.sh. It aggregates the parts' jobs
  and checks the merged floors. The sentence is still true in substance:
  every part runs with `VERIFY_FULL=1`, and a merge is still gated by the
  whole suite. Nothing an agent would do changes, which is why this is a
  minor. Fix: "CI runs `VERIFY_FULL=1 scripts/verify.sh` on every pull
  request, split into parallel parts, and its last job is the required check
  on `main`". `docs/conventions/git.md:423` reads the same way and can take
  the same half-sentence.
- **R1-5 (nit) `scripts/verify.sh:116`: the `VERIFY_SHARD` check accepts `7/6`.**
  Vitest rejects it later with its own message, and CI never passes it. Fix
  (optional): also check `index <= count`.

QA's D-3 is not a defect. `LiveRealmFact` (`src/backend/tests/Mustask.Api.IntegrationTests/Features/Realm/Live/LiveRealmFactAttribute.cs:10`)
skips unless `MUSTASK_KEYCLOAK_LIVE_TESTS=1`, and only the keycloak branch of
`verify.sh` sets it. The backend groups do not run the live tests, so
nothing is duplicated. QA's D-2 (the unexpanded names of skipped matrix jobs)
stays a cosmetic nit.

### Correctness checks (no finding)

- Verdict of the required check (`ci.yml` "Every needed job went green"):
  - plan failure: `check plan … 1` turns red, and the outputs being empty
    does not matter.
  - static failure, cancelled or skipped: red. It is always needed.
  - backend or frontend failure or cancelled: red.
  - backend or frontend skipped while its need flag is 1: red. Skipped while
    the flag is 0 or empty: ok.
  - matrix result: one red leg makes the result `failure`.
  - `if: always()` is right here. With `!cancelled()`, a run cancelled by
    hand would report a *skipped* required check, which counts as passing.
  - the later steps carry an implicit `success()`, so a red check skips the
    coverage steps.
- Checks lost against the old single job: none. static =
  prose/agents/shell/ci + CD cases + keycloak + frontend lint/build. backend =
  build + tests + floors (merged). frontend = full suite in shards + floor
  (merged). Setup .NET in static is needed only for KeycloakLive (the shell
  cases stub dotnet). Node and `npm ci` are needed only for frontend-static.
  The plan's `--list` has no early exit that skips `write_needs`
  (`verify.sh:351`, `:436`).
- Backend partition: `~A`, `~B`, `!~A&!~B` split by fully qualified name, so
  every test runs in exactly one group whatever its project. Every test
  project's namespaces start with the project name (the only other one is
  `Mustask.Testing` under `tests/Shared`, which holds no tests).
- Stamp and lock: `partial_run` gates both reading and writing the stamp, and
  the `rm -f` too. The shard path skips the lock and the full path keeps it.
  A run with no new variables behaves as before (`VERIFY_PART` defaults to
  `all`, and every guard is empty).
- Portability: there are no bashisms. `set --` reuse is safe because `$1` was
  consumed into `LIST_ONLY` before it. `cp -R "$project".` is POSIX.
  `native_path` covers Git Bash.

### Behavioural claims (item 6)

- ci.yml:18-20 "every job is verify.sh with a part": anchored to the jobs'
  `VERIFY_PART` env. The verify job runs no verify.sh, but the header says
  "the required check", which is accurate.
- ci.yml:29-30 "every test runs in exactly one … a new assembly falls into
  rest": anchored to ci.yml:158-162. It holds unless a new assembly's name
  contains one of the two substrings.
- ci.yml:33-34 "reports even when a job before it failed or was skipped":
  anchored to ci.yml:236 `if: always()`.
- ci.yml:40-41 "a docs-only run finishes in about a minute": anchored to run
  43920631146 (37 s, status Log).
- ci.yml:49 "Runs on main are never cancelled": anchored to `cancel-in-progress`
  being true only for `pull_request` events.
- ci-coverage-gate.sh:17 "called only after every job of that side went
  green": anchored to ci.yml step `if: needs.<side>.result == 'success'` plus
  the implicit `success()`.
- ci-coverage-gate.sh:12-16 "regrouped by project, merged line by line":
  anchored to :36-48 and `coverage-gate.mjs` `mergeLines`.
- verify.sh:65-66 "with no VERIFY_PART every part runs": anchored to
  `part()` and the `[ "$VERIFY_PART" != all ]` guard.
- verify.sh:77-79: became R1-3.
- verify.sh:82-83 "a part, a filter or a shard leaves no green stamp and
  reads none": anchored to `partial_run` (`verify.sh:128-131`, `:489`) and
  the stamp cases 4b and 5b.
- git.md:248-251 and :401-403: anchored to the ci.yml jobs and to the
  verify job's check step.
- Direction 2: I grepped for `change-proportionate`, `ci.yml`, `VERIFY_FULL=1`
  and "CI runs" in `docs/` (excluding `features/`), `.claude/`, `scripts/`,
  `.githooks/` and both `AGENTS.md` files. The hits were
  `docs/ai-workflow/README.md:119` (became R1-4), `git.md:423` (folded into
  R1-4), and `verify.sh:20,37`, `.githooks/pre-push:14` and
  `src/frontend/AGENTS.md:33-35`. The last four say "CI sets
  VERIFY_FULL=1 / a merge is gated by a full run", which is still true. I did
  not sweep `docs/features/*` (historical task artifacts).
