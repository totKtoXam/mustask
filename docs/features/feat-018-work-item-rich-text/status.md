# feat-018-work-item-rich-text

- type: feature
- size: L
- branch: feat/018-work-item-rich-text
- stage: 05-qa-backend ∥ 06-frontend (plan)
- epic: core/work-items
- deps: mod-003
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: approved 2026-10-13 (human, in chat: "Approve" on the amended spec + US-1..US-7, review rounds 3–4 closed; first approved 2026-10-12 on US-1..US-4)
gate-2: approved 2026-10-13 (human, in chat: "Approve и принять ADR-0027"; syntax highlighting via lowlight decorations is IN scope — design.md D-2 flips and is amended)
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
- 2026-10-13 [human] gate-1 approved on the amended spec + US-1..US-7. gate-2 approved ("Approve и принять ADR-0027"); ADR-0027 accepted; syntax highlighting through lowlight decorations IS in scope (design.md D-2 superseded). ADR-0007's index line now says "amended by ADR-0027".
- 2026-10-13 [orchestrator] stage 04a done: plan-backend.md + api-contract.md written (additive `descriptionFormat` plain|html; two new reasons description_markup_too_large, description_format_unknown; migration AddWorkItemDescriptionFormat; no lanes). Contract fixed — any change needs a note here and a Log line. Decisions: spec gap "ceiling vs blank 1 000 001-code-point value" — US-4 governs (refused, counts text read). Directory.Packages.props: the developer may add exactly the two package lines (HtmlSanitizer and the AngleSharp pins) after the spike. BR-RT-13 amendments (mod-003, feat-022, doc-002, feat-002 note) are applied at the end of stage 4/6, each with a status.md note. No E2E (human instruction).
- 2026-10-13 [orchestrator] stage 04b done (commits 05a68cd..4569225): spike chose AngleSharp alone (S2, pin 1.7.2; HtmlSanitizer not added — allowed by ADR-0027 fallback), migration AddWorkItemDescriptionFormat, reducer, handler, shared vocabulary + fixtures. Targeted suites green (Domain 534, Application 569, Architecture 83, Contracts 191, Infrastructure 474, Api WorkItems+Boards 391). `scripts/verify.sh` NOT run: tech-028 asked all sessions to pause full runs (CPU) — owed after the all-clear, before code review. api-contract.md unchanged.
- 2026-10-13 [orchestrator] OPEN for the human at gate-3: deviation — hostile deeply nested html is refused by a parse time budget (`RichText:ParseBudgetMilliseconds`, 1500 ms) with the existing reason description_markup_too_large; refusal by time is not hardware-independent (legit input at the ceiling parses in <0.7 s here). Backend QA is asked to probe it. Also owed: native proofreading of the two kk texts; spec wording on "ceiling vs blank value" to be clarified; frontend normaliser must match plan-backend.md § 6.2 choices.
- 2026-10-13 [orchestrator] backend QA (stage 5, targeted runs only) runs in parallel with the frontend plan (dispatch.md "Parallelism").
- 2026-10-13 [orchestrator] stage 6a done: plan-frontend.md (slices 0–7). Decisions recorded: F-3 an own decoration plugin over `lowlight` replaces `@tiptap/extension-code-block-lowlight` (the extension cannot do the design's 20 000 cp / 1 000 line cap and 150 ms idle recompute) — still lowlight as decorations per ADR-0027, human informed at gate-3; F-4 `react/no-danger` is enforced by ESLint no-restricted-syntax/properties + a source-scan test (eslint-plugin-react is not in the stack); F-5 the ADR's "entry chunk grows by zero bytes" is not literally achievable (lib/draft.ts needs the pure library) — proxy: no editor-library module reachable from the entry chunk + measured growth ≤ ~8 KB gzip, ceilings confirmed by the human after the first measurement at slice 3. Fixture cases the client adds go additively into contracts/rich-text/fixtures and are then run by the server suite (targeted). Design copy gaps G-1..G-4 and spec-silent F-6..F-10 are the developer's recorded choices, to be shown at gate-3.
- 2026-10-13 [orchestrator] backend QA (stage 5) verdict FAIL: D-1 blocker (≈2 KB PATCH of repeated <pre> overflows AngleSharp's stack and kills the process), D-2/D-3 majors, D-4..D-7 minors, R-1 risk (wall-clock parse budget makes accept/refuse host-dependent). Orchestrator decision: refusal by wall-clock is replaced by deterministic input-only structural bounds with the SAME reason description_markup_too_large; the time limit stays only as a safety net returning a server error. Defect loop 1 of 2 running with the backend developer; if AngleSharp cannot be guarded, a parser change (amends ADR-0027's spike conclusion) goes to the human.
- 2026-10-13 [orchestrator] stage 6b first half done (slices 0–3, commits a276a4f..c5a5dd1): pure library + normaliser port + fixture runners, contract plumbing, theme tokens, editor shell. Debounced onChange (150 ms) with `flush()` on submit is part of the plan (S3: the normaliser is >50 ms on big documents in jsdom). First bundle numbers (gzip): entry +≈6.2 KB library, editor chunk 161 KB now (≈185–200 KB projected), highlighter 23.6 KB, emoji 46.9 KB. Proposed ceilings for the human to confirm (ADR-0027 decision 10): entry growth 10 KB, editor chunk 220 KB, emoji chunk 52 KB. Open choice: an in-house link mark would save ≈20 KB gzip (@tiptap/extension-link only brings linkifyjs here; autolink is off).
- 2026-10-13 [orchestrator] frontend normaliser must follow the server's committed rule changes (CR reference → LF; link addresses containing < or > refused; structural guards) — port after the backend defect loop commits. The 18 fixture cases the client added go to the server suite (targeted) after that.
