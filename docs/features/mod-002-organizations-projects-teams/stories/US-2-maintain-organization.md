# US-2: The organization is renamed, deactivated and restored

- story: US-2
- depends-on: US-1
- shippable: no — it maintains the organization US-1 creates; one side of M1's structure
- status: pending

As a **signed-in user** (the organization administrator once mod-016 narrows
it, BR-M1-19), I want **to rename an organization, and to deactivate and later
restore it**, so that **its name stays current, and a retired organization
leaves every list without losing anything (doc-001 BR-6)**.

## Acceptance criteria

```gherkin
Scenario: The organization settings
  Given the active organization «Пример организации»
  When a signed-in user opens its settings
  Then they see its name with the rename action, and «Деактивировать»
    (BR-M1-23)

Scenario: The organization is renamed
  Given «Пример организации», with projects, teams and areas
  When it is renamed to «Пример организации 2», a name free instance-wide
  Then it is listed under the new name for every signed-in user
  And its projects, teams and areas are unchanged
  And renaming it to «пример организации 2», its own name in another letter
    case, is allowed (BR-M1-1)

Scenario: A rename to a taken or invalid name is refused
  Given organizations «Альфа» and «Бета»
  When «Бета» is renamed to «альфа»
  Then the action is refused with «Название «Альфа» уже занято»
  When «Бета» is renamed to an empty name, or one longer than 100 characters
  Then the action is refused with «Название должно содержать от 1 до 100
    символов»
  And «Бета» keeps its name

Scenario: Two renames to one free name keep one
  Given organizations «Альфа» and «Бета», and no organization «Гамма»
  When both are renamed to «Гамма» at the same moment
  Then exactly one organization is named «Гамма»
  And the other rename is refused with «Название «Гамма» уже занято»

Scenario: The organization is deactivated
  Given «Пример организации» with the project «Пример проекта»
  When a signed-in user deactivates it with «Деактивировать» in its settings
  Then they are on the organization list, where it is no longer among the
    active organizations and is among the deactivated ones (BR-M1-24)
  And opening its address, or the address of its project or of anything in
    that project, answers «Организация не найдена» (BR-M1-20)
  And opening its process screen (mod-001), or reading its process by a
    direct request, answers «Организация не найдена» (BR-M1-20, D-g)
  And nothing under it is deleted

Scenario: A deactivated organization takes no change
  Given a deactivated organization
  When a request renames it, or creates, changes, deactivates or deletes
    anything under it
  Then the request is answered «Организация не найдена», and nothing changes

Scenario: An unknown organization answers not found
  Given an organization identifier that names no organization, or a value
    that is not a well-formed identifier
  When its settings, its project list or its process screen is opened, or a
    request addresses anything under it
  Then the answer is «Организация не найдена», and nothing changes
    (BR-M1-20, D-g)

Scenario: A deactivated organization is listed separately, with no purge
  Given the organization deactivated above
  When a signed-in user opens the deactivated organizations
  Then «Пример организации» is listed there with «Восстановить», and with no
    action that deletes it (BR-M1-1, BR-M1-20)
  And its name is still taken for new organizations (US-1)

Scenario: The organization is restored exactly as it was
  Given the deactivated «Пример организации», whose project «Старый проект»
    had been deactivated on its own before the organization was
  When a signed-in user restores it with «Восстановить»
  Then it is back in the organization list with its name, process, projects,
    teams, default areas and area trees as before
  And its process screen opens again
  And «Старый проект» is still deactivated and is listed among the
    organization's deactivated projects (BR-M1-20)
```
