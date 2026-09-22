# Stages 6–7 — Frontend: development + QA

**Roles:** frontend-developer, qa-engineer (QA — a separate session) · **Model:** standard
**Input:** `spec.md` (the index) + the story or stories this pass implements
plus their `depends-on` chain, under `docs/features/<id>/stories/`, `design.md`,
`api-contract.md`, `docs/conventions/frontend.md`
**Output:** code + tests in the feature branch, `docs/features/<id>/qa.md` (Frontend section)

The stage can start in parallel with backend QA: development proceeds
against `api-contract.md`, and on mocks (MSW) until the backend is ready.

## 6a. Plan

1. Read the frontend conventions and the code of neighboring slices — search
   first, then read the one or two closest to the task and stop once the
   pattern is clear
   (features/).
2. Write a plan: for M — a `## Plan` section (5–10 lines) in the feature's
   `status.md`; for L — a separate `plan-frontend.md`. Contents: new
   feature slices, pages/routes, components, state (server state / client
   state), which MUI components are used.

## 6b. Development

- Strictly per `docs/conventions/frontend.md` (Vertical Slice:
  `features/<name>/`).
- Implement **every state from design.md**: loading, empty, error,
  permissions. A screen without empty/error states is unfinished work.
- The API layer is generated/written strictly per `api-contract.md`; a
  discrepancy between the contract and the real API is a blocker, escalated
  to the orchestrator — not "I'll patch it on the front."
- Tests together with the code: unit tests on logic (hooks, utilities),
  component tests on key scenarios (Testing Library), MSW for the API.
- Interface texts — from design.md, do not make up your own.

## 7. Frontend QA (separate session, clean context)

1. The **Frontend** section of the feature's `qa.md`, per the test-plan
   template: a criterion/screen → check matrix.
2. Run the tests, add the missing component tests.
3. **Ask the human whether to do E2E for this task**, through the question
   form, in the format of `feature.md`, "Gates and defect loops". Ask
   before writing or
   running anything under `src/frontend/e2e/` — do not draft specs "just in
   case" while waiting for the answer. Record the answer in the task's
   `status.md` under Decisions.
   - **Yes** — E2E against the real backend, per Playwright
     (`src/frontend/playwright.config.ts`, `src/frontend/e2e/`,
     `npm run e2e`): at minimum the feature's happy path plus one failure
     scenario. Since ADR-0023, specs sign in through the stand-only Keycloak
     client's impersonation right, not a typed password
     (`src/frontend/AGENTS.md` § E2E test suite) — an agent may bring up the
     stand and run `npm run e2e` itself, or a human may; either way the
     agent reads Playwright's JSON report from disk and records the result
     in `qa.md`.

     **The run that counts starts from a cold stand** — one brought up from
     nothing for it, not the one that has been serving the session's manual
     checks. `Mustask.Api`'s realm readiness *latches*: a stand that has been up
     for a while is permanently past the window in which the API has not yet
     seen the Keycloak realm, so no run against it can show a defect living in
     that window. The suite names which stand it got on every run (`cold
     stand …` / `warm stand …`, `src/frontend/e2e/support/apiReadiness.ts`)
     and writes the same fact to `src/frontend/e2e-results/stand.json`, beside
     Playwright's own report. `qa.md` quotes it from **there**: the console
     line reaches only the terminal of whoever started the run, and Playwright's
     JSON report carries no output from global setup at all — so on the path
     this step explicitly allows, where a human runs the suite and the agent
     reads the report from disk, that file is the only copy.

     Two things about that copy. It lives in the worktree the suite ran in and
     is gitignored, so it does not travel: if the human ran the suite in a
     different worktree than the one the QA session was handed, they paste its
     contents into `qa.md` — there is no path from here to there. And it is not
     cleared between runs, so check its `checkedAt` against when this run
     started before quoting it, or the quote may be the previous run's.

     A warm line does not mean the run failed — it means the run did not
     answer the question.

     Repeat it **once**, against a stand started from nothing. If the second
     run is warm as well, stop there. Say so in `qa.md`, and carry it forward
     as a deviation: into the gate-3 summary ([07-mr-complete.md](07-mr-complete.md),
     § "Human Gate 3") and into the MR's `## Deliberately left open`. Under
     standing approval gate-3 stays `pending` and the human approves with the
     Merge button, so that block is the only place the streak reaches them —
     it is not optional there. A third stand buys another coin flip, not an
     answer: the two facts are independent, and a stand started from nothing
     can still be observed warm. The E2E-without-password task
     ([example](../../templates/examples/tech-159-e2e-without-password/qa.md)) hit exactly that three times in a row
     before a fourth run finally caught the API not yet ready.

     > Introduced on 2026-09-15. Three QA runs of that task and an
     > approved code review all passed on a warm stand; the human's first cold
     > run failed specs 1 and 2 on 30s timeouts. The window is 30s wide and
     > the suite's own sign-in timeout is also 30s — a coin flip that the QA
     > procedure of the time could never have flipped, because it always
     > tested a stand it had warmed up itself.
   - **No** — record the reason next to the decision, and say in the
     Frontend section of `qa.md` that E2E was skipped by the human's
     decision.
4. Reconcile against design.md: every screen, every state, texts,
   permission behavior.
5. Defects → the Frontend section of `qa.md` → returned to the
   frontend-developer, cycle until the blocker list is empty.

The run-economy rules are shared with backend QA, see
[04-backend.md](04-backend.md), § "Run economy". When E2E was run, it carries
the only exceptions to the "green is not re-checked" rule, and there are two:
the Playwright scenario is re-run if the first run was against mocks rather
than the real backend, and once more — at most once — if the run reported a
warm stand (§ 7 above).

## Definition of Done

- [ ] Every screen and state from design.md is implemented
- [ ] Component tests are green
- [ ] The E2E question was asked and answered; the answer is in `status.md`
      under Decisions
- [ ] If yes: e2e happy path + one failure scenario are green — per an
      agent's own unattended run or a human's, per ADR-0023 — and the
      Playwright JSON report
- [ ] If yes: `qa.md` quotes that run's stand record
      (`src/frontend/e2e-results/stand.json`) — `"stand": "cold"`, or `"warm"`
      twice over and the deviation note § 7 asks for
- [ ] No discrepancies with api-contract.md
- [ ] `qa.md`, Frontend section: no blockers
- [ ] `status.md`: `stage: 06-code-review`
