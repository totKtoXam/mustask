# US-8: Process documents stop describing a human-only suite

- story: US-8
- depends-on: -
- shippable: no — describes a capability (an agent running the suite unattended) that is false until US-1/US-2/US-4 and the rest land; merging the docs alone would make them state something untrue
- status: pending

As the **human** relying on `src/frontend/AGENTS.md` and
`docs/runbooks/stand.md` as ground truth for what an agent may do, I
want those documents (and the stage instructions that gate a "yes" answer to
the E2E question) updated in this same task, so that no document tells the
next agent something this task has just made false (tech-119 D-32
precedent: process instructions are edited together with the harness).

## Acceptance criteria

```gherkin
Scenario: The frontend commands file no longer claims a human is required
  Given `src/frontend/AGENTS.md` § "E2E test suite" today says "an AI agent
    never runs this suite ... A human runs it" and lists `E2E_PASSWORD` in
    its variable table
  When this task lands
  Then that section says an agent may run the suite unattended under the
    conditions this spec sets (US-1–US-7), `E2E_PASSWORD` is removed from
    the variable table (D-7), and `E2E_KEYCLOAK_ADMIN_PASSWORD`/
    `E2E_ALLOW_DOCKER` are no longer described as human-only gates

Scenario: The runbook's E2E section matches
  Given `docs/runbooks/stand.md` § "E2E test suite" today says "no
    agent runs it" and gives a PowerShell `$env:E2E_PASSWORD` recipe
  When this task lands
  Then that section describes the unattended path this spec adds, drops the
    `E2E_PASSWORD` recipe, and its "Access from the local network" list
    gains the stand-only client's secret as a reason not to raise the stand
    on an untrusted network

Scenario: The stage instructions stop gating "yes" on a human's run
  Given `docs/ai-workflow/stages/04-backend.md` and `05-frontend.md` today
    define a "yes" answer to the E2E question as "e2e is green per the
    human's run"
  When this task lands
  Then both files instead accept a green run the agent itself performed,
    per this spec's US-1–US-7, as satisfying that Definition-of-Done item

Scenario: The suite's own header comments stop citing a document that doesn't exist
  Given `src/frontend/playwright.config.ts`, `e2e/support/auth.ts`,
    `e2e/support/env.ts`, `e2e/global-setup.ts`, and the `e2e/**` exclusion
    comment in `vite.config.ts` each state the human-only constraint today
    and cite `brief-e2e-playwright.md` (one of them also `e2e/README`) as
    the normative source, and neither document exists anywhere in the
    repository
  When this task lands
  Then every one of those comments is rewritten to describe the unattended
    conditions this spec sets and points at `src/frontend/AGENTS.md` § E2E
    as the live source, instead of reproducing or re-citing the dangling
    references
```
