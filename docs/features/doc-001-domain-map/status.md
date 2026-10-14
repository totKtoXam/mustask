# doc-001-domain-map

- type: docs
- size: M
- branch: doc/001-domain-map
- stage: complete
- epic: product/analysis
- deps: -
- gates: gate-1, gate-3

## Gates

gate-1: approved (2026-09-22, human in chat)
gate-3: approved (2026-09-22, human in chat, "approved, squash")

## Plan

1. Research input: `research/ado-boards.md` (domain model of Azure DevOps
   Boards read from the live Agile process via the REST API, configuration
   only) and `screenshots/` (UI reference, project data masked).
2. Stage 1 (business-analyst): `spec.md` = glossary, module map and
   boundaries, cross-module logical data model (entities and relations, no
   tables), MVP boundary and release plan, module order; stories for the
   umbrella-level behaviour; decomposition table for the module tasks
   doc-002..007 (already queued) and the first feature slices.
3. Stage 2 (requirements-reviewer, clean context) → gate-1.
4. After gate-1: the release/MVP decisions become the input of doc-002..007;
   gate-3 → local squash (no remote).

## Decisions

Human decisions of 2026-09-22 (gate-1 pre-round, answers to review round 1 and the spec's open questions); the spec must reflect them verbatim:

- D-1 Permissions are checked by permission codes (the existing `PermissionCatalog` pattern); realm roles are bundles of codes, containment comes from the group tree, never from a role hierarchy rule. Scope of a permission = membership in an organization/project (D-2), not the whole instance.
- D-2 Several organizations per instance, like Azure DevOps: Organization → Project → Team; a user may belong to several organizations. No "single tenant" shortcut.
- D-3 MVP ships the five built-in types Epic, Feature, User Story, Task, Bug with the backlog levels Epics / Features / Stories / Tasks and a board per portfolio and requirement level; bugs live at the requirement level (no "bugs as tasks" toggle in MVP). Sprint taskboard, capacity, burndown → release 2.
- D-4 Iterations are in MVP as a slice-founding L task (M5). Inline filters are a property of the work item list and board (M3/M4); module M6 is not founded in MVP.
- D-5 Comments and delete/restore are in MVP, each with its own story and slice. State «Удалена» (Removed) IS the recycle bin — one concept. Retention: indefinite; an administrator purges permanently by hand. — superseded in part by doc-010 D-13 (2026-09-25): purge is in-product, by the organization administrator only, work items in MVP; organizations, projects, teams and iterations are never purged
- D-6 No Reason field in MVP; any state → any state transition is allowed. Reason and transition rules come with the process editor (release 3).
- D-7 Glossary: work item = «Рабочий элемент»; «Задача» is only the name of the Task type.
- D-8 Attachments and the Area tree are in MVP (areas are the team-ownership mechanism, R-3).
- D-9 (R-23) The role admin/member/viewer lives in the MEMBERSHIP (organization membership; project/team membership derives from it), not in a realm role; permission codes are computed from the membership. Keycloak stays authentication plus instance-level roles only (instance administrator, dictionaries administrator). The organizations slice owes an amendment to ADR-0018. — superseded in part by doc-010 D-10 (2026-09-25): the instance-level roles are at least these two plus a grantable "create organizations" role/group, named by the ADR-0018 amendment
- D-10 (R-24) Organizations are created by an instance administrator; "create organizations" is an instance-level permission that can be granted to other users. An organization administrator adds members by picking existing realm users (search by name/e-mail); the creator becomes the organization's first administrator.
- D-11 (R-9) Iterations belong to the project (name, start, finish); a team selects the iterations it uses. The team's current iteration = the selected iteration whose dates contain today; on overlap the one with the earliest finish; with no dated match, the team's default iteration (a team setting).
- D-12 Russian names: types «Эпик», «Фича», «Пользовательская история», «Задача», «Баг»; backlog levels «Эпики», «Фичи», «Истории», «Задачи».

## Log

- 2026-09-22 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/doc-001-domain-map

- 2026-09-22 [orchestrator] route confirmed by the human: docs, M, stages 1–2 → gate-1 → gate-3 → local squash; gate-2 inapplicable (no screens)
- 2026-09-22 [orchestrator] human decisions: analysis split into an umbrella (this task) + six module tasks doc-002..007 (queued in main, deps doc-001); orientation is Azure DevOps Boards only (no Jira/ClickUp/Notion); scope adds a logical data model and ADO UI screenshots with the source project's data masked before capture
- 2026-09-22 [orchestrator] research note added: `research/ado-boards.md` — configuration and aggregate counts only, no work item content copied
- 2026-09-22 [orchestrator] `screenshots/` committed: 35 masked Azure DevOps UI captures with an index; delivery plans and taskboard-with-tasks not available in the source project
- 2026-09-22 [orchestrator] stage 1 delegated to business-analyst (standard tier): spec.md + stories/, write zone limited to those paths
- 2026-09-22 [business-analyst] spec.md (343 lines: glossary, module map M0–M6, ER model, MVP/release-2/release-3 tables, module order, BR-1..8, operation completeness, decomposition a/b) + stories US-1..US-6 (all `shippable: no`); 5 open questions for the human; `bash scripts/verify.sh` → RESULT: green (run by the orchestrator, the BA session had no shell)
- 2026-09-22 [orchestrator] stage 2 delegated to requirements-reviewer (deep tier, clean context); note for the reviewer: spec.md is over the ~250-line index guide — decide whether that is content leaking from stories or the umbrella's legitimate size
- 2026-09-22 [requirements-reviewer] round 1 in review.md: needs-human-decision — 3 blockers (R-1 M5/M6 founding sizes, R-2 backlog order owner, R-3 team/area mechanism), 11 majors (R-4..R-14), 6 minors (2 fixed in spec.md by the reviewer); human-only questions collected for gate-1
- 2026-09-22 [orchestrator] decisions requested from the human before round 2 (permission model, MVP cut of M5/M6, comments and delete/restore, Reason/transitions, glossary term, retention, tenancy)
- 2026-09-22 [orchestrator] human decisions D-1..D-8 recorded; the MVP grows (organizations, five types with levels and boards, areas, attachments, iterations, comments, delete/restore) — stage 1 rework delegated to business-analyst, round 2 review to follow
- 2026-09-22 [business-analyst] rework: spec.md 367 lines (D-1..D-8 applied), stories US-1..US-10 rewritten (old six removed by the orchestrator), all 22 round-1 findings marked resolved in review.md; 3 new open questions (self-service organization creation, Russian type/level names, current-iteration fallback); verify.sh green (orchestrator run)
- 2026-09-22 [orchestrator] round 2 review delegated to requirements-reviewer (delta over round 1 + D-1..D-8)
- 2026-09-22 [requirements-reviewer] round 2 in review.md: needs-human-decision — 21/22 round-1 findings verified, R-9 reopened (who pins the current iteration, overlap tie-break), new R-23 blocker (realm-global role vs per-membership role), R-24 major (how a user becomes an organization member), R-25..R-30 minor fixed by the reviewer, R-31 accepted-risk; round limit reached, escalated to the human
- 2026-09-22 [orchestrator] human decisions D-9..D-12 after round 2 (membership-held role, org creation by instance admin with a grantable permission, team-selected iterations with date-based current, names Фича/Баг); targeted rework delegated to business-analyst, then a narrow reviewer check of R-9/R-23/R-24 before gate-1
- 2026-09-22 [business-analyst] D-9..D-12 applied: spec.md 378 lines, US-1/US-9 rewritten, US-3/US-7 renamed, R-9/R-23/R-24 resolved in review.md; no open questions left; verify.sh green (orchestrator run)
- 2026-09-22 [orchestrator] narrow reviewer check of the D-9..D-12 delta delegated before gate-1
- 2026-09-22 [requirements-reviewer] delta check in review.md: approved-by-ai — R-9/R-23/R-24 hold, D-12 consistent, 3 new minors (R-32..R-34) fixed in place; verify.sh green
- 2026-09-22 [orchestrator] gate-1 requested from the human
- 2026-09-22 [orchestrator] gate-1: approved by the human in chat; decomposition (b) entered into the main queue as ten lines under Later (deps on doc-002..007 and on each other)
- 2026-09-22 [orchestrator] queue lines issued in main: mod-001-process-seed, mod-002-organizations-projects-teams, mod-003-work-item-core, feat-001-work-item-links, mod-004-work-item-attachments, feat-002-work-item-comments, mod-005-work-item-delete-restore, mod-006-boards-backlogs, mod-007-iterations, feat-003-work-item-search (all Later, deps by id); stages 3–6 skipped (docs task); gate-3 requested
- 2026-09-22 [orchestrator] gate-3: approved by the human in chat; stage: complete; local squash via scripts/finish-task.sh
- 2026-09-25 [doc-010] amended: Context, Roles (instance-level roles, Keycloak-granted "create organizations", ownership predicate, role table, team-level settings, actor wording), stories table, Module map, Glossary (absorbs doc-004/doc-005 terms; keys, category labels, verbs, markings), ER + ownership table, MVP table, Release 2/3, Module order, BR-2/3/4/5/6/9/10/11/13 rewritten, new BR-14/BR-15, § Deletion lifecycle, § Concurrency policy, Operation completeness, NFR Audit, Out of scope, Decomposition (founders + ADRs), US-1..US-10 (cross-review N-1..N-5, N-7, N-8, N-10..N-14, N-16..N-19, N-21, N-23..N-26, N-28, N-29, N-31, N-32; doc-010 D-4..D-18); also the context paragraph and § Open questions; also the context paragraph and § Open questions
- 2026-10-11 [mod-016] amended: § Roles (the ADR-0018 amendment and the ownership-predicate statement are mod-016's), BR-5 source column, § Decomposition (the mod-016 row split from mod-002; the projection row now names mod-016's member search) — commit 98a5811; US-1 and US-2 delivery lines (their membership scenarios are mod-016's) — doc-010 D-25, PR-7, mod-002 D-e

## Notes

Summary (from the queue line or an argument): Azure DevOps Boards domain map: glossary, module boundaries, cross-module logical data model, MVP boundary and releases, module order
