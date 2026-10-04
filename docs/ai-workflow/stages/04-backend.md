# Stages 4–5 — Backend: development + QA

**Roles:** backend-developer, qa-engineer (QA — a separate session) · **Model:** standard (overrides: the plan of an L — deep, QA of an L — deep)
**Input:** `spec.md` (the index) + the story or stories this pass implements
plus their `depends-on` chain, under `docs/features/<id>/stories/` — not the
whole folder, `design.md`, `docs/conventions/backend.md`
**Output:** code + tests in the feature branch, `docs/features/<id>/api-contract.md`, `qa.md` (Backend section)

## 4a. Plan (before code — mandatory)

1. Read the backend conventions and the code of neighboring slices. Search
   first, read second: find the symbol, route or migration you need, then
   read the one or two slices closest to the task. Stop once the pattern is
   established — a third example confirms what the first two already showed,
   and reading every sibling slice "to be sure" is how a stage arrives at
   the first line of code with a full context and no room left to think.
2. Write a plan: for M — a `## Plan` section (5–10 lines) in the feature's
   `status.md`; for L — a separate `docs/features/<id>/plan-backend.md`.
   Contents: affected slices, new endpoints, domain changes, DB migrations,
   integrations. A decision at the level of "affects every slice / a
   technology choice / a departure from conventions" is an ADR
   (`docs/adr/README.md`), not a plan item; the plan links to it.
3. **Decide whether the work splits into lanes** — four or more use cases
   with disjoint write zones, whatever the task's size. If it does, the plan
   names them: the phase-1 skeleton, which use case is the reference, and one
   row per lane with its write zone. See 4c below.
4. **Fix the API contract** in `api-contract.md`: endpoints, request/response
   DTOs, error codes. From this point the contract is the interface for the
   frontend stage; it can only be changed with a note in `status.md` and a
   line in the Log. If stage 6 (frontend) has already started — a contract
   change is escalated to the orchestrator: it decides which part of the
   frontend to restart.

## 4b. Development

- Strictly per `docs/conventions/backend.md` (the Onion + Vertical Slice
  hybrid).
- Order: DB migration → domain → slice (endpoint/handler) → integrations.
- Tests are written together with the code, not after: unit tests on domain
  rules, integration tests on every endpoint (Testcontainers + PostgreSQL).
- Every acceptance criterion from the story files must be covered by at least
  one test; the test carries a comment linking to the story/criterion.
- Small commits, by slice. Build and tests are green after every commit.


## 4c. Lanes — when the work splits into parallel agents

**The trigger is the shape of the work, not the size of the task.** `L` is
about risk — an irreversible migration, an ADR, a breaking contract change.
Lanes are about how many independent pieces there are. A large `M` qualifies
and a small `L` does not; the question is only whether the plan holds **four
or more use cases whose write zones do not overlap**.

They usually do not. In this codebase a use case owns its folder —
`Features/<Module>/<UseCase>/` with its command or query, handler, validator
and response, plus `Mustask.Application.Tests/Features/<Module>/<UseCase>/` and
one `<UseCase>EndpointTests.cs`. Shared per module there is essentially one
file: `Mustask.Api/Features/<Module>/<Module>Endpoints.cs`, plus the module's
error codes.

### Phase 1 — sequential, and yours

Everything the lanes will share, finished before any of them starts:

- domain and the entity configuration, the migration;
- `api-contract.md` fixed (4a, item 3);
- **`<Module>Endpoints.cs` written in full** — every route mapped. The
  contract already fixes the routes, so there is nothing to discover later,
  and this removes the one file the lanes would otherwise contend for;
- `<Module>ErrorCodes.cs` with every code the contract names;
- DI registration;
- **one use case implemented end to end**, tests included.

That last item is not optional. The rule "read the one or two closest slices
and stop once the pattern is established" works because the neighbours exist.
Lanes running at the same time have no neighbours to learn from — each would
invent its own reading of the same pattern. The reference use case is what
they copy.

### Phase 2 — one agent per remaining use case

A lane's write zone is exactly three paths, and nothing else:

```
src/backend/Mustask.Application/Features/<Module>/<UseCase>/
src/backend/tests/Mustask.Application.Tests/Features/<Module>/<UseCase>/
src/backend/tests/Mustask.Api.IntegrationTests/Features/<Module>/<UseCase>EndpointTests.cs
```

- **One verification run, at the end, by the orchestrator.** A lane does not
  run the suite. Five lanes each bringing up Testcontainers do not divide the
  test time, they multiply it and fight over ports — and builds and tests are
  half of everything an agent actually spends time on.
- **Lanes write files; the orchestrator commits.** Several agents in one
  worktree is already outside the "one worktree per stream" convention
  ([../../conventions/git.md](../../conventions/git.md), "Parallel work");
  disjoint zones make that safe for files, but not for a shared index.
