# US-2: A signed-in user reads a seeded process definition, read-only

- story: US-2
- depends-on: US-1
- shippable: no — it shows only what US-1 seeds, and ships with US-1 in this task's single queue line
- status: pending

As **a signed-in MUSTASK user**, I want **to see an organization's process
definition, with no way to change it**, so that **I understand how that
organization's work items are typed and stated before I ever open one**.

## Acceptance criteria

> **Superseded in part by bug-004 D-3 (2026-10-12), one name, one meaning:**
> the scenario "One state label belongs to two categories" no longer holds.
> «Разрешена» shows the category «Решённые» on «Эпик», «Фича», «Пользовательская
> история» and «Баг» alike, in one colour, and no state name of the process
> shows two categories. The screen's description lost its second sentence
> («У разных типов один и тот же статус может относиться к разным
> категориям.», design T-03), which said the opposite. The scenarios are left
> as written.

```gherkin
Scenario: A seeded process is shown in full
  Given an organization identifier a process has been seeded for (US-1)
  And a signed-in MUSTASK user
  When they open `/organizations/<that identifier>/settings/process`
  Then the screen shows the five types «Эпик», «Фича», «Пользовательская
    история», «Задача» and «Баг»
  And for each type it shows the key, the states in the seeded order with the
    initial state marked, each state's category label, the type's appearance
    token, and the backlog level (BR-M2-13)
  And if `mod-015` is in the `main` this task's branch is based on, the
    screen renders in `mod-015`'s shell, the token is drawn through the
    theme, each state shows its category's theme colour beside its category
    label (mod-015 BR-4), and loading, a failed read and not found use
    `mod-015`'s canon frames
  And otherwise the screen renders inside the panel's current application
    layout and shows the token by its name as plain text, with no category
    colour (BR-M2-12, BR-M2-13; status.md D-6)

Scenario: The Development stand shows a demonstration process
  Given a Development stand whose database schema is migrated
  When the API starts
  Then a process exists for the demonstration organization identifier that
    `src/backend/AGENTS.md` names, seeded through the seed command (BR-M2-1)
  And a signed-in user who opens the screen's address with that identifier
    sees the full definition
  And starting the stand again creates nothing new (BR-M2-9)
  And the API started in any environment other than Development seeds no
    process at startup (BR-M2-1)

Scenario: An unmigrated Development stand starts without the demonstration process
  Given a Development stand whose database has every earlier migration
    this build ships applied, but not this task's
  When the API starts
  Then the start completes, and the log says the demonstration seed was
    skipped
  And no process exists for the demonstration identifier (BR-M2-1)
  # With no migration applied at all, the start already stops by design
  # (BR-M2-1; ADR-0002, ADR-0021); this task does not change that.

Scenario: A failed demonstration seed does not stop the stand
  Given a Development stand whose database schema is migrated
  And the demonstration seed fails at startup, e.g. on a storage error
  When the API starts
  Then the start completes, and the log records the seed's failure
  And nothing of a process exists for the demonstration identifier
  And the next start seeds the complete process (BR-M2-1, BR-M2-8)

Scenario: Every signed-in user sees the same definition
  Given an organization identifier a process has been seeded for
  And two signed-in users: one holds every permission code the realm grants,
    the other holds none
  When each of them opens the screen for that identifier
  Then both see the full definition
  And what they see is identical (BR-M2-10, BR-M2-13)

Scenario: Categories are shown by their Russian labels
  Given the screen showing a seeded process
  When a user reads the states of «Задача»
  Then «Новая» shows the category «Новые», «Активна» shows «В работе»,
    «Закрыта» shows «Завершённые» and «Удалена» shows «Удалённые»
  And no category anywhere on the screen is shown by its English name or
    its category code (doc-010 D-5; status.md D-6)

Scenario: One state label belongs to two categories
  Given the screen showing a seeded process
  When a user compares «Пользовательская история» and «Баг»
  Then «Разрешена» of «Пользовательская история» shows the category
    «В работе»
  And «Разрешена» of «Баг» shows the category «Решённые» (doc-010 D-4)

Scenario: An identifier with no process is not found
  Given a signed-in MUSTASK user
  When they open the screen's address with an identifier no process has been
    seeded for, or with a value that is not a well-formed identifier
  Then they see «Организация не найдена»
  And no type, state or backlog level is shown, and none is in the answer to
    the request for the process definition
  And that answer carries the error code `organizations.organization.not_found`
    for the unknown and the malformed identifier alike (BR-M2-11)

Scenario: The not-found code is registered with its texts
  Given the error catalog as this task leaves it
  When the code `organizations.organization.not_found` is looked up in it
  Then it exists, in the organizations module's catalog
  And its Russian text says «Организация не найдена» and it has a Kazakh text
  And the generated error-code reference lists it (BR-M2-11)

Scenario: A failed read is not taken for a missing organization
  Given a signed-in MUSTASK user
  And an organization identifier a process has been seeded for
  When they open the screen's address for it and the read of the process
    fails, e.g. the server does not answer
  Then the screen says the process could not be loaded and offers to retry
  And it shows neither «Организация не найдена» nor any part of the process
    (BR-M2-11)

Scenario: A visitor who is not signed in sees nothing
  Given a visitor who is not signed in
  And an organization identifier a process has been seeded for
  When they open the screen's address for that identifier
  Then they are sent to sign in before anything of the process is shown
  And a request for the process definition without a valid sign-in is
    refused, with no type, state or backlog level in the answer (BR-M2-10)

Scenario: The read's code is registered, and nothing grants it yet
  Given MUSTASK as this task leaves it
  When its registered permission codes are compared with its Keycloak realm
    import
  Then `process.definition.read` is registered under that exact name, as an
    organization-scoped code, outside the catalog the realm import must match
  And the realm import declares no role of that name, and no realm role or
    group grants it
  And no code registered as organization-scoped appears in the realm import
    (BR-M2-10)

Scenario: No visible entry or link leads to the screen before mod-002
  Given the panel as this task leaves it, before `mod-002` brings real
    organizations
  When a signed-in user looks through its navigation, its menus and the
    screens that existed before this task
  Then no visible entry or link leads to the process-definition screen
  And where this task registers the «Процесс» entry (BR-M2-12), the
    navigation model holds it under «Настройки» with the code
    `process.definition.read`, hidden while the switcher reads placeholder
    data, so it is checked in the navigation model, not on screen
  And opening the screen's address with a seeded identifier shows the
    process (BR-M2-12)

Scenario: Nothing on the screen changes the process
  Given the screen showing a seeded process
  When a user looks for a way to create, rename, reorder or delete a type, a
    key, a state, an appearance token or a backlog level
  Then there is no such control anywhere on the screen (BR-M2-13)
```
