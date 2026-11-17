# Backend plan — feat-078-description-images

Stage 4a, 2026-11-17. Inputs: [spec](spec.md) (BR-IM-1..20), stories US-1..US-6,
[design](design.md) § Host seam, [ADR-0045](../../adr/0045-image-store-in-own-schema-libvips-hourly-sweep.md)
(accepted: NetVips, animated PNG like animated GIF, still GIF scaled nearest-neighbour),
[ADR-0039](../../adr/0039-rich-text-as-sanitized-html-with-tiptap.md) and feat-018's
reducer. The interface is fixed in [api-contract.md](api-contract.md). This plan
does not restate the ADR; it names the files, the order, the tests and the
points the ADR left to the implementation (§ 12).

## 1. What is backend

| Story | Backend part | Frontend only |
|---|---|---|
| US-1 | none beyond US-2/US-3 (the editor calls the upload and the reduction through the host seam) | everything |
| US-2 | the upload route, the intake (header reader, stripper, libvips, SSIM), the store | — |
| US-3 | the reducer's image element, the reference check and the bind in `SaveWorkItem` | round trip in the real editor |
| US-4 | the read route, its access rule and headers | display, full-size view |
| US-5 | the sweep, the purge cascade | — |
| US-6 | the reduction route (the intake's reduction mode) | offer, result, queue dialogs |

## 2. Slices and files

New module **Images** in the four usual places (ADR-0045 decision 1). Paths
below are under `src/backend/`.

| Layer | Path | What |
|---|---|---|
| Domain | `Mustask.Domain/Images/Image.cs` | the aggregate: `Id` (v4), `Purpose`, `Format`, `ByteLength`, `Width`, `Height`, `FrameCount`, `UploadedBy`, `UploadedAt`, `BoundAt`, `WorkItemId`; `ForWorkItemDescription(...)` factory; `CheckBinding(ImageBindingRule)` → `ImageBindingVerdict` |
| Domain | `Mustask.Domain/Images/ImageContent.cs` | `ImageId`, `Content` (byte[]); separate entity so a tracked `Image` never loads bytes |
| Domain | `Mustask.Domain/Images/ImagePurpose.cs`, `ImageFormat.cs` | enums, stored as strings; `ImagePurpose.WorkItemDescription` only |
| Domain | `Mustask.Domain/Images/ImageBindingRule.cs` | purpose-supplied rule: today `ForWorkItemDescription(workItemId, writerSubject)`; feat-059 adds its own factory |
| Domain | `Mustask.Domain/Images/ImageLimits.cs` | the spec's caps (§ 3) |
| Domain | `Mustask.Domain/Images/ImageErrorCodes.cs` | `[ErrorCodes]`, every `images.*` code of the contract |
| Domain | `Mustask.Domain/WorkItems/WorkItemErrorCodes.cs`, `WorkItemText.cs`, `WorkItemLimits.cs` | two description codes; `ValidateImages(count)`; `DescriptionMaxImages = 50` |
| Shared | `Mustask.Shared/Exceptions/ServiceUnavailableException.cs` | new descendant, 503, carries `RetryAfter` (§ 6.4) |
| Application | `Abstractions/Images/IImageIntake.cs` | the port of ADR-0045 decision 2 (§ 6.1) |
| Application | `Abstractions/Images/IImageStore.cs` | insert (row + content), find metadata, read content into a caller buffer |
| Application | `Abstractions/Images/IImageBinding.cs` | lock in id order, check the rule, bind (§ 7) |
| Application | `Abstractions/Images/IUploadBody.cs` | reads the request body under a cap (implemented in Api over `HttpRequest`) |
| Application | `Abstractions/WorkItems/IWorkItemReadAccess.cs` | "may this subject read this item now", for routes that carry no organization (§ 8) |
| Application | `Abstractions/WorkItems/IWorkItemRepository.cs` | + `ShareLockAsync(projectId, number)` (`FOR SHARE`) |
| Application | `Abstractions/RichText/IRichTextReducer.cs` | `RichTextReduction` gains `ImageIds` (document order, duplicates kept) |
| Application | `Features/Images/UploadDescriptionImage/` | command, handler, response (reference use case) |
| Application | `Features/Images/ReduceDescriptionImage/` | command, handler, response |
| Application | `Features/Images/GetImage/` | query, handler, result |
| Application | `Features/WorkItems/SaveWorkItem/SaveWorkItemHandler.cs` | the image count and the bind (§ 7) |
| Infrastructure | `Images/Configurations/ImageConfiguration.cs`, `ImageContentConfiguration.cs` | `ToTable(..., schema: "images")` |
| Infrastructure | `Images/ImageStore.cs`, `ImageBinding.cs`, `UnboundImageSweeper.cs`, `ImagesOptions.cs`, `ImagesServiceCollectionExtensions.cs` | |
| Infrastructure | `Images/Intake/ImageIntake.cs`, `DecodeGate.cs` | the pipeline and the limiter |
| Infrastructure | `Images/Intake/Headers/{ImageHeader,PngHeaderReader,JpegHeaderReader,GifHeaderReader,WebpHeaderReader,ImageHeaderReader}.cs` | ours, managed (§ 4) |
| Infrastructure | `Images/Intake/Stripping/{PngStripper,JpegStripper,GifStripper,WebpStripper,MinimalExif}.cs` | ours, managed (§ 4.2) |
| Infrastructure | `Images/Intake/Processing/{VipsCodec,VipsSetup,Ssim,QualitySearch,ReductionLadder}.cs` | the only files that name NetVips (§ 5) |
| Infrastructure | `WorkItems/WorkItemReadAccess.cs` | `IWorkItemReadAccess` (§ 8) |
| Infrastructure | `RichText/{CanonicalModel,DomConverter,CanonicalWriter,HtmlRichTextReducer}.cs` | the image block (§ 9) |
| Infrastructure | `Migrations/<ts>_AddImageStore.cs` | § 3.2 |
| Api | `Features/Images/ImagesEndpoints.cs` | all three routes, written in full in the first slice |
| Api | `Features/Images/HttpUploadBody.cs`, `ImageResults.cs` | body reader; the read's headers and 304 |
| Api | `ErrorHandling/BusinessExceptionHandler.cs` | `ServiceUnavailableException` → 503 + `Retry-After` |
| Api | `appsettings.json` | `Images` section (§ 6.3, § 10) |
| contracts | `errors/images.json` (new), `errors/workitems.json` | codes, ru + kk (`kkNeedsReview`) |
| contracts | `rich-text/fixtures/canonical.json`, `visible-count.json`, `hostile-corpus.json` | image cases (§ 9.3) |
| docs | `error-codes.md` (regenerated), `conventions/backend.md` (exception table row), `deploy/vm.md` + `vm.ru.md` (§ 11) | |
| deploy | `vm/nginx-mustask.conf`, `vm/nginx-mustask-locations.conf` | `client_max_body_size` (§ 11) |
| root | `Directory.Packages.props` | NetVips pins with a licence comment (§ 11.1) — through the orchestrator (write zone) |

Slices reference no other slice: the Images slices reach work items through
`Abstractions/WorkItems`, `SaveWorkItem` reaches images through
`Abstractions/Images`.

## 3. Domain and persistence

### 3.1 Domain

- `ImageLimits` (constants next to the concept, backend.md § Named values 3;
  the spec fixes them, the panel mirrors them as props): `MaxStoredBytes =
  5_242_880`, `MaxReductionInputBytes = 20_971_520`, `MaxPixels = 60_000_000`,
  `MaxSide = 20_000`, `MaxFrames = 1_000`, `MaxAnimationPixels =
  600_000_000`, `MaxPixelsAfterScaling = 16_777_216`, `ReencodeAboveBytes =
  1_048_576`, `MinSimilarity = 0.98`, `MinReductionSimilarity = 0.95`,
  `ReductionSideFloor = 0.5`, `MaxTextAlternativeCodePoints = 255`,
  `UnboundLifetime = 2 h`, `SweepInterval = 1 h`.
- `Image.ForWorkItemDescription(id, format, facts, uploadedBy, workItemId,
  now)`; `Bind(now)` sets `BoundAt` once (idempotent on a bound image);
  nothing else mutates.
- `Image.CheckBinding(rule)`: `Bound` when `BoundAt` set and `WorkItemId` is
  the rule's item; `Bindable` when unbound, same item, `UploadedBy` is the
  writer; otherwise `Unavailable`. Missing rows are `Unavailable` by the
  caller. One verdict for every refusal (non-disclosure).
- `WorkItemText.ValidateImages(int count)` → `description_too_many_images`
  above 50; applied after the visible-length check, before anything is locked.

### 3.2 Migration `AddImageStore`

One migration in the one stream (ADR-0002), generated by `dotnet ef
migrations add` and hand-checked:

- `EnsureSchema("images")`.
- `images.images`: `id uuid pk`, `purpose text not null`, `format text not
  null`, `byte_length integer not null`, `width integer`, `height integer`,
  `frame_count integer not null`, `uploaded_by text not null` (as
  `created_by` elsewhere), `uploaded_at timestamptz not null`, `bound_at
  timestamptz null`, `work_item_id uuid null`.
  - FK `work_item_id → public.work_items(id) ON DELETE CASCADE` (ADR-0030
    rule 7; the purge removes bound and unbound alike in its transaction).
  - `ck_images_purpose_owner`: `purpose = 'WorkItemDescription' AND
    work_item_id IS NOT NULL` (feat-059 widens it in its own migration).
  - `ck_images_format` (four values), `ck_images_byte_length` (`1..5242880`),
    `ck_images_dimensions` (`width/height between 1 and 20000`,
    `frame_count between 1 and 1000`).
  - `ix_images_work_item_id` (the cascade and the binding's lookups);
    `ix_images_unbound_uploaded_at` on `(uploaded_at, id) WHERE bound_at IS
    NULL` (the sweep).
- `images.image_contents`: `image_id uuid pk` FK → `images.images(id) ON
  DELETE CASCADE`, `content bytea not null`; then `migrationBuilder.Sql("ALTER
  TABLE images.image_contents ALTER COLUMN content SET STORAGE EXTERNAL")`.
- `Down`: drop `image_contents`, `images`, then `DropSchema("images")`.
- `xmin` comes from the convention (ADR-0010) and is not relied on.

Model-level guards (no Docker, `Mustask.Infrastructure.Tests/Conventions`):

- `ImagesSchemaOwnershipTests`: every entity mapped to schema `images` is in
  namespace `Mustask.Domain.Images`, and every entity of that namespace maps
  to `images` (ADR-0045 decision 1).
- `WorkItemReferenceInventoryTests`: the expected list gains
  `Image.WorkItemId` with `Cascade`.

## 4. The header reader and the stripper (ours, managed)

### 4.1 Header reader

`ImageHeaderReader.Read(ReadOnlySpan<byte>)` → `ImageHeader { Format,
CanvasWidth, CanvasHeight, FrameCount, TotalFramePixels, IsAnimated,
IsLossless (WebP), Orientation (1..8), ContainerEnd }` or a refusal
(`Format`, `Dimensions`, `Animation`). Iterative walkers with deterministic
bounds; nothing allocates in proportion to a declared size.

- **Recognition by magic only**: PNG `89 50 4E 47 0D 0A 1A 0A`; JPEG `FF D8
  FF`; GIF `GIF87a`/`GIF89a`; WebP `RIFF....WEBP`. Anything else (SVG, HTML,
  HEIC, AVIF, BMP, TIFF, ICO, a ZIP) is `Format`.
- **PNG**: chunk walk with length ≤ remaining bytes; `IHDR` first; `acTL`
  marks animated (D-2: an APNG is treated like an animated GIF), each `fcTL`
  adds its frame's pixels; `eXIf` yields the orientation; `IEND` required
  (a truncated file is `Format`); at most 100 000 chunks.
- **JPEG**: marker walk to `SOFn` (dimensions; height 0 with `DNL` refused),
  `APP1 Exif` parsed for IFD0 tag `0x0112` only, bounded; after each `SOS` a
  linear scan of the entropy-coded data (`FF 00` stuffing, `RSTn`) to the
  next marker; `EOI` required.
- **GIF**: logical screen, optional global table, then blocks: image
  descriptors (frame size counted; LZW sub-blocks skipped unread),
  extensions skipped by sub-block; trailer `3B` required. More than one frame
  is animated.
- **WebP**: RIFF size must fit the buffer; `VP8 ` (lossy, size from the frame
  header), `VP8L` (lossless, 14-bit sizes), `VP8X` (canvas, flags), `ANIM`,
  `ANMF` (frame sizes, frame count).
- **Caps** (BR-IM-3 (c)): either side > 20 000, canvas > 60 000 000 pixels,
  frames > 1 000 or all frames > 600 000 000 pixels → refused here, before
  any decode. The canvas of an animation counts against the still caps too.
- `ContainerEnd`: bytes after the container's end (a polyglot's tail) are
  never stored (§ 4.2).

### 4.2 Stripper

Rewrites the container keeping an allow-list (ADR-0045 decision 2 step 3).
Output is pixel-identical by construction (entropy-coded / compressed data is
copied byte for byte).

| Format | Kept | Dropped |
|---|---|---|
| PNG | `IHDR PLTE tRNS gAMA cHRM sRGB iCCP cICP sBIT IDAT acTL fcTL fdAT IEND` | `tEXt zTXt iTXt tIME eXIf pHYs` and every other ancillary chunk |
| JPEG | `SOI APP0(JFIF) APP2(ICC_PROFILE) APP14(Adobe) DQT DHT DRI SOFn SOS`+data, `EOI` | `APP1` (Exif, XMP), `APP13` (IPTC), `COM`, other `APPn`, `APP2` MPF, everything after `EOI` |
| GIF | header, screen, tables, image data, GCE (timing), `NETSCAPE2.0`/`ANIMEXTS1.0` (loop) | comment, plain-text and every other application extension |
| WebP | `VP8X` (flags rewritten), `ICCP ANIM ANMF ALPH VP8 VP8L` | `EXIF XMP ` and unknown chunks; RIFF size rewritten |

`MinimalExif` writes one IFD0 with tag `0x0112` only, when the orientation is
not 1 and the pixels are not re-encoded: as a JPEG `APP1`, a PNG `eXIf`, a
WebP `EXIF` (+ the `VP8X` flag). That is "the one surviving orientation field"
of BR-IM-4 (1).

## 5. libvips through NetVips

- **Only `Images/Intake/Processing/*` names NetVips** (an architecture test
  pins it: no `NetVips` reference outside that folder, none in Domain,
  Application or Api).
- **`VipsSetup`**, once at start (a hosted-service-free static init called by
  DI registration): `NetVips.ModuleInitialized` asserted (a missing native
  library fails the API's start with a clear log line, not the first
  upload); version ≥ 8.18 asserted; `Cache.Max = 0`, `Cache.MaxMem = 0`,
  `Cache.MaxFiles = 0`; `NetVips.Concurrency = Images:Intake:VipsThreads`;
  untrusted-operation block on (`NetVips.BlockUntrusted = true`), then every
  `VipsForeignLoad` blocked and the four loaders `VipsForeignLoadPngBuffer` /
  `Jpeg` / `Gif` / `Webp` unblocked (exact class names confirmed at the
  spike, § 12).
- **Loaders are called by name** (`PngloadBuffer`, `JpegloadBuffer`,
  `GifloadBuffer`, `WebploadBuffer`), never `NewFromBuffer`; `fail-on: error`,
  `access: sequential`. A loader error is `Format` (a structurally valid but
  corrupt file).
- **Pipeline on the re-encode path** (in the gate): decode → scale to ≤ 16
  777 216 pixels **before** rotation (so a rotation never materialises more
  than the scaled image) → apply orientation (`autorot`) → encode with
  `keep: icc` only. Kernels: Lanczos3 for PNG, JPEG, WebP; nearest-neighbour
  for GIF (ADR D-3), saved with `gifsave reuse` so the palette stays exact.
- **Encoders**: JPEG `jpegsave Q, optimize_coding, no interlace,
  subsample auto`; lossy WebP `webpsave Q, effort 4`; lossless WebP `lossless`;
  PNG `pngsave compression 9, no palette conversion`; GIF `gifsave reuse`.
- **Colour**: the source profile is carried, never converted (ADR decision 2).
  A CMYK JPEG that must be re-encoded is converted to sRGB through its profile
  (BR-IM-4 (1) allows it) — the only conversion.

### 5.1 SSIM (ours)

`Ssim.Mean(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, int width, int
height)` on 8-bit luminance planes, `Y = 0.299 R + 0.587 G + 0.114 B` of
the decoded values (both pictures carry the same profile, so like is compared
with like), every 8×8 window at stride 1 with uniform weights,
`C1 = (0.01·255)²`, `C2 = (0.03·255)²`, the mean over all windows. Running
column sums over an 8-row band: O(width) memory, O(pixels) time. Pinned by
unit tests against hand-computed values (identical planes = 1, a constant
offset, a known 16×16 pair) so a library change cannot move it. The planes
come from libvips (`colourspace b-w` is **not** used: its weights differ; we
compute Y ourselves from the RGB bands).

### 5.2 The quality search and the reduction ladder

- **Upload** (BR-IM-4 (4)): a lossy JPEG/WebP whose stripped form is over 1 MB
  (or that was scaled): binary search of `Q` over `[40, 100]` for the lowest
  `Q` whose decoded result has SSIM ≥ 0.98 to the reference (the picture
  after (1)-(2)); at most 7 encodes. Then BR-IM-4 (5): if the result is larger
  than the upload, the stripped upload is stored instead.
- **Reduction** (BR-IM-20 (2)): first the upload steps; if the result is over
  5 MB: for JPEG/lossy WebP the same search at SSIM ≥ 0.95; still over: scale
  the sides by 0.85, 0.72, 0.61, 0.52, then exactly 0.5 of the **original's**
  sides (each step re-checks the lowest `Q` ≥ 0.95 against the reference
  scaled to that size, starting from the previous step's `Q`); PNG, GIF and
  lossless WebP only scale (lossless encode). The first result ≤ 5 242 880
  bytes wins. None → `cannot_reduce`, nothing stored.
- Every result is re-read by our header reader before it is stored (the caps
  hold on the output too: US-6 "passes every check").

## 6. The intake port and the gate

### 6.1 `IImageIntake`

```csharp
public interface IImageIntake
{
    Task<ImageIntakeResult> AcceptAsync(ReadOnlyMemory<byte> upload, ImageIntakeMode mode, CancellationToken cancellationToken);
}
public enum ImageIntakeMode { Upload, Reduction }
public enum ImageIntakeOutcome { Accepted, Format, Dimensions, Animation, AnimatedReduction, CannotReduce, Busy }
public sealed record ImageFacts(ImageFormat Format, int Width, int Height, int FrameCount, int ByteLength);
public sealed record ImageIntakeResult(ImageIntakeOutcome Outcome, byte[]? Content, ImageFacts? Stored, ImageFacts? Original);
```

Order: header read (refusals) → strip → decide: animated (Upload: done;
Reduction: `AnimatedReduction`), still within the no-re-encode rules (done),
else enter the gate and process. The size cap is not the intake's: the body
reader enforces it (§ 6.2). `Original` reports the upload's dimensions with
the orientation applied (what the member saw), for US-6's «Было».

### 6.2 Reading the body

`HttpUploadBody` (Api) sets `IHttpMaxRequestBodySizeFeature.MaxRequestBodySize`
to cap + 1 for the request, refuses a declared `Content-Length` above the cap
without reading, then reads into an `ArrayPool` buffer counting bytes and
stops at cap + 1 → `too_large`. The cap is 5 242 880 (upload) or 20 971 520
(reduction); the body is read **after** the address checks, so a refused
caller costs no transfer. The declared `Content-Type` is ignored (BR-IM-2).

### 6.3 `DecodeGate` (ADR-0045 decision 5)

A `ConcurrencyLimiter` (`System.Threading.RateLimiting`) singleton, as
`AggregateReadLimiter`: `Images:Intake:MaxConcurrentDecodes` (default 2),
`QueueLength` (4), `MaxWaitSeconds` (10), `VipsThreads` (2). Past the queue or
the wait: `Busy` → 503 `images.busy`, `Retry-After: 5`.

Memory arithmetic per permit (defaults, to be measured on the stand): a 60 MP
still is scaled on load to ≤ 16.8 MP before anything is materialised; the
rotated scaled image ≤ 16.8 MP × 3 bytes ≈ 50 MB; two luminance planes for
SSIM ≈ 34 MB; one encoded candidate ≤ 20 MB; libvips tile buffers for 2
threads ≈ 20-40 MB. About 150 MB a permit, ~300 MB for two; the reduction of
a 48 MP / 20 MB JPEG is the same after its scale-on-load. Request bodies
waiting: (2 + 4) × 20 MB = 120 MB at most on the reduction route. Bodies of
animated and strip-only uploads never wait in the gate.

### 6.4 503 as a business answer

`ServiceUnavailableException(code, retryAfter)` joins the hierarchy (503);
`BusinessExceptionHandler` sets `Retry-After` from it. backend.md's table
gains the row. The decision (503 with its own code) is ADR-0045 decision 5;
this is its mechanism, not a new decision.

## 7. The description write: count, lock, check, bind (race rule)

In `SaveWorkItemHandler`:

1. `PrepareDescriptionAsync` (outside the transaction): the reducer now
   returns `ImageIds`; after the visible-length check,
   `WorkItemText.ValidateImages(ImageIds.Count)` (elements, duplicates
   counted). Plain format: no image is ever recognised (BR-RT-10).
2. In the transaction, **after the item's `FOR UPDATE` and `EnsureLive`** and
   before step 2 (`ValidateValues`), when the prepared description is html,
   unrefused and has images:
   `imageBinding.LockAndBindAsync(distinctIds,
   ImageBindingRule.ForWorkItemDescription(item.Id, currentUser.SubjectId),
   now, ct)`:
   - `SELECT id, … FROM images.images WHERE id = ANY(@ids) ORDER BY id FOR
     UPDATE` (metadata columns only, no content);
   - every id missing from the answer, or `Unavailable` by the domain →
     the outcome is refused (nothing updated);
   - otherwise `UPDATE images.images SET bound_at = @now WHERE id =
     ANY(@bindable)` (an `ExecuteUpdate`, independent of the item's change
     tracking).
   A refusal becomes the field reason
   `description: workitems.workitem.description_image_unavailable` in the one
   400 of step 2; the transaction rolls back, so a bind of an earlier id in
   the same write is undone.
3. Lock order of the save becomes: team `FOR SHARE` → hierarchy → sprint → item
   → **images (id order)** → area → parents. Nothing else locks image rows
   after an item, and the sweep takes no item lock (§ 8.2): no cycle.

A save whose description is not sent, is plain, or holds no image calls
nothing. An unchanged html value re-binds nothing (its images are bound).

## 8. Reads and the sweep

### 8.1 `GetImage`

Route `GET /api/images/{imageId}` (`.AuthenticatedOnly(reason)`: the address
carries no organization by BR-IM-10, so the item's rule is decided in the
handler). Steps: id parsed (`Guid`, "D" form) else 404; metadata row else 404;
unbound → caller is `UploadedBy` else 404; bound → `IWorkItemReadAccess
.MayReadAsync(workItemId, subject)` else 404; `If-None-Match` equal to the
ETag → 304; else read the content with `SequentialAccess` into a pooled buffer
of `ByteLength`, release the connection, write. Every refusal is the same
`404 images.image.not_found`.

`IWorkItemReadAccess` (Infrastructure/WorkItems) answers what the item's GET
would: the organization active and the subject a member
(`IOrganizationAccessReadModel`), the project reachable (`Reachability`), the
item in any state (`WorkItemSets.InAnyState`), and
`workitems.workitem.read` on the project through `EffectivePermissions` with
the grants of `IScopedAccessReadModel`. Read live, nothing cached. A parity
test pins it to the item's GET (§ 13).

### 8.2 `UnboundImageSweeper`

`BackgroundService` in `Mustask.Infrastructure/Images`, singleton +
`AddHostedService(sp => sp.GetRequiredService<UnboundImageSweeper>())` (the
`RealmReadinessMonitor` pattern), always registered; `Images:Sweep:Enabled`
(default `true`, `false` in the integration test hosts) only stops
`ExecuteAsync`. Runs once at start, then on a `PeriodicTimer(1 h)` over the
injected `TimeProvider`. `RunOnceAsync(ct)` is public for tests.

One run, cutoff `IClock.UtcNow − 2 h`, every purpose:

1. **Batches with `SKIP LOCKED`**: `DELETE FROM images.images WHERE id IN
   (SELECT id FROM images.images WHERE bound_at IS NULL AND uploaded_at <=
   @cutoff ORDER BY id LIMIT @batch FOR UPDATE SKIP LOCKED)`, each batch its
   own transaction, until a batch deletes nothing. What is still due after
   that is exactly what was locked by someone else: a plain `SELECT id`
   with the same predicate (no lock, so it never waits) lists it.
2. **The skipped ones, one row per transaction, waiting**: `DELETE … WHERE id
   = @id AND bound_at IS NULL AND uploaded_at <= @cutoff`. The row's holder
   (a save binding it, a purge deleting it) finishes first; the re-check then
   keeps a bound row and finds a purged one gone.
3. One `Information` line: `Image sweep removed {Count} unbound uploads`.

**Why this refines ADR-0045 decision 3** (same guarantee, no new decision):
the ADR has the sweep lock batches in id order and wait. A purge, however,
deletes an item's images by the cascade in no guaranteed order, so a waiting
batch could deadlock with a purge. With `SKIP LOCKED` batches the sweep never
waits while holding a lock, and the single-row pass waits holding nothing:
no cycle with a save, a purge or an upload. The bind/sweep outcomes are the
ADR's: a save that locked first binds and the sweep's re-check skips the row;
a sweep that deleted first leaves the save finding the row missing → refused.
The 3-hour bound holds: a row skipped in step 1 is decided in step 2 of the
same run.

`Images:Sweep:BatchSize` default 100. `RetentionTests`' hosted-service list
gains `UnboundImageSweeper`: "deletes unbound image uploads; writes no work
item row".

## 9. The reducer: one image element

### 9.1 Stored form (BR-IM-7, ADR-0039 amended)

`<img data-image-id="{uuid}" alt="{text}">`: void, these two attributes in
this order, double quotes, attribute escaping as today (`&amp;`, `&quot;`,
`&nbsp;`). `{uuid}` is the lower-case "D" form of a parsed `Guid`; an `img`
whose `data-image-id` does not parse, or that has none (any `src` alone:
http, https, data, relative, `//host`), is dropped. `alt` is cut to 255 code
points (never splitting a surrogate pair); a missing `alt` is written as
`alt=""`. Every other attribute is dropped. No `src` is ever stored: the
surface derives the address (BR-IM-19).

### 9.2 Placement (DomConverter → CanonicalModel)

- New block `ImageBlock(Guid id, string alt)`, allowed at the top level and in
  `ItemBlock.Blocks` (BR-RT-5's list item now holds paragraphs, nested lists
  and images).
- Met inside a paragraph (leaf): the current leaf is closed, the image block
  emitted, a new leaf of the same kind continues; an empty half is not kept.
- Inside a heading: queued and emitted right after the heading closes.
- Inside a link: the image leaves the link (the link stays around its text).
- Inside a table cell or a code block: dropped.
- `img` leaves the `Dropped` set; it is now a mapped element.
- Visible text and counts (BR-IM-9): an image adds no character and no line
  break; the line breaks between leaves are the existing rule ("a", image,
  "b" → `a\nb`). `HasContent` is true when any image is kept. Markup counts
  toward the 1 000 000 ceiling as written.
- `RichTextInputGuard`'s bounds are unchanged (an `img` is one element).

### 9.3 Shared fixtures

`canonical.json` gains US-3's eleven canonical cases (ids `image-*`, fixed v4
ids in the fixture); the existing case at line 58 (images with `src` only,
dropped) stays, its note retargeted from BR-RT-4 to BR-IM-7.
`visible-count.json`: "a", image, "b" = 3; an image alone = 0 and has
content. `hostile-corpus.json`: an `img` with a valid id plus `onerror`,
`src=javascript:`, `srcset`, `style` (must not contain any of them). The
panel's parity suite runs the same cases at its stage.

## 10. Configuration (`appsettings.json`, section `Images`)

| Key | Default | Why configuration |
|---|---|---|
| `Intake:MaxConcurrentDecodes` | 2 | tuned on the stand (ADR decision 5) |
| `Intake:QueueLength` | 4 | same |
| `Intake:MaxWaitSeconds` | 10 | same |
| `Intake:VipsThreads` | 2 | same |
| `Sweep:Enabled` | true | tests turn the timer off |
| `Sweep:BatchSize` | 100 | operational |

The caps, the 2 h window and the hourly cadence are the spec's and live in
`ImageLimits` (§ 3.1).

## 11. Packages and the native library

### 11.1 Pins

| Package | Version (nuget.org, 2026-11-17) | Referenced by | Licence (to verify at pin) |
|---|---|---|---|
| `NetVips` | 3.2.0 | Mustask.Infrastructure | MIT (wrapper) |
| `NetVips.Native.linux-x64` | 8.18.7 | Mustask.Infrastructure | libvips LGPL-2.1-or-later + the bundled libraries' own licences |
| `NetVips.Native.win-x64` | 8.18.7 | Mustask.Infrastructure | same |

The two RID packages, not the `NetVips.Native` meta-package (which pulls every
platform into the release). **Licences were not verified here**: none of the
three is in the local NuGet cache, and the stage does not download packages.
The dev stage, before the first commit that references them: restores, reads
`LICENSE` / `THIRD-PARTY-NOTICES` of the exact versions, and records in the
`Directory.Packages.props` comment (as AngleSharp's) what was read. Two
things to check specifically: (1) that the native bundle contains no GPL
component (e.g. a `libimagequant` of the GPL-3 line); if it does, it goes to
the human before code; (2) LGPL-2.1 for libvips as a dynamically loaded,
replaceable `.so`/`.dll` in an internal tool: the notices ship in the release;
any doubt about the company's position is the human's.

### 11.2 Where the native library comes from

- **Developer machine (Windows x64)**: the `win-x64` package puts
  `runtimes/win-x64/native/libvips-42.dll` (+ dependencies) under the build
  output of `Mustask.Api` and every test project (transitively, through the
  Infrastructure reference); nothing to install. macOS/ARM developers are not
  supported by these pins (adding `osx-arm64` is a one-line change if needed).
- **CI** (`.github/workflows/ci.yml`, `ubuntu-latest`, x64): the `linux-x64`
  package, restored with everything else; no `apt` step. The integration
  tests and `VipsSetup`'s start check prove it on every PR.
- **The VM** (Ubuntu 24.04 x64): there is no Dockerfile for the API; the
  release is `build-release.sh`'s framework-dependent `dotnet publish` without
  a RID, which copies both `runtimes/*/native` trees into `api/`; the
  `.deps.json` makes the runtime load the linux-x64 one. Nothing to install
  on the VM. `smoke.sh` gains no step: a missing or broken native library
  fails the API's start (§ 5 `VipsSetup`), which the smoke already catches.
- **nginx**: today's `location /api/` has no `client_max_body_size`, so the
  default 1 MB would refuse most uploads with a bare 413. Both
  `nginx-mustask.conf` and `nginx-mustask-locations.conf` set
  `client_max_body_size 21m;` on the API location (the reduction's 20 MB plus
  headroom; the API enforces the real caps). `docs/deploy/vm.md` and
  `vm.ru.md` say so, with the database volume note of ADR-0045 Consequences.

## 12. Points the ADR left to this plan, and the spike

Settled here: the element's markup (§ 9.1); 400 field reason, not 409, for an
unavailable image (it is a value of the description, answered with the other
field reasons); SSIM's exact window and luminance (§ 5.1); the quality search
and the ladder (§ 5.2); the sweep's `SKIP LOCKED` refinement (§ 8.2); BR-IM-4
(5) wins over (2) when both apply (the spec's text: "the upload is stored with
only (1) applied"); the read is `AuthenticatedOnly` with the item's rule in the
handler (§ 8.1).

**Spike, first thing in 4b** (½ day, recorded as § 12.1 here): NetVips 3.2.0
on Windows and in CI; the untrusted block API and the loader class names;
`gifsave reuse` keeps an exact palette after a nearest-neighbour scale (if
not, a still GIF over 16.7 MP or reduced is re-encoded by a managed LZW
writer of ours; the plan is amended); timings of the 5 MB / 12 MP upload and
the 20 MB / 48 MP reduction against the NFR (5 s / 15 s) on the developer
machine; memory peak per permit.

## 13. Tests (each criterion has one; comments carry `// US-n · "scenario"`)

Layers per [testing.md](../../conventions/testing.md). D = Domain unit, U =
Infrastructure unit (no Docker), M = Infrastructure on PostgreSQL
(Testcontainers), I = Api integration (Testcontainers, `World`, `ApiClient`,
`ProblemAssert`, `Race`). Every database is the test container's own;
nothing touches the `mustask` database on 5432 or the stand's ports.

**Image fixtures.** Small real samples are checked in under
`tests/Mustask.Infrastructure.Tests/Images/Fixtures/` (each < 200 KB: a JPEG
with camera data, GPS, a comment and orientation 6; a JPEG with a Display P3
profile; an animated GIF, APNG and animated WebP; HEIC, AVIF, BMP, TIFF, SVG,
HTML; the hostile corpus). The large ones (3-19 MB) are **generated by the
test** from fixed seeds through a `TestImages` builder on NetVips (gradients +
seeded noise encoded at chosen quality to hit the size), so the repository
carries no megabytes. Bombs and over-cap headers are written by hand in the
test (a valid header, a tiny body).

### US-2
| Criterion | Layer · test |
|---|---|
| A valid upload | I `UploadDescriptionImageEndpointTests.Upload_2MbPng_201_StoredUnboundForTheCaller_OnlyOpaqueFields` |
| Format from content (outline) | U `ImageHeaderReaderTests` one case per row; I `Upload_PngDeclaredJpeg_StoredAsPng`, `Upload_SvgNamedPng_400Format_NothingStored` |
| Caps (outline) | U `ImageHeaderReaderTests` (20 001 × 100, 20 000 × 3 000 accepted, 8 000², 40 KB bomb, 1 001 frames, 100 × 3 000²); U `ImageIntakeTests.Accept_OverCapHeader_CodecNeverCalled` (a spy codec); I `Upload_5242880Bytes_201`, `Upload_5242881Bytes_400TooLarge_NothingStored`, `Upload_BombPng_400Dimensions` |
| Metadata removed, orientation kept (600 KB / 3 MB) | U `ImageIntakeTests` on the camera JPEG: entropy data byte-equal + only orientation EXIF; re-encoded: rotated size, no EXIF |
| Colours kept | U `ImageIntakeTests.Accept_P3Jpeg3Mb_ProfileKept_SsimInSrgb≥0.98` |
| Screenshot exact | U `Accept_Png2560x1440_3Mb_PixelsEqual_NotLarger` |
| Large photo compressed | U `Accept_Jpeg4032x3024_4_8Mb_SameSize_Smaller_Ssim≥0.98` |
| Small photo left alone | U `Accept_Jpeg600Kb_EntropyDataUnchanged` |
| Very large picture scaled | U `Accept_Jpeg8160x6120_ScaledTo≤16777216_ProportionsKept_Ssim≥0.98` |
| Large PNG never lossy | U `Accept_Png3000x2000_4_9Mb_PixelsEqual` |
| Never bigger | U `Accept_LowQualityJpeg1_5Mb_StoredIsStrippedSource` |
| Animated keeps animation | U `Accept_Gif40Frames_FramesAndDelaysKept_NoComment`; same for APNG and animated WebP (ADR D-2) |
| Who may upload (outline) | I one test per row: read-only 403, «Удалена» 409 `workitems.workitem.deleted`, project deactivated 404, non-reader / no item 404; each asserts no row |
| No token | I `Upload_NoToken_401_NothingStored` |
| Hostile files | U `HostileImageCorpusTests` (refused, or stored with no metadata and no tail; nothing decoded past the caps); I one upload of the polyglot proves the route answers |
| Speed (NFR) | I `UploadDescriptionImageVolumeTests` (serial class): 5 MB / 12 MP JPEG within 5 s |
| SSIM itself | U `SsimTests` against hand-computed values |

### US-3
| Criterion | Layer · test |
|---|---|
| Canonical cases | U `ReducerFixtureTests` over the new `canonical.json` cases; I `SaveWorkItemDescriptionEndpointTests.Save_FixtureCanonicalCases_RoundTripThroughPatchGet` (changed: it first stores the fixture's image ids as the caller's unbound uploads for the bug) |
| Own uploads bound | I `SaveDescriptionImagesEndpointTests.Save_TwoOwnUploads_Stored_BothBound` |
| Re-saving keeps bound | I `Save_ImageBoundByAnother_ThirdMemberSaves_Kept` |
| Not the item's own (5 rows) | D `ImageTests.CheckBinding_*` (bound to Y, another's unbound, own for Y); I one Theory, each row → `ProblemAssert.Validation(…, "description", "workitems.workitem.description_image_unavailable")`, earlier description unchanged |
| Over 50 | D `WorkItemTextTests.ValidateImages_51_Refused`; I `Save_51Images_400TooManyImages_NothingStored` |
| Image alone | U reducer: `HasContent` true; I stored as the image, empty paragraphs still `""` |
| Images add nothing | U `visible-count.json`; I `Save_100000Chars_50ImagesWith255Alt_Accepted` |
| Markup names nothing | U reducer output shape; I read-back: every `img` matches `^<img data-image-id="[0-9a-f-]{36}" alt="[^"]*">$`, version nibble 4 |
| Plain format | I `Save_PlainWithImgTag_StoredVerbatim_NothingBound` |
| Save not touching Описание | I `Save_WithoutDescription_UploadStaysUnbound` |
| Canonical stable | U `ReducerRulesTests` idempotence over the image cases (frontend: jsdom) |
| Lock order | M `ImageBindingTests.LockAndBind_LocksInIdOrder_RefusalUpdatesNothing` |

### US-4
| Criterion | Layer · test |
|---|---|
| Reader sees images | I `GetImageEndpointTests.Get_ReadOnlyMember_200_Bytes` |
| Who reads (outline) | I reader 200; reader of an item in «Удалена» = the item's GET; non-reader 404; right revoked then read 404; no token 401 |
| Parity with the item | I `ImageReadParityTests`: for owner, member, viewer, project-grant holder, outsider, deactivated project, deactivated organization, deleted item: the image read is 200 iff the item's GET is 200 |
| Unbound: uploader only | I `Get_Unbound_UploaderGets_OtherEditor404` |
| Answers tell nothing | I `Get_ForeignSweptMadeUp_EqualStatusAndBody` (bodies compared without `traceId`, as the existing non-disclosure tests) |
| No shared cache | I headers: `Content-Type`, `nosniff`, `Cache-Control: private, no-cache`, CSP, CORP, `ETag`; `If-None-Match` → 304 without a body, still after the access check |
| Address names nothing | I upload answer and markup hold only a v4 id, unequal to every organization/project/item id of the arrangement |
| Display, full size, a11y | frontend (jsdom), manual QA |

### US-5
| Criterion | Layer · test |
|---|---|
| Removed after 2 h (10:05 / 11:00 / 12:00 / 13:00) | M `UnboundImageSweeperTests.Run_At1100_1200_Keeps_At1300_Removes` (controlled clock) |
| Removed from the editor before the save | I `Save_WithoutTheUpload_ThenSweep_Removed` |
| Save binds before the sweep | M `Run_AfterBind_Keeps` |
| Same moment | I `SweepRaceTests` (serial class): the sweep held after its lock (test hook in `RunOnceAsync`) while a save waits → refused, row gone, description unchanged; and the save held at commit (`CommitGate`) while the sweep waits → bound, kept |
| Save after the sweep | I `Save_SweptUpload_400ImageUnavailable` |
| Removed from text, kept | I `Save_WithoutBoundImage_StillReadable_SweepKeeps` |
| Deletion keeps, restore shows | I delete → image 200 for readers, restore → description and image unchanged |
| Purge takes the images | I `PurgeWorkItemEndpointTests.Purge_ItemWithThreeBoundAndOneUnbound_AllRemoved_Read404`; M `Purge_RolledBack_ImagesKept` (atomic by the cascade) |
| Removes nothing it should not | M `Run_BoundOld_UnboundTenMinutes_BothKept` |
| Every purpose | M `Run_OtherPurposeRow_Removed` (in its own database the test drops `ck_images_purpose_owner` and inserts a `FeedbackSubmission` row with raw SQL: the sweep's predicate names no purpose) |
| Log line, count only | M captured logger: one line, a count, no id or subject |
| Sweep vs purge, no deadlock | M `Run_WhilePurgeHoldsRows_NoDeadlock_RowDecidedInTheSamePass` |

### US-6
| Criterion | Layer · test |
|---|---|
| Big photo reduced | I `ReduceDescriptionImageEndpointTests.Reduce_7_4MbJpeg8160x6120_201_ResultAndOriginalFacts` |
| Result passes every check | U `ImageReductionTests` (≤ 5 242 880, same format, caps, no metadata); I exactly one row stored |
| Quality before size | U `Reduce_Jpeg12Mb4000x3000_KeepsSize_Ssim≥0.95` and a variant that must scale: ≥ 2 000 × 1 500 |
| Screenshot lossless | U `Reduce_Png7Mb5120x2880_Png_≥2560x1440_PixelsEqualToScaledSource` |
| Cannot reach | U + I `Reduce_NoisePng19Mb_400CannotReduce_NothingStored` |
| Server holds the line | I 6 MB ordinary → `images.upload.too_large`; reduction of 20 971 521 bytes → `images.reduction.too_large`; animated reduction → `images.reduction.animated` |
| Speed (NFR) | I `ReduceDescriptionImageVolumeTests` (serial): 20 MB / 48 MP within 15 s |
| Gate | I `IntakeGateTests` (serial): permits + queue exhausted → 503 `images.busy`, `Retry-After` |
| Offer, cancel, queue, failure, 50 at insert, keyboard | frontend |

### Migrations, inventories, contracts
- M `AddImageStoreMigrationTests`: up on a clean database, down, up again;
  the FK's `ON DELETE CASCADE` across schemas; `attstorage = 'e'` on
  `content`; the check constraints refuse a bad row.
- I `WorkItemWriteInventoryTests`: the two upload routes `RefusesADeletedItem`
  (`workitems.workitem.deleted`, proofs named above);
  `OrganizationAccessInventoryTests` sees them with the project scope;
  `EndpointCategoryInventoryTests` sees `GET /api/images/{imageId}` as
  `AuthenticatedOnly` with its reason. `RetentionTests` list gains the sweeper.
- Contracts: `images.json` and the workitems codes have ru + kk;
  `docs/error-codes.md` regenerated.
- Architecture: `NetVips` referenced only from `Mustask.Infrastructure/Images/Intake/Processing`.

## 14. Lanes (4c): no

Three use cases in the Images module (upload, reduction, read) plus a change
of an existing slice (`SaveWorkItem`) and a background service: fewer than
four use cases with disjoint zones. And the weight of the work is not in the
slices but in one shared component, the intake (header reader, stripper,
libvips, SSIM), which upload and reduction both depend on and which a lane
could not own without the others waiting for it. One developer, sequentially.

## 15. Order of work (small commits, green after each)

1. Spike (§ 12) and the pins, with the licence check (§ 11.1) — the pins go
   through the orchestrator (`Directory.Packages.props`).
2. Domain (`Images`, limits, codes), `ServiceUnavailableException`, EF
   configuration, migration, model and inventory tests.
3. Header reader and stripper with their unit tests and the hostile corpus.
4. SSIM, `VipsSetup`, `VipsCodec`, the quality search, `ImageIntake`,
   `DecodeGate` with their tests.
5. `ImagesEndpoints.cs` with all three routes; the upload slice end to end
   (reference use case), the error catalog, the write inventory row.
6. The reduction slice.
7. The read slice, `IWorkItemReadAccess`, headers, parity test.
8. The reducer's image element and the fixtures.
9. `SaveWorkItem`: count, `IImageBinding`, the bind; its tests.
10. The sweeper, `RetentionTests`, the race tests, the purge tests.
11. nginx, `vm.md` + `vm.ru.md`, backend.md's exception row.

## 16. Risks and open points

- **Licences** (§ 11.1): unverified until the dev stage restores the packages;
  a GPL component in the native bundle stops the work for the human.
- **GIF palette exactness** after a nearest-neighbour scale (§ 12): if
  libvips cannot keep the palette, a managed GIF writer is added (plan
  amendment, no ADR change).
- **NFR timings** depend on the SSIM search; measured at the spike, the search
  bounds (`[40, 100]`, 7 encodes) are tuned before the slices are built.
- **Text reuse**: the 600 000 000-pixel animation refusal shares the frames
  text «Анимация слишком большая: не больше 1 000 кадров.» (the design has one
  text for the frame caps); a separate text is a catalog edit if the human
  wants it (api-contract § 4).
- **Repository size**: generated large fixtures keep the repository small;
  checked-in samples are capped at 200 KB each.
