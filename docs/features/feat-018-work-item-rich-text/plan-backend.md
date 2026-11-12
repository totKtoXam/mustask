# Backend plan — feat-018-work-item-rich-text

Stage 04a, size L. Input: spec.md, US-1..US-7, ADR-0039 (accepted),
`docs/conventions/backend.md`, the work-items slice as it stands. Output: this
plan and [api-contract.md](api-contract.md) (fixed). No production code yet.

The contract is additive and small (one request field, one response field,
two error codes). The weight of the task is **one component**: the HTML
reduction with its canonical pass, which is a security boundary, and the two
shared artefacts that keep it equal to the editor's output (vocabulary file,
fixtures).

## 1. What is backend and what is frontend-only

| Backend (this plan) | Frontend-only (plan-frontend.md, not here) |
|---|---|
| US-2 whole, US-4 whole, US-7 whole | US-1, US-5, US-6 whole (the editor, toolbar, palettes, emoji picker, code and table behaviour, paste, auto-link, shortcuts) |
| US-3 server side: another field's save leaves the description untouched, "other reads are not broken", legacy worst cases fit the ceiling, existing rows unchanged by the migration | US-3 client side: legacy plain-to-html conversion (BR-RT-9), not dirty on open, the request carrying no `description`, canonical agreement of the editor |
| the vocabulary file `contracts/rich-text/vocabularies.json` and the fixtures — **authored here first**, consumed by the panel | the client normaliser, client counting of BR-RT-8, client ceiling check, the theme mapping of tokens, the "every token has a theme mapping" test, `react/no-danger` |
| the format marker on the wire, the two new codes and their texts in `contracts/errors/workitems.json` | showing the three refusals (`placeRefusal`, the banner for `description_format_unknown`) |

Both sides run the same fixture files; the panel adds `editorOutput` columns
only a real editor can produce (fixtures § 5.3).

## 2. Slices and files affected

No new slice and no new route. One use case changes behaviour (Save), one
changes shape (every `WorkItemView`), plus new shared pieces.

| Layer | Path | Change |
|---|---|---|
| Domain | `Mustask.Domain/WorkItems/DescriptionFormat.cs` (new) | enum `Plain`, `Html`; wire keys `plain`/`html`; `TryParse(string?)`, `Key(...)` |
| Domain | `WorkItemLimits.cs` | + `DescriptionVisibleMaxLength` 100 000, `DescriptionMarkupMaxLength` 1 000 000, `DescriptionRequestMaxLength` 2 000 000 (named by meaning; `DescriptionMaxLength` stays: plain, UTF-16 units) |
| Domain | `WorkItemErrorCodes.cs` | + `DescriptionMarkupTooLarge`, `DescriptionFormatUnknown` |
| Domain | `WorkItemText.cs` | + the description rules of § 3 (pure functions) |
| Domain | `WorkItem.cs` | `DescriptionFormat` property; `Save` takes the format and compares the pair; `Create` sets `Plain` |
| Application | `Abstractions/RichText/IRichTextReducer.cs` (new) | the reduction port (§ 4) |
| Application | `Features/WorkItems/SaveWorkItem/*` | command gains `DescriptionFormat`; handler prepares the description before the transaction (§ 7) |
| Application | `Abstractions/WorkItems/IWorkItemReadModel.cs` | `WorkItemView.DescriptionFormat` (string) |
| Infrastructure | `RichText/*` (new) | reducer, vocabulary loader, writer (§ 5, § 6) |
| Infrastructure | `WorkItems/Configurations/WorkItemConfiguration.cs`, `WorkItemsConstraints.cs`, `WorkItemReadModel.cs` | column, checks, projection (§ 8, § 9) |
| Infrastructure | `Migrations/<ts>_AddWorkItemDescriptionFormat.*` + snapshot | § 8 |
| Infrastructure | `Mustask.Infrastructure.csproj`, `DependencyInjection.cs` | packages, embedded vocabulary, singleton reducer |
| Api | `Features/WorkItems/WorkItemEndpoints.cs` | request record + one field; wire the command |
| Shared | `contracts/rich-text/` (new), `contracts/errors/workitems.json`, `docs/error-codes.md` (generated) | § 5.2, § 5.3, § 10 |
| Build | `Directory.Packages.props` | the two packages (§ 6) — through the orchestrator (write zone) |

`GetWorkItem`, `CreateWorkItem`, `GetWorkItems` and `SetWorkItemParent` have
no code change of their own: they return `WorkItemView`, which gains a field
in the read model. `GetWorkItems` selects `Description` as `string.Empty`
already and keeps doing so.

## 3. Domain

`Mustask.Domain` has no NuGet dependency (an architecture test enforces it),
so the HTML parser cannot be here. The Domain owns **what is valid and what
is stored**; Infrastructure owns **how html is reduced**.

- **`DescriptionFormat`** (`Plain`, `Html`): stored as a string by the
  convention (§ 8), wire keys lower case via `DescriptionFormats`
  (`TryParse` is exact and case-sensitive, `null` is "not declared").
- **The pair.** `WorkItem.Description` + `WorkItem.DescriptionFormat` are one
  concept; `WorkItemDescription` (a `readonly record struct`, value, format)
  has one factory that normalises the empty value to `Plain`, so the stored
  invariant "an empty description is plain" (api-contract § 1.1) cannot be
  broken by a caller. Equality is the record's — this is BR-RT-10's
  "(value, format)".
