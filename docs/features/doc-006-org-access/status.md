# doc-006-org-access

- type: docs
- size: L
- branch: doc/006-org-access
- stage: complete
- epic: product/analysis
- deps: doc-001
- gates: gate-1, gate-3

## Gates

gate-1: approved (2026-09-24, human in chat, "approve with the edits above": R-20, R-21, R-25)
gate-3: approved (2026-09-24, human in chat, "Approved, squash")

## Plan

1. Input: the umbrella `docs/features/doc-001-domain-map/spec.md` (module M1
   boundary, Roles, MVP table, ER model, BR-3/5/13, decomposition row (a) for
   doc-006), its binding decisions D-1..D-12 in `status.md`, and the Azure
   DevOps reference `research/ado-boards.md` + `screenshots/` (settings:
   areas, permissions, notifications, teams).
2. Stage 1 (business-analyst, deep tier — L): `spec.md` = roles and the
   membership-held permission-code model (D-1/D-9), the M1 entities
   ORGANIZATION / ORG_MEMBERSHIP / USER / PROJECT / TEAM / AREA and their
   invariants, the organization-creation gate and member-adding flow (D-10),
   the MVP org/project/team/area cut, the release-2 boundary (per-project
   overrides, notifications), BPMN of the main flows; stories with Gherkin
   criteria; the decomposition table refines `mod-organizations-projects-teams`
   without touching the queue.
3. Stage 2 (requirements-reviewer, deep tier, clean context) → gate-1.
4. Stages 3–8 skipped (docs task, no screens, no code); gate-3 → local squash
   via `scripts/finish-task.sh` (no remote).

## Decisions

- doc-001's human decisions D-1..D-12 are binding input; this task does not
  reopen them. In particular D-9/D-10 supersede the queue line's wording
  "groups and permissions via Keycloak": roles live on `ORG_MEMBERSHIP`,
  Keycloak keeps authentication and the two instance-level roles only. — superseded in part by doc-010 D-10 (2026-09-25): plus a grantable "create organizations" role/group
- "Notifications" from the queue line are a release-2 boundary section, not
  MVP stories (doc-001 MVP table, M1: "no notifications").
- gate-2 is inapplicable (no screens) and is not listed in `gates:`.
- An analysis task writes no ADR: the ADR-0018 amendment stays owed by
  `mod-organizations-projects-teams` (doc-001, decomposition (b)).
