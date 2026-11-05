# Audit 6-frontend — Frontend: navigation, routes, work context, settings pages

As-is audit, read-only; nothing was built or run. Every claim below was read in the code or the cited document. Paths are
repository-relative. `FE` = `src/frontend/src`. The reference tracker is not named (AGENTS.md); its analyses are cited by rule id.

## As-is (what exists, with repository-relative file paths and line numbers where it matters)

**Address scheme.** Three fixed prefixes (ADR-0024 § 2; mod-015 design § 5.5): `/organizations/:o/…`, `…/projects/:p/…`, `…/projects/:p/teams/:t/…`.
Ids are untyped strings the page validates; a missing level answers `NotFound` by level (page, organization, project, team, workItem: `FE/shared/ui/StateFrame.tsx:155-159`).
- Routes, `FE/app/routes.tsx` (all under `RequireAuth` → `AppLayout`): `/home`, `/dictionaries/languages[/new|/:code]`, `/ui-kit` (instance); `/organizations` and `/organizations/deactivated` outside any gate (:88-92).
- Under `OrganizationGate` (:93-195): `/organizations/:o/settings/{general,projects,projects/deactivated,members}` and `/organizations/:o/projects/:p/settings/{general,teams,teams/:t,areas}` (`FE/features/organizations/paths.ts:11-22`).
- Second gate block (:208-269): `…/settings/process`, `…/projects/:p/workitems[/:identifier]` (`FE/features/workitems/paths.ts:7-8`), `…/projects/:p/teams/:t/boards/:level` and `…/backlogs/:level`, level ∈ epics|features|stories (`FE/features/boards/paths.ts:7-10`, `lib/levels.ts:9`). Catch-all `NotFound` (:272).
- A team has two unrelated addresses: the settings page `…/settings/teams/:t` carries no team context, the surface `…/teams/:t/(boards|backlogs)/…` does (`FE/features/organizations/context/addressContext.ts:27-40` reads a team only when segment 4 is `teams`; test `useOrganizationsContextSource.test.tsx:170`).
- An area has no address of its own (one tree page, `…/settings/areas`). A project has no overview, board or backlog page: only «Общие» and the flat «Рабочие элементы» list (50 per page, no filter or sort; `FE/features/workitems/pages/WorkItemsPage.tsx:35`, page size `FE/features/workitems/api/workItemsApi.ts:161`).

**Navigation tree** (`FE/app/navigation/model.ts`; filtered by `visibility.ts`; rendered by `FE/app/shell/MainNavigation.tsx`, `SettingsNavigation.tsx`).
- Main, top: «Главная» (instance). Main, work: «Рабочие элементы» (scope project, :72), «Канбан» (scope team, :83), «Бэклоги» (scope team, :99). «Настройки» is derived, not an entry: it opens the first visible settings entry.
- Settings (second drawer, shown only on a settings page, `AppLayout.tsx:163-165`): «Организация» → Общие, Проекты, Участники, Процесс (scope organization); «Проект» → Общие, Команды, Области (scope project, :150-185); «Справочники» → Языки; «Интерфейс» → Витрина (instance).
- An entry is shown iff registered, its code is held, and the context level its scope needs is selected (`visibility.ts:42-87`; team scope needs organization+project+team, :50-51). So «Канбан»/«Бэклоги» are hidden for a project with no team; nothing is shown disabled (mod-015 BR-7, D-18).
- The trail is derived from the model: «Настройки / section / entry», or the main entry; a page adds only its own `ancestors` (`visibility.ts:170-193`). «Проекты» lives in organization settings; there is no top-level Areas / Projects / Teams entry and no «Обзор» (`HomePage.tsx` is a static greeting).

