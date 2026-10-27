# mod-017-work-item-owning-team

- type: module
- size: L
- branch: mod/017-work-item-owning-team
- stage: complete
- epic: core/work-items
- deps: feat-039
- gates: gate-1, gate-2, gate-3

## Gates

gate-1: approved (2026-10-17, human in chat: OQ-1..OQ-7 kept; OQ-8 and OQ-9 changed to B, delivered by mod-020)
gate-2: approved (2026-10-17, human in chat: design D-1..D-7 and plan D-1, D-2, D-5 kept)
gate-3: approved (2026-10-17, the human merged PR #11, https://github.com/totKtoXam/mustask/pull/11)

## Plan

Route L with UI: business analysis → requirements review → gate-1 → UX design →
gate-2 → backend plan (deep, `plan-backend.md`) → backend → QA pass 1 →
frontend → QA pass 2 → one code-review round + the security pass (≤ 2 delta
reads) → gate-3 → PR. Plans: `plan-backend.md`, `plan-frontend.md`.

The human switched the rest of the task to the light route on 2026-10-17
(«упрости mod-017 прямо сейчас»): one QA pass over both layers, run alongside
the code review and the security pass; one fix round; at most one delta read;
whatever is left goes to the human.

- Backend plan (f3dc2b46; `plan-backend.md` D-1..D-16, `api-contract.md`): the
  owning team is `work_items.owning_team_id` under composite keys onto
  `teams (project_id, id)`; `team: {teamId}` on create and save;
  `projects.default_team_id`; a team place is keyed `(work_item_id, team_id)`
  onto the item's owner. Areas own nothing, no team is "first", the last team
  of a project cannot be deleted.
- Routes: `PUT …/default-team` (204), `GET …/teams/{t}/owned-items/count`, the
  repeated `teamId` filter (an id or `none`) on the list, backlog and board;
  the area-owner and default-area routes go (inventory 47 → 47); two new codes
  (`organizations.team.not_in_project`, `…last_team_cannot_be_deleted`), four
  retired ones marked deprecated, entries kept.
- Backend as built (c48296bf domain, 81c07326 migration, 8d2ad641 save,
  82eccfa6 views and creation, 87f95224 filter, 9793611f retirement, b1d740c5
  + 5dd87e40 tests, 8af8c3ca § 12, 944cd1d3 codes in the panel): two additive
  migrations; data dropped = stale team places + the three markers
  (`areas.owner_team_id`, `areas.default_of_team_id`, `teams.is_first_team`).
  Audit follow-up 3116964d, e9c509d9, 0f3acfa1, 4f47af2b (P-10 notes in
  seven owners' `status.md`); fix round 990a49ea (D-14).
- Frontend (`plan-frontend.md`, as built cd15dca1): no new slice. Kit part
  `InlineMultiSelectField` (a6252ade); organizations: marks, «Команда по
  умолчанию», delete dialog with the count (85c35e09); workitems: `TeamField`
  on card and dialog, `TeamFilter`, the list's column (cf8ce0d5); boards: the
  column, card line and project views' filter row (cac56777).
- Frontend simplifications: test doubles moved to the new model once, old
  model tests retired by name, one shared `teamTexts`, no new shared dialog.
- E2E: the paused suite reads the retired model; the specs it breaks are
  listed in `plan-backend.md` "The MR names" (21, 23–29, `organizationsApi.ts`).

## Decisions

- E2E = NO: the standing answer since 2026-10-12; not asked, not run.
- Light route: the human's choice of 2026-10-17, see Plan.
- Decided under the night delegation (the recommended option, for the human),
  `spec.md` (the requirements review contested none):
  - OQ-1 A: keep the count-based guard, a project's last team cannot be deleted until feat-028.
  - OQ-2 A: map each item to the team that owned its area, keep the places naming it, drop the other team places and the markers.
  - OQ-3 A: only «Команда» on rows and cards now; «Проект» comes with feat-040.
  - OQ-4 A: a child keeps taking the parent's area as stored.
  - OQ-5 A: adding a team creates no area.
  - OQ-6 A: «Команда по умолчанию» is a command on the project's «Команды» page.
  - OQ-7 A: the list gets the kit's «Фильтры» band with «Команда» alone.
  - OQ-8 A: the switcher's fallback team is the list's first team by name.
  - OQ-9 A: a filter lives in the page's state for the visit, not in the address.
- `design.md` § 13:
  - D-1 A: «Команда» stands in Block B row 2, under «Область».
  - D-2 A: one new kit multi-select field as the filter on all three surfaces.
  - D-3 A: «Команда» is a kit field in the dialog beside the two legacy fields.
  - D-4 A: the project views' filter is a row of its own, no band chrome.
  - D-5 A: a notice says so when the filter hides a just-created item.
  - D-6 A: a note on the last team's page says why it has no «Удалить команду».
  - D-7 A: a filter change keeps the old rows while it reads.
- `plan-backend.md` § 1:
  - D-1: two additive migrations, not one.
  - D-2: the team is sent as the wrapper `team: { teamId }`, not a flat `teamId`.
  - D-3: team deletion locks `FOR NO KEY UPDATE`; ADR-0028 rule 8 reads `FOR UPDATE`, its text is left as accepted.
  - D-5: the four retired codes are marked `deprecated`, not deleted.
- `review-code.md` CR-6: leave ADR-0028's text, name the rule 8 clarification (D-3) in the MR; mod-018's amendment can carry it.
- Left open by the delta read (approve; no second round), for the human:
  - CR-7 minor: a creation that finds its named default deleted can deadlock with a second deletion (`40P01`, reasoned, not run). The fix is a simplification, one lock of all active teams then one default read, worth a queue line.
  - CR-8 minor: one comment in `CreateWorkItemHandler.cs:136` is broader than the code.
  - CR-3 minor: the project-reorder independence test cannot tell a reorder from a no-op (QA's added test covers it).
  - CR-4 minor: the frontend retirements lack rule ids; give them in the PR text (BR-OT-5 or -10 for the area cases, BR-OT-4 for «Агенты», design § 6.4 for the theme case).
  - CR-5 minor: the plan's three volume test names became `TeamFilterVolumeTests`; the team board is covered by `BoardChecklistsVolumeTests`.
  - S-2 minor: `IOrganizationStructure.FindProjectAsync` / `FindTeamAsync` still read by id across organizations; every caller compares the organization (record in api-contract § 7 or fold into feat-028).
  - QA D-2..D-5: design § 12 still sketches the flat `teamId`; a stale comment in `boards/lib/scope.ts`; the E2E list for the MR; P-1 naming of renamed tests. D-6 (this file) is closed.
  - Not verified (QA F18): layout at 640 px and the card band's stacking, nothing against a live stand.
  - P-10 notes for feat-028 and feat-043: neither has a folder yet; their analysis reads `plan-backend.md` § 11. The PR text names the dropped data, the clarification of ADR-0028 rule 8, the retired and rewritten tests and the broken E2E specs.

## Log

- 2026-10-18 [dev] note from `tech-045-mod020-default-team-prose` (CR-5 of `mod-020-team-switcher-default-and-filter-address`, `review.md` round 2): the spec's flowchart node N3, «it only starts new items», is amended in place: the default team starts new items and, when no team is chosen, is the team the team views' switcher opens (BR-OT-4 and BR-OT-11 as mod-020 amended them). The node said the opposite of BR-OT-4 in the same file. Prose only: no rule, shape, endpoint or code change, and `api-contract.md` is not touched.
- 2026-10-18 [dev] note from `mod-020-team-switcher-default-and-filter-address` (the human's answers to OQ-8 B and OQ-9 B): api-contract § 2.8 now says the project's default team is also the switcher's first choice (no shape or code change on the endpoint; the panel reads the mark from `GET …/teams`), and § 3's "Life" says the filter lives in the page's address too (the wire is unchanged). BR-OT-4, -8, -11, design §§ 6.3 and 8, the spec's fate row and access table, US-3 and US-5 carry amendment notes.
- 2026-10-17 [dev] P-10 note from `feat-028-org-level-teams` (BR-OL-1, -2, -6..-11; its api-contract §§ 0.3, 1, 2.1, 2.4): the project's team (BR-OT-11) and the last-team guard of BR-OT-12 are retired — a team is the organization's; an owning team, a default team or an unlink destination must be LINKED to the project (409 `team.not_in_project` otherwise, a deactivated project's team included), `PUT …/default-team` needs `project.write`, `GET …/owned-items/count` becomes the unlink's `…/unlink-count`, a deletion reassigns in every linked project and the default is resolved over the links; § 1.3 `Team` loses `projectId`, `defaultMark` and `isMember` (they live on `ProjectTeam`); BR-OT-14's merged migration stays.
- 2026-10-17 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/mod-017-work-item-owning-team
- 2026-10-17 [orchestrator] route L (queue size; doc-012 § Decomposition: breaking — retires area ownership, team default areas and the first team's role) taken under the human's night standing approval: no confirmation, gates stay pending for the human, work proceeds and is listed for the morning. E2E: no (paused 2026-10-12).
- 2026-10-17 [orchestrator] stacked on feat/039-project-board (0cf422c2, PR #8 → PR #6), which carries doc-012 and the MessagePack 2.5.303 pin (NU1902 fails main). Stages run as agents through one workflow.
- 2026-10-17 [orchestrator] the human switched the rest of the task to the light route («упрости mod-017 прямо сейчас»): one QA pass alongside review + security, one fix round, one delta read.
- 2026-10-17 [business-analyst] spec.md + US-1..US-7 (b0b1f1cc); OQ-1..OQ-9 decided A for the human.
- 2026-10-17 [requirements-reviewer] round 1: changes-requested, R-1..R-3 major (stale team places, child × deletion, reorder × save), R-4..R-10 minor; all resolved in place by the analyst (b0b1f1cc).
- 2026-10-17 [ux-designer] design.md + prototype (47fffb4a); D-1..D-7 decided A for the human.
- 2026-10-17 [backend-developer] plan + contract f3dc2b46; code c48296bf..944cd1d3 (two migrations, owning team, filter, areas own nothing); the orchestrator stopped that pass at 944cd1d3 for the light route; audit follow-up 3116964d, e9c509d9, 0f3acfa1, 4f47af2b.
- 2026-10-17 [frontend-developer] a6252ade kit multi-select, 85c35e09 panel teams, cf8ce0d5 card, dialog and list, cac56777 project views, cd15dca1 plan as built; one pass, light route.
- 2026-10-17 [security-reviewer] e03f061c: changes-requested, S-1 major (a creation with no default resolved takes no team lock), S-2 minor (by-id reads cross the tenant).
- 2026-10-17 [code-reviewer] round 1 (49ade255): changes-requested, CR-1 major (= S-1), CR-2 major (E2E list undercounted), CR-3..CR-6 minor; delta read (9e38a90d): approve, CR-7 and CR-8 minor left.
- 2026-10-17 [qa-engineer] one pass over both layers (d3de449b): D-1 major (= CR-1, S-1; red test by design), D-2..D-6 minor; 7 backend and 2 frontend tests added; E2E not run.
- 2026-10-17 [backend-developer] fix round 990a49ea: a creation that may default locks every active team FOR SHARE in id order when no default resolves; closes D-1, CR-1, S-1; CR-2 prose fixed.
- 2026-10-17 [orchestrator] `BASE=0cf422c2 bash scripts/verify.sh` at 9e38a90d: RESULT green, exit 0 (.NET 4376 passed, 17 skipped; Vitest 3954 passed). stage: complete; gates left pending for the human.
- 2026-10-17 [orchestrator] #9 merged feat-039 into main: the branch is rebased onto main (3e4f5638) and its PR targets main directly; no stacked base left.
- 2026-10-17 [orchestrator] the human merged PR #11 and answered the open decisions in chat. OQ-1..OQ-7, design D-1..D-7 and plan D-1, D-2, D-5 stay. OQ-8 → B (the switcher falls back to the project's default team) and OQ-9 → B (the team filter in the page address) are queued as mod-020. CR-7 (with CR-8) is queued as bug-005, a separate S task. S-2 joins feat-028's scope. ADR-0028 rule 8's clarification (CR-6, plan D-3) joins mod-018's amendment.
- 2026-10-18 [dev] P-10 note from feat-040-team-surfaces-across-projects: BR-OT-7's «Проект» arrives on every row and card of every team and project view (`project`, feat-040 BR-TS-3); § 3's team views are read at the team's address across the team's projects and take a repeated `projectId` filter beside `teamId` (BR-TS-8, the shared specification's second dimension); BR-OT-5's membership joins the project under the caller's organization (BR-TS-13); BR-OT-11's team order is the switcher's interim organization level, the user's own teams first (BR-TS-10). This api-contract is not edited.

## Notes

Summary (from the queue line or an argument): An explicit, optional owning team on the work item: the card, the creation dialogs, the list's «Команда» column and filter, the project views' team filter; team views hold owned items; the project default team and the creation defaults; retires area ownership, team default areas and the first team's role (doc-012 BR-MX-9..-11, -17; US-4)
