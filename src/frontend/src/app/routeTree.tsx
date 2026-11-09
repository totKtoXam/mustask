import { Navigate, Route, useLocation, useParams, useSearchParams } from 'react-router-dom';
import { AnonymousScreen } from '../features/auth/AnonymousScreen';
import { RequireAuth } from '../features/auth/RequireAuth';
import {
  BACKLOG_ROUTE,
  BOARD_ROUTE,
  DEPARTMENT_BACKLOG_ROUTE,
  DEPARTMENT_BOARD_ROUTE,
  DEPARTMENT_WORK_ITEMS_ROUTE,
  ORGANIZATION_BACKLOG_ROUTE,
  ORGANIZATION_BOARD_ROUTE,
  ORGANIZATION_WORK_ITEMS_ROUTE,
  TEAM_WORK_ITEMS_ROUTE,
  PROJECT_BACKLOG_ROUTE,
  PROJECT_BOARD_ROUTE,
  AggregateBacklogPage,
  AggregateBoardPage,
  AggregateWorkItemsPage,
  ProjectBacklogRoute,
  ProjectBoardRoute,
  ProjectWorkItemsRoute,
  TeamBacklogRoute,
  TeamBoardRoute,
  boardsPermissions,
} from '../features/boards';
import { LanguageFormPage, LanguagesRegistryPage } from '../features/dictionaries';
import { HomePage } from '../features/home/HomePage';
import {
  ADMIN_ORGANIZATIONS_PATH,
  AREA_MEMBERS_ROUTE,
  AREA_PREFIX_ROUTE,
  AREA_PROJECTS_ROUTE,
  AREA_SETTINGS_ROUTE,
  AREA_TEAMS_ROUTE,
  AllOrganizationsPage,
  AreaMembersPage,
  AreaPage,
  AreasPage,
  DEACTIVATED_ORGANIZATIONS_ROUTE,
  MEMBERS_ROUTE,
  MembersPage,
  ORGANIZATIONS_ROUTE,
  ORGANIZATION_GENERAL_ROUTE,
  ORGANIZATION_OVERVIEW_ROUTE,
  ORG_AREAS_ROUTE,
  ORG_AREA_ROUTE,
  ORG_TEAMS_ROUTE,
  ORG_TEAM_ROUTE,
  OrganizationGate,
  OrganizationGeneralPage,
  OrganizationListPage,
  OrganizationOverviewPage,
  OrganizationPage,
  OrganizationTeamsPage,
  OrganizationsSection,
  PROJECTS_ROUTE,
  PROJECT_ACCESS_ROUTE,
  PROJECT_GENERAL_ROUTE,
  PROJECT_PREFIX_ROUTE,
  PROJECT_SETTINGS_ROUTE,
  ProjectAccessPage,
  ProjectGeneralPage,
  ProjectListPage,
  TEAMS_ROUTE,
  TEAM_MEMBERS_ROUTE,
  TEAM_PREFIX_ROUTE,
  TEAM_PROJECTS_ROUTE,
  TEAM_SETTINGS_ROUTE,
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
  workItemsPermissions,
} from '../features/workitems';
import { HOME_PATH } from '../shared/config/paths';
import { NotFound } from '../shared/ui/StateFrame';
import { AppLayout } from './AppLayout';
import { KIND_ROUTE_PREFIX, menuFor, type MenuKind } from './navigation/addressModel';

const permissions = organizationsPermissions;

/**
 * A bare object address opens the first entry of the object's menu, replacing the history entry
 * (ADR-0032 § 1, design D-4); the query (`teamId`, `departmentId`) rides along.
 */
function OpensFirstEntry({ kind }: { kind: MenuKind }) {
  const params = useParams();
  const { search } = useLocation();
  const prefix = KIND_ROUTE_PREFIX[kind].replace(/:(\w+)/g, (_match, name: string) =>
    encodeURIComponent(params[name] ?? ''),
  );
  const first = menuFor(kind)[0];
  return <Navigate to={`${prefix}/${first?.segment ?? 'overview'}${search}`} replace />;
}

/** «Проекты» of the organization; `?status=deactivated` is the administrator's other view (D-5). */
function ProjectsRoute() {
  const [params] = useSearchParams();
  const deactivated = params.get('status') === 'deactivated';
  return (
    <OrganizationPage
      code={deactivated ? permissions.projectDeactivate : permissions.projectRead}
      title="Проекты"
      skeleton="registry"
      columns={deactivated ? ['Название', 'Ключ', ''] : ['Название', 'Ключ']}
    >
      <ProjectListPage view={deactivated ? 'deactivated' : 'active'} />
    </OrganizationPage>
  );
}

/**
 * The route table, in one place: the router renders it (`AppRoutes`) and the route-inventory test
 * reads it (every menu segment has a route, no retired address has one). The pages of an object's
 * menu are the interim ones until plan steps 9–12 move their bodies; the addresses are final.
 */
