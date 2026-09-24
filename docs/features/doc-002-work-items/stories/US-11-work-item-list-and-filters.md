# US-11: A team member finds work items in the list

- story: US-11
- depends-on: US-1, US-8, US-10
- shippable: no — analysis only; the list is delivered by
  `mod-003-work-item-core`, the filters by `feat-003-work-item-search`
- status: pending

As a **team member**, I want **one list of the project's work items that I
can narrow down in place**, so that **I can find the item I need without
opening a board or writing a query**.

## Acceptance criteria

The list's columns, following the reference product (screenshot 08):
identifier, Тип, Заголовок, Исполнитель, Статус, Область, Теги.

```gherkin
Scenario: The list shows the project's work items
  Given a project with work items of several types
  When a team member opens the work item list
  Then every non-deleted work item of that project is listed with the
    columns above, most recently changed first
  And the identifier and the title together open the item's form

Scenario: Filtering by state
  Given work items in different states (US-3)
  When the team member filters by «Активна»
  Then only items currently in that state are listed
  And clearing the filter brings the others back

Scenario: Filters combine
  Given a project with work items assigned to several people and tagged
    variously
  When the team member filters by an assignee and by a tag at once
  Then only the items matching both are listed
  And the filter controls show which conditions are currently applied

Scenario: Items of a former member can still be found
  Given «Иван», a former member of the organization, is still the assignee
    of some work items (W-3, US-2)
  When the team member opens the assignee filter
  Then «Иван» is offered, marked as a former member, and filtering by him
    lists exactly those items
  And the Исполнитель column shows him with the same former-member mark

Scenario: Filtering by area covers sub-areas
  Given an area tree where «Платформа» has the child «Платформа/API»
  When the team member filters by «Платформа»
  Then work items located in «Платформа/API» are listed as well
    (doc-001 BR-3, the same inheritance boards use)

Scenario: Keyword matches the title and the identifier
  Given a work item whose title contains «оплата», with identifier
    MSK-BUG-42 (US-4)
  When the team member types in the keyword filter «оплата», then
    «MSK-BUG-42», then only the number «42»
  Then the item is found all three times — the number alone is unique
    within the project (W-6)
  And a word that appears only in its Описание or in a comment does not find
    it — full text is release 2/3 (spec § Out of scope)

Scenario: Deleted items are reached through the state filter
  Given a project with deleted work items (US-10)
  When the team member filters by «Удалена»
  Then exactly the deleted items are listed, with restore reachable from
    each of them
  And with no state filter set, deleted items are absent (D-5)

Scenario: Tags of another project are never offered
  Given a tag that exists only in project «Проект Б»
  When the team member opens the tag filter in «Проект А»
  Then that tag is not offered (doc-001 BR-8)

Scenario: Filters are session state, not a saved query
  Given the team member has set several filters
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
  When a team member opens the list and applies a filter
  Then the result is on screen within 2 s (spec § NFR)

Scenario: A reader can list and filter
  Given a user with only the "viewer" role
  When they open the list and apply filters
  Then everything above works for them
  And no create, edit or delete control appears anywhere on the screen
```
