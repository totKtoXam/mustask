# US-2: A user created or changed in Keycloak reaches the projection

- story: US-2
- depends-on: US-1
- shippable: no — a feed with no read side (US-1) fills a store nothing reads
- status: pending

As a **Keycloak realm administrator**, I want **a user I create or edit in
realm `mustask` to appear in MUSTASK as I left them, with no step in
MUSTASK**, so that **organization administrators can find and add them, and
lists show their current name (doc-006 constraint 5, doc-010 D-9)**.

## Acceptance criteria

```gherkin
Scenario: A user created in the console becomes findable
  Given the stand is up and the provider is enabled for realm mustask
  When a Keycloak realm administrator creates user "a.smirnova" with first
    name «Анна», last name «Смирнова» and e-mail anna.smirnova@example.test
  Then within 10 s a search for "смирнова" answers «Анна Смирнова», active
    (NFR Freshness)
  And her id in the answer equals the sub of her tokens

Scenario: A change of name is followed
  Given the projection holds «Анна Смирнова»
  When the administrator changes her last name to «Кузнецова»
  Then within 10 s a lookup of her id answers «Анна Кузнецова»
  And a search for "смирнова" no longer answers her

Scenario: A change of e-mail is followed
  Given the projection holds her with anna.smirnova@example.test
  When the administrator changes her e-mail to a.kuznetsova@example.test
  Then within 10 s a search for "kuznetsova@" answers her with the new e-mail
  And a search for "anna.smirnova@" answers nobody

Scenario: A change of username alone moves the fallback display name
  Given a user "qa.fixture" with no first or last name
  When the administrator renames the username to "qa.robot"
  Then within 10 s a lookup of the same id answers the display name
    "qa.robot" (BR-UP-3)

Scenario: A user's own profile change is carried like an administrator's
  Given the E2E fixture user signed in to Keycloak's account pages through a
    session obtained by the stand-only impersonation of ADR-0023 — nobody
    types a password
  When the user changes their own last name there and saves it
  Then within 10 s a lookup of their id answers the new display name
    (BR-UP-4)

Scenario: A change outside the projected fields leaves the projection as it is
  Given the projection holds «Анна Кузнецова», active
  When the administrator resets her password, adds her to a group and
    assigns her a role
  Then a lookup of her id answers exactly what it answered before
    (BR-UP-4)

Scenario: A user created already disabled is inactive from the start
  Given no user "b.orlov" exists
  When the administrator creates "b.orlov" with the account disabled
  Then within 10 s a lookup of his id answers inactive
  And a search with the defaults does not answer him (US-1)

Scenario: A service-account user is never projected
  Given realm mustask holds the service-account user of a client with a
    service account — on the stand, the one of the stand-only client
    mustask-e2e (ADR-0023)
  When a consumer searches for "service-account" with "include inactive"
  Then nobody is answered (BR-UP-2; D-8)

Scenario: A user of another realm is never projected
  Given a user «Пётр Мастеров» created in the master realm
  When a consumer searches for "мастеров" with "include inactive"
  Then nobody is answered (BR-UP-2)
```
