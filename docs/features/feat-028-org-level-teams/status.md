# feat-028-org-level-teams

- type: feature
- size: L
- branch: feat/028-org-level-teams
- stage: complete
- epic: core/org-access
- deps: mod-017
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: approved (2026-10-17, human in chat: OQ-1 → C, the upgrade drops every team; OQ-2 A, plus an organization home page queued as feat-045 after mod-018; OQ-3..OQ-5 A)
gate-2: approved (2026-10-17, human in chat: design D-1..D-5 «⭐ Оставить все A»)
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
- The human at gate-1 + gate-2 (2026-10-17): OQ-1 C, not the recommended A,
  so the upgrade drops every team, its members, places and the projects'
  default teams, and the owned items go to «Без команды»; OQ-2 A, and the
  organization home page the human asked for besides is feat-045 (after
  mod-018); OQ-3, OQ-4, OQ-5 and D-1..D-5 A.
- For the human at gate-3, each built as the plan recommended (plan-backend
  § 12.3, plan-frontend § 6): D-3, the links are locked `FOR NO KEY UPDATE`
  where ADR-0028 rule 8 reads `FOR UPDATE` (the clarification goes to mod-018's
  amendment); D-4, a defaulting creation locks every link of the project, so
  bug-005 is superseded; D-7, a creation with members also needs `team.write`;
  «Отключить» is natively disabled (aria-disabled is a kit task); the upgrade
  leaves «Изменён» alone.
- The human's answers to the gate-3 package (2026-10-18, before the PR): D-3
  accepted, the ADR-0028 rule 8 clarification goes to mod-018's amendment;
  bug-005 is withdrawn from the queue after the merge; the open minors become
  one S line in the queue; the create-team form's «Участники» searches the
  server now («⛔ Серверный поиск сейчас», not the local filter the plan
  recommended), built as `GET …/teams/member-candidates` (api-contract § 2.3a);
  D-7 and «Изменён» stay as built. Gate-3 itself is the merge.

## Log

