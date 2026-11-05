# QA — feat-018-work-item-rich-text

## Backend

Session: separate, clean context, 2026-10-13, size L deep pass. The plan was
written from `spec.md`, US-2, US-3 (server half), US-4, US-7, `api-contract.md`
and ADR-0037 before any implementation file was opened (stage 04-backend § 5);
coverage was then checked against commits `05a68cd..4569225` and plan-backend
§ 6.2. E2E: not run, not asked: the human's standing answer is NO
(paused 2026-10-12), and this is not the last QA stage before review (frontend
QA follows). `scripts/verify.sh` and every frontend suite were not run
(machine saturated, another session asked for no full runs): targeted
`dotnet test` only.

### Coverage matrix

Dev = a test the developer wrote (file under `src/backend/tests`), QA = added
in this pass (`ReducerQaProbeTests`, `SaveWorkItemDescriptionQaTests`).

| Criterion | Check | Type | Status |
|-----------|-------|------|--------|
| US-2 kept subset, stripped, unwrapped, attributes, headings, links (12 addresses), images | `canonical.json` ids `subset-kept` .. `images-dropped`, `LinkAddress_*`; PATCH then GET `Save_FixtureCanonicalCases_RoundTripThroughPatchGet` | U+I (Dev) | ✔ |
| US-2 hostile corpus, re-parse stability (scripting on and off) | `Reduce_HostileCorpus_*`, `Save_HostileCorpus_StoredValueClean` (Dev); 40 more payloads, mXSS (noscript/title, math/mglyph/style, CDATA in svg, nested forms, foreign content) `QA_Reduce_HostilePayload_*` | U+I | ✔ |
| US-2 early abort, amplification | `Reduce_Amplifying2000000Units_*`, `Save_Html_Amplifying_RefusedWithin2s` (Dev) | U+I timed | ✔ for that shape; see D-1, D-2 for shapes that bypass it |
| US-2 idempotent | `Reduce_RandomMarkup_*` (Dev, 1 000 inputs) + `QA_Reduce_RandomStructures_*` (5 seeds x 600, wider grammar) | U | ✔ except D-3 |
| US-4 no declaration, html reported, empty plain, unknown format with and without description, format only, equality, plain over html | `SaveWorkItemDescriptionEndpointTests` (Dev, whole file, 36 tests green) + `Save_FormatNullOrAbsent_*`, `Save_FormatOfAnotherJsonType_*` (QA, 8 raw-JSON cases) | I | ✔ |
| US-4 counts, limits at both edges, markup reason order, request cap, blank over ceiling, legacy worst cases | `VisibleCountTests`, `ValidateReduced_*`, `Save_Html_*` (Dev); `QA_Reduce_AstralAtTheRawCap_*`, `QA_Reduce_EscapedCharacters_*`, `QA_Reduce_SurrogatesSplitByATag_*` (QA) | U+I | ✔ |
| US-4 lone surrogate "counts 3, stored U+FFFD" | reducer yes; over HTTP the binder answers 400 (D-4) | U+I | ✔ unit, ✖ wire (minor) |
| US-4 migration (rows unchanged, 100 000 whole, checks, down guard, up again) | `AddWorkItemDescriptionFormatMigrationTests` x3 (Dev), run here on a clean container | M | ✔ |
| US-4 response time | `Save_And_Get_100000VisibleCharacters_Within1s` (Dev); ceiling-sized document end to end (QA) | I timed | ✔ 100 000 visible; ✖ at the ceiling on a loaded host (R-1) |
| US-7 palettes, exact match, same family, two tokens, language list, mark order, legacy tags, code/link shapes, code block, headings, tables (rectangular), empty-looking | canonical.json (62 cases) via `FixtureParityTests` + 14 class-token and 16 link-address inputs of QA | U | ✔ |
| US-7 editor output equals canonical | `editorOutput` of `editor-*-chrome` cases | U | ✔ |
| US-3 server half: other field leaves description untouched, list never carries it | `Save_OtherFieldOnly_*`, `List_NeverCarries*` (Dev) | I | ✔ |
| NFR safety: vocabulary file equals server table, no token literal | `Vocabulary_Embedded_EqualsTheFileOnDisk`, `ServerCode_HoldsNoTokenLiteral` (Dev) | U | ✔ |
| § 0.4 order: 404 before 400 | `Save_BadPathOversizedDescription_404` (Dev); unknown format on a missing item, on another organization's item (QA) | I | ✔ |

