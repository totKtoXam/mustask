# US-6: Tables and code blocks

- story: US-6
- depends-on: US-1, US-7
- shippable: no — tables and code the server does not canonicalise (US-7) are lost or stored unfiltered
- status: pending

As a **member editing a description**, I want **proper tables and proper code
display and editing**, so that **steps, data and snippets stay readable and
can be copied**.

BR-RT-16, BR-RT-17, BR-RT-5, BR-RT-6, BR-RT-8.

## Interface copy (Russian)

| Where | Text |
|---|---|
| Table controls | «Таблица», «Добавить строку выше», «Добавить строку ниже», «Добавить столбец слева», «Добавить столбец справа», «Удалить строку», «Удалить столбец», «Строка заголовка», «Удалить таблицу» |
| Code controls | «Блок кода», «Язык кода», «Копировать код»; status «Код скопирован» |
| Languages | «Простой текст», «JavaScript», «TypeScript», «JSON», «HTML», «XML», «CSS», «SQL», «C#», «Python», «Bash», «YAML», «Markdown» |
| Hint on entering a code block | «Tab вставляет отступ. Esc, затем Tab — выйти из блока.» (live region) |
| Hint on entering a table | «Tab переходит между ячейками. Esc, затем Tab — выйти из таблицы.» (live region) |

## Acceptance criteria

```gherkin
Scenario: Insert a table and edit it in every direction
  Given the cursor in the editor
  When the member activates «Таблица»
  Then a 3 by 3 table with a header row is inserted and the cursor is in its first cell
  And from a cell the member can add a row above and below, a column left and right, delete the row, delete the column, toggle «Строка заголовка» and delete the table
  And there is no merge, split, column resize or width control

Scenario: Tab moves between cells
  Given the cursor in a table cell
  When the member presses Tab, then Shift+Tab
  Then the cursor moves to the next cell, then back
  And Tab in the last cell, or Shift+Tab in the first, moves focus out of the editor and adds no row
  And entering the table announces «Tab переходит между ячейками. Esc, затем Tab — выйти из таблицы.»
  And Esc then Tab, pressed in any cell, moves focus out of the editor forward (Esc then Shift+Tab backward)
  And Esc followed by any other key does nothing special, and an Esc that closed a picker or prompt does not arm the exit

Scenario: A pasted table is reduced
  Given an HTML table copied from a web page with merged cells, column widths and a list in one cell
  When the member pastes it
  Then it becomes a rectangular table without colspan, rowspan or widths: a merged cell's content sits in its top-left position and the positions it covered are empty cells, short rows are padded, the list is paragraphs
  And pasting inside a table cell inserts the pasted cells' text as paragraphs, no table nests

Scenario: A spreadsheet range becomes a table only on clear evidence
  Given a range copied from a spreadsheet application (the clipboard carries an html table), or plain text of at least 3 lines that all have the same number, at least 1, of tabs
  When the member pastes it outside a table and a code block
  Then it becomes a table without a header row, one cell per value, and Ctrl+Z removes it whole
  And plain text with tabs of differing counts, or of fewer than 3 lines, is inserted as paragraphs, tabs kept
  And Ctrl+Shift+V (Cmd+Shift+V) pastes any of these as plain paragraphs, no table

Scenario: A table can be left from its last row
  Given a table as the last block
  When the member presses Ctrl+Enter (Cmd+Enter) in a cell
  Then the cursor is after the table, with no paragraph stored until the member types there
  And when a block follows the table the cursor goes to its start

Scenario: Leaving a final table or code block never changes an untouched value
  Given a description ending with a table and one ending with a code block, both loaded untouched
  When the member moves the cursor into and out of the last block by keyboard and focus
  Then the serialised value equals the stored one, the form is not dirty and no trailing paragraph appeared

Scenario: Insert a code block with a language
  Given the cursor in a paragraph with text
  When the member activates «Блок кода» and picks «Python» in «Язык кода»
  Then the text is a monospace block with that language, whitespace and tabs kept
  And «Язык кода» offers exactly «Простой текст» and the 12 languages of BR-RT-16

Scenario: Tab indents inside code and Esc leaves
  Given the cursor in a code block
  When the member presses Tab, then Shift+Tab, then Esc and Tab
  Then a tab character is inserted, then one leading level is removed (one tab, else up to four leading spaces, on each selected line), then focus leaves the block and the editor
  And entering the block announces «Tab вставляет отступ. Esc, затем Tab — выйти из блока.»

Scenario: The code block can be left in the document
  Given the cursor at the end of a code block
  When the member presses Enter twice (or Ctrl+Enter, Cmd+Enter, or ArrowDown at the end of its text)
  Then the block ends without the two trailing line feeds and the cursor is after it (at the next block's start, or after the document's end without a stored paragraph until the member types)

Scenario: Copy the code
  Given a code block of three lines with indentation
  When the member activates «Копировать код»
  Then the clipboard holds those lines exactly, indentation included, and «Код скопирован» is announced
  And the button is not part of the stored value

Scenario: Code from an IDE or a web page is reduced to a block
  Given code copied from an IDE (html with a monospace, pre-wrap styled element and a span per token) or from a web page's pre element
  When the member pastes it into the editor
  Then it becomes one code block holding the plain text with its indentation and line breaks, tokens' styles gone
  And a language class of the pasted element is kept only when it is in the closed list, else the block is plain
  And pasted plain multi-line text inside a code block is inserted verbatim, tabs kept, and pasted html there is inserted as its text

Scenario: Nothing in code executes
  Given a code block holding "<script>alert(1)</script>" and "<img src=x onerror=alert(1)>"
  When it is saved, reopened and shown
  Then the text is shown as text, no script runs, no request is made, and the stored value escapes it
  And no marks (bold, colour, link) can be applied inside the block
```

## Verification

| Scenario | Automated | Not automatable (manual) |
|---|---|---|
| Table operations in every direction, header toggle, no merge/resize | C | pointer-based column resize absent: M (visual) |
| Tab between cells; exit at ends; Esc then Tab arming (one keystroke, not after a picker's Esc) | C (`userEvent.tab`, keydown) | the live-region announcement by a screen reader: M |
| Pasted HTML table, rectangular result; nesting; list in cell | C (stubbed `clipboardData`) + U (the reduction, US-7) | a real copy from a spreadsheet application: M |
| Spreadsheet evidence rule, ragged and short tab text, Ctrl+Shift+V | C | — |
| Table exit by Ctrl/Cmd+Enter; untouched final block unchanged | C | exit by ArrowDown from the last row (vertical motion needs layout): M |
| Code block, language list, Tab/Shift+Tab (tab, spaces), Esc, exit by Enter twice / Ctrl+Enter / ArrowDown at end of text | C | ArrowDown on a wrapped last line (layout): M; screen-reader announcement: M |
| Copy button | C (stubbed `navigator.clipboard`) | the real clipboard permission prompt: M |
| IDE paste | C (fixture HTML of a VS Code-style copy, a plain `pre`) | a real IDE's clipboard: M |
| Nothing executes | C (load, assert no script/handler in the document) + I (stored value) | — |
