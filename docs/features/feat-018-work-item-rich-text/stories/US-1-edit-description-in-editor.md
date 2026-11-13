# US-1: A member edits Описание in a WYSIWYG editor: core, lists, paste, limits

- story: US-1
- depends-on: -
- shippable: no — the editor without the server reduction (US-2, US-7) and the format contract (US-4) puts unfiltered markup in the store
- status: pending

As a **member who may edit a work item**, I want **a toolbar and keyboard
shortcuts that format Описание and show the result as I type, and a safe save
and paste**, so that **a description can carry headings and lists instead of a
flat wall of text**.

BR-RT-1, BR-RT-4, BR-RT-7, BR-RT-8, BR-RT-12, BR-RT-20. Marks, palettes, emoji
and links are US-5, tables and code US-6.

## Interface copy (Russian)

| Where | Text |
|---|---|
| Toolbar group (`role=toolbar`) | «Форматирование описания» |
| Controls of this story | «Заголовок 3», «Заголовок 4», «Полужирный», «Курсив», «Маркированный список», «Нумерованный список», «Увеличить отступ списка», «Уменьшить отступ списка», «Отменить», «Повторить» |
| Controls of US-5 and US-6 | quoted in their copy tables |
| Placeholder / helper (kept from mod-003) | «Добавьте описание» / «До 100 000 символов» |
| Image or file dropped / pasted | «Изображения в описание не вставляются.» (polite live region under the editor) |
| Over the visible limit | «Не более 100 000 символов.» (as the server's) |
| Over the markup ceiling | «Слишком много разметки. Упростите форматирование или сократите текст.» |

## Acceptance criteria

```gherkin
Scenario: The toolbar offers the subset and nothing beyond
  Given the work item form is open on Описание
  When the member looks at the editor's toolbar
  Then it offers headings (levels 3 and 4 only), bold, italic, underline, strikethrough, lists with indent and outdent, highlight, colour, size, inline code, clear formatting, emoji, link, code block, table and undo/redo
  And it offers nothing beyond that (no image, mention, alignment, free colour input, heading 1, 2, 5, 6)
  And every control has a Russian accessible name from the copy tables

Scenario: Lists nest by buttons, not by Tab
  Given the cursor is in a list item
  When the member activates «Увеличить отступ списка» (or presses Ctrl+], Cmd+] on macOS) and then «Уменьшить отступ списка» (Ctrl+[)
  Then the item is nested one level (up to three levels) and un-nested
  And pressing Tab in the list item moves focus out of the editor and does not indent
  And at the third level «Увеличить отступ списка» is disabled

Scenario: Formatting survives save and reopen
  Given Описание with a heading, a bulleted list, bold, coloured and highlighted text, a link, a code block and a table typed in the editor
  When the member saves and reopens the work item
  Then the editor shows the same of each

Scenario: Keyboard shortcuts
  Given the cursor is in the editor with text selected
  When the member presses Ctrl+B (Cmd+B on macOS)
  Then the selection becomes bold
  And Ctrl+I (Cmd+I) makes it italic
  And Ctrl+Z, then Ctrl+Y (Cmd+Z, Cmd+Shift+Z) revert, then reapply the last change

Scenario: Undo and redo by buttons
  Given the member has typed and formatted text
  When the member uses «Отменить», then «Повторить»
  Then the last change is reverted, then reapplied

Scenario: Paste is reduced to the subset
  Given rich content copied from a web page holding a heading 1, bold and underlined text, a paragraph with a red colour and font-size 30px, and an image
  When the member pastes it into the editor
  Then the heading is kept as level 3 (levels 4 to 6 become 4), bold and underline are kept
  And the red colour and the font size are gone (never mapped to the palette) and the image is dropped
  And pasting plain text inserts it as paragraphs, line breaks kept, except a tab-separated block that US-6 turns into a table
  And Ctrl+Shift+V (Cmd+Shift+V) always pastes the clipboard's text as plain paragraphs, no table, no link, no formatting

Scenario: Bold, italic, underline and strikethrough are recognised from pasted styles
  Given pasted html "<b style='font-weight:normal'><span style='font-weight:700'>a</span> b <span style='font-style:italic'>c</span> <span style='text-decoration:underline'>d</span> <span style='text-decoration:line-through'>e</span> <span style='font-weight:400'>f</span></b>"
  When the member pastes it
  Then a is bold, c italic, d underlined, e struck through
  And b and f are plain: the wrapper declaring font-weight normal does not make the whole paste bold
  And every other style is gone

Scenario: Images and files are not inserted
  Given an image file, or an image from another page, dragged onto the editor, or a file pasted from the clipboard
  When it is dropped or pasted
  Then nothing is inserted, the page does not navigate to the file
  And «Изображения в описание не вставляются.» is announced under the editor

Scenario: The editor is operable by keyboard alone
  Given the work item form
  When the member moves through the form with Tab and Shift+Tab
  Then focus enters the editor and leaves it again, Tab is not captured as indentation
  And the toolbar is reachable and its controls, including the palette and emoji pickers, can be activated from the keyboard
  And the editor has the accessible name «Описание»
  And the only two exceptions are US-6's, each announced on entry and each left by Esc then Tab: Tab inserts an indent inside a code block, and Tab moves between the cells of a table

Scenario: Over the visible limit the client refuses as the server does
  Given Описание whose visible text counted by BR-RT-8 is 100 001 code points
  When the member presses Save
  Then nothing is sent and «Не более 100 000 символов.» is shown under the editor
  And a text of exactly 100 000 is sent

Scenario: Over the markup ceiling the client refuses
  Given Описание whose normalised canonical markup is 1 000 001 code points while its visible text is under 100 000 (a table of many one-character cells)
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
| Toolbar subset, Russian names | C (roles and names) | visual theming in MUI tokens, light and dark: M |
| Nested lists, Tab, shortcuts [ ] | C (focus order with `userEvent.tab`, keydown) | macOS Cmd: M |
| Survives save and reopen | C (editor load/serialise) + I (PATCH then GET) | — |
| Shortcuts, undo/redo | C (keydown on the editor, history keymap) | real browser shortcut collisions: M |
| Paste reduced; pasted styles and the `font-weight:normal` wrapper; Ctrl+Shift+V | C (stubbed `clipboardData` with HTML and text) | a real copy from a real web page or an office web editor: M |
| Images and files | C (stubbed drop/paste `files`) | the browser's own navigate-to-file default: M |
| Keyboard only | C | screen-reader announcement of the live region and the toolbar: M |
| Client limits (visible, markup ceiling, precedence) | U (shared fixtures with the server, counted on the normalised string) + C | — |
| Refused save keeps input; server value after save; disabled while saving | C (mocked API) | — |
| Editor opening speed with 100 000 characters | — | M, observation only (NFR Speed) |
