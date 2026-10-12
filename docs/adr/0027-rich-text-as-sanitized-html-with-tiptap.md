# ADR-0027: Store the work item description as server-sanitized HTML, edited with TipTap beside MUI

- Status: proposed
- Date: 2026-10-12
- Context: feat-018-work-item-rich-text, stage 03-design (first design
  artifact, size `L`, spec D-2) — `docs/features/feat-018-work-item-rich-text/`.
  Departs from [ADR-0007](0007-frontend-ui-stack-and-screen-state-canon.md)
  and [ADR-0019](0019-ui-stack-narrowed-to-actual.md)

## Problem

Описание of a work item is plain text today (mod-003 BR-M3-19, stored exactly
as sent). The task makes it rich text — headings, lists, links, code, tables —
edited in a WYSIWYG editor, which brings three things the repository has
none of: a browser editor library, a server-side HTML sanitizer, and a stored
format that is no longer "the characters the user typed". Each of these is a
library choice with long-term consequences, and the editor departs from the
UI stack ADR-0007 fixed ("swapping any of the libraries is a new ADR",
`docs/adr/README.md` § When to write an ADR). On 2026-10-12 the human decided
the shape (spec D-1..D-4): sanitized HTML in storage, TipTap in the browser, a
server-side reduction on every write, lazy conversion of legacy text, a limit
on visible text with a higher physical ceiling. This ADR records that decision
with the alternatives it was made against, so it is not reopened by the next
argument.

Forces: description content is written by any member and read by every
member, so the stored value is a stored-XSS vector for the whole panel; the
reference tracker's work items routinely carry formatted descriptions
(doc-002 BR-M3-4); existing descriptions must show exactly what they showed
(US-3); the API contract of mod-003 must stay additive for old clients (US-4);
the same editor and sanitizer will be reused for comments and the three type
text fields (spec § Decomposition).

## Options considered

### Stored format

#### Option F1: Markdown

- Pros: human-readable, diff-friendly, small; the safest thing to render
  (no markup in the stored form); no server-side HTML parsing for storage.
- Cons: the browser still needs a WYSIWYG layer, i.e. an editor that parses
  and serialises Markdown on every load and save; Markdown has no canonical
  form (nested lists, tables, hard breaks and escaping all have several
  spellings), so the dirty baseline of US-3 is hard to make stable; the
  tables the subset requires exist only in a dialect (GFM) and cannot hold
  block content in cells; most Markdown renderers pass raw HTML through, so a
  server-side HTML reduction would be needed anyway for any renderer that is
  not ours. A plain-text description containing `*` or `<` would change
  meaning unless escaped at conversion, which is the same lazy-conversion
  problem with a lossier target.

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

### Browser editor

#### Option E1: TipTap (ProseMirror, MIT), custom toolbar in MUI (chosen)

- Pros: headless — the toolbar and the chrome are ours, so the theme stays
  MUI's (ADR-0024); ProseMirror's schema is an enforced model, not a
  convention, and can be configured to the exact canonical form of US-2 (no
  merged cells, paragraph-only cell content, no marks in inline code);
  HTML in, HTML out; extensions for lists, links, code block and tables are
  in the open-source packages; large ecosystem and a long track record.
- Cons: a new dependency family (ProseMirror core and TipTap extensions)
  with a real bundle cost; TipTap also sells Pro and Cloud extensions — as
  with the DataGrid in ADR-0007, only the MIT packages are in the stack;
  jsdom does not implement the selection and layout APIs ProseMirror uses,
  so part of US-1 stays manual QA.

#### Option E2: mui-tiptap (TipTap with ready-made MUI components)

- Pros: toolbar and styling work already done in MUI components.
- Cons: a community wrapper in front of two fast-moving majors (TipTap and
  MUI); its look follows its own idea of the theme and its schema defaults,
  which we must override to reach the canonical form; the toolbar here is
  a dozen buttons with Russian labels, cheaper to own than to adapt. Revisit
  if the toolbar grows beyond the allow-list.

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

