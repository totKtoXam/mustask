# US-1: A user sees a team's backlog per level

- story: US-1
- depends-on: -
- shippable: no — the backlog is worked by reordering (US-2) and filled by creating (US-3)
- status: pending

As a **signed-in user**, I want **to see my team's work items of one level
in one ordered list**, so that **the team agrees what comes next without
sifting the whole project**.

## Acceptance criteria

Data for every story of this task: organization «Пример организации»;
project «Мастаск», key `MSK`, root area «Мастаск», first team «Мастаск»;
team «Платформа», whose default area «Платформа» (a child of the root) it
owns, with «Платформа / API» below it and no owner of its own; «Отчёты», a
child of the root with no owner of its own. `<team>` is
`/organizations/<o>/projects/<p>/teams/<t>`. Spec § Model, BR-M4-1, -5,
-13, -14, -23.

```gherkin
Scenario: A team's backlog holds its own items of the level
  Given MSK-EPIC-1, MSK-US-2, MSK-TASK-3 and MSK-FEAT-6 in «Платформа»,
    MSK-BUG-4 in «Платформа / API» and MSK-US-5 in «Мастаск»
  When the user opens the «Истории» backlog of «Платформа»
  Then its rows are MSK-US-2 and MSK-BUG-4, in that order
  And the «Эпики» backlog of «Платформа» holds only MSK-EPIC-1, its «Фичи»
    backlog only MSK-FEAT-6
  And MSK-US-5 is on the «Истории» backlog of «Мастаск», MSK-TASK-3 on none

Scenario: An area with no owner of its own counts for its nearest owner
  Given MSK-US-7 in «Отчёты»
  Then it is on the «Истории» backlog of «Мастаск», not of «Платформа»

Scenario: Closed and resolved items stay on the backlog
  Given MSK-US-2 in «Закрыта» and MSK-BUG-4 in «Разрешена»
  Then both are rows of the «Истории» backlog of «Платформа»

Scenario: Items nobody has ordered yet are listed by number
  Given the «Истории» backlog of «Платформа», never reordered, holds
    MSK-US-9, MSK-BUG-5 and MSK-US-3
  Then its rows are MSK-US-3, MSK-BUG-5, MSK-US-9, «Порядок» 1, 2, 3

Scenario: A row shows the item at a glance and opens it
  Given MSK-BUG-4 «Падение при входе» in «Активна», with no assignee
  Then its row shows its «Порядок», the handle «Переместить MSK-BUG-4»,
    «MSK-BUG-4», the «Баг» icon with «Баг», the title as a link,
    «Не назначен», and the category dot with «Активна»
  And activating the title opens `/organizations/<o>/projects/<p>/workitems/MSK-BUG-4`

Scenario: The level is chosen by tabs and kept in the address
  When the user activates «Бэклоги» in the main navigation, team «Платформа»
  Then `<team>/backlogs/stories` opens, `h1` «Бэклоги», tab «Истории» selected
  And the tabs «Эпики», «Фичи», «Истории» open `<team>/backlogs/epics`,
    `…/features`, `…/stories`; `<team>/backlogs/tasks` is «Страница не найдена»

Scenario: Switching the team keeps the entry, not the level
  Given the «Фичи» backlog of «Платформа»
  When the user chooses «Мастаск» at the switcher's team level
  Then the «Истории» backlog of «Мастаск» opens (mod-015 BR-18)

Scenario: An empty backlog shows the empty frame, header and tabs kept
  Given a team «Аналитика» added today, with no work item in its area
  When the user opens its «Эпики» backlog
  Then the frame «Бэклог пуст», hint «Добавьте рабочий элемент — он встанет
    в конец бэклога.», action «Новый рабочий элемент» replaces the table

Scenario: A failed read is retried in place, header and tabs kept
  Given the backlog read fails
  Then the server's text and «Повторить» replace the table
  When the user activates «Повторить» and the read succeeds
  Then the rows are shown

Scenario Outline: An address whose context is not found
  When the user opens a backlog address whose <part>
  Then the page shows the frame <frame>

  Examples:
    | part                                         | frame                    |
    | team id is malformed                         | «Команда не найдена»     |
    | team has been deleted                        | «Команда не найдена»     |
    | team belongs to another project              | «Команда не найдена»     |
    | project has been deactivated                 | «Проект не найден»       |
    | organization id names no organization        | «Организация не найдена» |
```
