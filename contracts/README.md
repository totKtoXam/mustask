# contracts/ — the API error text catalog

The single source of MUSTASK's error message texts — in Russian and in Kazakh.
It belongs to neither the backend nor the frontend (ADR-0006): both consume
it, neither keeps its own copy.

## Who reads it and who edits it

- **Backend** (`Mustask.Api`) links this catalog's files as `EmbeddedResource`
  and parses them once at startup — the server returns `errorCode`, raw
  `params` and a Russian `detail` (a fallback and the text for logs),
  without choosing the user's language.
- **Frontend** wires up the same catalog to show text in the user's chosen
  language.
- **A human with no access to either codebase** (an analyst, a support
  agent) does not read these JSON files directly, but the
  `docs/error-codes.md` generated from them — plain markdown.
- **Whoever edits an entry** is whoever introduces the error code: the
  slice's developer — when adding a business code, the backend developer of
  a platform task — when changing `errors/http.json`.

## Format

One file — one module (`errors/<module>.json`); `errors/http.json` — platform
codes assigned by the handler by HTTP status, not thrown by any slice.
`errors/schema.json` — the JSON Schema of an entry: a hint for the editor,
the runtime does not read it.

```json
{
  "module": "tasks",
  "origin": "slice",
  "codes": {
    "tasks.wip_limit_exceeded": {
      "statuses": [409],
      "params": { "count": "number", "limit": "number", "column": "string" },
      "ru": "В колонке «{column}» уже {count} задач при лимите {limit}.",
      "kk": "«{column}» бағанында {limit} шектеуімен {count} тапсырма бар."
    }
  }
}
```

Entry fields:

| Field | Required | Value |
|---|---|---|
| `statuses` | yes (may be an empty `[]` — only for a "last resort" code, not tied to a status) | the list of HTTP statuses the code can occur under |
| `params` | yes (may be `{}`) | parameter name → a type hint from a closed set: `string`, `number`, `currency`, `date`, `datetime` |
| `ru` | yes, non-empty | the Russian text, `{name}` placeholders — exactly the `params` keys |
| `kk` | yes, non-empty, not letter-for-letter equal to `ru` | the Kazakh text, the same set of placeholders |
| `deprecated` | no, defaults to `false` | the code is retired, but the entry stays (history is not reused — ADR-0005) |
| `kkNeedsReview` | no, defaults to `false` | the Kazakh text was written by a non-native speaker and needs proofreading — see "Proofreading Kazakh texts" below |

Escaping a literal curly brace in the text — a doubled brace `{{`/`}}`.

## Checks

Blocking checks live in `tests/Mustask.Contracts.Tests` (no Docker, no HTTP
host): completeness (both languages for every code), no "orphans" (an entry
with no code in the code and no `deprecated`), matching placeholders across
`ru`/`kk`/`params`, `docs/error-codes.md` being up to date. Changing the
catalog file without running `dotnet test tests/Mustask.Contracts.Tests` risks
failing CI.

`ErrorCatalogLoader` (`Mustask.Api`) additionally checks conformance to
`schema.json` itself, in the places where an error would otherwise pass
silently: a duplicate key inside one entry, an unknown field (a typo like
`"depricated"`) — the whitelist mirrors the schema's
`additionalProperties: false`. This fails application startup, not just a
green build with no warning.

The set of "application codes" the catalog is checked against is collected
by reflection: `public const string` in classes marked with `[ErrorCodes]`
(`Mustask.Shared.Errors`). **A code in a class without this attribute is guarded
by no check at all** — a forgotten attribute gives a green build and a
generic 500 at runtime instead of the expected status; a mechanical ban on
this is not in place yet.

## Proofreading Kazakh texts

Entries with `"kkNeedsReview": true` were written by a backend developer,
not a translator: the auto-check guarantees a translation exists and is not
copied from the Russian, but not its quality — proofreading by a native
speaker is a separate step. After proofreading, the field is removed
together with the text fix, in the same commit.
