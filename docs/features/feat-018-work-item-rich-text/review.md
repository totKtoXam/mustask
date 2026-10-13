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

## Round 2 — requirements (delta)

Read: `spec.md` and `stories/US-1..US-4` at e07f409 against the round-1
findings, plus mod-003 api-contract § 0.6 and § 2.4 (PATCH semantics) for the
additive-contract check. The human's decisions D-1..D-4 (spec.md:23-30) are
not re-opened.

Verdict: **needs-fixes** — two new majors (R-14, R-15), both wording-level and
both for the analyst (iteration 2 of 2); no question for the human. Once they
are fixed the spec is ready for gate-1 without another review round.

### Round-1 findings

| # | Status | Where resolved |
|---|---|---|
| R-1 | resolved | size `L` stated (spec.md:11-12, :142), ADR a design deliverable (D-2, spec.md:28), OQ-4 folded into it; status.md:4, :34 |
| R-2 | resolved | BR-RT-10 additive contract with explicit format, absent = `plain` (spec.md:84); amendments listed in BR-RT-13 (spec.md:87); US-4:18-47 |
| R-3 | resolved | BR-RT-11 (spec.md:85), BR-RT-5 end to end (spec.md:79); US-3:44-59, :67-70; US-2:104-108. The canonical-form rule it introduced is not yet consistent with the allow-list — R-14 |
| R-4 | resolved | BR-RT-3 (spec.md:77); US-2 § Allow-list (US-2:15-47); mXSS re-parse scenario (US-2:94-97); `react/no-danger` (spec.md:103), BR-RT-12. The re-parse uses the sanitizer's own parser (US-2:119-124), not a browser's: accepted-risk, covered by the client loading through the editor schema and the manual probe |
| R-5 | resolved | US-1 § Verification (US-1:120-142); NFR Speed restated as server time (spec.md:104-107) |
| R-6 | resolved, except R-15 | BR-RT-7/8 (spec.md:81-82): units, entities, block separators, ceiling 1 000 000 derived from the legacy worst case, own reason; client counter (US-1:98-102, shared fixtures US-4:49-52); BR-RT-6 boundaries (spec.md:80, US-2:110-114); blank-lines legacy (US-3:61-65); down-migration (spec.md:116-120) |
| R-7 | resolved | legacy rule in BR-RT-9 (spec.md:83); US-3:19-37 incl. indentation and CR LF; BR-RT-12 requires whitespace-preserving rendering for future surfaces |
| R-8 | resolved | BR-RT-12 (spec.md:86); out of scope (spec.md:127-128) |
| R-9 | resolved | BR-RT-1 levels 3-4, nesting 3 (spec.md:75); US-1:38-49, :80-84 |
| R-10 | resolved | US-1 § Interface copy (US-1:15-26); error-code entries owed at design (spec.md:154-155) |
| R-11 | resolved | US-1:69-73, :109-113 |
| R-12 | resolved | follow-up line carries comments, sized `M` (spec.md:143-147) |
| R-13 | resolved (moot) | OQs settled by the human; rollback story owed to the ADR (D-2, spec.md:116-120); the marker's reason is BR-RT-10 |

Numbers checked: 100 000 LF → 100 001 `<p></p>` = 700 007 is the true
maximum of the legacy rule (an LF costs 7 per source character, `&` 5, U+00A0
serialised as `&nbsp;` 6, `&\n` pairs 6), so the 1 000 000 ceiling holds for
every legal legacy text, and the visible count of a converted value equals its
length minus the CRs, never more. 2 000 000 before parsing is consistent: the
panel never sends more than the ceiling (it checks its own serialisation, BPMN
step H), so the cap only bites hostile or foreign input.

