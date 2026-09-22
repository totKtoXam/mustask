# US-1: Agent runs the suite with no password

- story: US-1
- depends-on: -
- shippable: no — this is the outcome the other stories jointly deliver; without US-2's client/seed and US-4's OIDC flow there is no mechanism to make it true, so it can't even be verified alone
- status: pending

As an **AI agent**, I want to run the whole E2E suite without knowing or
typing any password, so that I can finish a task's QA stage myself instead
of asking a human to run the suite for me.

## Acceptance criteria

```gherkin
Scenario: Full run with E2E_PASSWORD unset
  Given a stand started via `dotnet run --project src/backend/Mustask.AppHost`,
    with no `E2E_PASSWORD` set anywhere in the process environment
  When an agent runs `npm run e2e` from `src/frontend`
  Then the run does not fail with "E2E_PASSWORD is not set"
  And every spec that used to call the real-password `signIn` helper
    still reaches a signed-in panel

Scenario: The stand-only client's secret never leaks through the run's own artifacts
  Given the run above has finished
  When the agent (or a QA step) greps `e2e-results/results.json`, the
    console output, and any trace kept for a failed spec, for the
    stand-only client's own secret value
  Then it finds no occurrence
  And the client-credentials/impersonation request that obtains the
    fixture user's token is made from the Node side of the suite only,
    never from inside a browser context — so it can never appear in a
    page, a request the browser made, or a trace of one

Scenario: One measured unattended run
  Given a stand started by the agent, with `E2E_ALLOW_DOCKER=1` set and no
    `E2E_PASSWORD` or `E2E_KEYCLOAK_ADMIN_PASSWORD` set anywhere in the
    process environment
  When the agent runs the whole suite once, unattended
  Then all 13 specs run — 13 passed, 0 failed, 0 skipped — or any spec that
    still skips is named and the reason recorded
  And the wall-clock time of that run is recorded in `qa.md`, and is at
    most 15 minutes
```
