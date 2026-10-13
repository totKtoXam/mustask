# feat-018-work-item-rich-text

- type: feature
- size: L
- branch: feat/018-work-item-rich-text
- stage: gate-1
- epic: core/work-items
- deps: mod-003
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending — reopened 2026-10-13 for the scope amendment (was approved 2026-10-12 on spec + US-1..US-4, review rounds 1–2 closed)
gate-2: pending
gate-3: pending

## Plan

<!-- S/M: implementation plan, 3-10 lines. L: see plan-backend.md / plan-frontend.md -->

## Log

- 2026-10-12 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-018-work-item-rich-text

## Notes

Summary (from the queue line or an argument): Описание as rich text (doc-002 BR-M3-4), converting the stored plain text

## Route confirmation

- 2026-10-12 [orchestrator] human confirmed: type feature, size M (as queued), scope = «Описание» only (not «Критерии приёмки», «Шаги воспроизведения», comments). Human instruction: no E2E tests for this task. Open points handed to analysis: editor library + storage format (ADR threshold — escalate to L if an ADR turns out to be needed), plain→HTML conversion reversibility / table population (migration threshold).
- 2026-10-12 [orchestrator] stage 01 done: spec.md + US-1..3; OQ-1..4 open for gate-1 (OQ-1 ADR → L unless human keeps M)
- 2026-10-12 [orchestrator] stage 02 round 1: verdict needs-human-decision; 1 blocker (R-1 size must be L: ADR is unavoidable, contract change), 7 majors, 5 minors — see review.md. Waiting for human: re-confirm size L + OQ-1..3.
- 2026-10-12 [human] size re-confirmed as L (escalation from M: an ADR on the editor is unavoidable — routing.md ADR threshold — and the `description` contract meaning changes). Diverges from the queue line's size:M.
- 2026-10-12 [human] OQ-1/OQ-4: sanitized HTML + TipTap (ProseMirror) + server-side sanitizer, new dependency accepted (ADR written and accepted before code). OQ-2: lazy conversion at read with a stored-format marker, no data migration. OQ-3: the limit counts visible text, with a higher physical storage ceiling.
- 2026-10-12 [orchestrator] stage 02 round 2 (delta): R-1..R-13 resolved; R-14, R-15 + m-1..m-5 fixed by the analyst in the last allowed iteration (review.md Round 2). Spec + US-1..US-4 at gate-1, human approval pending.
- 2026-10-12 [orchestrator] next (superseded): analyst revises spec/stories for review findings R-1..R-13 under these decisions; one delta read; then gate-1 (human approval is still pending).
- 2026-10-12 [human] gate-1 approved (spec + US-1..US-4). 2026-10-12 design + prototype + ADR-0027 (proposed) produced; gate-2 question dismissed unanswered, ADR not accepted.
- 2026-10-13 [human] scope feedback after viewing the prototype: (1) images must be insertable; (2) table operations do not work (prototype defect — the button is disabled in code); (3) the editor must offer everything on the reference tracker's comment-editor toolbar (bold, italic, underline, bullet/numbered lists, highlight, text colour, emoji, indent/outdent, strikethrough, font size, code, @mention, # work-item link, clear formatting, image, more) plus: proper tables, proper code display and editing, links, image crop.
- 2026-10-13 [human] decisions: delivery as three consecutive tasks — A (this task, feat-018): the editor with all text formatting, tables, code, links; B: images (upload as attachments, shown in the text, crop); C: @mentions and # work-item links. Colour, highlight and font size: closed palette stored as classes, no inline style. The PR-link button is omitted: the product has no pull-request entity (ordinary link covers it).
- 2026-10-13 [orchestrator] effect on A: spec amended (BR-RT-3 allow-list gains underline, strike, highlight/colour/size classes, indent/outdent lists, clear formatting, emoji; BR-RT-4 unchanged — images stay out of A and go to B; code and table behaviour specified), gate-1 reopened, gate-2 not yet given. Queueing B and C is BLOCKED: `scripts/queue.sh add` refuses because the main checkout has an untracked `.agents/teamwork/` — a human decision, not touched.
- 2026-10-13 [orchestrator] size L re-confirmed for the amended scope (routing.md "escalation by volume"): the diff grew by US-5..US-7 and the requirements review (round 3) recommended keeping one L, not splitting; if the diff doubles again, US-5 is the cut. Round 3: R-16..R-21 and m-6..m-13 fixed by the analyst; one delta read (round 4) owed, then gate-1 (human).
- 2026-10-13 [orchestrator] stage 02 round 4 (delta): verdict approve-for-gate-1, no blockers or majors; minors m-16..m-24 applied by the analyst as small edits, m-15 handed to design.md. Spec + US-1..US-7 at gate-1 (reopened), human approval pending; the previous design.md, prototype and ADR-0027 (proposed) predate the amendment and are redone after gate-1.
