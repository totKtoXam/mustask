# US-3: The panel search finds and chooses an object of the chosen organization

- story: US-3
- depends-on: US-1, US-2
- shippable: no — it chooses through US-2's rules and lives in US-1's panel; ships in this task
- status: pending

As an **organization member**, I want **to type part of a name at the top of the panel and pick a department, team or project from the answer**, so that **I reach an object without walking the columns**.

Cast: as US-1; Ann sees everything, Olga sees in «Ромашка» only VENUS, MARS, A and «Логистика». Rules: doc-016 BR-TD-21 (the panel field only, one organization), BR-TD-22; design § 3.1. The «Структура» filter field, the search across organizations and the global palette are out of scope.

## Acceptance criteria

```gherkin
Scenario: The field and the results replace the columns
  When Ann opens the panel
  Then the top field is labelled «Найти департамент, команду или проект» and the columns show below it
  And when she types, the columns are replaced by the grouped results until the field is cleared

Scenario: Matching, from the first character
  Given Ann's chosen organization is «Ромашка»
  When she types «jup»
  Then «Команды» lists JUPITER with its colour mark and the grey path «Финансы»
  And typing «fin» finds the project C by its key FIN, and «фин» finds the department «Финансы», each in its own group
  And matching ignores case

Scenario: Grouping and limits
  Given more than 5 teams match
  Then «Департаменты», «Команды», «Проекты» are shown in that order, an empty group is absent, each holds at most 5 rows by name
  And a department row shows no grey path

Scenario: Only the chosen organization is searched
  Given Olga is also a member of «Другая» with the team ORION and her chosen organization is «Ромашка»
  When she types «orion»
  Then the answer reads «Ничего не найдено»

Scenario: Only what one sees is found (BR-TD-12..-15)
  Given Olga sees in «Ромашка» VENUS, MARS, A and «Логистика» only
  When she types «jup», or «c», or «Финансы»
  Then JUPITER, the project C and «Финансы» are not found and the answer reads «Ничего не найдено»

Scenario: Choosing a result applies the choice rules
  Given Ann's selection is «Ромашка» / «Логистика» / MARS / A
  When she types «jup», presses ↓ and Enter
  Then the selection reads «Ромашка» / «Финансы» / JUPITER / «все», she sees «Выбор сброшен: проект A.» and the panel closes

Scenario: Keyboard
  Given results are shown
  Then the first row is active, ↑ ↓ move and wrap, Enter chooses, and Esc closes the panel

Scenario: Loading and failure
  When the search is running, skeleton rows show
  And on a failed read the field shows «Не удалось выполнить поиск. Попробуйте ещё раз.» with «Повторить»

Scenario: The answer within the volume (NFR)
  Given the chosen organization has 1 000 areas, 200 projects and 200 teams visible to Olga
  When she types part of a name
  Then the answer is shown within 2 s
```
