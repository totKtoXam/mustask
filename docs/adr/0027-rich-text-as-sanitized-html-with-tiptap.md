# ADR-0027: Store the work item description as server-sanitized HTML with closed class palettes, edited with TipTap beside MUI

- Status: proposed
- Date: 2026-10-12 (amended 2026-10-13 for the widened scope, D-5..D-7; still
  `proposed`, only the human accepts)
- Context: feat-018-work-item-rich-text, stage 03-design (first design
  artifact, size `L`, spec D-2) — `docs/features/feat-018-work-item-rich-text/`.
  Departs from [ADR-0007](0007-frontend-ui-stack-and-screen-state-canon.md)
  and [ADR-0019](0019-ui-stack-narrowed-to-actual.md)

## Problem

Описание of a work item is plain text today (mod-003 BR-M3-19, stored exactly
as sent). The task makes it rich text edited in a WYSIWYG editor, which brings
three things the repository has none of: a browser editor library, a
server-side HTML sanitizer, and a stored format that is no longer "the
characters the user typed". Each is a library choice with long-term
consequences, and the editor departs from the UI stack ADR-0007 fixed
("swapping any of the libraries is a new ADR", `docs/adr/README.md` § When to
write an ADR). On 2026-10-12 the human decided the shape (spec D-1..D-4):
sanitized HTML in storage, TipTap in the browser, a server-side reduction on
every write, lazy conversion of legacy text, a limit on visible text with a
higher physical ceiling.

On 2026-10-13, after the prototype, the scope was widened (D-5..D-7, BR-RT-14
..BR-RT-20, US-5..US-7): the editor now also carries underline and
strikethrough, highlight, text colour and font size from **closed palettes
stored as classes** (colour 8, highlight 8, size 4), inline code that may be
linked, code blocks with a language from a closed list of 12 and
decoration-only colouring, **rectangular merge-free tables**, auto-link,
emoji and clear formatting. This adds browser dependencies beyond D-2 (an
emoji picker with its data, a syntax highlighter, a longer list of editor
extensions), a second stored vocabulary (class tokens) that the server must
own, counting in Unicode code points, and two follow-up tasks that must not
be foreclosed (images, task B; mentions, task C). This ADR records the
decision with the alternatives it was made against, so it is not reopened by
the next argument.

Forces: description content is written by any member and read by every
member, so the stored value is a stored-XSS vector for the whole panel; the
reference tracker's work items routinely carry formatted descriptions
(doc-002 BR-M3-4); existing descriptions must show exactly what they showed
(US-3); the API contract of mod-003 must stay additive for old clients
(US-4); the same editor and sanitizer will be reused for comments and the
three type text fields (spec § Decomposition); the panel runs without a
network dependency on third-party hosts at runtime.

## Options considered

### Stored format

#### Option F1: Markdown

- Pros: human-readable, diff-friendly, small; the safest thing to render
  (no markup in the stored form); no server-side HTML parsing for storage.
- Cons: the browser still needs a WYSIWYG layer, i.e. an editor that parses
  and serialises Markdown on every load and save; Markdown has no canonical
  form (nested lists, tables, hard breaks and escaping all have several
  spellings), so the dirty baseline of US-3 is hard to make stable; it has
  no notation at all for underline, highlight, colour or size, so the
  widened subset would need an HTML escape hatch inside it — the worst of
  both; tables exist only in a dialect (GFM) and cannot hold block content
  in cells; most Markdown renderers pass raw HTML through, so a server-side
  HTML reduction would be needed anyway for any renderer that is not ours. A
  plain-text description containing `*` or `<` would change meaning unless
  escaped at conversion, which is the same lazy-conversion problem with a
  lossier target.

#### Option F2: the editor's document JSON (ProseMirror doc)

- Pros: structurally cannot contain a script element or an event attribute;
  lossless for the editor; no HTML parsing on the browser side.