**Work context and switcher.**
- Model: three fixed levels, nested organization → projects → teams (`FE/shared/lib/workContext.ts:12-41`); selection `{organizationId, projectId, teamId}`; `isReal` hides every scoped entry until real (:75-80).
- Source `FE/features/organizations/context/useOrganizationsContextSource.ts` reads one level at a time: active organizations, the effective organization's projects, the effective project's teams (:154-196; keys `api/queryKeys.ts:9-27`). Teams of other projects are never loaded (tree :364-386 gives `teams: []`). Teams are read per project: `GET …/projects/:p/teams`; there is no organization-level team list on the client.
- Order of authority: address (once per pathname; `resolveSelection.ts:65-124`) → remembered (`localStorage mustask.context.<sub>`, `storage.ts:10`) → first entries; a vanished entry falls back with a notice (:243-252; `ContextSwitcher.tsx:231-249`).
- The team level defaults to the first team the user is in, else the first (`defaultEntry`, `workContext.ts:124-128`; `TeamInList.isMember`, `organizationsApi.ts:56-58`; mod-016 BR-M1-27/D-5), on choosing an organization or project, first load, and fallback.
- A choice at a level the address carries calls `switchTarget` (`FE/app/navigation/switchTarget.ts:37-58`): it finds the entry the address is at or beneath by landing pattern and the address's own ids, opens that entry's landing in the new context, else «Главная». `withFirstEntries` (:85-111) reads the first project and default team before moving.
- The switcher is hard-wired to three levels and their words «Организация/Проект/Команда», «Нет проектов», «Нет команд» (`ContextSwitcher.tsx:30-56,273-385`). `TeamHead.tsx:106-116,168-264` offers «Сменить команду» from the same project's teams.

**Boards and backlogs** (`FE/features/boards`). Team-addressed inside a project: `/api/organizations/:o/projects/:p/teams/:t` (`api/boardsApi.ts:174-176`). Content = items of project P whose area resolves to team T (mod-006 spec § Model 1-3, BR-M4-5).
- Items carry no project or team field (`BacklogItem`, `boardsApi.ts:46-59`; `WorkItemListItem` even omits `projectId`, `workItemsApi.ts:143-152`). No filter, search or sort on either page; only the level drop-down. No Project Board, Area Board or cross-project view exists.
- The card opens over the page by `?workitem=<IDENTIFIER>` (`WORK_ITEM_PARAM`) and is read in the page's own project (`BoardPage.tsx:142-146`). Creating from a surface sends `teamId` and the item takes that team's default area (`useNewWorkItem.tsx:121`; `CreateWorkItemDialog.tsx:118-120`).
- No sprint or iteration UI exists: only the card slot `iteration`, "Owner: mod-007" (`FE/features/workitems/components/card/cardSlots.ts:27-28`).

**Settings pages that manage teams and areas** (`FE/features/organizations/pages`; every call and query key is `(organizationId, projectId)`: `api/organizationsApi.ts:103-113`, `hooks/mutations.ts:136-269`).
- `TeamsPage` (`…/settings/teams`): the project's teams with «Область по умолчанию»; first-team chip «Владеет корневой областью» (:35,140); add (creates team + child area, helper :161), rename, change default area, delete (not the first team; «Её области перейдут команде «<first>»», `DeleteTeamDialog.tsx:23`). No link/unlink: a team cannot exist outside one project.
- `TeamPage` (`…/settings/teams/:t`): name, default area, «Участники команды» (add/remove organization members; `api/membershipApi.ts:63-69`).
- `AreasPage` (`…/settings/areas`): the project's area tree on `TreeRegistry`: create child, rename, move, change owner team, delete; the root offers only create-child (:130-132); owner column with «унаследовано»; chip «По умолчанию». Area writes need `organizations.area.write`.
- `ProjectListPage` (`/organizations/:o/settings/projects`): create (name + key; helper says the first team and root area are created with it, `CreateProjectDialog.tsx:68-69`), deactivated tab, restore. `ProjectGeneralPage`: rename (root area renamed too, :111), deactivate, «Процесс» link. `MembersPage`: organization roles admin|member|viewer (`lib/roles.ts:7`).
- Writes to teams/areas invalidate only `organizations` keys, never `boards` keys; boards re-read on open, own write and window focus (mod-006 BR-M4-28; `boards/hooks/queries.ts:12-20`).

**Access in the panel.**
- Instance codes ride the token; organization codes come from `GET /api/organizations/:o/access` as `{role, permissions[]}` (`FE/features/organizations/api/accessApi.ts:13-17`). 13 codes (`FE/features/organizations/permissions.ts:9-24`).
- `PermissionScope` `project`/`team` only say how much context an entry needs; all answer from the organization's codes (`FE/shared/lib/access.ts:3-24`, `servedAccess.ts:90-100`). There is no area, project or team grant, no project visibility, no resource id in the check.
- Each organization page = `OrganizationGate` + `OrganizationPage` (one state order, mod-016 design § 2.1); `RequiresOrganizationCode` (`FE/shared/ui/RequiresOrganizationCode.tsx`) disables a dialog's primary action once its code is gone; pages hide write commands by the same bound check.

