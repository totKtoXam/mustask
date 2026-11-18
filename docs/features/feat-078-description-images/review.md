# Review: feat-078-description-images

## Round 1 — requirements

Reviewer: requirements-reviewer (clean context), 2026-11-16. Inputs: spec.md,
stories US-1..US-5, status.md Notes; cross-checked against feat-018 spec
(BR-RT-*), feat-059 spec (BR-FB-6, BR-FB-20 (a)-(e)), feat-059 design.md
(S2, S2a, S3a) and ADR-0041 decision 6 (feat-059 worktree, unmerged), mod-005
BR-DR-12, -15, -16.

### Coverage: inputs and rules → stories

| Input / rule | Covered by | Note |
|---|---|---|
| Human 2026-11-12: bytes in the DB, own schema (D-1) | BR-IM-17 (ADR), US-2, US-5; NFR Storage | design states volume and backup effect |
| Human 2026-11-12: large images compressed without visible loss (D-2) | BR-IM-4, US-2 | made measurable in R-6; PNG gap in R-12 |
| Formats and size cap (open) | BR-IM-2, -3; OQ-1, OQ-2 | for the human |
| feat-018 BR-RT-4 replaced, BR-RT-6 amended | BR-IM-9, -13, -18; US-1, US-3 | |
| feat-018 BR-RT-1, -3, -5, -8, -12 | BR-IM-7, -9, -12, -18; US-3 canonical cases | BR-RT-5 added to BR-IM-18 (R-11) |
| feat-059 BR-FB-20 (a) non-member pre-item upload | feat-059 (BR-IM-19, D-4); feat-078 ships the purpose slot | seam completed in R-1 |
| feat-059 BR-FB-20 (b) nothing in addresses | BR-IM-10; US-2, US-3, US-4 | |
| feat-059 BR-FB-20 (c) sender reads/adds through the feedback operation | feat-059; BR-IM-11 allows a purpose's own reader (R-1) | |
| feat-059 BR-FB-20 (d) unbound removed after 2 h, hourly | BR-IM-16 (D-3), US-5 "every purpose" | timing fixed in R-2 |
| feat-059 BR-FB-20 (e), BR-FB-6 image-only description | BR-IM-9, US-3 | |
| feat-059 BR-FB-13 purge leaves nothing | BR-IM-16 purge step, US-5 | |
| ADR-0041 decision 6 (c) stores through feat-078's storage | BR-IM-6, -19 | |
| mod-005 BR-DR-12, -16 purge | BR-IM-16, -18; US-5 | |
| Who uploads / who reads | BR-IM-5, -11; US-2, US-4 | |
| Crop (feat-018 D-5) | spun out, OQ-6 | for the human |

D-4 split check: feat-078 opens no write right nobody uses — the only upload
operation it ships is for an editor of an existing item; the purpose slot is a
data field, not a right. As first written, though, BR-IM-6, -8 and -11 were
closed rules that would have forbidden what feat-059 must build (R-1).

### R-1
Severity: major
Affects: BR-IM-6, BR-IM-8, BR-IM-11, BR-IM-19, US-1; feat-059 BR-FB-20 (c), (e), design S3a
Problem: The seam left to feat-059 was incomplete. (1) The editor took only its upload operation from the host, but feat-059's edit dialog shows the report's images to a non-member sender, who cannot use this task's read (BR-IM-11): the editor must also take the reference-to-address rule from the host, which in turn requires the stored markup to hold an identifier, not a route-bound address. (2) BR-IM-6 said an upload is bound "only by a description write of that same user to the item it was made for" and BR-IM-11 said "exactly those who may read the item": both, read literally, forbid feat-059's binding of a feedback upload made for no item and its sender's read. (3) BR-IM-8 would refuse feat-059's send, whose description references uploads made for no item.
Required resolution: scope BR-IM-6 and BR-IM-8 to the item-description purpose and this task's writes; let a purpose's own operation bind, check and read; make the editor take both upload and address from the host; state the stored reference is an identifier.
Status: resolved
Resolution: BR-IM-6 reworded per purpose; BR-IM-7 states the reference is an identifier and the address is derived by the showing surface; BR-IM-11 scoped to "this task's image read" with a purpose's own reader allowed; BR-IM-19 gains the address seam and the per-purpose reference check; US-1 gains the host-seam scenario.