### Server-side sanitizer (.NET)

`Directory.Packages.props` references no HTML parser and no sanitizer today:
every option below is a new package. Versions are not chosen in this ADR —
pin at implementation, via `Directory.Packages.props`.

#### Option S1: HtmlSanitizer (Ganss.Xss) (chosen)

- Pros: purpose-built, allow-list model (allowed tags, attributes, schemes;
  deny by default); built on AngleSharp, a parser that follows the WHATWG
  HTML5 parsing algorithm — the same tree construction a browser performs,
  which is what mXSS resistance rests on; its removal and post-processing
  hooks cover the canonical-structure pass without a second library;
  widely used, maintained, MIT.
- Cons: it is an allow-list filter, not a canonicaliser: heading level
  mapping, table header rows into the body, wrapping loose text in `p`, mark
  nesting order and the visible-text count are ours, written as a pass over
  the same AngleSharp DOM; configuration is large (tags, attributes, schemes,
  plus which elements are dropped with content versus unwrapped) and must be
  covered by the corpus; its behaviour for removed elements must be
  confirmed against US-2 § Allow-list by a spike before the design is final;
  AngleSharp comes in transitively, so an explicit reference follows the
  repository's precedent (the `Microsoft.IdentityModel.Protocols.OpenIdConnect`
  comment in `Directory.Packages.props`).

#### Option S2: AngleSharp alone, with our own allow-list walker

- Pros: one dependency, full control of every rule including the canonical
  form and the visible-text count; no hook semantics to learn.
- Cons: the security-relevant part — which element and attribute is
  dropped, how URLs are read, what happens to foreign content and
  `template` — becomes our code to write and to keep correct against future
  parser changes; an off-the-shelf allow-list has been probed by far more
  hostile input than a project walker will be. Kept as the fallback if S1's
  hooks cannot express the canonical pass.

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
2. **Editor: TipTap on ProseMirror (E1), MIT packages only**, with a
   toolbar of MUI components and the Russian names of US-1. `mui-tiptap` is
   not adopted (E2). The editor lives in `shared/ui` as a generic
   `RichTextEditor`, so comments and the type fields reuse it, and is loaded
   lazily on the work item form route, not in the main chunk. Its schema is
   configured to the canonical form of US-2; the editor's own restrictions
   are a convenience, not the safeguard (BR-RT-2).
3. **Server reduction: HtmlSanitizer (S1) plus a canonical-structure pass
   over its AngleSharp DOM.** The allow-list of elements and attributes is
   US-2's, and this is the only place it exists on the server. Placement per
   `docs/conventions/backend.md`: an interface in `Mustask.Application`
   (`Abstractions/`) and its implementation in `Mustask.Infrastructure`, so
   `Mustask.Domain` stays free of third-party dependencies; the Domain keeps
   the format enum, the two limits and the rule that a value is validated
   after reduction (extending `WorkItemText.ValidateDescription`). The
   reduction returns the reduced html and its visible-text length (BR-RT-8)
   from one parse. Exact type and slice names are the design's.
4. **Limits (D-4):** visible text at most 100 000 UTF-16 code units, stored
   markup at most 1 000 000, a raw request above 2 000 000 refused before
   parsing; derivation and order of checks are BR-RT-7. The database check
   `ck_work_items_description_length` is widened to the physical ceiling and
   the visible limit is enforced in code.
5. **Fixture-driven parity.** Shared fixture files (input, canonical, editor
   output; html, visible count) run in the server and client suites; they are
   the contract between the two implementations of the canonical form
   (BR-RT-5, BR-RT-8).

