# feat-033-orphaned-organization-rescue

- type: feature
- size: L
- branch: feat/033-orphaned-organization-rescue
- stage: 03-design
- epic: core/org-access
- deps: mod-016,tech-024,tech-025
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: approved (2026-11-03, human in chat)
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
- 2026-11-03 [business-analyst] Fix round after review: R-1..R-8 addressed (on-request rescue in any organization, add/raise only, required «Причина»; no administrator-state claim; record read by system-admins from «Все организации» and by the organization's admins on «Участники» under `organizations.membership.write`; ADR-0029 § 9 cited, ADR debt narrowed; list paged 50, filter rule, local-time dates, 403 «Недостаточно прав»); OQ-1..OQ-5 closed; new OQ-6 (the already-administrator refusal discloses one fact) open for gate-1; still three stories. Not committed by the analyst (no shell) — the orchestrator commits.

- 2026-11-03 [orchestrator] Stage 2 delta read (round 2): R-1..R-8 resolved, 0 blockers, 0 majors, 2 new minors (R-9 refusal order and «Причина» character unit; R-10 race with an admin's own change to the same membership, pre-tech-025 inactive rows accepted by name). Verdict needs-human-decision only for OQ-6 and the page size of 50. gate-1 asked.

- 2026-11-03 [orchestrator] gate-1 approved by the human in chat: minors R-9/R-10 to be closed by the analyst before design, no new gate; OQ-6 accepted (explicit refusal «Пользователь уже администратор этой организации»); page size 25, not 50. Analyst minor pass, then stage 3.

- 2026-11-03 [orchestrator] Minor pass (business-analyst, b6c28af5): BR-RS-6a refusal order (403, organization not found, user not appointable, self, already admin, reason), «Причина» in code points, BR-RS-11 race with an admin's own change, pre-tech-025 inactive rows refused as mod-016 US-1 does (citation checked), page size 25, OQ-6 closed. US-2 is 166 lines, over the ~150 budget: left as is. Stage 3 next.

## Notes

Summary (from the queue line): system-admin rescues an organization left with no active administrator: sees the list of all organizations and appoints an admin there, audited, with no access to the organization's content. Closes doc-006 accepted risk H-1