### R-2
Severity: major
Affects: US-5 "An upload nobody saves", "A save binds before the sweep"; BR-IM-16
Problem: US-5 removed an upload made at 10:05 by the 12:00 sweep, 1 h 55 min after the upload, contradicting BR-IM-16 "never earlier" than 2 hours. Its "save binds before the sweep" scenario was vacuous for the same reason (U was not eligible at 12:00 anyway).
Required resolution: align the scenario times with BR-IM-16.
Status: resolved
Resolution: kept at 11:00 and 12:00, removed at 13:00 (2 h 55 min); the binding scenario moved to a save at 12:50 and the 13:00 sweep.

### R-3
Severity: major
Affects: BR-IM-8, BR-IM-16, US-3, US-5
Problem: A save validates its references and the sweep removes uploads concurrently; nothing forbade the interleaving "save checks U valid → sweep removes U → save stores a reference to U", which leaves a permanently broken image no test would catch.
Required resolution: state the two allowed outcomes and test the race.
Status: resolved
Resolution: BR-IM-16 "a bind and the sweep never interleave"; US-5 scenario "A save and the sweep at the same moment" and a forced-race integration test row.

### R-4
Severity: major
Affects: US-2 "The caps"; BR-IM-3
Problem: The "bomb" row (20 000 × 3 000 = exactly 60 000 000 pixels, exactly 20 000 px on a side) expected a refusal, but BR-IM-3 says "at most", so it must be accepted; the test as written would force an off-by-one cap.
Required resolution: make the bomb exceed the cap and add the exact-boundary accept.
Status: resolved
Resolution: boundary 20 000 × 3 000 accepted; the bomb is 20 000 × 20 000.

### R-5
Severity: major
Affects: BR-IM-3, BR-IM-4 (6), OQ-2, OQ-7, NFR Safety
Problem: An animated GIF/WebP is capped only per frame; 10 MB of compressed frames can declare hundreds of 60 MP frames, a decompression bomb that keeping animations (OQ-7) makes reachable, contradicting "none decoded past the caps".
Required resolution: a cap on frames and on total pixels over all frames, checked from the header.
Status: resolved
Resolution: BR-IM-3 adds at most 1 000 frames and 600 000 000 pixels over all frames; US-2 gains two refusal rows; US-1 a notice «Анимация слишком большая: не больше 1 000 кадров.»; OQ-2 names the numbers for the human.

### R-6
Severity: major
Affects: BR-IM-4, US-2
Problem: "Without visible loss" was not fully testable and partly self-contradictory. (a) SSIM ≥ 0.98 named no reference picture (the upload, or the upload after orientation and scaling?) and no method, so two correct implementations could disagree. (b) "Metadata removed" would strip an embedded colour profile, so an iPhone Display P3 photo changes colour, while "colours look the same" was not checkable. (c) A JPEG ≤ 1 MB with an orientation flag "is not re-encoded" yet must have the orientation "applied" (rotated pixels, no flag): rotating pixels is a re-encode.
Required resolution: define the SSIM reference and method; say what happens to a colour profile; let a non-re-encoded image keep its orientation field.
Status: resolved
Resolution: BR-IM-4 (1) keeps the orientation field when pixels are not re-encoded and keeps the colour profile or converts to sRGB; (3) and (4) measure against the picture after (1)-(2), SSIM on luminance, 8×8 windows; US-2 gains an orientation outline (600 KB and 3 MB), a Display P3 scenario, a similarity check on the scaled case and a large-PNG scenario.

### R-7
Severity: minor
Affects: BR-IM-7, US-3 canonical cases
Problem: BR-IM-7 said an image inside a paragraph is "moved out to stand right after that block", while the canonical case `<p>a<img>b</p>` splits the paragraph at the image; BR-IM-12 ("stands where it was put") agrees with the split.
Required resolution: one rule.
Status: resolved
Resolution: BR-IM-7: a paragraph is split at the image (no empty half kept); a heading is followed by the image; a link is left around the rest of its text. Two canonical cases added (leading image, paragraph inside a list item).

