# US-1: The seed command creates an organization's complete process, once per organization identifier

- story: US-1
- depends-on: -
- shippable: no — until `mod-002-organizations-projects-teams` merges, only the Development stand's demonstration seed calls the command, and nothing shows its result without US-2
- status: pending

As **the organization-creation flow of
`mod-002-organizations-projects-teams`**, I want **one call that seeds a
complete, ready-to-use process for a new organization identifier and hands
back its identity**, so that **every organization starts with a process its
projects can type and state work items by, with nobody configuring anything
first**.

## Acceptance criteria

> **Superseded in part by bug-004 D-3 (2026-10-12), one name, one meaning:**
> «Разрешена» is in the category Resolved on «Эпик», «Фича» and
> «Пользовательская история» as on «Баг», so the first scenario's "each in the
> category stated there" reads doc-007's table as amended, and no state name
> of the seeded process has two categories (`StateNameRule`; the seed refuses
> a breach). Identities stay separate, as the last line of that scenario says.
> The scenarios are left as written.

```gherkin
Scenario: A new organization identifier receives the complete seeded process
  Given an organization identifier no process has been seeded for
  When the seed command is invoked with it
  Then exactly one process exists for that identifier
  And the command hands back that process's identity (BR-M2-1)
  And the process defines exactly the five types «Эпик», «Фича»,
    «Пользовательская история», «Задача» and «Баг», with the keys EPIC, FEAT,
    US, TASK and BUG respectively (BR-M2-9)
  And each type has exactly the states doc-007 § "MVP: the seeded process"
    lists for it, in that order, each in the category stated there, with
    «Удалена» last and the first state as the type's initial state (BR-M2-2)
  And each state holds its category as doc-007's category code, one of
    Proposed, InProgress, Resolved, Completed and Removed, and not as a
    label (status.md D-6)
  And «Задача» has no state in the Resolved category
  And each type is on exactly one of the four backlog levels «Эпики», «Фичи»,
    «Истории» and «Задачи», as doc-007's level table states, and each level
    carries the rank and kind stated there (BR-M2-4)
  And each type carries exactly one appearance token set by the seed, and
    no icon or colour of its own: «Эпик» `epic`, «Фича» `feature`,
    «Пользовательская история» `story`, «Задача» `task` and «Баг» `bug`, so
    no two types share a token (doc-007 P-5; status.md D-6)
  And no two types, states or backlog levels share an identity, even where
    their labels are equal: each type's «Новая» is a state of its own, and so
    is «Разрешена» on «Пользовательская история» and on «Баг» (BR-M2-9)

Scenario: Each organization identifier gets a process of its own
  Given an organization identifier that already has a seeded process
  When the seed command is invoked with a second, different identifier
  Then a second process exists, with the same types, keys, states,
    categories and backlog levels as the first
  And the two processes share no type, state or backlog level identity
    (BR-M2-1)

Scenario: A failed seed leaves nothing behind and says so
  Given an organization identifier no process has been seeded for
  And the seed command fails partway through for it, e.g. on a storage error
  When the command returns
  Then the caller is told the seed failed, and receives no process identity
  And no process, type, state or backlog level exists for that identifier
    (BR-M2-8)
  And a later call with the same identifier creates the complete process

Scenario: A malformed identifier is refused and nothing is created
  Given a value that is not a well-formed organization identifier, the empty
    identifier included (status.md D-10)
  When the seed command is invoked with it
  Then the caller is told the seed failed, and receives no process identity
  And no process, type, state or backlog level is created (BR-M2-9)

Scenario: A caller's failed operation takes the seed with it
  Given a caller that runs the seed inside its own operation, as creating an
    organization does
  And the seed completes for an identifier that had no process
  When a later step of the caller's operation fails
  Then no process, type, state or backlog level exists for that identifier
    (BR-M2-8)

Scenario: A repeated call creates nothing and hands back the same process
  Given an organization identifier the seed has completed for
  When the seed command is invoked again with the same identifier
  Then nothing new is created
  And the command hands back the identity of the process that already exists
    (BR-M2-9)

Scenario: Two racing calls leave exactly one process
  Given an organization identifier no process has been seeded for
  When two seed calls for it run at the same time
  Then exactly one process exists for that identifier
  And none of its types, keys, states or backlog levels is duplicated
  And each call either hands back that process's identity or reports a
    failure (BR-M2-9)

Scenario: No person can seed or change a process
  Given MUSTASK as this task leaves it
  When the requests it accepts are listed from its published API description
  Then none of them seeds a process
  And none creates, renames, reorders or deletes a process, type, key, state,
    appearance token or backlog level (BR-M2-13)
```
