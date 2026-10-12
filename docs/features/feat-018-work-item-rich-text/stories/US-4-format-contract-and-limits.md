# US-4: The API tells plain text from HTML, and bounds the size

- story: US-4
- depends-on: -
- shippable: no — a format contract with no editor (US-1) and no reduction (US-2) carries nothing
- status: pending

As a **client of the work item API**, I want **a description's format to be
declared in the request and reported in the response, and its size bounded
by what a person sees**, so that **an existing client keeps working and a
description can never be misread as the wrong format**.

BR-RT-6, BR-RT-7, BR-RT-8, BR-RT-10, BR-RT-13.

## Acceptance criteria

```gherkin
Scenario: A client that declares nothing is unchanged
  Given a save request with description "a < b <b>x</b>" and no format declaration
  When it is saved and the work item is read
  Then the description is returned exactly "a < b <b>x</b>" with format plain
  And a plain description over 100 000 code units is refused with description_too_long as before

Scenario: A declared html description is reduced and reported
  Given a save request with format html and description "<p>a</p><script>x</script>"
  When it is saved and read
  Then the returned description is "<p>a</p>" with format html

Scenario: A never-written or cleared description is plain and empty
  Given a work item whose Описание was never set, and another cleared by an html client
  When each is read
  Then both return "" with format plain, never null

Scenario: An unknown format is refused
  Given a save request with format "markdown"
  When it is saved
  Then it is refused with a field reason on the description and nothing is stored

Scenario: A format with no description changes nothing
  Given a save request with a format declaration and no description
  When it is saved
  Then the stored description and its format are unchanged

Scenario: A plain write over an html description is honoured
  Given an html description and a save request, no declaration, description "text"
  When it is saved
  Then it is stored as plain "text", exactly as sent

Scenario: Visible text is counted by BR-RT-8
  Given html values and expected counts: "<p>a&amp;b</p>" is 3; "<p>a</p><p>b</p>" is 3; "<ul><li>a</li><li>b</li></ul>" is 3; "<p>a<br>b</p>" is 3; "<table><tbody><tr><td>a</td><td>b</td></tr></tbody></table>" is 3; "<p>😀</p>" is 2
  When each is counted by the server and by the client
  Then both return the expected count (one shared fixture file)

Scenario: The visible limit is enforced on the server
  Given an html description whose visible text is 100 001 code units
  When it is saved
  Then it is refused with workitems.workitem.description_too_long and the stored description is unchanged

Scenario: Exactly the limit is accepted
  Given an html description whose visible text is exactly 100 000 code units
  When it is saved
  Then it is stored and read back whole

Scenario: Markup does not count against the visible limit
  Given an html description of 99 000 visible characters carrying bold and links, its markup longer than 100 000
  When it is saved
  Then it is stored

Scenario: The markup ceiling has its own reason
  Given an html description of 50 000 visible characters whose reduced markup is 1 000 001 code units
  When it is saved
  Then it is refused with workitems.workitem.description_markup_too_large, «Слишком много разметки. Упростите форматирование или сократите текст.», and nothing is stored
  And the reason is not description_too_long

Scenario: An oversized request is refused before parsing
  Given a request description of 2 000 001 code units
  When it is saved
  Then it is refused with description_markup_too_large without being parsed

Scenario: Legacy worst cases fit the ceiling
  Given html converted from 100 000 "&" (500 000 code units) and from 100 000 line feeds (700 007 code units)
  When each is saved
  Then both are stored

Scenario: Existing rows are unchanged by the release
  Given work items with plain descriptions before the migration
  When the migration that widens the stored bound has run
  Then every description and its format are byte-for-byte what they were
  And a description of exactly 100 000 plain code units still reads back whole

Scenario: Response time holds
  Given a work item with a 100 000-character visible html description
  When it is read and saved
  Then each answers within 1 second (2 seconds for a 1 000 000-code-unit save)
```

## Verification

U for counting and limits (the fixture files are shared with the client
suite, US-1 client limit), I for every API scenario and the migration
(existing rows seeded before the migration is applied), I with a timing
assertion for the response-time scenario on CI-class hardware. Nothing needs
a browser. The migration's down step is not scenario-tested beyond the note
that it fails once a row exceeds 100 000 (NFR Rollback; owned by the ADR's
runbook).
