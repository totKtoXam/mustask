# US-2: The server reduces an HTML description to the safe subset

- story: US-2
- depends-on: US-4
- shippable: no — with no editor (US-1) nothing in the product produces rich text to protect
- status: pending

As a **member reading a colleague's work item**, I want **a description never
to carry anything executable, whatever client wrote it**, so that **opening a
work item is safe**.

BR-RT-1..BR-RT-6. Applies to a write that declares format `html` (US-4); the
size limits are US-4's.

## Allow-list

| Element | Attributes kept | Rule |
|---|---|---|
| `p`, `br` | none | `br` only inside a block |
| `h3`, `h4` | none | `h1`, `h2`, `h3` become `h3`; `h4`, `h5`, `h6` become `h4` |
| `strong`, `em` | none | `b` becomes `strong`, `i` becomes `em` |
| `code` | none | inline code; inside `pre` it is the block's content |
| `pre` | none | always `<pre><code>…</code></pre>`; any `class` (language) removed |
| `ul`, `ol`, `li` | none | nested at most 3 levels; deeper list markup is unwrapped, text kept; `start` removed |
| `a` | `href`, `rel` | `rel` is always `noopener noreferrer`; `target` and `title` removed; link rules below |
| `table`, `tbody`, `tr`, `th`, `td` | none | top level only (a table inside a table or a list item is unwrapped); `thead` and `tfoot` rows are moved into `tbody`, header cells stay `th`; `colspan`, `rowspan` (also `="1"`), `style`, `colgroup`, `col` removed |

## Canonical structure

The reduced output is the editor's canonical form (BR-RT-5); the editor's
schema is configured to hold exactly this and no more:

- `li`, `th`, `td` hold only `p` blocks and, in `li`, nested `ul`/`ol`; inline
  content written directly in them is wrapped in `p`; a heading, `pre` or
  table inside them is unwrapped to `p`, text kept.
- Inside inline `code` no other mark survives (`strong`, `em`, `a` are
  unwrapped, text kept). Elsewhere marks nest in one fixed order: `a`
  outermost, then `strong`, then `em`; adjacent equal marks merge; empty
  inline elements are removed.
- A `br` inside a heading is kept; inside `pre` it becomes a line feed in the
  text.
- An empty block is `<p></p>`; `ol` has no `start`; cells are never merged.

- **Dropped with their content:** `script`, `style`, `iframe`, `object`,
  `embed`, `template`, `svg`, `math`, `noscript`, `head`, `title`, `link`,
  `meta`, `base`, `form`, `input`, `textarea`, `select`, `button`, `audio`,
  `video`, `canvas`, `frame`, `frameset`, `applet`; comments, processing
  instructions and CDATA; `img` and `hr` (void, nothing to keep).
- **Unwrapped, text kept:** every other element (`div`, `span`, `font`, `u`,
  `s`, `del`, `blockquote`, `sub`, `sup`, `caption`, `section`, custom
  elements, …).
- **Attributes:** none beyond the table; `id`, `name`, `class`, `style`,
  `title`, `data-*`, `aria-*` and every `on*` attribute are removed.
- **Loose text** at the top level, or inline content beside blocks, is wrapped
  in `p`. U+0000 is removed. Text is otherwise kept character for character,
  spaces and tabs included.
- **Link address:** read as a browser reads it — leading and trailing C0
  controls and spaces trimmed, ASCII tab, LF and CR removed anywhere, scheme
  compared case-insensitively after entities are decoded. Kept only if it is
  `http` or `https` with a non-empty host, or `mailto` with a non-empty
  address. Relative (`/x`, `x`), protocol-relative (`//host`), fragment-only
  (`#x`), empty and any other scheme: the link is removed, its text stays. A
  link with no text is removed.

## Acceptance criteria

