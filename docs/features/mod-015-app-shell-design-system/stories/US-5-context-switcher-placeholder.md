# US-5: The context switcher works on placeholder data

- story: US-5
- depends-on: US-2, US-4
- shippable: no — a slot on placeholder data; its real data arrives with mod-002 (BR-11, D-10)
- status: pending

As **a signed-in user**, I want **to see and change which organization,
project and team I am working in from the top bar**, so that **once the
organization module lands, every board, backlog and list opens in the
right context without my hunting for it**.

The placeholder dataset (BR-11), fixed and shipped with the panel:

| Organization | Project | Teams |
|---|---|---|
| «Пример организации 1» | «Пример проекта 1» | «Пример команды 1», «Пример команды 2» |
| «Пример организации 1» | «Пример проекта 2» | «Пример команды 3» |
| «Пример организации 2» | «Пример проекта 3» | «Пример команды 4» |

## Acceptance criteria

```gherkin
Scenario: Initial context (BR-11, D-11)
  Given a signed-in user on «Главная»
  Then the top bar's switcher shows three levels: «Пример организации 1»,
    «Пример проекта 1», «Пример команды 1»
  And their accessible names are «Организация: Пример организации 1»,
    «Проект: Пример проекта 1», «Команда: Пример команды 1»

Scenario: Levels list only what belongs to the level above
  Given the initial context
  When the user opens the project level
  Then it lists «Пример проекта 1» and «Пример проекта 2», the first marked
    as current
  When they open the team level
  Then it lists «Пример команды 1» and «Пример команды 2» only

Scenario: Changing a level resets the levels below it
  Given the initial context
  When the user chooses «Пример организации 2» at the organization level
  Then the switcher shows «Пример организации 2», «Пример проекта 3»,
    «Пример команды 4»
  When they choose «Пример организации 1» again and then «Пример проекта 2»
  Then the team level shows «Пример команды 3»

Scenario: No request, no persistence
  Given the switcher in a component test whose network mock fails the test
    on any request
  When the user opens every level and changes the selection
  Then the test passes: the switcher sent no request
  And in a browser, the network log shows no request while the switcher is
    used other than the session's own periodic token renewal
  When the user reloads the page
  Then the switcher shows the initial context again

Scenario: Screens of this task ignore the context (BR-18)
  Given a dictionaries reader on «Языки» showing its first page
  When they choose «Пример организации 2»
  Then the address, the rows and the navigation stay the same
  And the registry sends no new request

Scenario: «Все организации» waits for its page (BR-11, BR-7, D-20)
  Given a signed-in user in this task's build
  When they open the organization level
  Then it lists «Пример организации 1» and «Пример организации 2» and no
    «Все организации»

Scenario: A long name is truncated, not lost (BR-11, D-22)
  Given the switcher in a component test with an organization whose name
    is 100 characters long, the longest mod-002 allows (its BR-M1-1)
  Then the organization level's accessible name is «Организация: » followed
    by the full name
  And hovering over it or focusing it shows the full name as a tooltip
  And, measured by QA in a browser 1024 px wide, the level shows the name on
    one line, cut short with an ellipsis

Scenario: No organizations
  Given the switcher in a component test with a dataset that has no
    organizations
  Then it shows «Нет организаций» in place of the organization level, as
    text that is not a link in this task's build (D-20)
  And the project and team levels are not shown

Scenario: An organization with no projects
  Given the switcher in a component test with a dataset whose current
    organization has no projects
  Then the project level shows «Нет проектов»
  And the team level is not shown

Scenario: States mod-002 will wire are designed now (BR-8, BR-11, BR-18)
  Given `design.md` and the prototype
  Then they show the switcher loading, failing to load with «Повторить»,
    with no organizations, with an organization without projects, and with
    a remembered selection that is no longer available
  And they show the organization level ending with «Все организации», and
    «Нет организаций» as a link, both opening a placeholder organization
    list page (D-20)
  And they show a project-level page at an address under
    `/organizations/:organizationId/projects/:projectId/`, the switcher
    showing that context and the team level the project's first team (D-19)
  And they show that switching organization there re-evaluates the
    navigation, and opens the landing page of the same navigation entry in
    the new context or, where that entry is not visible, «Главная»
  And they show a work item's page doing the same: a switch opens its
    entry's landing page in the new context
  And they show an organization-level page whose project and team levels
    change without the page changing
  And they show links into an organization, a project and a team that are
    not found, answering «Организация не найдена», «Проект не найден» and
    «Команда не найдена», each with «На главную», the switcher keeping its
    selection (BR-18)
  And they show a long name truncated in the switcher (BR-11)
  And they show the switcher in the narrow-window layout of US-4
```
