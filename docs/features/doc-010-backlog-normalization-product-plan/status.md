# doc-010-backlog-normalization-product-plan

- type: docs
- size: L
- branch: doc/010-backlog-normalization-product-plan
- stage: complete
- epic: product/analysis
- deps: doc-002,doc-003,doc-004,doc-005,doc-006,doc-007
- gates: gate-1, gate-3

## Gates

gate-1: approved (2026-09-25, human in chat: "approved, sweep RA-15 before gate-3")
gate-3: approved (2026-09-25, human in chat, "approved, squash")

## Plan

No backend/frontend plan: a docs task. The plan is the route itself:
1. Cross-module review by four requirements-reviewers in clean contexts, one
   dimension each, findings in `cross-review/<dimension>.md`: A data model
   and ownership; B access and roles; C scope, releases and the queue vs the
   decompositions; D behaviour, rules, states and story seams.
2. Consolidation: `cross-review.md` — one ranked list, the decisions the
   human must make, the ones proposed as defaults.
3. Stage 1 (deep business-analyst): `spec.md` = the product plan (milestones,
   release scope, order and critical path, the normalized queue as the target
   table, amendments owed to the merged specs incl. the absorbed doc-008 and
   doc-009, ADR debt) + one story per milestone (acceptance demo scenario).
4. Stage 2 review (clean context) → gate-1.
5. After gate-1: the orchestrator applies the queue normalization in main via
   `scripts/queue.sh`; the amendments to the merged specs doc-001..007 are
   committed on this branch with a Log line in each amended task's status.md.
6. gate-3 → local squash.

## Decisions

- D-1 Route: docs, size L (queue line L, confirmed by the human 2026-09-24): reviewer and analyst on the deep tier; gate-2 inapplicable.
- D-2 The carry-back tasks doc-008-personal-object-ownership and doc-009-teams-per-project-carry-back are absorbed by this task: their queue lines are withdrawn (numbers never reused), mod-002 and mod-008 now depend on doc-010, and their content (doc-005 D-14 → doc-001 Roles/BR-5; doc-004 E-5 → doc-006 BR-M1-9/11, Team row, M1 decomposition) is delivered as amendments here.
- D-3 The product plan gives milestones and the critical path, not calendar dates.
- D-4 (Q-1) State labels: doc-002's uniform set wins for Эпик, Фича, Пользовательская история and Баг — «Новая» / «Активна» / «Разрешена» / «Закрыта»; Задача keeps W-2 «Новая» / «Активна» / «Закрыта». doc-007 P-1/P-2 are superseded. Consequence the amendments must state: «Разрешена» is InProgress on История (as User Story Resolved in ADO) and Resolved on Баг, so the Истории board maps columns per (type, state) — «Разрешена» of История gets its own column, not the «В работе» column — and a «Статус = Разрешена» filter is documented as spanning two categories.
- D-5 (Q-2) Category labels: «Новые» / «В работе» / «Решённые» / «Завершённые» / «Удалённые» (A-12). Board column headers derive from the per-level column mapping of D-4, not blindly from the category label.
- D-6 (Q-3) Team deletion IS in MVP, as a soft delete with cascade: refused while the team owns the root area; its other areas are re-owned to the first team; its boards and backlog entries are hidden; its iteration settings are dropped; its bound queries stop (doc-005 BR-10 moves into MVP). Root area: owned by the first team for good (H-9 stands). mod-002, mod-006, mod-007 grow accordingly.
- D-7 (Q-4) Per-team default area IS in MVP, together with "create a work item from a team's board/backlog" (the item takes that team's default area; the first team's default is the root). TEAM.defaultArea, BR-M1-14, doc-003 gains the create action, rule and story; doc-002's Область default; mod-002 and mod-006 grow.
- D-8 (Q-5) A work-item address carries the organization's internal id; the identifier КЛЮЧПРОЕКТА-ТИП-№ is resolved inside that organization (A-3). W-10/W-11 stand.
- D-9 (Q-6) Keycloak user directory is read through a PROJECTION fed by a custom Keycloak event-listener provider (user created/updated/disabled → MUSTASK), plus a backfill of existing users; member search runs on the projection. No admin-API calls from shipping code, ADR-0023 guards stay. This is its own slice: add `mod-NNN-keycloak-user-projection` (L, founds the projection and the Keycloak provider; owes an ADR) as a dependency of mod-002's member search.
- D-10 (Q-7) Granting "create organizations" is Keycloak realm administration, outside MUSTASK's role model (a realm role/group assigned by a Keycloak administrator); no MUSTASK role becomes a Keycloak administrator.
- D-11 (Q-8) Ownership: mod-002's ADR-0018 amendment states the predicate "permission code, then an optional ownership check on the resolved object"; feat-002 (own comments) and mod-004 (own attachments) implement the check per that rule in MVP; mod-008 registers the personal-object code in all role bundles (release 2). No separate M1 task.
- D-12 (Q-8b) D-14 of doc-005 covers ANY object a spec declares personal (personal queries now; personal dashboards or settings later without a new decision).
- D-13 (Q-9) Purge is in-product wherever a spec defines one, by the organization administrator only: work items in MVP; queries/folders/dashboards/widgets in releases 2/3 per doc-005 BR-9/D-13 (the purge list shows owner, folder and deletion date, never content). Organizations, projects and iterations are never purged.
- D-14 (Q-10) Filters split by surface: feat-003 = list filters (incl. «Удалена», former members, keyword), deps mod-003, mod-005; new `feat-NNN-board-backlog-filter` (M, deps mod-006, feat-003) takes doc-003 US-2's filtered-order scenario; feat-008 depends on the new line.
- D-15 (Q-11) Release-2 estimation fields = the ADO Agile set: Story Points on История and Баг; Original Estimate, Remaining Work, Completed Work and Activity on Задача and Баг. New line `feat-NNN-work-item-estimation-fields` (M).
- D-16 (Q-12) Release-2 saved queries cover everything the MVP inline filter can do: "Under" on Область and Итерация (subtree; "=" stays exact), a keyword clause on Заголовок and the identifier — in mod-008; Was Ever, @Today, Contains Words, In Group and tree/linked query types stay in mod-010.
- D-17 (Q-13) doc-001 is amended in place (MVP/release tables, ER model, ownership table, glossary, rules); it stays the umbrella every analysis starts from.
- D-18 (Q-14) Release-2/3 queue lines stay as they are; one analysis line `doc-NNN-release-2-scope` is added to give owners to the orphan release-2 items (notifications, audit, bulk edit, templates, copy/move) and to settle the double claims.
- D-19 (Q-15) mod-006 keeps its dependency on mod-005.
- D-20 Everything cross-review.md § Defaults lists is applied as written unless a decision above overrides it (N-16 concurrency table included).
- D-21 (OQ-1) No `doc-NNN-release-3-scope` line now; it is queued when MS-6 is reached. Until then the queued release-3 lines are not gated — PR-6 (a release-3 line starts only when no MVP or release-2 line is ready) is the rule. doc-007's process-editor rows stay unqueued.
- D-22 (OQ-2) No hard dependency of release-2/3 lines on the MVP's terminal lines; release order is priority (PR-6), deps stay minimal (PR-3).
- D-23 (OQ-3) The three analyst calls are confirmed: (a) a deleted team is never restored, its name becomes free, deletion asks for confirmation (doc-006 BR-M1-18); (b) a team added later comes with its own area under the root, named after it, as its default area (BR-M1-9, BR-M1-14); (c) «Разрешена» has its own column on the Эпики and Фичи boards too (doc-003 BR-M4-2).
- D-24 (OQ-4) The release-2 gate stays: mod-008 and the estimation-fields line (and mod-011 through it) depend on `doc-NNN-release-2-scope` (PR-5); doc-002's and doc-005's decomposition rows carry the dependency.