- Cons: the stored value is coupled to the editor's schema and version, so an
  editor upgrade or a swap becomes a data migration; the server would have to
  validate the document against that schema, and there is no .NET
  implementation of it — validation would be re-written by hand and drift;
  other consumers (a future CSV export, search indexing, a read-only
  renderer, an integration) must understand ProseMirror to read text; a
  link `href` still needs the scheme check, so the server-side reduction is
  not avoided, only moved into a custom walker.

#### Option F3: sanitized HTML (chosen)

- Pros: an open, tool-independent format; the server reduces every write to a
  fixed allow-list with an off-the-shelf HTML5 parser; the editor library can
  be swapped without touching stored data; every consumer can read it; the
  canonical form is checkable by a shared fixture (BR-RT-5).
- Cons: HTML is the format attackers know best: safety is a property of the
  reduction and of re-parse stability, not of the format (see Consequences,
  mXSS); the canonical form must be defined and enforced on both sides;
  stored size is larger than the text.

#### Option F4: HTML or plain, told apart by looking at the value (no marker)

- Pros: no new field, no contract change; old clients see no difference.
- Cons: rejected. A plain description `<b>x</b>` or `a < b` is
  indistinguishable from html; guessing either misrenders or, worse, treats
  attacker-controlled plain text as trusted html. BR-RT-9 requires that
  nothing in a plain value is interpreted as markup, which sniffing cannot
  guarantee. The format is stored explicitly and declared on every write
  (BR-RT-10).

### How colour, highlight and size are stored

#### Option P1: inline `style` attributes with a value allow-list

- Pros: what most editors and pasted HTML already carry; no vocabulary file.
- Cons: rejected (D-6). Free-form CSS values are the widest door a stored
  value can have; sanitising `style` means parsing CSS, and a value
  "allowed" today (`color`, `background-color`, `font-size`) stays in rows
  forever, outside any theme or dark mode; colours would be literals, not
  ADR-0024 tokens.

#### Option P2: closed palette stored as class tokens (chosen)

- Pros: the stored value names a choice (`rt-color-red`), not a rendering, so
  the theme maps it (light and dark) and can change without a data change;
  the server accepts only exact tokens and writes them from its own table,
  never echoing input (BR-RT-14); pasted foreign colour is dropped, never
  guessed into the palette.
- Cons: a second stored vocabulary that both sides must hold identically
  (see Decision 6); a token is a contract: renaming one is a data
  migration; the `class` attribute, which the allow-list used to ban
  entirely, is now allowed in a controlled form.

### Browser editor

#### Option E1: TipTap (ProseMirror, MIT), custom toolbar in MUI (chosen)

- Pros: headless — the toolbar and the chrome are ours, so the theme stays
  MUI's (ADR-0024); ProseMirror's schema is an enforced model, not a
  convention, and can be configured to the exact canonical form of US-7;
  HTML in, HTML out; its extension model lets us write the three
  vocabulary marks in-house (class-writing, not style-writing); a large
  ecosystem and a long track record.
- Cons: a new dependency family (ProseMirror core and TipTap extensions)
  with a real bundle cost; TipTap also sells Pro and Cloud extensions — as
  with the DataGrid in ADR-0007, only the MIT packages are in the stack;
  jsdom does not implement the selection and layout APIs ProseMirror uses,
  so part of US-1, US-5 and US-6 stays manual QA.

#### Option E2: mui-tiptap (TipTap with ready-made MUI components)

- Pros: toolbar and styling work already done in MUI components.
- Cons: a community wrapper in front of two fast-moving majors (TipTap and
  MUI); its look follows its own idea of the theme and its schema defaults,
  which we must override to reach the canonical form; the toolbar here is
  three dozen controls with Russian labels and closed palettes of our own,
  cheaper to own than to adapt. Revisit if the toolbar outgrows the
  allow-list.

#### Option E3: Lexical

- Pros: MIT, backed by a large company, React-first, small core, accessible
  by design.
- Cons: its native state is its own JSON and HTML import/export is an
  add-on rather than the model, so holding the canonical HTML form exactly is
  more work than with a schema-based editor; table and list handling are
  separate packages with less field time than ProseMirror's.

#### Option E4: Slate

