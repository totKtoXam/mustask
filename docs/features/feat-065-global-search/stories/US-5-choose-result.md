# US-5: Choosing a result opens it and sets the work context

- story: US-5
- depends-on: US-2, US-3
- shippable: no — the palette's behaviour on Enter; ships inside `feat-NNN-search-palette`
- status: pending

As a **member of an organization**, I want **a chosen result to open and, for an organization object, to become my work context like any other choice**, so that **the header and the left menu match what I opened**.

Context rules are doc-016's (BR-TD-9, -10) on ADR-0032 addresses: the bare object address opens the first entry of the object's menu. Cast: header selection «Ромашка › Логистика › MARS › все».

## Acceptance criteria

```gherkin
Scenario: A project
  Given the header selection is «Ромашка › Финансы › JUPITER › все» and Ann chooses project «Альфа»
  When she presses Enter on its row
  Then the page opens at the project's first menu entry
  And the selection becomes «Ромашка › Логистика › все › Альфа»
  And the notice reads «Выбор сброшен: команда JUPITER.» since JUPITER does not fit

Scenario: A team
  Given the selection is «Ромашка › все › все › все»
  When Ann chooses the team VENUS
  Then the team's page opens and the selection is «Ромашка › Логистика › VENUS › все», with no notice

Scenario: A department
  Given the selection is «Ромашка › Логистика › MARS › Альфа»
  When Ann chooses the department «Финансы»
  Then its page opens and the team and project reset to «все» with «Выбор сброшен: команда MARS, проект Альфа.»

Scenario: A choice that fits resets nothing
  Given the selection is «Ромашка › Логистика › все › Альфа»
  When Ann chooses the team VENUS (linked to «Альфа»)
  Then the selection is «Ромашка › Логистика › VENUS › Альфа» with no notice
  When she then chooses the department «Логистика»
  Then the selection stays «Ромашка › Логистика › VENUS › Альфа», the department's page opens, and no notice is shown

Scenario: A sub-area
  When Ann chooses the sub-area «Сборка» of project «Альфа»
  Then the node's page opens and the selection shows project «Альфа» and its department (ADR-0032 § 5)

Scenario: A work item
  Given the selection is «Ромашка › Логистика › MARS › все»
  When Max chooses ALP-US-7
  Then its card opens under its project (the address names the project first)
  And the project «Альфа» and department «Логистика» are chosen; the team stays MARS if it still fits, else resets with the notice

Scenario: A user
  When Ann chooses the user «Макс Орлов»
  Then the organization's «Рабочие элементы» opens with «Исполнитель» = Макс Орлов
  And no user page is opened (none exists)

Scenario: A user assigned nothing
  Given Vera «Вера Лис» is assigned no live item in a project Ann sees
  When Ann chooses Vera's row
  Then the list opens empty, as for any «Исполнитель» value that selects nothing (feat-003 BR-WS-10): the band shows a filter is on and offers «Очистить фильтры»

Scenario: New tab
  When Ann presses Ctrl+Enter, Cmd+Enter or middle-clicks a row
  Then the object opens in a new tab with the context taken from its address
  And the palette stays open and the current page's context is unchanged

Scenario: The palette closes
  When Ann chooses a row without a modifier
  Then the palette closes, the focus moves to the opened page, and the object goes to the top of the recent list (US-6)

Scenario: The object disappeared meanwhile
  Given Max chooses a row whose project was deleted a moment ago
  Then he lands on the usual «Проект не найден.» page for that address, no server error, and the entry is not added to recent

Scenario: A hidden object by address
  Given a link to a project Olga does not see
  Then she gets «Проект не найден.» as BR-TD-12 says; the palette played no part

Scenario: The object belongs to the palette's scope, not to the header's
  Given the palette's chip was set to «Другая» while the header shows «Ромашка»
  When Olga chooses the project «Зет»
  Then the address of «Зет» in «Другая» opens and the header context moves to «Другая» as for any cross-organization choice (ADR-0032 § 5)
```
