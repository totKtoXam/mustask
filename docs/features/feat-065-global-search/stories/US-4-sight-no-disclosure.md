# US-4: Only what the user sees is found, and nothing reveals what is hidden

- story: US-4
- depends-on: US-2, US-3
- shippable: no — a property of the search read; verified by the tests of both read lines
- status: pending

As a **member who takes part in only some of the organization**, I want **the search to find only what I see (doc-016 BR-TD-12..-15) and to show no trace of the rest**, so that **it is not a way around the access rules**.

Cast: Olga (member of VENUS, no grants) sees «Логистика», MARS, VENUS and project «Альфа» (ALP); not «Браво» (BRV), «Финансы», JUPITER, «Чарли», «Дельта». Max sees every project through his department grants; every «Видим всей организации» is off.

## Acceptance criteria

```gherkin
Scenario: A hidden project is not found
  When Olga types «браво» or «BRV»
  Then she gets «Ничего не найдено»
  And the same text and the same response body as for «запрос-которого-нет»

Scenario: A hidden project's items are not found
  Given BRV-BUG-3 «Оплата картой» exists in «Браво»
  When Olga types «BRV-BUG-3», «BRV-3» or «оплата картой»
  Then BRV-BUG-3 is in no row
  And ALP-US-7 «Оплата картой» is found for the title query

Scenario: Hidden teams and departments
  When Olga types «jupiter» or «финансы»
  Then nothing is found
  When she types «mars»
  Then MARS is found, because she sees project ALP linked to it (BR-TD-13)

Scenario: "More" never reflects a hidden row
  Given 7 projects contain «про»; Olga sees 4 of them, Max all 7
  When Olga types «про»
  Then «Проекты» shows 4 rows and no «Показать ещё»
  When Max types «про»
  Then «Проекты» shows 5 rows and «Показать ещё»

Scenario: No totals anywhere
  Then no row, heading, tooltip, live region or response body carries a total or a number of hidden matches

Scenario: Order does not depend on hidden rows
  Given the same visible matches, once with a hidden object that would rank above them
  Then the visible rows come in the same order and the same groups

Scenario: Users
  Given a caller without `organizations.membership.read` (no built-in role today; a handler test)
  Then the group «Пользователи» is absent for them, and a typed email finds nothing

Scenario: Sight changes between requests
  Given Olga searched and saw ALP
  When her only route to ALP ends (VENUS membership removed) and she types again
  Then ALP is no longer found, on the next request without signing out

Scenario: Recent item that left sight
  Given ALP was in Olga's recent list
  When her sight of ALP ends and she opens the palette with an empty field
  Then ALP is not listed, with no message

Scenario: Foreign organization
  Given the palette is scoped to «Ромашка»
  When Olga types «зет» or «ZED-US-1»
  Then «Зет» and ZED-US-1 of «Другая» are not found

Scenario: Not a member
  Given Ann is no member of «Другая», and neither is an instance administrator
  When either sends a search read or a recent-items read for «Другая»
  Then it answers «Организация не найдена.»

Scenario: Membership ends while the palette is open
  Given Olga has the palette open on «Другая»
  When her membership of «Другая» ends and she types again
  Then the palette says «Организация не найдена.» with no «Повторить»

Scenario: Entry-point tests
  Then there is a test per object kind (department, team, project, sub-area, user, work item) that holds a hidden one out of results, "more" and live region
  And the recycle bin's entry-point test lists the palette's read (deleted items hidden)
```
