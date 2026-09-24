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
  When they create a project named «Пример проекта»
  Then the project exists with status active and is listed for every member
    of the organization, whatever their role
  And exactly one team is created for it automatically, named after the
    project, with the creating administrator as its only member (BR-M1-9)
  And a root area named after the project is created, owned by that team
    (BR-M1-11)
  And the project uses its organization's process (BR-M1-3)

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

Scenario: Renaming a project renames its root area
  Given a project whose root area carries the project's name — the root has
    no rename of its own (BR-M1-11)
  When an administrator renames the project
  Then the project is listed under the new name for every member
  And its root area is renamed with it, while the rest of the area tree,
    the team and the work items are untouched (BR-M1-11)

Scenario: An administrator deactivates a project
  Given a project the organization no longer needs
  When its administrator deactivates it
  Then it disappears from the project list of every member and reader
  And its teams, areas and work items are unreachable, none of them
    hard-deleted (BR-6)

Scenario: A deactivated project is reachable for restoring
  Given the project deactivated in the previous scenario
  When an administrator opens the organization's deactivated-projects list
  Then the project is listed there with the restore action and no purge
    action (BR-M1-1: MVP has no purge)
  And the list is visible to administrators only

Scenario: An administrator restores a deactivated project
  Given a deactivated project
  When the administrator restores it
  Then it is listed again for every member of the organization
  And its team membership, area tree and work items are exactly as they
    were before deactivation

Scenario: A deactivated project's name stays reserved
  Given a deactivated project named «Пример проекта»
  When an administrator creates a new project with that same name in the
    same organization
  Then the action is refused with «Проект с таким названием уже есть в
    организации: он деактивирован» (BR-M1-1)
```
