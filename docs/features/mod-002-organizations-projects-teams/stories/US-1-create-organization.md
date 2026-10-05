# US-1: A user holding the right creates an organization

- story: US-1
- depends-on: -
- shippable: no — an organization with no project in it gives nobody anything to work in; ships with US-3
- status: pending

As a **signed-in user holding the "create organizations" right**, I want **to
create an organization**, so that **my company gets its own space in MUSTASK,
with the process its work items will follow (doc-010 D-10, BR-M1-3)**.

## Acceptance criteria

```gherkin
Scenario: An organization is created with its process
  Given a signed-in user holding the "create organizations" right
  When they create an organization named «Пример организации»
  Then the organization exists with status active and is in the organization
    list
  And its process was seeded by mod-001's seed at that moment and is recorded
    as the organization's one process (BR-M1-3)
  And the organization keeps who created it and when (BR-M1-21)
  And its project list shows «Пока нет проектов» with the create-project
    action

Scenario: The name is trimmed and its length checked
  Given a user holding the right, on the create-organization form
  When they submit «  Пример организации  »
  Then the organization is named «Пример организации»
  When they submit an empty name, a name of spaces only, or a name longer
    than 100 characters
  Then the action is refused with «Название должно содержать от 1 до 100
    символов»
  And no organization is created

Scenario: The name is unique instance-wide, deactivated organizations included
  Given an organization named «Пример организации», active or deactivated
  When a user holding the right creates an organization named «пример
    организации », differing only in letter case and surrounding spaces
  Then the action is refused with «Название «Пример организации» уже занято»
  And the message is the same for an active and a deactivated organization,
    and says nothing else about it (BR-M1-1)

Scenario: Two simultaneous creations of one name produce one organization
  Given no organization named «Пример организации» exists
  When two users holding the right create an organization with that name at
    the same moment
  Then exactly one organization named «Пример организации» exists
  And the other user is refused with «Название «Пример организации» уже
    занято»
  And the refused creation leaves no seeded process behind (BR-M1-3)

Scenario: A failed process seed leaves nothing behind
  Given the seed fails while «Пример организации» is being created
  When the creation is submitted
  Then the action is refused with «Не удалось создать организацию.
    Попробуйте ещё раз»
  And no organization named «Пример организации» exists, and the name is free
    (BR-M1-3)
  And a retry with the same name succeeds once the seed works

Scenario: Without the right the action is neither offered nor accepted
  Given a signed-in user who does not hold the "create organizations" right
  When they open the organization list
  Then the create-organization action is not offered
  And invoking it directly, bypassing the interface, is refused with
    «Недостаточно прав», and no organization is created

Scenario: The right is granted and revoked in the Keycloak console
  Given a signed-in user without the right
  When a Keycloak realm administrator assigns them, in the Keycloak console,
    the realm role or group that carries it
  And their token is renewed by signing in again
  Then the create-organization action is offered to them
  When they create «Организация Б» and the administrator then removes that
    role or group, and their token is renewed again
  Then the action is no longer offered
  And «Организация Б» still exists, unchanged (BR-M1-2)

Scenario: MUSTASK has no screen that grants the right
  Given a signed-in user holding every code the realm import declares
  When they look through every MUSTASK screen
  Then no screen or action grants or revokes "create organizations", to
    anyone (BR-M1-2)

Scenario: No organization yet
  Given an instance with no active organization
  When a signed-in user holding the right opens the organization list
  Then they see «Организаций пока нет» with the create-organization action
  And a signed-in user without the right sees «Организаций пока нет» with the
    hint «Право на создание организаций выдаёт администратор» and no action

Scenario: Until mod-016, every signed-in user sees every organization
  Given organizations «Альфа» and «Бета», created by two different users
  When a third signed-in user, who does not hold the right, opens the
    organization list
  Then both are listed, and both open (BR-M1-19)

Scenario: An anonymous request is refused
  Given a request with no signed-in user
  When it attempts any operation of this task, listing or creating
    organizations among them
  Then it is refused as unauthenticated, and nothing is created or changed
```