- Pros: MIT, fully controllable data model.
- Cons: a framework for building an editor, not an editor: lists, tables,
  link handling, paste and undo are ours to write and to test; its API has
  not reached a stable major.

#### Option E5: Quill

- Pros: mature, small, simple to embed.
- Cons: its model is the Delta format and HTML is a derived view of limited
  fidelity; table support is not part of the core and the subset requires
  tables; little control over output structure.

#### Option E6: CKEditor, TinyMCE and similar full suites

- Pros: complete out of the box.
- Cons: the licence of the editing product is GPL or commercial, not MIT;
  heavy; their own UI does not take the MUI theme. A licence is a budget
  decision for the human, not a slice edit.

### Emoji picker

#### Option M1: in-house picker over a bundled static data set (chosen)

- Pros: a MUI `Popover` with the search field and a grid of buttons
  (US-5 names, keyboard and focus are ours to specify and to test); themed
  by MUI tokens; no network at runtime (BR-RT-19); the data is a build-time
  input, trimmed to what is used (character, Russian name and keywords,
  group) and loaded as its own lazy chunk on first open of the picker.
  Candidate data source: `emojibase-data` (MIT), which ships Russian
  names and search keywords; verified against its package manifest at pin
  time. Glyphs are the system's native ones, nothing is drawn.
- Cons: a small component to write and own (grid, search, recent is out of
  scope); the data set must be kept in step with Unicode releases
  deliberately, by a dependency bump; flag emoji are shown as letter pairs
  on Windows, so the flags group is omitted from the picker (a
  presentation choice, not a storage one).

#### Option M2: a packaged picker (emoji-mart and similar)

- Pros: complete UI, skin tones, categories and recents out of the box.
- Cons: the common ones fetch their data from a CDN unless configured
  otherwise (a runtime third-party request, against BR-RT-19), render with
  their own styling outside the MUI theme and dark mode, and bring their
  own search and keyboard model that US-5 must then fight; the bundled data
  is larger than the trimmed set we need. Declined; could be reconsidered
  only with its data and styles fully local.

#### Option M3: a web-component picker (emoji-picker-element and similar)

- Pros: small wrapper, self-contained.
- Cons: shadow DOM defeats the MUI theme and the accessibility checks we
  run, its data comes from a CDN into IndexedDB by default. Declined for
  the same reasons.

### Syntax highlighting of code blocks

#### Option H1: `lowlight` (highlight.js grammars) through TipTap's code-block-lowlight extension, as ProseMirror decorations (chosen)

- Pros: the extension turns the highlighter's token tree into **editor
  decorations** over the block's text nodes — it never inserts a
  highlighter's HTML string, which is exactly BR-RT-16; highlight.js is
  BSD-3-Clause and lowlight is MIT (both confirmed against the package
  manifests at pin time); grammars are registered individually, so only the
  12 closed languages are bundled (`html` shares the `xml` grammar, and the
  list is not extended without an ADR amendment); the stored value never
  contains a token: colouring is not stored.
