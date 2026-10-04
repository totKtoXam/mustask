# US-10: Work-tracking patterns are designed and prototyped

- story: US-10
- depends-on: US-1, US-6
- shippable: no — a design and prototype deliverable; the code comes with its first consumer, mod-003, mod-006 or mod-002 (BR-14, D-9)
- status: pending

As **the designer of a later module** (stage 3 of mod-002, mod-003, mod-006,
mod-007), I want **the work item form, board, backlog and their companions
already drawn in the panel's language**, so that **my `design.md` fills a
known pattern with my module's fields instead of inventing a layout**.

Verified by the human at gate-2 in the prototype; QA checks at stage 7 that
`design.md` covers every element these scenarios list.

## Acceptance criteria

```gherkin
Scenario: Work item list with a type picker
  Given the prototype's «Рабочие элементы» page
  Then it shows a page header with the command «Новый рабочий элемент», a
    filter bar (keyword plus dropdown filters for type, assignee, state,
    area and tag) and a dense list with identifier, type icon, title,
    «Исполнитель», «Статус» with its category dot, «Область» and «Теги»
  When the human opens «Новый рабочий элемент»
  Then a menu lists «Эпик», «Фича», «Пользовательская история», «Задача»,
    «Баг», each with the icon and colour of its appearance token (BR-4)

Scenario: Type and category language
  Given the prototype
  Then it shows each of the five appearance tokens and each of the five
    categories with its label beside its colour, none in the accent colour

Scenario: Work item form
  Given the prototype's work item form for a «Пользовательская история»
  Then a band at the top shows the type icon, the type name and the
    identifier in the form `PRM-US-12`, then the editable title
  And the band's right end holds «Сохранить» and a menu «Ещё действия» with
    «Удалить»
  And a header strip holds «Исполнитель», «Статус», «Область», «Итерация»
    and «Теги»
  And the body is laid out in columns of titled sections: «Описание» and
    «Критерии приёмки» in the wide column, planning fields in the narrow one
  And «Обсуждение» sits under the description
  And tabs «Детали», «История», «Связи», «Вложения» switch the body
  And a deleted work item's form is read-only, with «Восстановить» in the
    place of «Сохранить»

Scenario: Board
  Given the prototype's board for the level «Истории»
  Then it shows one column per board column with its header and item count
  And each card shows a stripe in its type's colour, the identifier, the
    title, the state with its category colour and the tags as chips
  And the first column offers «Новый элемент»
  And a column can be collapsed to a narrow strip showing its header

Scenario: Ordered backlog with a side pane
  Given the prototype's backlog for the level «Истории»
  Then it shows the order number, a drag handle, type, title, state and
    iteration per row
  And a pane on the right titled «Планирование» lists iterations and can be
    closed and reopened from the page header

Scenario: Preview side pane
  Given the prototype's work item list
  When the human selects a row
  Then a pane on the right previews that work item in the form's layout,
    without leaving the list

Scenario: Tree registry
  Given the prototype's settings pages «Области» and «Итерации»
  Then each shows a tree registry: rows indented by level, each parent
    with an expand/collapse control, the commands «Новый» and «Новый
    дочерний» in the command bar, and row actions reachable by keyboard
  And `design.md` states that mod-002 builds it (BR-14, D-19)

Scenario: Empty work-tracking surfaces
  Given the prototype's board and backlog with no items
  Then each shows the shared empty frame of US-7 with its own heading and
    «Новый рабочий элемент»

Scenario: Vocabulary and naming
  Given every label in `design.md` and the prototype
  Then work-tracking terms are derived from the doc-001 glossary, and
    «Задача» names only the type, never the generic entity (doc-001 D-7)
  And all content is placeholder data with Russian copy
  And the reference tracker is not named anywhere (BR-16)

Scenario: The prototype stays local (BR-17)
  Given the prototype opened in a browser with the network log recording
  Then it loads nothing from a host other than the one serving it
  And it uses no UI dependency the panel does not already render with

Scenario: The prototype is clickable end to end
  Given the prototype opened in a browser
  Then the navigation, the settings navigation, the context switcher, the
    collapse toggle, the type picker, a card opening its work item, the side
    panes, the tree's expand controls and every dialog of US-9 respond to
    clicks
```
