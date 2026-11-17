# feat-078-description-images

- type: feature
- size: L
- branch: feat/078-description-images
- stage: 04-backend
- epic: core/work-items
- deps: feat-018
- gates: gate-1, gate-2, gate-3

## Gates

requirements-review: needs-human-decision (review.md Round 1: R-1..R-15, 14 resolved in the spec, R-14 accepted-risk; OQ-1..OQ-7 for the human)
gate-1: approved (the human, 2026-11-17)
gate-2: approved (the human, 2026-11-17)
gate-3: pending

## Plan

<!-- S/M: implementation plan, 3-10 lines. L: see plan-backend.md / plan-frontend.md -->

## Log

- 2026-11-16 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-078-description-images
- 2026-11-16 [route] the human confirmed: feature, L (L thresholds: an ADR on image byte storage, a new storage slice, new access rules for non-member uploads), full route, gates gate-1/gate-2/gate-3; economy mode; E2E answer NO
- 2026-11-16 [orchestrator] paused before stage 1: weekly usage (all models) at 99%, the 90% stop rule applies; resets 2026-11-20 00:00 UTC. Resume point: launch business-analyst (deep tier for L) with the Notes below as input
- 2026-11-16 [orchestrator] the human said «Запустить сейчас»: continue at 99% weekly usage, the 90% stop waived for this task; stage 1 started
- 2026-11-16 [business-analyst] stage 1 done: spec.md + US-1..US-5 (insert; upload checks and compression; stored form and reference rules; reads; sweep and purge). D-4: feat-078 ships the store, the unbound-upload model with a purpose slot, opaque addresses, the 2 h hourly sweep and the image-only description; feat-059 ships the feedback upload operation and binding (BR-IM-19). D-5: crop spun out as feat-NNN-description-image-crop (S). mod-004 is not a dependency (D-1). OQ-1..OQ-7 open for the human (formats, caps, compression, removed images, the 2 h window for items, crop split, animation), each with a recommendation. feat-059's files were read from its worktree on disk
- 2026-11-16 [requirements-reviewer] stage 2 round 1: verdict needs-human-decision. 15 findings (6 major, 9 minor), all but R-14 (no quotas, accepted-risk) fixed directly in spec.md and US-1..US-5: the feat-059 seam completed (editor takes upload and address from its host, per-purpose binding/reading, stored reference is an identifier), sweep timing in US-5 fixed to BR-IM-16, bind/sweep race ruled out, cap boundary row fixed, animation frame caps added (1 000 frames, 600 MP total), "without visible loss" made measurable (SSIM reference and method, colour profile, orientation), placement rules, no shared caching. OQ-1..OQ-7 remain for gate-1
- 2026-11-16 [business-analyst] the human's gate-1 answers applied: OQ-1, -3..-7 as recommended, now D-6, D-8, D-9, D-3, D-5, D-10; OQ-2 changed (D-7): stored images at most 5 MB, a still image over 5 MB is reduced in the system on the member's request and inserted only after they accept the shown result. New BR-IM-20 and story US-6 (the offer, before → after with a preview, cancel paths, cannot-reduce, no offer for animations) with its Russian copy; BR-IM-3 split into the stored cap (5 MB) and the intake for a reduction (20 MB); US-1/US-2 caps, notices and figures updated; NFR speeds re-based. Other caps unchanged, still consistent. New OQ-8 (20 MB intake) and OQ-9 (reduction floors: similarity 0.95, half size, format kept, so a PNG photo may still be refused) for the human. Gate values untouched
- 2026-11-17 [orchestrator] gate-1: the human answered OQ-8 (20 MB intake) and OQ-9 (half size, similarity 0.95, format kept) as recommended and approved the requirements (6 stories, the feat-059 split, crop as its own S line, R-14 no-quota risk accepted); US-6 was not re-reviewed, the human chose approve over a delta review. Stage 3 started
- 2026-11-17 [ux-designer] stage 3 done: design.md (images in Описание: «Изображение» control, placeholder, image block and its states, paste/drop/chooser, US-6 reduction dialog with queue, full-size view, keyboard and read-only, host seam for feat-059, narrow screens; 11 decisions D-1..D-11 for gate-2) and a clickable prototype/index.html (plain script, opens from file://). Open for the human: D-1 text alternative editing is not designed (out of scope per spec); D-6 read-only readers get a tab stop per image. gate-2 untouched
- 2026-11-17 [backend-developer] ADR-0045 written, status proposed (docs/adr/0045-image-store-in-own-schema-libvips-hourly-sweep.md, indexed in docs/adr/README.md): bytea in an `images` schema owned by a new Images module in the one MustaskDbContext and migration stream, metadata and bytes split, a cross-schema cascading key to work_items for the purge, random v4 ids, buffered reads with `private, no-cache` + ETag and nosniff; our own header reader, container metadata stripper and SSIM, NetVips (libvips) for decode/scale/encode only; hourly BackgroundService sweep with id-ordered row locks against the bind; ImagePurpose slot and IImageIntake/IImageBinding ports for feat-059; a process-wide decode limiter. For the human at gate-2: ADR D-1 library (licences to verify at pin; ImageSharp only with a commercial licence), D-2 animated PNG, D-3 still GIF scaling, D-4 backups. Owed by the orchestrator: ADR-0039 note pointing to ADR-0045 (BR-IM-18)

- 2026-11-17 [orchestrator] gate-2: the human approved the design (prototype clicked through in the browser by the orchestrator first: over-5 MB offer → reduce → result → insert, PDF refusal, read-only, «Изображение недоступно») and accepted ADR-0045 with D-1 NetVips, D-2 animated PNG like animated GIF, D-3 nearest-neighbour GIF scaling; ADR-0039 got its amendment note (BR-IM-18). Known: alt-text editing stays out of scope (design D-1); ADR D-4 backups proposed as a separate line. Stage 4 started
- 2026-11-17 [backend-developer] stage 4a: plan-backend.md and api-contract.md fixed. Routes: POST …/workitems/{identifier}/images (raw body, 5 MB), POST …/images/reductions (20 MB), GET /api/images/{imageId} (AuthenticatedOnly, the item's read rule in the handler, private no-cache + ETag, one 404 for every refusal); stored element `<img data-image-id alt>` (no src); two description field reasons (image_unavailable, too_many_images), nine `images.*` codes (catalog files written at 4b). Lanes: no (3 use cases + one shared intake). Refinement of ADR-0045 decision 3, same guarantee: the sweep deletes in SKIP LOCKED batches, then the skipped rows one per transaction, because a purge's cascade deletes image rows in no fixed order and a waiting batch could deadlock with it. nginx's default 1 MB body limit would refuse uploads: client_max_body_size 21m owed at 4b. NetVips 3.2.0 + NetVips.Native.linux-x64/win-x64 8.18.7 named; licences NOT verified (not in the local NuGet cache) — the dev stage reads the licence files before the pin and stops for the human on any GPL component. A ½-day spike opens 4b (untrusted-loader API, GIF palette exactness, NFR timings)

## Notes

Summary (from the queue line or an argument): Images in the work item description (feat-018 task B): paste, drop or upload an image into «Описание», shown inline, crop; bytes stored in the database in a separate PostgreSQL schema; large images compressed on insert without visible loss (the human, 2026-11-12, feat-018 status.md); formats and size cap to settle at analysis. Also serves feedback screenshots (feat-059)

Inputs for the analysis (the orchestrator, 2026-11-16; sources are files, read them):
- The human's decisions for this task: feat-018 `status.md` Log 2026-11-12 (bytes in the
  database in a PostgreSQL schema of its own; large images compressed on insert without
  visible loss; formats and the size cap still open). feat-018 spec BR-RT-4 (images
  dropped) and BR-RT-6 (no visible text = empty) are amended by this task.
- feat-059 (branch `feat/059-user-feedback-reports`, not merged) waits for this task.
  Its spec BR-FB-20 (a)-(e), BR-FB-6, ADR-0041 decision 6 and its `design.md` put these
  needs on feat-078: (a) a `user` holder with no membership uploads an image before the
  item exists, through the feedback operation only, bound to the report at send/edit by
  the uploader only; (b) nothing returned (upload answer, image address, markup) carries
  the organization, project or item id or key; (c) images of a report are read and added
  only through the feedback operation; (d) an upload bound to no report is removed
  2 hours after upload, hourly sweep (the human, 2026-11-16); (e) a description may be
  an image alone (BR-RT-6 amended). feat-078 decides whether it ships the pre-item
  upload itself or only the storage feat-059 builds on — say which in the spec.
- Working rules for this run: economy mode (one agent per stage, one Opus review); the
  E2E answer is NO; own test DB only (never `mustask` on 5432, never the stand on
  3100/5100/5010); click the prototype through in a browser before gate-2; the PR goes
  through `gh` with a merge commit and Auto-fix on.
- feat-059 takes no ADR number beyond 0041 (feat-059 session note, 2026-11-16): any ADR
  this task needs takes its own number, and feat-059 builds on it without a new one.
