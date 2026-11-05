# US-4: The panel is served the caller's codes per object and shows sections and controls by them

- story: US-4
- depends-on: US-3
- shippable: no — the sections it shows are US-5's
- status: pending

As an **organization member**, I want **each page to show only the sections and controls my codes on that object allow**, so that **I am not offered what the server will refuse, and a grant shows in the next page I open (BR-SA-11, BR-SA-12)**.

## Acceptance criteria

Fixture of US-1. Notice and section wording not quoted here is the design's.

```gherkin
Scenario: A read of an object carries the caller's codes on it
  Given Pavel holds «Изменение» on «Логистика» and is a «Читатель» of the organization
  When the panel reads «Логистика», A and MARS
  Then the answer for A carries the project codes his grant reaches (read and change items), «Логистика» carries the department codes, MARS carries the team codes of the organization role alone
  And the organization answer carries the role and the organization-scope codes only

Scenario: A view over several projects carries each project's codes once
  When the panel reads a list across projects
  Then the codes are given once per project, not per row, and no matrix of every object is served

Scenario: A section is shown by the object's codes
  Given Olga holds no grant, and Kirill holds «Управление» on A
  When each opens A
  Then Kirill sees «Настройки» and «Участники и доступ» with their controls; Olga sees «Участники и доступ» as a read-only list and no «Настройки»
  And Olga opening A's «Настройки» by its address sees «Доступ ограничен»

Scenario: Controls follow the grant at the next load
  Given Olga holds «Чтение» on A and opens A's «Канбан»
  When she is given «Изменение» and reloads
  Then the card-move and create controls are offered, and the direct change is kept
  When the grant is removed and she reloads
  Then the controls are gone, and the direct change answers 403

Scenario: Re-reads happen on the triggers (ADR-0029 § 10)
  When Olga's own grant changes, or a call under A answers 403 or 404
  Then the panel re-reads A's codes
  And a «Проект не найден» on a project page also re-reads the organization's project list

Scenario: A role change shows one notice per object
  Given Olga is on A's page
  When a re-read shows her codes on A changed
  Then one notice reports it for A without naming a role
  And the organization notice keeps its wording when the organization role changes

Scenario: Pickers offer what the caller may do (takes over m-3 and TC-30)
  Given Pavel holds «Управление» on «Логистика», Kirill holds only «Управление» on A
  When each opens «Структура»
  Then Pavel is offered the create dialog for a child of «Логистика», Kirill is offered none
  And a top-level «Новая область» is offered by the create code alone
  And «Новый проект» is disabled with a reason for anyone without a place their codes allow, and its form does not offer an empty «Расположение»

Scenario: The menu is built from the object's codes
  Given Vera is a «Читатель»
  When she opens a department, a project and a team
  Then she sees «Участники» of the department and «Участники и доступ» of the project and the team, read-only, and no «Настройки»
  And no entry is shown disabled

Scenario: The server decides
  Given Olga's served codes on A lack the change code
  When she sends a change of an item of A past the panel
  Then it answers 403 and changes nothing
```
