# US-5: A project is created at the organization level or in a department

- story: US-5
- depends-on: -
- shippable: yes
- status: pending

As an **organization administrator or a department «Менеджер»**, I want **to create a project at the organization's top level or inside a department, from wherever I am, with the place already filled in from my selection**, so that **a new project lands where I am working**.

The backend already decides creation by the place (ADR-0038 § 3); this story fixes the entry points and the pre-set place. Delivered by `feat-052-header-selectors-create-and-all-first` (reworded). OQ-7 A assumed: under OQ-7 B an organization «Участник» also gets «Верхний уровень».

## Acceptance criteria

```gherkin
Scenario: Created at the top level (BR-TD-6)
  Given Ann's selection is «Ромашка» / «все» / «все» / «все»
  When she opens «Создать проект» in the project selector
  Then «Новый проект» opens with «Расположение» set to «Верхний уровень»
  And after saving «Epsilon» with the key EPS, Epsilon stands at the top level and the project selector reads Epsilon

Scenario: Created in the chosen department
  Given Ann's selection is «Ромашка» / «Финансы» / «все» / «все»
  When she opens «Создать проект» in the project selector, or «Новый проект» on «Финансы»'s «Проекты»
  Then «Расположение» is set to «Финансы» and may be changed to any place she may create in

Scenario: A team chosen sets the team's department
  Given Ann's selection is «Ромашка» / «Логистика» / MARS / «все»
  When she opens «Создать проект»
  Then «Расположение» is set to «Логистика», and the new project is not linked to MARS until someone links it

Scenario: A department manager's places
  Given Max holds department «Менеджер» on «Логистика» only and his selection is «Ромашка» / «все» / «все» / «все»
  When he opens «Создать проект»
  Then «Расположение» offers «Логистика» alone and is set to it, never «Верхний уровень» (OQ-7 A)
  And a request to create a project at the top level is refused with «Недостаточно прав» and creates nothing

Scenario: No place to create in, no command
  Given Vera holds no grant and is organization «Читатель»
  Then no selector, list or page offers «Создать проект» to her

Scenario: Never in a sub-area, a project node or a team
  Then «Расположение» offers only «Верхний уровень» and departments, never a project, a sub-area or a team
  And a request naming a project node or a sub-area as the place is refused as mod-018 api-contract § 4 says, and creates nothing

Scenario: The new project is seen by its creator
  Given Max created «Omega» in «Логистика»
  Then «Omega» is in his project selector and «Проекты», reached by his department grant (US-6)
```