**Departure from conventions.** ADR-0007/0019 fix the panel's UI stack as MUI
core and `@mui/icons-material`, with swapping or adding a library a new ADR.
This ADR adds the TipTap/ProseMirror family as one exception, scoped to the
rich-text editor. On acceptance (not before): `docs/conventions/frontend.md`
§ MUI gets a sentence naming the editor and linking this ADR; the index entry
of ADR-0007 gets "amended by ADR-0027"; `docs/conventions/backend.md` gets a
reference to where the reduction lives. The mod-003 api-contract amendments
are BR-RT-13 and not repeated here.

## Consequences

**Simpler.** One editor and one reduction serve every future rich-text
field; the stored value is readable without the editor; swapping the editor
later is a code change, not a data migration. A stored canonical value is not
rewritten by an unchanged save (BR-RT-11), so history (feat-015) and
concurrency will not see spurious changes.

**Harder.**

- *Two implementations of one canonical form.* The server pass and the
  editor schema plus normalisation step must agree; the only guard is the
  shared fixtures. A new element in the allow-list is a change in three
  places (sanitizer configuration, editor schema, fixtures) and goes through
  an amendment of this ADR or a new one.
- *The reduction is a security boundary.* Its configuration is reviewed like
  auth code; the hostile corpus (US-2) is a permanent test input and grows
  with every bypass found. A library upgrade is taken only with the corpus
  green.
- *mXSS.* A stored string is accepted only if stable: parsed again by an
  HTML5 parser, serialised and reduced again, it is identical (US-2). This
  is a parser round trip in the server test; a real-browser probe is not
  automated. Defence in depth on the client: html is never assigned to the
  page (`react/no-danger` is an error, BR-RT-12) and is loaded through the
  editor's schema, which drops what it cannot hold. A future read-only
  surface uses that same path or the same reduction.
- *Rollback.* Widening the stored bound is additive, but its down migration
  fails once any row exceeds 100 000 characters (runbook in the design).
  Rolling the release back leaves html in the store, which the older release
  shows as raw tags in a plain field; the stored-format marker stays so the
  release can be rolled forward again without data repair. No downgrade of
  html to plain is built (spec § Out of scope).
- *Bundle weight.* ProseMirror and the TipTap extensions are a material
  addition to a panel whose DataGrid decision once pushed one chunk to
  552 kB (ADR-0007). The size is measured at implementation and recorded in
  `design.md`; the editor is a lazy chunk on the form route. If the measured
  size is unacceptable to the human, this ADR is revisited rather than the
  chunk quietly accepted.
- *Test limits.* jsdom lacks the layout and selection APIs of a
  contenteditable: keyboard shortcuts, clipboard paste, undo and Tab are
  manual QA items (US-1 § Verification); there is no E2E in this task.
- *Supply chain.* ProseMirror/TipTap in the browser, HtmlSanitizer and
  AngleSharp on the server: both sides sit on the path of every description
  write and read and must be kept current.

**What later features inherit.**

- *Comments (feat-002) and the three type text fields
  (`feat-NNN-work-item-rich-text-type-fields`)* reuse `RichTextEditor`, the
  server reduction and the fixtures, with the same allow-list. A narrower
  profile for comments is an extension point of the reduction (a profile
  parameter), not a fork of the sanitizer. Each field gets its own
  stored-format marker and limit pair: the marker is per field, not per
  table.
- The legacy rule (BR-RT-9) and lazy conversion apply to each text field that
  was plain before; every new rich-text field starts with the declaration
  contract, never with sniffing.
- The first read-only renderer (comments) is built under BR-RT-12: through
  the editor schema or the same reduction, whitespace preserved.
- Full-text search (feat-003), CSV import/export and history diffs
  (feat-015) read HTML; their handling of markup is theirs.

**Revisit when:** the measured bundle cost is rejected by the human; the
canonical form drifts between server and editor in practice and the fixtures
prove inadequate; HtmlSanitizer or AngleSharp stop being maintained; or the
product needs collaborative editing, for which the editor's JSON document
(F2) becomes the better stored form.

**Backlog generated:** none beyond the follow-up line already in the spec;
the documentation updates named under Decision are done at acceptance.