### R-14
Severity: major
Affects: BR-RT-5 (spec.md:79), US-2 § Allow-list (US-2:19-40), US-2 "The editor's canonical form is the server's" (US-2:104-108), US-3 "Editor and server agree on the canonical form" (US-3:67-70)
Problem: BR-RT-5 promises that *any* value the server stored, loaded into the
editor and serialised without edits, reduces back to itself, and US-3:67-70
demands byte equality between the editor's serialisation and the server's
canonical form. The allow-list admits structures the editor's schema cannot
hold, so for API-written html both are false:
- `thead` is kept (US-2:26), but a TipTap table has no `thead`: the header row
  is `th` cells inside `tbody`. A stored `<thead>` loads and serialises as
  `<tbody>`.
- Inline content directly inside `li`, `th`, `td` is kept as is (the loose-text
  rule, US-2:38-39, only covers the top level and inline beside blocks), but the
  editor always emits `<li><p>…</p></li>` and `<td><p>…</p></td>`.
- Marks inside inline code (`<code><strong>x</strong></code>`) are allowed,
  but TipTap's code mark excludes other marks; headings, code blocks and `br`
  in a heading are allowed by the table but not stated for the editor.
- TipTap's own table and link output (`colgroup`/`col`, `style="min-width…"`,
  `colspan="1" rowspan="1"`, link `target="_blank"` and `rel="… nofollow"`) is
  removed by the server, so "serialised without edits" equals the canonical
  form only if the client applies the same normalisation after `getHTML()`;
  the spec does not say the client serialiser includes it, and the client's
  markup-ceiling check (BPMN H) has to measure after it.
A fixture set that avoids these cases passes while the rule stays false;
one that includes them fails.
Required resolution: Make the server's output the editor's canonical form and
say so in US-2: `thead` rows go into `tbody` (header cells stay `th`); inline
content directly in `li`/`th`/`td` is wrapped in `p`; no marks inside `code`;
which blocks may sit in a list item. State that the client's serialisation is
the editor's output plus a normalisation to that form, used for the dirty
baseline, the request and the client-side ceiling check. Alternatively narrow
BR-RT-5 to values the editor itself produced — but then say a foreign html
value may be re-canonicalised on the first save that changes Описание.
Status: open
Resolution:

### R-15
Severity: major
Affects: US-3 "Legacy text of any length still saves" (US-3:39-42); BR-RT-7 (spec.md:81)
Problem: Both legacy texts have a visible count of exactly 100 000 after
conversion (100 000 `&`; 100 000 LF → 100 000 block separators). "Adds one
character and saves" makes the visible text 100 001, which BR-RT-7 and
US-4:54-57 refuse with `description_too_long`. The scenario's "both saves
succeed" contradicts the rule it is meant to exercise, and the client check
(US-1:98-102) would refuse before sending.
Required resolution: Make the edit length-neutral (replace one character, or
make one existing character bold), or start from 99 999 characters. Keep the
markup figures; see m-1 for the first one.
Status: open
Resolution:

### Minors (round 2)

- **m-1** US-3:42, US-4:81, spec.md:81 — 100 000 `&` in one paragraph is
  500 007 code units (`<p>` + `</p>`), not 500 000. Harmless to the ceiling;
  fix the figure, the test asserts it.
- **m-2** BR-RT-8 (spec.md:82) — "consecutive blocks" is ambiguous for nested
  blocks: in the editor's form `<li><p>a</p></li>` and `<td><p>a</p></td>`,
  li and p are both blocks. Say separators go between leaf (text-holding)
  blocks, and add fixtures in the editor's canonical form beside the p-less
  ones of US-4:50.
- **m-3** US-1 has the markup-ceiling copy (US-1:26) and the BPMN a client
  check (spec.md:56), but no scenario for the client refusing above
  1 000 000.
- **m-4** Reason precedence is open when visible > 100 000 and reduced markup
  > 1 000 000 at once (one reason per field, mod-003 § 0.6); and when an
  unknown format arrives with no description (US-4:34-42: refused or
  ignored?).
- **m-5** mod-003 § 2.4 "a value equal to the stored one changes nothing,
  `changedAt` moves only on a change": say equality compares the pair (value,
  format), so a plain write of "<p>a</p>" over a stored html "<p>a</p>" is a
  change, and an html re-send of the stored canonical value is not.

## Round 3 — requirements (scope amendment)