```gherkin
Scenario: Formatting inside the subset is kept
  Given a save request (format html) whose Описание has headings, bold, italic, nested lists, links, inline code, a code block and a table
  When it is saved and the work item is read
  Then every one of these is present in the returned description

Scenario: Executable and presentational markup is stripped
  Given Описание with a script element, an iframe, a style element, an onclick attribute and an inline style attribute around ordinary text
  When it is saved
  Then none of them is in the stored description
  And the script's, style's and iframe's own content is gone, the ordinary text and its allowed formatting remain

Scenario: Unknown elements are unwrapped, not dropped
  Given "<div><span style='color:red'>красный</span> <u>подчёркнутый</u></div>"
  When it is saved
  Then the stored value is "<p>красный подчёркнутый</p>"

Scenario: Attributes outside the allow-list are removed
  Given "<p id='x' class='y' title='z' data-a='1'>t</p><ol start='5'><li>a</li></ol><td colspan='2'>"
  When it is saved
  Then none of id, class, title, data-a, start, colspan is stored

Scenario: Heading levels are mapped
  Given headings of levels 1 to 6
  When it is saved
  Then levels 1, 2 and 3 are stored as level 3, and levels 4, 5 and 6 as level 4

Scenario: Links keep only absolute safe addresses
  Given links with the addresses https://example.org, http://example.org, mailto:a@example.org, javascript:alert(1), data:text/html;base64,AAAA, vbscript:x, " JaVaScRiPt:x", "java&#9;script:x", "/rel", "//evil.example", "#frag" and ""
  When the description is saved
  Then the first three remain links, with rel="noopener noreferrer" and no target
  And the others remain as their visible text without a link

Scenario: Images are dropped
  Given a description with an image element, one with a data: address, one with an https address, and an image inside a link
  When it is saved
  Then no image element is stored and the surrounding text remains

Scenario: A hostile corpus does not survive
  Given the checked-in corpus (nested and unclosed tags, mixed case, entity-encoded schemes, SVG and MathML payloads, noscript and style in foreign content, attribute-injection forms, DOM-clobbering ids)
  When each payload is saved
  Then the stored value of each contains no script, no event attribute, no non-allowed scheme and no element outside the allow-list

Scenario: A stored value is stable under a browser's re-parse
  Given the stored value of every payload in the corpus
  When it is parsed again by an HTML5 parser, serialised, and reduced again
  Then the result is identical to the stored value (re-parse and re-reduce change nothing)

Scenario: Reduction is idempotent
  Given a description already saved and read back
  When it is saved again unchanged
  Then the stored value is identical

Scenario: Structures the editor cannot hold are canonicalised
  Given "<table><thead><tr><th>h</th></tr></thead><tbody><tr><td>a</td></tr></tbody></table>", "<ul><li>a<ul><li>b</li></ul></li></ul>", "<code><strong>x</strong></code>" and "<em><strong><a href='https://example.org'>l</a></strong></em>"
  When each is saved
  Then the stored values are "<table><tbody><tr><th><p>h</p></th></tr><tr><td><p>a</p></td></tr></tbody></table>", "<ul><li><p>a</p><ul><li><p>b</p></li></ul></li></ul>", "<p><code>x</code></p>" and the link outermost around strong around em

Scenario: The editor's canonical form is the server's
  Given the shared fixture file of (input, canonical, editor output) triples, the editor outputs including colgroup, style min-width, colspan="1", link target and nofollow, ol start
  When the server reduces each input and each editor output
  Then both equal canonical, and reducing canonical again returns canonical
  # the client suite loads canonical in the editor, normalises its output (BR-RT-5) and expects canonical back (US-3)

Scenario: An empty-looking description is stored as empty
  Given a description of only empty paragraphs, whitespace, a table of empty cells, a code block of spaces, or a link with no text
  When it is saved
  Then the stored description is "" 
  And a code block holding "a" is not empty
```

## Verification

All **U** (the reduction unit, over the fixture and corpus files) and **I**
(PATCH then GET through the API); nothing here needs a browser. The mXSS
scenario is a parser round trip in the server test (the parser the sanitizer
itself uses, plus the re-reduction); a real-browser mXSS probe is not
automated and is covered by the corpus and the client never using raw
`innerHTML` (US-3, BR-RT-12).
