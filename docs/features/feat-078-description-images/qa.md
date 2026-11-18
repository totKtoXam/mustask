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

## Frontend

Plan written 2026-11-18 from spec.md, US-1, US-3, US-4, US-6, design.md, api-contract.md and
`docs/error-codes.md` only, before reading the implementation or plan-frontend.md. US-2 and US-5
have no screen; their client half is the refusals and the stored form. E2E: no (standing answer,
paused since 2026-10-12; the human's route answer for this task was also NO), so every check below
is jsdom/Vitest; the real clipboard, a real desktop drop, rendering, scaling, dark theme and screen
readers stay manual QA as US-1/US-4/US-6 Verification say.

### Test plan

| # | Criterion (story / design) | Check |
|---|---|---|
| F-1 | US-1 toolbar control | «Изображение» `aria-label`, tooltip «Вставить изображение», last control of group 5 after «Эмодзи»; hidden `input type=file multiple` with `accept` png/jpeg/gif/webp; in the roving tabindex |
| F-2 | US-1 several files by the chooser | three JPEGs → three blocks in the chosen order even when uploads finish out of order; alt = name without extension; focus back in the body |
| F-3 | US-1 paste of a clipboard image | placeholder «Загрузка изображения…» (`aria-busy`) at the caret → image; alt «Изображение» without a name; form dirty |
| F-4 | US-1 / BR-IM-13 insert place | caret in a heading, table cell, code block → after the block; in a paragraph → split, no empty half; list item → block in the item |
| F-5 | US-1 drop | inserted at the drop position, not the caret; default prevented (no navigation); dragging text shows nothing new |
| F-6 | US-1 client refusals | PDF, SVG → format text; 20 971 521 B → 20 MB text; 20 971 520 B still → offer (boundary); 5 242 880 B → plain upload, 5 242 881 B → offer; 6 MB animated GIF → animation text; 51st image → 50 text; nothing uploaded in each case |
| F-7 | US-1 mixed drop | PNG + PDF + WebP → two inserted, the format text once; several kinds → one line per kind |
| F-8 | US-1 server refusal / network | placeholder removed; a coded refusal announces the code's text (`docs/error-codes.md`); network or 5xx → «Не удалось загрузить изображение. Попробуйте ещё раз.»; rest unchanged |
| F-9 | US-1 / BR-IM-14 save waits | helper «Дожидаемся загрузки изображений…»; the description is sent only after the upload ends, with the image on success and without a trace on failure; a second save while waiting does not send twice |
| F-10 | US-1 rich paste | https image dropped with the other-site text and no request to that site; data-address image uploaded as a file; own-store image re-uploaded as this item's upload; unreadable own-store image left out with the upload-failed text; text kept |
| F-11 | US-1 move inside the description | cut + paste of an own image keeps the reference, no upload |
| F-12 | US-1 / BR-IM-15 remove + undo | Delete, Backspace, «Удалить изображение» remove; Ctrl+Z restores |
| F-13 | US-1 / BR-IM-19 host seam | upload goes through the host's operation, `img` loads from the host's address rule; the editor module names no image route |
| F-14 | US-1 no right / no host | read-only or «Удалена»: no control, paste/drop insert nothing, drop does not navigate; no `images` host: no control, silent ignore (design D-8) |
| F-15 | design D-5 placeholder removal | deleting a placeholder aborts its upload; a late answer is ignored |
| F-16 | design announcements | «Изображение вставлено.», «Изображение удалено.», keyboard-selection hint once on the transition |
| F-17 | design toolbar fit | 27 controls; at 640 px two rows or group 5 into «Ещё», «Изображение» reachable there by the same name |
| F-18 | design saving state | control disabled while the card saves; images stay openable |
| F-19 | US-3 canonical form, client side | shared fixture image-* cases at a fixed point in the client normaliser; alt drops `<` `>`, CR → LF (image-alt-angles), cut at 255 code points; foreign sources dropped |
| F-20 | US-3 round trip | a stored description with images loads and serialises byte-equal, form not dirty |
| F-21 | US-3 / BR-IM-9 image alone | an image-only description is content (not ""), the empty-description rule unchanged |
| F-22 | US-4 read-only view | image is a `role=button` tab stop «Открыть в полном размере: {alt}», click/Enter/Space opens the view; no selection, pointer buttons, menu or control |
| F-23 | US-4 states | loading skeleton; a failed read → «Изображение недоступно» (`role=img`), the other images show |
| F-24 | US-4 full-size view | title «Просмотр изображения», alt caption, scroll area «Изображение, полный размер» focusable, «Закрыть» and Esc close and return focus to the image (selected), card stays open; failed read inside → «Изображение недоступно» |
| F-25 | design keyboard | Enter/Space on a selected image opens the view; Shift+F10 / ContextMenu opens «Меню изображения» with two items; Esc returns to the image; printable keys ignored |
| F-26 | US-4 display | `max-width: 100%`, never up-scaled |
| F-27 | US-6 offer | title, text with «{размер}» one decimal with a comma («7,4 МБ»), «Уменьшить» focused; caption file name, «Файл n из N · name» for a queue |
| F-28 | US-6 reducing | «Уменьшаем изображение…» `role=status`; «Отмена» aborts and a late answer is ignored |
| F-29 | US-6 result | «Было: …», «Стало: …» lines (`aria-describedby`), preview «Уменьшенное изображение» via the host address; «Вставить» inserts where the file was put; «Отмена»/Esc insert nothing; preview read failure → «Вставить» `aria-disabled` |
| F-30 | US-6 cannot reduce / failure | warning text + «Закрыть»; error `role=alert` + «Повторить» resends, «Отмена» |
| F-31 | US-6 queue | 2 MB uploads at once; 7 MB offer then 8 MB after the first closes; chosen order kept, a cancelled one leaves no gap |
| F-32 | US-6 50 at insert | 50 images + a 7 MB file → 50 text, no offer; placeholders and queued offers count |
| F-33 | US-6 cancel at offer | nothing sent, nothing inserted, focus back in the editor |
| F-34 | units | «МБ» figures and the 5/20 MB caps use one unit (spec: 5 MB = 5 242 880 B, so binary); 5 MB/20 MB/50 are host props with those defaults |
| F-35 | dispose | object URLs created for previews/placeholders revoked on dispose |

### Coverage map

Files: `images.test.tsx` (developer), `images.qa.test.tsx` (QA, 52), `useWorkItemEdits.images.qa.test.tsx`
(QA, 3), `useWorkItemEdits.test.tsx`, `workItemImagesApi.test.ts`, `paste.test.tsx`,
`toolbar/model.test.ts`, `shared/lib/richText/images.test.ts`, `fixtures.test.ts`,
`canonical.parity.test.ts`. "QA:" marks tests added by this pass; an `it.fails` test pins an open
defect and turns red when the defect is fixed (then it is flipped to `it`).

| # | Status | Where |
|---|---|---|
| F-1 | ⚠ D-5 | QA: chooser `multiple` + `accept` of the four types; disabled while saving; tooltip pinned by `it.fails` (D-5). Roving order: hidden controls leave it (`Toolbar.tsx` filters `hidden`) |
| F-2 | ✅ | QA: reverse finishing order keeps the chosen order, alt = name without extension, three `aria-busy` placeholders |
| F-3 | ✅ | dev: pasted file inserted; QA: clipboard alt «Изображение», change reported |
| F-4 | ✅ | QA: heading, code block, table cell → after the block; paragraph split; list item keeps the image as its own block |
| F-5 | ✅ | QA: drop at the position the view answers (`posAtCoords` stubbed, jsdom has no layout), not at the caret; default prevented. The drag-over tint: manual |
| F-6 | ✅ | QA: 5 242 880 → upload; 5 242 881 and 20 971 520 → offer; 20 971 521, SVG, 6 MB animated GIF → their texts, no request; dev: non-image |
| F-7 | ⚠ D-11 | QA: PNG + PDF + WebP → two uploads, the format text said once; it is then overwritten (D-11) |
| F-8 | ⚠ D-8 | dev: coded `images.*` refusal announced; QA: network failure → «Не удалось загрузить…», no placeholder. Non-`images.*` refusals → D-8 |
| F-9 | ❌ D-4 | dev: hook waits, second save `busy`; QA: helper «Дожидаемся загрузки изображений…» as `role=status` and back; a failed upload does not stop the save; the busy state and edits during the wait → D-4 |
| F-10 | ✅ | QA: foreign image dropped with its text and no request to that site; data image uploaded; other item's image re-uploaded under a new id; unreadable store image left out with «Не удалось загрузить…», text kept |
| F-11 | ❌ D-6 | QA `it.fails`: cut + paste uploads a copy |
| F-12 | ✅ | dev: Delete; QA: Backspace + undo, «Удалить изображение» from the menu with «Изображение удалено.» |
| F-13 | ✅ | every test drives the editor through a stub host; `shared/ui/RichTextEditor` names no `/api/` route (grep); the item's routes live in `workItemImagesApi.ts` |
| F-14 | ✅ | dev: no host → no control, silent ignore (`paste.test.tsx`); QA: `canInsert: false` → no control, pasted and dropped files ignored, default prevented |
| F-15 | ✅ | QA: Delete on a placeholder aborts the signal; the late answer inserts nothing |
| F-16 | ⚠ D-11 | QA: «Изображение вставлено.», «Изображение удалено.»; the keyboard hint replaces «вставлено» at the document's end (D-11). Hint on an arrow-key selection: not tested (jsdom focus), manual |
| F-17 | ⚠ D-13 | by arithmetic, see D-13 |
| F-18 | ✅ | QA: control disabled with `disabled`; read-only images stay openable (dev) |
| F-19 | ✅ | `fixtures.test.ts` / parity: image-* cases incl. image-alt-angles; dev: CR → LF, 255 code points; QA: a chosen file named `a <b>\rc.png` is reported as `alt="a b\nc"` |
| F-20 | ✅ | parity suite: canonical image cases load and serialise byte-equal |
| F-21 | ✅ | dev `images.test.ts`: image alone `hasContent`, no visible length |
| F-22 | ✅ | dev: read-only `role=button` «Открыть в полном размере: {alt}» opens the view; QA: Esc returns focus to that button |
| F-23 | ✅ | dev: failed read → «Изображение недоступно»; skeleton by code reading only |
| F-24 | ✅ | QA: scroll area «Изображение, полный размер» `tabindex=0`, alt caption, «Закрыть» → image selected in the focused editor; read-only → focus back on the image button; failed read inside the view |
| F-25 | ✅ | dev: Enter, no overwrite; QA: Space, Shift+F10 and ContextMenu → «Меню изображения» with two items, «Открыть…» opens the view, Esc → image selected, editor focused |
| F-26 | ✅ | by code: `maxWidth: 100%; height: auto`; rendering is manual |
| F-27 | ⚠ D-10 | QA: «Размер файла — 7,4 МБ…», file name caption; paste path focuses «Уменьшить»; chooser path does not (D-10, `it.fails`) |
| F-28 | ✅ | QA: «Уменьшаем изображение…» `role=status`, «Отмена» aborts, late answer ignored |
| F-29 | ✅ | dev: «Было»/«Вставить»; QA: lines are the dialog's description, preview «Уменьшенное изображение» from the host address, «Вставить» focused; failed preview → `aria-disabled`, inert; Esc inserts nothing |
| F-30 | ✅ | QA: cannot-reduce warning + «Закрыть»; failure `role=alert` + «Повторить» resends |
| F-31 | ⚠ D-7 | QA: the small file uploads at once, «Файл 1 из 2 · …» then «Файл 2 из 2 · …», a cancelled offer leaves no gap; two confirmed offers land reversed (`it.fails`) |
| F-32 | ✅ | QA: at 50, a 51st and a 7 MB file refused, no offer; the same insert's placeholder counts |
| F-33 | ✅ | dev: «Отмена» at the offer sends nothing, inserts nothing |
| F-34 | ✅ | QA: binary МБ (7 759 462 B, the contract's example, → «7,4 МБ»; 7 400 000 B → «7,1 МБ»); consistent with the caps 5 242 880 / 20 971 520 of the spec, the backend and `DEFAULT_IMAGE_LIMITS` |
| F-35 | ✅ | dev: released on unmount, `URL.revokeObjectURL` in the item host; QA: an address arriving after unmount is released |

Refusal texts: `copy.ts` refusals and US-6 texts equal the stories' copy and `docs/error-codes.md`
(`images.upload.format_unsupported`, `images.reduction.too_large`, `.animated`, `.cannot_reduce`,
`description_too_many_images`); server texts are shown as the catalog's `detail`.

Developer's deviation, judged: **`canInsert` on `RichTextImages`** (a viewer's card passes the host
so images can be read, with `canInsert: false`). Accepted: design § Host seam puts the read and the
insert into one group, while § Read-only and D-11 ask for images shown and the control absent; a
reader cannot see a stored image without the address rule, so the flag is the smallest way to honour
both. It is additive and defaults to `true`; feat-059's dialogs need not set it. Recorded for gate-3
as a seam addition feat-059 should know of.

Not reachable in jsdom, manual QA as the stories say: real clipboard and desktop drag, rendering and
scaling, scroll in the full view, light/dark, 360 px `fullScreen`, screen-reader output.

### E2E

E2E: no (standing answer). The human's standing answer is NO since 2026-10-12, and this task's route
confirmation (2026-11-16) said NO; not asked again, no Playwright spec written or run.

### Defects

#### D-4: a save waiting for uploads is not busy, and an edit made during the wait is lost

- Severity: major
- Steps: open a card, insert an image whose upload is slow, press «Сохранить», type into «Название»
  while «Дожидаемся загрузки изображений…» shows, let the upload end.
- Expected: the form shows its usual saving state during the wait (design § Save with an upload
  running), so nothing can be typed; or whatever was typed is kept.
- Actual: `useWorkItemEdits.save` awaits `settleDescription()` before `setSaving(true)`, so the card is
  not busy; the save then sends the `edits` captured when it was called and runs
  `setEdits(NO_EDITS)`: the title typed during the wait is neither sent nor kept, silently.
  `useWorkItemEdits.images.qa.test.tsx` pins both (`it.fails`).
- Status: open, returned to the frontend developer.

#### D-5: the «Изображение» tooltip is the control's name, not «Вставить изображение»

- Severity: minor
- Steps: hover the toolbar's «Изображение».
- Expected: tooltip «Вставить изображение» (US-1 copy, design § Toolbar control).
- Actual: «Изображение»; `copy.images.insert` exists and is never used (the toolbar's tooltip is
  `control.name`). Pinned by `it.fails`.
- Status: open, debt.

#### D-6: moving an image by cut and paste uploads a copy

- Severity: major
- Steps: select an image in Описание, cut, put the caret elsewhere in the same Описание, paste.
- Expected: US-1 «An image moved inside the same description: the same image is kept, nothing is
  uploaded again».
- Actual: `takeImages` keeps an own-store image only when `holdsImage(id)`, and after a cut the
  document no longer holds it; the paste re-reads the bytes and uploads them again (a placeholder, a
  new reference, up to 5 MB sent again). If that upload fails (503 busy, network), the image the
  member only moved is gone from the text. Pinned by `it.fails`. A fix could remember the references
  the last cut removed, or keep a reference already bound to this item (any member may re-save it,
  US-3).
- Status: open, returned to the frontend developer.

#### D-7: two confirmed offers of one insert land in reverse order

- Severity: minor
- Steps: insert a 2 MB PNG, a 7 MB JPEG and an 8 MB JPEG in one choice; «Уменьшить», «Вставить» on
  both offers.
- Expected: PNG, 7 MB, 8 MB (design D-9, the chosen order).
- Actual: PNG, 8 MB, 7 MB. Both slots sit at one position; `slotsPlugin` maps them with `assoc -1`,
  so the second slot stays left of the first image inserted there. Pinned by `it.fails`.
- Status: open, debt.

#### D-8: an upload refused by the item's own rules announces the generic text

- Severity: minor
- Steps: upload while the item is moved to «Удалена» by someone else (409
  `workitems.workitem.deleted`), or after the role was lowered (403).
- Expected: design § Notices: a server refusal announces the server's reason, the text of its code.
- Actual: `refusalOf` turns only `images.*` codes into `ImageRefusal`; other coded refusals read «Не
  удалось загрузить изображение. Попробуйте ещё раз.», inviting a retry that cannot succeed.
- Status: open, debt.

#### D-9: client checks skipped for data-address images; animation sniffing reads a fixed prefix

- Severity: minor
- Steps: paste html holding a `data:image/png` image over 5 MB (or `data:image/svg+xml`); separately,
  choose an animated WebP over 5 MB whose ICC chunk precedes `ANIM` (beyond 128 bytes), or an APNG
  whose `acTL` is after 4 096 bytes.
- Expected: the checks of a file: the offer over 5 MB (BR-IM-20 (7)), the format text before any
  request; an animation over 5 MB refused without an offer.
- Actual: a data image goes straight to `upload` and the server answers «Изображение больше 5 МБ…» or
  the format text, so no reduction is offered; those animated files get the offer and then the
  server's «Анимация больше 5 МБ…». Both end in a correct refusal, after one wasted request (the
  VP8X animation flag at byte 20 would make the WebP check exact).