Read: `spec.md` and `stories/US-1..US-7` in the working tree (amendment of
2026-10-13, uncommitted over d96c771), status.md § Route confirmation,
routing.md (L thresholds, story cap, escalation by volume), 01-business-analysis
§ Rules (line budgets), doc-002 BR-M3-4. Delta only: rounds 1–2 stay closed;
D-1..D-7 and the no-E2E instruction are not re-opened.

Verdict: **needs-fixes** — no blocker, six majors, all wording-level and for
the analyst; no question for the human. After the fixes, one delta read
(the L cap allows two per round) and the spec goes to gate-1.

### Rule → story mapping (new and changed rules)

| Rule | Stories | Gap |
|---|---|---|
| BR-RT-1, -3 (widened subset, class) | US-1, US-2, US-7 | US-2 "one token" vs US-7 split (R-18) |
| BR-RT-4 (images out of A) | US-1, US-2 | cites "D-8", which is doc-002's (m-7) |
| BR-RT-5 (canonical form) | US-3, US-7 | ragged tables, code vs enclosing marks, nested same-family tokens (R-17, R-18) |
| BR-RT-7, -8 (code points) | US-1, US-3, US-4, US-5 | amplification before the ceiling check (m-10) |
| BR-RT-13 (amendments) | — (orchestrator) | doc-002 wording (m-6) |
| BR-RT-14 palettes | US-5, US-7 | — |
| BR-RT-15 mark order, paste | US-5, US-7, US-1 | pasted `font-weight` etc. has no scenario (m-12) |
| BR-RT-16 code | US-6, US-7 | Tab exception contradiction (R-16); layout-bound exit (R-21) |
| BR-RT-17 tables | US-6, US-7 | "no cells invented" vs a rectangular table (R-17); TSV paste (R-20) |
| BR-RT-18 links | US-5, US-3 | auto-link scope and undo (R-19) |
| BR-RT-19 emoji | US-5, US-4 | — |
| BR-RT-20 lists, clear formatting | US-1, US-5 | — |

### R-16
Severity: major
Affects: BR-RT-16 (spec.md:99), NFR Accessibility (spec.md:125-127), BR-RT-17 (spec.md:100), US-1:81, US-6:33-37, :61-65
Problem: BR-RT-16 calls Esc-then-Tab in a code block "the one documented
exception to 'Tab is never captured'", and the NFR says "Tab is never captured,
except inside a code block". BR-RT-17, US-1:81 and US-6:33-37 capture Tab in
table cells too. Three normative places disagree on the number of exceptions,
and the table one has no announcement (the code block has the live-region
hint, US-6:21) and no quick exit: a keyboard user in cell 2 of a 10×10 table
tabs through 98 cells to leave. The Esc "arming" is also undefined (does any
other key in between disarm it? does Esc while a picker is open count?).
Required resolution: Name both exceptions in BR-RT-16 and the NFR. Give
tables the same exit as code (Esc then Tab leaves the editor from any cell),
or state that tabbing through is the accepted exit. Announce the table
behaviour on entering a table (copy in US-6). Define Esc as arming exactly the
next keystroke, and not when a popup/picker consumed the Esc.
Status: open
Resolution:

