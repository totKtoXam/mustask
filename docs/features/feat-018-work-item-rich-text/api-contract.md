# API contract — feat-018-work-item-rich-text (Описание as rich text)

Fixed at stage 4a (2026-10-13), before the code. From now on it changes only
with a note in `status.md` and a line in its Log; once the frontend stage has
started, a change is escalated to the orchestrator (stages/04-backend.md, 4a).

This is an **additive amendment** of mod-003's contract
(`docs/features/mod-003-work-item-core/api-contract.md`, referred to below as
"mod-003 §"), not a new surface: no route is added or removed, no field is
renamed, no status code changes. A client that sends no declaration and reads
only `description` sees today's behaviour exactly (BR-RT-10).

> **Changed after QA (2026-10-13, defect loop 1; orchestrator's note owed in
> status.md).** The wall-clock parse budget is gone: refusals by size or
> structure are deterministic (§ 3, "Structural bounds"), still
> `description_markup_too_large`, 400. New stored-value notes (CR to LF, no
> `<`/`>` in a kept href), the JSON lone-surrogate wire behaviour, and the
> safety net answering 500 are in § 3. Shape, fields, routes, codes and
> statuses are unchanged; only the set of inputs refused with an existing
> reason widened. Copy proposal for that reason: § 4 notes.

## 0. Summary

1. `PATCH …/workitems/{identifier}` takes an optional `descriptionFormat`
   (`"plain"` or `"html"`) beside `description`; absent or `null` means
   `plain`, stored exactly as sent, 100 000 UTF-16 code units, as BR-M3-19.
2. Every `WorkItem` (create, read, save) carries `descriptionFormat`
   (`"plain"` or `"html"`, never `null`); a legacy, never-written or cleared
   description is `""` with `"plain"`.
3. An `html` description is reduced by the server to the safe subset and its
   canonical form (US-2, US-7); the response returns the reduced value.
4. Limits for `html`, in Unicode code points: visible text 100 000
   (`description_too_long`), reduced markup 1 000 000
   (`description_markup_too_large`, new), a raw request above 2 000 000
   UTF-16 units refused before parsing with the same markup reason; an unknown
   format is `description_format_unknown` (new). All three are field reasons
   of `errors.description`, status 400.
5. A save changes Описание iff the pair (value, format) differs from the
   stored pair; an unchanged pair changes nothing, `changedAt` included.

## 1. Request: `PATCH …/workitems/{identifier}` (mod-003 § 2.4)

One new optional body field. Everything else in mod-003 § 2.4, feat-001 § 3.3
and feat-022 § 3.2 is unchanged.

```json
{
  "description": "<p>Шаги:</p><ol><li><p>открыть форму</p></li></ol>",
  "descriptionFormat": "html"
}
```

| Field | Meaning |
|---|---|
| `description` | Описание (§ 3). `""` clears it; absent or `null` leaves it unchanged (BR-M3-25). Interpreted by `descriptionFormat`. |
| `descriptionFormat` | optional string, exactly `"plain"` or `"html"` (lower case, no trimming). Absent or `null`: not declared, which means `plain` when a `description` is sent. A valid declaration with no `description` is ignored: nothing changes. Any other string (`"markdown"`, `"HTML"`, `" html"`, `""`) is refused (§ 4), **with or without a `description`**. A value of another JSON type (number, object, array, boolean) fails binding: 400 `http.bad_request`, as for every other field. |

`POST …/workitems` (create) takes neither field: a new item has `description`
`""` and format `plain` (mod-003 BR-M3-20); nothing changes there.

### 1.1 What is stored, per write

| `descriptionFormat` | `description` sent | Stored value | Stored format |
|---|---|---|---|
| absent / `null` / `"plain"` | any string | exactly as sent (line breaks, markup characters, `""` kept); no reduction, nothing interpreted | `plain` |
| `"html"` | any string | the **reduced** html (US-2, US-7), or `""` when its visible text has no non-whitespace character (BR-RT-6) | `html`, or `plain` when the value is `""` |
| any valid / absent | absent / `null` | unchanged | unchanged |

The format of an empty description is always `plain`, so "cleared by an html
client" and "never written" are the same stored pair `("", plain)` and an
html re-send of `""` over it is not a change.

### 1.2 Equality and `changedAt`

A write changes Описание when its **(value, format) pair** differs from the
stored pair, the value being the reduced value for `html` and the sent string
for `plain`. Examples, over a stored `("<p>a</p>", html)`:

| Request | Result |
|---|---|
| `description` `"<p>a</p>"`, `"html"` | no change, `changedAt` does not move |
| `description` `"<b>a</b>"`, `"html"` (reduces to `<p><strong>a</strong></p>`) | a change |
| `description` `"<p>a</p>"`, no declaration | a change: stored as `("<p>a</p>", plain)` |
| `description` `""`, no declaration | a change: cleared, `("", plain)` |

`changedAt` moves only when at least one value changes (mod-003 § 2.4 is
read with "value" meaning the pair).

## 2. Response: `WorkItem` (mod-003 § 1.1)

