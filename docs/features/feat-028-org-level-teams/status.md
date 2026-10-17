# feat-028-org-level-teams

- type: feature
- size: L
- branch: feat/028-org-level-teams
- stage: 01-business-analysis
- epic: core/org-access
- deps: mod-017
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: pending
gate-2: pending
gate-3: pending

## Plan

Route L with UI on the light route (the human's choice of 2026-10-17 for the
lines after mod-017): business analysis → requirements review (one round) →
UX design → gate-1 + gate-2 put to the human as one package → backend (plan,
then code) → frontend → one QA pass over both layers alongside the code review
and the security pass → one fix round → at most one delta read → gate-3 → PR
to main. Plans: `plan-backend.md`, `plan-frontend.md`.

## Decisions

- E2E = NO: the standing answer since 2026-10-12; not asked, not run.
- Light route: the human's choice of 2026-10-17, see Plan.
- In scope by the human's answer of 2026-10-17: mod-017 S-2, so
  `IOrganizationStructure.FindProjectAsync` / `FindTeamAsync` read within the
  caller's organization.
- Out of scope: mod-020 (the switcher's fallback, the filter in the address),
  bug-005 (the creation deadlock), feat-040 (feat-029's card-dialog re-check).

## Log

- 2026-10-17 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-028-org-level-teams
- 2026-10-17 [orchestrator] route L (queue size; doc-012 § Decomposition: breaking per its Breaks cell) — the human chose it in chat («⭐ feat-028, облегчённо»). Light route. Gate-1 and gate-2 go to the human as one package after the design. E2E: no.
- 2026-10-17 [dev] P-10 note from mod-017 (`plan-backend.md` § 11): its last-team guard (OQ-1 A, `organizations.team.last_team_cannot_be_deleted`) and `organizations.team.not_in_project` hold until this task, which reworks them: unlinking or deleting a team reassigns the items it owns (SC-09).

## Notes

Summary (from the queue line or an argument): Teams become the organization's (the human, 2026-10-11): names unique in the organization, no team created with a project, descriptions on projects and teams. Project–team links, linked and unlinked from both pages (the project's end stays at projects/:p/settings/teams with the «По умолчанию» mark until the navigation line); unlinking or deleting a team reassigns the items it owns (SC-09); an owning team must be linked; team pages in the organization settings (doc-012 BR-MX-4..-6, -12; US-2; US-3, project–team)