- 2026-10-17 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-028-org-level-teams
- 2026-10-17 [orchestrator] route L (queue size; doc-012 § Decomposition: breaking per its Breaks cell) — the human chose it in chat («⭐ feat-028, облегчённо»). Light route. Gate-1 and gate-2 go to the human as one package after the design. E2E: no.
- 2026-10-17 [dev] P-10 note from mod-017 (`plan-backend.md` § 11): its last-team guard (OQ-1 A, `organizations.team.last_team_cannot_be_deleted`) and `organizations.team.not_in_project` hold until this task, which reworks them: unlinking or deleting a team reassigns the items it owns (SC-09).
- 2026-10-17 [business-analyst] spec.md + US-1..US-8 (767992cb); OQ-1..OQ-5 left for the human at gate-1, each going on with A.
- 2026-10-17 [requirements-reviewer] the one round of the light route: approved-by-ai, R-1..R-14 minor, all fixed in place (767992cb).
- 2026-10-17 [ux-designer] design.md (abb9dcab), no prototype (D-4); D-1..D-5 left for the human at gate-2, each going on with A.
- 2026-10-17 [orchestrator] the gate-1 + gate-2 package goes to the human; no code before the answer.
- 2026-10-17 [orchestrator] the human answered: gate-1 and gate-2 approved with OQ-1 → C (see Decisions); feat-045 queued (PR #13). The spec and design take OQ-1 C before the backend plan.
- 2026-10-17 [business-analyst] [ux-designer] the gate answers applied: OQ-1 C into US-7, the spec and the design's empty states; OQ and D-n boxes checked (9a2b2141).
- 2026-10-17 [backend-developer] plan-backend.md + api-contract.md (f2ef1758): one migration `OrganizationLevelTeams`, 47 → 55 routes, races C-1..C-18; D-3 and D-4 for the human.
- 2026-10-18 [backend-developer] backend built (53874646..d41b3896): domain, links, the migration, handler, endpoint, race, volume and statement tests; P-10 notes in mod-002, mod-016, mod-017, mod-006, feat-039; verify green.
- 2026-10-18 [frontend-developer] frontend built (c31be958..324178e9): kit multi-picker and `Tag.removeLabel`, organization «Команды», team page, link and unlink dialogs, project «Команды» and description; verify green.
- 2026-10-18 [qa-engineer] the one QA pass (b99c665f): defects, FD-1 and BD-2 major, BD-1, BD-3, FD-2 minor; 18 backend and 2 frontend tests added; verify green; E2E not run (standing NO).
- 2026-10-18 [code-reviewer] round 1 (f038da61): changes-requested, CR-1..CR-3 major, CR-4..CR-6 minor. Security pass (925e0517): approve, S-1..S-4 minor.
- 2026-10-18 [dev] the fix round: CR-1/BD-2 two retired tests deleted (e7a94ed7), CR-3 comments reworded to links (2862745d), CR-2/FD-1 the unlink alert when the team owns nothing (d2bca8c3).
- 2026-10-18 [code-reviewer] delta read 1 (fa0a216b): approve; DR-1 minor added. Every open finding is a minor, carried to gate-3 as debt (review-code.md, review-security.md, qa.md).
- 2026-10-18 [orchestrator] rebased onto main 7c29b857; `BASE=origin/main bash scripts/verify.sh` green (backend build and tests incl. integration, frontend lint and build, Vitest 2739 affected + 590 guards). Stage complete; gate-3 goes to the human with the PR.
- 2026-10-18 [backend-developer] api-contract.md changed at gate-3 by the human's choice («⛔ Серверный поиск сейчас»): GET …/teams/member-candidates?text= for the creation form's «Участники» (ad241e04); § 2.3a, routes 55 → 56, plan-backend § 12 A-8. The frontend still reads the local filter (`useMemberSearch`) until its stage switches it.
- 2026-10-18 [frontend-developer] the creation form's «Участники» switched to the server search (`useUserSearch`, scope `newTeam`); `useMemberSearch` deleted; route in the API layer, the MSW double and its guard; plan-frontend § 4 and § 6 rewritten.
- 2026-10-18 [code-reviewer] delta read 2 (185c8b49, the gate-3 server member search): approve; DR2-1, DR2-2 minor added. Every open finding is a minor (review-code.md).
- 2026-10-18 [backend-developer] PR #17 CI red on the CR-4/S-1 race (`TeamQaEndpointTests.TheFirstTeamOfATeamlessProject_…`; reproduced here at 150 rounds: the unlink answered 500, SQLSTATE 23001 RESTRICT on `fk_work_items_project_teams_project_id_owning_team_id` at its final `DELETE`): fixed in every binder (6c9720eb, plan-backend A-9 in b814b5f0) — item create and save, default naming, the creation that may default, and the unlink's own judgement of the pair, the path the test hit (binders alone left 4 of 4 runs red); "linked" is what the lock statement returned, api-contract.md unchanged; race test looped 32× at 6 rounds and 4× at 150 rounds, all green, and the window is reached deterministically in `LinkJudgementWindowTests`; no pinned statement count moved (the default naming's link lock went from two statements to one, pinned in `RepositoriesTests`); Architecture 83, Application 753, Infrastructure 377, Api integration Organizations+WorkItems+Boards 1750 green.

## Notes

Summary (from the queue line or an argument): Teams become the organization's (the human, 2026-10-11): names unique in the organization, no team created with a project, descriptions on projects and teams. Project–team links, linked and unlinked from both pages (the project's end stays at projects/:p/settings/teams with the «По умолчанию» mark until the navigation line); unlinking or deleting a team reassigns the items it owns (SC-09); an owning team must be linked; team pages in the organization settings (doc-012 BR-MX-4..-6, -12; US-2; US-3, project–team)
