# feat-078-description-images

- type: feature
- size: L
- branch: feat/078-description-images
- stage: 03-ux-design
- epic: core/work-items
- deps: feat-018
- gates: gate-1, gate-2, gate-3

## Gates

requirements-review: needs-human-decision (review.md Round 1: R-1..R-15, 14 resolved in the spec, R-14 accepted-risk; OQ-1..OQ-7 for the human)
gate-1: approved (the human, 2026-11-17)
gate-2: pending
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
