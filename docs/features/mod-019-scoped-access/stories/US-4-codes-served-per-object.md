# US-4: The panel is served the caller's codes per object and shows sections and controls by them

- story: US-4
- depends-on: US-3
- shippable: no — the sections it shows are US-5's
- status: pending

As an **organization member**, I want **each page to show only the sections and controls my codes on that object allow**, so that **I am not offered what the server will refuse, and a grant shows in the next page I open (BR-SA-11, BR-SA-12, BR-SA-18)**.

## Acceptance criteria

Fixture and users of US-1 (everyone a «Читатель» unless said). Notice and section wording not quoted here is the design's. «Настройки» is feat-043's derived last entry (BR-NV-18): it is always present; its groups are what the codes decide.

```gherkin
Scenario: A read of an object carries the caller's codes on it
  Given Pavel holds «Изменение» on «Логистика»
  When the panel reads «Логистика», A and MARS
  Then the answer for A carries the project codes his grant reaches (read and change items), «Логистика» carries the department codes, MARS carries the team codes of the organization role alone
  And the organization answer carries the role and the organization-scope codes only

Scenario: A view over several projects carries each project's codes once
  When the panel reads a list across projects
  Then the codes are given once per project, not per row, and no matrix of every object is served

Scenario: The tree read carries where the caller may write, once (R-9)
  Given Pavel holds «Управление» on «Логистика»
  When the panel reads «Структура» of an organization of 1 000 nodes
  Then the answer carries once the ids of the nodes where he holds `organizations.area.write`, from the set form, with the read's pinned statement count and the 2 s budget of feat-043 unchanged
  And the row commands (create a sub-area, rename, move, switch, delete) and the move-destination pickers offer only those ids
  And a node's own page carries its codes as any object read

Scenario: A group of «Настройки» and a section are shown by the object's codes
  Given Olga holds no grant, and Kirill holds «Управление» on A
  When each opens A
  Then Kirill sees «Участники и доступ» with its controls and the «Проект» group («Параметры») in «Настройки»; Olga sees «Участники и доступ» as a read-only list and no «Проект» group
  And Olga opening A's `…/settings` by its address sees «Доступ ограничен»
  And Vera's «Настройки» shows only the «Организация» group, and no entry is shown disabled

Scenario: Controls follow the grant at the next load
  Given Olga holds «Чтение» on A and opens A's «Канбан»
  When she is given «Изменение» and reloads
  Then the card-move and create controls are offered, and the direct change is kept
  When the grant is removed and she reloads
  Then the controls are gone, and the direct change answers 403

Scenario: Re-reads happen on the triggers (ADR-0029 § 10)
  When the caller writes a grant on A (her own included), or a call under A answers 403 or 404
  Then the panel re-reads A's codes
  And a change made by someone else shows at the next trigger: refocus, a 403 or 404, or a page load
  And a «Проект не найден» on a project page also re-reads the organization's project list

Scenario: A role change shows one notice per object
  Given Olga is on A's page
  When a re-read shows her codes on A changed
  Then one notice reports it for A without naming a role
  And the organization notice keeps its wording when the organization role changes

Scenario: Every panel consumer reads a non-organization code from the object (R-8, BR-SA-18)
  Given Mira is a «Участник» holding no grant
  When she opens, in turn, the kanban and backlog (project, team, department grouped views), a work item's card, its checklist, its parent links, the tree editor of work items, the item form and «Структура»
  Then each offers the controls it offers today, as one component test per surface
  And a unit test fails when any panel code reads a code whose scope is not the organization from the organization answer

Scenario: The grouped views follow each project's codes (feat-048 § Roles)
  Given Olga holds «Изменение» on A and no other grant
  When she opens the department's grouped «Бэклог» and board
  Then she moves rows and cards in A's group and lane only, and a direct move in B's answers 403

Scenario: The create commands are offered by their codes (m-3 and TC-30 kept under the split bundles)
  Given Pavel holds «Управление» on «Логистика», Kirill holds only the project role «Управление» on A, Anna is an admin
  When each opens «Структура» and «Проекты»
  Then Anna is offered «Новый департамент» and «Новый проект» with the top level and every department as places; Pavel «Новый проект» with «Логистика» as its only place and no «Новый департамент»; Kirill neither
  And «Новый проект» is absent, not disabled (BR-NV-1), when the caller's place list would be empty, so its «Расположение» is never empty
  And a place deleted meanwhile answers a non-admin 403 on submit (the admin «Область не найдена.»), and the dialog re-reads the places

Scenario: The server decides
  Given Olga's served codes on A lack the change code
  When she sends a change of an item of A past the panel
  Then it answers 403 and changes nothing
```