### R-8
Severity: minor
Affects: BR-IM-13, US-1
Problem: Nothing said where an insert lands with the cursor in a heading, a table cell or a code block; the server drops an image in a cell or code block, so the member would watch an upload succeed and the image vanish on save.
Required resolution: a client placement rule matching BR-IM-7.
Status: resolved
Resolution: BR-IM-13 "Where an insert lands"; US-1 outline with the three places.

### R-9
Severity: minor
Affects: BR-IM-11, NFR Safety, US-4
Problem: "Read live, a revoked right stops it" cannot hold if image answers are cacheable by a shared proxy; content sniffing was not ruled out.
Required resolution: no shared caching, declared type, no sniffing; be explicit that the reader's own browser cache may keep what it already showed.
Status: resolved
Resolution: BR-IM-11 and NFR Safety amended; US-4 scenario "No shared cache keeps an image" with a header test.

### R-10
Severity: minor
Affects: BR-IM-13, US-1
Problem: Pasting an own-store image the writer cannot read (another item, revoked, purged) had no defined outcome.
Required resolution: define it.
Status: resolved
Resolution: left out with the upload-failed notice, the rest pasted; US-1 scenario added.

### R-11
Severity: minor
Affects: BR-IM-18
Problem: An image block inside a list item changes feat-018 BR-RT-5's canonical structure ("list items hold only paragraphs and nested lists"), which BR-IM-18 did not list.
Required resolution: list the amendment.
Status: resolved
Resolution: BR-IM-18 names BR-RT-5.

### R-12
Severity: minor
Affects: OQ-3, OQ-4, D-2
Problem: The open questions understated their consequences. OQ-3: the human asked that large images be compressed (D-2), but a large photographic PNG is only re-compressed losslessly and may stay near 10 MB. OQ-4: keeping a removed image costs more than storage — a screenshot removed because it showed a secret cannot be erased short of purging the item; and the alternative breaks feat-059's frozen "as submitted" description unless it counts as a reference.
Required resolution: state the consequences in the questions, the recommendations unchanged.
Status: resolved
Resolution: OQ-3 and OQ-4 reworded; BR-IM-4 (3) permits lossless re-compression; US-2 "A large PNG is never encoded lossily".

### R-13
Severity: minor
Affects: NFR Safety
Problem: Per-file caps bound one decode, not many parallel ones; ten parallel 60 MP uploads can exhaust memory.
Required resolution: the design bounds concurrent decodes.
Status: resolved
Resolution: NFR Safety sentence added.

### R-14
Severity: minor
Affects: § Out of scope (quotas, rate limits)
Problem: With no per-user quota or rate limit, any editor can store 10 MB uploads without bound for the 2-3 hours before the sweep, and bound images without bound (OQ-4 keeps them). Internal users only, and the out-of-scope line says so.
Required resolution: none in the spec; the human should see it.
Status: accepted-risk
Resolution: kept out of scope; mentioned in the gate-1 summary.

### R-15
Severity: minor
Affects: BR-IM-7, US-3
Problem: "255 characters" for the text alternative named no unit, where feat-018 counts code points and the legacy rule UTF-16 units.
Required resolution: name the unit.
Status: resolved
Resolution: 255 code points, counted as BR-RT-8.

### Checked, no finding

- Size: the task is one L (ADR, new storage slice), the crop line is S; nothing in the decomposition exceeds its size.
- Open questions OQ-1..OQ-7: each has a recommendation, and the spec's rules are written to it (formats BR-IM-2, caps BR-IM-3, processing BR-IM-4, kept-on-removal BR-IM-16, 2 h for item uploads D-3/BR-IM-16, crop out of scope, animation BR-IM-4 (6)).
- Permissions per step: upload (BR-IM-5, US-2 outline), save binding (BR-IM-8, US-3 outline), read (BR-IM-11, US-4 outline), «Удалена» / deactivated / purge (US-2, US-4, US-5), anonymous 401.
- Concurrent saves by two editors: the later save may drop the earlier one's image from the text; under OQ-4 it stays bound and nothing breaks.
- The reference tracker is not named.

### Verdict

needs-human-decision: OQ-1..OQ-7 are product choices for the human at gate-1.

## Round 1 — code (general review and L security pass)