One new field in the shape returned by create (201 and the 200 replay), read
(`GET …/workitems/{identifier}`) and save:

```json
{
  "description": "<p>a</p>",
  "descriptionFormat": "html"
}
```

| Field | Meaning |
|---|---|
| `descriptionFormat` | `"plain"` or `"html"`, always present, never `null`. Every row stored before this task is `"plain"`; a new item is `"plain"`; an emptied description is `"plain"`. |

`description` is still `""`, never `null`, when empty. `WorkItemListItem`
(§ 1.2) does not change: the list, board and backlog never return the
description or its format.

The server never rewrites a stored value except on a write: a read of a
`plain` description returns the very string that was stored, and converting
legacy text to html is the displaying client's job (BR-RT-9, D-3).

## 3. Values and limits (replaces the Описание bullet of mod-003 § 0.6)

- **`plain`** (declared or not): stored exactly as sent; more than **100 000
  UTF-16 code units** → `workitems.workitem.description_too_long`
  (BR-M3-19, unchanged). U+0000 cannot be stored (unchanged).
- **`html`**: the value is reduced first; then, in this order and with one
  reason per field (the first that applies):

  | # | Check | Over the bound | Reason |
  |---|---|---|---|
  | 1 | the raw request value, UTF-16 code units, **before any parsing** | more than 2 000 000 | `description_markup_too_large` |
  | 2 | the reduced markup, Unicode code points; the reduction stops as soon as its output passes the bound; **and the deterministic structural bounds below** | more than 1 000 000, or a structural bound passed | `description_markup_too_large` |
  | 3 | the visible text of the reduced html (BR-RT-8), Unicode code points | more than 100 000 | `description_too_long` |
  | 4 | no non-whitespace character in the visible text | — | stored as `""`, format `plain` (BR-RT-6) |

  **Structural bounds of check 2** (QA loop 1; plan-backend § 6.3). Same
  reason, same 400, decided from the request value alone (no clock, no host):
  nesting deeper than 160 levels (html and body counted); more than 300 000
  elements; more than 350 000 start and end tags; more than 64 attributes in one
  tag (or squares of per-tag attribute counts over 8 000 000); a sum over
  elements of their nesting over 6 000 000; foster-parenting work over
  16 000 000 (text or inline content between table tags, charged by the
  parent's size; canonical output has none); and a value longer than 8 000 units holding any comment,
  declaration, processing instruction, or script, style, title, textarea, xmp,
  iframe, noembed, noframes, noscript, plaintext, svg, math, select, option,
  optgroup, frameset, frame or template markup. The panel's canonical output
  is inside all of them; a client sending large non-canonical markup is not.
  The same input always gets the same answer.
- **Stored-value notes.** A character reference to CR (`&#13;`) is stored as
  LF (what a re-parse of the stored value gives). An address containing `<` or
  `>` is not kept as a link; its text stays (US-2's address rule, extended).
- **Wire.** A lone surrogate in a JSON string escape is refused by the
  platform's JSON binder before the handler (400 `http.bad_request`, nothing
  stored); the U+FFFD replacement of BR-RT-8 applies to what reaches the
  reducer. The panel replaces lone surrogates itself.
- **Safety net.** A reduction that is abandoned by a wall-clock bound
  (`RichText:SafetyNetMilliseconds`, default 20 000) or runs the parser out of
  stack is a server defect and answers the platform's **500**, never a
  refusal reason; what is accepted or refused never depends on it.
- A value over both limits gets `description_markup_too_large` only. Checks
  1–3 come before 4: a blank value over the ceiling is refused, not stored as
  `""`. "Visible text" and "code points" are defined by BR-RT-8 (a lone
  surrogate is replaced by U+FFFD, in the stored html as well and counted 1;
  U+0000 in html input is removed).
- **Stored link address.** The `href` the server stores is the address as the
  server read it (leading and trailing C0 controls and spaces trimmed, ASCII
  tab, LF and CR removed), never a re-encoded or resolved form; this is the
  string the editor's normaliser must produce too (US-2 § Link address).
- **Serialisation.** The stored html is written in the WHATWG fragment
  serialisation form: attributes `href` then `rel` on `a`, `class` alone
  elsewhere, double quotes, `&`, `<`, `>` escaped in text and `&`, `"` in
  attributes, U+00A0 written `&nbsp;`, void `br` as `<br>`. This is the form
  the browser's `innerHTML` produces, which is what keeps the editor's
  normalised output equal to the server's byte for byte (BR-RT-5).
- The closed class vocabularies are `contracts/rich-text/vocabularies.json`
  (ADR-0027 decision 6); the server writes a token from its own table, never
  from the input.

## 4. Error codes

Module `workitems`, `contracts/errors/workitems.json`, all **400, field
reasons of `workitems.validation_failed`** under `errors.description`, one
reason per field (mod-003 § 0.5 is unchanged: the fields of an error are
`page`, `requestId`, `title`, `description`, and feat-022's):

| Code | Status | Params | ru | kk |
|---|---|---|---|---|
| `workitems.workitem.description_too_long` (existing) | 400 (field reason) | — | «Не более 100 000 символов.» (text unchanged) | unchanged |
| `workitems.workitem.description_markup_too_large` (new) | 400 (field reason) | — | «Слишком много разметки. Упростите форматирование или сократите текст.» | «Белгілеу тым көп. Пішімдеуді жеңілдетіңіз немесе мәтінді қысқартыңыз.» (`kkNeedsReview`) |
| `workitems.workitem.description_format_unknown` (new) | 400 (field reason) | — | «Неизвестный формат описания. Обновите страницу и попробуйте ещё раз.» | «Сипаттама пішімі белгісіз. Бетті жаңартып, қайталап көріңіз.» (`kkNeedsReview`) |

Notes:

- `description_markup_too_large` also answers the structural bounds of § 3
  check 2. **Text honesty:** «Слишком много разметки. Упростите форматирование
  или сократите текст.» fits nesting, element, tag and attribute bounds (too
  much markup), but "сократите текст" misleads for a short value rejected for
  holding a comment or a script (8 000-unit rule). Proposal, not applied
  (`contracts/errors` is unchanged): «Слишком сложная разметка. Упростите
  форматирование или уберите вставленное содержимое.» The panel never produces
  these cases (its output is canonical), so the copy is seen only by a foreign
  client or a defect; the human decides at gate-3 with the catalog diff.
- The meaning of `description_too_long` widens, its text does not: for
  `plain` it is 100 000 UTF-16 units, for `html` 100 000 code points of
  visible text.
- `description_format_unknown` answers a panel defect or a foreign client,
  never a user's input (as `request_id_invalid`); the panel shows it in the
  form's banner, not under the editor. It is reported under
  `errors.description` (the story: "a field reason on the description"). When
  it applies, the description is not interpreted, so no other reason of
  `errors.description` is reported with it.
- The texts of the two new codes are proposals for the human's confirmation
  at gate-3 together with the catalog diff; the markup text is spec-quoted
  (US-4). Both Kazakh texts are `kkNeedsReview`.
- Order of answers (mod-003 § 0.4) is unchanged: the path's 404 first, then
  the 400 with every field reason at once, then the 404 of a body reference,
  then the 409s. The reduction itself runs before the item is locked but its
  refusal is raised at the 400 step, so a request with a bad path and an
  oversized description still answers 404.
- No new 404, 409 or 5xx. A reduction failure other than a limit is a defect
  and answers the platform's 500, never a partially reduced value.

## 5. Amendments owed (BR-RT-13), applied by the orchestrator

All additive; each with a note in the owner's `status.md`. Nothing below is
edited by this stage.

| Document | Amendment |
|---|---|
| `docs/features/mod-003-work-item-core/spec.md` BR-M3-19 | "plain text, stored as sent" holds for a description in `plain` format; an `html` description is governed by feat-018 BR-RT-1..BR-RT-10. |
| mod-003 `api-contract.md` § 0.5 | the fields list unchanged; add that `errors.description` also carries `description_markup_too_large` and `description_format_unknown`. |
| mod-003 § 0.6, Описание bullet | replaced by this document's § 3 (both formats, three limits, code points for html). |
| mod-003 § 1.1 | add `descriptionFormat` to the example and the field table (§ 2 here). |
| mod-003 § 2.4 | add `descriptionFormat` to the body table; the 400 line lists the two new reasons under `errors.description`; "a value equal to the stored one changes nothing" is read as equality of (value, format) (§ 1.2 here). |
| mod-003 § 4 | the two new codes (table above). |
| `docs/features/feat-022-work-item-form-fields/spec.md` BR-M3-36 | reworded "as BR-M3-19 has it for a plain-format description"; its three texts stay plain; its decomposition note extending feat-018 to those fields is superseded (spec.md of feat-018, § Decomposition). |
| feat-022 `api-contract.md` | no change to its shape: its texts stay plain strings with no format marker. A note that the marker is per field (ADR-0027, "What later features inherit") and not added here. |
| `docs/features/doc-002-*` BR-M3-4 | the stored subset is widened by underline, strikethrough, highlight, text colour and font size (closed class palettes) and the code-block language class; shared by every BR-M3-4 field. |
| feat-002 | a note that comment rich text is owned by the type-fields line. |

Registry entries to add **in this task's implementation** (not owed to anyone
else): `contracts/errors/workitems.json` gets the two entries above (ru, kk,
`statuses: [400]`, `params: {}`), `WorkItemErrorCodes` the two constants
(`[ErrorCodes]` is already on the class), and `docs/error-codes.md` is
regenerated with `MUSTASK_UPDATE_ERROR_CODES=1 dotnet test
src/backend/tests/Mustask.Contracts.Tests` (never edited by hand).

## 6. Compatibility

- **Old client, no declaration:** identical to today in every respect, with
  one new property `descriptionFormat` in the responses it ignores.
- **Old client reading an html description** sees the markup in a plain
  field (spec § Out of scope): the panel is the only client.
- **New client, old server:** not a supported pair (one deployment); a server
  without this release would ignore `descriptionFormat` and store the html as
  plain, which is the rollback story of ADR-0027 and the plan's § Rollback.
