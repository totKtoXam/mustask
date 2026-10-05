# QA — mod-002-organizations-projects-teams

## Backend

Pass 1, 2026-10-04, qa-engineer (deep tier, L task), clean context. The
plan below was written from `spec.md`, the stories US-1..US-8 and
`api-contract.md` before any line of the implementation, its tests or
`plan-backend.md` was read, and is committed on its own (cfe3fd2). The
Status column is filled in afterwards, from the test code and the runs. What
a screen shows or offers is the Frontend section's (stage 7); a row marked
"stage 7" has no backend half. The diff under test is this task's own:
`git diff mod/001-process-seed...HEAD` (D-v). Tests added by this pass are
marked **(QA)**. Test names below are methods of `<UseCase>EndpointTests`
(`Mustask.Api.IntegrationTests/Features/Organizations`) unless another
class is named.

Stand step owed by D-j: the realm import changes (client role
`organizations.organization.create`, realm role `organization-creator`,
group «Создатели организаций», `admin` gains the code), so any stand used
for the frontend QA or the E2E run of this task is reset first with
mod-014's reset script (or, without it, the Keycloak volume is dropped and
the stand is brought up from nothing). This backend pass starts no stand.

### Coverage matrix

| Criterion (spec) | Check | Test type | Status |
|-----------------|----------|-----------|--------|
| US-1 / 1 An organization is created with its process | POST with the right: 201, `Location: /api/organizations/{id}`, body `{id,name}`, id lowercase canonical; listed by § 1.1; mod-001's process read answers 200 for it with the seeded process; the in-process read gives `ProcessId` = that process, `CreatedBy` = the token's `sub`, `CreatedAt` ≈ now; its project list is `[]` | integration | ☑ CreateOrganization `Create_WithRight_Returns201_ListedActive_ProcessRecorded_CreatorKept`; GetProjects `Get_NewOrganization_ReturnsEmpty`; `OrganizationContextFilterTests.ProcessRead_ActiveOrganization_PassesTheFilter` |
| US-1 / 2 The name is trimmed and its length checked | «  X  » stored «X»; empty, spaces only, `null`, 101 characters → 400 `organizations.validation_failed`, `errors.name[0].code` = `organizations.name_length` with the Russian text; 1 and 100 characters (100 Cyrillic) accepted; 100 characters plus surrounding spaces accepted; nothing created on a refusal | integration + unit | ☑ `Create_PaddedName_StoresTrimmed`, `Create_HundredCharactersPaddedWithSpaces_Returns201`, `Create_InvalidName_Returns400NameLength_CreatesNothing` (null, "", spaces, 101), `Create_NameMissing_…`; `EntityNameTests` (1 and 100 on the bound, tab/newline only, 101) |
| US-1 / 3 The name is unique instance-wide, deactivated included | «пример организации » against an active and against a deactivated «Пример организации»: 409 `organization.name_taken`, `params.name` = the stored «Пример организации», the two bodies identical apart from trace fields | integration | ☑ `Create_NameOfActiveOrDeactivated_Returns409WithStoredName_SameBodyForBoth` |
| US-1 / 4 Two simultaneous creations of one name | two creations released together: one 201, one 409 `name_taken` with the submitted trimmed name; one organization; process count = organization count (no orphaned seed) | integration (concurrency) | ☑ `Create_TwoConcurrentSameName_OneCreated_OtherRefused_NoOrphanProcess` (commit gate + `PgLocks`) |
| US-1 / 5 A failed seed leaves nothing behind | the seed throws: 500 `organizations.organization.create_failed` (not `http.internal_error`); no organization, no process; the same name then succeeds | integration | ☑ `Create_SeedFails_Returns500CreateFailed_NothingLeft_RetrySucceeds` |
| US-1 / 6 Without the right the action is refused | token without the code (and a token with every other realm code): 403 `organizations.organization.create_forbidden`, never `http.forbidden`; nothing created. "Not offered" is stage 7 | integration | ☑ `Create_WithoutRight_Returns403CreateForbidden_CreatesNothing`, **(QA)** `Create_EveryCodeButTheCreateRight_Returns403CreateForbidden_CreatesNothing`; `ForbiddenErrorCodeTests` (own code here, `http.forbidden` kept elsewhere) |
| US-1 / 7 The right is granted and revoked in the console | realm import: `organization-creator` is a composite of exactly that client role; group «Создатели организаций» maps it; `admin` carries it. A user who created with the right and later holds a token without it still reads and renames that organization | contract + integration | ☑ `OrganizationCreateRightRealmTests` (6); `Create_RightIsReadFromTheTokenOnly_EarlierOrganizationSurvives`; the realm's panel client keeps the `basic` scope, so its tokens carry `sub` (A-19) |
| US-1 / 8 No screen grants the right | the OpenAPI document holds exactly the 26 routes of § 5 (+ mod-001's read) under `/api/organizations`; none writes rights, roles or groups. Screens are stage 7 | integration | ☑ `OrganizationsOpenApiTests` (3) |
| US-1 / 9 No organization yet | § 1.1 answers `[]` on an empty instance, for a user with and without the right. Copy and hint are stage 7 | integration | ☑ at unit level: `GetOrganizationsHandlerTests.Handle_NoActiveOrganization_ReturnsEmpty`, `…_OnlyDeactivatedOrganizations_ReturnsEmpty` (the shared integration database is never empty) |
| US-1 / 10 Every signed-in user sees every organization | two creators, a third user without the right: lists both, GETs both with 200 | integration | ☑ GetOrganizations `Get_UserWithoutRight_SeesOrganizationsOfOtherCreators`; GetOrganization `Get_UserWithoutRight_Opens` |
| US-1 / 11 An anonymous request is refused | every route of § 5 and mod-001's read, no token: 401 `http.unauthorized`, nothing created or changed; 401 also wins over a malformed id; expired / foreign-issuer / foreign-audience token: 401 | integration | ☑ `OrganizationContextFilterTests.Anonymous_GetsTheChallengeBeforeTheNotFound` (24 addressed routes, malformed id); `*_NoToken_*` in every class; invalid tokens: the same JWT pipeline, pinned by mod-001's `Get_InvalidToken_Returns401WithNoProcessData` |
| US-2 / 1 The organization settings | backend half: § 1.4 answers the organization. The rest is stage 7 | integration | ☑ `Get_Active_ReturnsIdAndName` |
| US-2 / 2 The organization is renamed | 200, listed under the new name; projects, teams, areas byte-identical before and after; renaming to its own name in another letter case → 200 and stored as sent (trimmed) | integration | ☑ `Rename_FreeName_Returns200_ListShowsNewName`, `Rename_LeavesProjectsTeamsAreasUnchanged`, `Rename_OwnNameInAnotherCase_Allowed` |
| US-2 / 3 A rename to a taken or invalid name | «Бета» → «альфа»: 409 `name_taken`, `params.name` = «Альфа»; to a deactivated organization's name: same 409; empty / 101: 400 `name_length`; the name is unchanged after each | integration | ☑ `Rename_ToTakenNameAnyCase_Returns409WithStoredName_NameUnchanged` (active, deactivated), `Rename_InvalidLength_…` |
| US-2 / 4 Two renames to one free name | «Альфа» and «Бета» renamed to «Гамма» together: one 200, one 409; exactly one «Гамма» | integration (concurrency) | ☑ `Rename_TwoConcurrentToSameFreeName_OneWins` |
| US-2 / 5 The organization is deactivated | 204; leaves § 1.1, joins § 1.2; § 1.4, § 2.1, § 2.2, § 2.4, § 3.1, § 3.2, § 4.1 and mod-001's process read answer 404 `organization.not_found`; rows under it unchanged (read back after restore) | integration | ☑ `Deactivate_Returns204_LeavesActiveList_JoinsDeactivatedList`, `Deactivate_ThenEverythingBeneath_AnswersOrganizationNotFound`, `Deactivate_DeletesNothing_RowsBeneathUnchanged`; `GetProcessDefinitionEndpointTests.Get_DeactivatedOrganization_Returns404_RestoredAnswersAgain` |
| US-2 / 6 A deactivated organization takes no change | every write route of § 5 except § 1.7 (rename, deactivate again, project create/rename/deactivate/restore, team add/rename/default/delete, area create/rename/move/owner/delete): 404 `organization.not_found`, nothing changes | integration | ☑ `OrganizationContextFilterTests.EveryOrganizationRoute_DeactivatedOrganization_Returns404OrganizationNotFound_ChangesNothing` (23 routes; the filter answers before any handler, whatever ids follow); `Deactivate_AlreadyDeactivated_…` |
| US-2 / 7 An unknown organization answers not found | unknown id, malformed values (no hyphens, braces, all zeros, garbage, over-long, Cyrillic) on every organization-addressed route and mod-001's read: 404 `organization.not_found`; the canonical id in upper case is accepted | integration | ☑ `EveryOrganizationRoute_MalformedOrUnknownOrganization_…` (24 routes × 3 values); `Restore_Unknown_…` (malformed, zeros, no hyphens, unknown, a project's id); `AddressedIdTests`; mod-001's 12-value theory; `Get_UpperCaseId_ReturnsTheOrganizationWithItsLowerCaseId` |
| US-2 / 8 Listed separately, with no purge | § 1.2 lists it; `DELETE /api/organizations/{id}` is no operation (405/404, nothing deleted); its name is still taken (US-1 / 3) | integration | ☑ GetDeactivatedOrganizations (5 methods); `OrganizationsOpenApiTests.OpenApiDocument_OrganizationOperations_AreExactlyTheContracts`; US-1 / 3 |
| US-2 / 9 Restored exactly as it was | restore → 200 `Organization`; back in § 1.1; projects, teams, default areas, trees, process identical to before deactivation; mod-001's read answers 200 again; a project deactivated before the organization stays in § 2.2 and out of § 2.1 | integration | ☑ `Restore_BringsBackNameProcessProjectsTeamsDefaultsAndTrees`, `Restore_ProjectDeactivatedOnItsOwn_StaysDeactivated`, `Restore_ProcessReadAnswersAgain` |
| US-3 / 1 A project is created ready to use | 201, `Location`, body `{id,organizationId,name,key}`; § 2.1 lists name and key; § 3.1 = one team named after it, `isFirstTeam`, `defaultArea` = root with `path` = project name; § 4.1 = the root alone, named after it, `ownerTeamId` = first team, `defaultOfTeam` = first team; in-process: `FirstTeamId`, `RootAreaId`, `CreatedBy` = `sub`, `CreatedAt` | integration | ☑ `Create_Returns201_WithFirstTeam_RootOwnedByItAndItsDefault`, `Create_RecordsCreator`, `Create_UsesTheOrganizationsProcess`; `OrganizationStructureTests.FindProject_ReturnsFirstTeamRootAndReachability` |
| US-3 / 2 The key is required and well-formed | `null`, `""`, absent → `key_required`; `prm`, `1PRM`, `P`, `PROJECTKEY01`, `РМК` → `key_invalid`; boundaries `PR` and `ABCDEFGHIJ` accepted, 11 characters refused; not trimmed (` PRM`, `PRM ` refused); `PRM\n` refused; name and key both bad → both field reasons in one 400; nothing created | integration + unit | ☑ `Create_KeyMissingOrMalformed_Returns400WithTheKeyReason` (8 values), `Create_KeyAbsentFromTheBody_…`, `Create_NameAndKeyBothBad_Returns400WithBothReasons`; `ProjectKeyTests` (`AB`, `ABCDEFGHIJ`, `ABCDEFGHIJK`, `PRM\n`, ` PRM`, `PRM `; the pattern ends in `\z`); the database check `ck_projects_key_format` |
| US-3 / 3 The key is unique in the organization, deactivated included | `PRM` taken by an active and by a deactivated project: 409 `project.key_taken`, `params.key` = `PRM`; `PRM` free in «Бета» | integration | ☑ `Create_KeyOfActiveOrDeactivated_Returns409KeyTaken`, `Create_SameKeyInAnotherOrganization_Succeeds` |
| US-3 / 4 Two projects with one key at the same moment | released together: one 201, one 409 `key_taken`; one project with `PRM` | integration (concurrency) | ☑ `Create_TwoConcurrentSameKey_OneCreated_OtherRefused_NothingOrphaned` (also the name race) |
| US-3 / 5 The name is unique in the organization | create or rename to «пример проекта »: 409 `project.name_taken`; free in «Бета»; empty / 101: 400 | integration | ☑ `Create_NameTakenAnyCase_…`, `Create_SameNameInAnotherOrganization_Succeeds`, RenameProject `Rename_ToTakenName_Returns409NameTaken_ChangesNothing_FreeInAnotherOrganization`, `*_InvalidName_*` |
| US-3 / 6 A deactivated project's name stays reserved | create and rename to it: 409 `project.name_taken_by_deactivated`; name and key both taken → the name's code | integration | ☑ `Create_NameOfDeactivated_…`, `Rename_ToNameOfDeactivated_…`, `Create_NameAndKeyBothTaken_Returns409NameTaken` |
| US-3 / 7 Renaming a project renames its root and keeps its key | 200; root renamed; key, teams (first team keeps the old name), the rest of the tree unchanged; a `key` in the rename body is ignored; no route changes a key (OpenAPI inventory) | integration | ☑ `Rename_RenamesRoot_KeepsKeyTeamsAndTree`, **(QA)** `Rename_BodyAlsoCarryingAKey_RenamesOnly_TheKeyStays`; `OpenApiDocument_OnlyProjectCreation_TakesAKey` |
| US-3 / 8 The project settings | backend half: § 2.4, § 3.1, § 4.1. The rest is stage 7 | integration | ☑ GetProject, GetTeams, GetAreaTree |
| US-3 / 9 The «Процесс» link | stage 7; backend half: no route under a project writes anything of the process (inventory) | — | ☑ backend half: `OrganizationsOpenApiTests`; the link is stage 7 |
| US-3 / 10 A project is deactivated | 204; leaves § 2.1, joins § 2.2; § 2.4, § 3.1, § 3.2, § 4.1 → 404 `project.not_found`; every write under it (rename, deactivate again, team and area writes) → 404 `project.not_found`, nothing changes | integration | ☑ `Deactivate_Returns204_LeavesActiveList_JoinsDeactivated`, `Deactivate_ThenReadsBeneath_Return404ProjectNotFound` (4 reads), `Deactivate_Twice_…`, `Deactivate_DeletesNothing_…`; `*_UnderDeactivatedProject_Returns404AndChangesNothing` in each of the 11 write classes beneath a project |
| US-3 / 11 A project id that names no project of the organization | malformed, unknown, and the id of a project of «Бета» under «Альфа», on every project-addressed route: 404 `project.not_found` | integration | ☑ `*_ProjectNotOfOrganization_Returns404ProjectNotFound` (malformed, unknown, of another organization; GetTeam's is `Get_ProjectNotReachable_…`) in all 16 project-addressed classes |
| US-3 / 12 A deactivated project is restored exactly as it was | § 2.2 lists it; restore → 200 `Project`; back in § 2.1 with name, key, teams, default areas and tree identical; no DELETE route for a project | integration | ☑ `Restore_BringsBackKeyTeamsDefaultsAndTree`, `Restore_WritesOnlyThatProject_…`; GetDeactivatedProjects (7 methods); OpenAPI inventory |
| US-4 / 1 A second team comes with its own area | 201, `Location`, `Team`; § 3.1 = first team, then «Платформа»; the root's child «Платформа» with `ownerTeamId` = the team and `defaultOfTeam` = the team; `defaultArea.path` = «Платформа» | integration | ☑ `Add_CreatesTeam_WithRootChildOwnedAndDefault` |
| US-4 / 2 A team's name is unique among the project's active teams | add and rename to «платформа »: 409 `team.name_taken` (not `area.name_taken`, though the root also has «Платформа»); free in another project; empty / 101: 400; a team named after its project → `team.name_taken` | integration | ☑ `Add_NameTakenAnyCase_Returns409TeamNameTaken_CreatesNothing` (incl. «пример проекта»), `Add_SameNameInAnotherProject_Succeeds`, RenameTeam `Rename_ToTakenName_…`, `*_InvalidName_*` |
| US-4 / 3 A new team's area name must be free under the root | root has «Аналитика», no team of that name: 409 `area.name_taken`; no team, no area added | integration | ☑ `Add_RootHasChildOfThatName_Returns409AreaNameTaken_CreatesNothing` |
| US-4 / 4 Two teams of one name at the same moment | released together: one 201, one 409; one team, one root child «Платформа» | integration (concurrency) | ☑ `Add_TwoConcurrentSameName_OneTeamAndOneArea`; **(QA)** `Add_ConcurrentWithARootChildAreaOfTheSameName_OneRefused_NoTeamWithoutItsArea` (both orders: team vs a root child area of its name, NFR BR-M1-9) |
| US-4 / 5 A team is renamed | 200; § 3.1 and § 4.1's `resolvedOwner`/`defaultOfTeam` show the new name; its areas keep their names; ownership and default unchanged; renaming the first team leaves the root's name alone | integration | ☑ `Rename_FreeName_…`, `Rename_KeepsAreaNamesOwnershipAndDefault`, `Rename_FirstTeam_Returns200_DefaultStaysTheRoot_RootKeepsItsName` |
| US-4 / 6 The default area is an area the team owns explicitly | explicitly owned → 200, `defaultArea` moves; inherited-only → 409 `team.default_area_not_owned`; another team's → same 409; the root for a later team → same 409; another project's area, `null`, malformed, unknown → 404 `area.not_found`; the current default again → 200, nothing changes; the default unchanged after every refusal | integration | ☑ `Set_ExplicitlyOwnedArea_Returns200`, `Set_InheritedOrOtherTeamsArea_…` (inherited, another team's, the root), `Set_AreaOfAnotherProject_…`, `Set_AreaNullAbsentMalformedOrUnknown_…`, `Set_CurrentDefaultAgain_…`, `Set_AreaIdIsANumber_Returns400BadRequest` |
| US-4 / 7 The first team's default is the root, for good | PUT default-area on the first team → 409 `team.first_team_default_fixed`, also with an unknown `areaId` (§ 0.4 step 2 before step 4); the root stays its default | integration | ☑ `Set_FirstTeam_Returns409FirstTeamDefaultFixed` (current, another owned, unknown, malformed, null) |
| US-4 / 8 The project's teams | § 3.1: first team first, the rest in display order, each with `defaultArea`; `isFirstTeam` only on the first. The marker copy is stage 7 | integration | ☑ `Get_ActiveTeams_FirstTeamFirstThenDisplayOrder_WithDefaultAreaPaths` |
| US-4 / 9 A team id that names no team of the project | malformed, unknown, a team of another project of the same organization and of another organization: 404 `team.not_found` on § 3.2, § 3.4, § 3.5, § 3.6 | integration | ☑ `*_TeamNotAnActiveTeamOfTheProject_*` / `Set_TeamNotOfProject_*` in GetTeam, RenameTeam, SetTeamDefaultArea, DeleteTeam |
| US-5 / 1 The tree is shown through the tree registry | backend half: § 4.1 shape — pre-order, root first, `parentId` null only for the root, `depth`, `path`, siblings in display order. The registry is stage 7 | integration | ☑ `Get_ReturnsPreOrderNodes_WithPathsDepthsAndResolvedOwners`, `Get_NodeShape_IsExactlyTheContracts`, `Get_SiblingsInDisplayOrder_RussianCollation_IgnoringCaseAndYo` |
| US-5 / 2 The root comes with the project and follows it | rename, move, delete, set owner, clear owner on the root → 409 `area.root_is_fixed`, the root unchanged; root rename with an empty name and root move to an unknown parent still → `root_is_fixed` (§ 0.4 step 2) | integration | ☑ RenameArea `Rename_Root_…` (incl. ""), MoveArea `Move_Root_…` (incl. an unknown parent), DeleteArea `Delete_Root_…`, SetAreaOwner `Set_Root_…` (re-own, clear, unknown team) |
| US-5 / 3 A tree with only the root | § 4.1 answers the root alone. Copy is stage 7 | integration | ☑ `Get_NewProject_IsTheRootAlone` |
| US-5 / 4 A child area is created under any node | «Отчёты» under the root, «Квартальные» under it: 201, `Location`, `Area`; § 4.1 path «Отчёты / Квартальные», depth 2 | integration | ☑ `Create_UnderRootAndUnderChild_Returns201_TreeShowsThePath` |
| US-5 / 5 Sibling names unique, ignoring case and spaces | create and rename to «квартальные » → 409 `area.name_taken`; free under another parent; empty / 101 → 400; renaming to its own name in another case → 200 | integration | ☑ `Create_SiblingNameAnyCase_…`, `Rename_ToSiblingName_…`, `Rename_OwnNameInAnotherCase_Returns200`, `*_InvalidName_*` |
| US-5 / 6 Two creations of one sibling name | released together: one 201, one 409; one «Квартальные» | integration (concurrency) | ☑ `Create_TwoConcurrentSameSiblingName_OneCreated_OtherRefused_OneExists` |
| US-5 / 7 A child area is renamed | 200; its children and its owner unchanged | integration | ☑ `Rename_KeepsChildrenAndOwner` |
| US-5 / 8 A node moves with its whole subtree | «Отчёты» under «Архив»: paths and depths of the subtree follow; no «Отчёты» under the root; under its current parent → 200, nothing changes | integration | ☑ `Move_CarriesTheWholeSubtree_…`, `Move_UnderTheCurrentParent_Returns200_ChangesNothing` |
| US-5 / 9 A move needs a free name at its destination | 409 `area.name_taken`; tree unchanged | integration | ☑ `Move_NameTakenAtDestination_…`; **(QA)** `Move_ConcurrentWithACreationOfItsNameAtTheDestination_OneRefused_BothOrders` |
| US-5 / 10 Never under itself or a descendant | under itself, under a child, under a grandchild → 409 `move_into_own_subtree`; two crossing moves released together → one refused, the tree still one tree (every node reaches the root) | integration (+ concurrency) | ☑ `Move_UnderItselfOrADescendant_…` (3), `Move_TwoConcurrentFormingACycle_OneMoved_OtherRefused_BothOrders` |
| US-5 / 11 A node never leaves its project | move under, and create under, an area of another project → 404 `area.not_found`; both trees unchanged | integration | ☑ `Move_ParentNotAnAreaOfTheProject_…`, `Create_ParentNotAnAreaOfTheProject_…_BothTreesUnchanged` |
| US-5 / 12 Only an empty leaf is deleted | a parent → 409 `area.not_empty`; the leaf → 204, gone from § 4.1; deleting it again → 404 `area.not_found`; no restore route | integration | ☑ `Delete_Leaf_Returns204_Gone_NeighboursStay_NoRestore`, `Delete_NonLeaf_…_DeletableOnceEmpty`, `Delete_AreaNotOfProject_…` (unknown) |
| US-5 / 13 A leaf deleted while a child is put under it | delete vs create-child under it, and delete vs move-under-it, each released together: one refused, no area under a deleted parent | integration (concurrency) | ☑ `Delete_ConcurrentWithCreateUnderIt_…_BothOrders`, `Delete_ConcurrentWithMoveUnderIt_…_BothOrders` |
| US-6 / 1 A child area with or without an owner | owner = a team → explicit; `null` / absent → inherited; a deleted team → 404 `team.not_found`. The picker is stage 7 | integration | ☑ `Create_WithOwnerOrInheriting_…`, `Create_OwnerNotAnActiveTeamOfTheProject_…` (incl. deleted) |
| US-6 / 2 Inheritance from the nearest owned ancestor | «Платформа» explicit, «API» and «API / Шлюз» none: both `resolvedOwner` = «Платформа», `ownerInherited` true; a chain with no explicit owner resolves to the first team | integration + unit | ☑ `Get_InheritedOwnersMarked_RootFallsBackToFirstTeam`; `AreaTreeTests` |
| US-6 / 3 Other modules get the same answer | `IOrganizationStructure.FindAreaOwnerAsync` for «Шлюз» = «Платформа», inherited, same as § 4.1; `ProjectInfo.RootAreaId` = the root | integration | ☑ `Seam_FindAreaOwner_EqualsTheTreesResolvedOwner_ForEveryNode`, `Seam_ProjectRootArea_IsTheTreesRoot`; `OrganizationStructureTests` |
| US-6 / 4 An area is re-owned | «Отчёты» → «Платформа»: 200; «Квартальные» resolves to «Платформа», inherited; the same owner again → 200, nothing changes | integration | ☑ `Set_ReownsAndChildrenInherit_Returns200`, `Set_NothingToChange_…` |
| US-6 / 5 An explicit owner is cleared | `null` and absent `ownerTeamId` both clear; «Отчёты» and its children resolve to the first team, inherited | integration | ☑ `Set_Clear_ResolvesToRootOwner_Inherited` (null, absent) |
| US-6 / 6 A move re-resolves inherited owners and keeps explicit ones | as the story | integration | ☑ `Move_ReResolvesInheritedOwners_KeepsExplicitOnes` |
| US-6 / 7 The owner is a team of the area's own project | set owner and create with a team of another project, same organization and another organization → 404 `team.not_found`; nothing changes | integration | ☑ `Set_TeamOfAnotherProjectOrOrganizationOrDeleted_…`, `Create_OwnerNotAnActiveTeamOfTheProject_…`. The database refuses it too since the D-1 fix |
| US-6 / 8 A team's default area stays with its team | re-own to another team, clear, delete → 409 `area.is_team_default`, `params.team` = «Платформа»; after the default moves to «Отчёты», all three are allowed | integration | ☑ `Set_ReownOrClearADefaultArea_Returns409IsTeamDefault`, `Delete_DefaultArea_Returns409IsTeamDefault_NamingTheTeam`, `Set_AfterDefaultMoved_…`, `Delete_FormerDefaultArea_AfterTheDefaultMoved_Returns204` |
| US-6 / 9 The root's owner is fixed | see US-5 / 2 | integration | ☑ `Set_Root_Returns409RootIsFixed` |
| US-6 / 10 Default change vs re-own or delete, at the same moment | each pair released together: one refused; the team's default is an existing area it owns explicitly | integration (concurrency) | ☑ `Set_ConcurrentWithReown_OneRefused_BothOrders`, `Set_ConcurrentWithDelete_OneRefused_BothOrders`, `Set_TwoConcurrentDefaultChanges_Serialized_…` |
| US-7 / 1 A team is deleted, its areas pass to the first team | 204; gone from § 3.1; its explicit areas (default included) `ownerTeamId` = first team; inherited ones resolve to the first team; names and places unchanged; `defaultOfTeam` no longer names it; no restore route. The dialog copy is stage 7 | integration | ☑ `Delete_ReownsExplicitAreasToFirstTeam_InheritedFollow_NamesAndPlacesKept`, `Delete_TeamLeavesTheList`; GetAreaTree `Get_AfterTeamDeleted_ItsAreasBelongToTheFirstTeam_NoDefaultOfIt`; OpenAPI inventory |
| US-7 / 2 Declining the confirmation changes nothing | stage 7 | — | n/a |
| US-7 / 3 The first team cannot be deleted | 409 `team.first_team_cannot_be_deleted`; nothing changes | integration | ☑ `Delete_FirstTeam_Returns409FirstTeamCannotBeDeleted_ChangesNothing`; the database check `ck_teams_first_team_active` |
| US-7 / 4 The deleted team's name is free, its area's is not | add «Платформа» → 409 `area.name_taken`; rename the area, add again → 201 with a new root child «Платформа» as its default | integration | ☑ `Add_NameOfDeletedTeam_WhileRootChildRemains_…`, `Add_NameOfDeletedTeam_AfterAreaRenamed_Succeeds`, `Delete_FreesTheTeamName_TheAreaKeepsItsOwn` |
| US-7 / 5 A deleted team answers not found | § 3.2, rename, default-area, delete again, set it as an area's owner, create an area owned by it → 404 `team.not_found`; nothing changes | integration | ☑ `Delete_ThenEveryTeamOperationAndOwnerBinding_Returns404TeamNotFound` (read, rename, default, delete, owner); `Create_OwnerNotAnActiveTeamOfTheProject_…` (deleted) |
| US-7 / 6 Other modules read the deleted status | `ListActiveTeamsAsync` leaves it out; `FindTeamAsync` → `Status = Deleted`, `DefaultAreaId = null`, never `null` | integration | ☑ `Delete_Seam_FindTeamSaysDeleted_ListActiveTeamsOmitsIt`; `OrganizationStructureTests.FindTeam_DeletedTeam_…` |
| US-7 / 7 Re-own to a team vs that team's deletion | both orders forced: one refused, or the deletion passes «Отчёты» to the first team; no area owned by a deleted team in any order (D-r) | integration (concurrency) | ☑ `Delete_ConcurrentWithReownToIt_DeletionFirst_…`, `…_ReownFirst_BothSucceed_AreaEndsWithFirstTeam` |
| US-8 / 1 The switcher lists active organizations, projects, teams | backend half: § 1.1 without the deactivated, alphabetical; § 2.1 without the deactivated project, alphabetical; § 3.1 first team first, without the deleted team | integration | ☑ `Get_ActiveOnly_InDisplayOrder_YoBesideYe_LetterCaseIgnored` (organizations), GetProjects `Get_ActiveOnly_InDisplayOrder`, GetTeams `Get_ActiveTeams_…`, `Get_DeletedTeamLeftOut` |
| US-8 / 2 The default team is the first team | backend half: § 3.1's first element is `isFirstTeam` | integration | ☑ as US-4 / 8 |
| US-8 / 3..13 «Все организации», «Нет организаций», settings navigation, address sync, switches, remembered selection, fallback, own changes, failed load | stage 7 | — | n/a |
| BR-M1-20 / outermost not-found | unknown organization + unknown project → `organization.not_found`; deactivated organization + deactivated project → `organization.not_found`; project restore under a deactivated organization → `organization.not_found`; deactivated project + deleted team → `project.not_found` | integration | ☑ the filter tests (unknown organization, fresh ids below it); GetTeam `Get_OrganizationNotActive_…`, `Get_ProjectNotReachable_Returns404ProjectNotFound_EvenWithAWrongTeam`; RestoreProject `Restore_UnderDeactivatedOrganization_…` (deactivated and active project) |
| BR-M1-20, D-g / the filter covers every organization-addressed route | an inventory that fails when a route under `/api/organizations/{organizationId}` lacks the filter; mod-001's process read is under it | integration | ☑ `OrganizationContextInventoryTests` (3) |
| BR-M1-3, contract § 9 / mod-001's read and the removed demonstration seed | the old demonstration id answers 404; no `DemonstrationSeed*` code, event or doc line remains | integration + contract | ☑ `GetProcessDefinitionEndpointTests` (a process with no organization row answers 404); a search of `src/` finds no `Demonstration*` code, and `src/backend/AGENTS.md` says there is no seeded organization |
| § 6 / the 23 error codes | `contracts/errors/organizations.json` holds exactly § 6's codes with status, params, ru as in § 6, kk present and `kkNeedsReview`; `docs/error-codes.md` regenerated; `OrganizationErrorCodes` matches | contract | ☑ `OrganizationErrorCatalogTests` (contract table both ways, generated reference); 23 entries, each `kkNeedsReview: true` |
| § 8 / registrations | `PermissionCatalog` has the code (instance level); realm import as US-1 / 7 | unit + contract | ☑ `PermissionCatalogTests`, `OrganizationCreateRightRealmTests`, `DictionaryPermissionsRealmTests` (D-u's named exception) |

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|----------|----------|--------|
| A-1 | Name uniqueness with Cyrillic case: «ПРИМЕР» vs «пример», for organization, project, team, area; and the same pair created concurrently (the database's guard alone, not the handler's pre-check) | 409 in both; one row after the race. A `lower()` index under the C collation lowercases ASCII only — the race shows it | ☑ not a risk here: the indexes hold `normalized_name`, computed in .NET (`EntityName.Normalize`, trim + `ToUpperInvariant`), so the database's locale plays no part; `EntityNameTests.Normalized_…` («Ёлка»/«ёЛКА»); every `*_AnyCase_*` test; the races of US-1 / 4, US-4 / 4, US-5 / 6 hit the same index |
| A-2 | Names holding `%` and `_` («A_B» exists, create «AxB»; «100%» exists, create «100x») | 201: no LIKE/ILIKE wildcard in a uniqueness check | ☑ by reading: every uniqueness check is an equality on `normalized_name` (the four repositories); no `Like`/`ILike` in the module |
| A-3 | Names with inner whitespace and other trims: «Пример  проекта» (two spaces) vs «Пример проекта»; a tab or newline around a name | inner spaces are significant (distinct names); surrounding whitespace trimmed | ☑ `EntityName` trims (`string.Trim`, any white space) and keeps inner spaces; `EntityNameTests` (tab/newline only fails). Observation, not a defect: the 1–100 bound counts UTF-16 code units (`EntityName` doc, plan § 3), so a name of 51 emoji is refused; the panel's `.length` counts the same way |
| A-4 | Display order: «ёлка», «Ель», «еда», «Ежевика», «ель» | Russian collation, Ё with Е, lower before upper on a tie, then id (§ 0.6), in § 1.1, § 2.1, § 3.1 and siblings of § 4.1 | ☑ the four `*_InDisplayOrder_*` tests; `RussianCollationTests` (Inf) |
| A-5 | Id forms in path and body: upper-case canonical, no hyphens, braces, all zeros, 2000 characters, a JSON number for `areaId` / `parentId` / `ownerTeamId` | upper case accepted; the rest the level's not-found; a number → 400 `http.bad_request` | ☑ `AddressedIdTests`; `*_UpperCase*` tests; `*_IsANumber_Returns400BadRequest` (areaId, parentId ×2, ownerTeamId, name) |
| A-6 | § 0.4 order: validation (step 3) before a body not-found (step 4): POST area with an empty name and an unknown parent | 400 | ☑ `Create_InvalidName_Returns400NameLength_BeforeTheReferences` |
| A-7 | § 0.4 order: owner team before parent (step 4): POST area with an unknown owner and an unknown parent | 404 `team.not_found` | ☑ `CreateAreaHandlerTests.Handle_OwnerNotAnActiveTeamOfTheProject_IsTeamNotFound_BeforeTheParent`, `…_LocksTheOwnerThenTheParent_…` |
| A-8 | § 0.4 order: a body not-found before a 409: set owner of a team's default area to an unknown team | 404 `team.not_found`, not `is_team_default` | ☑ by reading `SetAreaOwnerHandler` (the team is resolved in step 4, `EnsureNotDefault` runs in `Area.SetOwner` after it) and `Set_Root_…` "re-own to an unknown team" for step 2 |
| A-9 | § 0.4 order: path before an object rule: the root's id under a deactivated project, rename | 404 `project.not_found`, not `root_is_fixed` | ☑ every area handler answers the project before it loads the area; `*_UnderDeactivatedProject_*` |
| A-10 | Project create with the name taken and the key taken | the name's code (§ 2.3) | ☑ `Create_NameAndKeyBothTaken_Returns409NameTaken` |
| A-11 | `…/deactivated` vs `{id}` routing: `GET /api/organizations/deactivated` and `GET …/projects/deactivated` | the list, not a 404 for an id «deactivated» | ☑ GetDeactivatedOrganizations, GetDeactivatedProjects |
| A-12 | Deactivate twice; restore an active organization / project; restore an unknown id | 404; 200 with no change; 404 | ☑ `Deactivate_AlreadyDeactivated_…`, `Deactivate_Twice_…`, `Deactivate_TwoAtTheSameMoment_…`, `Restore_ActiveOrganization_IsANoOp`, `Restore_ActiveProject_IsANoOp`, `Restore_Unknown_…` |
| A-13 | A team whose deleted predecessor's area was moved off the root | the name is free: 201 | ☑ by reading `AddTeamHandler`: the area check looks at the root's children only; `Add_NameOfDeletedTeam_AfterAreaRenamed_Succeeds` is the same path |
| A-14 | The deleted team's default area after the deletion | owned by the first team, `defaultOfTeam` null, can be re-owned, cleared and deleted | ☑ `Get_AfterTeamDeleted_ItsAreasBelongToTheFirstTeam_NoDefaultOfIt`; the cascade clears `default_of_team_id` (`ReassignExplicitAreasAsync`) |
| A-15 | Area created with an owner team that is deleted at the same moment (NFR, BR-M1-18) | no area ends owned by a deleted team | ☑ **(QA)** `Create_WithAnOwnerTeamBeingDeleted_NoAreaOwnedByADeletedTeam_BothOrders`: deletion first → `team.not_found`; creation first → the deletion passes the new area to the first team |
| A-16 | A write under an organization (or project) that is deactivated at the same moment (rename a project, add an area) | the write is either before the deactivation or refused; spec: "nothing under it can change until it is restored" | ☑ accepted by ADR-0026 rule 8 (no lock; a write whose check passed is ordered before the deactivation, which writes one row). A write to the addressed object itself re-checks after its row lock (`RenameOrganizationHandler`, `RenameProjectHandler`) |
| A-17 | In-process reads under a deactivated container: `FindProjectAsync`, `FindTeamAsync`, `FindAreaOwnerAsync` | the object with its own status (`Active`) and `IsReachable = false`; `FindOrganizationAsync` of a deactivated one: `Status = Deactivated`; unknown id → `null` | ☑ `OrganizationStructureTests` (5), `Seam_FindAreaOwner_UnderDeactivatedProject_IsUnreachable_…`, `Deactivate_KeepsTheOrganization_SeamReadsItWithItsStatus` |
| A-18 | Volumes (NFR): a 500-node, 10-level tree; tens of organizations, projects, teams | § 4.1 and the lists within 2 s, each node with its resolved owner | ☑ `ReadModelsTests.AreaTree_GetNodes_FiveHundredNodesTenLevels_TwoQueries` (query count); **(QA)** `Get_FiveHundredNodesTenLevels_ReturnsWithinTwoSeconds_EveryNodeResolved`, **(QA)** `Get_ThirtyTwoTeams_ReturnsWithinTwoSeconds_FirstTeamFirst` (over HTTP, after a warm-up). The organization and project lists are one ordered query each and are not timed |
| A-19 | The creator record when the token lacks `sub`, and whether the stand's realm import issues `sub` | creation does not store an empty creator; the realm's tokens carry `sub` | ☑ by reading: `HttpContextCurrentUser.SubjectId` throws on an authenticated request without `sub` rather than record «anonymous», and `Organization.EnsureCreator` refuses an empty creator; `CurrentUserTests.SubjectId_*` cover the claim and the anonymous request; the realm's `mustask-panel` keeps the `basic` scope, which carries `sub` |
| A-20 | Over-long bodies: a 10 000-character name | 400 `name_length`, not 500 | ☑ by reading: `EntityName.TryCreate` bounds the trimmed length before anything is stored; the 101 cases take the same path |
| A-21 | Responses: `application/problem+json`, `errorCode`, Russian `detail`, `params` only for the coded params, `errors` only with `validation_failed` | as § 0.5 | ☑ the media type in CreateOrganization and ForbiddenErrorCode tests; `detail` and `params` asserted across the classes |
| A-22 | Migration `AddOrganizationStructure` on a clean database; Down then Up; no pending model changes; the idempotent script both ways | applies, rolls back to mod-001's last migration, re-applies | ☑ `MigrationsTests.Migrator_AddOrganizationStructure_CreatesTheFourTables_AndRollbackRemovesThem`; Runs: `has-pending-model-changes`, both scripts |
| A-23 | Database guards on their own: unique indexes (organization name, project name and key per organization, active team name per project, sibling area name), the one-root and default-area checks, restrict keys | each refuses a direct violating insert | ☑ `OrganizationsSchemaTests` (9 unique indexes, 12 checks, 5 restrict keys); the cross-project gap D-1 is closed by the composite keys |
| A-24 | A move that makes an area a child of a team's default area, then the team is deleted | areas keep places; ownership passes as US-7 / 1 | ☑ by reading: the deletion is one `UPDATE … WHERE owner_team_id = team` and never touches `parent_id` |
| A-25 | A move race beyond the story's pair: a move of X under Y while Y is moved under X's descendant (three nodes) | still one tree | ☑ by reading: every move in a project takes the project row `FOR NO KEY UPDATE` first and reads the tree after it (`MoveAreaHandler`), so moves serialize whatever their shape; the two-node race exercises that lock |

### Runs

All on 2026-10-04 in this worktree, against Testcontainers PostgreSQL; no
stand was started. The developer's `verify.sh` on 3dfe838 (1981 tests,
green) was not repeated on its own: the final `verify.sh` below runs the
whole suite once.

| Date | What was run | Result |
|------|-----------------|-----------|
| 2026-10-04 | `dotnet build Mustask.sln` | exit 0, 0 warnings |
| 2026-10-04 | `dotnet test Mustask.Api.IntegrationTests --filter` the seven QA cases of A-15, US-4 / 4, US-5 / 9, US-3 / 7, plus a three-case temporary probe for D-1 (raw SQL in a rolled-back transaction; removed, never committed) | exit 1 by design: 7/7 QA cases passed; the probe's 3 cases failed because the database accepted each cross-project row (1 row each) → D-1 |
| 2026-10-04 | `dotnet test Mustask.Api.IntegrationTests --filter` the two QA volume cases of A-18 | exit 0, 2/2 (the 502-node tree test took 674 ms end to end, building the tree included; the 32-team test 6 s, mostly the 30 sequential additions) |
| 2026-10-04 | `dotnet test Mustask.Api.IntegrationTests --filter` the QA case of US-1 / 6 | exit 0, 1/1 |
| 2026-10-04 | `dotnet ef migrations has-pending-model-changes --project src/backend/Mustask.Infrastructure` | exit 0, "No changes have been made to the model since the last migration." |
| 2026-10-04 | `dotnet ef migrations script AddProcessDefinitions AddOrganizationStructure --idempotent` and the reverse | exit 0 both; Up: 4 tables, 9 unique and 3 plain indexes, 11 checks, no `xmin` column emitted; Down: drops exactly `areas`, `teams`, `projects`, `organizations` |
| 2026-10-04 | `bash scripts/verify.sh` on the QA additions plus this qa.md and the status.md Log line: links, backlog ids, status.md budget, spec hygiene, agent configuration, build, the whole backend suite | exit 0, RESULT: green; Shared 53, Domain 197, Architecture 68, Contracts 89, Application 535, Infrastructure 168, Api.IntegrationTests 881 — 1991 passed, 0 failed, 0 skipped. Only this cell and the Log line's result were written after it |

### Defects

#### D-1: The database accepts an area that refers to another project's team or area

- Severity: minor (no handler writes it today; defence in depth)
- Steps: two organizations A and B, each with a project, its root and a
  second team (`CreateStructureAsync`), and a child area `leafA` of A's
  root. On the database, in a transaction:
  1. `INSERT INTO areas (id, project_id, parent_id, name, normalized_name, owner_team_id, default_of_team_id) VALUES (gen_random_uuid(), '<project A>', '<root A>', 'Чужая', 'ЧУЖАЯ', '<second team of B>', NULL);`
  2. `INSERT INTO areas (…) VALUES (gen_random_uuid(), '<project A>', '<root B>', 'Чужая', 'ЧУЖАЯ', NULL, NULL);`
  3. `UPDATE areas SET owner_team_id = '<second team of B>' WHERE id = '<leafA>';`
- Expected / Actual: refused, as BR-M1-22 ("nothing in one organization
  refers to anything in another"; "a team or an area never moves between
  projects") is an invariant the database can express, and every other
  structural rule of this task has a backstop of its own
  (`OrganizationsSchemaTests`) / each statement succeeds, 1 row (Runs, row
  2). `fk_areas_teams_owner_team_id`, `fk_areas_teams_default_of_team_id`
  and `fk_areas_areas_parent_id` reference `id` alone, so only the handlers
  hold the rule. The same shape lets `organizations.process_id` name a
  process seeded for another organization id. mod-001's QA D-2 was the same
  gap in the process tables, and was closed with a composite key.
- Suggested fix (the developer's call): `UNIQUE (project_id, id)` on
  `teams` and `areas`, and composite keys `areas (project_id,
  owner_team_id)` and `areas (project_id, default_of_team_id)` →
  `teams (project_id, id)`, `areas (project_id, parent_id)` →
  `areas (project_id, id)` (MATCH SIMPLE leaves the nullable columns
  unchecked when null), in `AddOrganizationStructure` while it is
  unmerged, with a schema-test case each; or "won't fix" recorded with the
  reason.
- Status: fixed by the developer (status.md D-w) in this task's own
  migration `AddOrganizationStructure`, regenerated and still the only one:
  `teams` and `areas` gained the key `(project_id, id)` and the parent,
  owner and default keys of `areas` are composite onto them, shown by
  `OrganizationsSchemaTests.Write_AreaReferringToAnotherProject_IsRefusedByItsCompositeKey`
  (QA's three steps plus a parent and an owner from another project of the
  same organization; all five failed before, pass after) beside its
  same-project control `Write_AreaReferringToItsOwnProject_IsAllowed`;
  `organizations.process_id`, named above in passing, is outside D-w and
  unchanged.

### Verdict

blockers: 0 · majors: 0 · minors: 1 (D-1 fixed by the developer) · result: pass

## Frontend

Pass 1, 2026-10-04, qa-engineer (deep tier, L task), clean context. The
plan below was written from `spec.md`, the stories US-1..US-8, `design.md`
and `api-contract.md` before any line of the panel's implementation, its
tests, its E2E specs or `plan-frontend.md` was read, and is committed on its
own. The Status column is filled in afterwards. Rows marked "backend" were
settled by the Backend section above and are not repeated. Tests added by
this pass are marked **(QA)**. E2E: consented (status.md D-l); the run that
counts is on a stand brought up from nothing after the D-j reset (realm
import changed), the whole suite plus this task's specs, and its stand
record is quoted from `src/frontend/e2e-results/stand.json`.

### Coverage matrix

| Criterion (spec / design) | Check | Test type | Status |
|-----------------|----------|-----------|--------|
| US-1 / 1 An organization is created | «Создать организацию» opens «Новая организация» (field «Название», helper «От 1 до 100 символов», first field focused, `form`, Enter submits); «Создать» sends `POST /api/organizations {name}`; the dialog closes, `/organizations/:id/settings/projects` opens with «Пока нет проектов» + «Создать проект», notice «Организация «<название>» создана.», the switcher selects it by the address | component + e2e | ☐ |
| US-1 / 2 Name trimmed, length checked | on submit: empty, spaces only, 101 characters → «Название» helper «Название должно содержать от 1 до 100 символов», `aria-invalid`, no request; cleared as the field is edited; 100 characters with surrounding spaces accepted; no `maxLength` on the field | component | ☐ |
| US-1 / 3, / 4 Name taken (also the race loser) | 409 `organization.name_taken` → the server's text «Название «Пример организации» уже занято» as «Название»'s helper text, dialog open, input kept; placed by the code, not the text | component (+ e2e failure path) | ☐ |
| US-1 / 5 A failed seed | 500 `organization.create_failed` → dialog `Alert` «Не удалось создать организацию. Попробуйте ещё раз», the name stays, a second «Создать» succeeds | component | ☐ |
| US-1 / 6 Without the right | no «Создать организацию» in the header or the empty frame without `can('organizations.organization.create')`; a 403 `create_forbidden` shows «Недостаточно прав» in the dialog `Alert` | component + e2e (member user) | ☐ |
| US-1 / 7 Granted and revoked | the action follows the token's codes on render (token renewal); no screen part beyond that | component | ☐ |
| US-1 / 8 No screen grants the right | no route, entry or action of the slice writes rights, roles or groups | by reading | ☐ |
| US-1 / 9 No organization yet | active tab empty: holder → frame «Организаций пока нет», hint «Создайте первую организацию кнопкой «Создать организацию» выше.», action; non-holder → hint «Право на создание организаций выдаёт администратор», no action; frame is `h2` | component | ☐ |
| US-1 / 10 Every user sees every organization | the active table «Активные организации» lists what § 1.1 returns, in the order received, each name a link to its «Проекты» | component + e2e | ☐ |
| US-1 / 11 Anonymous | the shell's auth card (mod-015); the slice adds nothing | shell / e2e | ☐ |
| US-2 / 1 Organization settings | `/organizations/:o/settings/general`: h1 «Общие», trail «Настройки / Организация», facts «Название»; «Переименовать», «Деактивировать» only once loaded | component | ☐ |
| US-2 / 2 Renamed | «Переименование организации»: prefilled and selected, «Сохранить» disabled for the very same string, enabled for another letter case; `PUT …/name`; notice «Организация переименована.»; facts and switcher show the new name without a reload | component + e2e | ☐ |
| US-2 / 3, / 4 Rename refused | 409 `name_taken` → «Название» helper; length → helper, no request; name unchanged | component | ☐ |
| US-2 / 5 Deactivated | confirmation «Деактивировать организацию?» with § 6.4's text, contained non-error primary, focus on «Отмена»; on success `/organizations/deactivated` with it listed; switcher falls back with its notice, or «Нет организаций» + «Организация «<название>» деактивирована.»; its pages and mod-001's «Процесс» answer «Организация не найдена» | component + e2e | ☐ |
| US-2 / 6 No change to a deactivated organization | a dialog answered 404 `organization.not_found` shows «Организация не найдена.» in its `Alert`; closing refetches the page, which shows the frame | component | ☐ |
| US-2 / 7 Unknown organization | unknown and malformed `:o` on «Общие», «Проекты» (both tabs), project pages, mod-001's «Процесс» → frame «Организация не найдена» (`h1`, hint «Проверьте адрес или вернитесь на главную.», «На главную»), no settings navigation, switcher unchanged | component + e2e | ☐ |
| US-2 / 8 Listed separately, no purge | «Деактивированные» tab: table «Деактивированные организации», name as plain text, «Восстановить «<название>»», no delete action; frame «Деактивированных организаций нет» + hint when empty | component | ☐ |
| US-2 / 9 Restored | «Восстановить» sends at once; that row's button busy and disabled, other rows usable; the row leaves, notice «Организация «<название>» восстановлена.», back in the active tab and the switcher; focus to the next / previous row's button or the selected tab; on failure the row stays, focus on its button, error `Snackbar` with the server's text | component + e2e | ☐ |
| US-3 / 1 Project created | «Новый проект»: «Название» (focused) and «Ключ» (`autoComplete=off`, `spellCheck=false`, `autoCapitalize=characters`) with § 7.4's helpers; `POST …/projects {name,key}`; the row appears in place; switcher project level gains it; notice «Проект «<название>» создан.» | component + e2e | ☐ |
| US-3 / 2 Key required, well-formed | empty → «Укажите ключ проекта»; `prm`, `1PRM`, `P`, `PROJECTKEY01`, `РМК` → the format text; not trimmed, not upper-cased; no request; server field reasons `key_required` / `key_invalid` and `name_length` each land on their own field, both at once | component | ☐ |
| US-3 / 3, / 4 Key taken | 409 `key_taken` → «Ключ» helper «Ключ «PRM» уже используется в организации» | component (+ e2e failure path) | ☐ |
| US-3 / 5, / 6 Name taken | 409 `project.name_taken` and `name_taken_by_deactivated` → «Название» helper, in create and rename | component | ☐ |
| US-3 / 7 Renamed | «Переименование проекта» with § 8.3's text; notice «Проект переименован.»; switcher and the tree's root show the new name | component | ☐ |
| US-3 / 8 Project settings | `/…/projects/:p/settings/general`: «Название», «Ключ» plain text + caption «Задан при создании и не меняется», «Типы рабочих элементов» «Задаются процессом организации: » + link «Процесс»; nothing edits the key; «Команды» and «Области» are settings entries | component | ☐ |
| US-3 / 9 «Процесс» link | `href` `/organizations/:o/settings/process`, same tab; the page writes nothing of the process | component + e2e | ☐ |
| US-3 / 10 Deactivated | «Деактивировать проект?» with § 8.4's text (name and key); lands on `…/settings/projects/deactivated` with it listed; switcher falls back or «Нет проектов» + «Проект «<название>» деактивирован.»; its pages answer «Проект не найден» | component + e2e | ☐ |
| US-3 / 11 Project of another organization | unknown, malformed, another organization's project id under `:o` → frame «Проект не найден» | component | ☐ |
| US-3 / 12 Restored | «Деактивированные» tab of «Проекты» (trail «Настройки / Организация / Проекты»), «Восстановить «<название>»», notice «Проект «<название>» восстановлен.» | component | ☐ |
| US-4 / 1 A second team | «Добавить команду» → «Новая команда» with § 9.4's helper; row appears; switcher team level gains it; notice «Команда «<название>» добавлена.» | component + e2e | ☐ |
| US-4 / 2 Team name taken | 409 `team.name_taken` → «Название» helper (add and rename); length client-side | component | ☐ |
| US-4 / 3 Area name taken under the root | 409 `area.name_taken` on add → «Название» helper «Область с таким названием уже есть на этом уровне» | component | ☐ |
| US-4 / 4 Two teams at once | as / 2 and / 3 | component | ☐ |
| US-4 / 5 Renamed | «Переименование команды», helper «Названия областей команды не изменятся.»; notice «Команда переименована.»; the switcher and the tree's owner cells show the new name | component | ☐ |
| US-4 / 6 Default area among explicit areas | «Область по умолчанию»: text with the team's name; options = nodes whose `ownerTeamId` is the team, by path, tree order (inherited ones not offered); `Skeleton` while the tree loads, error `Alert` + «Повторить» on a failed read; «Сохранить» disabled until changed; 409 `default_area_not_owned` → «Область» helper; notice «Область по умолчанию изменена.» | component | ☐ |
| US-4 / 7 The first team's default is fixed | no «Изменить область по умолчанию» in its row menu, no «Изменить» on its page; caption «Корневая область проекта. У первой команды область по умолчанию не меняется.» | component | ☐ |
| US-4 / 8 The project's teams | table «Команды», columns «Команда», «Область по умолчанию», order received; first team's chip «Владеет корневой областью»; row menu «Действия: <название>» | component + e2e | ☐ |
| US-4 / 9 Team not of the project | unknown, malformed, deleted, another project's `:t` → frame «Команда не найдена» with the settings navigation kept and «Команды» current | component | ☐ |
| US-5 / 1 Tree registry | `shared/ui/TreeRegistry` names nothing of areas, owners or teams; `role="treegrid"`, rows with `aria-level`, `aria-setsize`, `aria-posinset`, `aria-selected`, `aria-expanded`; root expanded, others collapsed; keyboard model § 11.6 (arrows, Home/End, Enter/Space, Shift+F10, roving `tabIndex`) | unit/component + by reading | ☐ |
| US-5 / 2 The root | its row menu offers «Новая дочерняя область» only | component | ☐ |
| US-5 / 3 Root only | the root row, then frame `h2` «Вложенных областей пока нет», hint, action «Новая область» | component | ☐ |
| US-5 / 4 Child created | «Новая область» (under the root) and «Новая дочерняя область» (selected row; disabled until a row is selected); dialog with «Родительская область: <путь>», «Название», «Владелец» («Наследовать от родительской области» preselected, then active teams); `POST …/areas {parentId,name,ownerTeamId}`; notice «Область «<название>» создана.»; new row selected and focused, its parent expanded | component + e2e | ☐ |
| US-5 / 5, / 6 Sibling name taken | 409 `area.name_taken` → «Название» helper; length client-side | component | ☐ |
| US-5 / 7 Renamed | «Переименование области»; notice «Область переименована.»; the row keeps focus | component | ☐ |
| US-5 / 8 Moved with its subtree | «Перемещение области» with its text; `Autocomplete` over every path except the node, its subtree and its current parent, root marked «Корневая область», «Ничего не найдено»; «Переместить» disabled until chosen; notice «Область «<название>» перемещена.»; the tree refetches; the moved row selected under its expanded new parent | component | ☐ |
| US-5 / 9, / 10 Move refused | 409 `area.name_taken` and `move_into_own_subtree` → «Новая родительская область» helper | component | ☐ |
| US-5 / 11 Never leaves the project | both pickers list this project's areas and teams only | component | ☐ |
| US-5 / 12 Only an empty leaf | «Удаление области» destructive, text «Удалить область «<название>»? Восстановить её будет нельзя.», focus on «Отмена»; 409 `not_empty` → dialog `Alert`; 204 → row gone, notice «Область «<название>» удалена.», parent selected and focused | component + e2e | ☐ |
| US-5 / 13 Leaf deleted while a child is put under it | the loser's refusal in its dialog | component | ☐ |
| US-6 / 1 Create with or without an owner | owner picker: «Наследовать…» + active teams in § 4.6 order; inherit sends `null` | component | ☐ |
| US-6 / 2 Inherited owner shown | «Владелец» cell: resolved owner + caption «унаследовано»; accessible text «Платформа унаследовано»; default areas carry chip «По умолчанию» | component | ☐ |
| US-6 / 3 Other modules | no screen part | backend | ☐ |
| US-6 / 4, / 5 Re-owned, cleared | «Владелец области»: preselected with the explicit owner or «Наследовать…», helper «Сейчас: <команда>» / «Сейчас: <команда>, унаследовано», «Сохранить» disabled until changed; `PUT …/owner`; notice «Владелец области «<название>» изменён.»; refetched tree shows the children inherited | component | ☐ |
| US-6 / 6 A move re-resolves | the tree refetches after every write (no optimistic owner) | component | ☐ |
| US-6 / 7 Owner of the own project | as US-5 / 11 | component | ☐ |
| US-6 / 8 Default stays with its team | 409 `is_team_default` → dialog `Alert` «Область используется как область по умолчанию команды «Платформа»» in the owner and the delete dialogs | component | ☐ |
| US-6 / 9 Root's owner fixed | no «Изменить владельца» for the root | component | ☐ |
| US-6 / 10 Race | the loser's refusal in its dialog | component | ☐ |
| US-7 / 1 Deleted after a confirmation | «Удаление команды», destructive, the verbatim text with the team's and the first team's names, focus «Отмена»; `DELETE`; the row leaves, notice «Команда «<название>» удалена.» (or the fallback notice if it was the switcher's); from its own page → «Команды» of the project | component + e2e | ☐ |
| US-7 / 2 Declined | «Отмена» sends nothing | component | ☐ |
| US-7 / 3 First team | no «Удалить команду» in its menu or on its page; a 409 `first_team_cannot_be_deleted` (race) → dialog `Alert` | component | ☐ |
| US-7 / 4 Deleted team's area name | as US-4 / 3 | component | ☐ |
| US-7 / 5 Deleted team not found | its page → «Команда не найдена» | component (+ e2e) | ☐ |
| US-7 / 6, / 7 | no screen part / the loser's refusal in its dialog | backend / component | ☐ |
| US-8 / 1 Lists | organization level = § 1.1 then `Divider` + «Все организации»; project level = § 2.1; team level = § 3.1; order as received; never a deactivated / deleted entry | component + e2e | ☐ |
| US-8 / 2 Default team | a project choice selects its first team | component | ☐ |
| US-8 / 3 «Все организации» | a link (not `menuitemradio`, never checked) to `/organizations`; the selection stays | component + e2e | ☐ |
| US-8 / 4 «Нет организаций» | a link to `/organizations`; no project or team level | component | ☐ |
| US-8 / 5 Settings navigation | «Настройки» opens the organization's «Общие»; «Организация»: «Общие», «Проекты», «Процесс»; «Проект»: «Общие», «Команды», «Области»; the «Проект» section hidden with no project; `aria-current` page / true (deactivated tab, team page) | component + e2e | ☐ |
| US-8 / 6 The address sets the switcher | opening a project page of another organization sets organization, project and its first team and writes the remembered selection; an address answering not found sets nothing | component | ☐ |
| US-8 / 7 Switch on a project-level page | another project → same entry for it; another organization → same entry for its first project, «Главная» if none; from a deactivated tab → the entry's landing | component | ☐ |
| US-8 / 8 Team page | a project choice → «Команды» of the new project | component | ☐ |
| US-8 / 9 Organization-level page | project / team choices leave the page; an organization choice → the same entry of the new organization | component | ☐ |
| US-8 / 10 Remembered across a reload | `localStorage["mustask.context.<sub>"]` = `{"organizationId","projectId","teamId"}`, written by a choice and by an address sync, read on load | component (+ e2e) | ☐ |
| US-8 / 11 Fallback | deleted team, deactivated project, deactivated organization → first entries down the levels; notice «Ранее выбранный контекст недоступен. Выбрано: <о> / <п> / <к>.», empty levels left out; not on a first visit or an address sync; the page never moves | component | ☐ |
| US-8 / 12 Own changes | create / rename organization, add team reach the levels without a reload; deleting the chosen team or deactivating the chosen organization falls back with its notice in place of the action's (DC-7); the deactivation lands on the deactivated list | component + e2e | ☐ |
| US-8 / 13 Lists fail | organizations failed «Не удалось загрузить организации» + «Повторить»; projects «Не удалось загрузить проекты»; teams «Не удалось загрузить команды»; «Повторить» retries that level; loading levels are skeletons in `role="status"` «Загрузка контекста»; a failed level keeps its remembered choice | component | ☐ |
| design § 4.7 Page states | every page: loading (`RegistrySkeleton` / `FormSkeleton`, `role="status"` «Загрузка данных», header and tabs kept), error (`ErrorBanner` with the server's text + «Повторить», no entity commands); team page title «Команда» while loading and on error | component | ☐ |
| design § 4.3 Dialog conventions | busy primary spinner, every control disabled, Escape and backdrop ignored while busy; Escape / «Отмена» close an idle dialog; focus returns to the opener; confirmations open with focus on «Отмена» | component | ☐ |
| design § 4.4 Refusal placement | every code of api-contract § 6 placed per the table, by code; unknown code → dialog `Alert` | component | ☐ |
| design § 1 Routes, titles, trails | the nine routes; `deactivated` never read as an id; `/organizations/:o` and `/organizations/:o/projects/:p` alone → «Страница не найдена»; tab title «<h1> — Mustask» | component | ☐ |
| api-contract § 1–§ 6 | every request's method, path and body as the contract; error-code constants one-to-one with `contracts/errors/organizations.json` | unit / contract | ☐ |

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|----------|----------|--------|
| F-1 | Double activation of a dialog's primary (two quick clicks, Enter then click) | one request; the busy state disables the primary | ☐ |
| F-2 | Names with markup and quotes: «<b>Альфа</b>», «"Кавычки"», «A & B» | shown as text everywhere (tables, h1, notices, confirmations, switcher), never as markup | ☐ |
| F-3 | A 100-character name in a table cell, the switcher and an h1 | the cell truncates with an ellipsis and a tooltip, the h1 wraps | ☐ |
| F-4 | `localStorage` throws (blocked) or holds garbage / another shape under the key | nothing fails; treated as nothing remembered | ☐ |
| F-5 | Two users on one browser: the key carries the token's `sub` | one user's selection is never applied to the other | ☐ |
| F-6 | A dialog write answered by the not-found of an outer level (the project deactivated meanwhile) | the `Alert` shows the server's text; closing refetches; the page shows «Проект не найден» | ☐ |
| F-7 | Restore answered 404 (project restore under an organization deactivated meanwhile) | the row stays, error `Snackbar` with «Организация не найдена.» | ☐ |
| F-8 | A 500 with no code, or a network failure, on a list read and on a write | `ErrorBanner` + «Повторить» that refetches; a write shows the dialog `Alert` with the platform's text and keeps the input | ☐ |
| F-9 | A 400 `validation_failed` whose `errors` hold both `name` and `key` | both fields show their reasons at once | ☐ |
| F-10 | Rename with only surrounding spaces added («Альфа » for «Альфа») | either disabled as nothing to save, or sent and accepted (the server trims); never an error | ☐ |
| F-11 | A 500-node, 10-level tree | renders; the keyboard model still reaches every row; expand state kept across a refetch | ☐ |
| F-12 | Move picker typed text that matches nothing | «Ничего не найдено» | ☐ |
| F-13 | The selected row survives a refetch after a write; a deleted row's parent is selected | as § 11.7 | ☐ |
| F-14 | The window regains focus after another user's change | the switcher's levels and the open list refetch (TanStack default) | ☐ |
| F-15 | mod-001's «Процесс» page in a real organization | settings navigation, «Процесс» current, trail «Настройки / Организация»; the address sets the switcher | ☐ |
| F-16 | `/organizations` | no settings navigation, no main entry current, no trail | ☐ |
| F-17 | A user holding `admin` (every code) | sees «Создать организацию»; the create succeeds on the live stand | ☐ |
| F-18 | Accessible names: «Действия: <название>», «Восстановить «<название>»», «Развернуть/Свернуть «<название>»», «Изменить область по умолчанию» | as design § 11.2, § 13 | ☐ |
| F-19 | Kazakh locale (if the panel switches language) | the server's `detail` is shown as received; nothing hard-codes a Russian server text in the slice | ☐ |
| F-20 | No reference-tracker name in the slice, its tests and specs (C-3) | none | ☐ |

### Runs

| Date | What was run | Result |
|------|-----------------|-----------|

### Defects

### Verdict

### Results — 2026-10-05, the short route (D-z)

A task-executor run, not a QA session: by D-z the whole E2E suite on a
stand brought up from nothing replaces the deep frontend pass, so the
matrix above keeps its ☐ and no verdict is written here. Branch tip
23054bd plus the spec fixes below, worktree of its own, database
`mustask_mod002` on the external PostgreSQL (port 5432), the stand lock
held for the whole run. Times are UTC.

| Step | Command | Result |
|------|---------|--------|
| 1 | `bash scripts/stand-reset.sh --yes`, `ConnectionStrings__mustask` naming `mustask_mod002` (D-j) | exit 0: database "not present, nothing to drop", "created empty"; Keycloak store `mustask-keycloak-2c377c877fee07ee` "not present" — the pair started from nothing |
| 2 | `dotnet ef database update --project src/backend/Mustask.Infrastructure`, same variable | exit 0: InitialCreate, AddProcessDefinitions, AddUserDirectory, AddOrganizationStructure (20261005092819) applied |
| 3 | `bash scripts/warm-worktree.sh` | node_modules copied from the cache, no `npm ci` |
| 4 | `dotnet run --project src/backend/Mustask.AppHost`, default launch profile, same variable, started 09:44:52 | log: "Database: external PostgreSQL", "Keycloak store: volume mustask-keycloak-2c377c877fee07ee (pairs with localhost:5432/mustask_mod002)", "the live realm matches the realm import" (no drift), `mustask-e2e` and `e2e-fixture` provisioned. The wait polled the realm, `/alive` and the panel, not `/health`, so as not to warm the stand by waiting; at the first poll, 09:46:22, all answered 200 and `/health` already did too |
| 5 | `bash scripts/keycloak-seed.sh` (`KEYCLOAK_ADMIN_PASSWORD`, `MUSTASK_PASSWORD`, `MEMBER_PASSWORD` from AppHost's user secrets) | exit 0: `mustask` → «Администраторы», `member` → «Сотрудники / Участники» |
| 6 | `cd src/frontend && E2E_ALLOW_DOCKER=1 npm run e2e`, 09:46:29 | exit 1: 46 tests in 20 files, **43 passed, 3 failed, 0 skipped, 0 flaky**, 517 s. Failed: 19.12, 21.2, 21.3 |
| 7 | re-run 1 of the failed tests only, after the two spec fixes: `E2E_ALLOW_DOCKER=1 npx playwright test --grep "19\.12 \|21\.2 \|21\.3 "` | exit 1: 19.12 passed, 21.2 passed, 21.3 failed again at the same step. The second allowed re-run was not used: 21.3 fails deterministically (D-2) |
| 8 | `VITE_API_BASE_URL=http://verify.invalid npm run build` (as `verify.sh` runs it; `tsc -b` covers `e2e/`), Prettier check and ESLint on both changed specs | exit 0 each |
| 9 | stand stopped: `taskkill //PID <AppHost> //T //F`, the leftover container `mustask-e2e-keycloak-5010` removed with `docker rm -f` | ports 3000, 5000, 5010 free; lock released; `mustask_mod002` left as it is |

**Totals after the re-run:** 46 tests in 20 files, 45 passed, 1 failed
(21.3, D-2), 0 skipped. Specs 5 and 6 ran (Docker control allowed). Every
spec file other than 19 and 21 passed on the first run.

**Stand record**, `src/frontend/e2e-results/stand.json` after step 6 (the
run started 09:46:29, so `checkedAt` is this run's):

```json
{
  "stand": "warm",
  "waitedMs": 7,
  "url": "http://127.0.0.1:5000/health",
  "checkedAt": "2026-10-05T09:46:31.178Z"
}
```

After step 7: `"stand": "warm"`, `"waitedMs": 5`, `"checkedAt":
"2026-10-05T10:03:45.930Z"`. **The run was warm.** The stand was brought up
from nothing, but `Mustask.Api` had reached the realm before the suite's
first check (the Keycloak image was cached, and the stand answered about
90 s after the start), so this run did not exercise the cold-start window
of 05-frontend § 7. The brief allowed one run; whether to repeat it once on
a second stand from nothing is the orchestrator's call. If it is not
repeated, this is a deviation for the gate-3 summary and the MR's
`## Deliberately left open`.

#### Failures and their classification

| Test | Classification | Evidence | Done |
|------|----------------|----------|------|
| 19.12 the shell raises no script error… (`19-shell-in-a-browser.spec.ts`) | spec defect, timing | the trace shows «Настройки» clicked about 200 ms after «Главная» appeared, resolved with `href="/dictionaries/languages"`: the switcher's lists had not arrived, no organization was chosen yet, and by design § 2.1 / mod-015 § 4.2 «Настройки» then opens the first visible entry, «Языки». The panel did what the design says; the spec clicked before the context existed (19.4 waits for the three levels) | the spec now waits for the switcher's three levels before the click; passed in re-run 1 |
| 21.2 happy path: a project, its «Процесс» link, a team… (`21-organizations.spec.ts`) | spec defect, ambiguous locator | 88 ms after «Команды» was clicked, `page.getByRole('button', { name: 'Действия: Платформа' })` resolved to the area tree's row button (`tabindex="-1"`, the tree registry's roving button) for the area «Платформа», the team's default area named after it, still on screen from «Области». The area's menu has no «Изменить область по умолчанию», so the click timed out at 60 s | the spec waits for the table «Команды» and takes the row buttons and links from it; passed in re-run 1 (12.5 s) |
| 21.3 deactivate and restore… (`21-organizations.spec.ts`) | product defect D-2 (minor) | `expect(…«Отмена»).toBeFocused()` at line 253 failed in both runs: the dialog «Деактивировать организацию?» opened with focus on its own paper | not changed; recorded below |

#### D-2: On the stand a confirmation opens with focus on the dialog, not on «Отмена»

- Severity: minor. On the stand (the Vite dev server) every confirmation
  of the slice is affected (`ConfirmAction`): deactivating an organization
  or a project, deleting a team or an area. Focus on the dialog's paper
  confirms nothing on Enter, so the safety the rule exists for holds. A
  production build is expected to be right, but none was observed.
- Steps: spec 21.3. On an organization's «Общие», press «Деактивировать».
- Expected: the confirmation opens with focus on «Отмена» (design § 4.3;
  the matrix's US-2 / 5 row: "focus on «Отмена»").
- Actual: «Отмена» is not focused, in run 1 and again in re-run 1. The
  focused element is the paper (`role="dialog"`, `data-mui-focusable`,
  `tabindex="-1"`, from the trace's DOM snapshot).
- Where the cause is (by reading and a probe): `src/frontend/src/shared/ui/ConfirmDialog.tsx`
  focuses «Отмена» only through React's `autoFocus` (`cancelAutoFocus`,
  P-7), which runs once at mount. `src/main.tsx` renders under
  `<StrictMode>`, and in development StrictMode runs effects twice. On the
  first effect cleanup, MUI 9.4's `FocusTrap`
  (`@mui/material/Unstable_TrapFocus/FocusTrap.js`) returns focus to the
  opener (`nodeToRestore`). Its re-run then finds focus outside the trap
  and moves it to the `[data-mui-focusable]` paper. A throwaway Vitest
  probe, deleted and never committed, rendered an opener button that mounts
  `ConfirmDialog` with `cancelAutoFocus`. After a click, focus was on
  `BUTTON Отмена` without StrictMode and on `DIV role=dialog` with it. The
  component tests pass because they render without StrictMode.
- Not reached in either run: the rest of 21.3. That is the organization's
  deactivation through the panel, its «Организация не найдена» frame and
  its restore. The project's deactivation and restore earlier in the same
  test passed.
- Suggested direction (the developer's call): focus «Отмена» in a way that
  survives an effect re-run, for example from the dialog transition's
  `onEntered` or an effect with a ref, and add a component test rendered
  under `<StrictMode>`.

#### Specs changed

- `src/frontend/e2e/specs/19-shell-in-a-browser.spec.ts`, 19.12:
  `await expect(switcher(page).getByRole('button')).toHaveCount(3)`
  before the first click on «Настройки». The reason is in the comment
  beside it.
- `src/frontend/e2e/specs/21-organizations.spec.ts`, 21.2: after
  «Команды» is clicked, the spec waits for the table «Команды». The row
  buttons «Действия: Платформа» (twice) and the remaining-teams check now
  come from that table, not from the whole page. Nothing it asserts
  changed.

#### Re-check after the D-2 fix

A frontend-developer run, not a QA session: the fix, then files 20 and 21
on a live stand. Commit 453e309 on top of ebd9f26, worktree of its own,
database `mustask_mod002` reused as the run above left it (no reset, no
seed), the stand lock held from the stand's start to its stop. Times are
UTC.

**The fix (453e309).** `ConfirmDialog` no longer relies on `autoFocus`.
With `cancelAutoFocus`, a private `CancelButton` inside the dialog
focuses itself from an effect on `open`. React runs a child's effects
before its ancestor's, so on mount and again on StrictMode's re-run the
focus is already on «Отмена» when MUI's focus trap checks, and the trap
leaves it there. The effect also runs when a still-mounted dialog is
reopened. Callers without the prop render the same button as before.

| Step | Command | Result |
|------|---------|--------|
| 1 | the two new tests, against ebd9f26's `ConfirmDialog.tsx` | both failed at the first opening, with focus on the dialog's paper: `patternProps.mod002.test.tsx` (a button opener) and `TeamsPage.test.tsx` (the row menu's «Удалить команду», under `renderPage({ strict: true })`) |
| 2 | `npm run lint` | exit 0 |
| 3 | `VITE_API_BASE_URL=http://verify.invalid npm run build` | exit 0 |
| 4 | `npm test` | exit 0: 100 files, 1039 tests passed (1036 before, plus 3 new) |
| 5 | `dotnet run --project src/backend/Mustask.AppHost`, default launch profile, `ConnectionStrings__mustask` naming `mustask_mod002`, started 10:26:25 | log: "Database: external PostgreSQL", "Keycloak store: volume mustask-keycloak-2c377c877fee07ee (pairs with localhost:5432/mustask_mod002)", "the live realm matches the realm import" (no drift), `mustask-e2e` and `e2e-fixture` provisioned. The realm, `/health` and the panel all answered 200 at 10:27:54, 75 s after the start |
| 6 | `cd src/frontend && E2E_ALLOW_DOCKER=1 npx playwright test e2e/specs/20-process-definition.spec.ts e2e/specs/21-organizations.spec.ts`, 10:28:12 | exit 0: **14 tests, 14 passed, 0 failed, 0 skipped, 0 flaky**, 86 s (`e2e-results/results.json`). File 20: 6 of 6 passed. File 21: 8 of 8 passed. No re-run was needed |
| 7 | stand stopped: `taskkill //PID <AppHost> //T //F`, the leftover container `mustask-e2e-keycloak-5010` removed with `docker rm -f` | ports 3000, 5000 and 5010 free, no AppHost or DCP process left, lock released; `mustask_mod002` left as it is |

**21.3 is green to its end** (8.5 s, first attempt). «Деактивировать
организацию?» opened with focus on «Отмена». The organization was
deactivated through the panel and listed under «Деактивированные
организации». Its address answered «Организация не найдена». It was
restored, and its «Активные проекты» listed «Пример проекта». Spec 20
also deactivates an organization, and it passed unchanged.

**Stand record**, `src/frontend/e2e-results/stand.json` after step 6
(`checkedAt` is this run's):

```json
{
  "stand": "warm",
  "waitedMs": 5,
  "url": "http://127.0.0.1:5000/health",
  "checkedAt": "2026-10-05T10:28:19.096Z"
}
```

Warm, as expected: this re-check is narrowed to the fix and was not meant
to answer the cold-stand question. That question stays as the run above
left it.

**D-2: fixed** in 453e309. It is pinned by the two component tests under
`<StrictMode>` (a button opener and a row menu, each opened twice) and
confirmed on the stand by 21.3.

**Checked beside D-2.** A throwaway Vitest probe was used and deleted,
never committed. It opened the slice's real pages through
`renderPage({ strict: true })`. Both confirmation paths now hold:
«Деактивировать» on «Общие», and «Удалить команду» from a row menu. Each
held at the first and at the second opening, and focus went back to the
opener or to «Действия: Платформа» on close. Without StrictMode nothing
changed. The probe also found the same weakness in the form dialogs,
recorded as D-3.

#### D-3: Under StrictMode a form dialog opens with focus on the form, not its first field

- Severity: minor. It affects development only: the stand's Vite dev server
  renders under `<StrictMode>`. Without StrictMode the probe saw the
  correct focus, so a production build is expected to be right.
- Steps: «Общие» → «Переименовать», or «Команды» → «Добавить команду», on
  the stand.
- Expected: "The first field has focus. In a rename dialog the current
  name is selected." (design § 4.3).
- Actual (the probe under StrictMode): the focus is on the dialog's `form`
  (`role="dialog"`), and the name is not selected. Without StrictMode it
  is on the input, with «Альфа» selected (0–5).
- Cause: the same as D-2. The fields get their focus only through
  `autoFocus` (`NameDialog`, the team dialogs; by reading, also
  `CreateProjectDialog`, `AreaDialogs` and `DefaultAreaDialog`), and MUI's
  focus trap moves it to the paper on StrictMode's re-run. No E2E spec
  asserts it: Playwright's `fill` focuses the field itself.
- Not fixed here. The brief was D-2 only, and the remedy belongs in
  mod-015's shared `FormDialog`, whose callers in other slices have the
  same weakness. The `CancelButton` technique applies there: an effect
  inside the dialog that focuses the first field and selects a rename's
  text. A debt for gate-3.
