# US-3: A disabled, re-enabled or deleted account changes its status

- story: US-3
- depends-on: US-2
- shippable: no — one side of the feed; consumers of the status arrive with mod-016 and mod-003
- status: pending

As a **Keycloak realm administrator**, I want **disabling, enabling again or
deleting an account in Keycloak to change its status in MUSTASK, while the
person stays nameable**, so that **consumers can mark them «неактивен» and
stop offering them, without losing who they were (doc-006 BR-M1-17, D-4)**.

## Acceptance criteria

```gherkin
Scenario: A disabled account becomes inactive and stays nameable
  Given the projection holds «Иван Петров», active
  When a Keycloak realm administrator disables his account
  Then within 10 s a lookup of his id answers «Иван Петров», his e-mail and
    inactive
  And a search for "петров" with the defaults does not answer him
  And a search for "петров" with "include inactive" answers him, inactive

Scenario: An account enabled again becomes active under the same id
  Given «Иван Петров» disabled and inactive in the projection
  When the administrator enables his account again
  Then within 10 s a lookup of the same id answers him active
  And a search for "петров" with the defaults answers him again

Scenario: A deleted account becomes inactive for good and keeps its name
  Given the projection holds «Ольга Белова», active, with e-mail
    o.belova@example.test
  When the administrator deletes her account
  Then within 10 s a lookup of her id answers «Ольга Белова»,
    o.belova@example.test and inactive (BR-UP-5)
  And the record is never removed from the projection

Scenario: A reported deletion is final
  Given «Ольга Белова» deleted in Keycloak, the deletion received, and
    inactive in the projection
  When a push for her id arrives that says enabled, with a moment later
    than her deletion — in a test, sent as the provider would send it
  Then a lookup of her id still answers inactive (BR-UP-5, BR-UP-6)
  And unlike an absence (US-4), no later push or reconciliation changes that:
    after a complete reconciliation, which does not mark her absent, the
    same enabled push with a moment later than that reconciliation still
    leaves her inactive (BR-UP-8)

Scenario: A new user with a deleted user's username and e-mail is another user
  Given «Ольга Белова» deleted, with username "o.belova" and e-mail
    o.belova@example.test
  When the administrator creates a new user "o.belova" with the same e-mail
    and the name «Ольга Белова»
  Then within 10 s a search for "o.belova@" with the defaults answers
    exactly one user: the new one, active, under a new id
  And a lookup of the old id still answers the deleted one, inactive
    (BR-UP-5)

Scenario: A temporary brute-force lockout is not inactivity
  Given the projection holds «Иван Петров», active
  When five wrong passwords, typed at the realm's login form for his
    username — never his real password — lock his account temporarily
    (realm failureFactor 5, permanentLockout false)
  Then a lookup of his id still answers active (BR-UP-4)

Scenario: The status authorizes nothing
  Given «Иван Петров» holds an access token issued before his account is
    disabled
  When the projection marks him inactive and he calls a Mustask.Api endpoint
    that needs only authentication, before that token expires
  Then the call's outcome is the same as before the disable — the
    projection neither refuses nor admits (BR-UP-12)
  And once the token expires — at most 15 minutes after issue, realm import
    accessTokenLifespan 900 — renewal is refused by Keycloak, not by the
    projection
```
