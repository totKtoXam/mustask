# Review: feat-018-work-item-rich-text

## Round 1 — requirements

Reviewed: `spec.md` and `stories/US-1..US-3` at 4393e13, against doc-002
BR-M3-4 and § NFR, mod-003 spec (BR-M3-19, § Screens) and api-contract
(§ 0.6, `description`), feat-022 spec (BR-M3-36, § Decomposition amendment),
`docs/ai-workflow/routing.md` (L thresholds), `docs/adr/README.md`,
ADR-0007/0019, and the code the spec relies on: `WorkItemText.ValidateDescription`
(`src/backend/Mustask.Domain/WorkItems/WorkItemText.cs:41`),
`ck_work_items_description_length` (`char_length(description) <= 100000`,
`WorkItemConfiguration.cs:101`), the form's `TextField`
(`src/frontend/src/features/workitems/pages/WorkItemFormPage.tsx:379-390`),
the draft diff (`src/frontend/src/features/workitems/lib/draft.ts:76`, `:127`),
the client limit (`lib/rules.ts:52`) and the read model
(`WorkItemReadModel.cs:155-176`: the list never returns the description).

Verdict: **needs-human-decision** — OQ-1..OQ-4 are the human's, and the size
has to be re-confirmed (R-1). Nothing below was edited into the spec: the
majors need the analyst, and most depend on the OQ answers.

### Rule → story mapping

| Rule | Stories | Gap |
|---|---|---|
| BR-RT-1 subset | US-1 (toolbar), US-2 (kept) | heading levels, nested lists, table operations undefined (R-9) |
| BR-RT-2 server enforcement | US-2 | — |
| BR-RT-3 stripping, link schemes | US-2, US-1 (link prompt) | deny-list, not attribute allow-list; scheme edge cases (R-4) |
| BR-RT-4 no inline images | US-1 (paste), US-2 | drag-and-drop of files (R-11) |
| BR-RT-5 idempotency | US-2 | US-3 cites it without a scenario; editor round trip not covered (R-3) |
| BR-RT-6 empty → empty | US-2 | boundaries of "visible content" (R-6) |
| BR-RT-7 limit | US-2, US-3 | ceiling, counting, client check, rollback (R-6) |
| BR-RT-8 legacy text verbatim | US-3 | whitespace, CR/LF, NUL, silent conversion on unrelated save (R-3, R-7) |
| BR-RT-9 read-only rendering | US-3 | no surface exists today (R-8) |
| doc-002 BR-M3-4: Критерии приёмки, Шаги воспроизведения | out of scope → follow-up line | — |
| doc-002 BR-M3-4: comment text | out of scope | not carried by any line (R-12) |
| mod-003 BR-M3-19, api-contract § 0.6 | superseded | no amendment named (R-2) |

### R-1
Severity: blocker
Affects: spec § Decomposition (spec.md:105), OQ-1 (spec.md:116-139), OQ-4 (spec.md:172-177); status.md size
Problem: The size recommendation is wrong by the spec's own text. routing.md
(lines 175-187) makes "an ADR is accepted" an `L` threshold, with no opt-out,
and says "Lowering the size is not allowed under any circumstances" and "a
doubt between two sizes always means the larger one" (routing.md:160, :206).
OQ-1 itself says "Whichever is chosen, the ADR is written and the human accepts
it before code" (spec.md:138) — so the ADR threshold fires under every option,
and "keep M, the ADR is the first design artifact" is not an option the
routing rules offer. The human's own route confirmation (status.md:31) says
"escalate to L if an ADR turns out to be needed" — the spec has established
that it is needed. The ADR is needed twice over: a library with long-term
consequences (a security-critical server sanitizer, a browser editor), and a
departure from ADR-0007's fixed UI stack ("swapping any of the libraries is a
new ADR", ADR-0007:105; adr/README "departs from conventions"). OQ-4 is
therefore not a free-standing yes/no: it is part of the same ADR.
A second `L` threshold is not mentioned at all: mod-003's api-contract
§ 0.6 says `description` is "plain text, stored exactly as sent"
(mod-003 api-contract.md:96). Re-reading the same field as HTML is "a change of
the type or the semantics of something existing" — breaking (routing.md:189-192)
— unless the contract change is made additive (R-2).
Required resolution: State the size as `L` (gates unchanged, the full
pipeline, ADR at the design stage) and drop "keep M" from OQ-1 and from
spec.md:105. If the human wants M anyway, it is an explicit override of the
routing rule, recorded as such in status.md — not one of the options. Fold OQ-4
into OQ-1's ADR scope.
Status: open
Resolution:

