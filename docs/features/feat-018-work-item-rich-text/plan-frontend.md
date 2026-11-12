# Plan — frontend (feat-018-work-item-rich-text)

Stage 6a, size L: a separate deep pass, no production code. Written
2026-10-13 against `spec.md` (BR-RT-1..20), US-1..US-7, `design.md`,
ADR-0037 (accepted), `api-contract.md` (fixed), `plan-backend.md` § 5, § 6.2
("as built"), the shared files in `contracts/rich-text/`, and the code as it
stands (`WorkItemFormPage.tsx`, `lib/draft.ts`, `lib/rules.ts`,
`lib/refusals.ts`, `api/workItemsApi.ts`, `shared/ui/FormDialog.tsx`,
`app/theme`). Server source read for byte parity:
`Mustask.Infrastructure/RichText/{DomConverter,CanonicalWriter,CanonicalModel,
TableNormaliser,LinkAddress,RichTextVocabulary,HtmlRichTextReducer}.cs`.

Nothing was installed or run (CPU-saturated machine, other sessions paused
full runs). Package versions below come from `npm view` on 2026-10-13
(`@tiptap/* 3.31.4` MIT, `lowlight 3.3.0` MIT, `highlight.js 11.12.0`
BSD-3-Clause, `emojibase-data 17.0.0` MIT) and **TipTap API names are from
memory of v3 and are confirmed by spike S1 before they are relied on**; where
this plan names an API it is a statement of intent, not a checked signature.

## 0. Summary

| Question | Answer |
|---|---|
| Where does it live | `shared/ui/RichTextEditor/` (generic, no business logic, lazy chunk), `shared/lib/richText/` (pure functions, no React, no TipTap), `app/theme/richText.ts` (token table), the form's wiring in `features/workitems` |
| Core idea | One **pure normaliser** that is a line-for-line port of the server's reduction (`DomConverter` + `TableNormaliser` + `CanonicalWriter` + `LinkAddress`) over a `DOMParser` tree. The editor's `getHTML()`, the paste path and the legacy conversion all go through it, and the **whole** shared fixture file (not only the `editorOutput` column) runs against it |
| Dirty model | The draft's `description` is the **canonical string**. Its baseline is `descriptionBaseline(item)` = the stored html, or `legacyToHtml(plain)`. The editor reports a string only on a document change, and reports the baseline itself while `doc.eq(loadedDoc)`. `changesOf` sends `description` + `descriptionFormat: 'html'` iff the string differs from the baseline; untouched sends nothing |
| Not what the spec text suggests | (a) `react/no-danger` is realised without a new package (F-4). (b) Highlighting uses our own decoration plugin over `lowlight`, not `@tiptap/extension-code-block-lowlight`, because the design's 20 000 cp / 1 000 line cap and 150 ms idle recompute are not expressible in that extension (F-3). (c) "entry chunk grows by zero bytes" is replaced by a measurable proxy (F-5) |
| Order | 0 spike and foundation → 1 pure library + fixtures → 2 contract plumbing → 3 editor core → 4 marks, link, emoji → 5 tables, code, paste → 6 form swap → 7 measure and close (§ 12) |
| Needs a decision | § 14.4: seven items, three of them for the human (F-5 bundle wording, F-3 ADR deviation note, design copy gaps G-1..G-3) |

## 1. Scope

