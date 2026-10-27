/* eslint-disable react-refresh/only-export-components -- a test-only module:
   the probe lives beside the helper that mounts it. */
import { StrictMode } from 'react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { vi } from 'vitest';
import { AccessCheckContext, type AccessCheck } from '../../shared/lib/access';
import { AuthContext, type AuthContextValue } from '../auth/AuthContext';
import { WorkContextContext, type WorkContextSource } from '../../shared/lib/workContext';
import { checkFor, type TestRole } from '../../test/organizationRoles';
import { OrganizationsSection } from './components/OrganizationsSection';
import { OrganizationGeneralPage } from './pages/OrganizationGeneralPage';
import { OrganizationListPage } from './pages/OrganizationListPage';
import { MembersPage } from './pages/MembersPage';
import { ProjectGeneralPage } from './pages/ProjectGeneralPage';
import { ProjectListPage } from './pages/ProjectListPage';
import { AreaPage } from './pages/AreaPage';
import { AreasPage } from './pages/AreasPage';
import { OrganizationTeamsPage } from './pages/OrganizationTeamsPage';
import { TeamPage } from './pages/TeamPage';
import { TeamsPage } from './pages/TeamsPage';
import {
  ORG_AREAS_ROUTE,
  ORG_AREA_ROUTE,
  DEACTIVATED_ORGANIZATIONS_ROUTE,
  DEACTIVATED_PROJECTS_ROUTE,
  MEMBERS_ROUTE,
  ORGANIZATIONS_ROUTE,
  ORGANIZATION_GENERAL_ROUTE,
  PROJECTS_ROUTE,
  ORG_TEAMS_ROUTE,
  ORG_TEAM_ROUTE,
  PROJECT_GENERAL_ROUTE,
  TEAMS_ROUTE,
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

/** The holder of the create right (the instance code in the token, ADR-0018). */
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
  /** Instance codes in the token. */
  permissions?: readonly string[];
  /**
   * The caller's role in the page's organization (mod-016): the organization
   * codes the check answers are that role's bundle of api-contract § 0.2.
   * Default: the administrator, who holds every one. A page rendered here
   * stands without the gate, so this is the bound check the gate would give.
   */
  role?: TestRole;
  context?: WorkContextSource;
  /**
   * The signed-in user's id, the token's `sub` (mod-016): «Участники» marks
   * the row that is the caller's own by it. Default: `ACTING_USER`.
   */
  subject?: string;
  /** Under `<StrictMode>`, as `src/main.tsx` renders the panel in development (qa.md D-2). */
  strict?: boolean;
}

/** The user the pages are rendered as unless a test names another. */
export const ACTING_USER = 'user-acting';

/** A signed-in stub session: only the token's subject and instance codes matter to a page here. */
function sessionOf(subject: string, permissions: readonly string[]): AuthContextValue {
  return {
    status: 'authenticated',
    reason: null,
    loginFailedCode: null,
    displayName: 'Тестовый пользователь',
    subject,
    can: (code) => permissions.includes(code),
    signIn: vi.fn(),
    signOut: vi.fn(),
    retryBootstrap: vi.fn(),
    retrySignOut: vi.fn(),
  };
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
  role: initialRole = 'admin',
  context = contextSource(),
  subject = ACTING_USER,
  strict = false,
}: PageRenderOptions) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  function tree(
    currentPermissions: readonly string[],
    currentContext: WorkContextSource,
    role: TestRole,
  ) {
    const access: AccessCheck = checkFor(role, currentPermissions);
    return (
      <QueryClientProvider client={queryClient}>
        <AuthContext.Provider value={sessionOf(subject, currentPermissions)}>
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
                    <Route
                      path={ORGANIZATION_GENERAL_ROUTE}
                      element={<OrganizationGeneralPage />}
                    />
                    <Route path={PROJECTS_ROUTE} element={<ProjectListPage view="active" />} />
                    <Route
                      path={DEACTIVATED_PROJECTS_ROUTE}
                      element={<ProjectListPage view="deactivated" />}
                    />
                    <Route path={MEMBERS_ROUTE} element={<MembersPage />} />
                    <Route path={PROJECT_GENERAL_ROUTE} element={<ProjectGeneralPage />} />
                    <Route path={TEAMS_ROUTE} element={<TeamsPage />} />
                    <Route path={ORG_TEAMS_ROUTE} element={<OrganizationTeamsPage />} />
                    <Route path={ORG_TEAM_ROUTE} element={<TeamPage />} />
                    <Route path={ORG_AREAS_ROUTE} element={<AreasPage />} />
                    <Route path={ORG_AREA_ROUTE} element={<AreaPage />} />
                    <Route path="*" element={<p>Другая страница</p>} />
                  </Route>
                </Routes>
                <LocationProbe />
              </MemoryRouter>
            </WorkContextContext.Provider>
          </AccessCheckContext.Provider>
        </AuthContext.Provider>
      </QueryClientProvider>
    );
  }

  let currentPermissions = permissions;
  let currentRole = initialRole;
  let currentContext = context;
  const result = render(
    tree(currentPermissions, currentContext, currentRole),
    strict ? { wrapper: StrictMode } : undefined,
  );
  return {
    ...result,
    queryClient,
    /** Re-renders with a renewed token's codes or a new context, keeping state. */
    update(next: {
      permissions?: readonly string[];
      context?: WorkContextSource;
      /** The role the served codes now show (a re-read that found it changed). */
      role?: TestRole;
    }) {
      currentRole = next.role ?? currentRole;
      currentPermissions = next.permissions ?? currentPermissions;
      currentContext = next.context ?? currentContext;
      result.rerender(tree(currentPermissions, currentContext, currentRole));
    },
  };
}