### Additional scenarios (adversarial, QA)

| # | Scenario | Result |
|---|----------|--------|
| A-1 | 40 hostile payloads: noscript/title/textarea/iframe raw text, `math mtext table mglyph style`, `svg desc CDATA`, `form math mtext`, `a href=x table a`, listing/noembed/noframes, body/html/frameset handlers, scripting on and off | no element outside the allow-list, stable, idempotent |
| A-2 | class tokens: NBSP and VT are not separators, FF is, entity-encoded name, duplicate `class` attribute, tokens on `p h3 li a pre code mark`, `LANGUAGE-PYTHON`, two languages | as BR-RT-14 |
| A-3 | links: `&Tab;java&NewLine;script&colon;`, hex entities without semicolons, NBSP and U+2028 before the scheme, `https://@`, `https://?q`, `http:\\evil`, `https://evil\@good`, `file:`, `blob:`, empty text | removed or kept as read, stable |
| A-4 | 3 000 deep `div` on a 256 KB stack (Dev), 2 000 000 units of every flat tag among 86 (AngleSharp harness, 1 process each) | only `<pre>` and `<listing>` kill the process (D-1) |
| A-5 | 24 hostile shapes at 2 000 000 units, time to answer | all refused or accepted in 0.1 to 1.9 s, except distinct attributes (D-2) |
| A-6 | arithmetic: 999 993 astral at the raw cap = 1 000 000 markup, +1 refused; `&amp;` counted as written; pair split by a tag = two U+FFFD | exact |
| A-7 | 6 concurrent PATCH, html and title mixed; 4 identical html in parallel | all 200, both fields land, one `changedAt` |
| A-8 | alternating plain / html / plain of the same text | each a change, `changedAt` moves |
| A-9 | raw JSON: `descriptionFormat` null, absent, boolean, array, object, 1.5; `\u0000` in html | as the contract; NUL removed |
| A-10 | indented / pretty-printed html at 700 000 reduced | accepted; at 840 000 reduced plus 350 000 indent refused (D-5) |

### Runs

All `dotnet test`, one project at a time, Debug, from the worktree root. No
process of mine is left running; Testcontainers' containers are reaped by Ryuk.

| What | Result |
|------|--------|
| `src/backend/tests/Mustask.Domain.Tests --filter FullyQualifiedName~WorkItem` | 178 passed |
| `src/backend/tests/Mustask.Application.Tests --filter FullyQualifiedName~SaveWorkItem` | 19 passed |
| `src/backend/tests/Mustask.Contracts.Tests` (all) | 191 passed |
| `src/backend/tests/Mustask.Infrastructure.Tests --filter "FullyQualifiedName~RichText\|FullyQualifiedName~AddWorkItemDescriptionFormat"` | 454: 446 passed, 6 failed, 2 skipped. Failed: 4 x D-3 (QA), 1 x D-5 (QA, since skipped), 1 x D-7 (Dev test `Reduce_HostileDeepNesting_IsBoundedInTime(table)`) |
| same project, `FullyQualifiedName~ReducerQaProbeTests` after skips | 126: 119 passed, 4 failed (D-3), 3 skipped (D-1, D-2, D-5) |
| `src/backend/tests/Mustask.Api.IntegrationTests --filter FullyQualifiedName~SaveWorkItemDescription` | 50 tests, 48 green, 2 were QA tests with my own errors (visible limit of my table, surrogate expectation); fixed, then `SaveWorkItemDescriptionQaTests` 14/14 |

