# US-6: The admin-API specs stop needing a human-supplied secret

- story: US-6
- depends-on: -
- shippable: no — routes specs 6/9/11's admin-API calls through the stand-only client (US-2); there is nothing to route through without it
- status: pending

As an **AI agent**, I want specs 6, 9 and 11 (the ones that today skip
without `E2E_KEYCLOAK_ADMIN_PASSWORD`) to run without any human-supplied
secret, so that an unattended run has the same coverage a human's run has
today.

## Acceptance criteria

```gherkin
Scenario: Spec 9 (token renewal across two tabs) runs unattended
  Given no `E2E_KEYCLOAK_ADMIN_PASSWORD` is set anywhere
  When an agent runs the suite
  Then spec 9 executes rather than skipping, and asserts what it asserts
    today (both tabs renew without "Сессия истекла")

Scenario: Specs 11 and 6 run under the same condition
  Given the same environment as above
  When an agent runs the suite
  Then spec 11 ("session ended by an administrator") and spec 6
    ("mid-work renewal fails while Keycloak is down") also execute rather
    than skip, each reaching the same outcome it reaches today under
    `E2E_KEYCLOAK_ADMIN_PASSWORD`
```
