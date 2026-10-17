# feat-028-org-level-teams

- type: feature
- size: L
- branch: feat/028-org-level-teams
- stage: 03-ux-design
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
- The spec against the queue line: ADR-0029 makes this line register four
  permission codes (`organizations.team.create`, `.delete`, `.link`,
  `organizations.project.link`; admin-only until mod-019) and narrow
  `team.write`; of mod-016's inventory rows 15-26 that doc-012 names, only
  15-18 and 20 are team routes (21-26 are areas).
- For the human at gate-1: spec OQ-1..OQ-5; at gate-2: design D-1..D-5. Each
  went on with A, the recommended option.

## Log

- 2026-10-17 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-028-org-level-teams
- 2026-10-17 [orchestrator] route L (queue size; doc-012 § Decomposition: breaking per its Breaks cell) — the human chose it in chat («⭐ feat-028, облегчённо»). Light route. Gate-1 and gate-2 go to the human as one package after the design. E2E: no.
- 2026-10-17 [dev] P-10 note from mod-017 (`plan-backend.md` § 11): its last-team guard (OQ-1 A, `organizations.team.last_team_cannot_be_deleted`) and `organizations.team.not_in_project` hold until this task, which reworks them: unlinking or deleting a team reassigns the items it owns (SC-09).
- 2026-10-17 [business-analyst] spec.md + US-1..US-8 (767992cb); OQ-1..OQ-5 left for the human at gate-1, each going on with A.
- 2026-10-17 [requirements-reviewer] the one round of the light route: approved-by-ai, R-1..R-14 minor, all fixed in place (767992cb).
- 2026-10-17 [ux-designer] design.md (abb9dcab), no prototype (D-4); D-1..D-5 left for the human at gate-2, each going on with A.
- 2026-10-17 [orchestrator] the gate-1 + gate-2 package goes to the human; no code before the answer.

## Notes

Summary (from the queue line or an argument): Teams become the organization's (the human, 2026-10-11): names unique in the organization, no team created with a project, descriptions on projects and teams. Project–team links, linked and unlinked from both pages (the project's end stays at projects/:p/settings/teams with the «По умолчанию» mark until the navigation line); unlinking or deleting a team reassigns the items it owns (SC-09); an owning team must be linked; team pages in the organization settings (doc-012 BR-MX-4..-6, -12; US-2; US-3, project–team)
