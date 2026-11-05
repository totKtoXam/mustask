# US-5: The grants are sections of the objects' own pages

- story: US-5
- depends-on: US-1, US-2, US-4
- shippable: no — one side of the grant capability, with US-1 and US-3
- status: pending

As a **manager of a department, a project or a team**, I want **the object's grants on its own page, beside what already lives there**, so that **I see who can do what on the object, where it comes from, and change it where I am (BR-SA-1..-6; doc-012 US-5, US-10)**.

## Acceptance criteria

Fixture of US-1. Sections: «Участники» on a department; «Участники и доступ» on a project and on a team (on a team beside its members). A project node and a sub-area have no grant section (OQ-1 A). A section is absent for a user the object's read code does not reach.

```gherkin
Scenario: A department's «Участники» shows direct and inherited grants
  Given Olga holds «Чтение» on «Логистика», and Irina holds «Изменение» on «Логистика»
  When a member opens «Участники» of «Логистика»
  Then both are listed as direct grants with role, giver, date and time

Scenario: A project's «Участники и доступ» shows where each grant comes from
  Given Olga holds «Чтение» on «Логистика» and Irina holds «Изменение» on A
  When a member opens A's «Участники и доступ»
  Then Irina is listed as a grant on the project
  And Olga is listed as inherited from «Логистика», with no change or removal control on that line
  When the switch of A's node is off
  Then Olga is no longer listed on A

Scenario: A team's grants are apart from its members
  Given Olga is a member of VENUS with no grant, and Pavel holds «Управление» on VENUS
  When a member opens VENUS's «Участники и доступ»
  Then the members and the grants are two lists
  And Olga is a member and is listed as holding nothing; Pavel is listed as «Управление»

Scenario: A manager gives, changes and removes a grant from the page
  Given Pavel holds «Управление» on A
  When he picks Irina in the searchable member picker, gives «Изменение», changes it to «Чтение» and removes it
  Then each step shows without a page reload
  And the picker offers only active members who hold no grant on A, and typing narrows it

Scenario: Without the grant code the controls are absent
  Given Olga holds «Изменение» on A
  When she opens A's «Участники и доступ»
  Then she reads the list and the page shows no give, change or remove control

Scenario: A refused write is shown in the registry's words
  Given Pavel's grant on A was removed while his page was open
  When he gives a role
  Then he sees «Недостаточно прав», and the page re-reads A's codes and drops the controls

Scenario: An empty list says so
  Given nobody holds a grant on VENUS
  When a manager opens its «Участники и доступ»
  Then the grants list says that no grants are given, and offers to give one

Scenario: The page works at a long list
  Given 200 grants on «Логистика»
  When a member opens its «Участники»
  Then the list is shown within 2 s, narrowable by typed text

Scenario: The switch control sits on the node's page
  When a user who may switch it opens A's «Структура»
  Then «Наследовать доступ» is shown with its state and its hint (US-2), and a user who may not sees the state read-only
```
