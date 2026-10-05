# US-8: The shell's context switcher shows real organizations, projects and teams

- story: US-8
- depends-on: US-1, US-2, US-3, US-4, US-7
- shippable: no — it fills mod-015's switcher with the containers US-1, US-3 and US-4 create, and drops what US-2, US-3 and US-7 deactivate or delete; without them it has nothing to show
- status: pending

As a **signed-in user**, I want **the top bar's switcher to offer the real
organizations, projects and teams, and to open this task's pages in the
context I choose**, so that **I reach an organization, a project and their
settings without typing an address (mod-015 US-5, BR-11; D-b, D-d)**.

The switcher's look, its levels and its loading, failed and empty states are
mod-015's (BR-11, US-5). This story replaces its placeholder data and settles
what mod-015 leaves to this task: the lists, the default team, the
remembered selection and its fallback, and the entry to the organization
list (BR-M1-24).

## Acceptance criteria

```gherkin
Scenario: The switcher lists the active organizations, projects and teams
  Given the active organizations «Альфа» and «Бета» and the deactivated
    organization «Гамма»
  And «Альфа» with the active projects «Пример проекта» and «Аналитика» and a
    deactivated project
  And «Пример проекта» with its first team «Пример проекта», the team
    «Платформа» and a deleted team
  When a signed-in user opens the switcher's organization level
  Then it lists «Альфа» and «Бета», in alphabetical order, then «Все
    организации», and never «Гамма» (BR-M1-19, BR-M1-20)
  When they choose «Альфа» and open the project level
  Then it lists «Аналитика» and «Пример проекта», in alphabetical order, and
    not the deactivated project
  When they choose «Пример проекта» and open the team level
  Then it lists «Пример проекта» first, then «Платформа», and not the
    deleted team (BR-M1-18)

Scenario: The default team is the project's first team
  Given the project «Пример проекта» with the teams «Пример проекта» and
    «Платформа»
  When a signed-in user chooses «Пример проекта» at the project level
  Then the team level shows «Пример проекта», the team that owns the root
    (BR-M1-9; mod-016 makes the default follow team membership)

Scenario: «Все организации» opens the organization list
  Given a signed-in user with «Альфа» chosen in the switcher
  When they choose «Все организации» at the organization level
  Then the organization list opens with the active organizations, the
    deactivated ones, and the create action if they hold the right (US-1,
    BR-M1-20, BR-M1-23)
  And the switcher still shows «Альфа»

Scenario: With no organization, «Нет организаций» leads to the list
  Given an instance with no active organization
  When a signed-in user looks at the switcher
  Then it shows «Нет организаций», and no project or team level
  When they follow «Нет организаций»
  Then the organization list opens with «Организаций пока нет» (US-1)

Scenario: The settings are reached through the shell's navigation
  Given «Альфа» and «Пример проекта» chosen in the switcher
  When a signed-in user opens «Настройки»
  Then the settings navigation shows the entries this task registers under
    «Организация» and «Проект» (mod-015 § Navigation model, BR-8)
  And through them the settings of «Альфа», its project list and the
    settings of «Пример проекта» open (BR-M1-23)

Scenario: The address sets the switcher
  Given the switcher shows «Бета»
  When a signed-in user opens the address of the settings of «Пример
    проекта» in «Альфа»
  Then the switcher shows «Альфа», «Пример проекта» and the team «Пример
    проекта», its first team (mod-015 BR-11, BR-18)
  And that becomes the remembered selection
  When they open an address under a deactivated organization
  Then it answers «Организация не найдена» (BR-M1-20)
  And the switcher and the remembered selection keep «Альфа», «Пример
    проекта» and «Пример проекта»

Scenario: A switch on a project-level page opens the same page in the new context
  Given the settings of «Пример проекта» in «Альфа»
  When the user chooses «Аналитика» at the project level
  Then the settings of «Аналитика» open
  When they choose «Бета» at the organization level
  Then the settings of the first project of «Бета», in alphabetical order,
    open, or «Главная» if «Бета» has no project (mod-015 BR-18)

Scenario: A page that addresses one team or area lands on its entry's page
  Given a page of this task whose address names the team «Платформа» or one
    area of «Пример проекта»
  When the user chooses «Аналитика» at the project level
  Then the landing page of that page's navigation entry opens for
    «Аналитика», not a team or area of «Аналитика» (mod-015 BR-18, R-22)

Scenario: On an organization-level page, the lower levels do not move the page
  Given the settings of the organization «Альфа»
  When the user chooses another project or another team in the switcher
  Then the page stays the settings of «Альфа», and the switcher shows the new
    selection (mod-015 R-22)
  When they choose «Бета» at the organization level
  Then the settings of «Бета» open

Scenario: The selection is remembered across a reload
  Given a signed-in user chose «Альфа», «Пример проекта» and «Платформа» on
    «Главная»
  When they reload the page
  Then the switcher shows «Альфа», «Пример проекта» and «Платформа»

Scenario: A remembered selection that is gone falls back
  Given the remembered selection is «Альфа», «Пример проекта», «Платформа»
  When «Платформа» has since been deleted and the user reloads
  Then the team level shows «Пример проекта», the project's first team
  When «Пример проекта» has since been deactivated instead
  Then the project level shows «Аналитика», the first active project of
    «Альфа» in alphabetical order, with its first team
  When «Альфа» has since been deactivated instead
  Then the organization level shows «Бета», the first active organization in
    alphabetical order, with its first project and that project's first team
  And the fallback is shown as mod-015's design.md shows a remembered
    selection that is no longer available

Scenario: The user's own changes reach the switcher without a reload
  Given a signed-in user with «Альфа» chosen
  When, in the same tab, they create the organization «Дельта», rename «Бета»
    to «Бета 2», or add the team «Ядро» to the chosen project
  Then the switcher's levels list the change without a page reload
  When they choose «Ядро» and then delete it, or deactivate «Альфа» itself
    in its settings
  Then the switcher falls back as in the scenario above, without a reload
  And the fallback, unlike a choice (mod-015 BR-18), moves no page by
    itself: after the deactivation they are on the organization list
    (BR-M1-24, US-2)

Scenario: The lists fail to load
  Given the organizations cannot be loaded
  When a signed-in user opens the switcher
  Then it shows mod-015's failed state with «Повторить»
  When the load succeeds after «Повторить»
  Then the levels show the active organizations, projects and teams
```
