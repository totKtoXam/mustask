# API contract — feat-078-description-images (images in Описание)

Fixed at stage 4a (2026-11-17), before the code. From now on it changes only
with a note in `status.md` and a line in its Log; once the frontend stage has
started, a change is escalated to the orchestrator (stages/04-backend.md, 4a).

Three new routes and an **additive amendment** of the description write
(mod-003 § 2.4 as amended by feat-018's contract): the html subset gains one
element. No existing route, field or status changes. The plan:
[plan-backend.md](plan-backend.md); the decisions:
[ADR-0045](../../adr/0045-image-store-in-own-schema-libvips-hourly-sweep.md).

## 0. Summary

| # | Route | Right | Answer |
|---|---|---|---|
| 1 | `POST /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/images` | `workitems.workitem.write` on the project | 201 `ImageUpload` |
| 2 | `POST /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/images/reductions` | `workitems.workitem.write` on the project | 201 `ImageReduction` |
| 3 | `GET /api/images/{imageId}` | signed in; the item's read rule decided in the handler | 200 bytes / 304 |
| — | `PATCH …/workitems/{identifier}` (existing) | unchanged | the html subset gains `<img data-image-id alt>`; two new field reasons |

All three new routes answer 401 without a token, first. Routes 1-2 answer the
scoped check's 404 «Организация не найдена» / 403 as every organization route
(mod-016 api-contract § 4).

## 1. Upload: `POST …/workitems/{identifier}/images`

US-2, BR-IM-2..-6, -10. Makes an **unbound upload** of the caller for this
item, readable by the caller only until a description write binds it (§ 4).

**Request.** The body is the file's bytes, nothing else (no multipart, no
JSON). `Content-Type` should be `application/octet-stream`; any value is
accepted and ignored: the format is read from the content. At most
5 242 880 bytes; a declared `Content-Length` above it is refused without
reading.

**Response 201** (`ImageUpload`):

```json
{ "id": "3f2a1c9e-6b1d-4c8a-9f0e-2d7b5a41c6e3", "format": "png", "byteLength": 1834211, "width": 2560, "height": 1440 }
```

| Field | Meaning |
|---|---|
| `id` | The opaque reference: a random version-4 uuid, lower case. It is what the description stores (§ 4) and what the read takes (§ 3). It names no organization, project, item or user (BR-IM-10) |
| `format` | `"png"`, `"jpeg"`, `"gif"`, `"webp"`: the **stored** format (always the uploaded one, BR-IM-4) |
| `byteLength` | The stored size in bytes (≤ 5 242 880) |
| `width`, `height` | The stored picture's size in pixels, as displayed (orientation applied) |

Nothing else is in the body and no `Location` header is promised. The text
alternative is the client's (the file name without its extension, or
«Изображение»; US-1): the server never receives it at upload.

**Order of answers.** 401; the scoped check (404 organization, 403 role
without `workitems.workitem.write` on the project); the address as the item's
PATCH resolves it (404 `organizations.project.not_found`, 404
`workitems.workitem.not_found`); 409 `workitems.workitem.deleted` for an item
in «Удалена»; **then** the body is read: 400 size; 400 format, dimensions,
animation; 503 busy. A refusal stores nothing. The item is re-checked live
under `FOR SHARE` when the row is inserted (a deletion that commits meanwhile
answers 409; a purge, 404).

## 2. Reduction: `POST …/workitems/{identifier}/images/reductions`

US-6, BR-IM-3 (b), BR-IM-20. The same request shape and order of answers as
§ 1, with a body cap of **20 971 520** bytes. The server runs the upload's
processing, then reduces within the floors (similarity ≥ 0.95 for JPEG and
lossy WebP, then scaling, never below half the original's width and height;
PNG, GIF, lossless WebP only scaled, losslessly). The first result within
5 242 880 bytes is stored as the caller's unbound upload for the item; the
original is never stored. A body of 5 MB or less is processed as an ordinary
upload and answered in this shape.

**Response 201** (`ImageReduction`):

```json
{
  "image": { "id": "9b0e…", "format": "jpeg", "byteLength": 4718592, "width": 5712, "height": 4284 },
  "original": { "format": "jpeg", "byteLength": 7759462, "width": 8160, "height": 6120 }
}
```

| Field | Meaning |
|---|---|
| `image` | An `ImageUpload` (§ 1) of the stored result: what «Стало» shows and what the preview reads through § 3 |
| `original` | The file as sent: `byteLength` of the body, `width`/`height` as displayed (orientation applied): what «Было» shows. No `id`: it is not stored |