### Defects

#### D-1: consecutive `<pre>` or `<listing>` start tags crash the API process

- Severity: **blocker**
- Steps: `PATCH …/workitems/{id}` with `{"descriptionFormat":"html","description":"<pre><pre>…"}`, 400 repetitions
  (2 000 units; 200 repetitions pass). Same with `<listing>`. Reproduced
  through the real reducer (test host died with `Stack overflow` in
  `HtmlDomBuilder.InBodyStartTag → InBody → Home → PreventNewLine`, repeated)
  and in a standalone AngleSharp 1.7.2 harness with the reducer's exact parse
  call: exit code `0xC00000FD` at 400, 2 000, 20 000 and 2 000 000 units.
  `<pre><code>`, `<p><pre>`, `<li><pre>`, `<textarea>` and 84 other tags
  flooded flat do not crash. Test (skipped):
  `QA_Reduce_ConsecutivePreStartTags_AreRefusedNotACrash`.
- Expected / Actual: refusal with `description_markup_too_large` (or a 400)
  in under 2 s / process terminated; a stack overflow cannot be caught, and
  the parse budget does not help, it comes in milliseconds. Any signed-in
  member can take the API down with a 2 KB body. The developer's depth test
  (`Reduce_HostileDeepNesting_IsBoundedInTime`) covers `b`, `div`, `ul`,
  `table` but not `pre`/`listing`.
- Status: fixed in de5e52f, verified in re-check 1

#### D-2: the parse budget does not fire inside one tag; many distinct attributes take minutes

- Severity: **major** (NFR Speed: "a hostile 2 000 000-code-unit input ... refused within 2 s")
- Steps: `<p a0=1 a1=1 … a190000=1>x` (1.79 M units). Elapsed 441 s on the QA
  machine, answer `Exceeded` only afterwards. Scaling: 5 000 attributes
  (39 K units) 0.9 s, 10 000 (79 K) 2.6 s, 20 000 (169 K) 7.7 s, 40 000
  (349 K) 30 s: quadratic in the tag. Every other single-token giant
  (2 M-unit comment, tag name, attribute value, entity, doctype, CDATA)
  parses in 0.2 to 0.6 s. Test (skipped): `QA_Reduce_ManyDistinctAttributesInOneTag_*`.
- Expected / Actual: refused within about 2 s / one request pins a core and a
  gate slot (`MaxConcurrentReductions` = cores) for minutes; a handful of
  requests starve the API. Cancellation is checked between tokens only.
- Status: fixed in de5e52f, verified in re-check 1 (its sibling D-8 was missed by the new bounds)

#### D-3: a character reference to CR is stored raw and is not stable under re-parse

- Severity: **major** (NFR Safety: "every stored value re-parses and re-reduces to itself"; BR-RT-5 idempotency)
- Steps: save `<p>a&#13;b</p>` as html (also `&#x0D;`, in `pre`, in `li`).
  Stored `<p>a\rb</p>`; a second parse turns CR into LF, so
  `reduce(stored) != stored` and the editor's re-serialisation of the stored
  value differs from it (dirty on open, BR-RT-11). Tests (red):
  `QA_Reduce_NumericReferenceToCarriageReturn_*` (4 cases); the same defect
  found by the random generator before `&#13;` was removed from it.
- Expected / Actual: text with CR is written as the parser's preprocessing
  would see it (LF), a fixed point / raw CR stored.
- Status: fixed in de5e52f, verified in re-check 1

#### D-4: a lone surrogate on the wire is a 400 `http.bad_request`, not U+FFFD