- Status: open, debt.

#### D-10: the chooser path does not focus «Уменьшить»

- Severity: minor
- Steps: by keyboard, «Изображение» → choose a 7 MB JPEG.
- Expected: initial focus on «Уменьшить» (design § Dialog).
- Actual: the file input's `onChange` calls `editor.commands.focus()`, whose delayed focus fires after
  the dialog opened; the focus trap pulls focus back to the dialog frame, so Enter does nothing until
  Tab. The paste path is right. Pinned by `it.fails`.
- Status: open, debt.

#### D-11: notices of one action overwrite each other

- Severity: minor
- Steps: (a) drop a PNG and a PDF together; (b) insert an image at the very end of Описание.
- Expected: the format refusal readable (said once, design § Paste); «Изображение вставлено.» after
  an insert; the keyboard hint only on a keyboard selection (design § Announcements).
- Actual: (a) the refusal is replaced in the one notice line by «Изображение вставлено.» as soon as
  the PNG's upload ends, so a sighted member never reads why the PDF is missing (a screen reader
  heard it); the same for the other-site text of a rich paste. (b) the insert leaves the image
  selected, the hint selector takes the programmatic selection for a keyboard one, and «Enter —
  открыть в полном размере…» replaces «Изображение вставлено.». Both pinned by `it.fails`.
