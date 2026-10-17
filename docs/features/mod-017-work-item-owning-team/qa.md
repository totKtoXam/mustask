# QA — mod-017-work-item-owning-team

One light-route pass over both layers (the human's choice, 2026-10-17), clean
context, code under test `cd15dca1` (the two later commits on the branch are the review and
security documents, no code). The plan was written from `spec.md`, the
seven stories, `api-contract.md` and (for screens) `design.md` **before any
implementation file was read**; the matrix below is that plan, then filled in.
Test names are the method or `it` title (shortened to its first words where long).
St: ✓ covered by the named tests, which pass · ✓ added = QA added the tests · ✗ a defect.
**E2E: standing answer NO (paused 2026-10-12): not asked, not run, no spec written.**

## Backend

### Coverage matrix

| # | Criterion | Covered by | St |
|---|---|---|---|
| B1 | US-1, BR-OT-1/7: item reads, create/save answers, list rows carry `team {id,name}`/null; a rename shows at once | `SaveOwningTeam.Get_Save_AnswerTheTeamIdAndName_OrNone`, `TeamFilter.List_RowsCarryTheTeam`, `OwnedViews.Rows_AndCards_CarryTheTeam_OrNone` | ✓ |
| B2 | BR-OT-2: save sets / clears (`{teamId:null}`, `{}`); absent keeps the stored value; same value does not move `changedAt` | `Save_TitleOnlyWhileTeamChangedMeanwhile_*`, `Save_NoTeam_ThenNoTeamAgain_*`, `Save_TheSameTeamAgain_*`, `Save_EmptyTeamWrapper_IsNone`, `Save_NullInPlaceOfTheWrapper_*` | ✓ |
| B3 | BR-OT-1: a team change moves `changedAt` and nothing else | `Save_SetTeam_ChangesTeamAndChangedAtOnly_*`, `WorkItemOwningTeamTests` | ✓ |
| B4 | BR-OT-2/16: 409 `not_in_project`; 404 for deleted / unknown / malformed / foreign / deactivated-project team, foreign = unknown; nothing stored; no number consumed | `Save_TeamRefusals_StoreNothing`, `Save_ForeignTeam_*`, `Create_TeamRefusals_*`, `Create_ForeignTeam_*`, `ProjectTeamsTests.EnsureBindable_*` | ✓ |
| B5 | §0.4 order of answers; wrong JSON shape of `team` is 400 | `Save_AnswerOrder_*`; **QA** `Bodies_WithATeamOfTheWrongJsonShape_*` | ✓ added |
| B6 | child may differ from parent; assignee outside the team; membership moves move no item | `Save_ChildAndParentTeams_AreIndependent`, `Save_AssigneeOutsideTheTeam_*`, `AreasOwnNothing.Membership_Moves_MoveNoItem` | ✓ |
| B7 | area / state / card move / checklist tick keep the team | `Save_AreaChange_KeepsTheTeam`, `Save_StateByFormAndCardMoveAndChecklistTick_*`, `AreasOwnNothing.Save_ChildArea_ChangesNoTeam` | ✓ |
| B8 | BR-OT-3/4 start value: named, only team, none, named-then-deleted | `Create_NoTeamField_*` (4); **QA** `DefaultTeam_NamedThenDeleted_*` | ✓ |
| B9 | create naming a team: stored; refusals; dialog value wins over a default named meanwhile | `Create_FromTeamSurface_OtherTeamOrNone`, `Create_DefaultNamedWhileTheDialogIsOpen_*`, `Create_FieldOmitted_*` | ✓ |
| B10 | child takes the parent's team, else the default; a `team` in the body is not read; both orders vs the parent's change | `Create_Child_*`, `Create_ChildWithATeamInTheBody_*`, `Create_ChildWhileParentTeamChanges_*` (2), `Delete_VersusChildCreation_*` | ✓ |
| B11 | replay answers 200 before the team is judged, after the team was deleted | `Create_Replay_*`, `Create_TeamDeletedBeforeSubmit_*` | ✓ |
| B12 | created item unplaced on both orders; area = root, child = parent's | `Create_IsUnplacedOnBothOrders`, `Create_FromTeamSurface_OwnedByTheTeam_*`, `AreasOwnNothing.NewItem_TakesRoot_*` | ✓ |
| B13 | `PUT default-team`: name, clear (`null`, `{}`, no body), same again; marks `named` / `onlyTeam` / null; second team ends inference; deleting the named team clears it | `SetDefaultTeam.Name_*`, `Marks_NamedOnlyTeamNone`, `AddTeam.Add_SecondTeam_*`, `Delete_NamedTeam_*`; **QA** `DefaultTeam_NamedThenDeleted_*` | ✓ |
| B14 | default-team: 409 / 404 / 403 (member, reader) / deactivated 404; survives deactivate+restore; moves no item | `Name_Refusals_*`, `Name_Member_*`, `Name_ForeignTeam_*`, `Setting_SurvivesDeactivateAndRestore`, `Name_MovesNoItem_*` | ✓ |
| B15 | team views = owned items of the level outside Removed; project views all; unowned on no team's | `OwnedViews.Views_*` (2), `GetBacklog.Get_HoldsTheTeamsItemsOfTheLevel_*` | ✓ |
| B16 | area rename / move / re-save moves no item | `Views_AreaRenameMoveAndItemAreaSave_*`, `AreasOwnNothing.MoveAndRename_*` | ✓ |
| B17 | children follow the view; checklist not narrowed; `team` on rows/cards | `Backlog_Children_AreOnlyTheViewsOwn_*`, `ProjectBoard_Filter_*ChecklistNotNarrowed` | ✓ |
| B18 | team change drops the place, lands unplaced last, project place kept, stale reorder 409, back = unplaced | `Save_TeamChange_LeavesTheTeamView_*`, `Save_TeamBack_*`, `Reorder_AfterTheItemLeft_IsStale` | ✓ |
| B19 | reorder x team change, both orders, moved item and other member | `ReorderVersusTeamChange` (4), `OwningTeamBackstopTests` | ✓ |
| B20 | orders independent (with two orders that differ from creation order and each other); card move writes only the state | `TeamAndProjectOrders_AreIndependent` (weak, CR-3), `CardMove_WritesOnlyTheState_*`; **QA** `TeamAndProjectOrders_AreIndependent_WhenBothDifferFromCreationOrder` | ✓ added |
| B21 | filter on list / backlog / board: one, several, `none`, mixed; tree over matches; `order` 1..n; counters; surfaces agree | `TeamFilter.*` (list theory x7, backlog, board, `ThreeSurfaces_*` x7) | ✓ |
| B22 | non-team / foreign / other-project / deleted / malformed values select nothing, identical; a valid value among invalid ones matches; `None`, empty do not; paging | `Filter_NonTeamValues_*`, `List_FilterPaging_*`; **QA** `Filter_MixedValues_*` | ✓ added |
| B23 | project reorder takes no filter | `ProjectReorder_WhileFiltered_*`; **QA** `TeamViews_ReadNoFilter_*` | ✓ added |
| B24 | areas: no owner fields; owner / default-area routes 404; delete refused for children / items only | `AreasOwnNothing.RetiredRoutes_*`, `CreateArea`, `DeleteArea`, `GetAreaTree` | ✓ |
| B25 | `Team` has no first / default-area; add team creates no area; project = ordinary team + root owned by nobody | `AddTeam.Add_CreatesAnOrdinaryTeam_*`, `Add_NameOfAnExistingAreaUnderTheRoot_*`, `CreateProject.Create_Returns201_WithOneOrdinaryTeamAndTheRoot` | ✓ |
| B26 | BR-OT-14 migration over legacy data: tree resolution, kept / stale places, markers, `changed_at`, counts, deactivated + deleted team, rollback | `OwningTeamMigrationTests` (4), `RetireAreaOwnershipMigrationTests` (5) | ✓ |
| B27 | teams by name, no first; any team deletable while another remains; last team 409; two at once | `GetTeams.Get_ActiveTeams_ByName_*`, `Delete_TheTeamCreatedWithTheProject_*`, `Delete_LastTeam_*`, `Delete_TwoLastTeamsAtOnce_*` | ✓ |
| B28 | owned count (any state, admin only); deletion reassigns / clears / ends / frees; deactivated 404; deleted team + filter | `GetTeamOwnedItemCount` (4), `Delete_ReassignsEverything_*`, `Delete_InDeactivatedProject_*`; **QA** `DeletedTeam_ItsItemsAreUnderNone_*` | ✓ |
| B29 | writes racing the deletion, both orders, no 5xx, no item on a deleted team; a child takes the default as it stands after the deletion | `Delete_VersusSaveCreateNameReorder_*`, `Delete_VersusCreationOnTheDefault_*`, `Name_VersusDeleteTeam_*`, `OwningTeamBackstopTests`; **QA** `Delete_VersusChildCreation_TwoTeamsNoNamedDefault_*` | ✗ D-1 |
| B30 | 5 000-row views and filter < 2 s; deletion < 5 s, all or nothing; statement counts | `TeamFilterVolumeTests`, `DeleteTeamVolumeTests`, `DeleteTeamAtomicityTests`, `OwningTeamStatementCountTests` (12) | ✓ |
| B31 | route inventory follows +2 / -2; containment | `OrganizationAddressedRoutes`, `OrganizationContainmentTests` | ✓ |
| B32 | two codes registered (ru, kk `kkNeedsReview`), four deprecated, catalog + `error-codes.md` | `OrganizationErrorCatalogTests`, verify's contracts bucket | ✓ |
| B33 | retired by name (P-1), nothing skipped | every name in the fate table checked against the tests: retired ones are gone; kept-and-rewritten ones are the table's "Rewritten" list; no `Skip` / `Ignore` added | ✓ (see D-5) |

### QA's own tests (`OwningTeamQaEndpointTests`, 7)

| Test | Pins |
|---|---|
| `Bodies_WithATeamOfTheWrongJsonShape_*` | save / create / default-team with `teamId: 42` or `team: "<id>"` answer 400 `http.bad_request`, store nothing, consume no number (§0.2) |
| `TeamViews_ReadNoFilter_*` | a team's backlog and board answer the same with `teamId` of another team, `none` or garbage (§2.5) |
| `Filter_MixedValues_*` | a valid id among foreign / other-project / deleted / garbage ids still matches; `none` among them; `None`, `NONE`, empty, ` none`, `null` select nothing; list, backlog and board (§3) |
| `DeletedTeam_ItsItemsAreUnderNone_*` | after a real `DELETE` the items are under `none` on the three surfaces, the deleted id filters like an unknown one, its team views answer 404 |
| `DefaultTeam_NamedThenDeleted_*` | US-3 outline: named, deleted, the first team is `onlyTeam`, the next creation takes it, a third team ends the inference |
| `Delete_VersusChildCreation_TwoTeamsNoNamedDefault_*` | **red by design (D-1)**: BR-OT-13 (2) in a project of two teams and no named default |
| `TeamAndProjectOrders_AreIndependent_WhenBothDifferFromCreationOrder` | BR-OT-6: team order c,a,b then project order b,c,a, then team a,b,c: each leaves the other as it was (the existing test's project order ends on creation order: CR-3) |

### Defects (backend)

**D-1 · major · backend: a child created beneath an item of a team being deleted keeps a stale default when the project has two teams and none is named.**
Same defect as code review CR-1 and the security pass's S-1 (outcome 2); QA reproduced it deterministically.
- Steps: `OwningTeamQaEndpointTests.Delete_VersusChildCreation_TwoTeamsNoNamedDefault_ChildTakesTheTeamThatRemains`. A project of its own team and DOOMED, no default named; a feature owned by DOOMED; `DELETE …/teams/{DOOMED}` held at its commit; `POST …/workitems` of a story with `parentId` = the feature, which waits on the feature's row; release.
- Expected (BR-OT-13 (2), BR-OT-4): the deletion is 204, the feature has no team, the child takes "the project default as it stands after the deletion": the one team left.
- Actual: 204 and 201, the feature has no team, the child is «Без команды» (`Expected: <the remaining team's id> Actual: null`, test line 248). `WorkItemRepository.LockDefaultTeamAsync` returns `null` before taking any lock when no default resolves (`:65-67`); `CreateWorkItemHandler.cs:144,185` keeps that value across the wait on the parent. The existing `Delete_VersusChildCreation_BothOrders` builds three teams, so the default is `null` after the deletion as well and cannot see it.
- Not reproduced here: S-1 outcome 1 (a grandchild left owned by the deleted team) needs three requests at once; the fix's test should add it.
- Fix as CR-1 / S-1: with no default resolved, lock the project's active teams `FOR SHARE` in id order, then resolve again; the red test above is the check, and it must pass with no other change. **Status: open, returned to the backend-developer.** The test is red until then, on purpose.

D-2..D-6 (under "Debts") are documentation or debt, not backend code.

## Frontend

### Coverage matrix

| # | Criterion | Covered by | St |
|---|---|---|---|
| F1 | card: «Команда» under «Область», options «Без команды» first, loading / failed / no match, untouched omitted, undo, reader value only | `WorkItemCard.team.test.tsx` (8) | ✓ |
| F2 | refusal 409 / 404 under the field, choice kept, focus, no frame; 403 generic | same, `it.each` | ✓ |
| F3-4 | dialog: field between «Тип» and «Заголовок», start value, skeleton / failed + «Повторить», sends what it shows, caption line (none for «Задача»), refusals under «Команда» | `CreateWorkItemDialog.test.tsx` (mod-017 block, 6 + `it.each`) | ✓ |
| F5 | row «+» and «Добавить задачу» send the parent and no team | `ProjectBacklogPage.test` («+» on an epic…), `ProjectBoardPage.test` («Добавить задачу»…) | ✓ |
| F6 | list: eight headers, column order, filter band, options, clear, empty-after-filter, page 1, keep rows while reading, failure banner, reader, notice when the filter hides the new item | `WorkItemsPage.test.tsx` (mod-017 blocks, 9) | ✓ |
| F7 | project views: create command per role, three empty states per role, board «Новый элемент» | `ProjectBacklogPage.test`, `ProjectBoardPage.test`, `ProjectViews.team.test` US-2 block | ✓ |
| F8 | filter row only on project views, survives re-read / move / focus / level, dropped with the page; reorder announced among visible rows and sent against the full list; deleted chosen team stays and selects nothing | `ProjectViews.team.test` (US-5 blocks, 9); **QA** `ProjectBacklogPage.reorderFiltered.test` (2) | ✓ added |
| F9 | backlog column after «Статус», 120 px token, board card line + hidden prefix | `ProjectViews.team.test` (BR-OT-7 block), `theme.test` | ✓ |
| F10 | «Команды»: one column, marks, menu, no delete on the last team, «Команда по умолчанию» for the administrator only, dialog | `TeamsPage.test` (mod-017 blocks), `OrganizationsPages.roles.test` | ✓ |
| F11 | delete dialog: counting, failed + «Повторить», count / no count text, focus «Отмена», notices, refusals; team page marks + last-team note | `TeamsPage.test`, `TeamPage.test` | ✓ |
| F12 | areas without owner / default; dialogs and helpers; deleted `DefaultAreaDialog` | `AreasPage.test`, `AreaDialogs.test`, `ProjectListPage.create.test`, `TeamsPage.test` | ✓ |
| F13 | order by name; switcher fallback = first by name, own teams first | `useOrganizationsContextSource.defaultTeam.test`, `.test` | ✓ |
| F14 | wire matches `api-contract`: `team:{teamId}`, repeated `teamId` + `none`, `PUT default-team`, `GET owned-items/count`, no removed route | read `workItemsApi.ts`, `boardsApi.ts`, `organizationsApi.ts`; `organizationsApi.test`, `boardsApi.test`, `ProjectViews.team.test` | ✓ |
| F15 | new kit part: test, showcase entry, guide text, kit guard | `InlineMultiSelectField.test` (7), `FieldsDemo`, `registry.ts`, `ui-kit.md` §4-5, `kitGuard.test` | ✓ |
| F16 | accessible names (design §11), Escape, keyboard | `InlineMultiSelectField.test`, card and dialog tests (`combobox`, `option`) | ✓ |
| F17 | retired tests by name; the two new codes in the panel | `DefaultAreaDialog.test` removed; `errorCodes.organizations.test` | ✓ |
| F18 | 640 CSS px, hover / sticky / container-query layout, card band stacking under 720 px | cannot be shown in jsdom; E2E is NO | not checked |

Design strings spot-checked by `grep` in the source: the 22 notices, hints, helpers and
captions tried from design §§ 4-8 are all present verbatim.

### Defects (frontend)

No defect found in the panel. The minor D-3 below is a stale comment in it.

## Runs

| What | Result |
|---|---|
| `BASE=0cf422c2 bash scripts/verify.sh`, once, from a tree holding QA's first four backend tests and the two frontend ones (the later three backend tests were added after the .NET step had built) | **RESULT: green.** Docs checks ok; backend build ok, 0 warnings; .NET tests: Shared 53, Application 709, Architecture 83, Contracts 244, Domain 658, Infrastructure 325 (2 m 37 s, Testcontainers), Api.IntegrationTests 2296 passed, 17 skipped (the live-Keycloak facts, as before), 0 failed (8 m 35 s); frontend lint ok, `tsc -b` + `vite build` ok; Vitest affected: 260 files, 3954 passed, 6 skipped (existing), 0 failed (648 s); source-reading guards: 13 files, 590 passed. Notice only: a `status.md` Log line over 300 characters (D-6) |
| QA's backend tests, `dotnet test … --filter OwningTeamQaEndpointTests` | five of the seven: passed (four on the first run, all five after the default-team test was added). `TeamAndProjectOrders_AreIndependent_WhenBothDifferFromCreationOrder`: 1 / 1 passed. `Delete_VersusChildCreation_TwoTeamsNoNamedDefault_*`: **1 / 1 failed, as D-1 says** (5 s) |
| QA's 2 frontend tests, `npx vitest run …reorderFiltered.test.tsx` | 2 / 2 passed; `prettier --check` clean |
| E2E | not asked, not run (standing NO); no stand started, no process left running |

## Debts (minor, recorded, carried to gate-3; none is sent back)

- **D-2** (docs, design): `design.md` §12 still says create / save carry a flat `teamId` and the reads take `teamIds`; `api-contract.md` §0.2 and §3 (wrapper `team:{teamId}`, repeated `teamId`) are what is built and what the panel sends. Fix: one sentence in design.
- **D-3** (stale comment, frontend): `boards/lib/scope.ts` line 2 still reads "one team's, over the items its areas own (mod-006)".
- **D-4** (E2E, for the MR's "Deliberately left open"): the paused suite reads the retired model and fails when un-paused. The list is wider than the spec's, and code review CR-2 holds it by file and line: `21`, `23`, `24`, `25`, `26`, `27-organization-members`, `27-work-item-card`, `28`, `29` and `e2e/support/organizationsApi.ts` (`isFirstTeam`, still declared, so `tsc` stays green). Not run (E2E NO). QA agrees with CR-2 and adds nothing.
- **D-5** (P-1 hygiene): the fate table says a rewritten test keeps its name, but some were renamed (commit messages, plan-backend §12 item 9), and `GetAreaTreeHandlerTests.Handle_EveryNode_EqualsWhatAreaTreeResolves` is in the table's Retired list yet kept, rewritten without owners (intent unchanged). Code review CR-4 adds the frontend retirements without a rule id.
- **D-6** (orchestrator): this task's `status.md` is at `stage: 01-business-analysis`, and verify's docs check prints a notice for a Log line over 300 characters.

Carried to the MR as data dropped (BR-OT-14): the stale team places (`backlog_places` rows whose team is not the item's mapped owner) and the three markers (`areas.owner_team_id`, `areas.default_of_team_id`, `teams.is_first_team`); nothing else.

## Verdict

blockers: 0 · majors: 1 (D-1, open, backend) · minors: 5 · result: **fail (defects)**. One fix round: D-1 with
CR-1 / S-1 (one change, the red test is its check), then a delta read. Not verified: layout at 640 px and the card
band's stacking (F18), anything against a live stand (E2E NO).