- Severity: minor
- Steps: raw body `{"description":"<p>a\ud83db</p>","descriptionFormat":"html"}`
  (what `JSON.stringify` writes for a lone surrogate): the platform binder
  refuses the request, 400, nothing stored. The developer's
  `Save_Html_LoneSurrogate_StoredAsReplacementCharacter` is vacuous: the test
  client's serializer had already replaced the surrogate. Characterised by
  `Save_Html_LoneSurrogateEscapeOnTheWire_IsRefusedByTheBinder_NothingStored`.
- Expected / Actual: contract § 3 and US-4 say replaced and stored / 400.
  Needs a contract note or a deliberate binder setting, and the developer's
  test fixed.
- Status: documented in plan § 6.3, characterised by a test; minor, carried to gate-3

#### D-5: whitespace between blocks counts against the 1 000 000 ceiling

- Severity: minor
- Steps: 70 000 `<p>hello</p>` joined by `"\n    "` (840 000 reduced, 350 000
  of indent): refused. Skipped test `QA_Reduce_IndentedHtml_*`.
- Expected / Actual: BR-RT-7 (2) is "the reduced html": accepted / refused.
  Plan § 5.1 chose this to satisfy "blank over the ceiling is refused"; counting
  the dropped whitespace only when the result is blank satisfies both. The
  panel never sends indented html.
- Status: fixed in de5e52f (minor, verified)

#### D-6: hostile text survives inside a stored `href` value