- Status: open, debt.

#### D-12: small accessibility and visual leftovers

- Severity: minor
- Actual: (1) an unavailable image in the editing state is a `role=img` «Изображение недоступно»
  inside the node view's own `role=img` of the same name: announced twice. (2) With
  `canInsert: false` a file dragged over the read-only card still turns its border into the dashed
  drop tint (`onDragEnter` checks `images`, not `canInsert`), inviting a drop that does nothing. (3)
  the full-size `Dialog`'s `aria-labelledby` points at the alt caption, not the title.
- Status: open, debt.

#### D-13: the toolbar fit at 640 px is not recorded, and no «Ещё» overflow exists

- Severity: minor
- Steps: design § Toolbar control: «The developer checks at 640 px that it still fits two rows; if
  not, … group 5 moves into the «Ещё» `Menu`».
- Actual: no record of the check (status.md, plan-frontend.md), and no overflow code. By arithmetic
  from the model (27 controls of 32 px, 4 px gaps, six separators of about 17 px, groups wrap whole):
  two rows need about 547 px of toolbar width, row two being lists + links + code + table (539 px +
  8 px padding); about 511 px without «Изображение». Whether the card's editor is that wide at a
  640 px viewport is a browser check, owed before gate-3.
- Status: open, debt (manual check; the overflow only if it fails).

