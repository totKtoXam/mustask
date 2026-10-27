/* eslint-disable react-refresh/only-export-components -- a test-only module:
   the probes live beside the helper that mounts them. */
import { useState, type ReactNode } from 'react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider, useMutation, useQuery } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import { apiFetch } from '../../../shared/api/httpClient';
import { AccessCheckContext, useAccessCheck } from '../../../shared/lib/access';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { NoticeCornerContext } from '../../../shared/lib/noticeCorner';
import {
  WorkContextContext,
  type WorkContextOrganization,
  type WorkContextSelection,
  type WorkContextSource,
} from '../../../shared/lib/workContext';
import { servedFor, type TestRole } from '../../../test/organizationRoles';
import { server } from '../../../test/mocks/server';
import { getOrganizations } from '../api/organizationsApi';
import { organizationsKeys } from '../api/queryKeys';
import { API, PROBLEM_HEADERS, problem } from '../testFixtures';
import { OrganizationGate } from './OrganizationGate';
import { OrganizationPage } from './OrganizationPage';
import { useServedAccessCheck } from './servedAccess';
import { useAccessWatcher } from './useAccessWatcher';

export const ORG_A = 'a0000000-0000-4000-8000-000000000001';
export const ORG_B = 'a0000000-0000-4000-8000-000000000002';

/** An organization-scoped page of the gate tests: any route with this shape is gated. */
export const PAGE_ROUTE = '/organizations/:organizationId/settings/things';

export function pageAt(organizationId: string): string {
  return `/organizations/${organizationId}/settings/things`;
}

/** The code the stub page needs, and one it only uses for a control. */
export const READ_CODE = 'organizations.project.read';
export const WRITE_CODE = 'organizations.project.write';

/** Serves `GET …/organizations/:id/access` from `answer`, or a 404/403 body. */
export function serveAccess(
  answer: (organizationId: string) => Response | Promise<Response> | TestRole,
  options: { once?: boolean } = {},
): { requests: string[] } {
  const requests: string[] = [];
  server.use(
    http.get(
      `${API}/api/organizations/:organizationId/access`,
      async ({ params }) => {
        const organizationId = String(params.organizationId);
        requests.push(organizationId);
        const result = await answer(organizationId);
        return typeof result === 'string' ? HttpResponse.json(servedFor(result)) : result;
      },
      { once: options.once ?? false },
    ),
  );
  return { requests };
}

/** The 404 «Организация не найдена» body, as the access read and every scoped call answer it. */
export function notFoundResponse(): Response {
  const body = problem('organizations.organization.not_found');
  return HttpResponse.json(body, { status: 404, headers: PROBLEM_HEADERS });
}

/** The 403 «Недостаточно прав» body. */
export function forbiddenResponse(): Response {
  const body = problem('organizations.access.forbidden');
  return HttpResponse.json(body, { status: 403, headers: PROBLEM_HEADERS });
}

/** Counts `GET /api/organizations` (the switcher's list) and answers `organizations`. */
export function serveOrganizationList(
  organizations: readonly { id: string; name: string }[] = [],
): { count(): number } {
  let count = 0;
  server.use(
    http.get(`${API}/api/organizations`, () => {
      count += 1;
      return HttpResponse.json(organizations);
    }),
  );
  return { count: () => count };
}

/**
 * Observes the organization list the way the switcher does, so that
 * invalidating it reads it again. Its first read is the baseline of
 * `serveOrganizationList().count()`.
 */
function ListObserver() {
  useQuery({
    queryKey: organizationsKeys.organizations('active'),
    queryFn: () => getOrganizations('active'),
  });
  return null;
}

/** The shell's check, built from the cache the way `AppLayout` builds it. */
function ServedChecks({ children }: { children: ReactNode }) {
  const { access } = useServedAccessCheck(() => false);
  return <AccessCheckContext.Provider value={access}>{children}</AccessCheckContext.Provider>;
}

