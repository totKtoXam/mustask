# Plan — frontend (feat-028-org-level-teams)

Stage 6 (L). Inputs: `spec.md`, US-1..US-8, `design.md` (D-1..D-5 on A, OQ-1 C, OQ-2..-5 A), `api-contract.md`
(fixed), `plan-backend.md` §§ 11-12, `frontend.md`, `ui-kit.md`; the slice `features/organizations`, the kit's
fields, `Tag`, `DataTable`, `Section`, the showcase registry and `kitGuard.allow.ts` were read. E2E: standing NO.

## 1. Slices and files
- **Kit (`shared/ui`), first, with tests, showcase entry and guide block:** `fields/InlineMultiPickerField`
  (D-1: MUI `Autocomplete multiple` on `FieldFrame`, kit `Tag`s with a remove button, `aria-selected`
  options with a decorative `Checkbox`, the list open while picking) and `Tag.removeLabel?` (default
  «Удалить тег «x»»). Showcase: a section "Поля ввода" demo, `Теги` demo; `covers` gains the field.
- **`features/organizations`** is the only feature slice that changes. Barrel exports the new pages and
  `ORG_TEAMS_ROUTE`, `ORG_TEAM_ROUTE`, `organizationTeamsPath`, `organizationTeamPath`; `TEAM_ROUTE` and the
  project-nested `teamPath` go (design § 9). `workitems`/`boards` read the project's teams through
  `useTeams` only: `TeamInList` loses `projectId`, the key and the shape (`ProjectTeam`) otherwise stay.
- **`app`:** `routes.tsx` (two organization routes under the gate; the project's `…/teams/:teamId` gone, the
  catch-all answers «Страница не найдена»), `navigation/model.ts` (`organization-teams`, «Команды», after
  «Проекты», code `team.read`). `test/organizationRoles.ts` gains the four codes (admin only).

## 2. API layer, keys, hooks (strictly `api-contract.md`)
`organizationsApi.ts`: `Team {id,name,description}`, `TeamListItem`, `TeamProject`, `ProjectTeam
{id,name,defaultMark,isMember}`, `DeletionCounts`, `UnlinkCount`, `Project.description`; org-level team
functions, `linkTeamsToProject`, `linkProjectsToTeam`, `unlinkTeam` (POST, `destination` wrapper with
`teamId: null` for «Без команды»), `getUnlinkCount`, `getDeletionCounts`, descriptions, `createTeam`,
`createProject` with `description`/`teamIds`. `membershipApi.ts`: team-member routes at the team's address.
`errorCodes.ts`: `description_length`, `destination_required`. `queryKeys.ts`: `orgTeams`, `orgTeam`,
`teamProjects`, `deletionCounts`, `unlinkCount`; the project's `teams` key is unchanged (the shell and the
pickers share it). Every link, unlink, deletion and description write invalidates the organization's teams,
the team's projects, the project's teams and the projects (design § 13).

## 3. Pages and components (all new files kit-only)
- `OrganizationTeamsPage` (§ 3): `DataTable` «Команды организации», row menu `MoreActionsMenu`, empty/error/
  loading states, `team.create` primary.
- `TeamPage` (§ 4, moved): `FactsPanel` + `Section` «Проекты» (`DataTable` «Проекты команды», link/unlink by
  `team.link` + `project.link`) + the existing `TeamMembersSection` without a project.
- Dialogs: `CreateTeamDialog` (§ 5.1, `InlineMultiPickerField` ×2), `DescriptionDialog` (team and project,
  § 5.2), rewritten `DeleteTeamDialog` (§ 5.3, per-project list), `LinkDialog` (§ 6.1, two ends),
  `UnlinkDialog` (§ 6.2, `InlineSelectField`, nothing preselected).
- `TeamsPage` (§ 7, the project's end): linked teams with the mark, «Отключить от проекта», «Добавить
  команду», «Команда по умолчанию» (`project.write`), «Бэклог проекта»/«Доска проекта» while no team is linked.
- `ProjectGeneralPage` (description), `CreateProjectDialog` (description, «Команды» picker, new helper text),
  `refusals.ts` (`RefusalField`: `description`, `destination`, `teams`, `projects`, `members`).

## 4. State and codes
Server state in TanStack Query only; dialogs hold their drafts in `useState`. The picker's options: the
organization's teams/projects lists (client filter), members from `GET …/members` (design says "server's user
search": no organization-scoped search of active members exists at creation, so the list is filtered locally —
for the human, § 6). Gates: `useAccessCheck()`; link controls need both link codes.

## 5. Tests (Vitest, Testing Library, MSW double `OrganizationsTestApi`)
The double and `testFixtures` move to the organization-owned team plus a link list, keeping the example
dataset's names; new tests per state of §§ 3-8 and per role of § 10; retired tests by name with the rule id
in a comment at the place (doc-012 P-1). Kit: field and `Tag` tests, showcase registry, `kitGuard` counts.

## 6. For the human, and as built
- The create-team form has no server search of the organization's active members (the contract's search is at
  a team's address): `GET …/members` filtered locally (`useMemberSearch`); recommended. The alternative, the
  organization's `member-candidates`, offers non-members and leans on the 409 `member_not_in_organization`.
- «Отключить» is natively disabled until a destination is chosen (`ConfirmDialog.confirmDisabled`), where
  design § 6.2 says aria-disabled and focusable; the legacy dialog has no such state.
