import { Navigate, Route, Routes } from 'react-router-dom';
import { AnonymousScreen } from '../features/auth/AnonymousScreen';
import { RequireAuth } from '../features/auth/RequireAuth';
import {
  BACKLOG_ROUTE,
  BOARD_ROUTE,
  PROJECT_BACKLOG_ROUTE,
  PROJECT_BOARD_ROUTE,
  BacklogPage,
  BoardPage,
  ProjectBacklogPage,
  ProjectBoardPage,
  boardsPermissions,
} from '../features/boards';
import { LanguageFormPage, LanguagesRegistryPage } from '../features/dictionaries';
import { HomePage } from '../features/home/HomePage';
import {
  AreaPage,
  AreasPage,
  ProjectAreasPage,
  DEACTIVATED_ORGANIZATIONS_ROUTE,
  DEACTIVATED_PROJECTS_ROUTE,
  MEMBERS_ROUTE,
  ORGANIZATIONS_ROUTE,
  ORGANIZATION_GENERAL_ROUTE,
  MembersPage,
  OrganizationGate,
  OrganizationGeneralPage,
  ORG_AREAS_ROUTE,
  ORG_AREA_ROUTE,
  ORG_TEAMS_ROUTE,
  ORG_TEAM_ROUTE,
  OrganizationListPage,
  OrganizationPage,
  OrganizationTeamsPage,
  OrganizationsSection,
  PROJECTS_ROUTE,
  PROJECT_GENERAL_ROUTE,
  ProjectGeneralPage,
  ProjectListPage,
  AREAS_ROUTE,
  TEAMS_ROUTE,
  TeamPage,
  TeamsPage,
  organizationsPermissions,
} from '../features/organizations';
import {
  PROCESS_DEFINITION_ROUTE,
  ProcessDefinitionPage,
  processPermissions,
} from '../features/process';
import { UI_KIT_PATH, UiKitShowcasePage } from '../features/uikit';
import {
  WORK_ITEMS_ROUTE,
  WORK_ITEM_ROUTE,
  WorkItemFormPage,
  WorkItemsPage,
  workItemsPermissions,
} from '../features/workitems';
import { HOME_PATH } from '../shared/config/paths';
import { NotFound } from '../shared/ui/StateFrame';
import { AppLayout } from './AppLayout';