Reviewer: code-reviewer (clean context, not the author), 2026-11-18. One session ran the general
review and the security pass together (economy mode). Tree: `cc81ff9e`. The diff is against the
merge base with `origin/main`, `6f92b217`, and has 172 files; the local `main` is stale and gives
619. Inputs: status.md, spec.md, design.md, api-contract.md, plan-backend.md, plan-frontend.md, qa.md
(D-1..D-14), ADR-0045, ADR-0039. The test suite was not re-run. One libvips probe was run outside the
repository (F-1).

### F-1 (blocker, security): a GIF frame larger than its screen bypasses the pixel caps and is decoded

- Where: `src/backend/Mustask.Infrastructure/Images/Intake/Headers/GifHeaderReader.cs:79-103`. The
  reader checks only the logical screen against `CheckCanvas`, and the frame sizes against
  `CheckFrames` (600 MP). A frame's left, top, width and height are never bounded by the screen, or by
  60 MP and 20 000 px.
- Proof: a 174 KB GIF with a 1×1 screen and one 16 000×16 000 frame (256 MP) passes every header check
  (the canvas is 1×1; one frame, 256 M ≤ 600 M). libvips 8.18.7 `GifloadBuffer` (nsgif) grows the
  canvas to the frame. It loaded 16 000×16 000, and `CopyMemory` took 2.9 s and 1.7 GB of working set.
  An 8 000×8 000 frame from a 44 KB file was also decoded in full.
- Reach, reduction route: a stripped GIF over 5 MB is `needsWork` (`ImageIntake.cs:39`). Padding the
  LZW data is enough to get there. `VipsCodec.Decode` scales by the header's 1×1, so it never scales,
  and `CopyMemory` takes the whole frame (`VipsCodec.cs:40`, `:57`).
- Cost: a frame can be up to 600 MP. That is about 1.8 GB RGB plus nsgif's 2.4 GB frame buffer per
  permit, with two permits. Any editor can OOM the API.
- Reach, upload route: a file within 5 MB is stored without decoding, and every reader's browser then
  decodes the frame.
- Breaks:
  - BR-IM-3 (c);
  - spec NFR Safety, "none decoded past the BR-IM-3 caps" (`spec.md:134`) and "cannot exhaust the
    server's memory" (`:138-139`);
  - ADR-0045 decision 2, "the decode cap that does not depend on a library's honesty" (`0045-…md:57`).
- Fix:
  1. In the GIF reader, refuse a frame whose `left + width` or `top + height` goes beyond the screen
     (a format refusal).
  2. As defence in depth, refuse in `VipsCodec.Decode` before `CopyMemory` when `loaded` is not the
     size the header predicts (after shrink-on-load).
  3. Apply the same rectangle check to WebP `ANMF` offsets and APNG `fcTL` against the canvas. Those
     files are stored without decoding, but browsers decode them.
  4. Add hostile-corpus cases (screen 1×1, frame 8 000×8 000) at the header reader and through the
     reduction API.

### F-2 (minor, security): D-2, request bodies are buffered at the full cap before the gate

- Where: `src/backend/Mustask.Api/Features/Images/HttpUploadBody.cs:27` rents `maxBytes + 1` whatever
  the `Content-Length` says. ArrayPool rounds that up to 8 MB on the upload route and 32 MB on the
  reduction route.
- Why it does not block:
  - nginx buffers the whole body before proxying. `proxy_request_buffering` is on by default, and
    `deploy/vm/*.conf` leaves it on. Kestrel therefore reads every body at loopback speed.
  - The rental goes back in milliseconds unless the request enters the gate.
  - A slow client holds nginx's temp file, not API memory.
  - The attacker sends about as many bytes as the memory they hold. There is no amplification, unlike
    F-1.
- When it would become a major: if Kestrel is ever exposed directly, or request buffering is turned
  off.
- The NFR sentence at `spec.md:138` stays unanchored for bodies until this is fixed.
- Fix: as QA proposes, rent `min(Content-Length, cap) + 1` when the length is declared, and cap how many
  reduction bodies are held at once. This is cheap and sits next to F-1, so fold it into F-1's round.
  Add one line to vm.md § 7: request buffering must stay on in front of the images routes.

### F-3 (minor, prose): ADR-0045 describes the read and the limiter differently from the code