**What a top-level Areas / Projects / Teams navigation would change** (consequences only).
- Entries: organization-scoped entries are expressible today with no project/team; main entries need an icon and sit in the work group, settings entries sit in the second drawer. `NavigationScope` = `PermissionScope` has no `area`. `model.ts` order drives «Настройки» landing (BR-19), and `visibility.test.ts:75` pins the registered list.
- Addresses: Project has a context prefix, Team only below a project, Area none. An Area or project-less Team context changes `WorkContextSelection`, `addressContext`, `resolveSelection`, `storage`, `switchTarget`, `useOrganizationsContextSource`, `ContextSwitcher`, `TeamHead` and `scopeSelected`, which all hard-code organization → project → team.
- The prefix `…/projects/:p/teams/:t/…` states project → team. ADR-0024 lists the prefixes as "locked in" (Consequences). A Team Board over several projects has no single `:p`; brief § 11.6.9 asks for no false hierarchy.
- The trail and the "same entry's landing" switch rule (ADR-0024 § 2) assume one chain; a switch at an Area level, or a team that spans projects, has no rule yet.
- The project–team relation is read from the project (`getTeams(o,p)`) and the team's default is `isMember`-based; both are the data source of the switcher and of `TeamHead`.

## Responsibilities the current model carries (one duty per bullet; mark where one entity carries several duties)

- **Area carries SEVERAL duties:**
  - (a) Classification of an item: `WorkItem.area` path, the list's «Область» column, the card's `AreaField` (`FE/features/workitems/components/card/AreaField.tsx`, `useAreaTree(o,p)`).
  - (b) Team ownership: area → `resolvedOwner` → which team's backlog and board hold the item (BR-M1-11, mod-006 BR-M4-5). Setting an item's area, or re-owning or moving an area, silently moves items between teams (mod-006 BR-M4-21, BR-M4-26).
  - (c) Default placement of new items (`CreateWorkItemDialog`). (d) A tree to browse in settings.
  - Not used for access: no frontend rule or permission mentions area membership.
