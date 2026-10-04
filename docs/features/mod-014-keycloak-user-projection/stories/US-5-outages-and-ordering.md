# US-5: The feed survives outages, duplicates, reordering and concurrency

- story: US-5
- depends-on: US-2, US-4
- shippable: no — a property of the feed of US-2..US-4, not a capability of its own
- status: pending

As a **Keycloak realm administrator**, I want **user operations in Keycloak to
work the same whether MUSTASK is up or not, and the projection to end up
right whatever order its pushes arrive in, concurrent ones included**, so that **an outage of MUSTASK
never blocks identity administration and never leaves a wrong status
behind (BR-UP-6, BR-UP-7)**.

## Acceptance criteria

```gherkin
Scenario: Keycloak does not wait for a stopped MUSTASK
  Given the stand is up and Mustask.Api is stopped
  When a Keycloak realm administrator creates, then disables, then deletes a
    user in the console
  Then each operation completes successfully, no slower than the same
    operation with the provider disabled (median of 5 runs each, 500 ms
    margin), and within 2 s where that baseline is itself under 2 s
    (NFR Keycloak unaffected)
  And a failed delivery is logged on Keycloak's side, naming the user by id
    only (NFR Logging)

Scenario: Keycloak does not wait for a slow MUSTASK
  Given MUSTASK's receiving side answers only after 30 s — in a test, by a
    stand-in that delays
  When the administrator disables a user
  Then the operation completes within the same bound as in the previous
    scenario

Scenario: The projection catches up after MUSTASK comes back
  Given changes made in Keycloak while Mustask.Api was stopped: user A
    created, user B disabled, user C's name changed
  When Mustask.Api is started again
  Then within 5 minutes of it being reachable lookups answer A active, B
    inactive and C with the new name (NFR Freshness)

Scenario: Statuses stay as last received while the feed is interrupted
  Given «Иван Петров» active in the projection
  And the provider cannot reach MUSTASK
  When his account is disabled in Keycloak
  Then until the feed recovers, a lookup of his id answers active — as last
    received (doc-006 NFR Identity refresh)
  And searches and lookups keep answering

Scenario: The same push applied twice changes nothing
  Given a push stating «Анна Кузнецова», active, as of moment T
  When MUSTASK receives it twice
  Then the projection holds one record for her id, «Анна Кузнецова», active
  And the second receipt is counted as unchanged

Scenario: An older push arriving after a newer one is ignored
  Given pushes for one user: «Анна Смирнова» as of T1 and «Анна Кузнецова»
    as of T2, T2 later than T1
  When MUSTASK receives the T2 push first and the T1 push after it
  Then a lookup answers «Анна Кузнецова» (BR-UP-6)

Scenario: An older "enabled" after a newer "disabled" leaves the user inactive
  Given pushes for one user: enabled as of T1 and disabled as of T2
  When the T2 push arrives before the T1 push
  Then a lookup answers inactive

Scenario: A replayed push from the past cannot undo a later change
  Given a push captured when «Иван Петров» was active
  And his account has since been disabled and that state received
  When the captured push is sent again, with its valid credential
  Then a lookup still answers inactive (BR-UP-6)

Scenario: Concurrent pushes for one user end with the newest state
  Given pushes for one user: «Анна Смирнова» as of T1 and «Анна Кузнецова»
    as of T2, T2 later than T1
  When MUSTASK applies both at the same time, in two concurrent requests —
    in a test, released together, repeated 50 times
  Then every time a lookup answers «Анна Кузнецова» (BR-UP-6)

Scenario: A deletion applied concurrently with an "enabled" push wins
  Given a push reporting a user's deletion and a push saying enabled for
    the same user, with either moment the later
  When MUSTASK applies both at the same time, repeated 50 times
  Then every time a lookup answers inactive, and the user stays deleted
    against any later push (BR-UP-5, BR-UP-6)

Scenario: A disabled provider freezes the projection, not the reads
  Given the operator disables the provider in realm mustask
  When users are created and disabled in Keycloak afterwards
  Then the projection does not change and reads keep answering
  And when the provider is enabled again, the start reconciliation brings
    the projection to Keycloak's state within 5 minutes (US-4)
```