- **Rules in `WorkItemText`** (pure, unit-testable without a parser):
  - `ValidateFormat(string? raw, out DescriptionFormat? format)` → reason
    `DescriptionFormatUnknown` or `null`; absent/`null` yields `null` format.
  - `ValidatePlain(string)` → `DescriptionTooLong` above 100 000 UTF-16 units
    (today's `ValidateDescription`, kept as the plain rule; old tests stay).
  - `CheckRequestSize(string raw)` → `DescriptionMarkupTooLarge` above
    2 000 000 units (the pre-parse cap; html only).
  - `ValidateReduced(int markupCodePoints, int visibleCodePoints)` → the order
    of BR-RT-7: markup reason first, then `DescriptionTooLong`.
  - `Describe(string reducedHtml, int visibleCodePoints)` → the
    `WorkItemDescription`: `("", Plain)` when the visible text has no
    non-whitespace character (BR-RT-6), else `(html, Html)`. "Non-whitespace"
    is decided by the **reducer** (it holds the visible text, BR-RT-8) and
    passed in as a flag, not re-derived from markup in Domain.
- **`WorkItem.Save`**: `WorkItemEdit` gains `DescriptionFormat? DescriptionFormat`
  beside `Description` (`null` format with a description = `Plain`); the
  comparison is of the pair; the entity also guards its stored invariants
  with `ArgumentException` as it does for the title (html over 1 000 000 code
  points, plain over 100 000 units, an empty html): these are caller defects,
  never user input, because the handler validated first.
- No new domain events, no new status transitions.

## 4. Application: the reduction port

```
Mustask.Application/Abstractions/RichText/IRichTextReducer.cs
  RichTextReduction Reduce(string html, RichTextReductionLimits limits, CancellationToken ct);
  record RichTextReduction(bool Exceeded, string Html, int MarkupLength, int VisibleLength, bool HasContent);
  record RichTextReductionLimits(int MaxMarkupCodePoints);   // a profile point for comments later (ADR-0039)
```

- **Synchronous and CPU-bound**: no I/O, so it is legitimately outside any
  transaction (conventions: no external I/O inside a DB transaction; this is
  the stronger statement that nothing slow holds a row lock).
- `Exceeded == true` means the output passed the bound and the reduction
  stopped; `Html` is then empty and must not be read. The reducer never
  throws for a limit; it throws only for a defect (the platform's 500).
- One parse yields markup length, visible length and "has content"
  (ADR-0039 decision 5). The abstraction takes the **budget as a parameter**,
  so the reducer carries no Domain constant and the comments profile is a new
  argument, not a fork.
- A fake `IRichTextReducer` in `Mustask.Application.Tests` returns fixed
  results; the handler's tests need no parser.

## 5. Infrastructure: the reducer

`Mustask.Infrastructure/RichText/`: `HtmlRichTextReducer : IRichTextReducer`
(singleton), `RichTextVocabulary` (+ loader), `CanonicalWriter`,
`LinkAddress`. `internal` throughout; only the interface is public.

### 5.1 Pipeline (one parse, one serialisation)

1. **Normalise the raw string** (single pass, before parsing): U+0000
   removed, each lone surrogate replaced by U+FFFD (BR-RT-8, BR-RT-9). Counting
   code points is by `Rune` enumeration afterwards, never `string.Length`.
2. **Parse** with AngleSharp as a fragment in a `body` context, scripting
   **enabled** (as a browser), so `noscript` is read as the browser reads it
   and no parse differential exists on input.
3. **Allow-list stage** (US-2 § Allow-list): elements dropped with content,
   unwrapped, attributes removed, `class` reduced to exact vocabulary tokens
   on their element, link address by the rules of US-2 (own
   `LinkAddress.TryRead`, unit-tested against the story's table; the library's
   scheme check is a second line, not the rule).
4. **Canonical pass** (US-7) over the cleaned DOM into an **in-memory
   canonical model** (not by mutating the DOM): heading levels, marks in
   order and merged, token splitting, code-over-marks, list-item and cell
   paragraphs, `pre` to one `p` per line inside containers, rectangular
   tables, loose text wrapped in `p`, empty inline elements removed.
5. **Write** with `CanonicalWriter` (own WHATWG-form writer, api-contract § 3)
   under the **output budget**: it counts code points as it appends and
   aborts the moment the count passes the bound → `Exceeded`. Visible text
   (BR-RT-8, leaf blocks separated by one LF, `br` one LF) is accumulated by
   the same writer in the same pass.
6. Result `("", has content = false)` is how BR-RT-6 reaches the Domain.

Walkers are **iterative**, never recursive on DOM depth, and nothing calls a
library API that recurses on depth (`ToHtml`, `TextContent`, `Clone` of a
deep subtree). A hostile input of 600 000 nested `<b>` must cost time, not a
stack overflow that kills the process. A depth hazard found in the spike is
resolved in our walker (unwrapping beyond the list depth already removes most
of it), never by a new refusal reason.

The budget counts **text-node code points read**, kept or dropped as
inter-block whitespace, plus markup written. Without that, "whitespace only,
1 000 001 code points" (US-4) would reduce to nothing under the ceiling and be
stored as `""` instead of refused. The only observable difference from "the
ceiling on the reduced markup" is a document whose *dropped* whitespace alone
passes 1 000 000: it is refused. Recorded as a clarification for the human
(§ 14, item 2).

**Concurrency gate.** A 2 000 000-unit input can hold hundreds of thousands
of DOM nodes (order of 100 MB); several at once is a memory spike on a small
host. The reducer takes a `SemaphoreSlim` of `max(2, ProcessorCount)` slots
(configuration `RichText:MaxConcurrentReductions`); a waiting request still
meets the NFR (2 s) at normal load and a hostile burst queues instead of
exhausting memory. It honours the `CancellationToken` while waiting and
every few thousand nodes while working.

### 5.2 Vocabulary

`contracts/rich-text/vocabularies.json`, embedded into
`Mustask.Infrastructure` the way `contracts/dictionaries/*.json` already is
(csproj `EmbeddedResource` with `Link`, `LogicalName="RichText/vocabularies.json"`).
Shape, fixed here because the panel imports the same file:

```json
{
  "version": 1,
  "markOrder": ["link", "bold", "italic", "underline", "strike", "highlight", "textColor", "fontSize"],
  "families": [
    { "kind": "textColor", "element": "span", "tokens": [{ "name": "gray", "class": "rt-color-gray" }, "…"] },
    { "kind": "highlight", "element": "mark", "tokens": ["…"] },
    { "kind": "fontSize",  "element": "span", "tokens": ["…"] },
    { "kind": "codeLanguage", "element": "code", "within": "pre", "plain": "plain", "tokens": [{ "name": "javascript", "class": "language-javascript" }, "…"] }
  ]
}
```

- Content is exactly BR-RT-14 and BR-RT-16 (8 + 8 + 4 + 12 tokens;
  `plain` has no class).
- `RichTextVocabulary` loads it at startup (fails the start on an unknown
  `kind`, a duplicate class, a class not starting `rt-`/`language-`, an
  unknown `markOrder` entry; like `ErrorCatalogLoader`).
- **No token literal appears in server code.** An architecture/unit test
  greps `src/backend` for `rt-color-`, `rt-hl-`, `rt-size-`, `language-` and
  fails on a hit outside tests; ADR-0039 decision 6 made the file the only
  source.
- **Drift tests** (`Mustask.Infrastructure.Tests/RichText/`):
  (a) the embedded bytes equal `contracts/rich-text/vocabularies.json` on
  disk; (b) a generated theory over every file entry: the token is kept on
  its element, dropped on any other element, dropped when case-changed, and
  written from the table; (c) `markOrder` is the order the pass implements
  (a fixture of all eight marks on one text); (d) a kind the pass does not
  implement fails the load. The panel's mirror test (every token has a theme
  mapping) is frontend's.

### 5.3 Shared fixtures

`contracts/rich-text/fixtures/`, one JSON file per purpose, each case
carrying `id`, `story` (`US-2`..`US-7`), `note`:

| File | Case | Run by |
|---|---|---|
| `canonical.json` | `input`, `canonical`, optional `editorOutput` | server: reduce(input) = canonical; reduce(editorOutput) = canonical; reduce(canonical) = canonical. panel: load canonical, serialise, normalise = canonical |
| `visible-count.json` | `html`, `count` (US-4 list: `&amp;`, leaf-block separators, `br`, tables, 😀 = 1, ZWJ = 5, lone surrogate = 3) | both |
| `hostile-corpus.json` | `input`, `mustNotContain` (patterns) | server (US-2); panel loads each stored form and inspects the document (US-3) |
| `legacy.json` | `plain`, `html` (the client's conversion of it), incl. the worst cases (100 000 `&`, 100 000 LF) | panel: converts plain to html; server: reducing that html is the identity, it is within the ceiling and counts as expected |

The backend authors `input` and `canonical` from the US-2/US-4/US-7
scenarios first (phase 1, before the reducer, as the reducer's test oracle);
the frontend adds `editorOutput` and its own cases. Both suites run the
whole file, so a case added by either side runs on both.

## 6. Packages and the spike

Candidates, verified against the registry on 2026-10-13, to be pinned by the
orchestrator in `Directory.Packages.props` after the spike:

| Package | Candidate | Licence | Note |
|---|---|---|---|
| `HtmlSanitizer` (Ganss.Xss) | 9.2.1039 (latest stable; 10.0.x are betas) | MIT | pulls `AngleSharp` 1.7.2 and `AngleSharp.Css` 1.0.2 |
| `AngleSharp` | the version `HtmlSanitizer` resolves (1.7.2; 1.8.4 exists) | MIT | explicit reference with the comment style of the `Microsoft.IdentityModel.Protocols.OpenIdConnect` precedent; pinned to the resolved version, bumped only with the corpus green |

`AngleSharp.Css` arrives transitively because the sanitizer parses `style`;
we allow no `style` and no CSS property. It is a supply-chain addition worth
knowing about, not a choice.

**Spike (first working step of 4b, time-boxed; throwaway console or test
project, results written to `status.md` Log).** Question → what decides.

| # | Question | Decides |
|---|---|---|
| Q1 | Default removal: with `KeepChildNodes` on, are the contents of `script`, `style`, `template`, `noscript`, `svg`, `math` kept as text? Is the drop-with-content list expressible (a removal hook, or our pre-pass over the DOM)? Comments, PI, CDATA? | whether S1 can express drop-vs-unwrap or we do it in our pass |
| Q2 | Can the sanitizer run on an already parsed document and hand back the DOM (one parse), or only string to string (two parses)? | pipeline shape; time budget at 1 000 000 |
| Q3 | `class` handling: is the allowed-class match exact and case-sensitive; how is the value split (ASCII whitespace only? `Char.IsWhiteSpace` would treat U+00A0 as a separator); can a token be bound to an element? | whether classes go through the library or only our pass |
| Q4 | URL reading: does it trim C0 controls, remove tab/LF/CR inside, decode entities before comparing the scheme, treat `//host`, `/x`, `#x` as relative; `mailto:` with empty address | whether `LinkAddress.TryRead` is the rule (expected: yes) |
| Q5 | Output of `rel`/`target`; attribute removal on unlisted elements; `data-*`, `aria-*`, `id`, `name` (DOM clobbering) | configuration only |
| Q6 | Deep nesting (2 000 000 units of `<b>`, `<div>`, `<ul>`): stack depth, memory, time; the 1 000 000-code-point document: parse + reduce wall time on CI-class hardware (NFR 2 s) | gate size; walker rules; whether a different parser option is needed |
| Q7 | Parser round trip of the corpus: is AngleSharp's serialisation of the sanitised DOM stable under a second parse (the mXSS test of US-2) | the test oracle |

**Decision rule.** S1 stays if Q1–Q5 are met by configuration and hooks with
no second parse beyond the one Q2 shows, and Q6 meets the NFR. Otherwise the
fallback of ADR-0039 (S2: AngleSharp alone with our walker) is taken **without
amending the ADR** (it names S2 as the fallback), `HtmlSanitizer` is not
added, and the spike result is the Log line. Either way the canonical pass
and writer are ours; the sanitizer's contribution is the parse, the
element/attribute/scheme allow-list and its field time against hostile
input. The security-relevant tests do not change with the choice.

### 6.1 Spike result (stage 04b, 2026-10-13)

Throwaway console against `HtmlSanitizer` 9.2.1039 and `AngleSharp` 1.7.2
(Release build, .NET 10). **Decision: S2, AngleSharp alone with our own
walker. `HtmlSanitizer` and `AngleSharp.Css` are not added.** Only one package
is pinned: `AngleSharp` 1.7.2.

| Q | Finding | Consequence |
|---|---|---|
| Q1 | With `KeepChildNodes` on, the content of `script`, `style`, `noscript`, `iframe` is **kept as text** (`<script>alert(1)</script>x` gives `alert(1)x`); with it off, an unlisted `div`/`font` is dropped with its text. The drop-with-content vs unwrap split of US-2 is not expressible by configuration. AngleSharp parses `svg`/`math` children as foreign elements (an `img onerror` inside `svg style` is an element in the svg subtree). Comments and `<![CDATA[` outside foreign content arrive as comment nodes. | The allow-list stage is ours (S1's own condition for falling back). |
| Q2 | `Sanitize*` is string to string, or `SanitizeDom(IHtmlDocument, IHtmlElement, ...)`; either way its filter then needs a second pass of ours (class tokens per element, link rule, canonical pass). | Nothing gained from the library but a second configuration to keep equal to ours. |
| Q3 | Class match is exact and case-sensitive; tab splits, U+00A0 does not. | Same as US-7, but tokens must be bound to elements and written from the table: ours. |
| Q4 | Kept `//evil.example`, `#frag`, `/x` as links; trimmed C0 and removed tab inside the scheme correctly. | `LinkAddress.TryRead` is the rule (as expected), the library's check is not enough. |
| Q6a | **`HtmlParser.ParseFragment(html, bodyContext)` is quadratic in the number of sibling elements**: 4 000 `<p>` 1.3 s, 16 000 `<p>` 28 s, 40 000 `<p><b>` 390 s. `ParseDocument("<!DOCTYPE html><html><head></head><body>" + html)` is near linear: 1.15 M units (64 000 `<p>`) 0.34 s, 1.6 M units `<p><b>` 0.62 s, 2.3 M units of tables 1.1 s. | Reducer parses as a document after an explicit `<body>` (same "in body" insertion mode as the fragment case). `ParseFragment` is never used on input. |
| Q6b | **Parse time is quadratic in nesting depth** (tree builder's scope checks): `<b>` 16 000 deep 1.3 s, 64 000 deep 18 s; `<div>` 64 000 deep 58 s; `<table>` 32 000 deep 43 s. A 2 000 000-unit hostile input (hundreds of thousands deep) would take hours. No stack overflow was seen up to 64 000 deep. | **Superseded by § 6.3** (the wall-clock budget below was withdrawn after QA R-1; deterministic bounds replace it). Not solvable "in our walker": the parse itself is the hazard. The reducer parses with a **work budget (cancellation after `RichText:ParseBudgetMilliseconds`, default 1 500)** and treats expiry as `Exceeded` (refused with `description_markup_too_large`, the existing reason). Legitimate input parses in well under 0.7 s at the ceiling. Reported to the orchestrator as a deviation (§ 14 item 3 said "never a new refusal reason"; no new reason, but a new refusal condition). |
| Q5, Q7 | Not probed in the spike (Q5 is moot with S2: attributes are read, never kept, by our walker; the spike's Q7 probe was cut short by the Q6b hang). | The mXSS round trip is a permanent test instead: the stored value is re-parsed by AngleSharp, scripting on and off, and reduced again; it must be identical. |

### 6.2 As built: decisions the spec left to the implementation (stage 04b)

Each is covered by a fixture case or a rules test; the editor's normaliser
(frontend plan) must make the same choice.

- **Unwrapped list markup is a paragraph boundary.** `ul`/`ol`/`li` beyond three
  levels, or outside a root/item container (in a table cell), are unwrapped
  (US-2) but their items do not run together: `…<li>3<ul><li>4</li></ul>` gives
  paragraphs "3" and "4" in the depth-3 item (fixture `lists-depth-three`).
  Unknown *block* elements (`div`, `blockquote`) are not boundaries, as the
  story's example requires.
- **`br` is written inside the marks open at that point** (those of the
  preceding run); a leading `br` has none. The canonical form is a function of
  the sequence of runs and breaks only, so it is a fixed point.
- **Whitespace dropped between blocks is ASCII whitespace** (space, tab, LF, FF,
  CR) in a position with no open paragraph; any other whitespace (U+00A0)
  makes a paragraph. "No non-whitespace character" (BR-RT-6) is
  `Rune.IsWhiteSpace` (Unicode White_Space, U+00A0 and U+3000 included; U+200B
  is not whitespace).
- **Empty containers.** A list with no item and a table with no row are removed;
  a list item or table cell with nothing to write is `<li><p></p></li>`,
  `<td><p></p></td>`; an empty code block stays `<pre><code></code></pre>`.
- **Code blocks.** The language is the first valid token of the first direct
  `code` child of the `pre`; the extra leading LF the plan's writer note
  mentioned is not needed (a `code` sits between `pre` and its text).
- **Link address.** `http(s)` needs `//` right after the colon and a host that is
  not empty after userinfo and port; no space, control, backslash or markup
  character in the authority; `mailto` needs a non-empty address before `?`
  with no space or control. `https:example.org` and `https:/x` are refused.
- **Table spans.** `colspan` is clamped to 1 000, `rowspan` to the rows left in
  its row group (`0` = to the end of the group); a table whose positions could
  not fit the markup ceiling (16 code points each) is "exceeded" before the grid
  is built.
- **Concurrency gate and options** are `RichText:MaxConcurrentReductions` and
  `RichText:ParseBudgetMilliseconds` (configuration section `RichText`).

### 6.3 Defect loop 1 (QA D-1..D-7, R-1, R-2): deterministic bounds replace the parse budget

§ 6.1's wall-clock parse budget is **withdrawn**: it made the same document accepted or refused depending on the
host (QA R-1), did not fire inside one token (D-2) and did not stop a stack overflow (D-1). What a reduction
accepts or refuses is now a function of the input alone; the clock survives only as a safety net that answers a
server error. In the order they run (`RichTextInputGuard`, `HtmlRichTextReducer`):

| # | Bound | Value | Where | Why legitimate input is far inside it |
|---|---|---|---|---|
| 1 | raw request value | 2 000 000 units | handler (domain) | unchanged |
| 2 | length of a value holding tree-builder markup: `<!`, `<?`, script, style, title, textarea, xmp, iframe, noembed, noframes, noscript, plaintext, svg, math, select, option, optgroup, frameset, frame, template | 8 000 units | guard | the panel sends allow-list markup; its tokenizer-exactness is only claimed without these |
| 3 | start and end tag tokens | 350 000 | guard (exact tag tokenization) | ceiling-sized canonical: under 290 000 |
| 4 | attributes in one tag | 64 | guard | pasted markup carries about ten |
| 5 | sum of squares of attributes per tag | 8 000 000 | guard | tokenizer duplicate check is quadratic |
| 6 | ~~`pre` / `listing` start tags~~ | removed in loop 2 (QA D-9) | | the recursion (D-1) needs a run of unclosed ones, each the child of the last, so bound 7 caps it at about 160 frames; 1 000 closed code blocks are canonical output |
| 6a | foster-parenting work: sum over inserted nodes and appended characters before a table of the parent's child count | 16 000 000 | tree watcher (child counts, last child, text before it) | canonical output has none; about 4 000 insertions reach it, under 0.1 s (QA D-8) |
| 7 | DOM depth (html and body included) | 160 | tree watcher in the parser's element hook, and the walker | canonical: 17 |
| 8 | elements the parser creates | 300 000 | tree watcher | canonical ceiling: under 260 000 |
| 9 | sum over created elements of nesting at creation | 6 000 000 | tree watcher | canonical: under 3 000 000, measured table: 0.4 M |

| 10 | **text-append work (QA D-10, round 3):** sum over text pieces of the length of the text run they join; a run ends at a start tag that makes an element, a comment, or (inside a table) a table tag; end tags and tags the tree builder may ignore (`html`, `body`, `head`, `form`, `frame`, `frameset`, `isindex`, and `td`/`th`/`caption`/`tr`/`tbody`/`thead`/`tfoot`/`col`/`colgroup` outside a table) continue it | 200 000 000 characters copied | guard (token scan) | canonical output joins one to three pieces: its sum is under its text size (checked on every shared fixture: under 50 000) |
| 11 | **foster tokens (QA D-10):** a stack of table/cell contexts follows the table tags; each non-blank character, and each other tag, met while a table (not a cell or caption) is the open context counts the tokens read so far | 32 000 000 | guard | canonical output never has text or a tag between table tags (whitespace is not counted) |

Cause of D-10. AngleSharp appends text to the last text node by copying the node's whole string, so *n* pieces joined into one node cost *n*²/2 copied characters (`<td>x` ×200 000: 2·10¹⁰ characters, 5 s). A token the tree builder ignores (a `td` outside a table, an unmatched end tag, `html`/`body`) creates no element, so the element hook (bounds 7–9) never sees it, and nothing separates the pieces. The second form, `<table>x</table>y`, was not an append cost but table foster parenting, which the tree watcher (6a) charges only while the table is the last child of its parent: the `y` after `</table>` hid every earlier table from it. Bounds 10 and 11 are counted from the tokens before the parser runs, so they do not depend on the tree or the host; the tree watcher stays as the second line. Both over-count on purpose (an end tag never ends a run; a tag counts against the table whether or not the parser ignores it), so a miss in the model can only refuse more, never less than the cost it stands for; the sweep of `ReducerTextWorkSweepTests` (about 1 500 shapes: every special tag name × before/after/open/close/self-closing/doubled/in table/row/cell, at 400 000 units, plus a one-off run at 1 000 000) checks that nothing the counters accept is slow.

Refusal is the existing `description_markup_too_large`. Further:

- The parser runs on a **dedicated thread with a 32 MB stack**, never a pool thread. The depth bound (7) already
  caps the `pre`/`template` recursion at about 160 levels; the stack and
  `RuntimeHelpers.EnsureSufficientExecutionStack()` in the element hook are the net under it (an overflow that
  nobody foresaw is a server error, not a dead process). An every-tag-name flood test guards the class.
- **Safety net** `RichText:SafetyNetMilliseconds` (default 20 000): expiry throws
  `RichTextReductionAbortedException`, answered by the platform's server error. It never decides acceptance.
- **Async gate (R-2).** `IRichTextReducer.ReduceAsync`: `SemaphoreSlim.WaitAsync` and a dedicated thread completing a
  task, so no request thread is blocked while waiting or reducing.
- **D-3.** a CR (reached by `&#13;`) is written as LF, which is what a re-parse of the stored value gives.
- **D-5.** The ceiling bounds the reduced markup (BR-RT-7 (2)); the whitespace dropped between blocks counts only for a
  value with no content, which US-4 requires refused when it is over the ceiling, never stored as "".
- **D-6.** An address holding `<` or `>` is not kept as a link (its text stays): the serialisation of those in an
  attribute is not agreed between browser versions, and a URL does not contain them.
- **D-4.** A lone surrogate cannot reach the reducer over HTTP: the platform's JSON binder refuses it (400
  `http.bad_request`, nothing stored). The panel replaces lone surrogates itself (BR-RT-8).

## 7. Handler: `SaveWorkItem`

```
Handle(command):
  prepared = PrepareDescription(command)           // before the transaction, no lock held
  ExecuteInTransactionAsync:
    1. address (404s)                              // unchanged
    ... lock the item
    2. ValidateValues(command, item, type, prepared)   // title, description reason, feat-022 fields: one 400
    3..4 unchanged; edit = WorkItemEdit(..., Description = prepared.Value, DescriptionFormat = prepared.Format)
```

`PrepareDescription` (private static in the handler; the decisions live in
`WorkItemText`, § 3):

1. Format reason first, whether or not a description was sent: an unknown
   declared format → `DescriptionFormatUnknown`, and the description is then
   not interpreted.
2. `Description is null` → nothing more to do; a valid or absent format is
   ignored.
3. `Plain` (declared or not): `ValidatePlain`; value stored as sent.
4. `Html`: `CheckRequestSize` → refused without parsing; else
   `reducer.Reduce(...)`; `Exceeded` → `DescriptionMarkupTooLarge`;
   `ValidateReduced` → `DescriptionTooLong`; else `Describe(...)`.

The reason is **kept** and thrown inside `ValidateValues` with the title and
feat-022 reasons, so mod-003 § 0.4's order (404 before 400, every field
reason at once) holds and a bad path with an oversized description still
answers 404. The parse cost is paid even when the path then 404s; accepted
(the body is capped by the pre-parse limit and the gate of § 5.1).

`SaveWorkItemCommand` gains `string? DescriptionFormat = null` **at the end**
of its parameter list, so existing constructions and tests compile unchanged.
The handler gains `IRichTextReducer` (constructor). No other dependency.

## 8. Persistence

`work_items` (snake_case, conventions § Database):

| Change | Detail |
|---|---|
| column `description_format` | `text` NOT NULL, max length 16, default `'Plain'`, string-converted enum (`HasConversion<string>()` per Named values rule 2: the DB stores the enum name, the wire key is lower case in `DescriptionFormats`) |
| check `ck_work_items_description_format` | `description_format IN ('Plain','Html')` (built from `Enum.GetNames`, as `KeyCheck`) |
| check `ck_work_items_description_length` (replaced) | `char_length(description) <= 1000000` — same name, widened. PostgreSQL's `char_length` counts characters, the application's unit for html (ADR-0039 decision 8) |
| check `ck_work_items_plain_description_length` (new) | `description_format = 'Html' OR char_length(description) <= 100000` — plain keeps its bound in the database |
| check `ck_work_items_empty_description_plain` (new) | `description_format = 'Plain' OR description <> ''` — the invariant of api-contract § 1.1 |

**Migration** `AddWorkItemDescriptionFormat`, additive:

- **Up:** add the column with its default (a metadata-only change on
  PostgreSQL 11+, no table rewrite, every existing row reads `Plain`); add the
  format and the two new checks; drop the old length check and add the
  widened one. No `UPDATE`: **no row is rewritten** (D-3, US-4 "existing rows
  unchanged"). Check additions scan the table once under a brief lock; the
  table is small enough that `NOT VALID`/`VALIDATE` is not worth hand-written
  SQL, which a later reader would have to maintain.
- **Down:** a guard first, `DO $$ … RAISE EXCEPTION '…' $$` when any
  `char_length(description) > 100000` exists, with a message naming the cause
  (the spec's "fails once any row exceeds 100 000" made explicit); then drop
  the two new checks and the format check, drop the column, restore the
  100 000 check. Html rows that fit stay as strings in a plain field.
- **Rollback note (the runbook is `docs/runbooks/rich-text-rollback.md`;
  corrected by code review CR-2).** Rolling the *code* back without the
  migration is **not** safe once html rows exist: the old release never writes
  `description_format`, so clearing an html description violates
  `ck_work_items_empty_description_plain` (500) and plain text saved over an
  html row stays marked html. The supported ways are to roll the migration back
  with the code (refused while a row exceeds 100 000 characters; it loses the
  marker, so html rows become indistinguishable from plain text containing
  tags), or to keep the migration, drop the empty-description check while the
  old code runs and repair the touched rows before rolling forward (runbook).
- The model snapshot is regenerated, not edited; per conventions, if main
  brings migrations, this one is deleted and regenerated on the fresh
  snapshot. Verified: apply on a clean database, apply twice (idempotent
  script), down and up again with an under-limit row.

## 9. Read model and API

- `WorkItemReadModel.Rows`: `item.DescriptionFormat` added to `ItemRow` and
  mapped in `ViewAsync` to the key string (`"plain"`/`"html"`). The list's
  rows (`includeDescription: false`) get `Plain` and no extra column is
  needed by the list's response (`WorkItemListItemView` is unchanged).
- `WorkItemView` gains `DescriptionFormat` as a `required` init property,
  like feat-022's fields, so the positional constructor is untouched.
- `WorkItemEndpoints`: `SaveWorkItemRequest` gains `string? DescriptionFormat`;
  `SaveWorkItemAsync` passes it. No new route, `.ProducesProblem` unchanged
  (400 already declared). The OpenAPI document gains the two properties;
  `WorkItemsOpenApiTests` is extended to assert them.
- JSON binding of a non-string `descriptionFormat` is the framework's
  `http.bad_request` (as feat-022's wrong-type case), no code of ours.
- Kestrel's default request body limit (30 MB) covers a 2 000 000-unit
  description with JSON escaping; no limit is changed.

## 10. Error codes

In `WorkItemErrorCodes` (already `[ErrorCodes]`):
`DescriptionMarkupTooLarge = "workitems.workitem.description_markup_too_large"`,
`DescriptionFormatUnknown = "workitems.workitem.description_format_unknown"`.
Entries in `contracts/errors/workitems.json` (ru and kk as in api-contract
§ 4, `kkNeedsReview`), then `MUSTASK_UPDATE_ERROR_CODES=1 dotnet test
src/backend/tests/Mustask.Contracts.Tests` regenerates `docs/error-codes.md`.
The contracts tests (completeness, placeholders, generated reference) are the
check; nothing else is needed.

## 11. Tests (each criterion has one; comments carry the story/scenario)

Naming `Method_Scenario_ExpectedResult`. **U** = unit, **I** = integration
(WebApplicationFactory + Testcontainers), **D** = domain unit, **M** = migration.

### 11.1 Reducer — `Mustask.Infrastructure.Tests/RichText/` (U, no Docker)

| Test class | Covers |
|---|---|
| `AllowListTests` | US-2: formatting kept; executable/presentational stripped; unknown unwrapped (`<div><font…>` = `<p>красный стиль цитата</p>`); attributes removed; heading mapping; images dropped (4 forms, image in a link) |
| `LinkAddressTests` | US-2 link table (12 addresses incl. `" JaVaScRiPt:x"`, `java&#9;script:x`, `//evil.example`, `#frag`, `""`), mailto empty, host empty, link with no text |
| `CanonicalFormTests` | US-7 every scenario: palette kept/dropped, exact match and table-written token (incl. `rt-color-red&#xA0;x`, `RT-COLOR-RED`), inner of same family wins, two tokens split in order, code language closed list, mark order, legacy tags merged (`<b><i><strike><del>`), code wins/link survives (5 inputs), code block no marks, multi-line `pre` in cell/li, heading drops size, thead/nested list/table in a cell, rectangular tables (4 inputs), empty-looking → `""` |
| `FixtureParityTests` | `canonical.json`: reduce(input), reduce(editorOutput), reduce(canonical) all equal canonical; `legacy.json`: html is a fixed point, within the ceiling |
| `VisibleCountTests` | `visible-count.json`: separators, `br`, tables, 😀 = 1, ZWJ = 5, lone surrogate = 3 and U+FFFD stored |
| `HostileCorpusTests` | US-2 corpus: nothing survives (no script/event attribute/scheme/class/element outside), **and** the stored value re-parsed by AngleSharp, serialised and reduced again is identical (mXSS round trip); also parsed with scripting both on and off |
| `IdempotencyPropertyTests` | seeded generator over a small grammar (tags, entities, whitespace, deep nesting, tokens): `reduce(reduce(x)) = reduce(x)`, output only allow-list elements/attributes, deterministic seed so a failure reproduces |
| `BudgetTests` | abort without building past 1 000 000 (counter on the writer); amplification (8 stacked marks, link toggled per character, 2 000 000 units) → `Exceeded` in under 2 s (timed, CI-class budget); deep `<b>`/`<div>`/`<ul>` nesting does not crash (the spike's Q6 as a permanent test, run in a thread with the default stack) |
| `VocabularyDriftTests` | § 5.2 (a)–(d) and the no-token-literal scan |
| `WriterTests` | WHATWG form: escaping, `&nbsp;`, attribute order, `<pre><code>` with a leading LF, void `br`; equals AngleSharp's own serialisation for the same tree (oracle) |

### 11.2 Domain — `Mustask.Domain.Tests/WorkItems/` (D)

`WorkItemText` format/limit rules (plain 100 000 inclusive/over; request cap
2 000 000 inclusive/over; reduced checks in BR-RT-7's order, both-over gives
markup); `WorkItemDescription` normalises empty to plain; `WorkItem.Save`
equality of the pair (the four rows of api-contract § 1.2),
`ChangedAt` untouched when equal, a plain write over html honoured, entity
guards refuse an inconsistent edit; `Create` is `("", Plain)`.

### 11.3 Handler — `Mustask.Application.Tests/Features/WorkItems/SaveWorkItem/` (U, fake reducer)

Preparation order (format reason with no description; unknown format wins
over limits; request cap does not call the reducer — asserted on the fake);
the reason joins the title and feat-022 reasons in one 400; a bad path with an
oversized description is a 404; the reducer is called outside the
transaction (the fake records `unitOfWork` state); a valid format with no
description changes nothing.

### 11.4 Endpoints — `Mustask.Api.IntegrationTests/Features/WorkItems/` (I)

New file `SaveWorkItemDescriptionEndpointTests.cs` (the write zone of this
use case's description part; the existing `SaveWorkItemEndpointTests` stays
untouched and must stay green — that is "a client that declares nothing is
unchanged"). Map to the criteria:

| Story / scenario | Test |
|---|---|
| US-4 no declaration unchanged; plain over 100 000 refused | `Save_NoFormat_StoredExactlyAsSent_ReportedPlain`, `…_PlainOver100000_DescriptionTooLong` |
| US-4 declared html reduced and reported | `Save_Html_ReducedAndReported` |
| US-4 never-written or cleared is plain and empty | `Get_NeverWrittenOrClearedByHtml_EmptyPlain` |
| US-4 unknown format (with and without description) | `Save_UnknownFormat_400DescriptionFormatUnknown_NothingStored` (theory: `markdown`, `HTML`, `""`, `" html"`; and with no description) |
| US-4 format with no description | `Save_HtmlFormatWithoutDescription_Unchanged` |
| US-4 equality of (value, format) | `Save_HtmlResendOfStored_NoChange_ChangedAtSame`, `…_PlainResendOfHtmlValue_IsChange` |
| US-4 plain write over html honoured | `Save_PlainOverHtml_StoredAsPlain` |
| US-4 visible limit, exactly the limit incl. 100 000 emoji, markup does not count | `Save_Html_VisibleOver100000_TooLong`, `…_ExactlyLimitAndEmoji_Stored`, `…_99000VisibleWithMarkup_Stored` |
| US-4 markup ceiling own reason; both over; request cap; whitespace-only over ceiling; amplification | `Save_Html_MarkupOverCeiling_MarkupTooLarge`, `…_BothOver_MarkupOnly`, `…_RequestOver2000000_MarkupTooLargeNotParsed`, `…_BlankOverCeiling_RefusedNotEmpty`, `…_Amplifying_RefusedWithin2s` |
| US-4 lone surrogate | covered by `VisibleCountTests` (U) and one `I` test storing U+FFFD |
| US-4 legacy worst cases fit | `Save_LegacyWorstCases_Stored` (100 000 `&`, 100 000 LF as the client would send) |
| US-4 response time | `Save_And_Get_100000VisibleCharacters_Within1s` (2 s at the ceiling), timed assertion |
| US-2 / US-7 through the API | `Save_FixtureCanonicalCases_RoundTripThroughPatchGet` (every `canonical.json` case: PATCH then GET equals canonical; second PATCH changes nothing) and `Save_HostileCorpus_StoredValueClean` |
| US-3 server side | `Save_OtherFieldOnly_DescriptionByteForByteUntouched` (legacy and html items), list/board/backlog read tests unchanged and green |
| §0.4 order | `Save_BadPathOversizedDescription_404`, `Save_TitleAndDescriptionBothInvalid_BothReasonsAtOnce` (extended with the new reason) |

### 11.5 Migration — `Mustask.Infrastructure.Tests/WorkItems/` (M, Testcontainers)

`AddWorkItemDescriptionFormatMigrationTests` on the pattern of
`AddWorkItemFormFieldsMigrationTests`: rows seeded at the previous
migration (a 100 000-character plain description, a description containing
markup characters), migrate up, every description byte-equal and format
`Plain`, a row of exactly 100 000 plain code points reads back whole;
the new checks refuse an html row of 1 000 001, a plain row of 100 001, an
empty html row and an unknown format; migrate down: refused with the named
message when a row exceeds 100 000, succeeds otherwise; up again. The
existing `MigrationsTests` (clean database, repeated apply) must pass.

### 11.6 Contracts — `Mustask.Contracts.Tests` (U)

Existing completeness/placeholder/reference tests pass with the two new
entries; no new test file.

## 12. Lanes (4c): no

The trigger is four or more use cases with disjoint write zones. This task
has **one** use case whose behaviour changes (`SaveWorkItem`), a read shape
that changes in the shared read model, and one new shared component (the
reducer). The reducer is the long pole, but it is not a use case: it has no
`Features/<Module>/<UseCase>/` folder, and its pieces (allow-list stage,
canonical pass, writer, vocabulary) share one model and one fixture oracle —
the kind of coupling lanes cannot split. No lanes; one backend agent works
sequentially.

## 13. Order of work (small commits, green build after each)

| # | Step | Gate |
|---|---|---|
| 1 | Spike (§ 6); decision recorded in `status.md` Log; packages pinned (orchestrator) | decision rule |
| 2 | `contracts/rich-text/vocabularies.json` + `fixtures/` first drafts (backend-authored `input`/`canonical`, counts, corpus, legacy) | files reviewable by frontend; this unblocks plan-frontend |
| 3 | Domain: format, limits, rules, `WorkItem` pair; error-code constants; D tests | |
| 4 | Migration + configuration + checks; M tests | applies clean and twice |
| 5 | Application port, handler, fake-reducer tests | |
| 6 | Infrastructure reducer to the fixtures (allow-list stage, then canonical pass, then writer, then budget and gate); U tests grow with it | the oracle files are green |
| 7 | Read model + endpoint + error catalog + `docs/error-codes.md`; I tests | |
| 8 | Hostile corpus, property and timing tests; drift test; whole-suite `bash scripts/verify.sh` | DoD |

The frontend may start from step 2's files and the fixed api-contract; the
reducer (step 6) is not on its critical path except for the PATCH tests of
its own, which use the mock API (US-1 verification).

## 14. Risks, open points, and what needs the human

1. **The reducer is two implementations of one canonical form** (server and
   editor). Mitigated by the shared vocabulary and fixtures; residual risk is
   a divergence no fixture covers. The property test and the panel adding
   real `editorOutput` cases are the answer; budget for QA finding some.
2. **Spec wording gap, whitespace and the ceiling** (§ 5.1). The ceiling is
   "on the reduced markup" yet US-4 requires a blank 1 000 001 value to be
   refused. Plan counts read text as well; one-line confirmation by the
   human or the analyst's clarification in the spec (not blocking).
3. **Process-level hazards: stack depth and memory.** Hostile nesting must not
   crash the host; the walker is iterative and a gate bounds concurrency; both
   are asserted by tests that run in the normal suite (a crash would take the
   test host down, which is the signal). Spike Q6 may find a library API
   that recurses; then the fallback is our walker, not a new reason.
4. **Rollback runbook is owed and does not exist.** ADR-0039 and the spec
   NFR say the rollback story is stated "with a runbook (design stage)";
   design.md has none. The plan's § 8 note is the content; it belongs in
   `docs/runbooks/` (suggested `rich-text-rollback.md`). Written in step 4 by
   the backend developer unless the orchestrator wants it elsewhere.
5. **Documentation owed by ADR-0039 "on acceptance"** and not done yet:
   `docs/conventions/backend.md` pointer to the reduction and
   `contracts/rich-text/`, `contracts/README.md` section, ADR-0007's index
   entry "amended by ADR-0039". Backend conventions and contracts README are
   written in this task's step 2/6; the index entry is the orchestrator's.
6. **Texts of the two new codes** (api-contract § 4) are proposals; the
   markup text is quoted by the spec; `description_format_unknown` and the
   Kazakh of both are mine and `kkNeedsReview`.
7. **Library choice is provisional until the spike** (S1 vs S2); both paths
   keep the tests and the contract identical. `AngleSharp.Css` comes with S1.
8. **`Directory.Packages.props` and `Mustask.sln` are the orchestrator's
   write zone**: the two package lines are requested after the spike; the
   new test files live in existing projects, so the solution file does not
   change.
9. **Stored href form** (trimmed, tabs removed, not re-encoded) is a
   contract detail the editor's normaliser must match; it is in
   api-contract § 3 so the frontend plan picks it up.
10. **The existing `Save_Description_IsKeptAsEntered` test** keeps passing
    only because "no declaration is plain"; it is the regression anchor for
    the additive claim and is not edited.