Note, not a defect: `formatSize` rounds, so a 5 242 881-byte file reads «Размер файла — 5,0 МБ, а …
не больше 5 МБ». Rounding up past the cap would read better; left to the human.

### Runs

Per file, foreground. `images.qa.test.tsx` 52/52 (its first mount sits near the 5 s `findByRole`
limit under load, as the developer's file does); `useWorkItemEdits.images.qa.test.tsx` 3/3;
`images.test.tsx` 15/15; `useWorkItemEdits.test.tsx` + `workItemImagesApi.test.ts` green;
`fixtures.test.ts` + `images.test.ts` + `canonical.parity.test.ts` + `toolbar/model.test.ts` 448/448;
`paste.test.tsx` 17/17. Each `it.fails` was also run once as a plain `it` to confirm it fails for the
defect's reason, not a timeout. `npx tsc -b` clean; ESLint and Prettier clean on the two new files.

### Fixes after QA

2026-11-18, frontend developer, same branch. Fixed, each pin flipped from `it.fails` to `it`:
D-4 (`save` enters the saving state before the wait, so the card is locked; the save then reads the
edits as they stand after the wait), D-6 (the editor remembers the references it has held, so a
cut and paste inside one description keeps the id and uploads nothing), D-5 (tooltip «Вставить
изображение»), D-7 (the first slot maps left, the later ones right, so confirmed offers keep the
chosen order), D-10 (`editor.view.focus()` is synchronous, so the dialog keeps «Уменьшить»), D-11
(a refusal of the action stays readable: the swap of a placeholder is no edit that clears it, and
«вставлено» does not replace it; an insert's own selection raises no keyboard hint), D-12 (one
image role for an unavailable image, no drop tint without `canInsert`, the full-size dialog named
by its title; two tests added). Left as gate-3 debt, as asked: D-8, D-9, D-13.

### Verdict

blockers: 0 · majors: 2 (D-4, D-6, returned to the frontend developer) · minors: 8 (D-5, D-7..D-13,
debt for gate-3) · result: fail — re-check after the fixes of D-4 and D-6
