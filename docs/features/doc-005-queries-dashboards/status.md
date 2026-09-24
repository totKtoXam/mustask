# doc-005-queries-dashboards

- type: docs
- size: M
- branch: doc/005-queries-dashboards
- stage: complete
- epic: product/analysis
- deps: doc-001
- gates: gate-1, gate-3

## Gates

gate-1: approved (2026-09-24, human in chat)
gate-3: approved (2026-09-24, human in chat, "approve, squash")

## Plan

1. Input: the umbrella `docs/features/doc-001-domain-map/spec.md` (module M6
   boundary, MVP/release-2/release-3 tables, decomposition row (a) for
   doc-005, "Out of scope"), the binding human decisions D-1..D-12 in
   `docs/features/doc-001-domain-map/status.md` (not reopened here),
   `docs/features/doc-001-domain-map/research/ado-boards.md` §§ Queries,
   Dashboards, Permissions and `screenshots/13..16` (Azure DevOps, the only
   reference product).
2. Stage 1 (business-analyst): `spec.md` = M6 glossary, logical entities
   QUERY / DASHBOARD / DASHBOARD_WIDGET and their relations to M1/M3/M4/M5,
   the release-2 cut (personal saved queries, shared query folders, clause
   builder over inline filters) vs the release-3 cut (dashboards, widgets,
   charts, WIQL-subset query language, CSV import/export), query-language
   subset boundary, module business rules, operation completeness; stories
   for the release-2/3 behaviour (all `shippable: no` — M6 is not founded in
   MVP, D-4); decomposition table of future M6 slices with deps by slug.
3. Stage 2 (requirements-reviewer, clean context) → gate-1.
4. Stages 3–8 skipped (docs task, no screens, no code); gate-3 → local
   squash via `scripts/finish-task.sh` (no remote configured).

## Decisions