## Log

- 2026-09-24 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/doc-010-backlog-normalization-product-plan
- 2026-09-24 [orchestrator] route confirmed by the human: docs, L, stages 1–2 → gate-1 → gate-3 → local squash; gate-2 inapplicable; deps doc-002..007 verified merged (a480e92, 608fb63, 7a6ee2a, 7e84942, 17213d3, 6ef8eb2); queue line taken, doc-008/doc-009 withdrawn, mod-002/mod-008 re-pointed to doc-010 (queue-only commits in main)
- 2026-09-24 [orchestrator] cross-review A–D (70 findings) consolidated into cross-review.md (34 root causes: 3 blocker, 23 major, 8 minor); the human answered Q-1..Q-15 → D-4..D-20; note: on Q-1..Q-4 the human chose against the reviewers' recommendations (uniform «Разрешена», A-12 categories, team deletion and per-team default area in MVP) — the amendments must carry the stated consequences
- 2026-09-24 [orchestrator] stage 1 delegated in parallel (deep tier): product plan (spec.md + milestone stories) and the amendment pass over doc-001..007 per cross-review.md § Amendment map
- 2026-09-25 [business-analyst] product plan: spec.md 416 lines (releases and milestones MS-1..MS-8, normalized queue target table with 4 adds / 0 withdrawals / 24 edits, critical path to MVP = doc-010 → mod-001 ∥ keycloak-projection → mod-002 → mod-003 → mod-005 → mod-006 → feat-009 → feat-010 (5 L + 2 M, ~21 human stops), ADR debt, amendments summary, planning rules) + stories US-1..US-8 (one per milestone); open questions OQ-1 (release-3 scoping line) and OQ-2 (hard-dep release-2/3 roots on the last MVP lines) for gate-1
- 2026-09-25 [business-analyst ×2] amendment pass applied to doc-001..007 per cross-review § Amendment map with D-4..D-20: rules renumbered BR-M1..M6-n, doc-001 duplicates replaced by references, doc-001 gains BR-14/15, § Deletion lifecycle and § Concurrency policy, glossary absorbs doc-004/005 terms; each amended task carries a `[doc-010] amended` Log line and superseded decisions are marked; analysts' own calls where D-6/D-7 were silent (to confirm at gate-1): a deleted team cannot be restored and its name becomes free; a later team gets its own area under the root as default; the per-(type, state) column rule gives a separate «Разрешена» column on the Эпики and Фичи boards too
- 2026-09-25 [orchestrator] verify.sh green; scripted cross-reference check: every cited BR-M<k>-n and doc-001 BR-n resolves (BR-M1-15 is a release-2 rule defined in prose, BR-M5-14 is an explicit retirement note); sizes after: doc-001 434, doc-006 403, doc-007 342, doc-002 303 lines (over the index guide — reviewer to judge), stories ≤ 149
- 2026-09-25 [orchestrator] stage 2 delegated: two requirements-reviewers in clean contexts — the plan (review.md) and the amendments (review-amendments.md)
- 2026-09-25 [requirements-reviewer ×2] round 1: plan (review.md) — 4 majors (1 open: R-1 = OQ-4), 12 minors (2 open: R-11, R-15), 13 fixed in place; amendments (review-amendments.md) — 1 major (RA-1 = OQ-3), 12 minors (7 fixed, RA-9..RA-11 for the analyst, RA-12/RA-13 done by the orchestrator in feac3e5), 1 accepted risk (RA-14 sizes); 143/147 amendment-map rows applied, 3 partial, 1 missing (RA-13, done)
- 2026-09-25 [orchestrator] human answered OQ-1..OQ-4 → D-21..D-24; targeted fix pass delegated (R-11, R-15, RA-9, RA-10, RA-11, doc-004 US-8 vs doc-002 BR-M3-18), then a narrow delta check before gate-1
- 2026-09-25 [business-analyst] fix pass: spec.md 421 → 329 lines (§ Amendments one row per spec, § Releases scope pointers, § Permission codes added, PR-5/PR-6 per D-21/D-22/D-24, ADR-debt team row per D-23, OQ-1..4 closed), doc-001 R-11, RA-9 persona sweep over 29 story files + story tables, doc-004 US-8 reconciled with doc-002 BR-M3-18; R-1/R-11/R-15, RA-1/RA-9/RA-10/RA-11 resolved; verify.sh green (orchestrator run)
- 2026-09-25 [requirements-reviewer] delta check in review.md and review-amendments.md: approved-by-ai ×2 — nothing reopened; R-17..R-20, RA-16..RA-18 found and fixed in place; RA-15 left as a risk for the human (~150 Gherkin steps in doc-002..005 still say "a team member", wording only)
- 2026-09-25 [orchestrator] gate-1 requested from the human

