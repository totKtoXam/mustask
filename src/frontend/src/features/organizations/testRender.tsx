/* eslint-disable react-refresh/only-export-components -- a test-only module:
   the probe lives beside the helper that mounts it. */
import { StrictMode } from 'react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { vi } from 'vitest';
import { AccessCheckContext, type AccessCheck } from '../../shared/lib/access';
import { WorkContextContext, type WorkContextSource } from '../../shared/lib/workContext';
import { OrganizationsSection } from './components/OrganizationsSection';
import { OrganizationGeneralPage } from './pages/OrganizationGeneralPage';
import { OrganizationListPage } from './pages/OrganizationListPage';
import { ProjectGeneralPage } from './pages/ProjectGeneralPage';
import { ProjectListPage } from './pages/ProjectListPage';
import { AreasPage } from './pages/AreasPage';
import { TeamPage } from './pages/TeamPage';
import { TeamsPage } from './pages/TeamsPage';
import {
  AREAS_ROUTE,
  DEACTIVATED_ORGANIZATIONS_ROUTE,
  DEACTIVATED_PROJECTS_ROUTE,
  ORGANIZATIONS_ROUTE,
  ORGANIZATION_GENERAL_ROUTE,
  PROJECTS_ROUTE,
  PROJECT_GENERAL_ROUTE,
  TEAMS_ROUTE,
  TEAM_ROUTE,
} from './paths';
import { organizationsPermissions } from './permissions';

/** Shows the router's address, so a test reads where an action led. */
export function LocationProbe() {
  const { pathname } = useLocation();
  return (
    <div data-testid="location" hidden>
      {pathname}
    </div>
  );
}

/** The holder of the create right (the realm role or `admin`, D-i). */
export const CREATE_RIGHT = [organizationsPermissions.organizationCreate];

/** A context source with no selection: what a page sees outside the switcher's choice. */
export function contextSource(overrides: Partial<WorkContextSource> = {}): WorkContextSource {
  return {
    status: 'ready',
    organizations: [],
    selection: {},
    isReal: true,
    select: vi.fn(),
    ...overrides,
  };
}

export interface PageRenderOptions {
  /** The address to open. */
  path: string;
  /** Instance codes in the token; the scoped ones pass for any signed-in user (BR-M1-19). */
  permissions?: readonly string[];
  context?: WorkContextSource;
  /** Under `<StrictMode>`, as `src/main.tsx` renders the panel in development (qa.md D-2). */
  strict?: boolean;
}

/**
 * Renders the slice's pages at their routes inside `OrganizationsSection`,
 * a router, a fresh `QueryClient` and a stub session, as `renderShell` does
 * for the shell: the page under test is the one `path` opens, and a flow
 * moves to the next page as the product does.
 */
export function renderPage({
  path,
  permissions = [],
  context = contextSource(),
  strict = false,
}: PageRenderOptions) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  function tree(currentPermissions: readonly string[], currentContext: WorkContextSource) {
    const access: AccessCheck = (code, scope) =>
      scope === 'instance' ? currentPermissions.includes(code) : true;
    return (
      <QueryClientProvider client={queryClient}>
        <AccessCheckContext.Provider value={access}>
          <WorkContextContext.Provider value={currentContext}>
            <MemoryRouter initialEntries={[path]}>
              <Routes>
                <Route element={<OrganizationsSection />}>
                  <Route
                    path={ORGANIZATIONS_ROUTE}
                    element={<OrganizationListPage view="active" />}
                  />
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
                  <Route path="*" element={<p>Другая страница</p>} />
                </Route>
              </Routes>
              <LocationProbe />
            </MemoryRouter>
          </WorkContextContext.Provider>
        </AccessCheckContext.Provider>
      </QueryClientProvider>
    );
  }

  let currentPermissions = permissions;
  let currentContext = context;
  const result = render(
    tree(currentPermissions, currentContext),
    strict ? { wrapper: StrictMode } : undefined,
  );
  return {
    ...result,
    queryClient,
    /** Re-renders with a renewed token's codes or a new context, keeping state. */
    update(next: { permissions?: readonly string[]; context?: WorkContextSource }) {
      currentPermissions = next.permissions ?? currentPermissions;
      currentContext = next.context ?? currentContext;
      result.rerender(tree(currentPermissions, currentContext));
    },
  };
}