export const routeTree = (
  <>
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
            page, outside any work context and outside the organization gate. */}
        <Route path={UI_KIT_PATH} element={<UiKitShowcasePage />} />
        {/* The organization's pages (feat-043 design § 2, ADR-0032 §§ 1–2): the slice's pathless
            layout route hosts its notice. The two lists carry no organization and stand outside
            any gate. Every other route sits under the organization gate (mod-016 design § 2.1),
            and each page is wrapped by `OrganizationPage` with the read code the page needs:
            «Организация не найдена» for a non-member, «Доступ ограничен» for a missing code. The
            static `deactivated` outranks `:organizationId`. */}
        <Route element={<OrganizationsSection />}>
          {/* «Все организации» (feat-033 DC-1): instance-scoped, outside any organization and gate; the
              page decides by the code in the token. It stands here for the slice notice host. */}
          <Route path={ADMIN_ORGANIZATIONS_PATH} element={<AllOrganizationsPage />} />
          <Route path={ORGANIZATIONS_ROUTE} element={<OrganizationListPage view="active" />} />
          <Route
            path={DEACTIVATED_ORGANIZATIONS_ROUTE}
            element={<OrganizationListPage view="deactivated" />}
          />
          <Route element={<OrganizationGate />}>
            {/* The organization, nothing chosen. */}
            <Route
              path="/organizations/:organizationId"
              element={<OpensFirstEntry kind="organization" />}
            />
            <Route
              path={ORGANIZATION_OVERVIEW_ROUTE}
              element={
                <OrganizationPage code={permissions.organizationRead} title="Обзор" skeleton="form">
                  <OrganizationOverviewPage />
                </OrganizationPage>
              }
            />
            <Route
              path={ORG_AREAS_ROUTE}
              element={
                <OrganizationPage
                  code={permissions.areaRead}
                  title="Структура"
                  skeleton="registry"
                  columns={['Название', 'Тип', 'Описание']}
                >
                  <AreasPage />
                </OrganizationPage>
              }
            />
            <Route path={PROJECTS_ROUTE} element={<ProjectsRoute />} />
            <Route
              path={ORG_TEAMS_ROUTE}
              element={
                <OrganizationPage
                  code={permissions.teamRead}
                  title="Команды"
                  skeleton="registry"
                  columns={['Команда', 'Проекты', 'Описание']}
                >
                  <OrganizationTeamsPage />
                </OrganizationPage>
              }
            />
            <Route
              path={MEMBERS_ROUTE}
              element={
                <OrganizationPage
                  code={permissions.membershipRead}
                  title="Участники"
                  skeleton="registry"
                  columns={['Имя', 'Почта', 'Роль', 'Добавлен', 'Роль изменена']}
                >
                  <MembersPage />
                </OrganizationPage>
              }
            />
            <Route
              path={ORGANIZATION_GENERAL_ROUTE}
              element={
                <OrganizationPage code={permissions.organizationRead} title="Общие" skeleton="form">
                  <OrganizationGeneralPage />
                </OrganizationPage>
              }
            />
            {/* A department (and, on the same prefix, a project's node or a sub-area: the page
                resolves the id against the offers). The bare address opens «Обзор». */}
            <Route path={AREA_PREFIX_ROUTE} element={<OpensFirstEntry kind="department" />} />
            {[ORG_AREA_ROUTE, AREA_PROJECTS_ROUTE, AREA_TEAMS_ROUTE].map((path) => (
              <Route
                key={path}
                path={path}
                element={
                  <OrganizationPage code={permissions.areaRead} title="Область" skeleton="form">
                    <AreaPage />
                  </OrganizationPage>
                }
              />
            ))}
            <Route
              path={AREA_MEMBERS_ROUTE}
              element={
                <OrganizationPage code={permissions.areaRead} title="Участники" skeleton="registry">
                  <AreaMembersPage />
                </OrganizationPage>
              }
            />
            <Route
              path={AREA_SETTINGS_ROUTE}
              element={
                <OrganizationPage code={permissions.areaRead} title="Область" skeleton="form">
                  <AreaPage />
                </OrganizationPage>
              }
            />
            {/* A team: «Обзор», «Проекты», «Участники и доступ» and «Параметры» here; its boards and
                backlogs are under the second gate. */}
            <Route path={TEAM_PREFIX_ROUTE} element={<OpensFirstEntry kind="team" />} />
            {[ORG_TEAM_ROUTE, TEAM_PROJECTS_ROUTE, TEAM_MEMBERS_ROUTE].map((path) => (
              <Route
                key={path}
                path={path}
                element={
                  <OrganizationPage code={permissions.teamRead} title="Команда" skeleton="form">
                    <TeamPage />
                  </OrganizationPage>
                }
              />
            ))}
            <Route
              path={TEAM_SETTINGS_ROUTE}
              element={
                <OrganizationPage code={permissions.teamRead} title="Команда" skeleton="form">
                  <TeamPage />
                </OrganizationPage>
              }
            />
            {/* A project: «Общие», «Команды» and «Параметры» here; its work items, board and
                backlog are under the second gate. */}
            <Route path={PROJECT_PREFIX_ROUTE} element={<OpensFirstEntry kind="project" />} />
            <Route
              path={PROJECT_GENERAL_ROUTE}
              element={
                <OrganizationPage code={permissions.projectRead} title="Общие" skeleton="form">
                  <ProjectGeneralPage />
                </OrganizationPage>
              }
            />
            <Route
              path={TEAMS_ROUTE}
              element={
                <OrganizationPage
                  code={permissions.teamRead}
                  title="Команды"
                  skeleton="registry"
                  columns={['Команда']}
                >
                  <TeamsPage />
                </OrganizationPage>
              }
            />
            <Route
              path={PROJECT_ACCESS_ROUTE}
              element={
                <OrganizationPage
                  code={permissions.projectRead}
                  title="Участники и доступ"
                  skeleton="registry"
                >
                  <ProjectAccessPage />
                </OrganizationPage>
              }
            />
            <Route
              path={PROJECT_SETTINGS_ROUTE}
              element={
                <OrganizationPage code={permissions.projectRead} title="Общие" skeleton="form">
                  <ProjectGeneralPage />
                </OrganizationPage>
              }
            />
            {/* Everything else under an organization — the retired addresses of ADR-0032 § 4
                among it — answers «Страница не найдена» inside the gate, so a non-member sees
                «Организация не найдена» first. */}
            <Route
              path="/organizations/:organizationId/*"
              element={
                <OrganizationPage
                  code={permissions.organizationRead}
                  title="Страница не найдена"
                  skeleton="form"
                >
                  <NotFound level="page" />
                </OrganizationPage>
              }
            />
          </Route>
        </Route>
        {/* The process, the work items, a team's backlog and board: pages of an organization,
            each under the gate with its own instance (the slices that own them stay outside the
            organizations slice). */}
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
                <ProjectWorkItemsRoute />
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
                <TeamBoardRoute />
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
                <TeamBacklogRoute />
              </OrganizationPage>
            }
          />
          {/* feat-048: the views above the project level, grouped by the user's choice. */}
          <Route
            path={ORGANIZATION_BOARD_ROUTE}
            element={
              <OrganizationPage
                code={boardsPermissions.workItemRead}
                title="Канбан"
                skeleton="registry"
              >
                <AggregateBoardPage />
              </OrganizationPage>
            }
          />
          <Route
            path={ORGANIZATION_BACKLOG_ROUTE}
            element={
              <OrganizationPage
                code={boardsPermissions.workItemRead}
                title="Бэклог"
                skeleton="registry"
              >
                <AggregateBacklogPage />
              </OrganizationPage>
            }
          />
          <Route
            path={ORGANIZATION_WORK_ITEMS_ROUTE}
            element={
              <OrganizationPage
                code={workItemsPermissions.workItemRead}
                title="Рабочие элементы"
                skeleton="registry"
              >
                <AggregateWorkItemsPage />
              </OrganizationPage>
            }
          />
          <Route
            path={DEPARTMENT_BOARD_ROUTE}
            element={
              <OrganizationPage
                code={boardsPermissions.workItemRead}
                title="Канбан"
                skeleton="registry"
              >
                <AggregateBoardPage />
              </OrganizationPage>
            }
          />
          <Route
            path={DEPARTMENT_BACKLOG_ROUTE}
            element={
              <OrganizationPage
                code={boardsPermissions.workItemRead}
                title="Бэклог"
                skeleton="registry"
              >
                <AggregateBacklogPage />
              </OrganizationPage>
            }
          />
          <Route
            path={DEPARTMENT_WORK_ITEMS_ROUTE}
            element={
              <OrganizationPage
                code={workItemsPermissions.workItemRead}
                title="Рабочие элементы"
                skeleton="registry"
              >
                <AggregateWorkItemsPage />
              </OrganizationPage>
            }
          />
          <Route
            path={TEAM_WORK_ITEMS_ROUTE}
            element={
              <OrganizationPage
                code={workItemsPermissions.workItemRead}
                title="Рабочие элементы"
                skeleton="registry"
              >
                <AggregateWorkItemsPage />
              </OrganizationPage>
            }
          />
          {/* The project's own backlog and board (feat-039 BR-PB-1). */}
          <Route
            path={PROJECT_BOARD_ROUTE}
            element={
              <OrganizationPage
                code={boardsPermissions.workItemRead}
                title="Канбан"
                skeleton="registry"
              >
                <ProjectBoardRoute />
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
                <ProjectBacklogRoute />
              </OrganizationPage>
            }
          />
        </Route>
        {/* An unknown address keeps the address and answers inside the shell — no redirect to
            «Главная» (mod-015 BR-13, D-13). */}
        <Route path="*" element={<NotFound level="page" />} />
      </Route>
    </Route>
  </>
);