In: the work item form's «Описание» section becomes `RichTextEditor`; the
client half of US-1, US-3, US-4, US-5, US-6, US-7 (BR-RT-5's normalisation),
the client half of US-2 (nothing hostile reaches the page).

Out (unchanged by design): the type's own texts («Критерии приёмки» etc.) stay
`TextField`; no read-only renderer (BR-RT-12 binds later surfaces); no image,
mention or `#` link (tasks B, C — only their reserved toolbar position, § 6.3);
no E2E (human's instruction, `status.md`).

## 2. Decisions and deviations register

IDs are cited in the later sections. "Owner" is who must act before 6b can
rely on it; nothing here changes `api-contract.md`.

| ID | Decision | Why | Owner |
|---|---|---|---|
| F-1 | The client normaliser is a **port** of the server pass, not a second interpretation of the spec | BR-RT-5 demands byte equality; § 6.2 of the backend plan lists seven choices the spec is silent on; a port makes them the same by construction and lets the server's own `input` column be the client's oracle too | frontend |
| F-2 | Editor documents are **loaded as ProseMirror JSON** built by our own `DOMParser` + `preserveWhitespace: 'full'`, never as a string through TipTap's `content:` | TipTap's string path strips some whitespace-only text nodes (a text node of exactly `"\n"` or `"\n  "` — a one-line-feed code block would be lost) and its parse options are not the whole story; JSON is exact and also keeps `getSchema` as the single source of what the editor can hold. Re-checked at pin time (S1) | frontend |
| F-3 | Code colouring is an **own ProseMirror plugin** (`RtCodeHighlight`) calling `lowlight` directly; `@tiptap/extension-code-block-lowlight` is not a dependency | its plugin recomputes every code block synchronously on any change inside one, with no cap and no idle delay; design § Syntax colouring (D-9) requires both. Still "lowlight as decorations" (ADR-0037 decision 4, H1) and `highlight.js` grammars for the 12 languages; one package fewer. ADR decision 4 names the extension in its list, so this is an **implementation deviation to record**, not a new decision | orchestrator: a `status.md` note; human informed at gate-3 |
| F-4 | `react/no-danger` (spec NFR Safety) is delivered by ESLint `no-restricted-syntax` on the JSX attribute `dangerouslySetInnerHTML` and `no-restricted-properties` on `innerHTML`/`outerHTML`/`insertAdjacentHTML`, plus a source-scan test `src/test/noRawHtml.test.ts` in the style of `colourLiterals.test.ts` | `eslint-plugin-react` is not in the stack (`eslint.config.js` has only `react-hooks` and `react-refresh`); the equivalent needs no package. If the human prefers the plugin literally: one devDependency, one rule | orchestrator/human, low stakes |
| F-5 | Bundle claim "the entry and shared vendor chunks grow by zero bytes" (ADR decision 10) is checked as: **no editor-library module is reachable from the entry chunk** (module-level check from a build report) and the entry's gzipped growth is a **stated small figure** (target ≤ 8 KB, the pure library's size), measured, not zero | the pure library (`normalise`, vocabulary JSON, `legacyToHtml`) must be in the main bundle because `lib/draft.ts` runs on the form route without the editor (baseline, dirty, save check). A literal zero would need the whole form route lazy (routes are not lazy anywhere today) or a draft that carries counts from the editor chunk | human confirms the proxy (or picks the all-lazy variant, § 10.3) |
| F-6 | Auto-link scans **plain-text** pastes only (clipboard has no `text/html`), plus a single address over a non-empty selection | spec BR-RT-18 says "pasted (alone or inside plain text)"; an html paste already carries its own links and link-ifying its text would rewrite structure. Spec silent for html | analyst may confirm; low risk |
| F-7 | Plain-text paste keeps **blank lines as empty paragraphs** (own `clipboardTextParser`) | ProseMirror's default collapses runs of line breaks (`/(?:\r\n?\|\n)+/`), which contradicts "line breaks kept" (US-1) and the legacy rule's spirit | frontend |
| F-8 | In a code block, **Tab with a non-empty selection indents every selected line** (tab at each line start); with a caret it inserts a tab | spec says only "Tab inserts a tab character" and Shift+Tab "on every selected line"; replacing a selection by a tab is destructive, and symmetry with Shift+Tab is the least surprising | analyst/design may confirm (G-4) |
| F-9 | Exit by Enter-twice removes **all trailing line feeds** of the block, as the prototype does | spec: "the two trailing line feeds are removed"; with exactly one trailing LF before the second Enter this is identical | frontend |
| F-10 | `www.` rule: accepted when at least one character that is not `/?#` or whitespace follows `www.`; the result is `https://` + input and must then pass the server's address rule | spec: "non-empty host"; the prototype's `www.x.y` regex is stricter than spec. Pinned by a table test | frontend |
| F-11 | The editor uses an **explicit extension list**, not `StarterKit` | v3 `StarterKit` also brings link, underline, trailing node and list keymaps with their own Tab bindings; ADR-0037 decision 7 needs each of those configured. A test asserts the extension name list so an accidental addition fails | frontend |
| F-12 | Mark input rules (`**`, `*`, `_`, `~~`, `==`) are **off**; only the design's D-4 rules stay (`### `, `#### `, `- `, `* `, `1. `, backticks, ```` ``` ````) | design D-4 lists exactly those | frontend |

## 3. Files

### 3.1 New and changed, by zone

```
src/frontend/
  package.json, package-lock.json        # dependencies § 3.2
  vite.config.ts                         # § 10.1: fs.allow, emoji virtual module, bundle report
  eslint.config.js                       # F-4
  bundle-budget.json                     # § 10.2 (numbers filled after the first measurement)
  scripts/check-bundle.mjs               # § 10.2
  src/
    app/theme/
      richText.ts                        # theme.richText: palette, sizes, code-token colours (§ 6.5)
      richText.test.ts                   # every vocabulary token mapped, light + dark, contrast
      theme.ts, muiAugmentation.d.ts     # 3 + 6 lines: register the key (re-read before editing)
    shared/lib/richText/                 # PURE: no React, no TipTap, DOMParser only (§ 4)
      vocabulary.ts                      # typed view of contracts/rich-text/vocabularies.json
      normalise.ts                       # normaliseRichText(raw) (§ 4.2)
      whitespace.ts                      # isUnicodeWhiteSpace (the .NET Rune.IsWhiteSpace set)
      linkAddress.ts                     # readLinkAddress (port of LinkAddress.TryRead), promptAddress
      autolink.ts                        # findAutoLinks(text), trimAddress
      legacy.ts                          # legacyToHtml(plain)
      limits.ts                          # constants + checkDescription()
      pasteReduce.ts                     # client-only pre-pass before the normaliser (§ 7.5)
      spreadsheetPaste.ts                # tab-separated text -> rows (§ 7.6)
      index.ts
      *.test.ts, fixtures.test.ts        # § 11
    shared/ui/RichTextEditor/
      RichTextEditor.tsx                 # PUBLIC: lazy wrapper, Suspense, failure state, helper row, live region
      loadEditorImpl.ts                  # () => import('./EditorImpl'); the mock point for tests
      EditorImpl.tsx                     # the lazy chunk: useEditor, toolbar, menus, styled root
      copy.ts                            # Russian copy tables (§ 9)
      schema/                            # nodes and marks (§ 5)
        extensions.ts                    # the ordered list, with priorities
        rtMarks.ts                       # RtHighlight, RtColor, RtSize (class tokens, not style)
        codeBlock.ts                     # CodeBlock configured + nodeview with the chrome
        table.ts                         # Table/Row/Cell/Header configured + wrapper nodeview
        listItem.ts, heading.ts, link.ts # configured copies
      behaviour/
        keyboard.ts                      # Esc arming, Tab, Enter-twice, exits, gap cursor (§ 7.3)
        autoLink.ts                      # typed and pasted auto-link, first-undo (§ 7.4)
        paste.ts                         # transformPastedHTML/Text, handlePaste, handleDrop (§ 7.5)
        codeHighlight.ts                 # RtCodeHighlight plugin (§ 5.5)
        clearFormatting.ts, indent.ts    # commands
        loadDocument.ts                  # html -> PM JSON (F-2)
      toolbar/
        model.ts                         # TOOLBAR_GROUPS: the data (§ 6.1)
        Toolbar.tsx, useRovingToolbar.ts
        menus.tsx                        # size, colour, highlight, language, table menus
        LinkDialog.tsx                   # on shared/ui/FormDialog (§ 8.1)
        EmojiPopover.tsx, emoji/search.ts
      *.test.tsx                         # § 11
    shared/ui/FormDialog.tsx             # + event.stopPropagation() in the submit handler, + test (§ 8.1)
    features/workitems/
      api/workItemsApi.ts                # descriptionFormat on WorkItem and SaveWorkItemBody
      lib/draft.ts, rules.ts, refusals.ts   # § 9
      pages/WorkItemFormPage.tsx         # the editor replaces the TextField
      testApi.ts, testFixtures.ts        # mock follows api-contract § 1..4
    shared/api/errorCodes.ts             # two constants
    test/
      richTextDom.ts                     # jsdom polyfills (§ 11.2), imported from setup.ts
      richTextEditor.ts                  # helpers: typeIn, pasteInto, dropOn, htmlOf
      noRawHtml.test.ts                  # F-4
contracts/rich-text/fixtures/*.json      # frontend-added cases (§ 4.6) - shared files, see § 14
docs/features/feat-018-.../design.md     # measured bundle numbers at the end (owed by ADR decision 10)
```

`shared/` imports nothing from `features/`; `shared/ui/RichTextEditor` imports
`shared/lib/richText` and `shared/ui/FormDialog`; the theme is read through
`useTheme()`, never by importing `app/` (conventions). The form imports the
editor by its file path under `shared/ui` (there is no `shared` barrel today).

### 3.2 Dependencies (to pin at S1, MIT/BSD only, none with a key or paid tier)

Runtime: `@tiptap/core`, `@tiptap/pm`, `@tiptap/react` (peer: React 19 ok),
`@tiptap/extension-{document,paragraph,text,hard-break,heading,bold,italic,
underline,strike,code,link,code-block,table}`, `@tiptap/extension-list`
(BulletList, OrderedList, ListItem, ListKeymap), `@tiptap/extensions`
(UndoRedo, Gapcursor, Dropcursor, Placeholder), `lowlight`, `highlight.js`
(explicit, for the grammar imports; same major as lowlight's). Dev:
`emojibase-data` (build-time input only, § 10.1). Not used: `StarterKit`,
`extension-highlight`, `extension-text-style`, `extension-color` (they write
`style`), `extension-code-block-lowlight` (F-3), any TipTap Pro/Cloud package.
Licences are read from each package manifest, transitive `prosemirror-*`
included, at pin time (ADR-0037 decision 4). The lock file change is large;
`scripts/warm-worktree.sh` keys its cache on it, so the first install is one
`npm ci`-class cost.

## 4. The pure library (`shared/lib/richText`)

### 4.1 Vocabulary

`vocabulary.ts` imports `contracts/rich-text/vocabularies.json` (Vite JSON
import, ADR-0037 decision 6) and exports typed families: `TEXT_COLOURS`,
`HIGHLIGHTS`, `FONT_SIZES` (`{ name, class }[]`), `CODE_LANGUAGES`,
`MARK_ORDER`, and `firstToken(family, classAttr)`: split on **ASCII
whitespace only** (` \t\n\f\r`; U+00A0 is not a separator — fixture
`token-nbsp-is-not-a-separator`), exact case-sensitive match, **first valid
part in attribute order wins** (server `TokenFamily.FirstMatch`). No token
literal appears anywhere else in `src/` (a test greps for `rt-color-`,
`rt-hl-`, `rt-size-`, `language-` outside `vocabulary.ts`, tests and the theme
table, which is keyed by the imported class names).

### 4.2 `normaliseRichText(raw: string): NormalisedRichText`

```ts
interface NormalisedRichText {
  html: string;          // canonical; '' when hasContent is false (BR-RT-6)
  visibleLength: number; // BR-RT-8, code points, counted on the unblanked output
  markupLength: number;  // code points of the unblanked canonical string
  hasContent: boolean;   // a non-whitespace code point in the visible text
}
```

Total and side-effect free: never throws on any string, never touches the live
document. Steps, mirroring `HtmlRichTextReducer.ReduceCore`:

1. **Raw string**: remove U+0000; replace each lone surrogate with U+FFFD
   (a high surrogate not followed by a low one, a low not preceded by a high),
   *before* parsing, as the server does (fixture `lone-surrogate`; the fixture
   carries `{U+D83D}` as a placeholder, the test substitutes the code unit).
2. **Parse**: `new DOMParser().parseFromString('<!DOCTYPE html><html><head>
   </head><body>' + text, 'text/html')`. The document is inert (no script, no
   resource fetch, `template` content in a separate fragment). `body` that is
   not an `HTMLBodyElement` (a `frameset`) → `''`.
3. **Walk** (iterative, explicit stack, never recursion on DOM depth) into the
   same model the server builds: `Leaf(p|h3|h4)`, `CodeBlock`, `List`, `Item`,
   `Table`, `Cell`, runs with `Marks`.
4. **Write** the canonical string with an own writer (never `innerHTML`):
   WHATWG fragment form, attribute order `href` then `rel`, `&amp; &lt; &gt;`
   and U+00A0 as `&nbsp;` in text; `&amp; &quot;` and U+00A0 as `&nbsp;` in
   attributes; `br` void; counts code points as it appends.

The walk's rules are the server's, so the list below is the contract this
function is tested to (each is a fixture case or § 4.6 adds one):

| Area | Rule (server source) |
|---|---|
| Dropped with content | `script style iframe object embed template svg math noscript head title link meta base form input textarea select button audio video canvas frame frameset applet img hr`; any element outside the HTML namespace; comments, PI, CDATA |
| Unwrapped | every other unknown element (transparent: marks and text pass through) |
| Headings | `h1 h2 h3` → h3, `h4 h5 h6` → h4; a heading **not at root** (item, cell) is a paragraph; `br` kept |
| Marks | `strong/b`, `em/i`, `u`, `s/strike/del`, `code`, `a` (valid address only, inner link overrides outer), `mark` (highlight token or unwrapped), `span` (colour and size tokens from one element both apply; **inner of one family wins**, an inner element with no token keeps the outer). Written in `MARK_ORDER`, common prefix of open marks stays open (adjacent equal marks merge), code mark is written after the link and carries nothing else |
| Code over marks | a run with code carries only `Href` and `Code` (`Marks.Canonical`); a heading drops the size mark |
| Text and breaks | whitespace-only text between blocks (no open leaf) is **ASCII** whitespace only (space, tab, LF, FF, CR) and is dropped; any other text opens a paragraph; U+00A0 alone makes a paragraph (backend § 6.2) |
| `br` | written **inside the marks open at that point** (those of the preceding run, none when leading), never given marks of its own; in `pre` a line feed (backend § 6.2) |
| `pre` | at root: `CodeBlock`, language from the first direct `code` child's class via `firstToken`; text only, `br` as LF; inside an item or cell: one paragraph per line, empty lines kept as empty paragraphs |
| Lists | only at root, in an item or in a list, at most 3 deep; beyond that (and `li`/`ul` in a cell) **unwrapped, and each unwrapped item is a paragraph boundary** (backend § 6.2, fixture `lists-depth-three`); content directly in a list goes into an implicit item; `ol` has no `start`; a list or item with nothing to write is `<li><p></p></li>` (an item holding only an empty nested list gets an empty paragraph) |
| Tables | only at root (elsewhere unwrapped to cell paragraphs); `thead/tbody/tfoot` rows go into one `tbody`; cells hold paragraphs only (`CellContainer.Add` keeps leaves only); text and blocks outside any cell are placed **before** the table (foster-parenting); rows without cells and tables without rows are removed |
| Spans | `colspan`/`rowspan` read as trimmed non-negative integers of ≤ 9 digits, else 1; `colspan` clamped to **1 000**; `rowspan` clamped to the rows left **in its row group**, `0` = to the group's end; covered positions become empty cells of the spanning cell's element (`th` stays `th`); a short row is padded to the widest with empty cells of **the row's last cell's element**; table whose positions could exceed the markup ceiling (16 code points each) → treated as over the ceiling (return `markupLength` > ceiling without building the grid) |
| Link address | `readLinkAddress` (§ 4.4) |
| Class tokens | `class` read only on `mark`, `span`, and the first direct `code` of a `pre`; never copied: the writer emits the class from the vocabulary table |
| Empty | an empty block is `<p></p>`; empty inline runs are not written; an empty code block stays `<pre><code></code></pre>`; a leaf with no items is written as is |

Budgets: the client has no parse timeout; the guard against a pathological
paste is the **input size**: strings above 2 000 000 UTF-16 units skip the
walk and return `markupLength = DESCRIPTION_MAX_MARKUP + 1` (verdict
`markup-too-large`), so a paste cannot freeze the tab. This is a client convenience, never an answer
the server relies on.

### 4.3 Counting and limits

`codePointLength(s)` iterates by code point (`for…of`, or a loop over
`charCodeAt`), never `s.length`. `limits.ts`:

```ts
DESCRIPTION_MAX_VISIBLE = 100_000        // BR-RT-7 (1)
DESCRIPTION_MAX_MARKUP  = 1_000_000      // BR-RT-7 (2)
DESCRIPTION_RAW_CEILING = 2_000_000      // UTF-16 units, pre-parse (server only, client keeps below by 1)
type DescriptionVerdict = 'ok' | 'markup-too-large' | 'too-long';
checkDescription(n: NormalisedRichText): DescriptionVerdict
// markup first, then visible: "over both" answers markup (BR-RT-7, US-1)
// both are checked before blank (BR-RT-6): a blank value over the ceiling is refused, not ''
```

100 000 exactly is within; the visible count is the normaliser's own
accumulation (one LF between consecutive leaf blocks, one per `br`, a cell and
a list item add none, an empty paragraph is a leaf of length 0, U+00A0 is 1).

### 4.4 Link address

`readLinkAddress(raw): string | null` ports `LinkAddress.TryRead`: trim leading
and trailing code units ≤ U+0020; remove ASCII tab, LF, CR anywhere; the scheme
is `[A-Za-z][A-Za-z0-9+.-]*` before the first `:` (position > 0); `http`/
`https` (case-insensitive) need `//` **immediately** after the colon and a
non-empty host (authority up to `/ ? #`; no code unit ≤ U+0020, U+007F, `\`,
`< > " \` { } | ^`; userinfo stripped at the last `@`; port is not a host; a
bracketed host kept); `mailto` needs a non-empty address before any `?` with no
control or space. Result: the address as read (not re-encoded, not resolved).
`https:example.org` and `https:/x` are refused. `promptAddress(input)` =
trim; if it matches `^www\.` with a non-empty remainder (F-10) prefix
`https://`; then `readLinkAddress`. `findAutoLinks(text)` (§ 7.4) returns
`[start, end, href]` ranges.

### 4.5 `legacyToHtml(plain: string): string`

Per BR-RT-9 and `legacy.json`: remove U+0000, replace lone surrogates with
U+FFFD, `\r\n` and lone `\r` → `\n`, split at `\n`; each line is
`<p>` + escape(line) + `</p>` with `&`, `<`, `>` and **U+00A0 as `&nbsp;`**
(so the output is a fixed point of `normaliseRichText`, which the server also
asserts of `legacy.json.html`); an empty line is `<p></p>`; spaces and tabs
are kept. `''` → `''` (not `<p></p>`: the baseline of an empty value must
equal the normalised empty editor). Linear time; the worst cases of
`legacy.json` (100 000 `&`, 100 000 LF) are generated in the test.

### 4.6 Fixture runner and the cases the client must add

`fixtures.test.ts` loads the four files with `readFileSync` (tests may; the
production bundle imports only `vocabularies.json`):

| File | Client assertion |
|---|---|
| `canonical.json` | for **every** case: `normalise(input).html === canonical`; when `editorOutput`: `normalise(editorOutput).html === canonical`; `normalise(canonical).html === canonical` (idempotent). Component level (C): load `canonical` in the real editor, `getHTML()`, normalise = `canonical` (US-3) |
| `visible-count.json` | `visibleLength === count` (placeholder substitution for `{U+D83D}`; `stored` equals `html` after normalise) |
| `hostile-corpus.json` | `normalise(input).html` holds none of `mustNotContain` (case-insensitive) and is idempotent; component level: load `input` itself into the editor and assert the document, `getHTML()` and `view.dom.outerHTML` hold none of it, no `script`, no `on*`, no `style`, no `id`/`name` (US-3 "outside the subset is not rendered") |
| `legacy.json` | `legacyToHtml(plain) === html`; `normalise(html).html === html`; `visibleLength === count`; `markupLength` for the generated worst cases = 500 007 and 700 007 |

Cases to **add** (frontend-authored, `story` US-7/US-4, run by the server suite
too — the plan-backend § 5.3 contract: "both suites run the whole file"). Each
pins a § 6.2 choice that has no shared case today; if the server disagrees the
case is wrong or the server is, and either way it is found before QA:

1. `br` after a marked run (`<p><strong>a</strong><br>b</p>` →
   `<p><strong>a<br></strong>b</p>`) and a leading `br`.
2. Blank by `Rune.IsWhiteSpace`: `<p> </p>`, `<p>　</p>`, `<p>\u0085</p>`
   are **blank** (`""`); `<p>​</p>` and `<p>﻿</p>` are **not**
   (JS `\s` would call U+FEFF whitespace and U+0085 not: `isUnicodeWhiteSpace`
   is an explicit set, `[\t-\r \u0085   -   
     　]`).
3. ASCII-only dropping between blocks: `<p>a</p> <p>b</p>` has a middle
   paragraph.
4. `https:example.org`, `https:/x`, `http://`, `http://:80`, `mailto:`,
   `mailto:?subject=x` refused; `http://u@h`, `HTTPS://EXAMPLE.ORG` kept.
5. `colspan="1001"` clamp, `rowspan="0"`, a rowspan crossing `thead`/`tbody`.
6. Empty `li`, empty cell, an item holding only an empty list.
7. A `th` row padded with `th`, a `td` row padded with `td`.
8. Text outside a cell inside a table (foster-parented before the table).
9. A link with only an image (exists: `images-dropped`) and nested links.

## 5. Editor schema and extensions

### 5.1 Extension list (ordered; priorities fix the canonical mark order)

| # | Extension | Configuration (the canonical-form rule it enforces) |
|---|---|---|
| 1 | `Document` | `content: 'block+'` (a document may end in a table or code block) |
| 2 | `Paragraph`, `Text`, `HardBreak` | defaults; `HardBreak` `keepMarks: true` |
| 3 | `Heading` | `levels: [3, 4]`; `marks: 'link bold italic underline strike rtHighlight rtColor code'` (no `rtSize`); input rules `### `, `#### ` only; parse `h1 h2 → 3`, `h5 h6 → 4` is **not** done here (the normaliser runs first) |
| 4 | `BulletList`, `OrderedList` | `start` attribute removed (not parsed, not rendered); `ListKeymap` kept (Backspace/Delete in lists) |
| 5 | `ListItem` | content `paragraph (paragraph \| bulletList \| orderedList)*`; shortcuts **only** `Enter` (split), `Mod-]` (sink), `Mod-[` (lift): the stock `Tab`/`Shift-Tab` are removed (BR-RT-20) |
| 6 | `Link` (priority 1000) | `openOnClick: false`, `autolink: false`, `linkOnPaste: false`, `inclusive: false`, `HTMLAttributes: { rel: 'noopener noreferrer', target: null, class: null }`, `isAllowedUri: readLinkAddress(u) !== null`; parse only `a[href]` that passes it |
| 7 | `Bold` 900, `Italic` 800, `Underline` 700, `Strike` 600 | parse rules tags only (`strong`, `b` unless `font-weight: normal`, …); style recognition happens in the paste pre-pass at the spec's thresholds, not here; input rules off (F-12) |
| 8 | `RtHighlight` 500, `RtColor` 400, `RtSize` 300 | own marks: attr `token` (vocabulary `name`); parse `mark`/`span` by `firstToken`; render `mark`/`span` with `class` from the table only; never `style` |
| 9 | `Code` 200 | `excludes: 'bold italic underline strike rtHighlight rtColor rtSize'` (every mark **but link**); other marks do not exclude code but ProseMirror's symmetric check makes "code wins" (adding bold to code text is a no-op, adding code strips the rest); input rule backticks kept |
| 10 | `CodeBlock` | `isolating: true`, `marks: ''`, `code: true`, `defining: true`; attr `language` restricted to `CODE_LANGUAGES` names else `null`; parse `pre > code[class]` through `firstToken`; render `<pre><code class="language-x">`; `exitOnTripleEnter: false`, `exitOnArrowDown: false` (own exits, § 7.3); `defaultLanguage: null`; node view with the strip chrome (§ 5.4) |
| 11 | `Table` | `resizable: false`, `allowTableNodeSelection: false`; **`renderHTML` returns `['table', ['tbody', 0]]`** (no `colgroup`, no `style`); node view with a scroll wrapper (§ 5.4) |
| 12 | `TableRow`, `TableCell`, `TableHeader` | cell content `paragraph+`; attrs `colspan`/`rowspan` kept at default 1 because `prosemirror-tables` reads them, but `parseHTML` always yields 1 and `renderHTML` writes none; `colwidth` not registered; merge, split, resize commands never wired (no keymap, no toolbar control) |
| 13 | `UndoRedo` | `Mod-z`, `Mod-y`, `Shift-Mod-z` |
| 14 | `Gapcursor`, `Dropcursor` | gap cursor is how a final table/code block is left (§ 7.3) |
| 15 | `Placeholder` | «Добавьте описание», `showOnlyWhenEditable: true` |
| 16 | `RtKeyboard`, `RtAutoLink`, `RtPaste`, `RtCodeHighlight` | own, § 7 and § 5.5 |

**Not registered** (so no shortcut or rule can create them, design § Toolbar
last paragraph): blockquote, horizontal rule, task list, text align, trailing
node, `Selection`/`Focus`/`CharacterCount`, `Image`. A test lists
`editor.extensionManager.extensions.map(e => e.name)` and compares it with the
table, so a bump that re-adds one fails.

### 5.2 Schema rules, one line each (ADR-0037 decision 7 → where it is enforced)

| Rule | Schema | Normaliser (second line) |
|---|---|---|
| Mark order link > bold > italic > underline > strike > highlight > colour > size | priorities above | `MARK_ORDER`, merge |
| Code keeps only a link | `Code.excludes` | `Marks.Canonical` |
| Code block holds text only | `marks: ''` | `pre` text only |
| Heading drops size | `Heading.marks` | `Canonical(inHeading)` |
| List item / cell hold paragraphs (and nested lists) only | content expressions | unwrap per line |
| Table rectangular, no spans | spans fixed at 1, paste normalised before parse | `TableNormaliser` port |
| No trailing paragraph stored | `trailingNode` absent, `block+` | n/a |
| Header row | `toggleHeaderRow` | `th`/`td` kept as is |

Known harmless difference: a list item that **starts with a nested list**
(valid server canonical) gains an empty paragraph in the editor (strict
`paragraph` first). It is one-way and idempotent after the next save, and
cannot occur from editor-made lists; recorded so the first QA run does not
mistake it for a defect. If the human wants byte stability there too, the
server rule or the item content expression changes (not planned).

### 5.3 Whitespace and loading

`loadDocument(html)`: build the DOM with `DOMParser` (as § 4.2 step 2, but
**no walk**: the input is already canonical or `legacyToHtml` output), run
`PMDOMParser.fromSchema(schema).parse(body, { preserveWhitespace: 'full' })`,
`doc.toJSON()`, and hand JSON to `useEditor({ content })` (F-2).
`preserveWhitespace: 'full'` is required: canonical text keeps leading spaces,
tabs and runs of spaces (`indentation-kept`), which the default parse would
collapse. Paragraphs render with `white-space: pre-wrap`. The empty string
loads one empty paragraph.

### 5.4 Node views (DOM only, no React node views)

- **Code block**: `div.rt-codeblock > [div.rt-codeblock-strip contentEditable=false > span.label +
  button copy tabIndex=-1] + pre > code(contentDOM)`. The strip is chrome:
  never in `renderHTML`, never serialised (fixture `editor-code-block-chrome`).
  The label follows the node's `language` in `update()`. Copy writes
  `node.textContent` through `navigator.clipboard.writeText` and calls the
  editor's `announce('Код скопирован')`. `ignoreMutation` ignores the strip.
- **Table**: `div.rt-table-scroll > table > tbody(contentDOM)`; the wrapper
  scrolls horizontally (`overflow-x: auto`), cells have a `min-width` from the
  theme, the caret cell gets the 2 px inset frame from a
  `Decoration.node` class on the cell holding the selection head (CSS
  `:focus-within` cannot name a cell inside one contenteditable). Cell-range selection styling
  (`selectedCell`) is added in the same styled root.

### 5.5 `RtCodeHighlight` (F-3)

A plugin with a `DecorationSet` state, keyed by block position. On every
transaction: map the set; mark the touched code blocks dirty; a 150 ms idle
timer (cleared by the next transaction) then, **for dirty blocks only**,
recomputes: skip when the language is `null`/outside the list, when the block
is > 20 000 code points or > 1 000 lines (D-9), else
`lowlight.highlight(language, text)`, walk the hast tree and emit
`Decoration.inline(from, to, { class })` per leaf. On first mount all blocks
compute once, also after the idle delay (never on the typing path). The
`lowlight` instance registers exactly the 12 grammars from
`highlight.js/lib/languages/*` (`xml` serves `html`; an alias test asserts the
12 vocabulary names all resolve). Token classes map to the design's eight
theme classes by CSS in the styled root (`.hljs-keyword, .hljs-literal,
.hljs-name, .hljs-selector-tag` → `keyword`, … the full map in
`codeHighlight.ts` as data, validated against the highlight.js scope names at
S7; compound scopes arrive as two classes, `hljs-title function_`). The
decorations are not nodes: `getHTML()` and the normaliser never see them
(asserted by a test: the same document with colouring on and off serialises
equal, and a colour-only repaint is never a dirty form).

## 6. Toolbar, palettes, theme

### 6.1 Data model

```ts
interface ToolbarControl {
  id: string;                         // stable, used in tests and roving
  name: string;                       // Russian accessible name (copy.ts)
  shortcut?: string;                  // 'Mod+B' -> «Ctrl+B» / «⌘B» in the tooltip
  icon?: SvgIconComponent; text?: string;   // glyph: icons-material or 'H3'/'H4'
  kind: 'toggle' | 'action' | 'menu' | 'popover';
  state(ctx: ToolbarContext): { pressed?: boolean; disabled?: boolean; value?: string };
  run(ctx: ToolbarContext, arg?: string): void;
  menu?: { label: string; value: string }[];   // palettes, sizes, languages, table ops
}
interface ToolbarGroup { id: string; label: string; controls: ToolbarControl[] }
export const TOOLBAR_GROUPS: readonly ToolbarGroup[] = [ history, block, marks, lists, links, code, table ];
```

`ToolbarContext` is a **derived snapshot** from one `useEditorState` selector
(no re-render per transaction of the whole page): `inCode` (inline), `inCodeBlock`,
`inTable`, `inCell`, `inListItem`, `listDepth`, `headingLevel`, `inLink`, `marks`
`{ bold, italic, underline, strike, code, highlight, colour, size }`,
`canUndo`, `canRedo`, `selectionEmpty`, `disabled` (saving). `disabled` of a
control = the design rule below **or** `!editor.can()…` of its command; a test
asserts each design rule independently of `can()`.

### 6.2 Controls (26, design § Toolbar), with the state rules

| Group id | Controls (id → name) | Disabled when |
|---|---|---|
| `history` | `undo` «Отменить», `redo` «Повторить» | no step |
| `block` | `h3` «Заголовок 3», `h4` «Заголовок 4», `size` «Размер шрифта» (menu) | headings: in a table cell is allowed, in a list item/ code block not (schema); size: in a heading, in inline code, in a code block |
| `marks` | `bold` «Полужирный», `italic` «Курсив», `underline` «Подчёркнутый», `strike` «Зачёркнутый», `code` «Код в строке», `colour` «Цвет текста» (menu), `highlight` «Выделение цветом» (menu), `clear` «Очистить форматирование» | all but `code`, **including `clear`**: in inline code and in a code block (design table row 3); `code`: in a code block only |
| `lists` | `bullet` «Маркированный список», `ordered` «Нумерованный список», `indent` «Увеличить отступ списка», `outdent` «Уменьшить отступ списка» | lists: in a cell and a code block; indent: level 3 or first item; outdent: level 1 (only 2–3) |
| `links` | `link` «Ссылка», `unlink` «Убрать ссылку», `open` «Открыть в новой вкладке», `emoji` «Эмодзи» | link: in a code block; unlink/open: not in a link |
| `code` | `codeblock` «Блок кода», `language` «Язык кода» (menu), `copy` «Копировать код» | codeblock: in a cell or list item; language/copy: not in a code block |
| `table` | `table` «Таблица», `tableOps` «Операции с таблицей» (menu) | table: in a table, list item, code block; ops: not in a table; inside the menu: «Добавить строку выше» in the header row, «Удалить строку»/«Удалить столбец» at one row/column |

### 6.3 Reserved position for tasks B and C

`TOOLBAR_GROUPS` ends group `links` with `emoji`; the comment
`// RESERVED (feat-NNN images, mentions-links): append «Изображение», «Упомянуть»,
// «Ссылка на рабочий элемент» here, after 'emoji', before group 'code'`
marks the position. Nothing is drawn (no disabled placeholder, design). The
overflow fallback (group `links` into a `Menu` «Ещё», `MoreHoriz`) is **not
built**; `Toolbar` lays groups out from the array so it is a model change plus
one wrapper. A unit test pins group ids, control ids, and that `links` ends in
`emoji`, so adding a control is a deliberate test edit.

### 6.4 Roving tabindex (no dependency)

`useRovingToolbar(containerRef, controls)`: one `tabIndex=0` on the active
control, `-1` on the rest; `←`/`→` move over **enabled** controls with wrap,
`Home`/`End` first/last, `Enter`/`Space` activate (native button), `Tab` is
not intercepted; remembers the last control; when the active control becomes
disabled the stop moves to the nearest enabled one; handler ignores events
whose `target` is outside the toolbar DOM (menus and the dialog are portals but
React synthetic events bubble through the React tree — this also keeps an
Esc from a menu from reaching the editor's arming). Toolbar buttons call
`preventDefault()` on `mousedown` so the selection stays. After a keyboard
activation focus stays on the control; after a menu pick or an emoji, focus
returns to the body (`editor.commands.focus()`; MUI `Menu` gets
`disableRestoreFocus`). Disabled controls are wrapped in a `span` for the
tooltip (design).

### 6.5 Palettes and `theme.richText`

`app/theme/richText.ts` exports one keyed table, **keyed by the vocabulary's
class names** (`rt-color-red`, `rt-hl-yellow`, `rt-size-lg`) with `{ light,
dark }` values exactly as design § Palettes, the eight code-token colours of
§ Syntax colouring, and the sizes (`xs: 12`, `sm: 13`, `lg: 18`, `xl: 24` px,
line height 1.4; `sm`/`xs` read `typography.tableCell`/`caption`). A resolver
`theme.richText.colour(cls)` picks the column from `theme.palette.mode` (only
`light` exists today; the dark column is specified and tested now). Hex
literals live there (the colour-literal test exempts `app/theme/`). Tests:
(a) every token of the vocabulary has a mapping and every mapping a token
(ADR decision 6); (b) contrast: text colours ≥ 4.5 on paper, highlight
backgrounds ≥ 4.5 against `text.primary`/`#F5F5F5`, the eight code colours ≥
4.5 on `shell.tableHeader`/dark, using `app/theme/contrast.ts`. Content styles
are one `styled` root in `RichTextEditor` reading the theme object (`sx` only
for layout, conventions).

## 7. Behaviour

### 7.1 State carried by the editor wrapper

`EditorImpl` keeps refs, not state, for: `loadedDoc` (the PM doc right after
load), `lastReported` (string), `armed` (Esc). Props: `value`, `onChange`,
`disabled`, `label`, `placeholder`, `helperText`, `errorText`, `maxVisible`,
`counterFrom` (90 000), `copy` overrides, `onEditorReady` (`@internal`, tests).

### 7.2 Change reporting

On `editor.on('update')` (document changes only; selection moves never fire):
if `doc.eq(loadedDoc)` report `value` as loaded (the baseline string the
caller passed — exact, whatever the editor's own serialisation says);
otherwise `normaliseRichText(editor.getHTML())` and report
`{ html, visibleLength, markupLength }`; `html` is `''` when blank. Reporting is
synchronous. **Spike S3 measures it** on the 700 007-markup and 100 000-visible
worst cases; if the median per-change cost is over 50 ms the contract becomes:
report debounced 150 ms + an imperative `flush()` that the form's `submit`
calls first. The form code in § 9 is written so either works (it only sees
`onChange`). Held in reserve (not planned): a ProseMirror-document → canonical
string reader that skips the `DOMParser`, feeding the same writer.

`value` handling (controlled): when the prop changes and
`value === lastReported` do nothing (the normal echo); otherwise compare with
the current normalised string; **equal** → leave caret and undo alone; **not
equal** → replace the document and clear history by remounting the inner
editor with a bumped `key` (the spec's "replaced and the undo history
cleared"; TipTap has no history-reset API). This rule also gives the
`parent_changed` rebase and the after-save "server value" behaviour (§ 9.3).

### 7.3 Keyboard (`RtKeyboard`, priority 1000)

| Key | Rule |
|---|---|
| `Escape` | `armed = true`; returns false (nothing else on Esc). An Esc that closes a menu, the emoji popover or the dialog never reaches the editor DOM (portals; see § 6.4), so it cannot arm |
| any other key | `armedForThisEvent = armed; armed = false` — the next keystroke only (BR-RT-16) |
| `Tab`/`Shift-Tab`, armed | the code-block and table Tab handlers read `armedForThisEvent` and **return false**, so the browser moves focus out, forward or backward |
| `Tab`, code block | insert `\t` at the caret; non-collapsed selection: tab at the start of each selected line (F-8); `Shift-Tab`: on every selected line remove one leading tab, else up to four leading spaces |
| `Tab`/`Shift-Tab`, table cell | `goToNextCell(±1)`; when there is none **return false** (no row added; focus leaves) |
| `Tab`, elsewhere | never captured; the list item's stock binding is removed (extension 5) |
| `Enter`, code block | if the caret is at the end of the block and its text ends in LF: remove all trailing LFs, then exit (F-9); else insert LF (`exitOnTripleEnter` is off) |
| `Mod-Enter`, code block or table cell | exit |
| `ArrowDown`, code block | collapsed caret at the block's text end (nothing after it, no LF ahead): exit; otherwise default |
| `Mod-]`, `Mod-[` | sink / lift list item with the depth guards of § 6.2 |
| `Mod-k` | open the link prompt (not in a code block) |
| `Mod-z` | first press right after an auto-link removes only the link (§ 7.4), else history |

`exitBlock(state)`: the top-level node containing the selection; if a next
sibling exists, `TextSelection.near(posAfter, 1)` (its start); else
`new GapCursor(state.doc.resolve(state.doc.content.size))` — valid because the
block is `isolating` (`closedBefore`). **No transaction touches the document**
(selection only), so merely moving through a final block is not a change; the
paragraph appears only when the member types at the gap cursor (S5 asserts
that typing there creates it). The three exits and `Mod-Enter` are the only
ways; the arrow keys keep ProseMirror's own gap-cursor handling.

### 7.4 Links and auto-link (`RtAutoLink`)

`findAutoLinks(text)`: tokens split on Unicode whitespace; a token qualifies
when, after trimming trailing `. , ; : ! ? » " '` and an **unbalanced** `)` or
`]` (balanced pairs stay, `https://example.org/a_(b)`), it starts with
`http://`, `https://`, `mailto:` or `www.` (scheme case-insensitive) **and**
passes `promptAddress` (non-empty host). Bare domains, bare e-mails, file
names and `ftp://` do not qualify. Not applied: inside inline code, a code
block, an existing link, and never to a loaded legacy value (it is not typed).

- **Typed**: `handleTextInput` for `' '`, and Enter via `handleKeyDown`
  (running the stock Enter through `editor.commands.keyboardShortcut('Enter')`
  first). The link transaction is a **separate dispatch** after the typed
  character with `closeHistory(tr)` set on it, so history opens a new event for
  the link; the first `Mod-z` undoes only the link, the next the typing group.
  (An `appendTransaction` is not used: appended transactions are merged into
  the original's history event, S5/S8 prove the grouping.)
- **Pasted**: in `handlePaste` for plain-text-only clipboards (F-6): one
  dispatch inserts the content, a second with `closeHistory` adds the marks over
  the inserted range, so the first undo removes only the links. A single
  address over a **non-empty selection** links the selection and keeps its
  text.
- **`Mod-Shift-v`**: a key tracker marks the next paste plain: no auto-link, no
  link-over-selection, no table (ProseMirror's own shift-paste supplies plain
  paragraphs through our `clipboardTextParser`, F-7).
- The `Link` mark itself is created only through `setLink` with an address that
  passed `readLinkAddress` (defence in depth: `isAllowedUri`).

### 7.5 Paste and drop (`RtPaste`, `pasteReduce`)

Order for `text/html`: `transformPastedHTML(html)` → `pasteReduce` (client-only
recognisers) → `normaliseRichText` (the server's rules, F-1) → ProseMirror's
schema parse. Because the normaliser already unwraps tables and per-line `pre`
inside containers, merged cells, widths, lists in cells and images never reach
the parser.

`pasteReduce` (DOM pre-pass on an inert document; all rules are the spec's,
none touch the server's reducer, which never reads `style`):

1. **Bold/italic/underline/strike from style**: `font-weight` ≥ 600 or `bold`/
   `bolder` → wrap in `strong`; `font-style: italic|oblique` → `em`;
   `text-decoration(-line)` containing `underline` → `u`, `line-through` →
   `s`. A `b` (or `strong`) declaring `font-weight: normal|400` is **not bold**
   (the Google-Docs wrapper): it is unwrapped, so only its children's own
   declarations count. All other styles are discarded by the normaliser.
2. **Foreign colour, background, size** are never mapped to the palette
   (BR-RT-15); they vanish at the normaliser.
3. **Code from an IDE or page**: an element that is `pre`, or has inline
   `white-space: pre|pre-wrap` together with a monospace `font-family`, becomes
   one `<pre><code>` of its plain text — lines from block children, `br` and
   source newlines; token spans and styles are gone; a `language-<name>`
   class on the element or its `code` is kept **only** when in the closed list.
4. **Images**: any `img` (or a `data:` source) is dropped and
   `onNotice('image')` fires (D-7), also when it was the only image of rich
   content. A `files` clipboard or drop with no text/html/plain does the same
   (`handlePaste`/`handleDrop` return true, `preventDefault`, so the browser
   never navigates to the file; the `drop` default of ProseMirror would not).
5. Inside a **code block**: ProseMirror's code context inserts text verbatim
   (tabs kept); html there is inserted as its text. Inside a **cell** or
   **list item**: tables and code blocks become paragraphs (the normaliser);
   `prosemirror-tables`' own `handlePaste` (which overwrites cell ranges) is
   pre-empted by our plugin returning true after dispatching the paragraphs
   (S4).

### 7.6 Plain text and spreadsheet evidence

`clipboardTextParser(text, $context, plain)`: split on `\r\n|\r|\n`, each line a
paragraph, blank lines **kept** (F-7); in a code block, ProseMirror's own
verbatim path. A tab-table is built **before** that, outside a table and a
code block, only when not `plain` (Ctrl+Shift+V) and the clipboard has no
`text/html` table: ≥ 3 lines (one final empty line from a trailing LF
ignored), every line with the same number n ≥ 1 of tabs → a table of those
cells, **no header row** (`td` throughout), one history step. An html table
(spreadsheet) arrives through § 7.5 and has `td` only, hence no header row.

## 8. Prompts and popovers

### 8.1 Link prompt (`LinkDialog` on `shared/ui/FormDialog`)

Title «Ссылка»; «Адрес ссылки» (placeholder «https://…», autofocus), prefilled
with the address when the caret is in a link; «Текст ссылки» only when the
selection is empty and the caret is not in a link; «Отмена» and the filled
«Применить»; Enter applies. On apply: trim; empty → «Укажите адрес ссылки.»;
`promptAddress` null → «Допустимы адреса, начинающиеся с http://, https://,
mailto: или www.» as `error` + `helperText` on the field, dialog stays. On
success: selection → `setLink`, nothing selected → insert the text (address
when «Текст ссылки» is empty) as a linked run, in a link → change the address.
On close (apply, «Отмена», Esc) focus and the selection return to the editor.
`unlink` removes the mark over the link's extent; `open` calls
`window.open(href, '_blank', 'noopener,noreferrer')` (Ctrl/Cmd+click does the
same through `handleClick`; a plain click only places the caret).

**Portal submit bubbling fix**: `FormDialog`'s `onSubmit` gains
`event.stopPropagation()` (type `{ preventDefault(): void; stopPropagation():
void }`). New test in `FormDialog.test.tsx`: render `<form onSubmit={outer}>`
around a `FormDialog`, submit the dialog, `outer` not called, `onSubmit` once.
A second test at page level: applying a link on the form does not call save
(no PATCH). Existing users (`CreateWorkItemDialog` etc.) are unaffected: none
sits inside an outer form.

### 8.2 Emoji picker

MUI `Popover` (`role="dialog"`, name «Эмодзи») under the button, `TextField`
«Поиск эмодзи», grid of plain `<button>` elements (MUI `ButtonBase` ×1 800 is
too heavy), 8 columns, roving: ←→ by one, ↑↓ by a row, ↑ from the first row to
the search; Enter on the first match or a focused emoji inserts it as a
**text node** (`insertContent({ type: 'text', text })`, never as parsed html)
and returns focus to the body; Esc closes and returns to the button. Data:
`import('virtual:rich-text-emoji')` on first open (own chunk, § 10.1): tuples
`[emoji, label(ru), tags(ru), group]`, **flags and the component group
omitted** (design, ADR), no skin tones. Search: case- and `ё`/`е`-folded
substring over label and tags. Nothing found → «Ничего не найдено».
`content-visibility: auto` per group keeps first open under ~150 ms (manual
QA observation, S8 measures the node cost).

## 9. Copy, states, announcements

All strings are the stories'/design's (no invented text). Constants in
`shared/ui/RichTextEditor/copy.ts` (Russian default, as `FormDialog` carries
«Отмена»); feature-specific strings are props.

| Where | Text |
|---|---|
| Toolbar group | «Форматирование описания» |
| Controls | § 6.2 names; tooltip «Полужирный (Ctrl+B)», «⌘B» on macOS |
| Palettes | colours «Серый», «Красный», «Оранжевый», «Зелёный», «Бирюзовый», «Синий», «Фиолетовый», «Розовый», reset «Обычный цвет»; highlights «Жёлтый», «Оранжевый», «Красный», «Зелёный», «Бирюзовый», «Синий», «Фиолетовый», «Серый», reset «Без выделения»; sizes «Очень мелкий», «Мелкий», «Крупный», «Очень крупный», reset «Обычный размер» |
| Languages | «Простой текст», «JavaScript», «TypeScript», «JSON», «HTML», «XML», «CSS», «SQL», «C#», «Python», «Bash», «YAML», «Markdown» |
| Table menu | «Добавить строку выше», «Добавить строку ниже», «Добавить столбец слева», «Добавить столбец справа», «Удалить строку», «Удалить столбец», «Строка заголовка», «Удалить таблицу» |
| Code | «Копировать код», status «Код скопирован» |
| Placeholder / helper | «Добавьте описание» (also `aria-placeholder`) / «До 100 000 символов» (the form passes `TEXT_HELPER`) |
| Counter | «98 450 / 100 000» (`Intl.NumberFormat('ru-RU')`, from 90 000; ru-RU groups with U+00A0 — tests build the expected value with the same formatter), accessible name «Символов: 98 450 из 100 000», not live per keystroke |
| Limits | «Не более 100 000 символов.» (live above 100 000, `role="alert"` once when it appears), «Слишком много разметки. Упростите форматирование или сократите текст.» (on save, wins over the first) |
| Notice | «Изображения в описание не вставляются.»; code hint «Tab вставляет отступ. Esc, затем Tab — выйти из блока.»; table hint «Tab переходит между ячейками. Esc, затем Tab — выйти из таблицы.» |
| Link prompt | § 8.1 |
| Chunk | hidden «Загрузка редактора»; «Не удалось загрузить редактор.» + «Повторить» |
| Emoji | «Эмодзи», «Поиск эмодзи», «Ничего не найдено» |

### 9.1 States (design § States → implementation)

| State | Implementation |
|---|---|
| Form loading | unchanged `FormLoading`; the editor is mounted only after the item is read, so stored text never shows as plain |
| Editor code loading | `React.lazy` inside `Suspense`; fallback `Skeleton variant="rounded"` for the toolbar and six lines inside a box with `aria-busy`, hidden «Загрузка редактора» (`theme.mixins.visuallyHidden`), helper row already rendered by the wrapper; **the form's «Сохранить» works** (untouched → no description) |
| Chunk failure | local error boundary (a class component in `RichTextEditor.tsx`; the app's `ErrorBoundary` is screen-level) → `Alert severity="error"` «Не удалось загрузить редактор.» + «Повторить»; retry bumps an `attempt` state and recreates the `lazy` component (`useMemo`), because a rejected `lazy` is cached forever; S6 checks the browser's own failed-module cache (Chrome), the fallback if it does not retry is the same button doing `location.reload()` after a second failure — flagged in § 13 |
| Empty | placeholder; helper |
| Focused | card `focus-within` 2 px `primary.main`; toolbar focus ring from the theme |
| Disabled (saving) | `setEditable(false, false)`, `aria-disabled`, every toolbar control disabled, menus and prompt closed; same content, size, look as a disabled field; focus not moved |
| Over visible limit, live | counter `error.main` weight 600, border `error.main`, helper slot shows «Не более 100 000 символов.»; Save not disabled, pressing it sends nothing |
| Over markup ceiling | on save only (the string is built from the editor's last report); wins over the visible message; input kept |
| Server refusal | the field reason text from `errors.description` in the same slot (existing `placeRefusal`); `description_format_unknown` goes to the form banner (§ 10.2); input kept |
| After a save | § 7.2 value rule |
| Read-only | not built (spec/design) |

### 9.2 Live region

One `role="status"` `aria-live="polite"` element under the card, **always in
the DOM**. Messages: the image notice (cleared on the next edit or blur; a
second drop re-announces: set `''` in a microtask, then the text); the Tab
hint on the **transition** into a code block or table (a `useEditorState`
selector on `{ inCodeBlock, inTable }`, not per caret move), cleared on
leaving or blur; «Код скопирован». `aria-describedby` on the body lists the
helper, the counter and this region.

## 10. Lazy loading, vite config, bundle

### 10.1 `vite.config.ts` (re-read before editing; the file also carries test config)

1. `server.fs.allow`: `[searchForWorkspaceRoot(process.cwd()), <repo root>]` so
   the dev server serves `contracts/rich-text/vocabularies.json` (the import
   crosses the `src/frontend` root; tests already read `contracts/` by path).
   A deployment build context must therefore include `contracts/` (nothing in
   the repo builds the panel in a container today; noted).
2. `emojiDataPlugin()`: virtual module `virtual:rich-text-emoji`. At build and
   in dev it reads `emojibase-data/ru/compact.json`, drops group *flags* and
   *component*, drops skins, keeps `[emoji, label, tags, group]` ordered by
   `order`, emits a JS module. Used from `emoji/` only via dynamic `import()`,
   so Rollup makes it its own chunk. A tiny `.d.ts` declares the module.
3. `bundleReportPlugin()` (build only): in `generateBundle` writes
   `dist/bundle-report.json`: per chunk `fileName`, `isEntry`,
   `isDynamicEntry`, gzip size (`node:zlib`), and module ids.
4. No `manualChunks`: dynamic imports already split the editor; entry/vendor
   layout stays as it is.

### 10.2 Budget and the check

`bundle-budget.json`: ceilings in gzipped bytes for (a) the entry chunk's
**growth** over the recorded baseline, (b) the editor chunk, (c) the emoji
chunk, and the forbidden-module patterns for the entry's static closure
(`prosemirror`, `@tiptap`, `lowlight`, `highlight.js`, `emojibase`,
`virtual:rich-text-emoji`). `scripts/check-bundle.mjs` (`npm run
bundle:check`, run after `npm run build`; needs `VITE_API_BASE_URL`) reads the
report and fails over any ceiling or any forbidden module in the entry
closure. **The baseline and the three ceilings are measured on the first
build of slice 3 and written to `design.md` and the budget file; the ADR asks
the human to confirm or change them there.** Orientation only, not
measurements: ProseMirror + TipTap + extensions roughly 100–140 KB gzipped,
the twelve grammars with lowlight 30–40 KB, emoji data 35–50 KB before trim;
if the editor chunk exceeds the confirmed ceiling the ADR's fallbacks apply in
order (per-language grammar loading, then no colouring).

### 10.3 The all-lazy alternative (for F-5, if the human wants a literal zero)

Move `normaliseRichText` into the editor chunk; the editor reports
`{ html, visibleLength, markupLength }` and the draft carries
`descriptionCounts`; `draftErrors` reads them. Costs: draft shape change,
`legacyToHtml` stays in main (4 KB-class), a second path when the editor never
loaded (nothing to check there: untouched). Not recommended: it spreads state
for ~5 KB.

## 11. Form integration (`features/workitems`)

### 11.1 Contract plumbing (`api-contract.md`, nothing patched)

- `workItemsApi.ts`: `DescriptionFormat = 'plain' | 'html'` (closed set,
  `as const`); `WorkItem.descriptionFormat`; `SaveWorkItemBody.descriptionFormat?`.
  `WorkItemListItem` unchanged (it omits `description`). Create unchanged.
- `errorCodes.ts`: `descriptionMarkupTooLarge`
  (`workitems.workitem.description_markup_too_large`), `descriptionFormatUnknown`
  (`workitems.workitem.description_format_unknown`) next to `descriptionTooLong`.
- `refusals.ts`: `descriptionFormatUnknown` is placed like `fieldNotOnType`
  (a defect, not user input): **the banner**, not under the editor, although the
  server reports it under `errors.description` (api-contract § 4 notes). The
  two limit reasons need nothing: `VALIDATED_FIELDS.description` already puts
  their `detail` under the field.
- Test mock (`testApi.ts`, `testFixtures.ts`): records carry
  `descriptionFormat` (default `plain`); PATCH implements § 1.1/§ 1.2/§ 3: unknown
  format → `description_format_unknown` with or without a description; plain
  stored as sent; html stored as sent after `normaliseRichText` (the mock's
  "reducer" is the client's, so reduction scenarios override the response with
  `server.use`); the visible/markup limits by the same function; a blank html
  value → `("", plain)`; equality of the (value, format) pair for `changedAt`.

### 11.2 `lib/draft.ts` and `lib/rules.ts`

```ts
descriptionBaseline(item): string
  // item.descriptionFormat === 'html' ? item.description : legacyToHtml(item.description)
  // one-entry memo on (description, format): called several times per render

draftOf(item).description = descriptionBaseline(item)      // opening is not dirty (BR-RT-11)

changesOf: if (draft.description !== descriptionBaseline(item))
             { body.description = draft.description; body.descriptionFormat = 'html' }
// the pair is sent together, only then; no description key otherwise

draftErrors: description is checked only when it differs from the baseline:
  checkDescription(normaliseRichText(draft.description)):
    'markup-too-large' -> ruleTexts.descriptionMarkupTooLarge  (design copy)
    'too-long'         -> ruleTexts.descriptionTooLong
```

`DESCRIPTION_MAX_LENGTH` (plain, UTF-16 units) disappears from the panel's
rules: it never writes plain. `rebaseDraft` is unchanged: it compares the
draft's string with the loaded draft's, so a touched description survives the
`parent_changed` rebase and an untouched one takes the fresh baseline (the
editor's value rule then replaces the document).

### 11.3 Save semantics table (BR-RT-10, BR-RT-11)

| Situation | Request | Why |
|---|---|---|
| Opened, nothing typed, another field changed | no `description`, no `descriptionFormat` | `draft.description === baseline` |
| Legacy plain, text edited | `description: <canonical html>`, `descriptionFormat: 'html'` | first save that changes Описание |
| Edited then edited back (doc equal to the loaded one) | none | the editor reports the baseline string while `doc.eq(loadedDoc)` |
| Stored html, formatting changed | html + `'html'` | string differs |
| Edited to blank (legacy `\n\n\n` or html) | `description: ''`, `'html'` | baseline is the unblanked html; `''` differs; server stores `("", plain)` |
| Blank legacy, only moved the caret | none | no document change |
| Over a limit | none, field error | `draftErrors` |
| Server refuses | none kept stored; `edited` is kept, field error from `detail` | existing `placeRefusal`; input kept |

### 11.4 Page

`WorkItemFormPage` replaces the `TextField` in the first `FormSection` with
`RichTextEditor` (`label="Описание"`, `value={draft.description}`,
`onChange={({ html }) => edit('description', { description: html })}`,
`disabled={busy}`, `placeholder`, `helperText={TEXT_HELPER}`,
`errorText={errors.description}`, `maxVisible={DESCRIPTION_MAX_VISIBLE}`, the
tooLong copy). `edit()` already clears the field's error as the member types.
`FormSection` keeps the `h2` and the region name. There is **no leave guard**
in the form today; US-3 "a leave-guard does not fire" is satisfied by
`isDirty` being false (a future guard reads the same function). The other
sections and the create dialog are untouched.

## 12. Tests

### 12.1 Layers

| Level | Where | Notes |
|---|---|---|
| U | `shared/lib/richText/*.test.ts`, `app/theme/richText.test.ts`, `toolbar/model.test.ts`, `draft.test.ts`, `rules.test.ts`, `refusals.test.ts` | the fixture runner (§ 4.6), link table, auto-link table, whitespace set, legacy worst cases, mark-order, baseline rules |
| C (editor) | `shared/ui/RichTextEditor/*.test.tsx` (split: `core`, `marks`, `link`, `emoji`, `table`, `code`, `paste`, `keyboard`, `states`, `a11y`) | real TipTap in jsdom; mounts `EditorImpl` directly, the lazy wrapper tested separately with `loadEditorImpl` mocked |
| C (form) | `pages/WorkItemFormPage.description.test.tsx` (new) + the adapted existing files | MSW, real editor |
| I | backend | not frontend |
| M | QA session, real browser | § 12.4 |

### 12.2 jsdom adaptation

`src/test/richTextDom.ts` (imported from `setup.ts`; additive, harmless to
other tests): `Range.prototype.getClientRects` (empty list with `item`,
`length`, iterator), `Range.prototype.getBoundingClientRect` (zero rect),
`document.elementFromPoint`, `HTMLElement.prototype.scrollIntoView`; no
`DataTransfer`/`ClipboardEvent` in jsdom → `src/test/richTextEditor.ts` builds
paste events as a plain `Event('paste')` with a `clipboardData` stub
(`getData`, `types`, `files`, `items`), and drops likewise; `userEvent.paste`
(its own DataTransfer) is used where it is enough. `navigator.clipboard` is
stubbed per test (`writeText`). The platform is non-mac in jsdom, so `Mod` is
Ctrl. **S2 proves** typing (`userEvent.type` into the contenteditable),
keymaps (`{Control>}b{/Control}`), paste, selection by
`editor.commands.setTextSelection`, and the polyfill list before anything else
is built on it. The editor instance reaches tests through `onEditorReady`
(`@internal`; TipTap types never leave `shared/ui`).

### 12.3 Existing tests that change (and only these)

| File | Change |
|---|---|
| `WorkItemFormPage.test.tsx` `openForm` | also `await screen.findByRole('textbox', { name: 'Описание' })` (lazy chunk); `description()` stays a role query on the contenteditable |
| same, « title and description changed in one save » | `userEvent.type(description(), 'Шаги')` works on contenteditable; body expectation `{ title, description: '<p>Шаги</p>', descriptionFormat: 'html' }`; `toHaveValue('Шаги')` → `toHaveTextContent('Шаги')` |
| same, « saved… reopened » | `toHaveValue('Сохранено')` → text content; `api.record(...).description` `'<p>Сохранено</p>'`, `descriptionFormat` `'html'` |
| same, « 100 001 characters » | `fireEvent.change(textarea…)` → paste of 100 001 characters through the clipboard helper (S2 confirms time; fallback: seed the item with a stored html of 100 000 and type one more); `aria-invalid` is on the contenteditable; error text unchanged; no PATCH |
| same, « server's refusal » | `toHaveValue('Длинно')` → text content |
| same, « The description is plain text » | `fireEvent.change(text with <b> and LF)` → paste; the stored value is `<p>&lt;b&gt;важно&lt;/b&gt;</p><p>шаг 2</p>` and no `<b>` element in the page (`document.querySelector('b')` null) |
| same, « Two users » | `patches(api)[0].body` `{ description: '<p>Шаги</p>', descriptionFormat: 'html' }` |
| `WorkItemFormPage.related.test.tsx:702`, `sections.test.tsx` (Tab order) | typing and `getByRole('textbox', { name: 'Описание' })` as above; the Tab-order test now passes through the toolbar's one stop |
| `CreateWorkItemDialog.test.tsx:118-120` | `toHaveValue('')` → `toHaveTextContent('')` on the form's contenteditable |
| `draft.test.ts`, `rules.test.ts`, `refusals.test.ts` | baseline, pair, checks, banner placement |
| `FormDialog.test.tsx` | + the stopPropagation test |
| `testFixtures.ts`, `testApi.ts`, `workItemsApi.test.ts` | `descriptionFormat` |

The other four form test files (`areaState`, `assignee`, `planning`) do not
type in «Описание»; they run with the real editor and only gain wait time. If
S2 measures a per-file import cost that hurts, a shared `vi.mock` of
`loadEditorImpl` with a one-line stand-in is the fallback (the editor itself
is covered by its own files).

### 12.4 Verification table (no E2E)

U unit, C jsdom component with the real editor, I API integration (backend
suite, not repeated here), M manual QA in a real browser.

**US-1**

| Scenario | Level | Test (file) | Left to M |
|---|---|---|---|
| Toolbar offers the subset and nothing beyond; Russian names | C | `toolbar/model.test.ts` (ids, names, 26, no image/mention/align/H1/H2/H5/H6/free colour) + `a11y.test.tsx` (`getAllByRole('button')` names) | theming in light/dark |
| Lists nest by buttons, not Tab; third level disabled | C | `keyboard.test.tsx` (`Mod-]`, `Mod-[`, button states, `userEvent.tab()` leaves, no nesting) | macOS ⌘ |
| Formatting survives save and reopen | C + I | form test (type, save, MSW store, reopen, same text and structure) | — |
| Shortcuts | C | `keyboard.test.tsx` (Ctrl+B, Ctrl+I, Ctrl+Z, Ctrl+Y/Ctrl+Shift+Z) | collisions with the browser |
| Undo/redo buttons | C | `core.test.tsx` | — |
| Paste reduced; Ctrl+Shift+V | C + U | `paste.test.tsx` (stubbed `clipboardData`: h1→h3, h4–h6→h4, bold/underline kept, colour/size/img gone, plain paragraphs, `Mod-Shift-v` plain), `pasteReduce.test.ts` | a real web-page/office copy |
| Styles recognised, `font-weight:normal` wrapper | U + C | `pasteReduce.test.ts` (the story's html verbatim) | — |
| Images and files not inserted | C | `paste.test.tsx` (drop and paste with `files`, `defaultPrevented`, notice text, D-7 case) | the browser's navigate-to-file default |
| Keyboard only | C | `a11y.test.tsx` (focus order: field → toolbar → body → next; Tab never indents; name «Описание»; menus and emoji from keyboard) | screen-reader announcements |
| Client visible limit; exactly 100 000 sent | U + C | `limits.test.ts`, form test (100 001 → no PATCH, text under editor; 100 000 → PATCH) | — |
| Markup ceiling; precedence | U + C | `limits.test.ts` (a generated table of 1-character cells, 1 000 001 markup / < 100 000 visible; both over → markup text) | — |
| Server refusal keeps input | C | form test (`server.use` 400 for each reason) | — |
| After save, server value, not dirty | C | form test (server reduces: response differs; editor shows it; Save disabled) | — |
| Disabled while saving | C | `states.test.tsx`, form test (deferred response) | — |

**US-3**

| Scenario | Level | Test | M |
|---|---|---|---|
| Legacy lines / indentation / CRLF / markup characters / blank lines | U + C | `legacy.test.ts` (all `legacy.json`), `core.test.tsx` (editor shows five paragraphs; whitespace kept; no `<b>` element) | a populated copy, once |
| 50 000 emoji legacy, edit, save | U + C | counting test + form test | — |
| Any length (100 000 `&`, 99 999 LF) | U + I | `legacy.test.ts` (500 003 / 700 001 markup arithmetic after one edit) | — |
| Opening does not dirty (legacy; table + nested lists) | C | form test: Save disabled, `isDirty` false, no PATCH on submit | — |
| Another field leaves Описание untouched | C + I | form test: body has no `description` for both items | — |
| First save of legacy is the first save that changes | C | form test | — |
| Blank lines only | C | form test (untouched no PATCH; edit to blank → `""`) | — |
| Editor and server agree on canonical | C + U | `canonical.parity.test.tsx` (every case through the real editor, then `normaliseRichText`), plus the U runner | — |
| Stored markup outside the subset not rendered | C | `hostile.test.tsx` (every `input` into the editor, also disabled) + `noRawHtml.test.ts` | real-browser mXSS probe |
| Other reads | I | backend | — |

**US-4 (client half; the API scenarios are the backend's I)**

| Scenario | Level | Test | M |
|---|---|---|---|
| Visible count by BR-RT-8, emoji, ZWJ, nbsp | U | `visible-count.json` runner | — |
| Lone surrogate counts 1, stored U+FFFD | U | runner with the placeholder substitution | — |
| Visible/markup limits, precedence, blank-after-limits | U | `limits.test.ts` | — |
| Equality of the pair, `descriptionFormat` on write, no declaration never sent by the panel | U + C | `draft.test.ts`, form test body assertions | — |
| Legacy worst cases fit | U | `legacy.json` generators | — |

**US-5**

| Scenario | Level | Test | M |
|---|---|---|---|
| Underline, strikethrough | C | `marks.test.tsx` | ⌘U |
| Palettes closed and named; resets; nothing free-form | C + U | `marks.test.tsx` (menu options = vocabulary, no input/number/hex), `richText.test.ts` | contrast look, light/dark |
| Marks combine, canonical order after save/reopen | C | `marks.test.tsx` (six marks, normalised string nests in `MARK_ORDER`) | — |
| Inline code carries only a link; controls disabled | C | `marks.test.tsx` | — |
| Heading takes no size | C | `marks.test.tsx` | — |
| Clear formatting (heading→paragraph, link stays, list/table stay) | C | `clearFormatting.test.tsx` | — |
| Emoji as characters, count 1, focus returns | C + U | `emoji.test.tsx` (keyboard search «улыб», Enter, text node, no image), counting fixture | native glyph look per OS |
| Add / edit / remove link; unsupported address; `www.` | C + U | `link.test.tsx`, `linkAddress.test.ts` (table incl. `https:example.org`) | — |
| Open on purpose, not by click | C | `link.test.tsx` (click places caret, no `window.open`; button/Ctrl+click calls it with `noopener,noreferrer`) | the real tab |
| Typed/pasted address; reach; exclusions; paste over selection | C + U | `autolink.test.ts` (table: `example.org`, `a@b.ru`, `setup.py`, `ftp://x.org`, `(см. https://example.org).`, `https://example.org/a_(b)`), `autolink.test.tsx` | — |
| First undo removes only the link | C | `autolink.test.tsx` (asserted explicitly, typed and pasted) | — |
| Legacy not auto-linked on open | C | `autolink.test.tsx` + form test (not dirty) | — |

**US-6**

| Scenario | Level | Test | M |
|---|---|---|---|
| Insert 3×3 with header; all operations; no merge/resize | C | `table.test.tsx` | visual |
| Tab between cells, ends leave, no row, hint, Esc-Tab, Esc-other, Esc closing a picker | C | `table.test.tsx`, `keyboard.test.tsx` (arming is one keystroke; a menu's Esc does not arm) | screen reader |
| Pasted table reduced; into a cell | C + U | `paste.test.tsx`, canonical runner | a real spreadsheet |
| Spreadsheet evidence rule; ragged/short tab text; Ctrl+Shift+V | C + U | `spreadsheetPaste.test.ts`, `paste.test.tsx` | — |
| Ctrl+Enter exits; gap cursor; paragraph only on typing | C | `table.test.tsx` (selection is `GapCursor`, `getHTML()` unchanged, then type) | — |
| Untouched final table/code block unchanged | C | `table.test.tsx`, `code.test.tsx` (dispatch count of doc-changing transactions is 0, `onChange` not called, Save disabled) | — |
| Insert code block, language list of exactly 13 | C | `code.test.tsx` | — |
| Tab/Shift-Tab, Esc-Tab, hint | C | `code.test.tsx` | screen reader |
| Exits: Enter twice, Mod-Enter, ArrowDown at end | C | `code.test.tsx` | ArrowDown on a wrapped last line |
| Copy code | C | `code.test.tsx` (stubbed clipboard, announcement, not serialised) | the permission prompt |
| IDE / `pre` paste, language class | C + U | `pasteReduce.test.ts` (VS Code-style and JetBrains-style fixtures), `paste.test.tsx` | a real IDE |
| Code block pasted into a cell / list item → paragraphs | C | `paste.test.tsx` | — |
| Nothing executes; no marks in a block | C | `code.test.tsx`, `hostile.test.tsx` | — |
| Colouring: 12 grammars resolve; cap; no change to value/behaviour | U + C | `codeHighlight.test.ts` (names, cap by cp and by lines, 150 ms idle with fake timers), `code.test.tsx` (serialisation equal with colouring) | token colours look; typing latency |

**US-7** (client half) — `canonical.json` runner and parity C test above; every
palette token, language, nested list, header-row table and link of the file
passes through the editor; mapping of tokens to theme colours is U
(`richText.test.ts`) plus M for the look. **US-2** (client half) —
`hostile.test.tsx`; the allow-list itself is the server's.

**Manual QA list for stage 7** (the human's no-E2E decision stands): real
shortcuts and macOS ⌘; real clipboard from a web page, an office editor, a
spreadsheet and an IDE; screen-reader announcements (live region, toolbar,
roving); light/dark contrast of the mapped colours; emoji glyphs per OS and
first-open speed; ArrowDown on a wrapped last line; opening speed with 100 000
visible characters (observation, not a gate); browser retry after a failed
chunk (S6); the toolbar's two-row wrap at 640 px.

## 13. Order of work (slices, small commits, green after each)

Targeted runs only while the machine is saturated (`npx vitest run <file>`);
`scripts/verify.sh` once at the end of slice 7 after the all-clear.

| # | Slice | Contents | Gate to leave |
|---|---|---|---|
| 0 | **Spike and foundation** | pin dependencies (S1); jsdom viability (S2); normaliser cost sketch (S3, on a throwaway script); vite plugins, `fs.allow`, bundle report; ESLint rule and `noRawHtml.test.ts`; polyfills | S1–S8 answered in `status.md` Log by the orchestrator (the plan does not edit it); any "no" returns here |
| 1 | **Pure library + fixtures** | vocabulary, whitespace, linkAddress, legacy, limits, `normaliseRichText`, runners for the four files, the § 4.6 cases added to the shared files and run by the backend | green fixtures; orchestrator relays the added cases to the backend (§ 14) |
| 2 | **Contract plumbing** | types, error codes, refusal placement, MSW mock to the contract; the old `TextField` untouched (still plain, no declaration — valid) | existing suites unchanged and green |
| 3 | **Editor core** | `theme.richText` + tests; `extensions.ts`, schema rules, `loadDocument`, content styles, `EditorImpl` shell, wrapper with Suspense/failure, helper row/counter/limits, disabled, history, headings, bold/italic, lists + indent, toolbar model + roving; parity C test on the whole canonical file; first bundle measurement written to the budget | US-1 editor scenarios, US-3 parity |
| 4 | **Marks, links, emoji** | RtHighlight/RtColor/RtSize, palettes menus, clear formatting, link dialog + `FormDialog` fix + tests, auto-link, first-undo, emoji chunk and popover | US-5 |
| 5 | **Tables, code, paste** | table + node view + operations, code block + chrome + exits + highlight plugin, Esc/Tab, paste pipeline, spreadsheet rule, drop/file notice, live region hints | US-6, US-1 paste |
| 6 | **Form swap** | `draft.ts`/`rules.ts` baseline and pair, page uses the editor, mock/fixtures, adapted tests (§ 12.3), the new description test file, notices | US-1 form scenarios, US-3, US-4 client |
| 7 | **Close** | a11y pass, bundle measurement final + `design.md` numbers, `npm run bundle:check`, full `verify.sh`, `docs/conventions/frontend.md` sentence (ADR owed on acceptance), self-review against the DoD | DoD of `05-frontend.md`; `qa.md` Frontend left to the QA session |

Slices 3–5 build `shared/ui/RichTextEditor` in isolation (the page is not
touched), so the form is never half-migrated; slice 6 is the only place the
page and its tests change. Each slice is several commits (library function +
its test per commit; one control group per commit in 4–5).

## 14. Risks, spikes, and what is needed from others

### 14.1 Spikes (slice 0; each has a stated decision rule)

| # | Question | Decides |
|---|---|---|
| S1 | Pin versions; confirm each API name used here (v3 split: `@tiptap/extensions`, `extension-list`, `extension-table`); licences incl. transitive; `getSchema`, `Table.renderHTML` override, `closeHistory` export, `editor.commands.keyboardShortcut`; TipTap's string-content whitespace handling | F-2 stands or is relaxed; any renamed API |
| S2 | Does ProseMirror run usefully in jsdom 27: typing via `userEvent`, keymaps, `paste`, selection, `Range` polyfills; time to paste 100 001 characters; per-file import cost | the test strategy of § 12; the `vi.mock` fallback |
| S3 | `normaliseRichText` time on the 700 007-markup legacy case and on 100 000 visible characters of ordinary text; one `getHTML()` + normalise per change | sync report (default) vs debounce + `flush()` (§ 7.2) |
| S4 | `prosemirror-tables` with spanless cells: commands, `fixTables`, its `handlePaste`, wrapper node view; header-row toggle on a pasted table | whether `RtPaste` pre-empts it as planned |
| S5 | Gap cursor after a final table/code block: typing creates a paragraph; no doc-changing transaction on moving through; `closeHistory` gives a separate undo event | § 7.3, § 7.4 as written, or a different undo mechanism |
| S6 | Failed dynamic import retried in Chrome and Firefox | the «Повторить» mechanism (fallback: reload) |
| S7 | lowlight hast shape and highlight.js 11 scope names for the 12 grammars; 20 000 cp timing | the token → theme-class map |
| S8 | `emojibase-data/ru/compact.json` fields, group ids, gzip size after trim; DOM cost of the grid | emoji chunk ceiling, grid grouping |

### 14.1a Spike results, slices 0–3 (2026-10-13, measured; S4–S6 still open)

| # | Result | Consequence |
|---|---|---|
| S1 | **Pinned, exact:** `@tiptap/{core,pm,react}` and `@tiptap/extension-{document,paragraph,text,hard-break,heading,bold,italic,underline,strike,code,link,code-block,table,list}`, `@tiptap/extensions` all **3.31.4**; `lowlight` **3.3.0**; `highlight.js` **11.11.2** (not 11.12.0: lowlight 3.3.0 depends on `~11.11.0`, so this pin leaves one copy); `emojibase-data` **17.0.0** (dev). 51 packages added; every licence field is MIT except `highlight.js` (BSD-3-Clause), transitive `prosemirror-*`, `linkifyjs`, `orderedmap`, `rope-sequence`, `w3c-keyname`, `fast-equals`, `@floating-ui/*` included. **Names confirmed:** `UndoRedo`, `Gapcursor`, `Dropcursor`, `Placeholder` in `@tiptap/extensions`; `BulletList`, `OrderedList`, `ListItem`, `ListKeymap` in `extension-list`; `Table`, `TableRow`, `TableCell`, `TableHeader` in `extension-table`; `closeHistory`, `GapCursor`, `goToNextCell`, `fixTables`, `tableEditing` in `@tiptap/pm/{history,gapcursor,tables}`; `getSchema`, `isMacOS`, `Editor` in core; `useEditorState({ editor, selector })`. **Differences from memory:** `CodeBlock` carries a VS Code paste plugin that takes any language name from the clipboard (dropped) and has `exitOnArrowUp`; `ListKeymap` binds **Tab** (removed, BR-RT-20); the stock `Table` Tab adds a row (replaced); `OrderedList` has `start` and `type` (removed); `TableCell` content is `block+`, `ListItem` `paragraph block*` (both narrowed); the list nodes add a `listItemBranchingDeleteKeymap` extension (Delete only, kept, pinned in `schema.test.ts`); `@tiptap/extension-link` pulls `linkifyjs` (59 KB raw, ~20 KB gzipped) although autolink is off — see § 14.1b. | F-2 stands: documents are loaded as ProseMirror JSON (the whole canonical file round-trips, `\n` code blocks included). F-11 stands, extension list pinned by `schema.test.ts` |
| S2 | **Yes.** In jsdom 27: typing (`userEvent.type`, or `click` then `keyboard`), keymaps (`{Control>}b{/Control}`, Ctrl+Z/Y), paste through `userEvent.paste` and through a dispatched `paste` event with a `clipboardData` stub (`defaultPrevented` honoured), selection by commands, disabling, `view.dom` inspection all work. Polyfills needed and added (`src/test/richTextDom.ts`): `Range#getClientRects`, `Range#getBoundingClientRect`, `document.elementFromPoint`, `HTMLElement#scrollIntoView`. Paste of 100 001 characters through the stub: 8 ms. **Caveats:** `editor.commands.focus('end')` followed by `user.keyboard` **loses the first character of an empty document** (jsdom selection sync); a `click` on the body first — what `userEvent.type` does — is correct. jsdom's parse5 puts text foster-parented out of a table **after** it where browsers and AngleSharp put it before; so no shared fixture pins it. Per-file import of the editor: ~1–3 s of a 4–9 s file; no `vi.mock` stand-in needed so far | § 12 strategy holds; the form tests type with `userEvent.type`/`click` |
| S3 | `normaliseRichText`, jsdom (parse5, JS): typical 2 570-char value **10 ms**; 100 000 visible characters in one paragraph **45 ms**; 100 000 visible in 100 marked paragraphs (160 700 markup) **660 ms**; the 700 007-markup legacy worst case (100 001 empty paragraphs) **4.0 s** (2.5 s of it the parser); 500 007 markup of `&` 54 ms. A real browser's native parser is not measured here | Over the plan's 50 ms on large documents, so the **debounced contract** of § 7.2 is built: the editor reports 150 ms after the last change, `RichTextEditor` takes a `ref` with `flush()` that the form's submit calls first. The form sees only `onChange` either way |
| S4 | **Yes (jsdom).** `prosemirror-tables` runs on the spanless schema: insert 3 × 3 with a header row (`insertTable`), add row above/below and column left/right, delete row, column and table, `toggleHeaderRow`, `goToNextCell` — each leaves a rectangular table with no `colspan`, `rowspan`, `style` or `colgroup` in `getHTML()`; the stock Tab (adds a row at the last cell) is removed, and the table plugin's own paste never meets a cell range because `transformPasted` turns tables into paragraphs when the caret is in a cell or list item (`paste.test.tsx`). A node view with a scroll wrapper (`div.rt-table-scroll > table > tbody`) keeps its DOM through edits (`update` answers true for the same type) | `RtPaste` pre-empts as planned |
| S5 | **Yes.** Ctrl+Enter, ArrowDown at the end of a final code block and Ctrl+Enter in a final table put a `GapCursor` after it and dispatch no transaction that changes the document (`update` never fires, `onChange` not called, Save stays disabled); typing a character there creates the paragraph (`<pre>…</pre><p>x</p>`). `closeHistory` on a separate dispatch makes the first Ctrl+Z take off only the auto-link, typed or pasted, and the second the typing (`autolink.test.tsx`) | § 7.3 and § 7.4 stand as written |
| S6 | Still open: needs a real browser (QA list). The wrapper does not rely on the browser: the chunk is loaded by its own `import()` call, and «Повторить» runs it again (a new attempt is a new call, which Chrome does not answer from its failed-module cache for a query-less URL only after a reload — the one case the QA list names) | the fallback of plan § 9.1, `location.reload()` after a second failure, is **not** built; add it if QA sees the retry fail |
| S7 | `lowlight.highlight(lang, text)` returns a hast `root` of `span` (`className: ['hljs-…']`) and `text` nodes. Scopes seen over the 12 grammars: `hljs-{attr, attribute, built_in, bullet, code, comment, emphasis, function, keyword, literal, meta, name, number, operator, params, punctuation, section, selector-class, selector-tag, string, strong, symbol, tag, title, variable}` and the compound `function_`, `class_`, `inherited__` (carried as a second class beside `hljs-title`). `html` is registered by `registerAlias({ xml: ['html'] })`. Timing in Node, no DOM: 20 000 code points **6.5–12.7 ms** for every grammar tried (javascript, sql, python, csharp, css, markdown) | The 150 ms idle and the 20 000 cp cap hold with a wide margin. The token → theme-class map is built in slice 5 over these names |
| S8 | `emojibase-data/ru/compact.json`: entries `{ hexcode, label, unicode, group?, order?, tags?, skins? }`; `messages.json` has the 10 group names. Groups 2 (component) and 9 (flags) and the 26 entries with no group are omitted: **1 644 emoji in 8 groups**, skins dropped. Chunk: **208.7 KB raw, 46.9 KB gzipped**. DOM cost of the grid not measured yet (slice 4) | Emoji ceiling proposed at 52 KB |

### 14.1b First bundle measurement (slice 3, gzipped, `bundle-budget.json`)

| What | Measured | Note |
|---|---|---|
| Entry chunk of the app today (nothing of the editor reachable) | **279 519 B** (883.7 KB raw) | One chunk; there is no shared vendor chunk today |
| The pure library (`shared/lib/richText`, vocabulary JSON included) | **≈ 6 200 B** | Its only cost to the entry; the wrapper's own code is ~6 KB raw. Target was ≤ 8 KB |
| Editor chunk (ProseMirror, TipTap, extensions, toolbar, MUI parts only it uses), **before** highlighting, marks menus, link dialog, emoji popover, node views | **161 256 B** (501.6 KB raw) | Largest parts raw: prosemirror-view 248 KB, @tiptap/core 182 KB, prosemirror-model 125 KB, prosemirror-transform 84 KB, prosemirror-tables 72 KB, linkifyjs 60 KB, @popperjs/core 59 KB (may be shared with the entry in the real build), fast-equals 27 KB |
| Highlighter (lowlight + highlight.js core + 11 grammar modules, `html` via alias) | **23 617 B** (74.4 KB raw) | Joins the editor chunk in slice 5. Import `createLowlight` by name and the grammars individually: `import('lowlight')` as a namespace drags the `all` set (289 KB gzipped) |
| Emoji chunk | **46 857 B** | Loaded on first open of the picker |
| Editor chunk projected at the end | ≈ 185–200 KB | 161 + 24 + the slices 4–5 code |

Proposed ceilings, for the human (ADR-0037 decision 10): entry growth **10 KB**, editor chunk **220 KB**, emoji chunk **52 KB**. A cheap way to cut ~20 KB from the editor chunk, **not taken** (it replaces a planned package, so it is the human's call): write the link mark in-house (a mark with `href`, parse, render, two commands: ~40 lines) instead of `@tiptap/extension-link`, which only brings `linkifyjs` here.

### 14.1c Slices 4–5: choices the plan left open (all in code, none changes the contract)

- **Auto-link** reads tokens split on Unicode white space and does not strip an opening bracket: `(https://x.org)` links nothing at its start; `(см. https://x.org).` links the address. A link is a **separate dispatch** after the typed space or the paste, `closeHistory` first. Typed: on space and on Enter (the stock Enter runs first through `keyboardShortcut`, the link goes onto text before the caret). Pasted: only plain-text clipboards (F-6), inserted by the editor's own `pasteText`, then linked; a single address over a selection links it and keeps the text. Ctrl+Shift+V is tracked by its keydown.
- **Keymap in one handler.** `RtKeyboard` is one `handleKeyDown` at priority 1000, so the order inside it is explicit and the Esc arming (one keystroke; a lone modifier does not disarm) can be read by every Tab rule. The stock Tab of the table is removed (a binding there would also take the Tab that Esc armed); `Mod-]`/`Mod-[` live here with the depth guards.
- **Tab in a code block with a selection** indents each selected line (F-8); the line the selection ends at the very start of is not included.
- **The strip of a code block** draws its copy icon as an inline SVG of the node view, so the hostile-corpus test strips `.rt-codeblock-strip` before scanning the page for `<svg`.
- **Emoji grid.** Plain `<button>` elements styled once by the grid's selector (1 644 emotion computations were the cost of `ButtonBase`s: 16–20 s a test in jsdom, 1.5–4 s now). Groups with their Russian names while the search is empty, one flat grid while it is not. Arrow Up/Down jump by 8 inside a group and to the same column of the neighbouring group at its edge. A failed load of the emoji chunk shows an empty popover (design G-1: no text exists); a failed copy to the clipboard is silent (G-2).
- **Notices.** One `role="status"` region, always in the DOM. A notice (image not pasted, «Код скопирован») stays until the next edit or blur; the paste or drop that raised it is not that edit. The Tab hint follows `{ in a code block or a table } × { focused }` through `useEditorState`. The same notice again is announced again (emptied, then filled in the next task).
- **Paste.** `transformPastedHTML` returns the normaliser's own canonical string (the pre-pass works on the parsed `<body>` and `normaliseBody` reduces it, so no `innerHTML` is involved). A heading's `font-weight` is not read as bold (it is bold already). White-space + monospace elements and every `pre` become one code block of their text. `handleDOMEvents.drop` (not `handleDrop`) refuses a dropped file, because ProseMirror's own drop returns before `handleDrop` when the point has no document position.
- **Code colouring** is `behaviour/codeHighlight.ts`: the grammar of a block is its `language` attribute (`html` via an alias of `xml`), the 8 token classes are CSS hooks in the styled root (`rt-code-<token>`, never stored). `createLowlight` and the grammars are imported by name.
- **Server rules ported in slice 5b** (backend plan § 6.3 D-3, D-5, D-6): a CR from a numeric reference is written as LF (counted 1); a link address holding `<` or `>` is not kept (text stays); whitespace dropped between blocks counts against the ceiling only for a value with nothing in it. **Not mirrored, on purpose:** the structural refusals (depth > 160, > 300 000 elements, > 350 000 tags, > 64 attributes in a tag, > 1 000 `pre`/`listing`, > 8 000 units of tree-builder markup): the panel's canonical output (depth 17, ceiling-sized under 260 000 elements) never meets them, a refusal by them is the server's alone and reaches the form as `description_markup_too_large`, and an editor that mirrored them would only duplicate bounds it cannot reach. The normaliser's own over-ceiling answer (`markupLength` above the ceiling) is unchanged.
- **Bundle at the end of slice 5** (gzipped, throwaway build whose entry holds the wrapper and the library): editor chunk **223 654 B** with everything but the real app's shared MUI parts (the build counts `Dialog`, `TextField`, `Menu`, `Popover`, `Alert` in the editor chunk because nothing else in that entry uses them; in the app's entry most are already there), emoji chunk **46 857 B**; the highlighter (23.6 KB) is inside the editor chunk. The proposed ceilings in `bundle-budget.json` still hold (225 280 B for the editor chunk, razor-thin in this isolated build; the real number comes at slice 7 with the page wired). Not taken, the human's call recorded 2026-10-13: write the link mark in-house instead of `@tiptap/extension-link` (about 20 KB gzipped, only `linkifyjs` comes with it).

### 14.2 Risks

1. **Two implementations of one form** (server C#, client TS). Mitigation: the
   port (F-1), the shared `input` column run both ways, the added § 4.6 cases;
   residual: DOM-parser differences the corpus does not cover (`noscript` is
   parsed with scripting **off** by `DOMParser`, on by the server; both drop
   it, but a parse-dependent case outside the corpus could differ — the
   normaliser is a convenience, the server has the last word, and a divergence
   shows as one extra "unchanged?" save).
2. **ProseMirror in jsdom** is the largest test risk (S2); the manual list
   carries what it cannot.
3. **`prosemirror-tables` assumptions** (spans, `fixTables`, paste) against
   our spanless schema (S4).
4. **Failed-chunk retry** is browser-dependent (S6); the editor is optional for
   the rest of the form (the form still saves other fields).
5. **Emoji grid weight** and Windows glyph gaps (flags omitted by design).
6. **Test suite time**: each form test file now imports the editor chunk.
7. **A list item starting with a nested list** differs by one empty paragraph
   between server canonical and the editor (§ 5.2); harmless, recorded.
8. **The `www.` rule and html-paste auto-link** are spec-silent (F-6, F-10).
9. The per-change normalisation of a huge document (S3).

### 14.3 What the contract or the backend must still provide

Nothing in `api-contract.md` blocks the frontend: `descriptionFormat`, the two
reasons, the equality rule and the limits are fixed, and `vocabularies.json`
and the four fixture files exist. Remaining, none a blocker:

1. **Fixture additions** (§ 4.6) go into the shared `contracts/rich-text/
   fixtures/canonical.json` and `visible-count.json`: it is a shared file the
   backend suite also runs (plan-backend § 5.3 invites it). The orchestrator
   should tell the backend session so the server runs them; a failure there is
   a finding either way.
2. **Confirm the `Rune.IsWhiteSpace` set** for the blank rule (§ 4.6 item 2):
   the client uses an explicit table; two fixture cases settle it.
3. **Markup count parity** is assumed:
   for editor output the dropped-whitespace term of the backend budget is 0,
   so `markupLength` of the canonical string equals the server's. If the
   backend ever counts anything else for an editor-made value, the ceiling
   check disagrees at the margin.
4. **`description_format_unknown` placement**: the contract reports it under
   `errors.description` but says the panel shows it in the banner; the frontend
   does that (§ 11.1). Stated so QA does not read it as a defect.
5. The server's mock-visible behaviour for `""` + `html` (stores `("", plain)`)
   is in the contract; no action.

### 14.4 For the orchestrator and the human

| # | Item | Who |
|---|---|---|
| 1 | Record F-3 (own highlight plugin instead of `extension-code-block-lowlight`) as an implementation deviation from ADR-0037 decision 4's list; same for F-4 and F-5's wording | orchestrator note in `status.md`; human at gate-3 |
| 2 | Confirm or change the bundle ceilings once the first measurement exists (slice 3) — ADR decision 10 requires the human's confirmation | human |
| 3 | Design copy gaps, none invented: **G-1** no text for an **emoji chunk** load failure (the plan reuses «Не удалось загрузить редактор.» + «Повторить» until design says otherwise); **G-2** no text or announcement for **copy failure** (clipboard denied): the plan is silent; **G-3** loading text for the emoji popover: spinner only; **G-4** Tab with a selection in a code block (F-8) | designer/analyst, minor |
| 4 | `app/theme/{theme.ts,muiAugmentation.d.ts}` and `vite.config.ts`, `package.json`, `setup.ts` are shared files a neighbouring session may change: re-read before each edit; the additions are small and additive | frontend (self) |
| 5 | `docs/conventions/frontend.md` § MUI sentence naming the editor and ADR-0037 and the ADR-0007 index line are owed "on acceptance"; the sentence is a slice 7 commit unless the orchestrator already did it | orchestrator |
| 6 | The Kazakh texts of the two new reasons are `kkNeedsReview` (backend plan § 14.6); the panel shows only the server's `detail`, so no frontend change | human at gate-3 |
| 7 | Run economy: the plan assumes targeted vitest runs until the CPU all-clear (tech-028); a full `verify.sh` is owed once, at the end of slice 7 | orchestrator |

## 15. As built (stage 6b, closed 2026-10-13)

What stands in the code, where it differs from §§ 1–14, and what is left to a human. Slices 0–7 are
committed; `scripts/verify.sh` and the whole-suite run are **owed** (tech-028 asked every session to
pause them), as is E2E (the human's standing answer is no).

### 15.1 Differences from the plan, all recorded where they were made

| Plan | As built | Why |
|---|---|---|
| § 7.2 synchronous report | **debounced 150 ms** report and `flush()` on a `ref`; the form flushes on submit, the editor flushes on Tab out of the body and on blur | spike S3 (jsdom: 660 ms on 100 000 visible characters with marks) |
| § 3.1 `@tiptap/extension-code-block-lowlight` | own decoration plugin `RtCodeHighlight` (F-3) | its plugin has no cap and no idle delay |
| § 5.1 Tab bindings | the table's and the list keymap's stock Tab are **removed**; Tab is `RtKeyboard`'s alone | an armed Tab must not be taken by a second binding |
| § 9.1 `React.lazy` + boundary | own `import()` in an effect, state for the component, an `Alert` with «Повторить» | `static-components` lint; a rejected `lazy` is cached |
| § 9 emoji chunk failure (G-1) | the popover shows «Не удалось загрузить редактор.» and «Повторить» (the human's decision) | no new copy |
| § 9 copy failure (G-2) | notice line «Не удалось скопировать код.» — **new copy for the human at gate-3** | the human's decision |
| § 6.2 headings in a cell | disabled in a cell too (cells are `paragraph+`) | the coordinator confirmed |
| link mark | `@tiptap/extension-link` kept (the in-house link mark, about 20 KB gzipped, is not adopted) | the coordinator's decision |
| § 11.2 `DESCRIPTION_MAX_LENGTH` | gone; `descriptionError` takes the canonical html | BR-RT-7 |

### 15.1a Table handles (design change of 2026-11-12, US-6, BR-RT-17)

The human asked for the table operations to live on the table. Built as an overlay beside the editor
(`shared/ui/RichTextEditor/table/`: `handles.ts` the pure part, `TableHandles.tsx` the view, mounted by
`EditorImpl` in a `position: relative` box next to the content). It is not a document node, so the
canonical html, the copy and the paste are untouched.

- **What shows.** Only while the editor is focused, editable, and the caret is in a table cell (not a
  multi-cell selection): row «+» on the table's left edge at the active row's top and bottom boundaries,
  row «−» at the right edge on the active row, column «+» on the top edge at the active column's left and
  right boundaries, column «−» at the top on the active column. Rules are the «…» menu's: no «above» in the
  header row (it would put a row over the header; the menu disables it too), no «−» when one row or one
  column remains (use «Удалить таблицу»). A toolbar menu or the link dialog takes the focus, which hides them.
- **Position.** From the rects of the active cell, its table and its `.rt-table-scroll` wrapper, relative to
  the overlay; recomputed on transaction, focus, blur, scroll (captured on the box), window resize and a
  `ResizeObserver` on the body. A handle outside the visible part of a scrolled table or body is not drawn.
  In the first cell the row «+ above» steps 12 px out of the border so it does not sit on the column «+».
- **Commands.** The tiptap ones the menu runs; after a delete the caret goes to the neighbour that took the
  cell's place (the next row or column, the previous one at the last). `mousedown` is prevented, so the
  editor keeps focus.
- **Size.** A 16 px visible dot in a 20 px hit target, `theme` colours (`inputBorder`, `paper`, `primary`;
  «−» turns `error`), the theme's `focusVisible` ring. Names and tooltips: `copy.tableHandles`.
- **Kept.** The toolbar «…» menu with all eight items is the keyboard and screen-reader path; the handles are
  out of the tab order. Tab, Shift+Tab and Esc then Tab are unchanged.
- Tests: `tableHandles.test.tsx` (visibility for header/first/middle/last and one-row/one-column tables,
  commands and the documents, caret, canonical html, disabled/blur). Looked at in Chromium on the
  standalone preview page (first/middle/last cells, after scroll, 380 px).

### 15.2 The bundle, measured on the real build (`npm run build` wiring, gzipped)

| | bytes | against the proposed ceiling |
|---|---|---|
| entry chunk (baseline 395 696, see below) | **406 652**, **+10 956** | growth ceiling 10 240: **exceeded by 716** |
| editor chunk (`EditorImpl`, with ProseMirror, TipTap, the toolbar, the highlighter, the link dialog) | **182 083** | 225 280 |
| emoji chunk | **46 857** | 53 248 |

The entry growth is the pure library (normaliser, vocabulary, legacy conversion, limits, link rule), the
wrapper, and `theme.richText`; no editor-library module is reachable from the entry (`bundle:check`
passes). The editor chunk is below the isolated measurement (223 654) because the MUI parts the entry
already holds are not counted again. The ceilings stay *proposed* in `bundle-budget.json` until the human
confirms them (ADR-0037 decision 10).

Re-measured on 2026-11-09 on the merged tree (code review CR-5; the table above is that measurement,
replacing slice 7's 289 381 / +9 862 against the pre-merge baseline 279 519). The baseline is main's
entry at the merge-base side of the merge (`a0a43386`) built the same way, 395 696 B (this file's own
gzip level 6; the report's figure for the same kind of file differs by about 15 B). **The entry growth
now exceeds the proposed ceiling by 716 B**; the editor and emoji chunks are inside theirs. The ceilings
are not changed here: raising the entry ceiling (or trimming the entry) is the human's call at gate-3.
`bundle:check` therefore fails today on the entry growth alone.

### 15.3 Left to a human (QA by hand — jsdom cannot show it)

1. **S6**: a failed chunk load retried in Chrome and Firefox («Повторить» after the network came back);
   if it does not retry, add the `location.reload()` fallback.
2. **Real clipboard sources**: a web page, an office editor, a spreadsheet (Excel/Calc as html and as
   tab text), VS Code and a JetBrains IDE; Ctrl+Shift+V; an image copied from a page; a dropped file.
3. **Keys on the platform**: ⌘ on macOS (tooltips and shortcuts), Ctrl+click on a link, ArrowDown at the end
   of a **wrapped** last line of a code block (jsdom has no layout, so this was only tested at the end of
   the text), Ctrl+Z after an auto-link in a real browser (history grouping by time).
4. **Emoji**: glyphs per OS (Windows has gaps; flags are omitted by design), first-open time of the picker
   (1 644 buttons), the grid at 640 px.
5. **Screen reader pass** (NVDA/VoiceOver): toolbar name and roving, pressed/expanded states, the notice
   line (image, «Код скопирован», the Tab hints on entering a block), the counter's name, the dialog and the
   popover, the menus.
6. **Look**: light/dark mapping of the palette (the dark column is specified and tested, not drawn), the
   code colours on the block background, the two-row toolbar at 640 px, the card's focus and error frames,
   the table's active-cell frame and scroll wrapper, the colouring latency while typing in a long block.
7. **Opening speed** with 100 000 visible characters (observation, not a gate).

### 15.4 Owed

- `scripts/verify.sh` (whole frontend and backend suites), after the tech-028 all-clear.
- The human's confirmation of the three ceilings, and of the new copy «Не удалось скопировать код.».
- Server side of the fixtures: the 22 cases added by the frontend run in the backend suite (QA re-check).
