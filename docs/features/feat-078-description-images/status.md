# feat-078-description-images

- type: feature
- size: L
- branch: feat/078-description-images
- stage: 01-business-analysis
- epic: core/work-items
- deps: feat-018
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

<!-- S/M: implementation plan, 3-10 lines. L: see plan-backend.md / plan-frontend.md -->

## Log

- 2026-11-16 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-078-description-images
- 2026-11-16 [route] the human confirmed: feature, L (L thresholds: an ADR on image byte storage, a new storage slice, new access rules for non-member uploads), full route, gates gate-1/gate-2/gate-3; economy mode; E2E answer NO

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
