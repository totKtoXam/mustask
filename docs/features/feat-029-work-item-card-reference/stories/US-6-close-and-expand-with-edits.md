# US-6: A user closes or expands the card without losing edits by accident

- story: US-6
- depends-on: US-4
- shippable: no — it guards the dialog of US-1
- status: pending

As a **signed-in user**, I want **the dialog to ask before it throws away what
I typed, and to take my edits with me when I expand it to the page**, so that
**one stray Esc, one click outside the window or a wish for more room does not
cost me a long description**.

## Acceptance criteria

Data: US-1's. Spec BR-C-3..BR-C-5; D-4 (Q-1 decided (a): this story is the
answer); `design.md` § 4.1, § 6, § 9. The dialog is open on MSK-US-2.

```gherkin
Scenario Outline: Every way to close asks when edits are unsaved
  Given the user changed «Описание»
  When the user <closes>
  Then a dialog «Закрыть без сохранения?» opens above the card with «Несохранённые
    изменения рабочего элемента MSK-US-2 будут потеряны.», «Продолжить
    редактирование» (focused) and the error-coloured «Закрыть без сохранения»
  And the card is still open behind it, the edit kept

  Examples:
    | closes                          |
    | presses Esc, no menu being open |
    | activates «Закрыть»             |
    | clicks outside the dialog       |

Scenario: Keep editing
  Given the question is open
  When the user chooses «Продолжить редактирование» or presses Esc
  Then the question closes, the card stays with its edit, and focus is back
    where it was in the card

Scenario: Close without saving
  Given the question is open
  When the user chooses «Закрыть без сохранения»
  Then both dialogs close, the address has no `?workitem`, focus is on the title
    that opened the card, and nothing was sent
  And opening MSK-US-2 again shows the stored «Описание»

Scenario: Nothing unsaved, nothing asked
  Given no field is changed, or a change was changed back
  When the user closes the dialog in any of the three ways
  Then it closes at once

Scenario: A saved card closes at once
  Given the user changed «Описание» and saved it
  When the user presses Esc
  Then the dialog closes with no question

Scenario: Esc belongs to what is open
  Given a list of «Статус» or the assignee's search is open
  When the user presses Esc
  Then only that list closes; the dialog and its edits are untouched

Scenario: Back and other navigation are not guarded
  Given the user changed «Описание»
  When the user goes Back in the browser
  Then the dialog closes with no question, as every page does (mod-015 § 9.7)

Scenario: Switching to a related item asks too
  Given the user changed «Описание»
  When the user activates «Открыть MSK-FEAT-6»
  Then a dialog «Перейти без сохранения?» opens with «Несохранённые изменения
    рабочего элемента MSK-US-2 будут потеряны.», «Продолжить редактирование» and
    «Перейти без сохранения»
  When the user chooses «Перейти без сохранения»
  Then the dialog shows MSK-FEAT-6 with none of the edits

Scenario: Expand carries the edits to the page
  Given the user changed «Заголовок» to «Письмо со ссылкой для входа» and
    «Приоритет» to «1»
  When the user activates «Развернуть на страницу»
  Then the full page of MSK-US-2 opens, with no question
  And its «Заголовок» and «Приоритет» show the edits, «Сохранить» is enabled,
    and a save sends those two fields only
  When the user goes Back
  Then the backlog is drawn with the dialog open on MSK-US-2 as it was read,
    without the edits (they went with the page)

Scenario: Edits do not travel to a changed item
  Given the user changed «Описание» and, before the page was read, another
    user changed «Приоритет»
  When the user activates «Развернуть на страницу»
  Then the page shows the edited «Описание» and the stored «Приоритет»

Scenario: A page opened by its address has no expand
  When the user opens MSK-US-2's page by its address
  Then the card has no «Развернуть на страницу» and no «Закрыть»
```