- **A lane that needs something shared escalates, it does not negotiate.**
  A helper another lane might also want is not agreed between lanes: the lane
  reports the need, and the orchestrator either puts it in the phase-1 zone or
  says to inline it. Two agents talking is not durable — a message is not in
  git, and a lane re-run in a fresh session has lost it, while the pipeline's
  whole premise is that state is handed over through files
  (`AGENTS.md`, "How agents work here").
- **A re-run replaces a lane's zone wholesale**, it does not append. A lane
  that failed halfway leaves partial files, and merging a second attempt into
  them is how two half-implementations become one broken one.
- On a module with many use cases, dispatch in waves rather than all at once:
  a wave of deep-tier agents has already hit a session rate limit here.

### What this buys, honestly

Measured over 30 tasks of this repository: turns shorter than 30 seconds —
the thinking that lanes compress — are 10% of a task's wall clock. Builds and
tests are another 12% and do not compress. The remaining 78% is waiting, most
of it in nine pauses longer than two hours.

So lanes are worth it for the active hour, not for the calendar day. The
effect on cost should be favourable — a turn in a subagent re-reads about
80 K of context against the orchestrator's 270 K (measured) — but that is a
prediction from the per-turn numbers, not a measurement. The first task run
this way settles it: `python scripts/usage-report.py --task <id>`.

## 5. Backend QA (separate session, clean context)

The QA agent **does not look at the implementation before writing the test
plan** — only spec + api-contract:

1. Write the **Backend** section of the feature's `qa.md` per
   `docs/templates/test-plan.md`: a criterion → test matrix, additional
   negative scenarios, boundary values, concurrency, permissions.
2. Check actual coverage: run the tests, read the test code, add what is
   missing. Run the migrations on a clean database.
3. Defects — as a list in the Backend section of `qa.md` (severity,
   reproduction steps). Blocker/major → returned to the backend-developer;
   the re-check that follows covers the fixes and what they could have
   broken, nothing else, and there are at most two re-checks. **A minor is
   recorded and carried to gate-3 as a debt** ("Deliberately left open",
   then a queue line if it earns one): it is not sent back and earns no
   re-check of its own. The cycle ends when no blocker or major is open —
   the same economy as the review loop
   ([06-code-review.md](06-code-review.md), "Every finding").
4. **The E2E question is asked once per task**, at the QA stage that is its
   last one before code review. For a task that goes on to stages 6–7,
   that is frontend QA (`05-frontend.md`, step 3) — backend QA here does not
   ask it, to avoid asking twice. For a purely backend task, where stages 6–7
   are skipped (`routing.md`, "Rules for skipping stages"), this is that last
   QA stage, and backend QA asks it here, through the same form and with the
   same recording in `status.md`. A "yes" still means Playwright specs under
   `src/frontend/e2e/` exercising the change through whatever UI already
   surfaces it — there is no backend-only E2E harness. Since ADR-0023, the
   suite signs in through the stand-only Keycloak client's impersonation
   right, not a typed password (`src/frontend/AGENTS.md` § E2E test suite) —
   an agent may run it itself and read the JSON report, or a human may; a
   "yes" here means green either way.

### Run economy

QA costs the human's time, not just tokens. What needs checking is what has
not been checked yet, not what is convenient to re-check:

- **Green is not re-checked.** Running the same test set a second time
  "just in case" is forbidden. Instability is caught by a re-run only when
  there is a suspicion of a race — and then it is recorded as testing a
  hypothesis, not as a ritual.
- **Do not bring up the environment by hand if a test already does it.** A
  green integration test on `/health` through Testcontainers is sufficient
  proof; a manual `dotnet run` of AppHost is needed only when checking
  something the test does not cover (for example the dashboard itself, or
  container connectivity).
- **Meta-checks of the guards — once per project.** That
  `TreatWarningsAsErrors`, the architecture rules and the hooks actually
  fire is checked at bootstrap and locked in by a test
  (`src/backend/tests/Mustask.Architecture.Tests`, `RulesAreNotVacuous`), not
  repeated in every task.
- **For skeleton `tech` tasks** (no business logic — solution, CI, config) a
  full coverage matrix is not written: a checklist of "structure follows
  conventions, it builds, tests pass, it runs" + a defect list.

## Definition of Done

- [ ] `api-contract.md` is fixed
- [ ] All spec criteria are covered by tests, tests are green
- [ ] Migrations apply on a clean database and repeatedly (rollback
      idempotency verified)
- [ ] `qa.md`, Backend section: no blockers
- [ ] For a purely backend task (no stages 6–7 to follow): the E2E question
      was asked and answered, the answer is in `status.md` under Decisions,
      and if yes, e2e is green — per an agent's own unattended run or a
      human's, per ADR-0023 — and the Playwright JSON report
- [ ] `status.md`: `stage: 05-frontend`
