# QA — feat-078-description-images (images in Описание)

## Backend

Plan written 2026-11-18 from spec.md, US-1..US-6 and api-contract.md only, before reading the implementation. US-1 is the editor's; its server side is US-2/US-3. "QA:" marks tests added by this pass.

### Coverage matrix

| Criterion (spec) | Check | Test type | Status |
|-----------------|----------|-----------|--------|
| US-2 valid upload | 2 MB PNG → 201 `{id, format, byteLength, width, height}` only; id is v4 lower-case uuid; row unbound, uploader = caller, item = X | integration | ✅ UploadDescriptionImageEndpointTests |
| US-2 format by content | PNG named .jpg / declared image/jpeg stored as png; JPEG, GIF, WebP kept; SVG-as-png, HTML, HEIC, AVIF, BMP, TIFF, truncated PNG → 400 `format_unsupported` | integration + unit | ✅ ImageHeaderReaderTests.Foreign, Upload_SvgNamedPng, Upload_PngDeclaredJpeg |
| US-2 caps (bytes) | 5 242 880 accepted; 5 242 881 → 400 `too_large`; declared Content-Length over cap refused without reading | integration | ✅ Upload_5242880/5242881; QA: UnsizedBody_OverEachCap |
| US-2 caps (pixels) | 20 001×100 refused; 20 000×3 000 accepted; 8 000×8 000 refused; 40 KB bomb 20 000² refused not decoded; GIF 1 001 frames, GIF 100×3 000² → `animation_too_large`, not decoded | integration + unit | ✅ ImageHeaderReaderTests, Accept_OverCapHeader_CodecNeverCalled |
| US-2 metadata / orientation | 600 KB JPEG: EXIF/GPS/comment gone, orientation flag kept, pixels not re-encoded; 3 MB: rotated pixels, no flag | integration/unit | ✅ ImageStripperTests; QA: Upload_LargeRotatedJpeg_PixelsRotated_NoFlag_NoMetadata |
| US-2 colour profile | P3 JPEG: profile kept or sRGB, SSIM ≥ 0.98 | unit | ❌ D-3 |
| US-2 screenshot exact / large PNG lossless | stored PNG decodes to identical pixels, not larger | unit/integration | ✅ QA: Upload_Screenshot_ExactPixels_NotLarger; Strip_Png_DecodesToTheSamePixels |
| US-2 large photo | 4.8 MB JPEG 4 032×3 024 → same dims, smaller, SSIM ≥ 0.98 | unit | ✅ QA: Upload_LargeJpeg_Reencoded_SimilarityAtLeast098 |
| US-2 small photo untouched / never bigger | ≤ 1 MB JPEG not re-encoded; recompress-bigger → source with metadata removed | unit | ✅ Upload_SmallJpeg_IsStoredStripped; QA: Upload_NeverLargerThanTheSource ×3 |
| US-2 > 16 MP scaled | 8 160×6 120 → ≤ 16 777 216 px, proportions kept | unit | ✅ Upload_PngOverThePixelCap_IsScaledDown (similarity: D-3) |
| US-2 animation kept | 40-frame GIF → 40 frames, same delays | unit | ✅ QA: Upload_AnimatedGif_KeepsFramesAndTiming (40 frames, delays) |
| US-2 who may upload | read-only role (403), «Удалена» (409 deleted), deactivated project, non-reader / missing item (404) — nothing stored; no token 401 | integration | ✅ Upload_Viewer_403, _Stranger_404, _DeletedItem_409, _NoToken_401; QA: Upload_Deactivated_AnswersAsTheDescriptionEdit ×2 |
| US-2 hostile corpus | script-as-image, SVG-as-png, bomb, truncated, metadata script, polyglot: refused or stored stripped | unit/integration | ✅ HostileCorpus_RefusedOrStoredClean, VipsSetup_LoadersBlocked, Upload_PngWithScriptTail |
| US-3 bind on save | two own unbound uploads referenced → stored, both bound to X | integration | ✅ Save_OwnUploads_StoredAndBoundAndReadable |
| US-3 re-save keeps bound | another member's bound image survives a third member's save | integration | ✅ Save_ImageBoundByAnotherMember_KeptByAThirdMembersEdit |
| US-3 foreign reference refuses whole save | Y's image; another member's unbound for X; own unbound for Y; own swept; made-up → 400 `description_image_unavailable`, description unchanged, nothing bound | integration | ✅ Save_ForeignReference_RefusesTheWholeSave_NothingBound ×5 |
| US-3 > 50 images | 51 → 400 `description_too_many_images`; 50 accepted | integration | ✅ Save_FiftyOne…, Save_FiftyElementsOfOneImage_Accepted |
| US-3 image-only not empty | single image stored as image; empty paragraphs → "" | unit + integration | ✅ Save_ImageAlone_StoredAsHtml, Reduce_EmptyParagraphs_NoImage_HasNoContent |
| US-3 visible count | 100 000 chars + 50 images with 255-cp alts accepted; a,img,b = 3 | unit | ✅ Reduce_FiftyImagesWithLongAlt_AddNoVisibleCharacter, visible-count fixtures |
| US-3 canonical cases | shared fixture file image-* cases, fixed point | unit (fixtures) | ✅ canonical.json image-* (16), run by the reducer suite |
| US-3 plain format | `<img …>` stored as characters, binds nothing | integration | ✅ Save_PlainFormat_StoredAsCharacters_BindsNothing |
| US-3 save not touching Описание | upload stays unbound | integration | ✅ Save_WithoutDescription_BindsNothing |
| US-4 who reads | reader 200; «Удалена» item as item read (200); non-reader 404; revoked right 404 next read; no token 401 | integration | ✅ ImageReadParityTests ×3, DeletedItem_ImagesStayReadable, Get_RightRevoked_404; QA: MembershipRemoved… |
| US-4 unbound own only | uploader 200, other editor of X 404 | integration | ✅ Get_Unbound_UploaderOnly |
| US-4 equal answers | non-reader / swept / made-up / malformed / purged → identical status + body except traceId | integration | ✅ Get_EveryRefusal (3 kinds); QA: EveryRefusal_IncludingSweptPurgedAndForeignUnbound_IsTheSame404 (6 kinds) |
| US-4 headers | Content-Type stored format, Content-Length, ETag `"<id>"`, `private, no-cache`, nosniff, CSP `default-src 'none'; sandbox`, CORP same-origin; 304 on If-None-Match only after access passes (non-reader with matching ETag gets 404, not 304) | integration | ✅ Get_ReadOnlyMember_200_BytesAndHeaders, Get_IfNoneMatch_304…AfterTheAccessCheck |
| US-5 2 h window | controlled clock: 1 h 59 kept, ≥ 2 h removed; bound never removed; 10-min unbound kept | integration | ✅ RunOnce_TwoHourLine (119/121 min), RunOnce_RemovesOldUnbound_KeepsYoungAndBound |
| US-5 hourly cadence, ≤ 3 h | sweeper interval 1 h; a run every hour bounds removal to < 3 h | unit/config | ✅ QA: HostedSweep_RunsAtStart_HourlyWithATwoHourLifetime (start run, constants; tick untested) |
| US-5 bind vs sweep race | sweep holding the row lock while save binds (and reverse): only refused+removed or bound+kept; never a stored dangling reference | integration (forced) | ✅ SweepRaceTests ×3, ImageBindingTests; QA: SaveWaitingOnTheSweepsDelete_IsRefused_NothingDangling (through PATCH) |
| US-5 save after sweep | refused as US-3 | integration | ✅ Save_ForeignReference(own-upload-swept), SweepDeletedFirst_BindIsRefused |
| US-5 unreferenced bound kept | later save drops reference; still readable, sweep keeps it | integration | ✅ LockAndBind_AlreadyBound, Purge_RemovesTheItemsImages (unreferenced row) |
| US-5 delete/restore/purge | delete keeps images, restore shows; purge removes all (incl. unreferenced, incl. unbound uploads for X) atomically; failed purge keeps both | integration | ✅ ImageLifecycleTests ×3, cascade migration test (restore: D-3) |
| US-5 log line | one line per run with count, nothing identifying | unit | ✅ RunOnce_LogsOneLine_WithTheCountOnly |
| US-6 reduction | 201 `{image, original}`; result ≤ 5 242 880, same format, metadata stripped, original not stored | integration | ✅ Reduce_BigJpeg_201_ResultAndOriginalFacts_OneRowStored |
| US-6 quality before size | 12 MB JPEG 4 000×3 000 keeps dims if SSIM ≥ 0.95 fits; else never below 2 000×1 500 | unit | ✅ Reduction_OfALargePhoto_FitsTheCap; QA: Reduction_Jpeg_QualityFloor095_SideFloorHalf |
| US-6 PNG lossless scaled | ≥ half dims, exact pixels of scaled source | unit | ✅ Reduction_LargePng_StaysPng_Scaled_NotBelowHalf (pixel equality: D-3) |
| US-6 cannot reduce | 400 `cannot_reduce`, nothing stored | unit + stub integration | ✅ Reduction_NoisePng_CannotReduce, Reduce_IntakeCannotReduce (stub) |
| US-6 server holds the line | 6 MB on ordinary route → `too_large`; 20 971 521 on reductions → `reduction.too_large`; 20 971 520 accepted for reading; animated → `reduction.animated`; ≤ 5 MB body answered in reduction shape | integration | ✅ Upload_6MbOrdinary, Reduce_20971521Bytes, Reduce_AnimatedOver5Mb, Reduce_SmallPng; QA: Reduce_Exactly20971520Bytes_IsTakenIn |
| NFR speed | 12 MP 5 MB upload ≤ 5 s; 48 MP 20 MB reduction ≤ 15 s; read ≤ 1 s; item GET with 50 images no bytes | integration (budget) | ⚠️ ImageVolumeTests assert 2× the target (D-1); read 1 s untested |
| NFR decode bound | parallel uploads beyond the gate → 503 `images.busy` + `Retry-After: 5` | integration/unit | ✅ Reduce_WhilePermitsAndQueueAreExhausted_503Busy_RetryAfter, Gate_RefusesWhenTheQueueIsFull (bodies not bounded: D-2) |
| Migration | AddImageStore applies on a clean DB, `images` schema, FK cascade to work_items; down/up idempotent | integration | ✅ AddImageStoreMigrationTests (up, down, up; no pending changes; cross-schema cascade; checks), clean Testcontainers DB |

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|----------|----------|--------|
| A-1 | Empty body (0 bytes) on upload and reduction | 400 format, nothing stored, no 500 | ✅ QA: EmptyBody_IsAFormatRefusal_OnBothRoutes |
| A-2 | Chunked body with no Content-Length over 5 MB | 400 too_large after reading at most cap+1 | ✅ QA: UnsizedBody_OverEachCap_IsRefusedForSize_NothingStored |
| A-3 | Order of answers: read-only role + oversized body | 403 before 400 size (body not read) | ✅ QA: OrderOfAnswers_RightAndDeletedState_BeforeTheBody |
| A-4 | Upload for a «Удалена» item with a bad body | 409 deleted, not 400 | ✅ same test |
| A-5 | GET /api/images/{id} with uppercase or braced uuid, non-uuid, very long id | 404 identical body | ✅ malformed and braced ids 404 (QA parity test); an upper-case canonical id is read as the same image (Guid "D" parse) |
| A-6 | Same image id referenced twice in one description | accepted (counts 2 toward 50) or dedup — no double-bind error | ✅ counted twice, bound once (Save_FiftyElementsOfOneImage_Accepted; LockAndBind distincts the ids) |
| A-7 | Save referencing an image bound to X while X's other user's unbound upload is also present | only the foreign unbound one refuses | ✅ by code: one Unavailable verdict refuses the whole bind (LockAndBind_OneUnavailable_UpdatesNothing) |
| A-8 | Image of another organization's item (different org, same user not member) | 404 equal | ✅ Stranger token in the parity tests (no membership in the item's organization) |
| A-9 | Description write of exactly 50 images where 49 already bound + 1 new | accepted | ◐ by code: verdict Bound or Bindable per id, count over elements; no dedicated test |
| A-10 | Two concurrent saves of X binding the same own upload | both succeed or one conflicts; image bound once | ◐ by code: the item lock serialises them; no test |
| A-11 | Concurrent upload while X is purged | 404 or row cascaded; no orphan image row | ◐ by code: insert under FOR SHARE on the item, purge cascades by FK; no test |
| A-12 | 304 path for a bound image after the right was revoked | 404, never 304 | ✅ QA: MembershipRemoved_NextRead404_AndNo304 |
| A-13 | Kazakh texts of the 11 codes present and flagged kkNeedsReview | catalog test | ✅ catalog tests; read by QA: 11 codes, ru as the contract, kk present, kkNeedsReview |
| A-14 | Reduction route: still JPEG exactly 5 242 880 | answered as plain upload, original = image dims | ✅ Reduce_SmallPng_201_ProcessedAsAnOrdinaryUpload |