### R-17
Severity: major
Affects: BR-RT-17 (spec.md:100) "no cells invented", BR-RT-5 (spec.md:88), US-6:39-43, :45-48, US-7:39-42, :84-87
Problem: Unmerging "content kept in the one cell, no cells invented" leaves
ragged rows (`<tr><td colspan=2>a</td></tr><tr><td>b</td><td>c</td></tr>` →
1 cell then 2). The editor's table model (prosemirror-tables) assumes a
rectangular grid and its normalisation pads short rows with empty cells on the
next transaction; its commands misbehave on ragged maps. Consequences: a stored
ragged table (API-written, or the server's own unmerge output) loads and
re-serialises with extra cells, so BR-RT-5's byte identity and "not dirty on
open" (US-3) fail; and unmerging without filling the spanned positions shifts
every following cell one column left, scrambling pasted data. A ragged TSV
range (US-6:45-48) has the same problem. Empty `<tr>`/`<table>` have no rule.
Required resolution: Make the canonical table rectangular: a cell with
colspan n / rowspan m keeps its content at its top-left position and each
spanned position becomes an empty cell `<td><p></p></td>` (`th` in a row of
`th`, or say which); short rows are padded at the end to the widest row; rows
with no cell and tables with no row are removed. Replace "no cells invented"
accordingly and add a ragged and a rowspan case to the US-7 fixture.
Status: open
Resolution:

### R-18
Severity: major
Affects: BR-RT-15 (spec.md:98), US-7 § Canonical structure (US-7:29-35), US-7:69-72, US-2:23-24, BR-RT-5 normalisation list (spec.md:88)
Problem: The canonical form is underdetermined where the editor's schema
will decide on its own, so the (input, canonical, editor output) triples can
disagree:
- Inline code vs enclosing marks: US-7 says marks *inside* `code` are
  unwrapped, but not what `<strong><code>x</code></strong>` or
  `<a href=…><code>x</code></a>` become. TipTap's code mark excludes every
  other mark, so the editor drops the bold and the **link** — a linked
  identifier loses its link. Say which survives (and whether link is the
  exception).
- Nested elements of one family (`<span class=rt-color-red>a<span
  class=rt-color-blue>b</span></span>`): "within a family the first valid
  token wins" covers one element only; the editor applies the inner mark
  (same-type marks exclude each other). Say inner wins.
- US-7:69-72 expects "strong, em, s, s elements" for
  `<strike>c</strike><del>d</del>`, but BR-RT-15 merges adjacent equal marks:
  canonical is one `<s>cd</s>`.
- US-2:23-24 says `span`/`mark` keep "one … token, else the element is
  unwrapped"; US-7 splits several valid tokens into nested elements. Align
  US-2 with US-7.
- Cells and list items "hold only p": a heading or `ul` becomes `p`, but a
  `pre` (multi-line) inside a cell or `li` — one `p` per line, or `br`?
- BR-RT-5's normalisation enumerates what it removes (colgroup, style,
  colspan=1, data-*, target, nofollow); TipTap also emits `ol start`/`type`
  when set. State the rule as "every attribute not in the allow-list".
Required resolution: Add these to US-7 § Canonical structure with a fixture
each; fix US-7:72 and US-2:23-24.
Status: open
Resolution:

### R-19
Severity: major
Affects: BR-RT-18 (spec.md:101), US-5:80-83, :91-96
Problem: Auto-link is stated as a goal, not a rule the test can pin down, and
the library default (linkify) does more than the rule allows: it links bare
domains and e-mails (`example.org`, `a@b.ru`) and file names on country TLDs
(`setup.py`, `readme.md`, `main.rs`); trailing punctuation
(`(см. https://example.org).`) is undefined; auto-link inside inline code and
headings is not addressed; pasting a URL over a selection (link-on-paste) is
not addressed. "Ctrl+Z turns the last one back into plain text" is not the
editor's behaviour out of the box: auto-link is applied in the same history
step as the typed or pasted text, so undo removes the text, not just the link —
this needs dedicated work and must be stated precisely. Finally the prompt
refuses "www.example.org" (US-5:80-83: not absolute) while auto-link accepts
it as https — two rules for the same input.
Required resolution: State: only text starting `http://`, `https://`,
`mailto:` or `www.` is auto-linked (no bare domains, no bare e-mails); trailing
`.,;:!?)»"'` not part of the address unless balanced; never inside inline
code; pasting an allowed address over a selection links the selection (or
not — choose). Undo: "the first Ctrl+Z after an auto-link removes the link and
keeps the text". The prompt accepts `www.` the same way (prefix https). Add
fixtures for each.
Status: open
Resolution:

