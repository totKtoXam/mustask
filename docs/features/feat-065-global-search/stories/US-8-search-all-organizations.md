# US-8: The user can search all their organizations at once

- story: US-8
- depends-on: US-2, US-3, US-4, US-5, US-7
- shippable: no — a third option of the palette's scope chip; delivered by `feat-NNN-search-all-organizations` after the palette and the full search page
- status: pending

As a **member of several organizations**, I want **to choose «Все организации» in the palette's scope and find objects of any of them, each under that organization's own access rules**, so that **I do not have to switch organization first to find something I only half remember**.

Cast: Olga is «Участник» in «Ромашка» (sees ALP, VENUS, MARS, «Логистика» only) and «Участник» in «Другая» (sees department «Склад», team ORION, project «Зет» (ZED), ZED-US-1 «Оплата картой»). Max is a member of «Ромашка» only. Ann is «Администратор» of «Ромашка» and of «Третья»; her membership in «Другая» is deactivated. In «Другая» there is also a project with key ALP, «Альфа-2», holding ALP-US-7 «Оплата в магазине».

## Acceptance criteria

```gherkin
Scenario: The option appears only for several organizations
  Given Olga belongs to two organizations and Max to one
  When each opens the scope chip's menu
  Then Olga's lists «Ромашка», «Другая» and «Все организации»
  And Max's lists «Ромашка» only, as a read-only tag

Scenario: The default is unchanged and the choice is short-lived
  Given Olga is on a page of «Ромашка»
  When she opens the palette
  Then the scope is «Ромашка»
  When she chooses «Все организации», closes the palette and opens it again
  Then the scope is «Ромашка» again

Scenario: Results from both organizations, grouped by type
  Given Olga chose «Все организации»
  When she types «оплата»
  Then «Рабочие элементы» holds ALP-US-7 «Оплата картой» (Ромашка), ZED-US-1 «Оплата картой» (Другая) and ALP-US-7 «Оплата в магазине» (Другая)
  And no group is split by organization

Scenario: Every row names its organization
  Then the secondary line of every row, the current organization's included, ends with its organization's name, for example «В работе · Альфа · Ромашка»

Scenario: Same identifier in two organizations
  When Olga types «ALP-US-7»
  Then two rows are listed, «ALP-US-7 Оплата картой» (Ромашка) and «ALP-US-7 Оплата в магазине» (Другая), told apart by the organization name
  And choosing each opens its own organization's card

Scenario: Each organization's own sight
  Given «Ромашка» has the project «Браво» (BRV) that Olga does not see there
  When she types «браво» or «BRV»
  Then nothing of it is found, though she is a member of «Ромашка» and sees other objects there
  And a grant Olga holds in «Другая» changes nothing in «Ромашка»

Scenario: Each organization's own gates
  Given a caller lacks `organizations.membership.read` in «Другая» but holds it in «Ромашка»
  When he types a colleague's name
  Then «Пользователи» holds only the «Ромашка» person

Scenario: A deactivated membership contributes nothing
  Given Ann is an active member of «Ромашка» and «Третья», and her membership in «Другая» is deactivated
  When she chooses «Все организации» and types «зет»
  Then nothing from «Другая» is found and nothing mentions it

Scenario: No trace of organizations the user is not in
  Given an organization «Тайная» where Olga is no member and which holds «Оплата тайная»
  When Olga searches «Все организации» for «тайная»
  Then she gets «Ничего не найдено»
  And the response body equals the one for a name that exists nowhere
  And the client never sends organization ids; the server reads her active memberships

Scenario: Ranking across organizations
  Given «Ромашка» (current) has the team «Корион» (contains «ори») and «Другая» has the team ORION (starts with «ори»)
  When Olga types «ори»
  Then ORION comes before «Корион», though it is in another organization
  Given instead «Ромашка» has «Орион-1» and «Другая» has «Орион-2», both starting with «ори»
  Then «Орион-1» (current organization) comes first, then «Орион-2»
  And among equal tiers in two other organizations the order is by organization name, then BR-GS-8's own tie-break

Scenario: Limits are shared
  Given «Ромашка» and «Другая» hold 4 and 6 matching projects
  When Olga types the common word
  Then «Проекты» shows 5 rows in total and «Показать ещё»
  When she chooses «Показать ещё»
  Then up to 20 rows in total are shown

Scenario: Choosing a row of another organization
  Given Olga's header context is «Ромашка › Логистика › MARS › все»
  When she chooses the project «Зет» of «Другая»
  Then the project opens at its address in «Другая»
  And the current organization and header context become «Другая › Склад › все › Зет», with no «Выбор сброшен» notice
  And «Зет» goes to the recent list of «Другая»

Scenario: A user row of another organization
  When Olga chooses a person from «Другая»
  Then «Другая»'s «Рабочие элементы» opens filtered by that person as «Исполнитель»

Scenario: Recent items in this mode
  Given Olga has recent items in both organizations
  When she opens the palette with «Все организации» chosen and an empty field
  Then she sees the recent items of the current organization only, as in one-organization mode
  And choosing «Другая» in the chip shows «Другая»'s recent items

Scenario: Full search carries the scope
  Given Olga chose «Все организации» and typed «оплата»
  When she chooses «Искать везде, включая описания»
  Then the page opens with the same scope and query, results of both organizations by relevance, each row naming its organization
  And the page's scope control can return it to one organization, which replaces the address's scope

Scenario: One request
  Given Olga belongs to 5 organizations
  When she types a query in «Все организации»
  Then one search request is sent, not one per organization
  And the statement count of the read is the same as for 1 and for 3 organizations

Scenario: Speed
  Given 5 organizations at the volumes of the spec and a user who sees all of them
  When a 2-word query is sent
  Then the read answers within 500 ms at p95 and the rows are on screen within 800 ms of the last keystroke at p95

Scenario: Failure of the mode
  Given the read fails or answers 429
  Then the message and «Повторить» are those of the one-organization mode and the scope stays «Все организации»
```
