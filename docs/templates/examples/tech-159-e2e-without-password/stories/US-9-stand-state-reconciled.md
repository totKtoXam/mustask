# US-9: An aborted or unattended run leaves the stand in a known state

- story: US-9
- depends-on: US-1, US-7
- shippable: no — reconciliation only matters once agents actually run the suite unattended (US-1) or control Docker unattended (US-7); there is nothing to reconcile before then
- status: pending

As an **AI agent**, I want the suite to reconcile the stand's realm settings
and its `keycloak` container to their expected state before a run starts,
and I want to know for certain what happens to the stand I started once the
run ends, so that an aborted run never leaves the next run — mine or
anybody else's — guessing why a spec behaves differently.

## Acceptance criteria

```gherkin
Scenario: Global setup reconciles state left by an aborted prior run
  Given a `keycloak` container that may have been left stopped by a run
    that was aborted, killed, or died on a global failure before its own
    `afterEach`/`afterAll` restored it
  When the suite's global setup runs, before any spec starts
  Then it starts the `keycloak` container if it finds it stopped, and
    reports the discrepancy it fixed in its own output
  # Post-D-8 (docs/templates/examples/tech-159-e2e-without-password/status.md):
  # accessTokenLifespan is no longer this scenario's concern. AppHost is now
  # its sole writer, at every stand start (120s, not the realm import's
  # 900s) — no spec shortens it anymore, so there is nothing for a killed
  # spec to leave mis-set and nothing left here to restore. An earlier
  # version of this scenario still described restoring it to 900, which
  # predates D-8 and was never updated when D-8 landed (QA D-F2,
  # docs/templates/examples/tech-159-e2e-without-password/qa.md § Frontend) — that
  # text is removed, not implemented.

Scenario: The agent accounts for the stand after an unattended run
  Given an agent started the stand and ran the suite unattended (US-1
    scenario 3, or specs 5/6 under US-7)
  When the run ends, whether it passed, failed, or was aborted
  Then the agent either stops the stand it started itself, or declares in
    its answer to the human that the stand is still running, on which
    ports, and the exact command to stop it (AGENTS.md § Hard rules)
```
