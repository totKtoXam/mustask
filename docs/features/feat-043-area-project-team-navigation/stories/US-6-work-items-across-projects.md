# US-6: Work items across projects, and in the project's section

- story: US-6
- depends-on: US-1, US-4
- shippable: no — the entry «Рабочие элементы» and the project's section are one list; it carries feat-003's filters because feat-003 merged first (doc-013 BR-NE-8)
- status: pending

As an **organization member**, I want **one list of the work items of every project I may see, and the same list fixed to a project, with the filters of the project's list**, so that **I find an item without knowing its project first, and see which team owns it**.

## Acceptance criteria

Cast as US-2. Items of A: MSK-1 owned by MARS, MSK-2 owned by VENUS, MSK-3 with no owning team, MSK-4 deleted (owned by MARS, assigned to Ivan, whose membership ended); B has BET-1 (MARS); C has FIN-1 (MARS). Olga «Участник», Ann «Администратор». Every project is visible to every member until `feat-041-project-visibility`, which applies the set form to this read and re-runs the visibility clauses of doc-012 US-10 (BR-NV-15).

```gherkin
Scenario: The organization's list holds every project's items once (BR-MX-9, BR-MX-16; BR-NV-12)
  When Olga opens «Рабочие элементы»
  Then she sees MSK-1, MSK-2, MSK-3, BET-1 and FIN-1, newest change first and then by identifier, at most 50 to a page, each once
  And each row shows its project and its owning team, «Без команды» for MSK-3, and its title opens the card in the item's own project
  And MSK-4, deleted, is in no row and in no count (mod-005 BR-DR-6)

Scenario: The project's section is the same list fixed to the project (BR-NV-12)
  When Olga opens A's «Рабочие элементы»
  Then she sees MSK-1, MSK-2 and MSK-3 only, in the same order and with the same columns, and its «Команда» filter offers exactly MARS, VENUS and «Без команды»
  And the organization's list offers every team of the organization in «Команда», because it spans projects

Scenario: Filtering by team (BR-MX-9; mod-017 BR-OT-8; mod-020)
  When Olga chooses MARS in «Команда»
  Then the list shows only the items MARS owns, MSK-1, BET-1 and FIN-1, and the address carries the choice, so a link opens the narrowed list
  And choosing «Без команды» shows only MSK-3, and clearing the filter shows every item again, with the page back at 1

Scenario: Feat-003's filters are on both lists (BR-NE-8; BR-WS-1..-7)
  When Olga types «оплат» in «Поиск», chooses «Исполнитель» and «Статус» on the organization's list and then on A's section
  Then both lists offer the same fields, narrow by the same rules, keep the filters in the address, and show «Ничего не найдено» with «Сбросить фильтр» when nothing matches
  And the options of «Исполнитель» on the organization's list are the people assigned to the items of the listed projects, former members marked «(не в организации)», then «Не назначен»

Scenario: Deleted items are listed only when «Удалена» is chosen (BR-NE-8; BR-WS-4)
  When Olga chooses «Удалена» in «Статус» on the organization's list
  Then MSK-4 is listed, reading «Удалена», its title opens the read-only card of mod-005, and counts follow
  And without that choice MSK-4 is in no row, no count, no option of «Исполнитель» and no keyword result, on either list

Scenario: The list is read within the budget (NFR)
  Given an organization of 200 projects with 5 000 items in each
  When Olga opens «Рабочие элементы» and then page 2
  Then each page is read within 2 s, with a number of statements that does not grow with the number of projects

Scenario: A project the caller cannot reach is not listed (BR-MX-1)
  When Olga opens «Рабочие элементы» in «Ромашка»
  Then no item of «Другая» is in it, and an address that names an item of «Другая» in «Ромашка» answers «Рабочий элемент не найден»

Scenario: An empty organization (BR-MX-2)
  Given «Север» has no work item
  When a member opens «Рабочие элементы»
  Then she sees «Рабочих элементов пока нет», with no filter applied, and a link to «Проекты»; there is no create command on this list

Scenario: The entry keeps the filter when it opens its own list (mod-020 review CR-2)
  Given Olga is on the organization's list narrowed to MARS
  When she chooses «Рабочие элементы» in the navigation
  Then the list stays on the same page with the filter in the address, as the project's tab and «Весь проект» did after mod-020
  And a link to the list without `teamId` opens it unfiltered
```
