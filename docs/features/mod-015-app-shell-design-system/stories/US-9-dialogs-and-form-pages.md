# US-9: Dialogs and form pages share one pattern

- story: US-9
- depends-on: US-6, US-7
- shippable: no — its consumers in this task are the language form and the default-language dialog, restyled by US-11
- status: pending

As **a signed-in user who edits data**, I want **every dialog and every
form to be laid out and to behave the same way**, so that **I know where
to type, where errors appear and how to back out, on any screen**.

## Acceptance criteria

```gherkin
Scenario: Dialog anatomy
  Given a dictionaries administrator opens «Сделать языком по умолчанию» on
    an active language
  Then the dialog has a title, a close control named «Закрыть» in its
    header, its content, and a footer with «Отмена» at the left of the
    primary action
  And the primary action is the only filled button in the dialog

Scenario: Backing out of a dialog
  Given the same dialog is open and idle
  When the user presses Escape, activates «Закрыть» or activates «Отмена»
  Then the dialog closes, nothing is changed, and focus returns to the
    button that opened it

Scenario: A busy dialog cannot be dismissed
  Given the user confirmed and the request has not answered yet
  Then «Отмена», «Закрыть» and the primary action are disabled
  And a progress indicator is shown
  And Escape and a click outside the dialog do nothing

Scenario: A destructive confirmation looks destructive
  Given `ConfirmDialog` used for an irreversible action
  Then its primary action uses the error colour and names the action
  And `design.md` and the prototype show it with an example from a later
    module, e.g. «Удалить навсегда» for a work item

Scenario: Form page layout
  Given a dictionaries administrator opens «Добавить язык»
  Then the page header holds the title «Добавить язык» and no «К списку»
    button; the page trail's «Языки» and «Отмена» lead back to the registry
  And the fields «Код», «Эндоним», «Порядок» stand in one column of limited
    width, each with its label and helper text
  And «Отмена» and «Добавить» sit together below the last field, «Добавить»
    filled in the accent colour
  And «Код» has keyboard focus

Scenario: Edit form layout
  Given a dictionaries administrator opens the language «ru»
  Then the page header holds the title «Язык: ru»
  And the code, «Эндоним», «Порядок», «Активен» and the change stamp stand in
    the same column, «Отмена» and «Сохранить» below them
  And while a version conflict is being compared, the comparison panel
    takes the place of «Отмена» and «Сохранить», as today

Scenario: Field and form errors
  Given the server refuses the new language with a field error on «Код»
  Then the error text appears under «Код» in the error colour and the field
    is marked invalid (`aria-invalid="true"`)
  Given the server refuses it with an error not tied to a field
  Then the message appears in an error banner above the first field

Scenario: A busy form cannot be submitted twice
  Given the user activated «Добавить» and the request has not answered
  Then the fields, «Отмена» and «Добавить» are disabled

Scenario: No leave guard in the MVP (D-22)
  Given a dictionaries administrator has typed into «Эндоним» on «Добавить
    язык» and not saved
  When they activate «Главная» in the navigation, the product mark or the
    trail's «Языки»
  Then the new page opens with no confirmation and the input is dropped,
    as today

Scenario: Patterns beyond this task are designed now
  Given `design.md` and the prototype
  Then they show a form dialog (create in a dialog, e.g. a new work item's
    title and type), and a large settings dialog with its own section list
    on the left and «Отмена» / «Сохранить» in the footer
  And `design.md` states that no form page, dialog or the work item form
    guards leaving with unsaved input in the MVP
```
