# US-3: A project is created with its key, renamed, deactivated and restored

- story: US-3
- depends-on: US-1
- shippable: no — a project is the container that US-4..US-7 fill; one side of M1's structure
- status: pending

As a **signed-in user** (the organization administrator once mod-016 narrows
it, BR-M1-19), I want **to create a project with its key, and later rename,
deactivate and restore it**, so that **each body of work has its own place,
and its work item identifiers have a stable prefix (doc-002 W-10,
BR-M3-5)**.

## Acceptance criteria

```gherkin
Scenario: A project is created ready to use
  Given the organization «Пример организации»
  When a signed-in user creates the project «Пример проекта» with the key
    `PRM`
  Then the project is active and appears in the organization's project list
    with its name and its key
  And it has one team, «Пример проекта» (BR-M1-9)
  And it has a root area «Пример проекта», owned by that team for good and
    set as that team's default area (BR-M1-11, BR-M1-14)
  And it uses the organization's process (BR-M1-3)
  And it keeps who created it and when (BR-M1-21)

Scenario: The key is required and well-formed
  Given the create-project form with the name filled in
  When the key is left empty
  Then the action is refused with «Укажите ключ проекта»
  When the key is `prm`, `1PRM`, `P`, `PROJECTKEY01`, or `РМК` typed in
    Cyrillic letters
  Then each is refused with «Ключ проекта: от 2 до 10 латинских заглавных
    букв и цифр, начиная с буквы»
  And no project is created

Scenario: The key is unique in the organization, deactivated projects included
  Given a project with the key `PRM` in the organization «Альфа», active or
    deactivated
  When another project with the key `PRM` is created in «Альфа»
  Then the action is refused with «Ключ «PRM» уже используется в
    организации»
  And the key `PRM` is still free in the organization «Бета» (BR-M1-1)

Scenario: Two projects with one key at the same moment
  Given no project with the key `PRM` in «Альфа»
  When two projects with the key `PRM` are created there at the same moment
  Then exactly one exists, and the other creation is refused with «Ключ
    «PRM» уже используется в организации»

Scenario: The name is unique in the organization
  Given an active project «Пример проекта» in «Альфа»
  When another project named «пример проекта » is created in «Альфа», or an
    existing project there is renamed to it
  Then the action is refused with «Проект с таким названием уже есть в
    организации»
  And the name is still free in «Бета»
  And an empty name, or one longer than 100 characters, is refused with
    «Название должно содержать от 1 до 100 символов»

Scenario: A deactivated project's name stays reserved
  Given a deactivated project «Пример проекта» in «Альфа»
  When a new project «Пример проекта» is created in «Альфа»
  Then the action is refused with «Проект с таким названием уже есть в
    организации: он деактивирован» (BR-M1-1)

Scenario: Renaming a project renames its root area and keeps its key
  Given the project «Пример проекта» with the key `PRM`
  When it is renamed to «Платформа данных»
  Then it is listed under the new name
  And its root area is renamed «Платформа данных» with it (BR-M1-11)
  And its key, its teams and the rest of its area tree are unchanged; the
    first team keeps the name «Пример проекта»
  And no action anywhere changes the key (BR-M1-1)

Scenario: The project settings
  Given the project «Пример проекта»
  When a signed-in user opens its settings
  Then they see its name with the rename action, its key read-only,
    «Деактивировать», its teams, its area tree and the link «Процесс»
    (BR-M1-23)

Scenario: The project settings link to the organization's process
  Given the project «Пример проекта» of «Пример организации»
  When a signed-in user follows «Процесс» in the project settings
  Then mod-001's read-only process screen of «Пример организации» opens, at
    its own address, which carries the organization (D-c)
  And nothing in the project settings creates, changes or removes anything
    of the process (doc-007 R-6)

Scenario: A project is deactivated
  Given the project «Пример проекта» with teams and areas
  When a signed-in user deactivates it with «Деактивировать» in its settings
  Then they are on the organization's project list, where it is no longer
    among the active projects and is among the deactivated ones (BR-M1-24)
  And opening its address, or the address of one of its teams or areas,
    answers «Проект не найден»
  And nothing under it is deleted, and a request that changes anything
    under it answers «Проект не найден» and changes nothing until it is
    restored (BR-M1-20)

Scenario: A project identifier that names no project of the organization
  Given the organizations «Альфа» and «Бета», and the project «Отчётность»
    of «Бета»
  When an address or a request under «Альфа» names a project by a value
    that is not a well-formed identifier, by an identifier that names no
    project, or by the identifier of «Отчётность»
  Then the answer is «Проект не найден», and nothing changes (BR-M1-20,
    BR-M1-22)

Scenario: A deactivated project is restored exactly as it was
  Given the deactivated project
  When a signed-in user opens the organization's deactivated projects
  Then it is listed there with «Восстановить», and with no action that
    deletes it
  When they restore it
  Then it is back in the project list with its name, key, teams, default
    areas and area tree as before
```