- **Team carries SEVERAL duties.** People group (members, `TeamMembersSection`); owner of areas hence of backlog/board content; default area for creation; third switcher level and `isMember` default; address segment of the boards; child of exactly one project (`Team.projectId`, `organizationsApi.ts:43-50`); the first team owns the root and receives areas on delete (`isFirstTeam`).
- **Project carries SEVERAL duties.** Container of items and of the item key/identifier; context prefix for project-level pages; parent of the team list and the area tree in switcher and API; the process link; creation side effects (first team, root area).
- **Organization.** Tenant boundary and the one access unit: membership, role → code bundle, switcher level 1, address prefix, per-page gate.
- **Switcher selection carries SEVERAL duties.** Navigation context for entries; remembered state; default-team heuristic; the "which team" input of «Канбан»/«Бэклоги».
- **Navigation `scope` carries two duties**: the context level an entry needs, and the level a permission is "evaluated at" (it is always the organization's).
- No frontend duty exists today for: an item's owning team shown on a card/row/list, a "no team" item, a team's sprint, a project-wide board, an area-wide aggregate, per-resource permission.

## Rules and contracts the matrix model touches (by id) — for each: what in the brief collides with it

- **mod-002 BR-M1-9** (project creation creates the first team + root area; a team creates a child area): brief § 4 and § 11.5 want no mandatory team or area. The copy lives in `CreateProjectDialog`, the `TeamsPage` helper, `ProjectGeneralPage`.
- **mod-002 BR-M1-11** (one immutable root area, owned by the first team for good): brief § 3.2 makes an area a workspace with an optional parent, not a per-project root.
- **mod-002 BR-M1-12** (a project's own area tree, sibling names unique): brief § 3.2 puts the tree in the organization and links projects M:N.
- **mod-002 BR-M1-13/14** (a team's default area, per project): brief § 5 has no team default area; the human decision adds a project default team instead.
- **mod-002 BR-M1-18** (delete team: its areas pass to the first team): the brief has no first team; SC-09 needs a different unlink/delete rule.
- **mod-002 BR-M1-22** (a team or area never moves between projects): brief § 5 makes Project ↔ Team M:N.
- **mod-002 BR-M1-23** (settings in two places, organization and project): brief § 11.1-11.4 gives Area, Project and Team their own pages.
- **mod-002 BR-M1-24** (switcher: organization → projects → that project's teams, first team first): brief § 11.6.4 asks for "Area / Project / Team" switching.
- **mod-002 api-contract § 3 (teams at `projects/:p/teams`), § 4 (areas at `projects/:p/areas`)**, **mod-006 api-contract § 2-3 (team-addressed boards/backlogs), § 7 (create with `teamId`)**, **mod-003 api-contract § 1.1 (`area` on the item), § 2.2**, **mod-016 api-contract § 2 (team members under a project), § 3.3 (`isMember`)**: every URL the panel calls is project-nested; brief §§ 7 and 14 need organization-level teams and areas and Area/Project/Team boards.
- **mod-016 BR-M1-27 / D-5** (default team = the first team the user is in): the human decision replaces it with a project default team (a setting, or the single linked team). `defaultEntry`, `TeamInList.isMember` and four test suites encode the old rule.
- **mod-016 BR-M1-25** (the creator joins the first team): collides with "a project may have no team".
- **mod-016 BR-M1-26** (assignee = active organization member, `isMember` mark): consistent with brief § 6 (assignee need not be in the owning team); the mark means organization membership, not team.
- **mod-015 BR-7/8/11/18/19, D-17..D-20** and **ADR-0024 § 2 (+ Consequences "locked in")**: the context is organization/project/team in the address; the brief changes the context set and probably the team prefix, so ADR-0024 needs an amendment (brief § 11.6.4, .9).
- **ADR-0024 § 2 / doc-006 BR-M1-8** (a context the user may not see answers like a missing one): brief § 10.3 says a Restricted project must answer «Проект не найден» identically in switcher, address, counters and search (§ 10.4.4).
- **ADR-0027** (§ 1 organization codes only from membership; § 3 one check from `{organizationId}`; § 2 team membership grants no code): brief § 10 adds Area/Project/Team scopes, while the served payload and `AccessCheck` carry no resource id (P-5: extend, not a parallel model). "Team grants nothing" agrees with brief § 5 last paragraph.
- **mod-006 BR-M4-1/5/20/21/26/28 and § Model 1-3** (one backlog and board per team per level; membership by area owner; a deleted team's areas pass to the first team; reorder stale): brief § 7.3 (Team Board = the team's tasks from all projects) and § 6 (optional `OwningTeamId`); P-2 asks what a team's backlog holds once teams own no areas.
- **mod-003 BR-M3-2** (an item belongs to one project) and **mod-003 api-contract § 1.1/§ 2**: compatible; `WorkItem` has no team or sprint field today.
- **doc-004 BR-M5-1/5/7, E-7/E-10..E-12** (an iteration belongs to a project; a team selects iterations; an item's iteration needs the owning team via area) and mod-015 § 4.1 «Итерации» under «Проект»: brief § 8 makes a sprint a Team's and cross-project; `cardSlots.iteration` is owned by mod-007.
- **feat-028** (queue line: org-level teams; routes, switchers, settings pages) overlaps this audit's whole surface. **feat-027 BR-T-16 / feat-001 BR-H-15,16** (team head, «Сменить команду», level drop-down) assume team-in-project.

## Regression surface (existing behaviour that must keep working, with the test or spec that proves it today)

- Address → switcher → navigation: `FE/features/organizations/context/{addressContext,resolveSelection,storage}.test.ts`, `useOrganizationsContextSource.{test,teamLevel.test,defaultTeam.test}.tsx`, `FE/shared/lib/workContext.test.ts`, `FE/app/navigation/{switchTarget,visibility}.test.ts`, `FE/app/shell/ContextSwitcher{,.levels}.test.tsx`, `FE/app/AppLayout.{context,navigation,access,edges}.test.tsx`.
- Routes and not-found frames by level: `FE/app/routes{,.organizations,.boards,.workitems,.members,.access,.edges}.test.tsx` (e.g. `routes.boards.test.tsx:472-492`, deleted team → «Команда не найдена»).
- Settings pages: `FE/features/organizations/pages/{TeamsPage,TeamPage,AreasPage,AreasPage.workitems,ProjectGeneralPage,ProjectListPage,MembersPage}*.test.tsx`, `components/{AreaDialogs,DefaultAreaDialog,TeamMembersSection}.test.tsx`, `OrganizationsPages.roles.test.tsx` (role views).
- Boards/backlogs/cards: `FE/features/boards/pages/{BoardPage,BacklogPage}.*.test.tsx` (drag and drop, reorder, tree edit, create, checklist), `FE/features/workitems/pages/WorkItemFormPage.*.test.tsx` (area state, assignee, membership), `WorkItemsPage*.test.tsx`.
- Guards that fail `npm test` for new screens: `FE/test/kitGuard.test.ts`, `FE/test/colourLiterals.test.ts`.
- Specs those tests cite: mod-002 US-8 (switcher on real lists, address sets selection, remembered selection, fallback notice), mod-015 design § 4 (navigation), § 5 (switcher states, address rule), mod-016 design § 2.1 (gate row order), mod-006 US-1/US-4/US-7. The stored key `mustask.context.<sub>` is `{organizationId, projectId, teamId}` (`storage.test.ts`).
- E2E (outside `npm test` and `verify.sh`): `src/frontend/e2e/specs/{19,21,22,23,28,29}-*.spec.ts` use project-nested team/area/board addresses (`21-organizations:257,432`, `23-backlogs-boards:74-75,292,329`, `22-work-items:207`); `e2e/support/organizationsApi.ts:109,171` builds the old API URLs.

## Risks and open questions for the analyst

- Q1. The word «Область» is used for the item's classification path and (brief) for a universal workspace. Does the item keep a classification field, and under what name, so the card/list/dialog copy and `AreaField` are not reused for two meanings?
- Q2. Today an item's team is implicit (area owner). Where does the owning team appear and get changed in the UI (list column, card, backlog row, creation dialogs)? What replaces the "set area to move between teams" behaviour (BR-M4-21)?
- Q3. Which address does a Team Board (many projects) and an Area Board live at? Is the `…/projects/:p/teams/:t/…` prefix kept, replaced by `…/teams/:t/…`, or both? Old links die with the data; is a redirect or a not-found enough?
- Q4. Is Area a context level of the switcher (a fourth level, an alternative axis) or only a page family? What does the switch rule do on an Area-level address?
- Q5. A project with no team and a team with no project are legal: which entries and landings show then (`scopeSelected` hides team-scope entries; the switcher says «Нет команд»; a project-level board is needed)? The project default team (human decision) feeds which entry?
- Q6. Card dialog and `?workitem=<IDENTIFIER>` read the item in the page's project; a cross-project board needs the card's own project, and `BacklogItem`, `WorkItemRef` and list rows carry none (breaking change to mod-006 api-contract § 3, feat-001/027/032 contracts, per P-10).
- Q7. Filters, search, sort, paging/virtualisation, counters (brief § 7) have no existing UI to extend; boards and backlogs load whole levels. Which existing mechanism is "kept"?
- Q8. Scoped permissions need a resource-aware check on the client (`AccessCheck` has none) and per-project 404s; how do entries, buttons and the switcher stay consistent without the client becoming the authority (brief § 10.4.2)?
- Q9. Restricted projects must vanish from switcher lists, search, parent/assignee pickers, `useAreaTree`, `TeamHead` menus and counters; the switcher caches per-level lists under shared keys (`organizationsKeys`), so a visibility change needs a defined refresh (P-4).
- Q10. The delete-team dialog, create-project helper, project-rename text and SC-09 unlink need new copy; Russian strings are product copy owned by spec/design (language policy).
- Q11. Operation completeness (P-6): link/unlink team↔project, area↔project, area↔team, default team, sprint states each need a role and a surface; today's settings pages have none of them.
- Q12. `FE/shared/ui` kit guard and mod-015 patterns: new Area/Project/Team pages (tree, multiselect links, boards) need kit components first (`src/frontend/AGENTS.md`); how much is delivery-line scope versus this analysis?
- Risk: feat-028 and the project-scoped mutation hooks (`(organizationId, projectId)` everywhere) make a partial migration unstable; the test files in the first regression bullet encode the three-level model and the first-team rules and will all need a decision (keep, replace or retire by name, P-1).