Refusals specific to this route: 400 `images.reduction.too_large` (over 20 MB),
400 `images.reduction.animated` (an animated image), 400
`images.reduction.cannot_reduce` (no step reaches 5 MB within the floors).
The NFR is 15 s for a 20 MB, 48 MP JPEG; the client draws no timeout of its
own (design D-7) and may abort.

## 3. Read: `GET /api/images/{imageId}`

US-4, BR-IM-10, -11. `{imageId}` is the stored reference. The route carries no
organization (BR-IM-10), so it is `AuthenticatedOnly` and the access is
decided per image:

- **bound** to an item: readable by exactly those whose `GET
  …/workitems/{identifier}` of that item answers 200 now (organization
  active and a membership, project reachable, `workitems.workitem.read` on
  the project with its grants, the item in any state, «Удалена» included);
- **unbound**: readable by its uploader only.

**Response 200**: the stored bytes, and these headers:

| Header | Value |
|---|---|
| `Content-Type` | `image/png`, `image/jpeg`, `image/gif` or `image/webp` (the stored format) |
| `Content-Length` | the stored size |
| `ETag` | `"<imageId>"` (bytes never change) |
| `Cache-Control` | `private, no-cache` (the browser may keep it, must revalidate: every display re-checks access; no shared cache) |
| `X-Content-Type-Options` | `nosniff` |
| `Content-Security-Policy` | `default-src 'none'; sandbox` |
| `Cross-Origin-Resource-Policy` | `same-origin` |

**Response 304**: a request whose `If-None-Match` equals the `ETag`, **after**
the access check passes; no body, the same `ETag` and `Cache-Control`.

**Response 404** `images.image.not_found`, identical (status and body but the
`traceId`) for: a malformed id, an unknown id, a swept upload, a purged
item's image, another user's unbound upload, an image of an item the caller
may not read. No 403 is ever answered here.

**Client note (design § Host seam).** The panel cannot put its bearer token
on an `<img>`, so the item form's address rule fetches this route with the
token (`fetch`, `Authorization: Bearer`) and shows the bytes through an object
URL; the editor itself names no route. An image copied from another
description is re-uploaded through § 1 after a read here (BR-IM-13); a 404 is
announced as the upload-failed notice.

## 4. Description write: `PATCH …/workitems/{identifier}` (amended)

US-3, BR-IM-7, -8, -9. Only `descriptionFormat: "html"` is affected; `plain`
stores `<img …>` as characters and binds nothing (BR-RT-10).

### 4.1 The image element

The stored (and returned) form of one image, a block of its own:

```html
<img data-image-id="3f2a1c9e-6b1d-4c8a-9f0e-2d7b5a41c6e3" alt="Скриншот ошибки">
```

- Exactly these two attributes, in this order, double-quoted; a void element
  (no `/>`). `alt` is always present (`alt=""` when none was sent), at most
  255 code points (longer is cut), escaped as feat-018's attributes (`&amp;`,
  `&quot;`, `&nbsp;`).
- The server reads `data-image-id` and `alt` and drops every other attribute
  (`src`, `width`, `style`, `class`, `on*`, …). An `img` without a
  `data-image-id` that parses as a uuid is dropped, whatever its `src`
  (http, https, data, relative, `//host`); the text around stays. **The
  stored markup has no `src`**: the surface derives the address from the id.
- Placement (BR-IM-7): at the top level or in a list item, as a block. In a
  paragraph it splits the paragraph (an empty half is not kept); in a heading
  it moves right after the heading; in a link it leaves the link; in a table
  cell or a code block it is dropped. The canonical cases are in
  `contracts/rich-text/fixtures/canonical.json` (`image-*`), run by both
  suites.
- Counting (BR-IM-9): an image adds no character and no line break to the
  visible text (`a`, image, `b` counts as `a\nb`, 3); an html value whose only
  content is images is **not** empty and is stored as such; its markup counts
  toward the 1 000 000-code-point ceiling.

### 4.2 References on a write

For each `data-image-id` of the reduced value: it must be an image already
bound to this item, or the writer's own unbound upload for this item;
otherwise the **whole** write is refused and nothing is stored. More than 50
image elements are refused the same way. On success, the writer's unbound
uploads it references become the item's images, in the same transaction.
Both refusals are field reasons of `workitems.validation_failed` under
`errors.description`, status 400, in the existing order of answers (after the
path's 404 and the item's 409 `deleted`, together with every other field
reason). An image taken out of the text stays the item's image (D-9); nothing
is unbound.

