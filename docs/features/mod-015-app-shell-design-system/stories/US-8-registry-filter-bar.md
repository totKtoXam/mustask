# US-8: Registries share a dense table with a filter bar

- story: US-8
- depends-on: US-6, US-7
- shippable: no — its only consumer in this task is «Языки», restyled by US-11
- status: pending

As **a signed-in user working a list**, I want **every registry to be a
dense table with its filters in one bar above it**, so that **I scan many
rows at once and narrow them the same way on every list**.

## Acceptance criteria

```gherkin
Scenario: Filter bar above the table
  Given a dictionaries reader on «Языки»
  Then one filter bar sits between the page header and the table
  And it holds «Код», «Эндоним», «По умолчанию», «Активность» and «Порядок»
    in a single band at 1440 px width
  And while no filter is set, no clear control is shown

Scenario: Clearing all filters from the bar
  Given the user set «Активность» to «Неактивные» and typed into «Код»
  Then the filter bar shows a text button «Очистить фильтры»
  When they activate it
  Then every filter is empty, page 1 is shown and «Очистить фильтры» is gone

Scenario: Two ways out of an empty filter, never two controls of one name
  Given a filter that matches no language
  Then the filter bar offers «Очистить фильтры» and the empty frame offers
    «Сбросить фильтр» (US-7)
  And each clears every filter and shows page 1

Scenario: Dense table
  Given «Языки» with ten languages on the page
  Then the column headers sit on a header row visually distinct from the
    rows
  And the row under the pointer is highlighted
  And, measured in a browser, every row is at most 40 px high (BR-3)

Scenario: Sorting is visible
  Given the registry is unsorted
  When the user activates the header «Код»
  Then the rows are requested sorted by code ascending, as today
  And the header shows an ascending indicator and `aria-sort="ascending"`
  When they activate it again
  Then the indicator and `aria-sort` turn descending

Scenario: Row actions are reachable without a pointer
  Given a dictionaries administrator on «Языки»
  Then every row's actions are icon buttons named «Редактировать <код>» and,
    on an active non-default language, «Сделать языком по умолчанию <код>»
  And they are reachable by Tab, not only shown on hover

Scenario: Paging in Russian
  Given 25 languages match
  Then the paging control under the table reads «1–10 из 25»
  When the user goes to the next page
  Then it reads «11–20 из 25»

Scenario: Behaviour unchanged
  Given `LanguagesRegistryPage.test.tsx` as it stands at the start of this task
  When it runs against the restyled registry
  Then every test in it passes without an edit
```