### R-2
Severity: major
Affects: BR-RT-1, BR-RT-8, OQ-2 (spec.md:140-153), US-3 "Other readers" (US-3:45-48); mod-003 BR-M3-19, mod-003 api-contract § 0.6
Problem: The spec never says what the API carries. Today `PATCH`/`GET`
`description` is plain text stored as sent. After this task: does the server
treat every incoming `description` as HTML? Then any client that still sends
plain text (the existing integration tests, a script, the future CSV import)
gets "a < b" mangled and `<b>` turned bold — the exact thing BR-RT-8 forbids for
stored text, now happening to new writes. Under OQ-2 (a) the format marker
must cross the API somehow (a new `descriptionFormat` field, or the server
escapes legacy text on read and always returns HTML) — each choice has a
different contract and a different client. US-3 "any reader that returns the
description returns it in a form that shows the same text" is not testable
until that is fixed. The spec also supersedes mod-003 BR-M3-19 and its
form copy (mod-003 spec:89, :134, :224) and feat-022 BR-M3-36, which defines the
three type texts "as BR-M3-19 has it for Описание" — yet the only amendment
named is feat-022's decomposition note (spec.md:108-109).
Required resolution: Add a rule fixing the contract shape: request format
(how the server knows a value is HTML — e.g. a new required/explicit format
field, additive), response format for legacy and new values, and which side
escapes legacy text. Make it additive so R-1's breaking-contract threshold does
not fire, or say that it does. List the amendments owed: mod-003 BR-M3-19 and
api-contract § 0.6, feat-022 BR-M3-36 (so it no longer leans on a rule that
changed).
Status: open
Resolution:

### R-3
Severity: major
Affects: BR-RT-5, BR-RT-8, US-1 "Formatting survives save and reopen", US-3 "Opening without saving changes nothing" (US-3:34-37), "First save" (US-3:39-43)
Problem: The form decides what to send by `draft.description !== item.description`
(draft.ts:76, :127) and enables Save on `dirty` (WorkItemFormPage.tsx:345).
An editor normalises what it loads: a legacy plain text becomes `<p>…</p>`,
and editor-specific HTML (table `colgroup`/`style="min-width"`, `<p>` inside
`<li>`, attribute order, entity forms) re-serialises differently from what the
server's sanitizer stored. Consequences the spec does not address: (1) the form
is dirty on open, so leave-guards and the Save button misbehave; (2) a member
who changes only Статус silently converts the legacy description, and if the
conversion is lossy (R-7) changes its text without touching it; (3) every save
of a formatted description rewrites it, so BR-RT-5's idempotency holds on the
server but not end to end. US-3:34-37 only covers "leave without saving". "Written
in the new format on the first save" (spec.md:143) does not say whether that is
any save of the item or a save that changes Описание.
Required resolution: Add rules and scenarios: (a) opening a work item and
saving another field leaves the stored description byte-for-byte unchanged
(legacy and formatted alike); (b) opening the form does not make it dirty;
(c) round-trip stability: a value the server stored, loaded into the editor
and serialised without edits, is what the server stores again (editor ∘
sanitizer is idempotent, not only the sanitizer); (d) "first save" means the
first save that changes Описание.
Status: open
Resolution:

### R-4
Severity: major
Affects: BR-RT-1, BR-RT-3 (spec.md:55, :57), US-2 "Executable…", "Links…", "Hostile corpus" (US-2:23-42)
Problem: The sanitization rule is a deny-list (script, iframe, style,
event attributes, inline style) next to an element allow-list, and leaves the
security-relevant parts open:
- Attributes: no allow-list. `id`/`name` (DOM clobbering), `class`, `target`,
  `title`, `colspan`/`rowspan`, `start`, a code block's `language-*` class —
  which survive? "Nothing else survives" (BR-RT-1) reads as elements only.
- Disallowed elements: dropped with their content or unwrapped keeping the
  text? US-2:26 "the ordinary text … remain" — is a `<script>`'s body
  "ordinary text"? `<div>`, `<span>`, `<font>`, `<u>`, `<s>`, `<blockquote>`,
  `<hr>`, `<br>` — kept, unwrapped or dropped? `<br>` matters for legacy line
  breaks (R-7).
- Link addresses: relative (`/x`), protocol-relative (`//evil.example`),
  fragment-only (`#x`), schemes with leading whitespace or embedded
  tab/newline/control characters (`java\tscript:`), uppercase (`JAVASCRIPT:`),
  `href` without scheme. Only http/https/mailto are named; how the scheme is
  determined (after URL parsing as a browser would) is not.
- `target`/`rel`: US-3:54 asks for "no access to the opening window" at render
  time, but not whether the server strips `target` or forces
  `rel="noopener noreferrer"`.
- Mutation XSS: the corpus assertion (US-2:42) checks the stored string. mXSS
  fires only when a browser re-parses that string (namespace confusion in
  `svg`/`math`, `noscript`, `<style>` inside foreign content), so a passing
  string check proves nothing about it.