### 4.3 Response

Unchanged (`WorkItem`): `description` returns the reduced value with its image
elements. The bytes are never in it.

## 5. Error codes

New codes; the catalog files (`contracts/errors/images.json`, new, and
`contracts/errors/workitems.json`) and the regenerated `docs/error-codes.md`
are written by the dev stage with these texts and Kazakh translations marked
`kkNeedsReview`.

| Code | Status | Params | ru | Where |
|---|---|---|---|---|
| `images.upload.too_large` | 400 | — | «Изображение больше 5 МБ. Уменьшите его и вставьте снова.» | § 1 |
| `images.upload.format_unsupported` | 400 | — | «Можно вставить только изображения PNG, JPEG, GIF или WebP.» | § 1, § 2 |
| `images.upload.too_many_pixels` | 400 | — | «Изображение слишком большое: не больше 60 мегапикселей и 20 000 пикселей по стороне.» | § 1, § 2 |
| `images.upload.animation_too_large` | 400 | — | «Анимация слишком большая: не больше 1 000 кадров.» | § 1, § 2 (over 1 000 frames, or over 600 000 000 pixels in all frames) |
| `images.reduction.too_large` | 400 | — | «Файл больше 20 МБ. Уменьшите изображение в другой программе и вставьте снова.» | § 2 |
| `images.reduction.animated` | 400 | — | «Анимация больше 5 МБ. Уменьшите её в другой программе и вставьте снова.» | § 2 |
| `images.reduction.cannot_reduce` | 400 | — | «Не удалось уменьшить изображение до 5 МБ без сильной потери качества. Уменьшите его в другой программе и вставьте снова.» | § 2 |
| `images.busy` | 503 (+ `Retry-After: 5`) | — | «Сервер сейчас обрабатывает другие изображения. Попробуйте ещё раз через несколько секунд.» | § 1, § 2 |
| `images.image.not_found` | 404 | — | «Изображение недоступно.» | § 3 |
| `workitems.workitem.description_image_unavailable` | 400 (field reason) | — | «Изображение больше недоступно. Удалите его и вставьте снова.» | § 4 |
| `workitems.workitem.description_too_many_images` | 400 (field reason) | — | «В описании может быть не больше 50 изображений.» | § 4 |

Existing codes these routes also answer: `organizations.organization.not_found`
and the scoped check's 403 `organizations.access.forbidden`, `organizations.project.not_found`,
`workitems.workitem.not_found`, `workitems.workitem.deleted` (§ 1, § 2);
`http.unauthorized` (all). A processing failure that is not a refusal
(a libvips defect) is the platform's 500, never a stored partial image.

Notes:

- Texts quoted from the stories and the design are used verbatim (US-1, US-3,
  US-6, design § Forms and validation). New wording, for the human's look at
  gate-3 with the catalog diff: `images.upload.too_large` (the panel never
  sends an over-5 MB file as an ordinary upload, so a foreign client or a
  defect sees it), `images.busy`, and `images.image.not_found` (the panel
  shows «Изображение недоступно» for any failed read anyway, US-4).
- `images.upload.animation_too_large` covers both animation caps with the
  design's one text, which names only the frame count; a separate text for
  the 600 000 000-pixel case would be a catalog edit.
- The client announces a 400 of § 1-2 by the code's text (design § Notices);
  a network failure, a 5xx other than `images.busy`, or an unknown code is
  «Не удалось загрузить изображение. Попробуйте ещё раз.» (upload) or
  «Не удалось уменьшить изображение. Попробуйте ещё раз.» (reduction).

## 6. Values the client mirrors

| Value | Server | Client (props defaults, design § Host seam) |
|---|---|---|
| Stored and ordinary upload cap | 5 242 880 bytes | offer above it |
| Reduction intake cap | 20 971 520 bytes | refuse above it before sending |
| Images per description | 50 | refuse at insert |
| Formats | by content | by declared type, as a convenience |

## 7. Compatibility

- A client that never sends `data-image-id` sees feat-018's behaviour
  exactly: every `img` is dropped, as before.
- Existing stored descriptions hold no `img` (feat-018 dropped them), so no
  data migration.
- feat-059 adds its own upload operation and purpose; these routes and the
  image element are shared by it unchanged (BR-IM-19).
