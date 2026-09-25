# US-1: MS-1 «Организация, проект, команды, процесс засеян»

- story: US-1
- depends-on: -
- shippable: no — a milestone, delivered by the lines listed
- status: pending

As the **product owner**, I want **MS-1 «Организация, проект, команды,
процесс засеян»**, so that **every later milestone builds on a real tenancy
skeleton. That skeleton is an organization with its process, members taken
from the company directory, a keyed project, several teams and an owned area
tree.**

Delivered by `mod-NNN-keycloak-user-projection`, `mod-001-process-seed` and
`mod-002-organizations-projects-teams` (spec § Releases and milestones).

## Acceptance criteria

```gherkin
Scenario: Demo — from an empty instance to a project with its teams
  Given a user granted the right to create organizations by a Keycloak realm
    administrator (D-10)
  And «Анна Смирнова», a Keycloak user who has never signed in to MUSTASK
  When the user creates the organization «Пример организации»
  Then they are its administrator
  And its process screen lists «Эпик», «Фича», «Пользовательская история»,
    «Задача» and «Баг» with the keys EPIC, FEAT, US, TASK and BUG
  And «Пользовательская история» has «Новая», «Активна», «Разрешена» and
    «Закрыта», with «Разрешена» in the category «В работе» (D-4, D-5)
  And «Баг» has the same states, with «Разрешена» in the category «Решённые»
  And «Задача» has «Новая», «Активна» and «Закрыта»
  When the administrator finds «Анна Смирнова» by name and adds her as a
    member (D-9: the search reads the user projection)
  And creates the project «Пример проекта» with the key MSK
  Then the project has its first team, with the administrator as its only
    member, and that team owns the root area
  When the administrator adds the team «Платформа» and adds «Анна Смирнова»
    to it
  Then the team comes with the area «Платформа» under the root, owned by it
    and set as its default area (D-7)
  And both teams are listed, the first with the root area as its default
  When the administrator adds the team «Черновик», which comes with the area
    «Черновик», and then deletes the team (D-6)
  Then «Черновик» is no longer listed
  And the area «Черновик» is now owned by the project's first team
  When the administrator tries to delete the project's first team
  Then the deletion is refused, because that team owns the root area

Scenario: Guard — another organization stays invisible
  Given «Анна Смирнова», a member of «Пример организации» only
  And an organization in which she holds no membership
  When she opens that organization's address, or the address of one of its
    projects
  Then the answer is «Организация не найдена»
  And it cannot be told apart from the answer for an organization that never
    existed
```