- Severity: minor
- Steps: `<a href="mailto:a@example.org?body=<script>alert(1)</script>">` and
  `<a href='https://e.org/"><img src=x onerror=alert(1)>'>` store the literal
  `<script>` / `<img … onerror=…>` inside the quoted attribute (`<` and `>`
  are not escaped there, api-contract § 3). Inert for an HTML parser and the
  tree is clean (`QA_Reduce_HostilePayload_*` passes), but a lexical check of
  the stored string (the corpus's own `mustNotContain` is lexical) fails, and
  the HTML Standard's current serialisation escapes `<` and `>` in attribute
  values, so the editor's `innerHTML` may differ from the server's byte for
  byte on such a link.
- Expected / Actual: refuse such an address or escape them / stored raw.
- Status: fixed in de5e52f (minor, verified: an address with < or > is not a link)

#### D-7: a developer test depends on host speed

- Severity: minor
- Steps: `Reduce_HostileDeepNesting_IsBoundedInTime(tag: "table", refused: False)`
  expects `Exceeded == false` for 222 000 `<table>` tags; under load the parse
  budget fired and it went red in the one full run of the project on the QA
  machine (its parse sits near the budget there). Same root as R-1.
- Status: fixed in de5e52f (minor, verified: the test is green, its outcome no longer depends on time)

### Risk R-1 (reported with evidence, not a verdict): the parse time budget

`RichText:ParseBudgetMilliseconds` = 1 500 is a wall-clock cancellation on the
parse. The same input is accepted or refused depending on the host. Evidence,
all on the 22-logical-core QA machine shared with other sessions (so "idle" is
not idle); smallest budget of a ladder that still accepted the document, after
one warm-up; "loaded" = one busy thread per core:

| Document (canonical, under both limits) | idle | loaded |
|---|---|---|
| 10-column table, 999 886 units | 300 ms (one run 800 ms) | 800 ms; in another run 1 500 ms |
| 1-column table, 999 979 units | n/m | refused at 1 500 ms, ok at 800 ms (two runs) |
| nested lists, 999 960 units | 400 ms (one run 1 500 ms) | 600 ms; refused at 1 500 ms in another run |
| spec's legacy worst case, 100 001 `<p></p>` (700 007) | 300 ms | 800 ms |
| 10-column table, 500 000 / 250 000 units | 200 ms | 300 / 400 ms |

- End to end: a canonical table document of 999 834 units (visible 86 000,
  under both limits) was refused with `description_markup_too_large` after
  1.9 to 2.0 s in each of the three runs of `Save_TableDocumentAtTheCeiling_*`
  made with that document (after a warm-up save); the first full `Reduce` of a
  process was 2 017 ms (cold JIT, 10-column table, accepted).
- Reduce total is 1.1 to 2.0 s for 1 M units here, so the NFR "PATCH at the
  ceiling within 2 s" is also not met on this host; the budget covers only the
  parse, the walk and the write are on top.
- The documents at risk are the ones the spec insists are storable
  (US-4 "Legacy worst cases fit the ceiling" is 700 007 units) and any
  table-heavy one above about 500 000 units. The refusal text tells the user to
  simplify formatting, which does not apply. The panel's re-save of a stored
  document after one character edit re-parses it whole, so a document that was
  once accepted can be refused later on a busier host.
- Margin on a 3x slower or contended host: 1M-unit documents sit between 0.2 and
  1.0 of the budget idle here; below 500 000 units the margin is 4x or more.
  Nothing in the budget scales with the input's size.
- The same mechanism is why D-1 and D-2 matter: the budget is the only bound
  and it is neither a stack bound nor a within-token bound.

### Risk R-2 (observation)

The reducer is synchronous on the request thread: `SemaphoreSlim.Wait` and
`ParseDocumentAsync(...).GetAwaiter().GetResult()`. Under a burst of large
bodies the pool threads are blocked, not awaited; this was reasoned, not
measured.

### Tests added

`src/backend/tests/Mustask.Infrastructure.Tests/RichText/ReducerQaProbeTests.cs`
(126 cases, 3 skipped for D-1, D-2, D-5; 4 red for D-3) and
`src/backend/tests/Mustask.Api.IntegrationTests/Features/WorkItems/SaveWorkItemDescriptionQaTests.cs`
(14, green; one of them records R-1 and asserts only "200 and round trip, or
the markup reason").

### Verdict

First pass: blockers: 1 (D-1) · majors: 2 (D-2, D-3) · minors: 4 (D-4..D-7) ·
risk: R-1 · result: fail. Superseded by re-check 1 below.

### Re-check 1 (commit de5e52f; scope: the fixes and what they could break)

Method: the new reducer driven in a **fresh process per case** (a throwaway
console, reflection on `HtmlRichTextReducer`, scratchpad `flood2/`), once as
shipped ("guarded") and once with the guard's tag, attribute and `pre` bounds
switched off so that only the tree watcher and the 32 MB parser stack stand
("watcheronly"); plus the in-repo suites.

| Item | Result |
|------|--------|
| D-1 `pre`/`listing`/`template`, 2 M units | guarded: refused in 22 to 25 ms; watcheronly: refused in 0.25 to 0.33 s by the depth bound. Every element name (172 specs: about 140 flat names, obsolete and foreign included, plus pairs and nests) at 2 M units: guarded, 172 of 172 exit 0 answered, none died; watcheronly 162 answered, 10 hit the 20 s safety net as a *server error* (raw-text names and `<a><table>`), none died |
| D-2 distinct attributes | 65 refused at once, 64 accepted, 190 000 refused in under 1 s (guard); every single-token giant still 0.2 to 0.6 s |
| D-3 CR | `&#13;` and `&#x0D;` written as LF; stable under re-parse; 5 seeds x 500 random inputs with CR references, tables, links, code, comments: idempotent, in allow-list, stable |
| R-1 legitimate documents vs bounds | 9 documents built to sit near each bound, 0.4 to 1 M units, with a busy thread per core: all accepted, same answer twice (142 000 empty paragraphs 994 000; 99 990 `<br>` in one paragraph; 13 000 x three-level empty list items 975 000; all eight marks per run 995 000; 50 000-cell row; 17 000 small tables; 50 000 alternating marks; 12 000 links; mixed headings/lists/tables/code). Cold fresh-process answers for 1 M units of tables, lists, marks and plain text equal the in-process ones. The ceiling table that was refused end to end in pass 1 (999 834 units) is stored, 200 in 1.8 s |
| R-2 | the gate awaits and the parse runs on its own thread: 24 writes of 500 000 units at once, reads of another item answered in 60 to 209 ms meanwhile, all writes 200 |
| Safety net | forced to 1 ms through configuration: 500, nothing stored; never a 400 |
| Fixture files, whole | `canonical.json` 63 cases (input, editorOutput where present, canonical, fixed point, stable under re-parse, in allow-list), `visible-count.json` 14, `legacy.json` 7, `hostile-corpus.json` 36: 185 of 185 pass, no failing case, so no client-side expectation to diagnose either |
| Un-skipped tests are real | read the diff: attribute theory asserts `Exceeded` and under 1 s (190 000), pre theory asserts the refusal and the 64 / 100 / 150 accepted edge, indented-html test asserts acceptance at 840 000; none was weakened |

New defects found by the re-check:

#### D-8: foster-parented inline content between tables is quadratic and no bound counts it

- Severity: **major** (NFR Speed; same class as D-2, a request pins a core)
- Steps: `<table>x` repeated, or `<table><b>`, `<table><a>`, `<b><table>`,
  `<p><table>`: 100 000 units 0.9 s, 300 000 units 6.3 s (`<table><b>` 3.0 s),
  500 000 units of `<a><table>` 5.5 s, 1 000 000 units run to the 20 s safety
  net and answer 500. Tags (250 000) and elements (125 000) are inside their
  bounds, depth is 3. Canonical many-table documents are fine (17 000 small
  tables 0.8 to 3 s), so a count of `<table>` start tags (or of foster-parent
  events) would separate them. Test (red): `QA_Reduce_FosterParentedInline*`.
- Expected / Actual: answered in about 2 s whatever the answer / seconds to
  tens of seconds per request; the gate is the number of cores.
- Status: fixed in c07118b, verified in re-check 2 for every shape named here and 60 variants; its sibling D-10 remains

#### D-9: 1 001 sequential code blocks are refused by the `pre` start-tag bound

- Severity: minor
- Steps: `<pre><code>x</code></pre>` x 1 001 (25 KB): refused in 9 ms with
  "too much markup". 1 000 pass. The recursion the bound guards against needs
  unclosed nesting, which the depth bound already caps at 160.
- Status: fixed in c07118b (10 000 sequential code blocks accepted; verified)

### Verdict (re-check 1)

blockers: 0 · majors: 1 open (D-8) · minors: D-4, D-9 carried · risk R-1
closed, R-2 closed · result: **fail**, one major. Return D-8 to the backend
developer; re-check 2 covers it and what a table-start-tag bound could break
(canonical many-table documents, the 17 000-table case). Tests of this pass:
`ReducerQaProbeTests` 5 added (3 red for D-8, 1 skipped for D-9); integration
`SaveWorkItemDescriptionQaTests` 16, all green. Commands: `dotnet test
src/backend/tests/Mustask.Infrastructure.Tests --filter FullyQualifiedName~RichText`
(683: 679 passed, 3 red D-8, 1 skipped), `...Mustask.Api.IntegrationTests
--filter FullyQualifiedName~SaveWorkItemDescription` (51 passed),
Application `~SaveWorkItem` 19, Domain `~WorkItem` 178, Architecture 83, all green.

### Re-check 2 (commits c07118b, ea044d7; scope: D-8, D-9, what the bound could break; the last re-check)

| Item | Result |
|------|--------|
| D-8 and D-9 tests real | read the diff: my three red D-8 tests and the D-9 test are unchanged apart from the skip removed; the developer's added tests assert both the 2.5 s time and `Exceeded` determinism at 2 000 000 units |
| Foster shapes, fresh process, 2 M units, guarded | 75 specs (my D-8 shapes; foster through nested tables, tables in cells, `caption`, `colgroup`, `col`, svg/math/template inside tables, many tables with one child, alternating table/text, `form`, `input`, `pre`, lists, formatting elements): 73 answered under 2.1 s, none died; **2 reached the 20 s safety net (500): `<table>x</table>y` and `<tbody>x`**, see D-10 |
| Legitimate shapes | developer's: 17 000 and 17 800 small tables, 8 000 tables between paragraphs (also pretty-printed), 10 000 sequential code blocks. Mine: 280 cells of 40 paragraphs, 9 000 table/paragraph alternations, 4 000 table/list/code triples, 5 000 tables with marks and links, a 25 000 `th` row over a 25 000 `td` row, and the 9 near-bound documents of re-check 1 under a busy thread per core: all accepted, output equals input, 0.1 to 1.3 s. No canonical shape found that the bound refuses |
| Nesting, fresh process | `<pre>` x 150 accepted, x 160 and above refused; same for `listing`; `template` x 300 accepted, 340 refused; pre inside table inside list item x 18..22, pre in table in table, svg/foreignObject and math/mglyph with pre, `b`/`a`/`div` wrapped pre and `template`: every case answered with exit 0 on the 32 MB stack, none died |
| Safety net | forced to 1 ms: 500, nothing stored (API test green) |
| Fixture files, whole | `canonical.json` 66 cases (two checks each), `visible-count.json` 17, `legacy.json` 7, `hostile-corpus.json` 36: 192 of 192 tests pass |
| Other suites | `Mustask.Infrastructure.Tests ~RichText` 712 passed before my additions; `Mustask.Api.IntegrationTests ~SaveWorkItemDescription` 51 passed |

#### D-10: text split by tokens the tree builder ignores is quadratic and no bound counts it

- Severity: **major** (NFR Speed; the same class as D-2 and D-8: one request pins a core, the gate is the number of cores)
- Steps: `<td>x`, `<tr>x`, `<tbody>x`, `<th>x`, `<col>x`, `<html>x`, `<body>x`, an unmatched end tag
  followed by `x`, or `<table>x</table>y`, repeated. `<td>x`: 150 000 units
  0.5 s, 300 000 1.0 s, 600 000 4.5 to 5.4 s, 1 000 000 13.2 s, 2 000 000 the
  20 s safety net and a 500. `<table>x</table>y` 600 000 units 6.0 s. The
  parser appends each piece to one text node (visible text only 120 000
  characters); tags (240 000) are inside their bound, no element is created,
  and the table work bound does not see it because no table is open.
- Expected / Actual: a refusal or an answer in about 2 s / seconds to tens of
  seconds. A deterministic bound on the number of text pieces appended to one
  node (or on text tokens between ignored tags) would separate it from
  canonical output, which has no ignored tags.
- Test (red): `QA_Reduce_TextSplitByIgnoredTokens_IsAnsweredWithinTheNfr` (4 cases).
- Status: open. No re-check is left in this loop (two done): the orchestrator
  decides between a third fix round and carrying it to gate-3 as a known DoS.

### Final backend verdict

blockers: 0 · majors: 1 open (D-10) · minors: D-4 (documented, carried) ·
R-1, R-2 closed · D-1..D-3 and D-5..D-9 closed ·
result: **fail** (not "backend QA pass"). D-10 is the one remaining way for a
signed-in member to hold a core for seconds, up to 20 s and then a 500 at the
request cap. Commands: `dotnet test src/backend/tests/Mustask.Infrastructure.Tests --filter "FullyQualifiedName~RichText"`
(4 red: D-10), `... --filter FullyQualifiedName~ReducerFixtureTests` (192 passed),
`dotnet test src/backend/tests/Mustask.Api.IntegrationTests --filter FullyQualifiedName~SaveWorkItemDescription` (51 passed);
fresh-process harness `flood2.dll guarded|watcheronly <units> <spec>|@<file>`.
