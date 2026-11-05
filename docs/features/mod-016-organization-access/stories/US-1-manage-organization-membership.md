# US-1: An organization administrator manages membership on «Участники»

- story: US-1
- depends-on: -
- shippable: no — membership is one side of the scoped check (US-2); alone it would decide nothing
- status: pending

As an **organization administrator**, I want **to add existing users to my
organization with a role, change that role and remove them on the
organization's «Участники» page**, so that **the right people work in the
organization and only they (doc-001 D-9, D-10)**.

## Acceptance criteria

doc-006 US-3, scenarios 2 (no outsider's memberships in the search), 3
(«Пользователи не найдены»), 5 (the search reads the projection with Keycloak
down), 6 («Пользователь уже состоит в организации»), 7 (who added, who changed
the role), 8 (a role change applies on the next request), 9 (the last active
administrator kept), 10 (stepping down while another admin remains), 11 (two
admins removing each other keep one), 14 (a re-enabled account loses
«неактивен»), 15 (every member reads the list, only an admin changes it) and
16 (a removed member's access ends immediately) — unchanged. Changed and new:

```gherkin
Scenario: An administrator adds a member from «Участники»
  Given an organization «Пример организации» the user administers
  And a user «Анна Смирнова», active in the user projection and not a member
  When the administrator opens «Участники», presses «Добавить участника»,
    types «Смирн» in «Пользователь», picks «Анна Смирнова» and presses
    «Добавить» with «Роль» left at «Участник»
  Then «Анна Смирнова» is listed with «Роль» «Участник» and «Добавлен» showing
    the administrator's name and today's date, and «Роль изменена» «—»
  And on her next request, with no re-login, «Пример организации» is in her
    organization list and its projects open for her (BR-M1-6)

Scenario: The search needs two characters
  Given the «Добавление участника» dialog
  When the administrator has typed one character in «Пользователь»
  Then the list shows «Введите не менее 2 символов имени или почты»
  And no search request is sent

Scenario: More than twenty matches are cut and said so
  Given more than 20 active projected users whose name contains «ов»
  When the administrator searches for «ов»
  Then 20 candidates are shown with «Показаны первые 20. Уточните запрос.»

Scenario: An inactive or unknown user is neither offered nor added
  Given a user whose Keycloak account is disabled, and a user id the
    projection does not know (mod-014 BR-UP-17)
  When the administrator searches for the first by name
  Then they are not offered
  And adding either of them directly through the API is refused with
    «Этого пользователя нельзя добавить в организацию» (BR-M1-32)
  And no membership is created

Scenario: An unknown administrator does not count as the one who remains
  Given an organization whose admins are A, active, and B, whose id the
    projection does not know
  When A changes their own role to «Участник» or removes themselves
  Then the change is refused with «В организации должен остаться хотя бы
    один администратор» (BR-M1-4, BR-M1-32)

Scenario: An administrator changes a member's role from the row menu
  Given «Анна Смирнова» with the role «Участник»
  When the administrator opens «Действия: Анна Смирнова», chooses «Изменить
    роль», picks «Читатель» and presses «Сохранить»
  Then the row shows «Читатель» and «Роль изменена» with the administrator's
    name and today's date
  And her write actions in the organization are refused from her next
    request with «Недостаточно прав»

Scenario: A role outside the three is refused
  When a request sets a membership's role to anything but admin, member or
    viewer
  Then it is refused with «Проверьте правильность заполнения полей.» and the
    role is unchanged

Scenario: An administrator removes a member
  Given «Анна Смирнова», a member of the team «Платформа» and the assignee of
    the work item «PRM-US-1»
  When the administrator chooses «Удалить из организации» on her row and
    confirms «Удалить участника?», whose text reads «Пользователь Анна
    Смирнова потеряет доступ к организации «Пример организации» и будет
    исключён из всех её команд. Назначенные ему рабочие элементы сохранят
    исполнителя.»
  Then she is no longer listed, nor in «Платформа»'s members (BR-M1-36)
  And «PRM-US-1» keeps her as its assignee (BR-M1-26)
  And her next request to the organization answers «Организация не найдена»

Scenario: Cancelling the removal changes nothing
  Given the confirmation «Удалить участника?» is open
  When the administrator presses «Отмена»
  Then the membership is unchanged

Scenario: An administrator removes themselves while another admin remains
  Given an organization with two active administrators, A and B
  When A chooses «Удалить из организации» on their own row
  Then the confirmation reads «Удалить себя из организации?» with «Вы
    потеряете доступ к организации «Пример организации».»
  And after «Удалить» A lands on the organization list, where the
    organization is no longer listed

Scenario: Removing a membership that is already gone
  Given a membership another administrator has just removed
  When the administrator changes its role or removes it
  Then the action is refused with «Участник не найден.» and the list is read
    again

Scenario: An inactive or unknown member is shown as such
  Given a member whose Keycloak account is disabled, and a member the
    projection does not know
  When anyone in the organization opens «Участники»
  Then the first is listed with «неактивен» after the name
  And the second as «Неизвестный пользователь» with an empty «Почта», after
    every member with a name
  And a «Добавлен» or «Роль изменена» that names either shows that name the
    same way
  And both keep their role and team memberships until an administrator
    removes them (BR-M1-17)
  And a work item assigned to either shows the assignee with no mark — the
    marks are feat-017's (D-4)

Scenario: Past 500 ids the list and the member searches still answer
  Given an organization with 501 members, some of whose «Добавлен» name
    users who are no longer members
  When anyone opens «Участники»
  Then all 501 members are listed, each «Добавлен» showing a name
  And a search in «Участник организации» on a team page, or in
    «Исполнитель» on a work item, finds a member among all 501, at most 20
    in name order (mod-014 BR-UP-15, BR-UP-17)
```
