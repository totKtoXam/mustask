# US-7: A team is deleted and its areas pass to the first team

- story: US-7
- depends-on: US-4, US-6
- shippable: no — it removes what US-4 adds and re-owns what US-6 assigns; bound to the soft-deactivation ADR this task owes
- status: pending

As a **signed-in user** (the organization administrator once mod-016 narrows
it, BR-M1-19), I want **to delete a team that is no longer needed**, so that
**its areas, and later its work, pass to the project's first team, and the
team leaves every list for good (doc-010 D-6, D-23 (a))**.

## Acceptance criteria

```gherkin
Scenario: A team is deleted after a confirmation, and its areas pass to the first team
  Given the project «Пример проекта», whose team «Платформа» owns the areas
    «Платформа» and «Платформа / API» explicitly, and «Платформа / API /
    Шлюз» by inheritance
  When a signed-in user chooses «Удалить команду» for «Платформа»
  Then a confirmation reads «Удалить команду «Платформа»? Её области перейдут
    команде «Пример проекта». Восстановить команду будет нельзя.»
  When they confirm
  Then «Платформа» is no longer among the project's teams, and no picker
    offers it
  And «Платформа» and «Платформа / API» are owned explicitly by «Пример
    проекта», and «Платформа / API / Шлюз» resolves to it by inheritance
    (BR-M1-18)
  And the areas keep their names and their places in the tree
  And no action restores the team

Scenario: Declining the confirmation changes nothing
  Given the confirmation for deleting «Платформа» is shown
  When the user cancels it
  Then the team, its areas, their owners and its default area are unchanged

Scenario: The first team cannot be deleted
  Given the project's first team «Пример проекта», the owner of the root
  When a signed-in user looks at its actions
  Then «Удалить команду» is not offered
  And a direct request to delete it is refused with «Нельзя удалить команду,
    которой принадлежит корневая область проекта», and nothing changes

Scenario: The deleted team's name is free, but its area's name is not
  Given the team «Платформа» was deleted, and the root still has the child
    area «Платформа»
  When a team «Платформа» is added
  Then it is refused with «Область с таким названием уже есть на этом
    уровне» (BR-M1-9)
  When that area is renamed «Платформа (архив)» and the team is added again
  Then the team «Платформа» is created, with a new child area «Платформа» of
    the root as its default area

Scenario: A deleted team answers not found
  Given the deleted team «Платформа»
  When its address is opened, or a request renames it, changes its default
    area, deletes it again or makes it an area's owner
  Then the answer is «Команда не найдена», and nothing changes

Scenario: Other modules read the deleted status
  Given the deleted team «Платформа»
  When another module lists the project's teams, or asks for «Платформа» by
    its identity
  Then the list leaves it out, and the answer for «Платформа» says it is
    deleted, never that it is active (BR-M1-18, doc-010 PR-9)

Scenario: A re-own to a team and that team's deletion at the same moment
  Given the team «Платформа» and the area «Отчёты», owned by the first team
  When «Отчёты» is re-owned to «Платформа» at the same moment «Платформа» is
    deleted
  Then one of the two is refused, or the deletion passes «Отчёты» to the
    first team
  And in no order does an area end up owned by a deleted team (NFR
    Concurrency)
```
