# US-1: A member edits Описание in a WYSIWYG editor

- story: US-1
- depends-on: -
- shippable: no — the editor without the server reduction (US-2) puts unfiltered markup in the store
- status: pending

As a **member who may edit a work item**, I want **to format Описание with a
toolbar and keyboard shortcuts and see the result as I type**, so that **a
description can carry headings, lists, links and code instead of a flat wall
of text**.

BR-RT-1, BR-RT-4.

## Acceptance criteria

```gherkin
Scenario: The toolbar offers exactly the safe subset
  Given the work item form is open on Описание
  When the member looks at the editor's toolbar
  Then it offers headings, bold, italic, bulleted and numbered lists, link, inline code, code block and table
  And it offers nothing beyond that (no image, colour, font or alignment control)
  And every control has a Russian accessible name

Scenario: Formatting survives save and reopen
  Given Описание with a heading, a bulleted list, a link, bold text and a code block typed in the editor
  When the member saves and reopens the work item
  Then the editor shows the same heading, list, link, bold text and code block

Scenario: Keyboard shortcuts
  Given the cursor is in the editor with text selected
  When the member presses Ctrl+B (Cmd+B on macOS)
  Then the selection becomes bold
  And Ctrl+I (Cmd+I) makes it italic
  And Ctrl+K (Cmd+K) asks for a link address and, on confirmation, makes the selection a link

Scenario: Paste is reduced to the subset
  Given rich content copied from a web page holding a heading, a coloured paragraph and an image
  When the member pastes it into the editor
  Then the heading is kept, the colour is gone, and the image is dropped
  And pasting plain text inserts it as paragraphs, line breaks kept

Scenario: A link with an unsupported address is not accepted
  Given the link prompt
  When the member enters "javascript:alert(1)"
  Then no link is created and the member is told which addresses are accepted (http, https, mailto)

Scenario: Undo and redo
  Given the member has typed and formatted text
  When the member presses Ctrl+Z, then Ctrl+Y (Cmd+Z, Cmd+Shift+Z)
  Then the last change is reverted, then reapplied

Scenario: The editor is operable by keyboard alone
  Given the work item form
  When the member moves through the form with Tab and Shift+Tab
  Then focus enters the editor and leaves it again, Tab is not captured as indentation
  And the toolbar is reachable and its controls can be activated from the keyboard
  And the editor has the accessible name «Описание»

Scenario: A save refused by the server keeps the member's input
  Given a description the server refuses (US-2)
  When the save fails
  Then the editor still holds what the member typed and the reason is shown under it
```
