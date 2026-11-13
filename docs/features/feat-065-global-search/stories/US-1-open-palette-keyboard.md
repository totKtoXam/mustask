# US-1: The palette opens from Ctrl+K or the header button and works by keyboard

- story: US-1
- depends-on: -
- shippable: no — an empty palette finds nothing; it ships with US-2/US-3 in `feat-NNN-search-palette`
- status: pending

As a **member of an organization**, I want **to open a search dialog with Ctrl+K (Cmd+K on macOS) or the magnifier in the header and drive it entirely from the keyboard**, so that **I can jump anywhere without reaching for the mouse or the menus**.

## Acceptance criteria

```gherkin
Scenario: Open with the shortcut
  Given Olga is signed in and on any page of organization «Ромашка»
  When she presses Ctrl+K (Cmd+K on macOS)
  Then a centred modal dialog labelled «Поиск» opens
  And the focus is in its field
  And the browser's own Ctrl+K binding does not fire

Scenario: Open with the header button
  Given Olga is on any page of the shell
  When she clicks the magnifier at the right end of the header
  Then the same dialog opens
  And the button's tooltip and aria-label read «Поиск (Ctrl+K)», «Поиск (⌘K)» on macOS

Scenario: The shortcut toggles
  Given the palette is open
  When Olga presses Ctrl+K again
  Then the palette closes

Scenario: Closing returns focus
  Given Olga opened the palette with the magnifier
  When she presses Esc
  Then the palette closes, whatever the field holds
  And the focus is back on the magnifier

Scenario: Arrow keys cross groups
  Given the palette shows results in several groups
  When Olga presses ↓ on the last row of a group
  Then the first row of the next group becomes active, skipping group headings
  And ↓ on the very last row activates the first row, ↑ on the first activates the last
  And the active row is scrolled into view

Scenario: Enter chooses the active row
  Given a row is active
  When Olga presses Enter
  Then the row is chosen as in US-5
  And with Ctrl or Cmd held it opens in a new tab and the palette stays open

Scenario: Focus stays in the dialog
  Given the palette is open
  When Olga presses Tab repeatedly
  Then the focus cycles inside the dialog and never reaches the page behind it

Scenario: Screen reader
  Given the palette is open and shows 7 rows
  Then the field is exposed as a combobox over a listbox with aria-activedescendant
  And a polite live region says «Найдено: 7»
  And no hidden total is ever announced

Scenario: Narrow screen
  Given the viewport is 640 CSS px wide
  When the palette opens
  Then it fits the width and every control stays reachable by keyboard

Scenario: Typing a shortcut inside a text field of the page
  Given Olga is typing in a plain text field of a page (a form's «Заголовок»)
  When she presses Ctrl+K
  Then the palette opens, and the field's text is unchanged

Scenario: The rich-text editor keeps its own Ctrl+K
  Given Olga is typing in a work item's «Описание» editor
  When she presses Ctrl+K
  Then the editor's link command runs as today and the palette does not open
  And the magnifier in the header still opens the palette

Scenario: Another dialog is open
  Given a modal dialog of the page is open (a «Новый рабочий элемент» form)
  When Olga presses Ctrl+K
  Then nothing happens and the dialog keeps the focus

Scenario: IME composition
  Given an input method composition is open in the field
  When Olga presses Enter to commit the composition
  Then no row is chosen and no search is started until the composition ends

Scenario: No organization
  Given a signed-in user who belongs to no organization
  Then the header has no magnifier
  And Ctrl+K does nothing

Scenario: Scope organization
  Given Olga is on «Главная» with no organization in the address
  When she opens the palette
  Then the scope chip names her remembered organization, else her first by name
  And choosing another organization in the chip changes the palette's scope only, not the header's selectors
  And after the palette closes and opens again, the chip shows the default organization again
```
