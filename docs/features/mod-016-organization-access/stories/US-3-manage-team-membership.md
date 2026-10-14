# US-3: An organization administrator manages a team's members

- story: US-3
- depends-on: US-1
- shippable: no — team membership only matters through the default team (US-7) and later capacity
- status: pending

As an **organization administrator**, I want **to say who is in each team on
the team's page**, so that **each person's views open on their own team
(D-5); team membership carries no access of its own (doc-006 § Roles)**.

## Acceptance criteria

doc-006 US-6, scenarios 5 (an organization member added to a team keeps their
role), 7 (a removed team member still sees the project and keeps their
assignments) and 12 (a reader sees every team and its members and changes
nothing) — unchanged; doc-006 US-3 scenario 12 (removing an organization
membership removes the user from every team) — unchanged. Changed and new:

```gherkin
Scenario: An administrator adds an organization member to a team
  Given the team «Платформа» of the project «Пример проекта» and
    «Анна Смирнова», an active member of its organization
  When the administrator opens the team's page, presses «Добавить в
    команду», types «Смирн» in «Участник организации», picks her and presses
    «Добавить»
  Then «Участники команды» lists «Анна Смирнова» with her «Почта»
  And her organization role is unchanged

Scenario: Only active organization members are offered and accepted
  Given a user of another organization, a projected user in no organization,
    and a member of this organization whose Keycloak account is disabled
  When the administrator searches for each in «Участник организации»
  Then none is offered, and a search with no candidate shows «Пользователи
    не найдены»
  And adding any of them directly is refused with «Добавить в команду можно
    только активного участника организации» (doc-006 BR-M1-10, BR-M1-17)

Scenario: A user is in a team at most once
  Given «Анна Смирнова» already in «Платформа»
  When she is added to «Платформа» again
  Then it is refused with «Пользователь уже состоит в команде»
  And she is listed once
  And when two administrators add a user to «Платформа» at the same moment,
    one add succeeds, the other is refused with «Пользователь уже состоит в
    команде», and the user is listed once (NFR Concurrency)

Scenario: An administrator removes a team member
  Given «Анна Смирнова» in «Платформа»
  When the administrator chooses «Исключить из команды» in «Действия: Анна
    Смирнова»
  Then she is no longer in «Участники команды»
  And she still reads the project and every team's backlogs and boards

Scenario: Removing a team member who is already gone
  Given a team membership another administrator has just removed
  When the administrator chooses «Исключить из команды» on that row
  Then the action is refused with «Участник команды не найден.» and the list
    is read again

Scenario: A team with no members says so
  Given a team with no members
  When an administrator opens its page
  Then «Участники команды» shows «В команде пока нет участников» with «Добавьте
    участников организации кнопкой «Добавить в команду».»
  And a member or reader sees «В команде пока нет участников» with «Состав
    команды задаёт администратор организации.» and no add action

Scenario: An inactive or unknown team member stays and is marked
  Given a team member whose Keycloak account has been disabled, and a team
    member the projection does not know
  When anyone opens the team's page
  Then the first is listed with «неактивен» after the name
  And the second as «Неизвестный пользователь» with an empty «Почта», as on
    «Участники»
  And both stay listed until an administrator removes them

Scenario: The project's creator is its first team's first member
  Given an organization administrator
  When they create the project «Пример проекта»
  Then its first team «Пример проекта» lists them as its only member
    (doc-006 BR-M1-9, BR-M1-25)

Scenario: A creator the projection does not know yet still gets the creation memberships
  Given a signed-in user holding the create right whom the projection does
    not know yet (mod-014 BR-UP-17)
  When they create the organization «Пример организации» and then the
    project «Пример проекта» in it
  Then both creations succeed
  And they hold the admin membership of «Пример организации» and are the
    only member of the first team «Пример проекта» (BR-M1-25)
  And «Участники» and «Участники команды» show them as «Неизвестный
    пользователь» until the projection receives them

Scenario: A deleted team's memberships end
  Given the team «Платформа» with two members, one of whom is also in
    «Аналитика»
  When an administrator deletes «Платформа» (mod-002 US-7)
  Then neither is a member of «Платформа» any longer
  And the one in «Аналитика» still is
  And the team's page and its member list answer «Команда не найдена»
    (BR-M1-36)

Scenario: A team member added while the organization membership is removed
  Given «Анна Смирнова», a member of the organization
  When one administrator adds her to «Платформа» while another removes her
    from the organization at the same moment
  Then afterwards she is in no team of the organization: either the add is
    refused with «Добавить в команду можно только активного участника
    организации» or the removal ends the team membership it made (NFR
    Concurrency)

Scenario: A member added to a team that is being deleted
  Given the team «Платформа» being deleted
  When an administrator adds a member to it at the same moment
  Then the add is refused with «Команда не найдена» or the membership ends
    with the team — no membership of a deleted team remains
```