### R-20
Severity: major
Affects: BR-RT-17 (spec.md:100), US-6:45-48, US-1:67, BR-RT-9 spirit
Problem: "Plain text with tabs and line feeds, at least 2 lines of at least
2 columns" becomes a table. US-1:67 says pasted plain text is inserted as
paragraphs, line breaks kept. Tab-separated logs, `make` output, tab-indented
steps — common in a description — now turn into tables without the member
asking. The rule also leaves ragged lines (R-17) and the escape hatch open.
Required resolution: Narrow the trigger (every line has the same number of
tabs, ≥ 1, and ≥ 2 lines; and/or only when the clipboard came with a
`text/html` table), add "Ctrl+Shift+V pastes as plain paragraphs" (or another
explicit escape), and amend US-1:67 to point at the exception.
Status: open
Resolution:

### R-21
Severity: major
Affects: BR-RT-16 (spec.md:99), US-6:50-53, :67-70, US-6 § Verification (:94-103)
Problem: Two exits depend on layout, which jsdom does not have, yet are
marked C: "ArrowDown on its last line" of a code block (a wrapped last line is
only known from layout; the editor's own exit fires at the end of the block's
text) and "ArrowDown from its last row" of a table (vertical motion and the gap
cursor use coordinates). The table scenario has no row in the verification
table at all. And the obvious way to meet "an empty paragraph follows the
table" — a trailing-paragraph plugin — appends `<p></p>` on load, which breaks
BR-RT-5's byte identity and the not-dirty-on-open rule for every description
that ends with a table or a code block.
Required resolution: Reword the code exit to "ArrowDown at the end of the
block's text" (C). For the table, either mark ArrowDown as M, or specify a
deterministic exit (e.g. Ctrl/Cmd+Enter in a cell inserts a paragraph after the
table) that is C; add the row to the verification table. State that leaving a
final table or code block never changes the serialisation of an untouched
value (no stored trailing paragraph, or the trailing paragraph is dropped by
normalisation and BR-RT-6 — choose one).
Status: open
Resolution:

### Minors (round 3)

- **m-6** BR-RT-13 (spec.md:96), doc-002 amendment: doc-002 BR-M3-4 already
  has headings, lists (unbounded nesting), links, code blocks, tables. The real
  widening of the *stored* subset is underline, strikethrough, highlight,
  colour, size (closed class palettes) and the code-block language class.
  Indent/outdent (nesting is already "lists"), emoji (plain text) and auto-link
  (editor behaviour) are not subset changes; listing them blurs the amendment.
  Say also that the subset is shared by all BR-M3-4 fields, so the type-fields
  and comment line inherits the palettes; and drop "(and, when B and C merge,
  images and mentions)" — those amendments are B's and C's own (BR-M3-4
  already allows images as attachments). Name who applies it (a note in
  doc-002's `status.md`, like mod-003's).
- **m-7** BR-RT-4 (spec.md:87) cites "D-8"; this spec has D-1..D-7. Write
  "doc-002 D-8".
- **m-8** New dependencies: an emoji picker/data set and a syntax highlighter
  are not in D-2's accepted set, and ADR-0027 (still `proposed`) does not
  mention them. Owe them to the ADR (or state built in-house). Add: emoji data
  bundled, native glyphs, no request to a CDN; highlighting applied as
  decorations over text nodes, never by inserting a highlighter's HTML string.
- **m-9** Class tokens: say matching is exact and case-sensitive after
  splitting on ASCII whitespace, and the server emits the token from its own
  table (never echoes the input attribute), so `class="rt-color-red&#9;x"` and
  `RT-COLOR-RED` have a defined result.
- **m-10** Size arithmetic re-checked: the legacy figures (700 007, 500 007)
  still hold — the legacy rule emits no `span`/`mark`/class; the panel's
  normalised string ≤ 1 000 000 code points is ≤ 2 000 000 UTF-16 units, so the
  pre-parse cap never trips for it; `char_length` counts code points, matching
  BR-RT-7; a table of one-character cells reaches the ceiling at the visible
  limit (US-1:90 is consistent). Gap: canonical re-nesting amplifies — with link
  outermost, toggling the link per character reopens the whole eight-mark
  stack per character, roughly ×8 — so a 2 000 000-unit hostile input builds
  tens of MB before the ceiling refuses it. Require the reduction to abort once
  output passes 1 000 000, and add a timing case at the pre-parse cap to NFR
  Speed. Also define lone surrogates (counted 1, or refused) so the .NET and JS
  counters agree.