### Runs

All on Testcontainers PostgreSQL; never the `mustask` DB on 5432 or the stand. The developer's green
runs (Application 881, Contracts 239, Architecture 84, Api.IntegrationTests images/inventory/workitems
1091/1092) were not repeated.

| Date | What was run | Result |
|------|-----------------|-----------|
| 2026-11-18 | `dotnet build Mustask.sln` | 0 warnings, 0 errors |
| 2026-11-18 | `dotnet test Mustask.Domain.Tests --filter ~.Images.` | 15/15 |
| 2026-11-18 | `dotnet test Mustask.Infrastructure.Tests --filter ~.Images.` (not in the dev's report; includes `AddImageStoreMigrationTests`: up → down → up on a clean database, no pending model changes, cross-schema cascade) and the 9 QA tests | 77/77 |
| 2026-11-18 | `dotnet test Mustask.Api.IntegrationTests --filter ~ImagesQaTests` (10 QA tests) | 10/10 |
| 2026-11-18 | `dotnet test Mustask.Infrastructure.Tests --filter ~.Images.\|~.RichText.` | 2566/2581: 15 failures, all wall-clock budgets of the rich-text reducer (ReducerBoundCostTests, ReducerRulesTests, RichTextInputGuardTests); no image test failed |
| 2026-11-18 | Hypothesis "the branch made the reducer timing tests flaky": the three timing classes alone on this branch, then the same classes at the merge base `6f92b217` (temp worktree, removed) | branch 7/386 failed, base 7/370 failed, the same tests (HostileDeepNesting "table", SpanBomb, WorstCaseUnderTheBounds cases, ManyFosterTokens): **not caused by this branch**; the local budget is 1× (10× under `CI`) on a shared machine |

`Save_BurstOfLargeDocuments` was not re-run: an image-free save does no image work (`ImageIds` empty, bind skipped), so the load explanation holds for it too.

### Defects

#### D-1: the speed NFR is not verified; ImageVolumeTests assert twice the target under names that claim it

- Severity: minor
- Steps: read `ImageVolumeTests` — `Upload_5MbJpeg12Mp_StoredWithin5Seconds` asserts `< 10 s`,
  `Reduce_20MbJpeg48Mp_ReducedWithin15Seconds` asserts `< 30 s` (`Headroom = 2`). No test covers the
  1 s image read. The developer's own best upload was 4.9 s against 5 s on a developer machine; the
  spec's targets are "on the stand", not observed.
- Expected / Actual: the target shown by a stand observation (US-2/US-6 Verification); actual: a 9 s
  upload is green. Honest as a regression guard on a loaded CI, not as proof of the NFR.
- Status: open, carried to gate-3 as debt: rename to `…WithinTwiceTheTarget` (or similar) and record a
  stand timing of the 5 MB / 12 MP upload, the 20 MB / 48 MP reduction and a 5 MB read before the merge.

#### D-2: request bodies are buffered at the full cap before the decode gate

- Severity: minor
- Steps: `HttpUploadBody.ReadAsync` rents `maxBytes + 1` (5 MB on the upload, 20 MB on the reduction)
  for every request that passes the address checks, whatever its `Content-Length`, and fills it before
  `ImageIntake` enters the `DecodeGate`. Send 50 concurrent `POST …/images/reductions` with slow 20 MB
  bodies as one editor: about 1 GB is held, while the gate admits 2 and refuses the rest with 503 only
  after their bodies are read.
- Expected / Actual: NFR "parallel ones at the caps cannot exhaust the server's memory": decoding
  is bounded, the bodies are not (R-14: no rate limits). A 40 KB upload also holds a 5 MB rental.
- Status: open, debt: rent `min(Content-Length, cap) + 1` when the length is declared, and take a
  gate (or a separate body semaphore) before buffering a reduction body.

#### D-3: three processing criteria are not measured by any test

- Severity: minor
- Steps: search the tests for the US-2 "Colours are kept" scenario (Display P3 JPEG, profile kept or
  sRGB, SSIM ≥ 0.98 after rendering to sRGB), the over-16 MP similarity after scaling, and US-6
  "A screenshot stays lossless" (result decodes to exactly the source scaled to its dimensions); none
  exists. US-5 "after X is restored its Описание shows them again" is covered only by the read in
  «Удалена» (restore is a state change, so the risk is low).
- Expected / Actual: US-2/US-6 Verification says the similarity and pixel equality are measured by the
  test. QA added the SSIM, rotation, exact-screenshot, never-larger, frame-timing and 0.95-floor
  measurements (`ImageIntakeQaTests`); these three remain.
- Status: open, debt (a colour-profile fixture needs an embedded ICC profile; NetVips can write one).

No blocker, no major. The forced bind/sweep race holds; the
non-disclosure 404 is byte-identical across six refusal kinds; the migration goes up, down and up.
The Kazakh texts of the 11 new codes were read: complete, consistent with the Russian, flagged
`kkNeedsReview` like every other Kazakh entry in `contracts/errors/`; a native review is
project-wide, not a defect here.

### Verdict

blockers: 0 · majors: 0 · minors: 3 (D-1..D-3, debt for gate-3) · result: pass
