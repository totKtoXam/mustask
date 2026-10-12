# US-1: A member edits Описание in a WYSIWYG editor

- story: US-1
- depends-on: -
- shippable: no — the editor without the server reduction (US-2) and the format contract (US-4) puts unfiltered markup in the store
- status: pending

As a **member who may edit a work item**, I want **to format Описание with a
toolbar and keyboard shortcuts and see the result as I type**, so that **a
description can carry headings, lists, links and code instead of a flat wall
of text**.

BR-RT-1, BR-RT-4, BR-RT-7, BR-RT-8, BR-RT-12.

## Interface copy (Russian)

| Where | Text |
|---|---|
| Toolbar group (`role=toolbar`) | «Форматирование описания» |
| Controls | «Заголовок 3», «Заголовок 4», «Полужирный», «Курсив», «Маркированный список», «Нумерованный список», «Увеличить отступ списка», «Уменьшить отступ списка», «Ссылка», «Убрать ссылку», «Код в строке», «Блок кода», «Таблица», «Добавить строку», «Удалить строку», «Добавить столбец», «Удалить столбец», «Строка заголовка», «Удалить таблицу», «Отменить», «Повторить» |
| Placeholder / helper (kept from mod-003) | «Добавьте описание» / «До 100 000 символов» |
| Link prompt | title «Ссылка», field «Адрес ссылки», hint «https://…», buttons «Применить», «Отмена» |
| Unsupported link address | «Допустимы адреса, начинающиеся с http://, https:// или mailto:.» |
| Image or file dropped / pasted | «Изображения в описание не вставляются.» (polite live region under the editor) |
| Over the visible limit | «Не более 100 000 символов.» (as the server's) |
| Over the markup ceiling | «Слишком много разметки. Упростите форматирование или сократите текст.» |

## Acceptance criteria

```gherkin
Scenario: The toolbar offers exactly the safe subset
  Given the work item form is open on Описание
  When the member looks at the editor's toolbar
  Then it offers headings (levels 3 and 4 only), bold, italic, bulleted and numbered lists, link, inline code, code block and table
  And it offers nothing beyond that (no image, colour, font, alignment, strike-through or heading 1, 2, 5, 6)
  And every control has the Russian accessible name from the table above

Scenario: Lists nest by buttons, not by Tab
  Given the cursor is in a list item
  When the member activates «Увеличить отступ списка» and then «Уменьшить отступ списка»
  Then the item is nested one level (up to three levels) and un-nested
  And pressing Tab in the list item moves focus out of the editor and does not indent

Scenario: Table operations are bounded
  Given the cursor is in the editor
  When the member activates «Таблица»
  Then a 3 by 3 table with a header row is inserted
  And inside it the member can add or delete a row or a column, toggle the header row and delete the table
  And there is no merge or split of cells

Scenario: Formatting survives save and reopen
  Given Описание with a heading, a bulleted list, a link, bold text, a code block and a table typed in the editor
  When the member saves and reopens the work item
  Then the editor shows the same heading, list, link, bold text, code block and table

Scenario: Keyboard shortcuts
  Given the cursor is in the editor with text selected
  When the member presses Ctrl+B (Cmd+B on macOS)
  Then the selection becomes bold
  And Ctrl+I (Cmd+I) makes it italic
  And Ctrl+K (Cmd+K) opens the link prompt and, on «Применить», makes the selection a link

Scenario: Paste is reduced to the subset
  Given rich content copied from a web page holding a heading, a coloured paragraph and an image
  When the member pastes it into the editor
  Then the heading is kept (a level 1 or 2 becomes level 3, a 4 to 6 becomes 4), the colour is gone, and the image is dropped
  And pasting plain text inserts it as paragraphs, line breaks kept

Scenario: Images and files are not inserted
  Given an image file, or an image from another page, dragged onto the editor, or a file pasted from the clipboard
  When it is dropped or pasted
  Then nothing is inserted, the page does not navigate to the file
  And «Изображения в описание не вставляются.» is announced under the editor

Scenario: A link with an unsupported address is not accepted
  Given the link prompt
  When the member enters "javascript:alert(1)" (or "/relative", "//evil.example")
  Then no link is created and the prompt shows «Допустимы адреса, начинающиеся с http://, https:// или mailto:.»

Scenario: A link is not followed inside the editor
  Given a link in the description
  When the member clicks it in the editor
  Then the page does not navigate and the cursor is placed in the link
  And «Ссылка» opens the prompt with the address filled in, «Убрать ссылку» removes the link and keeps the text

Scenario: Undo and redo
  Given the member has typed and formatted text
  When the member presses Ctrl+Z, then Ctrl+Y (Cmd+Z, Cmd+Shift+Z), or uses «Отменить», «Повторить»
  Then the last change is reverted, then reapplied

Scenario: The editor is operable by keyboard alone
  Given the work item form
  When the member moves through the form with Tab and Shift+Tab
  Then focus enters the editor and leaves it again, Tab is not captured as indentation
  And the toolbar is reachable and its controls can be activated from the keyboard
  And the editor has the accessible name «Описание»

Scenario: Over the visible limit the client refuses as the server does
  Given Описание whose visible text counted by BR-RT-8 is 100 001 code units
  When the member presses Save
  Then nothing is sent and «Не более 100 000 символов.» is shown under the editor
  And a text of exactly 100 000 is sent

Scenario: Over the markup ceiling the client refuses
  Given Описание whose normalised canonical markup is 1 000 001 code units while its visible text is under 100 000 (a table of many one-character cells)
  When the member presses Save
  Then nothing is sent and «Слишком много разметки. Упростите форматирование или сократите текст.» is shown under the editor
  And when both limits are exceeded that message is the one shown (the server's precedence)

Scenario: A save refused by the server keeps the member's input
  Given a description the server refuses (US-4)
  When the save fails
  Then the editor still holds what the member typed and the reason is shown under it

Scenario: After a save the editor shows what the server stored
  Given the member saved a description the server reduced (a link outside the allow-list was removed)
  When the save succeeds
  Then the editor holds the server's returned value, not the draft
  And the form is not dirty

Scenario: The editor is disabled while a save is in flight
  Given a save in progress
  Then the editor and its toolbar are not editable and show the same content
```

## Verification

No E2E (the human's instruction). Levels: **U** unit, **C** jsdom component
test with the real editor (keyboard and paste events dispatched, clipboard and
drop data stubbed), **I** API integration test, **M** manual QA in a real
browser, recorded in the QA report.

| Scenario | Automated | Not automatable (manual) |
|---|---|---|
| Toolbar subset, Russian names | C (roles and names) | visual theming in MUI tokens: M |
| Nested lists, Tab | C (focus order with `userEvent.tab`) | — |
| Table operations | C | — |
| Survives save and reopen | C (editor load/serialise) + I (PATCH then GET) | — |
| Shortcuts | C (keydown on the editor, Ctrl) | macOS Cmd: M; real browser shortcut collisions: M |
| Paste reduced | C (stubbed `clipboardData` with HTML and text) | a real copy from a real web page: M |
| Images and files | C (stubbed drop/paste `files`) | the browser's own navigate-to-file default: M |
| Unsupported link | C | — |
| Link not followed | C (click, no navigation) | — |
| Undo/redo | C (history keymap) | — |
| Keyboard only | C | screen-reader announcement of the live region and the toolbar: M |
| Client limits (visible, markup ceiling, precedence) | U (shared fixtures with the server, counted on the normalised string) + C | — |
| Refused save keeps input; server value after save; disabled while saving | C (mocked API) | — |
| Editor opening speed with 100 000 characters | — | M, observation only (NFR Speed) |
