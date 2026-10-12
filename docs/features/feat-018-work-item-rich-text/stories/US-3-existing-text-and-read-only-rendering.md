# US-3: Descriptions written before this task keep their text, and the subset renders read-only

- story: US-3
- depends-on: US-1, US-2
- shippable: no — it is the other half of US-1 and US-2: without it the new editor mangles what exists
- status: pending

As a **member**, I want **every description written before this task to look
the same and to stay safe, and a formatted description to be readable
wherever it is shown**, so that **turning on rich text loses nothing and
breaks nothing**.

BR-RT-5, BR-RT-8, BR-RT-9. How the conversion is carried out is OQ-2; the
scenarios hold on every option.

## Acceptance criteria

```gherkin
Scenario: Legacy text shows the same visible text and line breaks
  Given a work item whose description was saved as plain text with three lines and two blank lines
  When the member opens it
  Then the editor shows the same three lines and two blank lines, character for character

Scenario: Markup characters in legacy text are not interpreted
  Given a legacy description "<b>жирный</b> & <script>alert(1)</script>"
  When the member opens it
  Then the editor shows exactly those characters as text, nothing is bold, and no script runs

Scenario: Legacy text of any length still saves
  Given a legacy description of exactly 100 000 characters containing "<", ">" and "&"
  When the member opens the work item and saves it without changes
  Then the save succeeds (OQ-3)

Scenario: Opening without saving changes nothing
  Given a legacy description
  When the member opens the work item and leaves it without saving
  Then the stored description is byte-for-byte what it was

Scenario: First save moves a legacy description to rich text
  Given a legacy description the member edits and formats in the editor
  When the member saves
  Then the formatting is stored and reopened as in US-1
  And the unchanged lines show the same text as before

Scenario: Other readers of the stored value are not broken
  Given a work item list or another read that already returns the work item without the description
  When this task ships
  Then those reads behave as before, and any reader that returns the description returns it in a form that shows the same text

Scenario: Read-only rendering uses the same subset
  Given a formatted description shown anywhere outside the editor
  When it is displayed
  Then headings, lists, links, code and tables are rendered
  And links open safely (no access to the opening window)
  And a stored value carrying markup outside the subset is displayed without it
```
