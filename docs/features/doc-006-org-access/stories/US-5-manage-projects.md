# US-5: An organization administrator manages projects

- story: US-5
- depends-on: US-2
- shippable: no — analysis only; delivered by `mod-002-organizations-projects-teams`
- status: pending

As an **organization administrator**, I want **to create projects in my
organization and to rename, deactivate and restore them**, so that **each
body of work has its own place, ready to use from the moment it is created**.

## Acceptance criteria

```gherkin
Scenario: A project is created ready to use
  Given an organization the user administers (US-2)
  When they create a project named «Пример проекта» with the key `PRM`
  Then the project exists with status active and that key, and is listed for
    every member of the organization, whatever their role
  And its first team is created automatically, named after the project,
    with the creating administrator as its only member (BR-M1-9)
  And a root area named after the project is created, owned by that team
    for good, and is that team's default area (BR-M1-11, BR-M1-14)
  And the project uses its organization's process (BR-M1-3)

Scenario: The project key is required
  Given an organization administrator on the create-project form
  When they submit a name but no key
  Then the action is refused with «Укажите ключ проекта»
  And no project is created

Scenario: A malformed key is refused
  Given an organization administrator on the create-project form
  When they submit the key `prm`, `1PRM`, `P` or `PROJECTKEY01`
  Then each is refused with «Ключ проекта: от 2 до 10 латинских заглавных
    букв и цифр, начиная с буквы» (BR-M1-1)
  And no project is created

Scenario: A key is unique in the organization, deactivated projects included
  Given a project with the key `PRM` in organization A, active or
    deactivated
  When an administrator of A creates another project with the key `PRM`
  Then the action is refused with «Ключ «PRM» уже используется в
    организации»
  And the key `PRM` is still free in organization B (BR-M1-1)

Scenario: Renaming a project renames its root area and keeps its key
  Given a project whose root area carries the project's name — the root has
    no rename of its own (BR-M1-11)
  When an administrator renames the project
  Then the project is listed under the new name for every member
  And its root area is renamed with it, while the rest of the area tree,
    the teams and the work items are untouched (BR-M1-11)
  And its key is unchanged, and no action anywhere changes it (BR-M1-1)

Scenario: The project name is unique inside its organization
  Given a project named «Пример проекта» in organization A
  When an administrator of A creates another project with the same name,
    differing only in letter case or surrounding spaces
  Then the action is refused with «Проект с таким названием уже есть в
    организации»
  And the same name is still free in organization B (BR-M1-1)

Scenario: Only an organization administrator creates a project
  Given a user with the role member and a user with the role viewer in the
    organization
  When either opens the project list
  Then the create-project action is not offered
  And invoking it directly is refused with «Недостаточно прав», and no
    project is created

Scenario: An administrator deactivates a project
  Given a project the organization no longer needs
  When its administrator deactivates it
  Then it disappears from the project list of every member and reader
  And its teams, areas and work items are unreachable, none of them
    hard-deleted (doc-001 BR-6)

Scenario: A deactivated project is reachable for restoring
  Given the project deactivated in the previous scenario
  When an administrator opens the organization's deactivated-projects list
  Then the project is listed there with the restore action and no purge
    action (BR-M1-1: projects are never purged)
  And the list is visible to administrators only

Scenario: An administrator restores a deactivated project
  Given a deactivated project
  When the administrator restores it
  Then it is listed again for every member of the organization
  And its key, teams, team membership, area tree and work items are exactly
    as they were before deactivation

Scenario: A deactivated project's name stays reserved
  Given a deactivated project named «Пример проекта»
  When an administrator creates a new project with that same name in the
    same organization
  Then the action is refused with «Проект с таким названием уже есть в
    организации: он деактивирован» (BR-M1-1)
```
