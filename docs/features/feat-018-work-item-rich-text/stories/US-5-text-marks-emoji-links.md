# US-5: Text marks, palettes, clear formatting, emoji and links

- story: US-5
- depends-on: US-1, US-7
- shippable: no — marks and links the server does not canonicalise (US-7) would be stripped or stored unfiltered
- status: pending

As a **member editing a description**, I want **underline, strikethrough,
highlight, text colour, font size, inline code, clear formatting, emoji and
links**, so that **I can emphasise and link without leaving the editor, as in
the reference tracker's editor**.

BR-RT-14, BR-RT-15, BR-RT-18, BR-RT-19, BR-RT-20, BR-RT-3.

## Interface copy (Russian)

| Where | Text |
|---|---|
| Mark controls | «Подчёркнутый», «Зачёркнутый», «Код в строке», «Очистить форматирование» |
| Pickers | «Цвет текста», «Выделение цветом», «Размер шрифта», «Эмодзи»; search field «Поиск эмодзи» |
| Text colours (8) | «Серый», «Красный», «Оранжевый», «Зелёный», «Бирюзовый», «Синий», «Фиолетовый», «Розовый»; reset «Обычный цвет» |
| Highlights (8) | «Жёлтый», «Оранжевый», «Красный», «Зелёный», «Бирюзовый», «Синий», «Фиолетовый», «Серый»; reset «Без выделения» |
| Sizes (4) | «Очень мелкий», «Мелкий», «Крупный», «Очень крупный»; reset «Обычный размер» |
| Link controls | «Ссылка», «Убрать ссылку», «Открыть в новой вкладке» |
| Link prompt | title «Ссылка», fields «Адрес ссылки» (hint «https://…») and «Текст ссылки» (shown when nothing is selected), buttons «Применить», «Отмена» |
| Unsupported link address | «Допустимы адреса, начинающиеся с http://, https:// или mailto:.» |

## Acceptance criteria

```gherkin
Scenario: Underline and strikethrough
  Given text selected in the editor
  When the member activates «Подчёркнутый» (or Ctrl+U, Cmd+U) and then «Зачёркнутый»
  Then the text is underlined and struck through, and activating again removes each

Scenario: Highlight, colour and size come from closed palettes
  Given text selected in the editor
  When the member opens «Выделение цветом», «Цвет текста» and «Размер шрифта»
  Then each offers exactly its named choices of BR-RT-14 and its reset, and nothing free-form (no hex field, no colour wheel, no number input)
  And picking one applies it to the selection, and the reset removes it

Scenario: Marks combine and keep the canonical order
  Given a selection made bold, italic, underlined, highlighted, coloured and large
  When the member saves and reopens
  Then all six are shown, and the stored nesting is link, bold, italic, underline, strikethrough, highlight, colour, size

Scenario: Inline code carries no other mark
  Given a selection that is bold and coloured
  When the member activates «Код в строке»
  Then it is monospace and neither bold nor coloured
  And inside inline code the bold, colour, highlight and size controls are disabled

Scenario: A heading takes no size
  Given the cursor is in a level 3 heading
  Then «Размер шрифта» is disabled, and bold, colour and highlight remain available

Scenario: Clear formatting
  Given a selection with bold, underline, highlight, colour, size and inline code, in a level 4 heading, containing a link
  When the member activates «Очистить форматирование»
  Then all those marks are gone, the heading is a paragraph, the link stays
  And the same on a list item or a table cell leaves the list and the table in place

Scenario: Emoji are inserted as characters
  Given the cursor is in the editor
  When the member opens «Эмодзи», finds a face by «Поиск эмодзи» with the keyboard and activates it
  Then the character is inserted at the cursor, the picker closes and focus returns to the editor
  And the stored value holds the Unicode character, no image element
  And a 😀 counts 1 toward the 100 000 limit (BR-RT-8)

Scenario: Add a link
  Given a selection "документация"
  When the member presses Ctrl+K (Cmd+K), enters "https://example.org" and activates «Применить»
  Then the selection is a link; with nothing selected the prompt also asks «Текст ссылки» and inserts that text as the link

Scenario: Edit and remove a link
  Given the cursor in a link
  When the member activates «Ссылка», changes the address and applies, then activates «Убрать ссылку»
  Then the address changed, then the link is gone and its text stays

Scenario: A link with an unsupported address is not accepted
  Given the link prompt
  When the member enters "javascript:alert(1)" (or "/relative", "//evil.example")
  Then no link is created and the prompt shows «Допустимы адреса, начинающиеся с http://, https:// или mailto:.»

Scenario: A link is opened on purpose, not by a click
  Given a link in the description
  When the member clicks it, then activates «Открыть в новой вкладке» (or Ctrl+click, Cmd+click)
  Then the plain click only places the cursor and navigates nowhere
  And the explicit control opens the address in a new tab with noopener and noreferrer, the page keeps its state

Scenario: A typed or pasted address becomes a link
  Given the cursor in a paragraph
  When the member types "https://example.org " (a space or Enter after it), or pastes "www.example.org", or pastes "mailto:a@example.org"
  Then each becomes a link (the www form to "https://www.example.org")
  And Ctrl+Z turns the last one back into plain text
  And "javascript:alert(1)" typed or pasted stays plain text

Scenario: A legacy text is not auto-linked on open
  Given a legacy description holding "https://example.org"
  When the member opens the work item
  Then it shows as text and the form is not dirty
```

## Verification

| Scenario | Automated | Not automatable (manual) |
|---|---|---|
| Underline, strikethrough, combined marks, code, heading size | C (toolbar state, serialised html after normalisation) | macOS Cmd+U: M |
| Palettes closed and named; no free-form control | C (roles and options) | contrast of mapped colours in light and dark: M (design.md) |
| Clear formatting | C | — |
| Emoji picker, count | C (keyboard) + U (counting fixture) | the picker's visual rendering of colour emoji on each OS: M |
| Link add, edit, remove, refusal | C | — |
| Open in new tab | C (assert the opener call has `noopener,noreferrer`, no navigation) | the browser actually opening a tab: M |
| Auto-link typed and pasted, undo | C (input rules; paste event) | — |
| Legacy not auto-linked | C | — |