Required resolution: Rewrite BR-RT-3 as an allow-list of elements *and*
their attributes; state "dropped with content" for script/style/iframe/object/
embed/template/svg/math/noscript and "unwrapped, text kept" for any other
unknown element; state link handling (absolute http/https/mailto only after
URL parsing; relative and protocol-relative dropped; `target` stripped or
`rel` forced). Add the mXSS check as a verifiable criterion: the stored value,
parsed again by an HTML5 parser and sanitised again, is identical to itself;
and the client renders stored HTML only through the editor schema or a
client-side sanitiser, never by raw `innerHTML` (defence in depth, BR-RT-9).
Status: open
Resolution:

### R-5
Severity: major
Affects: NFR Tests (spec.md:85-86), US-1 (all scenarios), NFR Speed (spec.md:78-79)
Problem: No E2E is right (the human's instruction) and the spec respects it,
but it then puts criteria into US-1 that a jsdom test cannot verify reliably:
keyboard shortcuts, paste of HTML clipboard data, undo/redo, Tab moving focus
in and out of a contenteditable. ProseMirror-family editors depend on
selection and layout APIs jsdom does not implement. Without saying how each is
verified, QA will either mark them unverifiable or reach for E2E against the
instruction. The Speed NFR "opening and saving the form within 1 s" is a
client-side bound for the editor rendering 100 000 characters, which only a
browser run can measure; doc-002 § NFR states server response times.
Required resolution: For each US-1 scenario say the verification level —
component test with the real editor (and which jsdom shims are acceptable),
the library's own guarantee recorded in the ADR, or a manual QA check — and
restate Speed as server response time for GET and PATCH with a
100 000-character description (plus the reduction at the physical ceiling,
R-6), as doc-002 has it.
Status: open
Resolution:

### R-6
Severity: major
Affects: BR-RT-6, BR-RT-7 (spec.md:60-61), OQ-3 (spec.md:154-171), US-2 limit scenarios (US-2:54-67), US-3 "Legacy text of any length" (US-3:29-32)
Problem: OQ-3 (c) is recommended but its parts are left open, and US-2/US-3
are written against it:
- "Visible text" is undefined: entities decoded? Do block boundaries
  (paragraph, list item, table cell) count as a character? UTF-16 code units, as
  `WorkItemText` and `char_length` count today (WorkItemText.cs:6-8), or code
  points? US-2:55-61 cannot be tested without it.
- The physical ceiling has no number, and no rule for what happens above it.
  US-3:30 (100 000 legacy characters of `<`, `>`, `&`) escapes to ~400 000
  characters before paragraph markup, and a text of 100 000 line breaks becomes
  100 000 empty paragraphs (~700 000). If the ceiling is lower, US-3's scenario
  contradicts it; the ceiling must be derived from the worst-case expansion of a
  legal legacy text, and the counting must ensure a legacy text's visible count
  never exceeds its original length.
- Which reason a value over the ceiling but under 100 000 visible gets — the
  existing message «Не более 100 000 символов.» would then be false.
- The client check (`rules.ts:52`, raw `length`) must count the same way, or
  the client refuses what the server accepts.
- BR-RT-6 boundaries: a table of empty cells, an empty code block, a
  whitespace-only code block (whitespace there is content), a link with no
  text — empty or not? And a legacy text of only blank lines: BR-RT-8 promises
  its line breaks, BR-RT-6 stores it as empty on first save.
- Widening `ck_work_items_description_length` is additive, but its down
  migration fails once any row exceeds 100 000 — the "reversible" note in
  OQ-3/OQ-2 should say so.
Required resolution: Define visible-text counting precisely (units, entities,
block boundaries); fix the ceiling as a number derived from worst-case legacy
expansion; name the reason above the ceiling; make the client counter a
requirement; settle the BR-RT-6 boundary cases and the blank-lines-only legacy
case; note the down-migration limit.
Status: open
Resolution:

### R-7
Severity: major
Affects: BR-RT-8 (spec.md:62), US-3 "Legacy text shows the same…" and "First save" (US-3:19-22, :39-43)
Problem: "Exactly the visible text and characters it had" collides with HTML
semantics the spec does not address: runs of spaces and tabs collapse when
rendered as HTML (pasted logs and indented steps are common in descriptions);
the HTML parser normalises CR LF to LF and replaces U+0000 with U+FFFD
(tech-013 m2 already tracks U+0000 in work item texts); single vs double line
breaks have to become `<br>` vs paragraphs. Whether legacy text is shown with
`white-space: pre-wrap`, converted line-by-line to paragraphs, or into code
blocks decides whether "character for character" holds after the first save
(US-3:43 "the unchanged lines show the same text").
Required resolution: State the conversion rule for legacy text (line → paragraph
or `<br>`, blank lines, runs of whitespace and tabs, CR LF, control
characters) and say which differences are accepted as invisible. Add a scenario
with leading indentation and multiple spaces.
Status: open
Resolution:

### R-8
Severity: major
Affects: BR-RT-9 (spec.md:63), US-3 "Read-only rendering" (US-3:50-55), Operation completeness (spec.md:69)
Problem: There is no read-only surface for Описание today: the list does not
return it (`WorkItemReadModel.cs:125`, `includeDescription: false`), the
backlog and board do not show it, and the form is the only place it appears.
"Shown anywhere outside the editor" has nothing to test against, and building a
renderer for a consumer that does not exist is scope beyond «Описание on the
form». Either the scenario is unverifiable or it adds a component nobody uses.
Required resolution: Name the surfaces that exist in this task (the editor
itself, a disabled editor while saving), and phrase BR-RT-9 as a requirement on
future readers (any surface rendering the stored value goes through the same
schema/sanitiser) — or name the surface this task adds and why.
Status: open
Resolution:

### R-9
Severity: minor
Affects: BR-RT-1, US-1 "The toolbar offers exactly the safe subset" (US-1:18-23), "Operable by keyboard" (US-1:53-58)
Problem: The subset is under-specified for an editor: heading levels (h1 would
break the page's heading order — the section heading «Описание» is an `h2`,
mod-003 spec:224); nested lists (if Tab is not indentation, US-1:56, a keyboard
user needs indent/outdent controls); table operations (insert/delete row and
column, header row) — a table toolbar is a sizeable piece of US-1 that no
criterion bounds; whether a link is followed on click inside the editor.
Required resolution: Fix heading levels (e.g. h3–h4 under the section's
h2), nested lists and their keyboard controls, the table operations offered,
and link-click behaviour in edit mode.
Status: open
Resolution:

### R-10
Severity: minor
Affects: NFR Language (spec.md:83-84), US-1 (US-1:23, :46, :63)
Problem: No interface copy is given — toolbar names, the link prompt, the
scheme refusal ("told which addresses are accepted"), the placeholder and
helper (mod-003 has «Добавьте описание», «До 100 000 символов»; does the helper
stay?). "Every control has a Russian accessible name" cannot be asserted
without the strings.
Required resolution: Quote the strings in the spec, or state that design.md
owns them and US-1 asserts against it.
Status: open
Resolution:

### R-11
Severity: minor
Affects: BR-RT-4, US-1 "Paste" (US-1:37-41)
Problem: Drag-and-drop of an image file or an image from another page into
the editor is not covered (the browser may insert it or navigate to the file);
nor is pasting a file from the clipboard. The BPMN step "form shows the
sanitized result" (spec.md:45) has no US-1 criterion: after the server drops
something (e.g. a link added outside the editor's own checks), the editor
should show the stored value, not the draft.
Required resolution: Add drop/paste-of-file behaviour to US-1, and a criterion
that after a successful save the editor holds the server's returned value.
Status: open
Resolution:

### R-12
Severity: minor
Affects: Out of scope (spec.md:90-93), Decomposition follow-up line (spec.md:106)
Problem: Out of scope says the three type texts "and comments … become the
follow-up line below", but that line covers only the three fields; doc-002
BR-M3-4 includes comment text and `feat-002-work-item-comments` (backlog.md:36)
does not mention rich text, so comment rich text is owned by nobody. The
follow-up line is sized `S`, but it crosses domain, API and screen and converts
three stored fields — routing 2.1 makes that `M` at least.
Required resolution: Either add comments to the follow-up line or name the
line (feat-002 amendment) that carries them; size the follow-up `M`.
Status: open
Resolution:

### R-13
Severity: minor
Affects: OQ-1 (spec.md:116-139), OQ-2 (spec.md:140-153)
Problem: Options are mostly complete; gaps: OQ-1 names only TipTap-like
editors — an MUI-themed wrapper (e.g. `mui-tiptap`) is directly relevant to
OQ-4's theming condition, and Lexical is the other mainstream MIT choice; (a)'s
cons omit the parser-differential (mXSS) risk of server-side HTML sanitising
(R-4). OQ-2 (a) "reversible" holds for the schema, but rolling back the release
after some descriptions were saved as HTML shows raw tags in the old plain-text
field — every option shares that, and the ADR should state the rollback story.
OQ-2 should also list, as ⛔, detecting the format by content without a marker
(ambiguous for a legacy `<b>`), so the reason the marker exists is on record.
Required resolution: Add those points to the options; no change to the
recommendations.
Status: open
Resolution:

### Size

`M` is not right: `L` — the ADR threshold fires under every OQ-1 option by the
spec's own wording and by the human's route confirmation (R-1), and the
contract change is breaking unless R-2 makes it additive. Scope «Описание» only
is respected (the other fields and comments are out of scope); nothing in the
spec asks for E2E.
