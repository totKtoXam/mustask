# US-7: A user works on the same card as a full page, at any width

- story: US-7
- depends-on: US-3
- shippable: no — the page is where «Развернуть на страницу» of US-6 and the list «Рабочие элементы» lead
- status: pending

As a **signed-in user**, I want **the work item's page, opened by its address,
to be the same card as the dialog**, so that **a link I was sent, the list's
title and the expanded dialog all show one look, on a wide screen and on a
narrow one**.

## Acceptance criteria

Data: US-1's. Spec BR-C-6, BR-C-14, BR-C-17, BR-C-19, BR-C-20; mod-003 § Page
and § Not-found answers; `design.md` § 5, § 6, § 7, § 8. The window is
1 440 × 900 with the navigation expanded unless a scenario says otherwise.

```gherkin
Scenario: The page is the card
  When the user opens `…/projects/:p/workitems/MSK-US-2`
  Then the shell's trail «Рабочие элементы» stands above a white card with the
    header, band, tab and body of US-2 and US-3, on the application
    background, with the card's soft shadow
  And the type line «Пользовательская история MSK-US-2» is the page's only
    `h1`, and the browser tab reads «Пользовательская история MSK-US-2 —
    Mustask»
  And the card has no «Развернуть на страницу» and no «Закрыть»
  And the page, not the card, scrolls, and the header and the band scroll with
    it

Scenario Outline: The columns fold by the card's width, not the window's
  Given the window is <window> px wide
  Then the body stands in <columns>
  And the page does not scroll sideways

  Examples:
    | window | columns |
    | 1 440, navigation expanded (body about 1 128) | three columns: the first about 456 px, the others 320 |
    | 1 440, navigation collapsed to the rail        | three columns |
    | 1 000, navigation as an overlay (body about 944) | two columns |
    | 640, navigation as an overlay                   | one column |

Scenario: At 640 px the card is whole
  Given the window is 640 px wide
  Then the header's action row wraps: the assignee on its own line, the buttons
    below it at the right
  And «Статус» and «Область» stand one under the other, each label at the left
    of its value, and «Изменён» and the tab below them
  And every control is operable and named, and no text is cut

Scenario: From the list
  Given the list «Рабочие элементы»
  When the user clicks a title
  Then the full page opens, as before; the list does not open the dialog

Scenario: The page keeps mod-003's answers
  When the user opens `…/workitems/MSK-US-999`
  Then the frame «Рабочий элемент не найден» shows with «Проверьте адрес или
    вернитесь на главную.» and «На главную», the shell's «Рабочие элементы»
    marked
  When the project or the organization does not exist
  Then the frame is «Проект не найден» or «Организация не найдена»

Scenario: Loading and failing
  When the item is being read
  Then the card's frame shows at once with skeletons in the shape of the header,
    the band and the columns, announced once as «Загрузка данных»
  When the read fails
  Then an error banner with «Повторить» shows under the title «Рабочий
    элемент»

Scenario: The read-only mode, given a reason
  Given a host that sets the card read-only with the text «Рабочий элемент
    доступен только для чтения.»
  Then every field is a value with no frame and does not change on hover or
    focus, the Save group and «Отменить все изменения» are not drawn, «Обновить»
    and «Ещё действия» stay, and an info alert with that text stands between
    the band and the body
  And nothing in the product sets it today

Scenario: The page of an item opened from the dialog
  Given the user expanded the dialog of MSK-US-2
  Then the page is the one above, and «Рабочие элементы» in the trail leads to
    the project's list
```