/** Mounts the shell's watcher, and shows the role-change notice it raises as text. */
function WatcherProbe({
  names,
  fallbackShown,
}: {
  names: Readonly<Record<string, string>>;
  fallbackShown: boolean;
}) {
  const { roleChange, dismissRoleChange } = useAccessWatcher({
    fallbackShown,
    organizationName: (id) => names[id],
  });
  const [claims, setClaims] = useState(0);
  return (
    <NoticeCornerContext.Provider
      value={{
        roleChangeId: roleChange?.id ?? null,
        claim: () => {
          setClaims((count) => count + 1);
          dismissRoleChange();
        },
      }}
    >
      <p data-testid="role-change">{roleChange?.text ?? ''}</p>
      <p data-testid="claims">{claims}</p>
      <button type="button" onClick={dismissRoleChange}>
        Закрыть уведомление
      </button>
    </NoticeCornerContext.Provider>
  );
}

/** A page of the gate tests: a control only a writer is given, a button that fails a call. */
export function StubPage({ children }: { children?: ReactNode }) {
  const access = useAccessCheck();
  return (
    <>
      <h1>Страница</h1>
      {access(WRITE_CODE, 'organization') && <button type="button">Создать</button>}
      {children}
    </>
  );
}

/**
 * A page whose «Вызвать» sends a write under the organization — the kind of
 * call a dialog's submit makes — and shows the server's text when it is refused.
 */
export function StubCaller({ organizationId }: { organizationId: string }) {
  const call = useMutation({
    mutationFn: () =>
      apiFetch(`/api/organizations/${organizationId}/projects`, { method: 'POST', body: {} }),
  });
  return (
    <StubPage>
      <button type="button" onClick={() => call.mutate()}>
        Вызвать
      </button>
      {call.isError && <div role="alert">{apiErrorText(call.error)}</div>}
    </StubPage>
  );
}

export function LocationText() {
  const { pathname } = useLocation();
  return <p data-testid="location">{pathname}</p>;
}

export interface GateRenderOptions {
  path: string;
  /** The page's content; defaults to `StubPage`. */
  page?: ReactNode;
  /** The code the page needs (default: the read code). */
  code?: string;
  /** Names of the organizations the switcher lists, for the role-change notice. */
  names?: Readonly<Record<string, string>>;
  fallbackShown?: boolean;
  /**
   * The switcher's selection (default: none). A test of "whose codes" names an
   * organization other than the address's: the selection lags the address
   * (design § 2.6).
   */
  selection?: WorkContextSelection;
  /** Mount the shell's watcher (default: yes). */
  watcher?: boolean;
  queryClient?: QueryClient;
}

/** A work context source that lists `names` and shows no fallback. */
export function sourceOf(
  names: Readonly<Record<string, string>>,
  fallback = false,
  selection: WorkContextSelection = {},
): WorkContextSource {
  const organizations: WorkContextOrganization[] = Object.entries(names).map(([id, name]) => ({
    id,
    name,
    projects: [],
    teams: [],
  }));
  return {
    status: 'ready',
    organizations,
    selection,
    isReal: true,
    select: () => {},
    fallback: fallback ? { id: 1, names: ['x'] } : null,
  };
}

/**
 * The gate, a page under it and (by default) the shell's watcher, on a real
 * router and a fresh client: what `app/routes.tsx` and `AppLayout` assemble,
 * without the shell's chrome.
 */
export function renderGate({
  path,
  page = <StubPage />,
  code = READ_CODE,
  names = {},
  fallbackShown = false,
  selection = {},
  watcher = true,
  queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  }),
}: GateRenderOptions) {
  const result = render(
    <QueryClientProvider client={queryClient}>
      <WorkContextContext.Provider value={sourceOf(names, fallbackShown, selection)}>
        <ServedChecks>
          <MemoryRouter initialEntries={[path]}>
            <ListObserver />
            {watcher && <WatcherProbe names={names} fallbackShown={fallbackShown} />}
            <main id="main-content" tabIndex={-1}>
              <Routes>
                <Route element={<OrganizationGate />}>
                  <Route
                    path={PAGE_ROUTE}
                    element={
                      <OrganizationPage
                        code={code}
                        title="Вещи"
                        skeleton="registry"
                        columns={['Название', 'Ключ']}
                      >
                        {page}
                      </OrganizationPage>
                    }
                  />
                </Route>
                <Route path="*" element={<p>Другая страница</p>} />
              </Routes>
            </main>
            <LocationText />
          </MemoryRouter>
        </ServedChecks>
      </WorkContextContext.Provider>
    </QueryClientProvider>,
  );
  return { ...result, queryClient };
}
