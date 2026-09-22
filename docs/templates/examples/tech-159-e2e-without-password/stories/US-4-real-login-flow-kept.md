# US-4: The suite still tests the real login flow

- story: US-4
- depends-on: -
- shippable: no — a constraint on how US-2's mechanism must behave (that it doesn't bypass the OIDC round trip); there is nothing to constrain without US-2
- status: pending

As the **human** reviewing this task, I want the shortcut to replace only
the typing of credentials at Keycloak's form, so that the suite keeps
proving the panel's actual sign-in works end to end, not a bypass of it.

## Acceptance criteria

```gherkin
Scenario: The panel's own OIDC round trip is observed, not just asserted
  Given the suite obtained an SSO cookie for the fixture user through the
    stand-only client's impersonation, instead of typing a password into
    Keycloak's form, and injected that cookie only on Keycloak's own origin
  When the panel is opened next, for at least one spec
  Then the run records a navigation to
    `{keycloakUrl}/realms/mustask/protocol/openid-connect/auth`, a request to
    `{apiUrl}/api/session/callback`, and a `Set-Cookie: mustask.session` header
    on that response
  And `mustask.session` is not present in the browser context before that
    callback response

Scenario: A structural guard keeps the shortcut from swallowing the flow
  Given the suite's own code under `src/frontend/e2e/**`
  When a test or lint rule inspects it
  Then it fails if anything under `e2e/` sets an `mustask.session` cookie
    directly (other than the impersonation helper injecting the Keycloak
    session cookie, and only on Keycloak's own origin) or stubs/mocks
    `/api/session/login`, `/api/session/callback`, or `/api/session/token`
  And this guard runs in the same `scripts/verify.sh` pass as the rest of
    the suite's own checks

Scenario: The one negative case that must keep using the real form stays
  Given spec 3 ("wrong password stays on the Keycloak form, no ?authError=
    round trip") exercises a deliberately wrong, non-secret literal, not
    `E2E_PASSWORD`
  When this task's changes land
  Then spec 3 still exists, still drives the real Keycloak form, and still
    asserts the same outcome as before
  And it submits that wrong password against a username that is not the
    fixture identity the rest of the suite signs in as, so realm `mustask`'s
    brute-force counter (`bruteForceProtected: true`) never accumulates on
    the account an unattended run repeatedly needs

Scenario: Specs 2 and 3 never sign in at all
  Given specs 2 (login-page assertions) and 3 (wrong password) exist to
    exercise the anonymous state and the real form's rejection
  When the suite's setup decides which contexts receive which cookies
    (BR-7)
  Then specs 2 and 3 start with neither the Keycloak SSO cookie nor
    `mustask.session`

Scenario: Spec 4 proves sign-out in the same context, not a fresh one
  Given spec 4 signs in, signs out, and re-opens `/dashboard`
  When the suite drives spec 4
  Then it is the same browser context across sign-in, sign-out and
    re-opening — no new context is created and no cookie is cleared in
    between — so seeing Keycloak's login form again is evidence the
    Keycloak session itself ended (BR-7), not an artifact of starting fresh

Scenario: Spec 10's second context proves mustask.session, not a silent SSO re-login
  Given spec 10 signs in in a first context and builds a second context
    from that context's `storageState`
  When the suite builds the second context
  Then `mustask.session` is carried over into it and the Keycloak SSO cookie is
    removed from it before the second context opens the panel
  And the second context's rendered dashboard can therefore only be
    explained by `mustask.session` (D-23), and the spec asserts `mustask.session`
    is present in that second context, not only that the dashboard renders
```
