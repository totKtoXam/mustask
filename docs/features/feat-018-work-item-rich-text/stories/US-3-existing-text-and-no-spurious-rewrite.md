# US-3: Existing descriptions keep their text and an unchanged Описание is never rewritten

- story: US-3
- depends-on: US-1, US-4
- shippable: no — it is the other half of US-1 and US-4: without it the new editor mangles what exists
- status: pending

As a **member**, I want **every description written before this task to look
the same, and a description I did not touch to stay exactly as it was**, so
that **turning on rich text loses nothing and rewrites nothing behind my
back**.

BR-RT-5, BR-RT-8, BR-RT-9, BR-RT-11, BR-RT-12. The conversion is lazy (D-3):
the stored plain value is never rewritten by reading.

## Acceptance criteria

```gherkin
Scenario: Legacy text shows the same lines
  Given a work item whose description was saved as plain text with three lines and two blank lines
  When the member opens it
  Then the editor shows five paragraphs: the same three lines and two empty ones, character for character

Scenario: Indentation and runs of spaces are kept
  Given a legacy description "  шаг 1:\n\t- a   b\n    код"
  When the member opens it
  Then the editor shows the leading spaces, the tab and the three-space gap as they were, not collapsed

Scenario: CR LF is accepted as a difference
  Given a legacy description "a\r\nb"
  When the member opens it
  Then the editor shows two paragraphs "a" and "b"

Scenario: Markup characters in legacy text are not interpreted
  Given a legacy description "<b>жирный</b> & <script>alert(1)</script>"
  When the member opens it
  Then the editor shows exactly those characters as text, nothing is bold, and no script runs

Scenario: Legacy text of any length still saves
  Given a legacy description of exactly 100 000 characters of "&", and another of 100 000 line feeds
  When the member opens the work item, adds one character and saves
  Then both saves succeed (the stored markup is 500 000 and 700 007 code units, within the ceiling of US-4)

Scenario: Opening does not make the form dirty
  Given a legacy description, and a formatted description with a table and nested lists
  When the member opens the work item
  Then the Save button is disabled and a leave-guard does not fire on leaving

Scenario: Saving another field leaves Описание untouched
  Given a legacy description and a formatted description (two work items)
  When the member changes only «Статус» and saves
  Then the save request carries no description
  And the stored description of each is byte-for-byte what it was, still in its own format

Scenario: First save of a legacy description is the first save that changes it
  Given a legacy description the member edits and formats in the editor
  When the member saves
  Then the formatting is stored as html and reopened as in US-1
  And the unchanged lines show the same text as before

Scenario: A legacy description of blank lines only
  Given a legacy description of three blank lines
  When the member opens it
  Then the editor shows three empty paragraphs and a save of another field leaves it as stored
  And when the member edits it so that no non-whitespace remains, it is stored as ""

Scenario: Editor and server agree on the canonical form
  Given each canonical value of the shared fixture file (US-2)
  When it is loaded in the editor and serialised without edits
  Then the string is the canonical value itself

Scenario: Stored markup outside the subset is not rendered
  Given a stored html description holding a script element, an onclick attribute, an image and a javascript: link (as if written by another path)
  When the editor loads it, also disabled during a save
  Then the editor's document contains none of them, nothing executes, and the page never assigns the value as raw html

Scenario: Other reads are not broken
  Given the work item list, board and backlog, which return no description
  When this task ships
  Then they respond as before
  And a plain-format client reading a plain description gets the same string it stored (US-4)
```

## Verification

| Scenario | Automated | Not automatable (manual) |
|---|---|---|
| Legacy conversion: lines, whitespace, CR LF, markup characters, blank lines | U (the conversion function) + C (editor shows it) | looking at a populated database copy: M, once, at QA |
| Of any length | U (count and size arithmetic) + I (PATCH at those sizes) | — |
| Not dirty on open; another field leaves it untouched | C (button state, request body) + I (PATCH without `description`, GET) | — |
| First save; blank lines only | C + I | — |
| Canonical agreement | C (client) + U (server), same fixture file | — |
| Outside the subset not rendered | C (load hostile value, inspect document); lint rule `react/no-danger` in the verify run | real-browser mXSS probe: M |
| Other reads | I (existing list tests pass unchanged) | — |
