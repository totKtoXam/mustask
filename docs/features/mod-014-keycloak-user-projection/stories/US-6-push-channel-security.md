# US-6: Only the authenticated provider writes, and it only reads Keycloak

- story: US-6
- depends-on: US-2
- shippable: no — the guard of the channel US-2 opens; nothing to guard without it
- status: pending

As an **operator**, I want **only the provider, proving the shared push
credential, to be able to change the projection, that credential kept only
in environment variables / user secrets, and the provider unable to change
anything in Keycloak**, so that **nobody can forge or erase a user in
MUSTASK, and the feed adds no Keycloak client, token, service account or
write path that could reach production (BR-UP-9, BR-UP-10, BR-UP-13,
ADR-0023)**.

## Acceptance criteria

```gherkin
Scenario: A push without the credential is refused
  Given the projection holds «Анна Кузнецова», active
  When MUSTASK receives a well-formed push disabling her, carrying no
    credential
  Then it is refused and a lookup still answers her active
  And the refusal is logged without her name or e-mail (NFR Logging)

Scenario: A push with a wrong credential is refused
  Given the same user
  When MUSTASK receives a push disabling her with a wrong credential
  Then it is refused and nothing changes

Scenario: A Keycloak access token is not a push credential
  Given a valid access token of realm mustask — of an ordinary user, and of
    a user holding every instance-level code
  When either is presented to the push channel in place of the credential
  Then the push is refused and nothing changes (BR-UP-10)

Scenario: No MUSTASK user can write the projection
  Given users of realm mustask with every instance-level code
  When they call every Mustask.Api endpoint the task adds, with their tokens
  Then none of them creates, changes or deletes a projected user
    (BR-UP-1, BR-UP-12)

Scenario: A sign-in does not fill the projection
  Given a user of realm mustask whom the provider has not pushed — in a
    test, one created while the feed is stopped, holding a valid access
    token for their id
  When they call Mustask.Api endpoints with that token
  Then a lookup of their id still answers "unknown", not active and not
    inactive, until their push arrives (BR-UP-1, BR-UP-17)

Scenario: A provider with the wrong credential fails loudly and recovers
  Given the provider configured with a credential MUSTASK does not hold
  When a user is disabled in Keycloak
  Then the operation completes in Keycloak, the refusal is logged on both
    sides, and the projection does not change
  And after the operator sets the right credential and the provider
    restarts, the projection matches Keycloak within 5 minutes (US-4)

Scenario: The credential comes only from environment or user secrets
  Given the repository at the task's final commit
  When it is searched for the stand's push credential value and for any
    push credential literal in code, configuration or the realm import
  Then none is found
  And MUSTASK and the provider each read the credential from an environment
    variable or user secrets (BR-UP-10)

Scenario: The credential is never printed
  Given a stand run that exercises accepted, refused and failed pushes and
    a reconciliation
  When the logs of Keycloak, Mustask.Api and the AppHost are searched for
    the credential value
  Then it does not occur

Scenario: A fresh clone needs no new secret
  Given a fresh clone with only the three secrets src/backend/AGENTS.md
    lists
  When the stand is started
  Then the provider is enabled for realm mustask and pushes are accepted,
    the stand having generated the credential and handed it to both sides
    (NFR Stand)

Scenario: The feed adds no Keycloak client and no service account
  Given the realm-import files and the shipping sources
  When the ADR-0023 guards run, their rule unchanged
  Then the client allowlist is still exactly mustask-panel and mustask-api,
    no listed client has a service account, and no scanned source calls
    /admin/realms/ (BR-UP-9; D-7)

Scenario: The provider's read-only guard passes on its sources
  Given the provider's sources at the task's final commit
  When the provider's read-only guard runs
  Then it passes: no call writes to Keycloak's model, no realm other than
    mustask is read, and no credential, session or custom attribute of a
    user is read (BR-UP-13)

Scenario: The read-only guard catches a write
  Given a scratch copy of the provider's sources with one added call that
    changes a user in Keycloak's model — and, in a second copy, one that
    reads a user's credentials
  When the provider's read-only guard runs over each copy
  Then it fails on each, naming the offending call (BR-UP-13)
  And the guard is recorded as a second line: review of the provider's
    sources stays the first (D-7)
```