- 2026-09-25 [business-analyst] fix pass: spec.md 421 -> 329 lines (section Amendments one row per spec, Releases scope pointers, Permission codes added, PR-5/PR-6 per D-21/D-22/D-24, ADR-debt team row per D-23, OQ-1..4 closed), doc-001 R-11, RA-9 persona sweep over 29 story files and the story tables, doc-004 US-8 reconciled with doc-002 BR-M3-18; R-1/R-11/R-15, RA-1/RA-9/RA-10/RA-11 resolved; verify.sh green (orchestrator run)
- 2026-09-25 [requirements-reviewer] delta check in review.md and review-amendments.md: approved-by-ai x2 - nothing reopened; R-17..R-20, RA-16..RA-18 found and fixed in place; RA-15 left as a risk for the human (~150 Gherkin steps in doc-002..005 still say 'a team member', wording only)
- 2026-09-25 [orchestrator] gate-1 requested from the human
- 2026-09-25 [orchestrator] gate-1: approved by the human in chat; RA-15 mechanical sweep ordered before gate-3; the queue normalization (spec.md § Queue operations) is applied in main next
- 2026-09-25 [task-executor] RA-15 sweep: 147 Gherkin steps in 20 story files of doc-002..005 (team member -> member; team kept where it is the subject); orchestrator fixed the same actor in doc-002's BPMN node A; verify.sh green
- 2026-09-25 [orchestrator] queue normalized in main per spec.md § Queue operations: adds mod-014-keycloak-user-projection, doc-011-release-2-scope, feat-013-board-backlog-filter, feat-014-work-item-estimation-fields (ec600c9..e33eeff); 22 line edits + the epic paragraph in one queue-only commit (9e55f1c); 29 lines, deps complete and acyclic; E-24 (Later -> Next for mod-001, mod-014, doc-011) waits for the merge
- 2026-09-25 [orchestrator] gate-3 requested from the human
- 2026-09-25 [orchestrator] gate-3: approved by the human in chat; stage: complete; local squash via scripts/finish-task.sh

## Notes

Summary (from the queue line or an argument): Cross-module review of doc-001..007 (gaps, contradictions, defects, queue vs decomposition drift), backlog normalization (merge/split/withdraw/re-dep/resize, absorb the carry-backs doc-008/doc-009) and the product plan: milestones, release scope, critical path
