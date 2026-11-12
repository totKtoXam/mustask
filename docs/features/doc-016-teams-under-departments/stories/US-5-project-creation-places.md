# US-5: A project stands in a department and links only its department's teams

- story: US-5
- depends-on: US-1
- shippable: yes
- status: pending

As an **organization administrator or a department «Менеджер»**, I want **every project to be created and kept inside a department, the department filled in from my selection, and linked only to that department's teams**, so that **the structure stays a strict tree and a new project lands where I am working**.

Decided by the human, 2026-11-12: OQ-8 (every project stands in a department; replaces the request's "a project can be created at the organization level"), OQ-2 B. Who creates is ADR-0038 § 3's place rule, unchanged: `organizations.area.write` on the department. Delivered by `mod-NNN-projects-in-departments`; the selector's «Создать проект» entry point by `feat-052-header-selectors-create-and-all-first` (reworded). The move refusal follows OQ-9 A, the open recommendation.

## Acceptance criteria

```gherkin
Scenario: Created in the chosen department (BR-TD-6)
  Given Ann's selection is «Ромашка» / «Финансы» / «все» / «все»
  When she opens «Создать проект» in the project selector, or «Новый проект» on «Финансы»'s «Проекты»
  Then «Расположение» is set to «Финансы» and may be changed to any department she may create in
  And after saving «Epsilon» with the key EPS, Epsilon stands in «Финансы» and the project selector reads Epsilon

Scenario: No department chosen: the department is required
  Given Ann's selection is «Ромашка» / «все» / «все» / «все»
  When she opens «Создать проект»
  Then «Расположение» is empty and offers «Логистика» and «Финансы», never «Верхний уровень», a project, a sub-area or a team
  And saving with it empty shows «Выберите департамент» and creates nothing
  And a request naming no department is refused with «Выберите департамент» and creates nothing

Scenario: A department manager's place
  Given Max holds department «Менеджер» on «Логистика» only and his selection is «Ромашка» / «все» / «все» / «все»
  When he opens «Создать проект»
  Then «Расположение» offers «Логистика» alone and is set to it
  And a request to create a project in «Финансы» is refused with «Недостаточно прав» and creates nothing

Scenario: No place to create in, no command
  Given Vera holds no grant and is organization «Читатель»
  Then no selector, list or page offers «Создать проект» to her

Scenario: Never in a sub-area or a project node
  Then a request naming a project node or a sub-area as the place is refused as ADR-0038 § 3 says, and creates nothing

Scenario: A team chosen does not link the new project
  Given Ann's selection is «Ромашка» / «Логистика» / MARS / «все»
  When she creates «Omega» through «Создать проект»
  Then Omega stands in «Логистика» and is linked to no team until someone links it

Scenario: Links join one department's team and project (BR-TD-3)
  When Ann opens «Добавить команду» on C's page
  Then the picker offers JUPITER alone, the team of «Финансы»
  And a request to link MARS to C is refused with «Команду можно связать только с проектом своего департамента.» and nothing changes
  And a request to link JUPITER to C again succeeds and changes nothing

Scenario: A project is never moved to the top level
  When Ann opens «Переместить» on A
  Then the destination offers «Финансы», not «Верхний уровень»
  And a request moving A's node to the top level is refused with «Проект должен находиться в департаменте.» and nothing changes

Scenario: A linked project is not moved (BR-TD-4, OQ-9 A)
  When Ann moves A to «Финансы»
  Then she is refused with «Проект связан с командами. Отключите их перед перемещением.», since MARS and VENUS are linked to A, and nothing changes
  And once both are unlinked (feat-028's dialog, choosing where their items go), the move succeeds

Scenario: The new project is seen by its creator
  Given Max created «Omega» in «Логистика»
  Then «Omega» is in his project selector and «Проекты», reached by his department grant (US-6)
```