export function AppRoutes() {
  return (
    <Routes>
      {/* Вне RequireAuth: возврат сюда после RP-initiated logout не должен
          перепроверять сессию сетевым запросом. */}
      <Route path="/signed-out" element={<AnonymousScreen />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route index element={<Navigate to={HOME_PATH} replace />} />
          <Route path={HOME_PATH} element={<HomePage />} />
          {/* Реестр языков и форма языка на двух путях (создание и
              редактирование) — каждая страница гейтирует свой маршрут сама
              (RequirePermission), поэтому здесь никакого дополнительного
              гейта. */}
          <Route path="/dictionaries/languages" element={<LanguagesRegistryPage />} />
          <Route path="/dictionaries/languages/new" element={<LanguageFormPage />} />
          <Route path="/dictionaries/languages/:code" element={<LanguageFormPage />} />
          {/* The UI kit's showcase (tech-015 design § 10.1): an instance-level settings
              page, outside any work context and outside the organization gate. The page
              gates itself (a permission, not a role) and makes no API request. */}
          <Route path={UI_KIT_PATH} element={<UiKitShowcasePage />} />
          {/* Organizations, projects, teams and areas (mod-002 design § 1):
              ten routes for eight pages inside the slice's pathless layout
              route, which hosts its notice. The two lists carry no
              organization and stand outside any gate: they are open to every
              signed-in user and hold the user's own organizations. Every other
              route sits under the organization gate (mod-016 design § 2.1,
              plan-frontend P-14), which reads the access of the organization
              in the address, and each page is wrapped by `OrganizationPage`
              with the read code the page needs, its title and the skeleton of
              its first rows: «Организация не найдена» for a non-member, «Доступ
              ограничен» for a missing code, one state order for every page.
              The static `deactivated` outranks `:organizationId`. */}
          <Route element={<OrganizationsSection />}>
            <Route path={ORGANIZATIONS_ROUTE} element={<OrganizationListPage view="active" />} />
            <Route
              path={DEACTIVATED_ORGANIZATIONS_ROUTE}
              element={<OrganizationListPage view="deactivated" />}
            />
            <Route element={<OrganizationGate />}>
              <Route
                path={ORGANIZATION_GENERAL_ROUTE}
                element={
                  <OrganizationPage
                    code={organizationsPermissions.organizationRead}
                    title="Общие"
                    skeleton="form"
                  >
                    <OrganizationGeneralPage />
                  </OrganizationPage>
                }
              />
              <Route
                path={PROJECTS_ROUTE}
                element={
                  <OrganizationPage
                    code={organizationsPermissions.projectRead}
                    title="Проекты"
                    skeleton="registry"
                    columns={['Название', 'Ключ']}
                  >
                    <ProjectListPage view="active" />
                  </OrganizationPage>
                }
              />
              <Route
                path={DEACTIVATED_PROJECTS_ROUTE}
                element={
                  <OrganizationPage
                    code={organizationsPermissions.projectDeactivate}
                    title="Проекты"
                    skeleton="registry"
                    columns={['Название', 'Ключ', '']}
                  >
                    <ProjectListPage view="deactivated" />
                  </OrganizationPage>
                }
              />
              <Route
                path={MEMBERS_ROUTE}
                element={
                  <OrganizationPage
                    code={organizationsPermissions.membershipRead}
                    title="Участники"
                    skeleton="registry"
                    columns={['Имя', 'Почта', 'Роль', 'Добавлен', 'Роль изменена']}
                  >
                    <MembersPage />
                  </OrganizationPage>
                }
              />
              <Route
                path={PROJECT_GENERAL_ROUTE}
                element={
                  <OrganizationPage
                    code={organizationsPermissions.projectRead}
                    title="Общие"
                    skeleton="form"
                  >
                    <ProjectGeneralPage />
                  </OrganizationPage>
                }
              />
              {/* feat-028: the teams are the organization's — their list and a team's page sit
                  in its settings (BR-OL-12). The project's «Команды» lists the teams linked to
                  it; `…/projects/:p/settings/teams/:t` is no route any more and the catch-all
                  answers «Страница не найдена». */}
              <Route
                path={ORG_TEAMS_ROUTE}
                element={
                  <OrganizationPage
                    code={organizationsPermissions.teamRead}
                    title="Команды"
                    skeleton="registry"
                    columns={['Команда', 'Проекты', 'Описание']}
                  >
                    <OrganizationTeamsPage />
                  </OrganizationPage>
                }
              />
              <Route
                path={ORG_TEAM_ROUTE}
                element={
                  <OrganizationPage
                    code={organizationsPermissions.teamRead}
                    title="Команда"
                    skeleton="form"
                  >
                    <TeamPage />
                  </OrganizationPage>
                }
              />
              {/* mod-018: the organization's area tree and an area's page; and the project's own subtree. */}
              <Route
                path={ORG_AREAS_ROUTE}
                element={
                  <OrganizationPage
                    code={organizationsPermissions.areaRead}
                    title="Области"
                    skeleton="registry"
                    columns={['Название', 'Тип', 'Описание']}
                  >
                    <AreasPage />
                  </OrganizationPage>
                }
              />
              <Route
                path={ORG_AREA_ROUTE}
                element={
                  <OrganizationPage
                    code={organizationsPermissions.areaRead}
                    title="Область"
                    skeleton="form"
                  >
                    <AreaPage />
                  </OrganizationPage>
                }
              />
              <Route
                path={AREAS_ROUTE}
                element={
                  <OrganizationPage
                    code={organizationsPermissions.areaRead}
                    title="Области"
                    skeleton="registry"
                    columns={['Название', 'Тип', 'Описание']}
                  >
                    <ProjectAreasPage />
                  </OrganizationPage>
                }
              />
              <Route
                path={TEAMS_ROUTE}
                element={
                  <OrganizationPage
                    code={organizationsPermissions.teamRead}
                    title="Команды"
                    skeleton="registry"
                    columns={['Команда']}
                  >
                    <TeamsPage />
                  </OrganizationPage>
                }
              />
            </Route>
          </Route>
          {/* The process, the work items, a team's backlog and board: pages of
              an organization, each under the gate with its own instance (the
              slices that own them stay outside the organizations slice).
              The read-only process screen (mod-001 D-9) makes no permission
              check of its own: the wrapper does (design § 2.1). Work items
              (mod-003 spec § Screens and copy): the project's list and an
              item's form, both project-level pages (D-5). A team's boards and
              backlogs (mod-006 spec § Screens and copy), team-level pages:
              each answers a level that is not one of the three as an unknown
              address, and an address level it does not find with its own
              frame. */}
          <Route element={<OrganizationGate />}>
            <Route
              path={PROCESS_DEFINITION_ROUTE}
              element={
                <OrganizationPage
                  code={processPermissions.definitionRead}
                  title="Процесс"
                  skeleton="form"
                >
                  <ProcessDefinitionPage />
                </OrganizationPage>
              }
            />
            <Route
              path={WORK_ITEMS_ROUTE}
              element={
                <OrganizationPage
                  code={workItemsPermissions.workItemRead}
                  title="Рабочие элементы"
                  skeleton="registry"
                >
                  <WorkItemsPage />
                </OrganizationPage>
              }
            />
            <Route
              path={WORK_ITEM_ROUTE}
              element={
                <OrganizationPage
                  code={workItemsPermissions.workItemRead}
                  title="Рабочий элемент"
                  skeleton="form"
                >
                  <WorkItemFormPage />
                </OrganizationPage>
              }
            />
            <Route
              path={BOARD_ROUTE}
              element={
                <OrganizationPage
                  code={boardsPermissions.workItemRead}
                  title="Канбан"
                  skeleton="registry"
                >
                  <BoardPage />
                </OrganizationPage>
              }
            />
            <Route
              path={BACKLOG_ROUTE}
              element={
                <OrganizationPage
                  code={boardsPermissions.workItemRead}
                  title="Бэклоги"
                  skeleton="registry"
                >
                  <BacklogPage />
                </OrganizationPage>
              }
            />
            {/* The project's own backlog and board (feat-039 BR-PB-1), project-level
                pages of the same two entries (BR-PB-9): the same wrapper and read
                code as a team's, an address `:level` the page does not serve or a
                project or level the read does not find answered by its own frame. */}
            <Route
              path={PROJECT_BOARD_ROUTE}
              element={
                <OrganizationPage
                  code={boardsPermissions.workItemRead}
                  title="Канбан"
                  skeleton="registry"
                >
                  <ProjectBoardPage />
                </OrganizationPage>
              }
            />
            <Route
              path={PROJECT_BACKLOG_ROUTE}
              element={
                <OrganizationPage
                  code={boardsPermissions.workItemRead}
                  title="Бэклоги"
                  skeleton="registry"
                >
                  <ProjectBacklogPage />
                </OrganizationPage>
              }
            />
          </Route>
          {/* An unknown address keeps the address and answers inside the
              shell — no redirect to «Главная» (mod-015 BR-13, D-13). */}
          <Route path="*" element={<NotFound level="page" />} />
        </Route>
      </Route>
    </Routes>
  );
}
