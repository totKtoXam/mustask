# feat-033-orphaned-organization-rescue

- type: feature
- size: L
- branch: feat/033-orphaned-organization-rescue
- stage: 02-requirements-review
- epic: core/org-access
- deps: mod-016,tech-024,tech-025
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

<!-- L: see plan-backend.md / plan-frontend.md -->

## Log

- 2026-11-03 [orchestrator] task started in the worktree .claude/worktrees/antigravity-gemini-analysis-8e712c (app-made; branch renamed, based on origin/main 41d784ee; new-task.sh not used because the session is already bound here). Orchestrated from the doc-012 session as a parallel lane next to feat-048; local test lane shared through it (full runs and pushes need its go).
- 2026-11-03 [orchestrator] size mismatch: queue has size:M, human confirmed L. Route confirmed by the human: feature, L (thresholds: the authorization rules change — an instance role acts inside an organization without a membership — and an ADR amending ADR-0027/ADR-0033), full route, gates 1/2/3 separate; block 3.3: only the queue line. Economy mode; E2E no (standing answer).
- 2026-11-03 [orchestrator] queue: PR #54 takes the line (`queue.sh take`, auto-merge on; the doc-012 session's go for the push). Stage 1 started (business-analyst, deep tier for L).
- 2026-11-03 [business-analyst] spec.md (BR-RS-1..11, BPMN, operation completeness, two development passes, no new queue lines) + stories US-1 list, US-2 appoint, US-3 appointment record (all `shippable: no`); open questions OQ-1..OQ-5 with recommendations; ADR amending ADR-0027 rule 4 / ADR-0029 § 8 / ADR-0033 § 1 owed to the design/plan stage. Not committed by the analyst (no shell in the session) — the orchestrator commits.
- 2026-11-03 [orchestrator] Stage 2 (requirements-reviewer): needs-human-decision, 1 blocker, 2 majors, 5 minors. R-1 blocker, checked by the orchestrator in ADR-0034 (on tech/024 and tech/025, not yet on main): the projection no longer learns that an account was disabled or deleted, so "no known active administrator" never fires in H-1's case. tech-025 added to deps (R-5). gate-1 questions: the R-1 trigger, and the R-6 disclosures to system-admin. Spec goes back to the analyst after the answers, then a delta read.
- 2026-11-03 [orchestrator] Human's answers before gate-1: R-1 = A (rescue on request in any organization: adds or raises to admin only, never removes or downgrades, a required «Причина» kept in the record; the administrator-state column and filter go; wider than the queue line by the human's choice); R-6 = both disclosures accepted (every organization's status; the user search with name and e-mail); OQ-1 and OQ-2 as recommended; OQ-3 changed: the appointment record is read by system-admins AND by the organization's admins. Spec back to business-analyst, then a delta read.

## Notes

Summary (from the queue line): system-admin rescues an organization left with no active administrator: sees the list of all organizations and appoints an admin there, audited, with no access to the organization's content. Closes doc-006 accepted risk H-1
