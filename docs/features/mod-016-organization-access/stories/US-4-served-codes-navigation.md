# US-4: The panel learns its codes from Mustask.Api and shows only what the role allows

- story: US-4
- depends-on: US-2
- shippable: no — the panel side of US-2's checks; without them it would hide what the API still allows
- status: pending

As a **member of an organization**, I want **the panel to show me the pages
and actions my role allows there, and to follow a change of my role without a
reload**, so that **I never meet controls I cannot use, and a granted right is
usable at once (D-9, mod-015 BR-8, BR-9)**.

## Acceptance criteria

The served access, the navigation and the controls; mod-015 BR-8's interim
clause («any signed-in user satisfies an organization code») is gone.

```gherkin
Scenario: Mustask.Api serves the role and the codes of one organization
  Given a user whose role in A is member
  When the panel reads A's served access
  Then the answer holds the role member and exactly the member bundle of the
    spec's matrix
  And it holds no instance code — those stay in the token
  And for an organization the user is not a member of it answers 404
    «Организация не найдена»

Scenario Outline: The controls follow the role
  Given a user whose role in A is <role>, on A's settings and work pages
  Then <shown> are shown
  And <hidden> are not shown at all, rather than shown disabled (mod-015 BR-9)

  Examples:
    | role   | shown                                                                 | hidden                                                                                             |
    | admin  | every write action of the spec's § Screens and copy                   | nothing                                                                                            |
    | member | «Новый рабочий элемент» on the list and the backlog, «Новый элемент» on the board, the editable form with «Сохранить», «Родитель», «Исполнитель», backlog reorder and row actions, card moves | «Переименовать» and «Деактивировать», «Создать проект», the «Деактивированные» project tab, team and area actions, «Добавить участника» and «Действия: <имя>» on «Участники», «Добавить в команду» and «Исключить из команды» |
    | viewer | the work items list, the work-item form with every field's value, the backlogs and boards, «Участники», «Участники команды» | everything the member lacks, plus «Новый рабочий элемент», «Новый элемент», «Сохранить», any input that accepts a change on the form, the «Родитель» and «Исполнитель» pickers, backlog reorder and row actions, card moves |

Scenario Outline: An empty page names no control the user lacks
  Given a user whose role in A is <role>
  When they open <page> of A with nothing on it
  Then they see <heading> with <hint>
  And no action is offered

  Examples:
    | role             | page                         | heading                        | hint                                                                                   |
    | member or viewer | «Проекты»                    | «Пока нет проектов»            | «Проекты создаёт администратор организации.»                                           |
    | member or viewer | a project's «Области»        | «Вложенных областей пока нет»  | «Области задаёт администратор организации.»                                            |
    | viewer           | a project's work items       | «Рабочих элементов пока нет»   | «Рабочие элементы создают участники организации с ролью «Участник» или «Администратор».» |
    | viewer           | a team's backlog             | «Бэклог пуст»                  | «Рабочие элементы создают участники организации с ролью «Участник» или «Администратор».» |
    | viewer           | a team's board               | «Канбан пуст»                  | «Новые рабочие элементы этого уровня появятся в колонке «Новые».»                      |

Scenario: Every role sees «Участники» in the settings navigation
  Given a user with any role in A, with A chosen in the switcher
  When they open «Настройки»
  Then «Организация» lists «Общие», «Проекты», «Участники», «Процесс» in this
    order

Scenario: A page opened by address without its code
  Given a user whose role in A is member
  When they open A's deactivated projects page by its address
  Then they see «Доступ ограничен» with «У вас нет прав для просмотра этого
    раздела. Обратитесь к администратору, чтобы запросить доступ.»

Scenario: While the codes are read, a page waits rather than denies
  Given a user whose role in A is admin, opening A's deactivated projects
    page by its address
  When the page renders before A's served access has answered
  Then it shows the loading skeleton, not «Доступ ограничен», and no
    organization-scoped write control
  And once the access answers, the page shows its content (BR-M1-31)

Scenario: A failed access read offers a retry
  Given a member of A opening A's work items
  When the read of A's served access fails
  Then the page shows the error with «Повторить» (mod-015 BR-12), not
    «Доступ ограничен»
  And after «Повторить» succeeds, the work items and the member's controls
    show
  And when a later re-read fails instead, the controls of the last codes
    read stay

Scenario: A granted role shows up on refocus, without a reload
  Given a signed-in user with the role viewer in A, on A's work items
  When an administrator changes their role to member
  And the user returns to the panel's window
  Then «Новый рабочий элемент» appears on the work items with no page
    reload (D-9)
  And creating a work item with it succeeds

Scenario: An organization switch re-reads the codes
  Given a user who is admin in A and viewer in B, on A's «Проекты»
  When they choose B in the switcher
  Then B's «Проекты» shows no «Создать проект», with no page reload
  And at no moment does a page of B show a control that only A's codes
    allow

Scenario: Stepping down hides the administrator's controls at once
  Given administrators A and B of «Пример организации», A on «Участники»
  When A changes their own role to «Участник» and presses «Сохранить»
  Then, with no reload or refocus, «Добавить участника» and every
    «Действия: <имя>» menu disappear
  And A's row shows «Участник» (BR-M1-31)

Scenario: A 403 re-reads the codes and hides what is gone
  Given a member of A whose role an administrator has just changed to viewer
  When they press «Новый рабочий элемент» and submit before any refocus
  Then the dialog shows «Недостаточно прав» where it shows its refusals,
    keeping what was typed (mod-002 design § 4.4)
  And the panel re-reads A's codes and «Новый рабочий элемент» disappears
    from the page

Scenario: A removed member's page turns into «not found»
  Given a member of A on A's backlog
  When an administrator removes them and their next action reaches the API
  Then the page shows «Организация не найдена»
  And the panel re-reads the organization list, so A is no longer in the
    switcher and the switcher falls back as mod-002 BR-M1-24 says
  And the 404 of A's served access itself triggers no further read
    (BR-M1-31)

Scenario: Instance codes still come from the token
  Given a user holding the create right and no membership
  When they open the organization list
  Then «Создать организацию» is shown, as before this task
  And «Языки» is shown under «Справочники»
```
