# US-7: The server's canonical form: closed vocabularies, mark order, tables, code

- story: US-7
- depends-on: US-2, US-4
- shippable: no — it is the shape of what US-5 and US-6 write and US-3 reads back
- status: pending

As a **member**, I want **a description I saved, opened and saved again
untouched to come back byte for byte, and its colours, languages, tables and
marks to be only the ones the product defines**, so that **nothing drifts and
nothing free-form is stored**.

BR-RT-5, BR-RT-6, BR-RT-14, BR-RT-15, BR-RT-16, BR-RT-17.

## Closed vocabularies (the server accepts only these)

| Kind | Element | Tokens |
|---|---|---|
| Text colour | `span` | `rt-color-gray`, `rt-color-red`, `rt-color-orange`, `rt-color-green`, `rt-color-teal`, `rt-color-blue`, `rt-color-purple`, `rt-color-pink` |
| Highlight | `mark` | `rt-hl-yellow`, `rt-hl-orange`, `rt-hl-red`, `rt-hl-green`, `rt-hl-teal`, `rt-hl-blue`, `rt-hl-purple`, `rt-hl-gray` |
| Font size | `span` | `rt-size-xs`, `rt-size-sm`, `rt-size-lg`, `rt-size-xl` |
| Code language | `code` in `pre` | `language-javascript`, `-typescript`, `-json`, `-html`, `-xml`, `-css`, `-sql`, `-csharp`, `-python`, `-bash`, `-yaml`, `-markdown`; none = plain |

## Canonical structure

The reduced output is the editor's canonical form (BR-RT-5); the editor's
schema is configured to hold exactly this and no more:

- **Marks** nest outermost to innermost: `a`, `strong`, `em`, `u`, `s`,
  `mark`, colour `span`, size `span`. Adjacent equal marks merge; empty inline
  elements are removed; an element carries one token (several are split into
  nested elements in that order; within one family the first valid token wins).
- **Nested elements of one family** (colour inside colour, size inside size,
  highlight inside highlight): the inner one wins, the outer is dropped for
  that text.
- **Inline `code` wins over every mark but a link:** `strong`, `em`, `u`, `s`,
  `mark` and `span` around or inside it are dropped (text kept, code stays); a
  link around it stays outermost, a link inside it is hoisted outward around
  the code. A **heading** drops size spans; other marks stay. A **code block**
  holds text only, no marks; a `br` in it is a line feed.
- **Lists:** `li` holds only `p` blocks and nested `ul`/`ol`; inline content
  directly in it is wrapped in `p`; a heading is unwrapped to `p`, a `pre`
  becomes one `p` per line (empty lines kept as empty `p`), a table becomes its
  cells' paragraphs; text kept. `ol` has no `start`.
- **Tables are rectangular:** `th` and `td` hold only `p` (same unwrapping as
  list items); rows of `thead`/`tfoot` go into `tbody`; no `colspan`/`rowspan`,
  `colgroup` or widths. A cell spanning n columns or m rows keeps its content
  at its top-left position and each other position it covered becomes an empty
  cell of the same element (`<td><p></p></td>`, `th` for `th`); a short row is
  padded at its end to the widest row with empty cells of its last cell's
  element; a row with no cell and a table with no row are removed; a table
  inside a table, a list item or a cell is unwrapped to its cells' paragraphs.
- An empty block is `<p></p>`; a `br` in a heading is kept.
- **Class tokens:** the `class` value is split on ASCII whitespace; a token
  counts only if it equals a table entry exactly (case-sensitive); the output
  is written from the server's table, never echoed.

## Acceptance criteria

