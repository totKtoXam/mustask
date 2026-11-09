// The page-level render of the frontend test kit (tech-035, docs/conventions/testing.md § 3):
// the real routes and the real shell, one call.
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import { AppRoutes } from '../app/routes';
import { AuthContext } from '../features/auth/AuthContext';
import { serveOrganizations } from '../features/organizations/testApi';
import type { Dataset } from '../features/organizations/testFixtures';
import { apiUrl } from './api';
import { server } from './mocks/server';
import { servedFor, type TestRole } from './organizationRoles';
import { authValue, LANGUAGES_READ, LocationProbe } from './renderShell';

export interface RenderPageOptions {
  /** The caller's role in every organization (served by `GET /api/organizations/{id}/access`). Default: administrator. */
  role?: TestRole;
  /** The organizations module's dataset the API answers from. Default: the stories' example. */
  data?: Dataset;
  /** The instance codes in the token. Default: the languages read. */
  permissions?: readonly string[];
  /** The token's `sub`. */
  subject?: string;
}

/**
 * Mounts the panel's real route table at `path` under a router, a fresh `QueryClient` without retries and
 * a signed-in session; serves `data` and the access of `role`. The address is readable as
 * `screen.getByTestId('location')`. A page test names a path, a role and (when the example is not
 * enough) the data — nothing else to assemble.
 */
export function renderPage(
  path: string,
  {
    role = 'admin',
    data,
    permissions = [LANGUAGES_READ],
    subject = 'user-kit',
  }: RenderPageOptions = {},
) {
  const api = serveOrganizations(data);
  if (role !== 'admin') {
    // The dataset's handler answers as the administrator; any other role is served over it.
    server.use(
      http.get(apiUrl('/api/organizations/:organizationId/access'), () =>
        HttpResponse.json(servedFor(role)),
      ),
    );
  }
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const result = render(
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={{ ...authValue(permissions), subject }}>
        <MemoryRouter initialEntries={[path]}>
          <AppRoutes />
          <LocationProbe />
        </MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
  return { ...result, queryClient, api };
}
