import { Navigate, Route, Routes } from 'react-router-dom';
import { AnonymousScreen } from '../features/auth/AnonymousScreen';
import { RequireAuth } from '../features/auth/RequireAuth';
import { LanguageFormPage, LanguagesRegistryPage } from '../features/dictionaries';
import { HomePage } from '../features/home/HomePage';
import {
  AREAS_ROUTE,
  AreasPage,
  DEACTIVATED_ORGANIZATIONS_ROUTE,
  DEACTIVATED_PROJECTS_ROUTE,
  ORGANIZATIONS_ROUTE,
  ORGANIZATION_GENERAL_ROUTE,
  OrganizationGeneralPage,
  OrganizationListPage,
  OrganizationsSection,
  PROJECTS_ROUTE,
  PROJECT_GENERAL_ROUTE,
  ProjectGeneralPage,
  ProjectListPage,
  TEAMS_ROUTE,
  TEAM_ROUTE,
  TeamPage,
  TeamsPage,
} from '../features/organizations';
import { PROCESS_DEFINITION_ROUTE, ProcessDefinitionPage } from '../features/process';
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
          {/* The read-only process screen (mod-001 D-9). No gate on the
              route: the page makes no permission check (design DD-6), and
              the API answers a missing organization with its own code. */}
          <Route path={PROCESS_DEFINITION_ROUTE} element={<ProcessDefinitionPage />} />
          {/* Organizations, projects, teams and areas (mod-002 design § 1):
              nine routes for seven pages inside the slice's pathless layout
              route, which hosts its notice. No gate on any route: every
              signed-in user passes until mod-016 (BR-M1-19), and each page
              answers a context it does not find with its own frame. The
              static `deactivated` outranks `:organizationId`. */}
          <Route element={<OrganizationsSection />}>
            <Route path={ORGANIZATIONS_ROUTE} element={<OrganizationListPage view="active" />} />
            <Route
              path={DEACTIVATED_ORGANIZATIONS_ROUTE}
              element={<OrganizationListPage view="deactivated" />}
            />
            <Route path={ORGANIZATION_GENERAL_ROUTE} element={<OrganizationGeneralPage />} />
            <Route path={PROJECTS_ROUTE} element={<ProjectListPage view="active" />} />
            <Route
              path={DEACTIVATED_PROJECTS_ROUTE}
              element={<ProjectListPage view="deactivated" />}
            />
            <Route path={PROJECT_GENERAL_ROUTE} element={<ProjectGeneralPage />} />
            <Route path={TEAMS_ROUTE} element={<TeamsPage />} />
            <Route path={TEAM_ROUTE} element={<TeamPage />} />
            <Route path={AREAS_ROUTE} element={<AreasPage />} />
          </Route>
          {/* An unknown address keeps the address and answers inside the
              shell — no redirect to «Главная» (mod-015 BR-13, D-13). */}
          <Route path="*" element={<NotFound level="page" />} />
        </Route>
      </Route>
    </Routes>
  );
}