```gherkin
Scenario: Palette classes are kept, anything else is dropped
  Given "<span class='rt-color-red'>a</span><mark class='rt-hl-yellow'>b</mark><span class='rt-size-lg'>c</span><span class='rt-color-chartreuse'>d</span><span class='evil rt-color-blue'>e</span><span style='color:red'>f</span><mark>g</mark><p class='rt-color-red'>h</p>"
  When it is saved
  Then the stored value keeps a (red), b (yellow), c (large) and e (blue, the unknown token dropped)
  And d, f and g are plain text, and the p carries no class

Scenario: Tokens are matched exactly and written from the table
  Given "<span class='rt-color-red&#9;x'>a</span><span class='RT-COLOR-RED'>b</span><span class=' rt-color-red '>c</span><span class='rt-color-red-dark'>d</span>"
  When it is saved
  Then a and c are red spans carrying exactly the class "rt-color-red", b and d are plain text

Scenario: The inner of nested same-family marks wins
  Given "<span class='rt-color-red'>a<span class='rt-color-blue'>b</span>c</span>"
  When it is saved
  Then a and c are red and b is blue, never both on b

Scenario: Two tokens on one element are split in canonical order
  Given "<span class='rt-size-lg rt-color-red'>x</span>"
  When it is saved
  Then the stored value is the colour span around the size span around "x"

Scenario: Code language is a closed list
  Given "<pre><code class='language-python'>a</code></pre>", "<pre><code class='language-brainfuck'>b</code></pre>" and "<pre><code class='language-python x'>c</code></pre>"
  When each is saved
  Then the stored values keep "language-python" for a and c, and no class for b

Scenario: Marks are put in the canonical order
  Given "<em><strong><a href='https://example.org'>l</a></strong></em>" and "<span class='rt-color-red'><s><u>x</u></s></span>"
  When each is saved
  Then the first is stored as link around strong around em, and the second as u around s around the colour span around "x"

Scenario: Legacy and foreign tags are normalised
  Given "<b>a</b><i>b</i><strike>c</strike><del>d</del>"
  When it is saved
  Then the stored value is a strong "a", an em "b" and one s "cd" (adjacent equal marks merged)

Scenario: Code wins over marks, a link survives
  Given "<code>a<a href='https://example.org'>b</a>c</code>" (the link keeps its own extent: code "a", linked code "b", code "c"), "<strong><code>x</code></strong>", "<a href='https://example.org'><code>y</code></a>", "<code><a href='https://example.org'>z</a></code>" and "<code><strong>w</strong></code>"
  When each is saved
  Then the stored values are "<p><code>x</code></p>", "<p><a href=\"https://example.org\" rel=\"noopener noreferrer\"><code>y</code></a></p>", the same shape for z, and "<p><code>w</code></p>"

Scenario: A code block takes no marks
  Given "<pre><code><em>y</em><br>z</code></pre>"
  When it is saved
  Then the stored value is a block whose text is "y" LF "z"

Scenario: A multi-line code block inside a cell or a list item is unwrapped per line
  Given "<ul><li><pre><code>a\n\nb</code></pre></li></ul>" and "<table><tr><td><pre><code>a\n\nb</code></pre></td></tr></table>"
  When each is saved
  Then the list item and the table cell each hold three paragraphs "a", "" and "b"

Scenario: A heading drops size only
  Given "<h3><span class='rt-size-xl'>big</span> <strong>bold</strong></h3>"
  When it is saved
  Then the heading holds "big" plain and "bold" in strong

Scenario: Structures the editor cannot hold are canonicalised
  Given "<table><thead><tr><th>h</th></tr></thead><tbody><tr><td>a</td></tr></tbody></table>", "<ul><li>a<ul><li>b</li></ul></li></ul>" and a table inside a cell
  When each is saved
  Then the stored values are "<table><tbody><tr><th><p>h</p></th></tr><tr><td><p>a</p></td></tr></tbody></table>", "<ul><li><p>a</p><ul><li><p>b</p></li></ul></li></ul>" and the inner table's cell paragraphs in place of it

Scenario: Tables are made rectangular
  Given "<table><tr><td colspan='2'>a</td></tr><tr><td>b</td><td>c</td></tr></table>", "<table><tr><td rowspan='2'>a</td><td>b</td></tr><tr><td>c</td></tr></table>", "<table><tr><td>a</td></tr><tr><td>b</td><td>c</td></tr></table>" and "<table><tr></tr></table>"
  When each is saved
  Then the first is rows "a, empty" and "b, c"; the second rows "a, b" and "empty, c"; the third rows "a, empty" and "b, c"
  And the fourth is stored as ""
  And every stored table is a rectangle of paragraph cells

Scenario: The editor's canonical form is the server's
  Given the shared fixture file of (input, canonical, editor output) triples covering every mark, palette token, language, nested list, header-row table and link, the editor outputs including colgroup, style min-width, colspan="1", link target and nofollow, ol start, code-block chrome
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

All **U** (the canonicalisation unit over the shared fixture file) and **I**
(PATCH then GET). The client half of the fixture (canonical loaded in the
editor, normalised, equal to itself) is a **C** test in US-3. The mapping of
tokens to theme colours, light and dark, is design.md's and is checked
manually (**M**) at QA.
