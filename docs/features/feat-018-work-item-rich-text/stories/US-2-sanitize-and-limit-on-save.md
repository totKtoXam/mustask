# US-2: The server reduces a saved description to the safe subset and enforces the limit

- story: US-2
- depends-on: -
- shippable: no — with no editor (US-1) nothing in the product produces rich text to protect
- status: pending

As a **member reading a colleague's work item**, I want **a description never
to carry anything executable, whatever client wrote it**, so that **opening a
work item is safe**.

BR-RT-2, BR-RT-3, BR-RT-4, BR-RT-5, BR-RT-6, BR-RT-7. Limit scenarios assume
OQ-3 (c).

## Acceptance criteria

```gherkin
Scenario: Formatting inside the subset is kept
  Given a save request whose Описание has headings, bold, italic, lists, links, inline code, a code block and a table
  When it is saved and the work item is read
  Then every one of these is present in the returned description

Scenario: Executable and presentational markup is stripped
  Given a save request whose Описание contains a script element, an iframe, a style element, an onclick attribute and an inline style attribute around ordinary text
  When it is saved
  Then none of them is in the stored description and the ordinary text and its allowed formatting remain

Scenario: Links keep only safe schemes
  Given links with the addresses https://example.org, http://example.org, mailto:a@example.org, javascript:alert(1), data:text/html;base64,AAAA and vbscript:x
  When the description is saved
  Then the first three remain links
  And the other three remain as their visible text without a link

Scenario: Images are dropped
  Given a description with an image element, one with a data: address, and one with an https address
  When it is saved
  Then no image element is stored and the surrounding text remains

Scenario: A hostile corpus does not survive
  Given the corpus of hostile payloads kept with the tests (nested and unclosed tags, mixed case, entity-encoded schemes, SVG and MathML payloads, attribute-injection forms)
  When each is saved
  Then the stored value of each contains no script, no event attribute, no non-allowed scheme and no element outside the subset

Scenario: Reduction is idempotent
  Given a description already saved and read back
  When the member saves it again unchanged
  Then the stored value is identical

Scenario: An empty-looking description is stored as empty
  Given a description holding only empty paragraphs and whitespace
  When it is saved
  Then the stored description is the empty description, as for an untouched field

Scenario: The limit is enforced on the server
  Given a description whose visible text is 100 001 characters
  When it is saved
  Then the save is refused with reason workitems.workitem.description_too_long and the stored description is unchanged

Scenario: Exactly the limit is accepted
  Given a description whose visible text is exactly 100 000 characters, in markup
  When it is saved
  Then it is stored and read back whole

Scenario: Markup does not count against the visible limit
  Given a description of 99 000 visible characters carrying bold and links
  When it is saved
  Then it is stored, though its markup is longer than 100 000 characters
```