- `docs/adr/0045-…md:49` says the read goes "with sequential access into a pooled buffer of the known
  length". In fact `ImageStore.cs:23` materialises a fresh `byte[]` through EF, up to 5 MB on the LOH
  per read. The other half of the sentence holds: the connection is released before the write.
- `:81` names `System.Threading.RateLimiting`. `DecodeGate` is a `SemaphoreSlim`. The behaviour is the
  same and the Log records the change, but the ADR does not.
- `:82` says the limiter's memory arithmetic holds. F-1 disproves that.
- Fix: correct or weaken the three sentences. The wording of `:82` depends on F-1's fix.

### F-4 (minor, prose): "a refused caller costs no transfer"

- `src/backend/Mustask.Application/Abstractions/Images/IUploadBody.cs:5`. Behind nginx, the whole body
  is transferred and buffered before the API answers 403 or 404. What holds is that the API reads no
  byte of it.
- Fix: say exactly that.

### F-5 (minor, process): BR-IM-18's amendment notes are still owed

- Only ADR-0039 carries its note (`0039-…md:9-12`).
- No note mentions feat-078 in:
  - feat-018's `spec.md` (BR-RT-4, `:87`) or its `status.md`;
  - mod-005's spec (BR-DR-12, -16);
  - doc-002 BR-M3-4.
- BR-RT-4 limits itself ("until it merges"), so nobody acts wrongly today.
- Fix: the orchestrator applies the notes as BR-IM-18 says, at or before the merge.

### Security pass (the L checklist and the brief): no further finding

- **Authorization.**
  - Routes 1-2: `RequiresOrganizationPermission(WorkItemsWorkItemWrite, Project)`. The address, type
    key and «Удалена» are checked before the body is read, and again under `FOR SHARE` at the insert
    (`DescriptionImageIntakeFlow.cs:37-41`, `:73-78`).
  - Route 3: `AuthenticatedOnly`, with the item's read rule re-derived live
    (`WorkItemReadAccess.cs:20-63`) and pinned by `ImageReadParityTests`.
- **One 404.** A malformed id, an unknown id, a foreign unbound upload and a refused bound image all
  throw the same `NotFound()` (`GetImageHandler.cs:28-42`). QA proved the bodies are byte-identical.
  - The query count differs: 1 for an unknown id, up to 5 for a refused bound one.
  - That makes an existence oracle only for ids the caller already holds, and ids are random v4.
    Accepted.
- **304 after the access check** (`GetImageHandler.cs:35-49`). After a membership is revoked, a
  revalidation gets 404, never 304 (a QA test).
- **Headers** are as the contract says (`ImageResults.cs:20-24`). The JSON 404 needs none of them.
- **Decoder hardening.**
  - Every libvips loader is blocked except the four buffer loaders (`VipsSetup.cs:51-56`). Loaders are
    called by name with `fail-on: error`, and the caches are off.
  - The PNG, JPEG and WebP walks are bounded: lengths are checked, chunks are capped, and nothing is
    allocated by a size the file declares. EXIF reads IFD0 only.
  - GIF is the gap (F-1).
  - The gate bounds the wait, not the decode, so two long reductions can turn other uploads into 503.
    That is by design (R-14, no quotas).
- **Metadata stripping** works at the container level, and the stored form is read again by the header
  reader (`ImageIntake.cs:122-128`). QA covered GPS, comments and rotated pixels.
- **PATCH reference check.**
  - Rows are locked `FOR UPDATE` in id order, after the item's lock (`ImageBinding.cs:25-28`).
  - `Image.CheckBinding` requires the same purpose and item, and for an unbound upload the writer's
    own. So foreign, cross-item, cross-org, swept and deleted ids all get the one field reason.
  - The 50 cap is checked before any lock.
- **Sweep against bind.** Batches use `SKIP LOCKED`. The leftovers are deleted one per statement with
  `bound_at IS NULL` re-checked (`UnboundImageSweeper.cs:97`, `:112`). A bind that loses the race finds
  the row missing and is refused (`ImageBinding.cs:28`). QA forced this race through PATCH.
- **Alt text and canonical html.**
  - The server drops `<` and `>`, writes CR as LF, escapes `&`, `"` and NBSP, and cuts to 255 code
    points (`CanonicalWriter.cs:121`, `:350`; `DomConverter` `CutAlt`).
  - The client mirrors this (`imageAlt.ts`, `normalise.ts`), and the shared fixtures run both.
  - Both sides check that the id is a UUID.
