# US-4: Teams are added, renamed and given their default area

- story: US-4
- depends-on: US-3
- shippable: no — a team matters only through the areas it owns (US-5, US-6); one side of M1's structure
- status: pending

As a **signed-in user** (the organization administrator once mod-016 narrows
it, BR-M1-19), I want **to add teams to a project, rename them, and set where
each team's new work lands**, so that **every team has its own part of the
area tree (doc-010 D-7, doc-001 BR-3)**.

## Acceptance criteria

```gherkin
Scenario: A second team comes with its own area
  Given the project «Пример проекта» with its first team (US-3)
  When a signed-in user adds the team «Платформа»
  Then the project has the teams «Пример проекта» and «Платформа»
  And the root has a child area «Платформа», owned by the team «Платформа»
    and set as its default area (BR-M1-9, BR-M1-14)

Scenario: A team's name is unique among the project's active teams
  Given the project has a team «Платформа»
  When another team of the project is added as, or renamed to, «платформа »
  Then the action is refused with «Команда с таким названием уже есть в
    проекте»
  And «Платформа» is still free as a team name in another project
  And an empty name, or one longer than 100 characters, is refused with
    «Название должно содержать от 1 до 100 символов»

Scenario: A new team's area name must be free under the root
  Given the root already has a child area «Аналитика», and there is no team
    of that name
  When the team «Аналитика» is added
  Then the action is refused with «Область с таким названием уже есть на
    этом уровне» (BR-M1-9, BR-M1-12)
  And no team and no area is created

Scenario: Two teams of one name at the same moment
  Given the project has no team «Платформа»
  When two teams named «Платформа» are added at the same moment
  Then exactly one exists, with exactly one child area «Платформа» under the
    root
  And the other is refused

Scenario: A team is renamed
  Given the team «Платформа», owning the areas «Платформа» and
    «Платформа / API»
  When it is renamed to «Платформа данных»
  Then the new name is shown wherever the team is listed or offered
  And its areas keep their names, and its ownership and default area are
    unchanged (BR-M1-9)

Scenario: A team's default area is chosen among the areas it owns explicitly
  Given the team «Платформа» owns «Платформа» and «Платформа / API»
    explicitly, and «Платформа / API / Шлюз» only by inheritance
  When a signed-in user opens the team's default-area picker
  Then it offers «Платформа» and «Платформа / API» only
  When they choose «Платформа / API»
  Then the team's default area is «Платформа / API» (BR-M1-14)
  And setting, by a direct request, an area the team does not own explicitly
    is refused with «Область по умолчанию должна принадлежать самой команде»,
    and the default is unchanged

Scenario: The first team's default is the root, for good
  Given the project's first team «Пример проекта»
  When a signed-in user opens its settings
  Then its default area is the root, and no action to change it is offered
  And a direct request to change it is refused, and nothing changes
    (BR-M1-14)

Scenario: The project's teams
  Given the project with the teams «Пример проекта» and «Платформа»
  When a signed-in user opens the project's teams
  Then every active team is listed with its default area
  And «Пример проекта» is marked «Владеет корневой областью», the one team
    that cannot be deleted (US-7)

Scenario: A team identifier that names no team of the project
  Given the project «Пример проекта», and the team «Ядро» of another project
  When an address or a request under «Пример проекта» names a team by a
    value that is not a well-formed identifier, by an identifier that names
    no team, or by the identifier of «Ядро»
  Then the answer is «Команда не найдена», and nothing changes (BR-M1-20,
    BR-M1-22)
```