- H-1 (Q-1/R-8, 2026-09-24) An organization left with no usable administrator has no recovery path in MVP — deferred by the human; the case is recorded in spec.md as a known limitation (accepted risk), not solved. The concurrency rule that keeps the last administrator (R-8) is still stated, it is correctness, not this decision.
- H-2 (R-2) Organization names are public within the instance: a name-conflict refusal may say the name is taken. BR-M1-8's invisibility covers an organization's content (projects, members, work items), not the existence of its name.
- H-3 (R-3) The M1 audit log is release 2, built on the audit/history ADR owed by `mod-work-item-core`, not a second mechanism; MVP keeps only who/when on the membership record itself. No separate audit queue line.
- H-4 (Q-4/R-9) A member whose Keycloak account is disabled or deleted keeps their memberships and is shown as inactive in lists and in «Исполнитель»; they cannot sign in; an administrator decides whether to remove them.
- H-5 (R-11) Backlog shape: `mod-002-organizations-projects-teams` founds all of M1 as doc-001 decomposed it (US-1..US-7), no new queue lines, no rename. — superseded by doc-010 D-9 (2026-09-24)
- H-6 (gate-1) R-20 accepted: constraint 5 on the ADR-0018 amendment (a channel for MUSTASK to read Keycloak's user directory for member search and the inactive status) plus a "Keycloak unreachable" scenario in US-3. — superseded by doc-010 D-9 (2026-09-24)
- H-7 (gate-1) R-21 accepted: the concurrency NFR covers every M1 check-then-act invariant (BR-M1-1/4/5/10, sibling area name uniqueness, BR-M1-12 cycles, BR-M1-13 area delete vs work item create).
- H-8 (gate-1) R-25: inactive accounts are not offered in member search or the team member picker; the «Исполнитель» side is doc-002's.
- H-9 (gate-1) The analyst's four calls are approved: only active administrators count for BR-M1-4; members and viewers cannot leave on their own; a re-enabled account loses «неактивен»; the root area is not renamed or re-owned on its own.

## Log

- 2026-09-22 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/doc-006-org-access
- 2026-09-22 [new-task] size mismatch: queue has size:M, human confirmed L
- 2026-09-22 [orchestrator] route confirmed by the human: docs, L (raised from the queue's M), stages 1–2 → gate-1 → gate-3 → local squash; gate-2 inapplicable (no screens); checklist: block 0 no behaviour change, block 1 no L threshold fired, 2.1 not S (spec + stories, > 2 h), 3.1 none, 3.2 n/a, 3.3 queue line predates D-9/D-10 — D-1..D-12 taken as binding
- 2026-09-22 [orchestrator] queue: the doc-006 line had already left main's queue in commit 47347d2 ("Take doc-007-process-customization", another session) before `scripts/queue.sh take doc-006` ran here — the take found nothing to remove; the number is held by branch doc/006-org-access and this folder

- 2026-09-22 [orchestrator] layout: subagent writes are sandboxed to this session's worktree, so the separate doc-006 worktree was removed (it held only the committed status.md) and branch doc/006-org-access is checked out in .claude/worktrees/trusting-cartwright-dc8e3e — finish-task.sh resolves the branch by name, nothing else depends on the path
- 2026-09-22 [orchestrator] stage 1 delegated to business-analyst (deep tier — L), write zone spec.md + stories/
- 2026-09-22 [business-analyst] spec.md (289 lines: roles and the membership-held permission-code table, BPMN, M1 ER model and invariants, BR-M1-1..15, operation completeness, MVP/release-2 cut, decomposition narrowing mod-002 to US-1..4 plus three feat-NNN lines) + stories US-1..US-7 (all `shippable: no`, max 82 lines); 4 open questions for the human (Q-1 last-admin lockout, Q-2 where "create organizations" is granted, Q-3 one team vs several per project, Q-4 disabled Keycloak accounts); spec.md is ~39 lines over the index guide — analyst argues it is entity-driven, offers to move the ER section to model.md; `bash scripts/verify.sh` → RESULT: green (orchestrator run)

- 2026-09-22 [orchestrator] stage 2 delegated to requirements-reviewer (deep tier, clean context); note for the reviewer: spec.md is over the ~250-line index guide — decide whether that is story content leaking into the index or the legitimate size of an entity-heavy M1 index

- 2026-09-22 [requirements-reviewer] round 1 in review.md: needs-human-decision — 3 blockers (R-1 realm roles admin/member/viewer still declared in mustask-realm.json and the ADR-0018 contract test vs the spec's "no such realm role"; R-2 name-conflict refusal reveals an organization exists vs BR-M1-8; R-3 audit rule BR-M1-15 with no story, no MVP placement, yet a queued feat line), 8 majors (R-4..R-11: Q-2/Q-3 answerable from ADR-0018/doc-001, degenerate area ownership with one team, seed failure path, admin-removal race, former-member marking pre-decided, purge actor, decomposition sizes and mod-002 slug), 8 minors (R-12..R-16 fixed in place, R-17..R-19 open); spec.md length ruled legitimate, no split; human-only questions collected for gate-1

- 2026-09-24 [orchestrator] human decisions H-1..H-5 on review round 1 recorded in Decisions; Q-2/Q-3 resolved from ADR-0018/doc-001 per R-4/R-5 (not human questions); rework delegated to business-analyst (deep tier)

- 2026-09-24 [business-analyst] rework: R-1..R-19 resolved per H-1..H-5 (Resolution lines in review.md); spec.md 327 lines, stories 56–127 lines (US-3 127); new BR-M1-16 (who/when on membership), BR-M1-17 (inactive account marking), concurrency NFR, ADR-0018 amendment requirements; decomposition = mod-002 only; no new open questions; analyst calls for gate-1: BR-M1-4 counts only active admins, no self-leave for members/viewers, re-enabled account clears «неактивен», root area not renamable/re-ownable; verify.sh green (orchestrator run)
- 2026-09-24 [orchestrator] round 2 delta review delegated to requirements-reviewer (deep tier, clean context)

- 2026-09-24 [requirements-reviewer] round 2 in review.md: needs-human-decision, no blockers — R-1..R-19 verified, H-1..H-5 faithful; new R-20 major (member search and inactive status need a Keycloak directory channel not in ADR-0018, no Keycloak-unreachable path), R-21 major (concurrency NFR misses BR-M1-5/10, sibling area names, area delete vs work item create), R-25 minor (are inactive accounts offered in pickers); R-22..R-24 minors fixed in place; spec.md 331 lines ruled legitimate; round limit reached, escalated to gate-1
- 2026-09-24 [orchestrator] gate-1 requested from the human

- 2026-09-24 [orchestrator] gate-1: approved by the human in chat with edits R-20/R-21/R-25 (H-6..H-8) and the analyst's four calls (H-9); targeted edits delegated to business-analyst

- 2026-09-24 [business-analyst] gate-1 edits applied: constraint 5 (Keycloak user-directory channel) in § Roles, widened concurrency NFR, inactive accounts excluded from member search and team picker (BR-M1-17), US-3 +2 scenarios (142 lines), US-6 +1 (70); spec.md 335 lines; Resolution lines for R-20/R-21/R-25 in review.md; orchestrator updated the stale H-range in § Context; verify.sh green
- 2026-09-24 [orchestrator] stages 3–8 skipped (docs task, no screens, no code); queue unchanged (H-5: mod-002 keeps its line); gate-3 requested

- 2026-09-24 [orchestrator] gate-3: approved by the human in chat; stage: complete; local squash via scripts/finish-task.sh
- 2026-09-25 [doc-010] amended: Context, § Roles (instance roles, constraint 1 scope attribute, constraint 5 user projection, new constraints 6–8, codes table, team membership carries no access), stories table, BPMN + team-deletion flow, lifecycle, ER (PROJECT.key, TEAM name/status, TEAM default area, PROJECT–TEAM one or more), BR-M1-1/2/7/9/11/13/14/17 rewritten, new BR-M1-18 (team deletion), Operation completeness, NFR, MVP/Release 2, Out of scope, Decomposition (+ mod-NNN-keycloak-user-projection), US-1..US-7 (cross-review N-2, N-3, N-5..N-8, N-10..N-14, N-19, N-20, N-26, N-28, N-29; doc-010 D-6, D-7, D-9, D-10, D-11, D-13, D-18); also § Open questions; also § Open questions

## Notes

Summary (from the queue line or an argument): Organization, projects, teams and access module analysis: projects, teams, areas, groups and permissions via Keycloak, notifications
