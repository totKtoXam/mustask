# US-5: A missing stand-only client fails clearly, not with a wall of noise

- story: US-5
- depends-on: -
- shippable: no — a failure-mode guard for a client (US-2) that does not exist without it; the check is unreachable until then
- status: pending

As an **AI agent**, I want the suite to refuse immediately and name the
cause when the stand-only client is missing or unusable, so that I get one
diagnosable error instead of thirteen unrelated red specs.

## Acceptance criteria

```gherkin
Scenario: The client does not exist in the realm
  Given a realm `mustask` imported without the stand-only client (for example,
    an older import, or a copy of the stand built without it)
  When the suite's global setup runs, before any spec starts
  Then the run fails with one message naming the missing client as the
    cause — the same precedent the existing global setup already sets for
    an unreachable panel/API/realm

Scenario: The client exists but its credential does not match
  Given a realm where the client exists, but the secret the suite/AppHost
    holds for it does not match what the realm has
  When the suite's global setup runs
  Then the run fails with one message naming that mismatch as the cause,
    before any spec starts
```
