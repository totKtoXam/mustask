# US-6: Work items across projects, and in the project's section

- story: US-6
- depends-on: US-1, US-4
- shippable: no — the entry «Рабочие элементы» and the project's section are one list; it carries feat-003's filters because feat-003 merged first (doc-013 BR-NE-8)
- status: pending

As an **organization member**, I want **one list of the work items of every project I may see, and the same list fixed to a project, with the filters of the project's list**, so that **I find an item without knowing its project first, and see which team owns it**.

## Acceptance criteria

Cast as US-2. Items of A: MSK-1 owned by MARS, MSK-2 owned by VENUS, MSK-3 with no owning team, MSK-4 deleted (owned by MARS, assigned to Ivan, whose membership ended); B has BET-1 (MARS); C has FIN-1 (MARS); the deactivated E has EEE-1. MSK-1, BET-1 and FIN-1 each have the number 1. Olga «Участник», Ann «Администратор». The organization's list scenarios hold with nothing chosen in the selectors. Every project is visible to every member until `feat-041-project-visibility`, which applies the set form to this read and re-runs the visibility clauses of doc-012 US-10.

```gherkin
Scenario: The organization's list holds every active project's items once (BR-MX-9, BR-MX-16; BR-NV-12)
  When Olga opens «Рабочие элементы»
  Then she sees MSK-1, MSK-2, MSK-3, BET-1 and FIN-1, at most 50 to a page, each once
  And each row shows its project and its owning team, «Без команды» for MSK-3, and its title opens the card in the item's own project
  And MSK-4, deleted, and EEE-1, of a deactivated project, are in no row, no count and no option (mod-005 BR-DR-6; BR-NV-13)

Scenario: One total order on both lists, across a page boundary (BR-NV-12; BR-WS-12)
  Given three items changed at the same moment: MSK-10, MSK-2 and BET-2, and a page boundary falls between them
  When Olga reads page 1 and page 2 of the organization's list
  Then the items stand by change time descending, then number descending, then project key: MSK-10, BET-2, MSK-2 (10 before 2 numerically; the tie at number 2 goes to the project key, BET before MSK)
  And no item repeats or is dropped between the pages, and A's section keeps the same order for its own items

Scenario: The project's section is the same list fixed to the project (BR-NV-12)
  When Olga opens A's «Рабочие элементы»
  Then she sees A's live items only, with the same columns, and its filter bar offers «Без команды» as an option; teams are chosen in the team selector, which offers exactly MARS and VENUS under A
  And the organization's list has no «Команда» in its filter bar (Q-6 A)

Scenario: Filtering by team is the team selector (BR-MX-9; mod-017 BR-OT-8; mod-020; Q-6 A)
  When Olga, on the organization's list, chooses MARS in the team selector
  Then MARS's menu opens, and there is no «Рабочие элементы» in it; MARS's «Канбан» and «Бэклог» span its projects
  And the organization-wide list under a chosen team or department is reached by setting the selectors to «все», which shows every item again, with the page back at 1
  And with A chosen and MARS in the team selector, A's «Рабочие элементы» lists only MSK-1, the address carries `teamId`, and a link opens the narrowed list; the filter-bar option «Без команды» on A's list shows only MSK-3 and is exclusive with a chosen team

Scenario: Feat-003's filters are on both lists (BR-NE-8; BR-WS-1..-7)
  When Olga types «оплат» in «Поиск» and chooses «Исполнитель» and «Статус» on the organization's list and then on A's section
  Then both offer the same fields, narrow by the same rules, keep the filters in the address, and show «Ничего не найдено» with «Сбросить фильтр» when nothing matches
  And the options of «Исполнитель» on the organization's list are the people assigned to the items of the listed projects, former members marked «(не в организации)», then «Не назначен»

Scenario: The keyword on the organization's list (BR-NV-12; BR-WS-9, BR-WS-15)
  When Olga types «1» in «Поиск»
  Then MSK-1, BET-1 and FIN-1 are listed, once each, because a number matches the item with that number in every listed project, and an item whose title contains «1» is listed too
  And «MSK-1» lists MSK-1 only, as a whole identifier names one item in the organization
  And no item of «Другая» is ever found, and on A's section the keyword finds A's items only

Scenario: Deleted items are listed only when «Удалена» is chosen (BR-NE-8; BR-WS-4)
  When Olga chooses «Удалена» in «Статус» on the organization's list
  Then MSK-4 is listed, reading «Удалена», its title opens the read-only card of mod-005, and the counts follow
  And without that choice MSK-4 is in no row, no count, no option of «Исполнитель» and no keyword result, on either list

Scenario: The list meets its budgets at volume (NFR, H-4 A)
  Given an organization of 200 projects with 5 000 items each
  When Olga reads a page unfiltered, then with a team filter, with an assignee filter, with a state filter, and the options of «Исполнитель»
  Then each read, the pager's count included, takes at most 2 s, and a keyword that is an identifier or a number takes at most 2 s
  And a keyword that contains-matches titles takes at most 5 s, and the number of statements does not grow with the number of projects

Scenario: A foreign project's item is not found (BR-MX-1; BR-NV-7)
  When Olga opens the path address of an item of «Другая» in «Ромашка»
  Then she sees «Проект не найден»
  And when she opens the organization's list with `?workitem=` naming an item of «Другая», the list shows and the card answers «Рабочий элемент не найден»

Scenario: A card is reached from either list (BR-NV-4, BR-NV-10)
  When Olga opens MSK-1 from the organization's list, and then from A's section
  Then the card is at `…/projects/<A>/workitems/MSK-1` both times, the project selector reads A (a team selector, if one is chosen, stays), and «Рабочие элементы» is marked in A's menu
  And the card's back link «Рабочие элементы» opens A's section, unfiltered, and a purge of the item from the card returns to the list it was opened from, filters included, on page 1 (BR-WS-5)

Scenario: An empty organization (BR-MX-2)
  Given «Пустая» has no work item
  When a member opens «Рабочие элементы»
  Then she sees «Рабочих элементов пока нет», with no filter applied, and a link to «Проекты»; there is no create command on this list

Scenario: The selected entry opens the list unfiltered (H-1 A; BR-WS-11; mod-020 review CR-2)
  Given Olga is on A's «Рабочие элементы» with MARS in the team selector, a keyword and a state filter
  When she chooses «Рабочие элементы» in the menu
  Then the page's filters are cleared, the selectors (A and MARS) stay as they are, every link without `teamId` opens the list unfiltered, and BR-OT-8 and `design.md` § 6.3 of mod-017 state the rule once

Scenario: Every read across projects takes the caller (BR-NV-15; ADR-0029)
  When the signature test over the reads of the organization's list, «Обзор» and the lists is run
  Then each takes the caller's membership and role as a required argument, and a read without it does not compile
```