Human decisions of 2026-09-24 (answers to review round 1; binding for the rework, alongside doc-001's D-1..D-12):

- D-13 (R-1) Deletion of M6 objects — queries, query folders, dashboards, widgets — is soft, exactly as doc-001 BR-6 / D-5 state for the rest of the system: a deleted object can be restored, retention is indefinite, an administrator purges by hand. No amendment to doc-001 needed.
- D-14 (R-2) A `viewer` may create, edit and delete their OWN private objects (personal queries in «Мои запросы»); every other write stays member+. This is the first write a reader may perform and changes doc-001's Roles section («read codes only») and what M1's permission catalog must express (ownership-based check, D-9 / ADR-0018 amendment) — it must be carried back into doc-001 and M1 by a separate task, not edited in doc-001 from here.
- D-15 (spec open question 1) Dashboards exist at two levels: team dashboards (one default per team) and project dashboards, as in Azure DevOps.
- D-16 (spec open question 3) `@CurrentIteration` in a query binds only to a team inside the query's own project (BR-1, D-11).

## Log

- 2026-09-22 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/doc-005-queries-dashboards
- 2026-09-22 [orchestrator] size checklist: 0.1 no behaviour change, not in the f-route list; block 1 clean (no migration, no project, no ADR, no slice founded — D-4 keeps M6 out of MVP, no breaking boundary change, no external party, no auth); 2.1 not S (two agent stages, spec + stories + review + status); 3.1 none, 3.2 n/a, 3.3 the queue line's "query language, search, charts, widgets, analytics" read as release-2/3 ground inside the M6 boundary
- 2026-09-22 [orchestrator] route confirmed by the human: docs, M, stages 1–2 → gate-1 → gate-3 → local squash; gate-2 inapplicable (no screens); size matches the queue line
- 2026-09-22 [orchestrator] deps checked: doc-001 merged in main (b5fde80); queue line taken in main (be4bc9a)
- 2026-09-22 [business-analyst] stage 1 (standard tier): spec.md (189 lines: roles, 5 stories, BPMN, ER model QUERY/QUERY_FOLDER/DASHBOARD/DASHBOARD_WIDGET, glossary, BR-1..8, operation completeness, NFR, out of scope, decomposition of three future M6 slices) + stories US-1..US-5 (all `shippable: no`, 41-49 lines each); 3 open questions for the human; the agent's Write tool was blocked from the task worktree (this session is rooted in another worktree), so the orchestrator placed the files verbatim, only expanding the decomposition deps from short ids to full ids; `bash scripts/verify.sh` -> RESULT: green (orchestrator run)
- 2026-09-22 [orchestrator] stage 2 delegated to requirements-reviewer (deep tier, clean context)
- 2026-09-22 [requirements-reviewer] round 1 in review.md: needs-human-decision — 2 blockers (R-1 hard vs soft delete of queries/dashboards vs doc-001 BR-6, R-2 viewer writing personal queries vs the read-only bundle of D-9), 7 majors (R-3..R-9: release-2 field set, operator/macro subset table, founder straddling release 2/3, charts task bundling tree/linked queries, personal-root cardinality and orphaning, NFR bounds for results and dashboards, result columns), 3 minors open (R-10..R-12), 7 minors fixed in place (R-13..R-19; spec.md, US-1, US-5); every doc-001 cross-reference verified; reviewer wrote via a scratchpad mirror (cross-worktree writes blocked), orchestrator copied verbatim; verify.sh green (orchestrator run)
- 2026-09-22 [orchestrator] decisions requested from the human before round 2 (R-1, R-2, dashboards team-only vs project-wide, @CurrentIteration team binding)
- 2026-09-24 [orchestrator] session restored after a Claude Desktop reinstall: the task branch now lives in worktree wizardly-dijkstra-d8714d (the separate doc-005 worktree is gone), last commit 0d2eb73
- 2026-09-24 [orchestrator] human decisions D-13..D-16 recorded (soft delete for M6 objects; viewer may write own private queries — needs a doc-001/M1 carry-back task; team + project dashboards; @CurrentIteration bound to a team of the query's project); stage 1 rework of R-1..R-12 delegated to business-analyst, round 2 review to follow
- 2026-09-24 [business-analyst] rework: R-1..R-12 resolved in review.md applying D-13..D-16; spec.md 235 lines (field/operator/macro tables moved to US-1 to stay in budget), stories US-1..US-5 updated (57-93 lines); decomposition now six rows incl. new doc-NNN-personal-object-ownership (carry-back of D-14 to doc-001/M1), mod-NNN-dashboards-widgets (release 3), mod-NNN-query-hierarchy-operators; no open questions left; verify.sh green (orchestrator run)
- 2026-09-24 [orchestrator] round 2 review delegated to requirements-reviewer (delta over round 1 + D-13..D-16)
- 2026-09-24 [requirements-reviewer] round 2 in review.md: approved-by-ai — R-1..R-12 resolutions verified, D-13..D-16 reflected; rework findings R-20 (major: carry-back docs row must not own the ADR-0018 amendment) and R-21..R-29 (minor) all fixed in place; spec.md 239 lines; verify.sh green (orchestrator run)
- 2026-09-24 [orchestrator] gate-1 requested from the human; normative numbers flagged for the human: 10 widgets per dashboard, results 2 s flat / 4 s tree, dashboard 3 s flat widgets, CSV 5,000-row cap refused above, UTF-8 BOM with `;`
- 2026-09-24 [orchestrator] gate-1: approved by the human in chat ("approve as is", normative numbers accepted)
- 2026-09-24 [orchestrator] decomposition entered into the main queue under Later: doc-008-personal-object-ownership (D-14 carry-back), mod-008-queries-dashboards (release 2), mod-009-dashboards-widgets, feat-005-csv-import-export, feat-006-dashboard-charts, mod-010-query-hierarchy-operators (release 3); deps by id; stages 3–8 skipped (docs task); gate-3 requested
- 2026-09-24 [orchestrator] gate-3: approved by the human in chat; stage: complete; local squash via scripts/finish-task.sh (no remote)

## Notes

Summary (from the queue line or an argument): Queries, search, dashboards and analytics module analysis: query language, saved queries, charts, widgets