- **m-11** BR-RT-16 Shift+Tab "removes one leading level": define a level
  (one tab; or up to N leading spaces).
- **m-12** BR-RT-15's paste recognition from `font-weight` / `font-style` /
  `text-decoration` has no scenario; add one, including the wrapper
  `<b style="font-weight:normal">` that office web editors put around a whole
  copy (it must not make everything bold).
- **m-13** US-5:42-45: the selection has six marks, the "stored nesting" line
  lists eight; say "in the order link, bold, …" as an order, not a content
  claim.
- **m-14** Route: the amendment roughly doubles the approved scope;
  routing.md:74-75 and :204-207 want a re-confirmation of the route. The
  human's D-5 is the substance; the orchestrator should log an explicit "size
  L re-confirmed for the amended scope" line.

### Size

One `L` with seven stories is within the rules: routing.md caps stories only
for `M` (routing.md:253); spec.md is 186 lines (budget ~250) and the largest
story (US-1) 130 lines (budget ~150). Per-story delivery
(01-business-analysis § Per-story delivery) would technically allow US-5
(palettes, emoji) to ship later, being an additive widening; but D-5 puts all
text formatting in A, and US-5/US-6 are not shippable without US-7. Tables and
code were already in the gate-1-approved subset. **Recommendation: keep one L,
do not split**; the L ceremony (separate gates, security pass, two QA passes)
fits the security surface. If the diff later doubles again, escalation by
volume applies and US-5 is the cut to spin out.

## Round 4 — requirements (delta)

Read: `spec.md` and `stories/US-1..US-7` at 1f2f6d5, against the Round 3
findings only. The last delta read the L cap allows. D-1..D-7 and the no-E2E
instruction are not re-opened.

Verdict: **approve-for-gate-1** — R-16..R-21 and m-6..m-13 are resolved; the
fixes introduced no blocker and no major. Ten minors below, all one-clause
edits or design-stage notes; none needs another review round or a human
answer.

### Round-3 findings

| Finding | Status | Where |
|---|---|---|
| R-16 Tab exceptions | resolved | spec.md:99 (two exceptions, Esc arms exactly the next keystroke, a picker's Esc does not arm), spec.md:100, NFR spec.md:125-127, US-1:89, US-6:21-22, US-6:34-41, US-6:109 |
| R-17 rectangular tables | resolved | spec.md:88 ("rectangular tables without merged cells"), spec.md:100, US-7:45-52, US-7:122-127 (colspan, rowspan, short row, empty row), US-6:43-47 |
| R-18 canonical gaps | resolved | spec.md:88 ("every element and attribute … not a closed list"), spec.md:98, US-7:29-44, US-7:72-75 (inner wins), US-7:92-95 (one `s` "cd"), US-7:97-100 (code vs marks, link kept), US-7:107-110 (pre in li), US-2:23-24 (aligned with US-7) |
| R-19 auto-link reach, undo, prompt | resolved | spec.md:101, US-5:26, US-5:82-121 |
| R-20 TSV paste trigger | resolved | spec.md:100 (defers to US-6), US-6:49-54 (html table, or ≥ 3 lines with equal tab count ≥ 1; Ctrl/Cmd+Shift+V), US-1:67-68 |
| R-21 layout-bound exits, trailing paragraph | resolved | spec.md:99 (ArrowDown at end of text; no stored paragraph), spec.md:100 (Ctrl/Cmd+Enter, gap cursor), US-6:56-65, US-6:79-82, US-6:112-113 |
| m-6 doc-002 amendment | resolved | spec.md:96 |
| m-7 "doc-002 D-8" | resolved | spec.md:87 |
| m-8 new dependencies | resolved | spec.md:99 (decorations), spec.md:102 (bundled data, no CDN), spec.md:179-183 |
| m-9 class token matching | resolved | spec.md:97, US-7:54-56, US-7:67-70 |
| m-10 amplification, lone surrogates | resolved | spec.md:90 (early abort), spec.md:91 (U+FFFD), spec.md:122-123 (timing), US-2:104-107 |
| m-11 Shift+Tab level | resolved | spec.md:99, US-6:76 |
| m-12 pasted styles | resolved | spec.md:98, US-1:70-75 |
| m-13 order, not content | resolved | US-5:46 |

Checked and found consistent: BR-RT-14..20 against US-1, US-2, US-5, US-6,
US-7 (Tab exceptions, list indent by buttons only, heading without size, clear
formatting keeps links, palettes); the early abort against idempotency (the
abort threshold equals the ceiling, a stored value is ≤ 1 000 000 and
re-reduces to itself, so the abort never fires on a canonical value; client
and server refuse the same values); the rectangular rule against the editor
(prosemirror-tables' `fixTables` is a no-op on a server-canonical table, so
byte identity holds; pasted merges must be split on paste, US-6:46); code
excluding every mark but link is a plain `excludes` on a customised code mark
and parses `<strong><code>` and `<code><strong>` to the canonical result in
both directions; nested same-family marks: ProseMirror's self-exclusion keeps
the inner, as US-7:33-35 says; Verification tables of all seven stories use
only U/C/I/M, no E2E.

### Minors (round 4)

- **m-15** spec.md:99, US-6:62-65, :79-82 — holdable, owed to design.md: stock
  `prosemirror-gapcursor` refuses a gap cursor after a node with inline content
  unless the node is `isolating`, TipTap's code-block exits (`exitCode`,
  `exitOnArrowDown`, triple Enter) insert a paragraph, and TipTap v3
  StarterKit's TrailingNode appends one. The code block must be `isolating`
  (or carry a custom selection), the exits custom, TrailingNode off. The table
  is already isolating, so its gap cursor works stock.
