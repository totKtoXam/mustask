# Code review, round 1

`mod-017-work-item-owning-team` (L, with UI, breaking). Diff `8b1be5de..cd15dca1`, read in a clean context. The suite was not
run by the reviewer: QA runs it in this checkout at the same time. The QA pass and the security pass write their own files.

## Round 1

**Verdict: changes-requested.** There are two majors. CR-1 is a code fix plus a test, so QA re-runs over the creation handler,
the default lock and the deletion race test. CR-2 is a prose-only fix, which gets a delta read and no QA. CR-3..CR-6 are minor
and stay open as debts. Light route: one fix round, then at most one delta read.

### Findings

**CR-1 · major · backend: in a 2-team project with no named default, a creation racing a team deletion keeps the stale default.**
`Mustask.Infrastructure/WorkItems/WorkItemRepository.cs:65-67` returns `null` and locks nothing when
`DefaultTeams.ResolveAsync` finds no default. That is the case of two or more active teams with none named.
`CreateWorkItemHandler.cs:144` keeps that `null`. `:185` then gives a child `ChildTeam(parent.OwningTeamId, null)`. The race:
- The project has teams A and B, none named. A child of an A-owned parent resolves the default as `null`.
- `DeleteTeam(A)` takes A and B `FOR NO KEY UPDATE` (`TeamRepository.cs:35`), clears the parent and commits.
- The child's parent lock (`:182`) re-reads the parent with no team. The child is saved «Без команды».
BR-OT-13 (2) says the child "takes the project default as it stands after the deletion". By BR-OT-4 that default is B, the only
team left. A creation with no parent and no team sent has the same gap: BR-OT-3 says such a creation takes the default "as stored
when it is saved". The handler comment at `CreateWorkItemHandler.cs:129-132` claims "a deletion of a team commits before it or
waits for it, never in between (D-14)". Nothing makes that true on this path. The end state is still legal: no key violation and
no item left on a deleted team. `DeleteOwnedItemsEndpointTests.Delete_VersusChildCreation_BothOrders` (`:365`) builds a 3-team
world (OwnTeam, Mars, Venus), so the default stays `null` after the deletion as well and the test cannot see the gap.
**Fix:** when no team is named, `LockDefaultTeamAsync` should lock the project's active teams `FOR SHARE` in id order, then
resolve again. This sits at step (2) of ADR-0028 rule 8, before the number lock. The deletion takes the same rows in the same
order, so it queues behind the lock or the lock queues behind it, and neither can deadlock. Shares do not conflict with each
other or with a binder's share. Add a 2-team case to `Delete_VersusChildCreation_BothOrders`: when the creation commits second,
the child lands on the remaining team. Add a no-parent, no-team counterpart. (The other route, narrowing BR-OT-13 (2), changes
the spec after gate 1. That is the human's call and not recommended.)

**CR-2 · major · docs: the MR's list of broken E2E specs undercounts.** `plan-backend.md:583-585` (MR names) and
`spec.md:144` name only `21-organizations.spec.ts` plus "any spec placing an item on a team through an area". The API no longer
returns `isFirstTeam` or a team's `defaultArea`. `e2e/support/organizationsApi.ts:37` still declares `isFirstTeam: boolean`, so
`tsc` stays green while these specs throw at setup or on first use:
- `23-backlogs-boards.spec.ts:72` and `:310` («The project has no first team.»);
- `24-work-item-hierarchy.spec.ts:59`, `25-work-item-fields.spec.ts:56`, `26-backlog-tree-editing.spec.ts:138`,
  `28-team-members.spec.ts:101` and `29-role-views.spec.ts:105` (`teams.find((team) => team.isFirstTeam)`);
- `27-work-item-card.spec.ts:189` (`platform!.defaultArea.id`);
- `21-organizations.spec.ts:222-231` (the «Область по умолчанию» dialog, and areas passing to the first team).
`19-shell-in-a-browser.spec.ts:64` is a comment only. Anyone who later un-pauses E2E and scopes the repair from the MR's list
would fix one spec and hit eight. **Fix (prose only):** list these specs and the support type by name in plan-backend's
"The MR names", for the orchestrator's MR text. E2E stays paused, so no spec is edited.

**CR-3 · minor · backend test: the independence test cannot tell a project reorder from a no-op.**
`ReorderVersusTeamChangeEndpointTests.cs:201-202`: `ProjectBoardsTestData.ReorderToAsync(.., second, third)` moves each item to
the bottom in turn and ends at `first, second, third`. That is the creation order, which the unplaced rows read anyway. So
`:202` holds whether the project reorder wrote anything or not, and `:203` only shows that the team order survived nothing.
**Fix:** reorder the project to an order that differs from creation, for example `first` alone, giving
`second, third, first`. Assert that order, and assert that the team order is unchanged.

**CR-4 · minor · frontend: P-1 traceability is incomplete for the frontend retirements.** In commit `cac56777`:
- `theme.test.tsx` «fits a 1 440 px window ...» is retired as "superseded by design § 6.4", which is not a rule id;
- `routes.boards.test.tsx` loses the second team «Агенты» with no rule named;
- `BacklogPage.changes.test.tsx` is retired by describe names («An area given another owner takes its items along» and others).
  None of these is in the spec's "Existing rules" table. The table names `BacklogPage.test.tsx` «MSK-US-5 ...» for BR-M4-5 instead;
- "Rewritten" is given by group ("the layout/header/skeleton cases", "the switcher cases"), not by name.
Commits `cf8ce0d5` and `85c35e09` follow P-1. The backend's lists in `9793611f` and `82eccfa6` match the spec's table.
**Fix:** in the gate-3 summary, give each of the four retired cases its rule id (BR-OT-5 or BR-OT-10 for the area cases,
BR-OT-4 for «Агенты», the NFR or design § 6.4 for the theme case), and name the rewritten cases or point to the spec rows.

**CR-5 · minor · docs: the plan's test names were never built under those names.** `plan-backend.md:435`, `:452` and `:513-515`
name `OwnedViewsVolumeTests`, `ListFilterVolumeTests` and `ProjectViewsFilterVolumeTests`. What exists is one class,
`TeamFilterVolumeTests.cs:19`, which covers the team's backlog and the project's list, backlog and board, filtered or not. The
team board at 5 000 is covered by `BoardChecklistsVolumeTests.cs:20,35`, which was re-seeded by owning team. § 12 (deviations)
does not record the merge. **Fix:** add one § 12 line: the three names became `TeamFilterVolumeTests`, and the team board is
covered by `BoardChecklistsVolumeTests`.

**CR-6 · minor · docs: ADR-0028 rule 8 still says the deletion takes the team `FOR UPDATE`.** See `0028-matrix-organization-model.md:149`.
D-3 locks all the project's active teams `FOR NO KEY UPDATE` instead (`TeamRepository.cs:35`). Every guarantee rule 8 states
still holds: binders' `FOR SHARE` queues, and `:155` holds. The change is disclosed in plan-backend's risks and MR names
(`:579`, `:586`), so it is not silent. **Open question,** decided: leave the accepted ADR's text and name the clarification in
the MR (recommended) under the night delegation — for the human. If the human wants the text changed, mod-018's amendment of
ADR-0028 can carry it.

### Behavioural claims checked (item 6)

- `CreateWorkItemHandler.cs:129-132`, "a deletion commits before it or waits for it, never in between" → CR-1.
- spec BR-OT-13 (2), child × deletion → CR-1. It holds when a default is named (`WorkItemRepository.cs:63-80`, re-read loop).
- spec BR-OT-3, "this rule's value as stored when it is saved" → CR-1 (2-team case). Holds otherwise: `CreateWorkItemHandler.cs:144`.
- `WorkItemRepository.cs:46-47`, "a team of another organization locks nothing" → `:50-54` (joins `projects.organization_id`).
- `WorkItemRepository.cs:73-74`, "the next attempt reads the default as it stands after it" → `:63-80`.
- spec BR-OT-13 (1), binder × deletion → `WorkItemRepository.cs:54` `FOR SHARE` against `TeamRepository.cs:35`; refusal `:195`.
- spec BR-OT-13 (3), reorder × save, both orders → `BacklogRepository.cs:37,57,95` (team share, members key-share, locked re-read); four gate-order tests in `ReorderVersusTeamChangeEndpointTests`.
- spec BR-OT-6, "the two orders never affect each other" → anchored in code. Its test is weak → CR-3.
- spec BR-OT-13 (4), two deletions that would empty a project → `Team.cs:67-69` under `TeamRepository.cs:35`, `DeleteTeamHandler.cs:78-84`.
- `Team.cs:56`, "has locked all the project's active teams" → `DeleteTeamHandler.cs:78`.
- spec BR-OT-13, "never a key violation or a server error" → `WorkItemRepository.cs:195` maps the owner-key 23503 to `TeamNotFound`.
- spec BR-OT-14, "only stale team places are dropped" → `20261017075902_RetireAreaOwnership.cs:34` (`IS DISTINCT FROM`).
- `RetireAreaOwnership.cs:14`, "each count is logged with RAISE NOTICE" → `:40`, `:69`; `AddWorkItemOwningTeam.cs:58`.
- plan D-5, "the four retired codes are deprecated, entries kept" → `contracts/errors/organizations.json` (4 × `"deprecated": true`), `docs/error-codes.md:49,72-74`.
- `organizations/hooks/queries.ts:80`, "the next opening counts again" → `:87` `gcTime: 0`.
- design, "«Удалить» waits for the count" → `organizations/components/DeleteTeamDialog.tsx:71` (`confirmDisabled`).
- spec, "the filter is one value set, an id or «Без команды»" → `workitems/api/workItemsApi.ts:303-306` (`teamFilterQuery`, `'none'`).
- `TeamFilterVolumeTests.cs:10-14`, "each served within 2 s" → `:22` with its timed loop. Its names in the plan → CR-5.
- spec NFR, "a team's board of 5 000 within 2 s" → `BoardChecklistsVolumeTests.cs:20,35`.
- plan-backend `:583-585` and spec `:144`, the broken E2E specs → CR-2.
- plan D-3, "a clarification of ADR-0028 rule 8" → CR-6.
- commit lists, "retired by name with a rule id" (P-1) → backend anchored in the spec table; frontend → CR-4.

**Direction 2.**
- **Behaviours swept:** the area owner, a team's default area, the first team, the flat create `teamId`, return-to-place on a team
  order, and `BoardTeam.defaultArea`.
- **Identifiers grepped:** `isFirstTeam`, `defaultArea`, "default area", "first team", "area owner", `ownerTeamId` and
  `default_area`, under `docs/` (outside this feature), `.claude/`, every `AGENTS.md`, `contracts/`, `src/frontend/e2e/` and the
  backend comments.
- **Hits that became findings:** the E2E specs 19, 21, 23–29 and `e2e/support/organizationsApi.ts:37` (CR-2), and
  `0028-matrix-organization-model.md:149` (CR-6).
- **Hits that are not findings:**
  - `0026-soft-deactivation-by-status.md:100-102` and `:174-178` describe areas re-owned to the first team. That text is
    superseded by ADR-0028 § 6 rule 2 (doc-012, already merged), so it is not this diff's.
  - `0029-scoped-effective-permissions.md:118` already lists the default area as a retired name.
  - `docs/runbooks/local-migrations.md:206` and `CreateProjectHandler.cs:36` use "first team" for the team created with the
    project. That team still exists, so the wording still holds.
  - `docs/error-codes.md:72` is a deprecated entry, as intended.
- **Not swept:** the other owners' contracts and stories that the P-10 notes are owed to (`plan-backend.md:587-591`, mod-002,
  -003, -006, -016, feat-027, -028, -039, -043). They are the orchestrator's to annotate, and their stale text is that work list.

**Checked with no finding:**
- Tenant scoping of every team lock and read.
- The lock order of binder, deletion, reorder and default naming against ADR-0028 rule 8 steps (2), (5) and (6).
- The migrations' data mapping and `Down`.
- The route count (47 → 47).
- Frontend query keys, `keepWhileFiltering` and the filter state per project.
- Refusal placement (`workitems/lib/refusals.ts:69,88`).
- The kit guard's counts, which only shrink.
- N+1: `OwningTeamStatementCountTests` pins small = large.
