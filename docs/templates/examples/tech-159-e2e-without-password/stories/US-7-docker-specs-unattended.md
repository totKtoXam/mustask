# US-7: The Docker-controlling specs run unattended, scoped to the agent's own stand

- story: US-7
- depends-on: US-9
- shippable: no — "unattended" presumes the whole no-password capability (US-1) already exists, and this story's own last scenario relies on US-9's reconciliation to make its guarantee true for a hard abort
- status: pending

As an **AI agent**, I want to run specs 5 and 6 (`E2E_ALLOW_DOCKER=1`, which
stop and start the `keycloak` container) unattended, so that the suite's
Keycloak-down coverage does not depend on a human being present, as long as
the container in question is the one belonging to the stand I myself
started.

## Acceptance criteria

```gherkin
Scenario: The container resolver is scoped to one stand, not "the first match"
  Given more than one Aspire stand may have a `keycloak` container on the
    same Docker host at once (e.g. two worktrees), and today's resolver
    (`docker.ts`) takes the first match of
    `docker ps -a --filter name=keycloak`, which also matches stopped
    containers left by earlier or sibling stands, in no defined order
  When the suite resolves which container specs 5/6 control
  Then it selects exactly one container belonging to the stand under test
    (the one whose Keycloak the suite's own `E2E_KEYCLOAK_URL` addresses)
  And it fails with one named message on ambiguity, rather than acting on
    an arbitrary match

Scenario: Specs 5 and 6 run against the agent's own stand
  Given the agent has itself started the stand it is testing against, the
    container resolves unambiguously (previous scenario), and it sets
    `E2E_ALLOW_DOCKER=1`
  When it runs the suite
  Then specs 5 and 6 execute, stop and restart only that resolved
    container
  And when a spec passes or fails, its own `afterEach` restores the
    container before the run continues — the same guarantee that already
    holds for a human's run
  And a hard abort that runs no hook at all (the process killed or
    signalled mid-spec) is not something this run can recover from itself
    — that case is recovered by the *next* run's global setup, per US-9
    scenario 1's reconciliation, not by a guarantee made here (D-6's intent
    is met by US-9, not by this scenario)

Scenario: Docker control stays declined without the flag
  Given `E2E_ALLOW_DOCKER` is unset
  When the suite runs
  Then specs 5 and 6 still skip with the existing clear reason — this task
    does not make Docker control the default for an unattended run
```
