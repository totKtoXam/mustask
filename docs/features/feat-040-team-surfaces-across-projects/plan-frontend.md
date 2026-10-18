# Plan — frontend (feat-040-team-surfaces-across-projects)

Stage 5 (2026-10-18), size L. Inputs: `spec.md`, US-1..US-7, `design.md` (D-1..D-7 A), `api-contract.md`, `plan-backend.md` § 14,
`docs/conventions/frontend.md`, `ui-kit.md`. Contract and API agree; nothing escalated. No new kit part, icon, colour, dependency or
permission code; one table token (`layout.backlogTable.projectColumn`, 120 px). Slices: `features/boards` (the views), `features/workitems`
(creation dialog, card, filter helper), `features/organizations` (switcher source, project «Команды»), `app/` (shell wiring, nav model).

## 1. Work context (BR-TS-10) — `shared/lib/workContext`, `organizations/context`, `app/shell`, `app/navigation`

- The team level moves from the project to the organization: `WorkContextOrganization.teams` (own first, each group by name; `isMember`
  from `GET /teams`, api-contract § 4.1); `WorkContextProject` loses `teams`. `defaultEntry` stays (default team, else the first).
- `chosen.teamId` means a team **chosen** (switcher, head menu, team address); a fallback team is derived per render: the effective
  project's default team (its `GET …/teams` `defaultMark`, mod-020), else the level's first entry. Only a chosen team is remembered
  (storage keeps its shape), so a reload neither makes a fallback chosen nor a chosen one a fallback. A project change keeps a chosen
  team; an organization change drops it; a chosen team that is gone falls back with mod-002's notice, as today.
- `addressContext`: `…/teams/:t/…` under the organization carries the team; `…/projects/:p/teams/:t/…` carries the project only.
  `addressSync` takes the team from the organization's team list; a team address keeps the project level at its current selection.
- `ContextSwitcher` draws the team level from the organization, also with no project («Нет проектов»); «Нет команд» only with no team.
- Navigation: a `team` scope needs the organization and a team; «Бэклоги»/«Канбан» land on the team's «Истории» page; `switchTarget` and
  `withFirstEntries` follow (an organization choice on a team page reads the new organization's first project and teams).

## 2. Boards slice — addresses, reads, filter, rows and cards

- `paths.ts`: `…/teams/:t/backlogs|boards/:level`; the old project-nested ones are no route and fall to `NotFound level="page"`.
  `BoardScope` = team `{teamId}` | project `{projectId}`; components lose the separate `projectId` prop and use `item.project.id` for every
  per-item call (title link, parent write, child create, state write, checklist). `workitems` hooks take an optional project per call.
- `boardsApi`: `getBacklog/getBoard(org, team, level, projectIds)`, `reorderBacklog(org, team, level, body)`, `project` on every item and
  `projects` on the team reads. Keys carry the sorted project filter; the team reads keep the previous rows while another filter is read.
- Filter in the address: the shared `addressFilter` helper (extracted from mod-020's `useTeamFilter`, which keeps its exports) + the boards
  `useProjectFilter` (`projectId` repeated). `ProjectFilter` (`InlineMultiSelectField`, options from the read's `projects`) +
  `FilterRow` over a dimension (project on team views, team on project views); «Фильтр задан»/«Нет проектов» per design § 4.2; the level
  menu, tab and the other-page command keep it on a team view, the head menu, switcher and navigation drop it.
- Rows: «Проект» column after «Команда» (`projectColumn`); cards: right end of the assignee row (`Проект: ` hidden prefix); skeleton columns.
- Empty states (design § 10 state 10): (a) team on no project → «Бэклог пуст»/«Канбан пуст» + «Команда не участвует ни в одном проекте.»,
  no filter row, no create; (b) today's; (c) filtered-empty with the filter row above. `dropVerdict` gains `project` (judged first) and
  `refusalNotice` its text; `ChangeParentDialog` gets the row's project.
- `TeamHead`: menu «Команды» over the organization's teams (no «Весь проект», no divider); no button on a project page; `TEAM_CHOSEN_STATE`
  and the keyboard model kept; the other-page command and tab carry the project filter on a team view.

## 3. Creation, card, project «Команды»

- `CreateWorkItemDialog` takes either `projectId` (project views, as today) or `projects` (a team view): «Проект» first (`InlinePickerField`,
  «Найти проект»), required («Выберите проект»), preselected by the filter's single project or T's only one, decided once; «Команда» read-only
  holding T with the caption until a project is chosen (D-4), then the project's teams + «Без команды»; a project change that drops the shown
  team sets it back to T with the announcement and line (D-5); the area line follows the project; refusals `organizations.project.not_found` →
  under «Проект» (options read again), team refusals under «Команда». `createdNotice` gains the team reason (D-6).
- Card dialog: on a team view the host passes no project; the dialog resolves the identifier's key through the organization's active
  projects (`useProjects`, the switcher's key); an unknown key shows the not-found frame without a request. `WorkItem.project` fills a
  read-only «Проект» in the band (block A, row 2).
- Project «Команды»: «Бэклог проекта» and «Доска проекта» always, ahead of «Команда по умолчанию».

## 4. Tests and retirements

Test fixtures (`boards/testApi.ts`, `testFixtures.ts`, `testRender.tsx`, `organizations/testApi.ts`, `workitems` fixtures) are team-led:
`project` on items, `isMember` on teams, `GET /teams`, `GET /projects`. New: `addressFilter`, `useProjectFilter`, `ProjectFilter`/`FilterRow`,
rows/cards «Проект», drop verdict, creation dialog (project, D-4, D-5), created notice, card by address, head menu, switcher level, project
«Команды», the retired address. Rewritten/retired by name with the rule id as spec § "Existing rules" lists them. E2E: NO, not run;
the specs the change breaks are named in `plan-backend.md` § 14.
