# US-11: A member finds work items in the list

- story: US-11
- depends-on: US-1, US-8, US-10
- shippable: no — analysis only; the list is delivered by
  `mod-003-work-item-core`, the filters by `feat-003-work-item-search`
- status: pending

As an **organization member**, I want **one list of the project's work items that I
can narrow down in place**, so that **I can find the item I need without
opening a board or writing a query**.

## Acceptance criteria

The list's columns, following the reference product (screenshot 08):
identifier, Тип, Заголовок, Исполнитель, Статус, Область, Теги. The same
filters on a team's board and backlog are doc-003's
`feat-NNN-board-backlog-filter`.

```gherkin
Scenario: The list shows the project's work items
  Given a project with work items of several types
  When a member opens the work item list
  Then every non-deleted work item of that project is listed with the
    columns above, most recently changed first (Дата изменения)
  And the identifier and the title together open the item's form

Scenario: Filtering by state
  Given work items in different states (US-3)
  When the member filters by «Активна»
  Then only items currently in that state are listed
  And clearing the filter brings the others back

Scenario: «Разрешена» spans two categories
  Given a «Пользовательская история» and a «Баг», both in «Разрешена»
  When the member filters by «Разрешена»
  Then both are listed — the filter matches the state's label on every type,
    although the story's state is InProgress and the bug's is Resolved
    (spec § Types, levels and states)

Scenario: Filters combine
  Given a project with work items assigned to several people and tagged
    variously
  When the member filters by an assignee and by a tag at once
  Then only the items matching both are listed
  And the filter controls show which conditions are currently applied

Scenario: Items of a former or inactive member can still be found
  Given «Иван», a former member of the organization, and «Пётр», a member
    whose account is disabled, both still assignees of some work items
    (US-2)
  When the member opens the assignee filter
  Then «Иван» is offered marked «не в организации» and «Пётр» marked
    «неактивен», and filtering by either lists exactly their items
  And the Исполнитель column shows each with the same mark (spec § Fields)

Scenario: Filtering by area covers sub-areas
  Given an area tree where «Платформа» has the child «Платформа/API»
  When the member filters by «Платформа»
  Then work items located in «Платформа/API» are listed as well
    (doc-001 BR-15; the same inheritance boards use, doc-001 BR-3)

Scenario: Keyword matches the title and the identifier
  Given a work item whose title contains «оплата», with identifier
    MSK-BUG-42 (US-4)
  When the member types in the keyword filter «оплата», then
    «MSK-BUG-42», then only the number «42»
  Then the item is found all three times — the number alone is unique
    within the project (W-6)
  And a word that appears only in its Описание or in a comment does not find
    it — full text is release 2/3 (spec § Out of scope)

Scenario: Deleted items are reached through the state filter
  Given a project with deleted work items (US-10)
  When the member filters by «Удалена»
  Then exactly the deleted items are listed, with restore reachable from
    each of them
  And with no state filter naming «Удалена», deleted items are absent from
    the list and from every other filter's result (D-5, doc-001 BR-14)

Scenario: Tags of another project are never offered
  Given a tag that exists only in project «Проект Б»
  When the member opens the tag filter in «Проект А»
  Then that tag is not offered (doc-001 BR-8)

Scenario: Filters are session state, not a saved query
  Given the member has set several filters
  When they navigate away and come back
  Then the filters are not kept as a named, reusable query — MVP has no
    saved or shared queries (D-4)

Scenario: A filter hides items but never reorders them
  Given the list in whatever order it currently shows
  When any filter is applied or cleared
  Then the items still shown keep their relative order — a filter never
    changes the order, and never writes the backlog order it may be
    displaying (doc-001 BR-12)

Scenario: An empty result says so
  Given a filter combination matching nothing
  When it is applied
  Then the list shows a clear «Ничего не найдено» state, not an error and
    not the previous result
  And a project with no work items at all shows its own empty state, with
    the «Создать рабочий элемент» control (US-1) — not the same message as
    a filter that matched nothing

Scenario: A big list stays usable
  Given a project with 5 000 work items
  When a member opens the list and applies a filter
  Then the result is on screen within 2 s (spec § NFR)

Scenario: A reader can list and filter
  Given a user whose role in the organization is viewer
  When they open the list and apply filters
  Then everything above works for them
  And no create, edit or delete control appears anywhere on the screen
```