- **Object URLs.** One per reference, revoked on dispose or on a late arrival (`runtime.ts:61`, `:78`).
  The pasted copy is released in `finally` (`controller.ts` `fetchPasted`).
- **Bearer fetch.** Only `/api/images/{uuid}` is fetched. A foreign `src` is never fetched
  (`paste.ts:46-58`), and requests use `credentials: 'omit'`.

### QA debts weighed

- **D-2:** minor, as F-2. It does not block. Fold the cheap half into F-1.
- **D-1:** does not block, because the targets are "on the stand" and CI cannot show that.
  - The developer machine measured 4.9 s against the 5 s upload target (best run), so the upload sits
    at its edge, and 9.1 s against the 15 s reduction target.
  - Owe a stand measurement after deploy. Rename the tests that assert twice the target so the name
    says "2× budget".
- **D-3:** does not block. The colour-profile SSIM case is the one worth a test.
- **D-8, D-9, D-13, D-14:** do not block. D-14 is the one a member would notice: a dropped image comes
  back in a later paste (`paste.ts:128`). The fix is cheap: clear `pending` on a drop.

### Behavioural claims checked

- api-contract § 1, "a declared Content-Length above it is refused without reading" → `HttpUploadBody.cs:22-25`
- api-contract § 1, order of answers → `DescriptionImageIntakeFlow.cs:37-78`
- api-contract § 3, headers → `ImageResults.cs:20-24`; 304 after access → `GetImageHandler.cs:35-49`; identical 404 → `:28-42` and QA's byte-identical test
- api-contract § 4.1, stored form and alt → `CanonicalWriter.cs:117-125`, `:350-358`; fixtures `image-*` in `canonical.json`
- api-contract § 4.2, references, the 50 cap, the bind in the same transaction → `SaveWorkItemHandler.cs:233-238`, `ImageBinding.cs`
- spec BR-IM-3 (c), "checked from the header before decoding, on every path" → F-1 (false for GIF)
- spec NFR Safety, "none decoded past the caps" and "cannot exhaust memory" → F-1, F-2
- spec BR-IM-16, "bind and sweep never interleave" and "at most 3 h" → `UnboundImageSweeper.cs:90-113`, `ImageBinding.cs:25-28`
- ADR-0045 decision 2, "the decode cap does not depend on the library" → F-1
- ADR-0045, the read's pooled buffer, the limiter type and the arithmetic → F-3
- `IUploadBody.cs:5`, "costs no transfer" → F-4
- `VipsSetup` summary, "every loader blocked but four" → `VipsSetup.cs:51-56`
- `UnboundImageSweeper` remarks, "batches never wait" → `:97`
- backend.md, the 503 row with Retry-After → `BusinessExceptionHandler.cs:126-129`
- vm.md and vm.ru.md, `client_max_body_size 21m` → both `deploy/vm/nginx-mustask*.conf`
- the ADR-0039 amendment note → present; the other BR-IM-18 notes → F-5
- Direction 2, three changed behaviours swept by identifier:
  1. Images are kept, not dropped. Grepped `BR-RT-4` and «Изображения в описание не вставляются»
     in `docs/`, `.claude/`, `src/frontend/src` and `contracts/`. Hits:
     - feat-018's spec, design, stories and status (BR-RT-4 limits itself, see F-5);
     - ADR-0039, which has its note;
     - `copy.ts:251` and `paste.ts:129`, still used for editors without a host, which is correct.
  2. The body limit. Grepped `client_max_body_size`: only the files this diff updates.
  3. The new 503. Grepped `ServiceUnavailableException`: only backend.md, which this diff updates.
  - Not swept: the AGENTS.md files, for the NetVips native dependency.

### Verdict

needs-fixes: 1 blocker (F-1), 0 majors, 4 minors (F-2..F-5).

- F-1 goes back to the backend developer.
- The next round is a delta over F-1's fix, with backend QA over the intake only. F-2's cheap half and
  D-14 may ride along.
- The minors and QA debts D-1, D-3, D-8, D-9, D-13 and D-14 go to gate-3 under "deliberately left open".
