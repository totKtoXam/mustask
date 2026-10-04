# US-4: Existing users are backfilled and missed changes reconciled

- story: US-4
- depends-on: US-2, US-3
- shippable: no — the backfill fills the store US-1 reads; alone it serves nobody
- status: pending

As an **operator**, I want **the users already in realm `mustask` loaded into
MUSTASK, and any change whose push was lost healed later, with no admin REST
call and no service account**, so that **the projection matches Keycloak
from the first day and stays matched (doc-006 constraint 5, doc-010 D-9,
ADR-0023)**.

## Acceptance criteria

```gherkin
Scenario: Existing users are loaded into an empty projection
  Given realm mustask holds 200 users, 5 of them disabled, plus the
    service-account user of mustask-e2e
  And MUSTASK's projection is empty
  When the provider starts with Keycloak and MUSTASK is reachable
  Then within 5 minutes lookups answer all 200 users, the 5 disabled ones
    inactive and the rest active (BR-UP-8)
  And the service-account user is not among them (BR-UP-2)

Scenario: The stand comes up with its users already projected
  Given a stand after a reset, started, the database migrated (ADR-0002) and
    scripts/keycloak-seed.sh run, with the E2E fixture user present
  When the stand reports ready and 5 minutes pass
  Then a search for each seed user and the fixture user answers them, active
  And no step beyond the migration and the seed script was taken for it
    (NFR Stand)

Scenario: A second stand start keeps the same users under the same ids
  Given the stand of the previous scenario, and the ids a lookup answered
  When the stand is stopped and started again, with no reset
  Then the same ids answer the same users, active
  And a search for each seed user's display name with "include inactive"
    answers that user once, not once per start
  And the start reconciliation logs 0 added, 0 changed, 0 marked absent,
    0 deleted (D-5)

Scenario: A realm import that differs from the live realm fails the start
  Given a stand run against an external database (tech-002) on a server
    that holds another database too, started, migrated and seeded once
  And a client role of mustask-api that the realm import declares deleted
    in the Keycloak console, as when an import edit adds a role the
    stand's store never got
  When the stand is stopped and started again, with no reset
  Then it does not come up ready, and an error in the AppHost's output or
    dashboard names the missing role and scripts/stand-reset.sh
    (NFR Stand, D-10)
  And after scripts/stand-reset.sh, a new start, the database migrated and
    the seed script run, the stand comes up ready and a search for each
    seed user answers that user, active, within 5 minutes: the projection
    is rebuilt by the backfill (BR-UP-8)
  And the other database on that server is untouched (D-10, D-12)

Scenario: Stands of two checkouts take turns on one external database
  Given two checkouts of the repository whose stands carry the provider,
    both configured for the same external database (tech-002)
  And the first one's stand started, migrated and seeded, then stopped,
    with the ids a lookup answered
  When the second one's stand is started on the same ports
  Then it starts, no container of the first stand being left behind (D-12)
  And the same ids answer the same users, active, and its start
    reconciliation logs 0 added, 0 marked absent: one store serves that
    database (NFR Stand)
  And after a third checkout's stand, on its own database container, is
    started once, stopped and reset with scripts/stand-reset.sh, the
    second stand started again answers the same ids with the same users:
    stands on different databases share no store

Scenario: The backfill reads Keycloak in-process only
  Given the provider's and MUSTASK's sources and the realm-import files
  When the ADR-0023 guards and the provider's read-only guard run
  Then all pass, ADR-0023's guards unchanged (BR-UP-9, BR-UP-13)
  And the realm import declares no client beyond mustask-panel and
    mustask-api

Scenario: A deletion whose push was lost is caught by the next reconciliation
  Given «Ольга Белова» active in the projection
  And MUSTASK is stopped, her account is deleted in Keycloak, and Keycloak
    is restarted before MUSTASK comes back
  When MUSTASK is started again
  Then within 5 minutes of MUSTASK being reachable a lookup of her id
    answers inactive, with her last received name (BR-UP-5, BR-UP-8)

Scenario: A change whose push was lost is caught by the next reconciliation
  Given «Иван Петров» active in the projection
  And his last name was changed to «Петров-Водкин» while his push was lost
    in the same way
  When MUSTASK is started again
  Then within 5 minutes of MUSTASK being reachable a lookup of his id
    answers «Иван Петров-Водкин»

Scenario: A user created during a reconciliation stays active
  Given 200 active users in the projection and a reconciliation whose
    enumeration has begun
  When a user «Мария Новикова» is created in Keycloak before that
    reconciliation completes, and her creation push is applied
  Then after the reconciliation completes a lookup of her id answers active
  And it still answers active after the next reconciliation (BR-UP-8)

Scenario: An absence yields to a newer push
  Given «Пётр Зайцев» marked absent by a complete snapshot, and inactive
  When a push for his id arrives that says enabled, with a moment later
    than that snapshot's enumeration start — in a test, sent as the
    provider would send it
  Then a lookup of his id answers active under the same id
  And a push for a moment earlier than that enumeration start changes
    nothing (BR-UP-6, BR-UP-8)

Scenario: The reconciliation repeats on its period
  Given the reconciliation period configured to 2 minutes for the test
  When 5 minutes pass with both sides up
  Then at least two completed reconciliations are logged with their counts
    received, added, changed, marked absent, deleted (NFR Logging)
  And with no configuration the period is 60 minutes (BR-UP-8)

Scenario: An interrupted snapshot marks nobody absent
  Given 200 active users in the projection
  When a reconciliation is cut off before it completes — in a test, by
    cutting the connection after MUSTASK has received part of it
  Then every entry MUSTASK received before the cut is applied as a push
  And none of the 200 is marked absent, deleted or inactive (BR-UP-8)

Scenario: Reads keep answering during a reconciliation
  Given a projection of 10 000 users and a reconciliation of all of them
    running
  When a consumer searches and looks up during it
  Then each answer arrives within 500 ms and is never empty for a user held
    before the reconciliation began
  And the reconciliation completes within 5 minutes (NFR Volumes)

Scenario: A repeated reconciliation over an unchanged realm changes nothing
  Given the projection already matches realm mustask
  When a reconciliation runs
  Then its log line counts 0 added, 0 changed, 0 marked absent, 0 deleted
    (BR-UP-6)

Scenario: The provider's tests gate verification
  Given a change to the provider's sources only
  When scripts/verify.sh runs
  Then it runs the provider's tests
  And a failing provider test makes it fail (NFR Stand)
```