- **m-16** spec.md:98, US-7:36-39 — "a link inside code is hoisted outward
  around the code" is undefined for a link covering part of the code; the
  editor keeps the link on its own text (`<code>a</code><a …><code>b</code></a><code>c</code>`
  for `<code>a<a …>b</a>c</code>`). Say so and add that fixture, else the server
  may widen the link and the triple disagrees.
- **m-17** spec.md:101 vs US-1:68, US-6:54 — BR-RT-18 auto-links any paste and
  links a selection a URL is pasted over; US-1 says Ctrl/Cmd+Shift+V pastes "no
  link". Add the plain-paste exception to BR-RT-18.
- **m-18** US-6:50, :53 — a spreadsheet's `text/plain` ends with a line break,
  so its last "line" has 0 tabs and fails "the same number of tabs"; say a
  final empty line is ignored.
- **m-19** spec.md:91 — a lone surrogate is replaced "before counting"; say the
  reduction also replaces it in the stored html (a UTF-8 store cannot hold one).
- **m-20** spec.md:89-90 — BR-RT-6 vs the ceiling: a blank value whose markup
  passes 1 000 000 (≈ 143 000 empty paragraphs) is refused, not stored `""`;
  state that the limits are checked before BR-RT-6 applies (matches the BPMN).
- **m-21** US-6:112 — the manual row "exit by ArrowDown from the last row" has
  no rule or scenario behind it any more (BR-RT-17 has only Ctrl/Cmd+Enter);
  drop it or state the behaviour. US-6:56's title says "from its last row"
  while the scenario works from any cell.
- **m-22** spec.md:100 — "Tab and Shift+Tab move between cells" is stated
  twice in BR-RT-17; keep the second.
- **m-23** US-5:117-121 — the first-undo-removes-only-the-link rule (BR-RT-18)
  also covers a pasted address; the scenario tests typing only. Add the paste
  case.
- **m-24** US-7:107-110 — the title says "a cell or a list item", the fixture
  has only the list item; add the cell. No client scenario covers pasting a
  multi-line `pre` into a cell or list item: the editor needs a paste
  transform there (ProseMirror would otherwise lift the block out of the
  cell); add it to US-6:43-47.
