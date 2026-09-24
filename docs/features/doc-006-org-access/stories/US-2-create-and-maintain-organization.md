# US-2: A permitted user creates an organization and maintains it

- story: US-2
- depends-on: US-1
- shippable: no — analysis only; delivered by `mod-002-organizations-projects-teams`
- status: pending

As a **user holding the instance-level "create organizations" right**, I want
**to create an organization and later rename, deactivate or restore it**, so
that **my company has its own isolated space in MUSTASK and I can retire it
without losing its history (D-10, BR-6)**.

## Acceptance criteria

```gherkin
Scenario: An organization is created and its creator becomes its administrator
  Given a signed-in user holding the "create organizations" right (US-1)
  When they create an organization named «Пример организации»
  Then the organization exists with status active
  And the creator holds a membership in it with the role admin (D-9)
  And the organization's single process is seeded by M2 at that moment
    (BR-M1-3) — M1 does not define what a process is
  And the organization has no projects yet, and the project list shows
    «Пока нет проектов» with the create-project action

Scenario: The organization name is unique instance-wide
  Given an organization named «Пример организации» already exists
  And a permitted user who is not a member of it
  When that user creates an organization with the same name, differing only
    in letter case or surrounding spaces
  Then the action is refused with «Название «Пример организации» уже
    занято»
  And no second organization is created (BR-M1-1: names are public within
    the instance)
  And the message is the same whether that organization is active or
    deactivated, and says nothing else about it (BR-M1-8)

Scenario: Two simultaneous creations of the same name produce one organization
  Given no organization named «Пример организации» exists
  When two permitted users create an organization with that name at the
    same moment
  Then exactly one organization named «Пример организации» exists
  And the other user is refused with «Название «Пример организации» уже
    занято» (NFR Concurrency)

Scenario: An empty or over-long name is refused
  Given a permitted user on the create-organization form
  When they submit an empty name, or a name longer than 100 characters
  Then the action is refused with «Название должно содержать от 1 до 100
    символов»
  And no organization is created

Scenario: A failed process seed leaves nothing behind
  Given a permitted user creating an organization named «Пример организации»
  And M2 fails to seed the organization's process
  When the creation is submitted
  Then the action is refused with «Не удалось создать организацию.
    Попробуйте ещё раз»
  And no organization named «Пример организации» exists, and no membership
    was created for it (BR-M1-3)
  And a retry with the same name succeeds once the seed works

Scenario: An administrator renames the organization
  Given an organization the user administers
  When they rename it to a name that is free instance-wide
  Then every member sees the new name
  And the organization's projects, members, teams and areas are unchanged
  And renaming it to a name that is taken is refused with the same
    «Название «…» уже занято» message, naming the taken name

Scenario: A member or reader cannot rename or deactivate the organization
  Given a user whose membership role in the organization is member, and a
    user whose role is viewer
  When either opens the organization's settings
  Then the rename, deactivate and restore actions are not offered
  And invoking them directly is refused with «Недостаточно прав»

Scenario: An administrator deactivates an organization
  Given an organization with projects and work items that is no longer used
  When its administrator deactivates it
  Then it disappears from every member's organization list
  And its projects, teams, areas and work items are unreachable, none of
    them hard-deleted (BR-6)
  And an administrator still sees it in the deactivated list, with the
    restore action and no purge action (BR-M1-1: MVP has no purge)

Scenario: An administrator restores a deactivated organization
  Given the organization deactivated in the previous scenario
  When its administrator restores it
  Then it is listed again for every member, with its projects, teams, areas,
    memberships and work items exactly as before

Scenario: A user who belongs to no organization sees an explicit empty state
  Given a signed-in user with no membership in any organization
  When they open MUSTASK
  Then they see «Вы пока не состоите ни в одной организации», not an empty
    list indistinguishable from a loading error
  And the create action is shown only if they hold the "create
    organizations" right; otherwise the text tells them to ask an
    administrator to add them
```