- Cons: highlight.js grammars are heuristic and size is not trivial
  (grammars are the main weight); the decoration tokens need theme-mapped
  colours in light and dark (design.md's); the extension's own handling of
  the `language-` class is configured to the closed list and, as always,
  re-checked by the server.

#### Option H2: Prism or Shiki

- Pros: Prism is small and MIT; Shiki gives editor-quality colouring.
- Cons: Prism's usual integration is string-to-HTML, a pattern this ADR
  forbids, and there is no maintained ProseMirror decoration adapter to
  rely on; Shiki ships a WASM regex engine and large grammars and themes,
  and is built for rendering HTML. Declined.

#### Option H3: no highlighting, plain monospace

- Pros: nothing to bundle; fewer moving parts.
- Cons: the human asked for proper code display (D-5, US-6) and the
  reference tracker highlights; the cost of H1 is bounded by registering 12
  grammars. Kept as the fallback if the bundle budget (Decision 8) cannot
  be met.

### Server-side sanitizer (.NET)

`Directory.Packages.props` references no HTML parser and no sanitizer today:
every option below is a new package. Versions are not chosen in this ADR —
pin at implementation, via `Directory.Packages.props`.

#### Option S1: HtmlSanitizer (Ganss.Xss) (chosen)

- Pros: purpose-built, allow-list model (allowed tags, attributes, schemes,
  classes; deny by default); built on AngleSharp, a parser that follows the
  WHATWG HTML5 parsing algorithm — the same tree construction a browser
  performs, which is what mXSS resistance rests on; its removal and
  post-processing hooks cover the canonical-structure pass without a second
  library; widely used, maintained, MIT.
- Cons: it is an allow-list filter, not a canonicaliser: heading level
  mapping, header rows into the body, rectangular tables, wrapping loose
  text in `p`, mark nesting order, token-to-element binding (a colour token
  only on `span`, a language only on `code` in `pre`), writing tokens from
  the table and the visible-text count are ours, written as a pass over the
  same AngleSharp DOM; configuration is large and must be covered by the
  corpus; its behaviour for removed elements and for the class allow-list
  must be confirmed against US-2 and US-7 by a spike before the design is
  final; AngleSharp comes in transitively, so an explicit reference follows
  the repository's precedent (the `Microsoft.IdentityModel.Protocols
  .OpenIdConnect` comment in `Directory.Packages.props`).

#### Option S2: AngleSharp alone, with our own allow-list walker

- Pros: one dependency, full control of every rule including the canonical
  form, the early abort and the visible-text count; no hook semantics to
  learn.
- Cons: the security-relevant part — which element and attribute is
  dropped, how URLs are read, what happens to foreign content and
  `template` — becomes our code to write and to keep correct against future
  parser changes; an off-the-shelf allow-list has been probed by far more
  hostile input than a project walker will be. Kept as the fallback if S1's
  hooks cannot express the canonical pass or the early abort.

#### Option S3: HtmlAgilityPack

- Pros: widely known, small.
- Cons: rejected for this use. A tolerant parser, not an HTML5
  tree-construction implementation: it does not reproduce the browser's
  parse, so it cannot carry a re-parse stability argument; no allow-list
  model, everything would be ours.

#### Option S4: Microsoft AntiXSS or hand-written regular expressions

- Pros: none that outweigh the cons.
- Cons: rejected. The AntiXSS library is no longer maintained; regular
  expressions cannot parse HTML and are the classic bypass.

## Decision

1. **Storage: sanitized HTML (F3) under an explicit stored-format marker**
   (`plain` or `html`), declared on every write and reported on every read;
   no declaration means `plain`, stored as sent; the API change is additive
   (BR-RT-10). Content sniffing (F4) is rejected. Legacy plain text is
   converted lazily by the displaying client under the legacy rule
   (BR-RT-9); no migration rewrites a row (D-3).
2. **Colour, highlight and size are class tokens (P2).** `style` is never
   stored. The server is the only writer of a token: it matches exact,
   case-sensitive input and writes the class from its own table (BR-RT-14).
   The `rt-` prefix is reserved for the product's tokens.
3. **Editor: TipTap on ProseMirror (E1), MIT packages only**, with a toolbar
   of MUI components and the Russian names of US-1, US-5 and US-6.
   `mui-tiptap` is not adopted (E2). The editor lives in `shared/ui` as a
   generic `RichTextEditor`, so comments and the type fields reuse it, and
   is loaded lazily on the work item form route, not in the main chunk.
   Its schema is configured to the canonical form of US-7; the editor's own
   restrictions are a convenience, not the safeguard (BR-RT-2).
4. **Browser dependencies accepted (beyond D-2), all MIT or compatible,
   confirmed against each package manifest's licence field at pin time; any
   package that needs a licence key, a private registry or a paid tier is
   declined:**
   - the TipTap core and the open-source extensions the subset needs: table
     (with row, header and cell), underline, highlight (configured to write
     class tokens, or replaced by an in-house mark), link, placeholder,
     code-block-lowlight, and the history, list, heading, gap-cursor and
     drop-cursor pieces of the starter set. None of these is in TipTap's
     paid tier. **Not used:** text-style and colour extensions as shipped,
     because they write inline `style`; colour and size are three small
     in-house marks (`rtColor`, `rtSize`, and the highlight above) that
     read and write the tokens of the shared vocabulary. No TipTap Pro
     or Cloud extension is accepted;
   - `lowlight` with highlight.js grammars for the 12 languages only (H1),
     as decorations, never stored, never an HTML string;
   - **emoji: the in-house picker (M1)** over a bundled static data set
     (`emojibase-data` is the candidate, MIT), loaded as its own lazy chunk;
     no CDN, no runtime network, system glyphs. Packaged pickers are
     declined (M2, M3).
5. **Server reduction: HtmlSanitizer (S1) plus a canonical-structure pass
   over its AngleSharp DOM.** The allow-list of elements, attributes and
   classes is US-2's and US-7's, and this is the only place it exists on the
   server. Placement per `docs/conventions/backend.md`: an interface in
   `Mustask.Application` (`Abstractions/`) and its implementation in
   `Mustask.Infrastructure`, so `Mustask.Domain` stays free of third-party
   dependencies; the Domain keeps the format enum, the two limits and the
   rule that a value is validated after reduction (extending
   `WorkItemText.ValidateDescription`). The reduction returns the reduced
   html and its visible-text length (BR-RT-8) from one parse. Exact type and
   slice names are the design's.
6. **One source of the closed vocabularies, consumed by both sides.** The
   tokens (kind, element, class, the palettes and the language list, with
   the stored class names) live in one checked-in file,
   `contracts/rich-text/vocabularies.json`, beside the existing
   `contracts/errors/*.json` and `contracts/dictionaries/` the repository
   already shares between server and panel. The server embeds it as a
   resource and loads it at startup into its token table; the panel imports
   the same file at build time (a Vite JSON import) and generates the
   editor's marks, toolbar options and the normaliser's table from it.
   Neither side hand-writes a token. Two tests guard it: a server unit test
   fails if the loaded table and the file disagree on any entry, and a
   panel test fails if a token has no theme mapping (colour, light and dark
   — design.md) or a theme mapping has no token. The shared fixture files
   (point 9) sit in the same folder. Adding an entry is a change to this file
   alone plus its theme mapping and copy.
7. **Canonical-form consequences for the editor schema.** The schema is
   configured, not defaulted; each item is a US-7 rule the editor must hold
   so that its output normalises to the server's form:
   - mark order in the schema is the canonical nesting (link, bold,
     italic, underline, strikethrough, highlight, colour, size), set by
     extension priority;
   - the inline `code` mark excludes every other mark **except link**, and
     link does not exclude code (the extension's default "excludes all" is
     overridden); a code block holds text only, no marks;
   - the code block node is `isolating`, so editing commands do not merge
     across its boundary, and table cells hold `paragraph+` only (no
     headings, lists or nested tables);
   - the trailing-node behaviour is **off**: the editor never appends a
     paragraph after a last table or code block, since BR-RT-5 stores none;
     exits use the gap cursor and the Enter-twice and Ctrl/Cmd+Enter
     commands of BR-RT-16 and BR-RT-17, so merely moving through a final
     block does not change the value (US-3, US-6);
   - tables are rectangular by construction: `colspan`/`rowspan` are parsed
     and expanded into the empty cells the server's rule gives, merge, split
     and column-resize are not registered, and the editor's table-fixing
     step runs on load; list `start` and the code-block chrome (copy
     button, language select) are not serialised;
   - the heading node drops the size mark; heading levels are 3 and 4.
   The client's normaliser is the second line of the same rules, applied to
   the editor's raw output before it is compared, counted or sent (BR-RT-5).
8. **Counting, ceiling and abort (BR-RT-7, BR-RT-8).** For html, both limits
   are counted in **Unicode code points** by server and client: on the
   server by enumerating runes over the reduced value (a lone surrogate is
   replaced by U+FFFD before counting and in the stored text), on the client
   by iterating code points, never `string.length`. The physical ceiling is
   1 000 000 code points; PostgreSQL's `char_length` counts characters, not
   UTF-16 units, so the widened `ck_work_items_description_length` is
   the same unit as the application's. A plain description keeps BR-M3-19's
   100 000 UTF-16 units. A request description above **2 000 000 UTF-16
   units is refused before parsing**, so the parser's input is bounded;
   re-nesting in the canonical pass can amplify, so the pass carries an
   **output budget**: it counts code points as it builds and aborts the
   moment the output passes 1 000 000, refusing with
   `description_markup_too_large` (checks in BR-RT-7's order). The
   amplification case is a timed server test (NFR Speed).
9. **Fixture-driven parity.** Shared fixture files (input, canonical, editor
   output; html and visible count with emoji and ZWJ sequences; hostile
   corpus) run in the server and client suites; they are the contract
   between the two implementations of the canonical form (BR-RT-5,
   BR-RT-8, US-7).
10. **Bundle: lazy, and measured against a budget.** Everything in the
    editor — ProseMirror, the extensions, lowlight grammars, the toolbar —
    is in one lazy chunk loaded on the form route; the emoji data is a
    second lazy chunk loaded the first time the picker opens; a code
    block's grammars are registered up front (12 is bounded) or, if the
    budget requires, per language on first use. **Proposed budget, for the
    human to confirm or change once the build's chunk report gives the
    baseline (no figure is measured yet):** the entry and the shared vendor
    chunks grow by zero bytes (checkable); the editor chunk, gzipped, has a
    stated ceiling recorded in `design.md` and re-checked in CI by the
    chunk report; the emoji chunk has its own, smaller, ceiling. A build
    that exceeds either is not merged without a human decision; the
    fallbacks in order are per-language grammar loading, then H3 (no
    highlighting). The measured numbers are written into `design.md` at
    implementation.

**Departure from conventions.** ADR-0007/0019 fix the panel's UI stack as MUI
core and `@mui/icons-material`, with swapping or adding a library a new ADR.
This ADR adds the TipTap/ProseMirror family, lowlight and one emoji data
package as the exception, scoped to the rich-text editor. On acceptance (not
before): `docs/conventions/frontend.md` § MUI gets a sentence naming the
editor and linking this ADR; the index entry of ADR-0007 gets "amended by
ADR-0027"; `docs/conventions/backend.md` gets a reference to where the
reduction lives and to `contracts/rich-text/` (and `contracts/README.md`
the same). The mod-003 api-contract amendments and doc-002 BR-M3-4's widened
subset are BR-RT-13 and not repeated here.

## Consequences

**Simpler.** One editor and one reduction serve every future rich-text
field; the stored value is readable without the editor and names choices,
not CSS; swapping the editor later is a code change, not a data migration. A
stored canonical value is not rewritten by an unchanged save (BR-RT-11), so
history (feat-015) and concurrency will not see spurious changes.

**Harder.**

- *Two implementations of one canonical form.* The server pass and the
  editor schema plus normalisation step must agree, now over marks,
  palettes, code and rectangular tables; the guard is the shared vocabulary
  file and the shared fixtures. A new element, mark or token in the subset
  is a change in four places (vocabulary file, sanitizer configuration,
  editor schema, fixtures) and goes through an amendment of this ADR or a
  new one, and doc-002 BR-M3-4 where the subset is shared.
- *The reduction is a security boundary.* Its configuration is reviewed like
  auth code; the hostile corpus (US-2) is a permanent test input and grows
  with every bypass found, including hostile `class` and `style` values and
  scripts inside code blocks. A library upgrade is taken only with the
  corpus green. Tokens are never echoed, so a class cannot be used to carry
  a payload, a CSS hook or a clobbering name.
- *mXSS.* A stored string is accepted only if stable: parsed again by an
  HTML5 parser, serialised and reduced again, it is identical (US-2). This
  is a parser round trip in the server test; a real-browser probe is not
  automated. Defence in depth on the client: html is never assigned to the
  page (`react/no-danger` is an error, BR-RT-12) and is loaded through the
  editor's schema, which drops what it cannot hold; code-block colouring is
  decorations over text nodes, never markup built from a string; emoji
  are text.
- *Rollback.* Widening the stored bound is additive, but its down migration
  fails once any row exceeds 100 000 characters (runbook in the design).
  Rolling the release back leaves html in the store, which the older release
  shows as raw tags in a plain field; the stored-format marker stays so the
  release can be rolled forward again without data repair. No downgrade of
  html to plain is built (spec § Out of scope).
- *Bundle weight.* ProseMirror, the extensions, grammars and emoji data are
  a material addition to a panel whose DataGrid decision once pushed one
  chunk to 552 kB (ADR-0007). Decision 10 binds it to a lazy split and a
  budget; if the measured size is unacceptable to the human, this ADR is
  revisited rather than the chunk quietly accepted.
- *Test limits.* jsdom lacks the layout and selection APIs of a
  contenteditable: keyboard shortcuts, clipboard paste, undo (including the
  custom first-undo-after-auto-link step), the Esc-then-Tab exit and Tab
  inside tables and code are manual QA items where the stories name them;
  there is no E2E in this task.
- *Supply chain.* ProseMirror/TipTap, lowlight and an emoji data package in
  the browser, HtmlSanitizer and AngleSharp on the server: both sides sit on
  the path of every description write and read and must be kept current;
  emoji data moves with Unicode releases.
- *Platform glyphs.* Emoji are system glyphs, so appearance differs by OS
  and some recent emoji show as boxes on an old one; the stored character
  is correct either way. Flags are omitted from the picker for Windows.

**What later features inherit.**

- *Comments (feat-002) and the three type text fields
  (`feat-NNN-work-item-rich-text-type-fields`)* reuse `RichTextEditor`, the
  server reduction, the vocabulary file and the fixtures, with the same
  allow-list. A narrower profile for comments is an extension point of the
  reduction (a profile parameter), not a fork of the sanitizer. Each field
  gets its own stored-format marker and limit pair: the marker is per
  field, not per table.
- The legacy rule (BR-RT-9) and lazy conversion apply to each text field
  that was plain before; every new rich-text field starts with the
  declaration contract, never with sniffing.
- The first read-only renderer (comments) is built under BR-RT-12: through
  the editor schema or the same reduction, whitespace preserved, with the
  palette tokens mapped by the same theme.
- *Images (task B)* are not foreclosed and not decided here. BR-RT-4 drops
  `img` today; B amends it by adding an element and an attribute to the
  allow-list, an editor node and, most of all, a **storage and access
  decision of its own** (images as attachments, URL form, who may fetch the
  bytes, crop) that needs its own ADR — it is not made by pre-reserving
  anything in this one. What is kept open: the vocabulary file and the
  reduction's allow-list are data, so a new node is additive; the `rt-`
  prefix leaves room; the stored format value stays `html`. Rolled back, an
  older server would drop the images on the next write, which B's runbook
  states.
- *Mentions and work-item links (task C)* likewise: an inline node with its
  own attributes (an element and a data attribute the allow-list refuses
  today, `data-*` being stripped by BR-RT-3), amended by C and gated on the
  user directory and search that do not exist yet. Nothing here consumes
  the `@` or `#` triggers: auto-link keys on `http://`, `https://`,
  `mailto:` and `www.` only (BR-RT-18) and the emoji picker is opened by a
  control, not a typed character, so C's suggestion plugin has the triggers
  free. C adds its node to the same schema, the same file and the same
  fixtures.
- Full-text search (feat-003), CSV import/export and history diffs
  (feat-015) read HTML; their handling of markup, class tokens included, is
  theirs.

**Revisit when:** the measured bundle cost is rejected by the human; the
canonical form drifts between server and editor in practice and the fixtures
prove inadequate; HtmlSanitizer or AngleSharp stop being maintained; a
packaged emoji picker becomes fully local and themeable; or the product
needs collaborative editing, for which the editor's JSON document (F2)
becomes the better stored form.

**Backlog generated:** none beyond the follow-up lines already in the spec
(B, C, type fields); the documentation updates named under Decision are done
at acceptance. The vocabulary file's location in `contracts/` is confirmed or
moved by `design.md`.
