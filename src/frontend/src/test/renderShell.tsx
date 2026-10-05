/* eslint-disable react-refresh/only-export-components -- a test-only module:
   the stub pages live beside the helper that mounts them. */
import type { ReactNode } from 'react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { vi } from 'vitest';
import { AppLayout } from '../app/AppLayout';
import { navigationModel } from '../app/navigation/model';
import { NavigationModelContext } from '../app/navigation/NavigationModelContext';
import type { NavigationModel } from '../app/navigation/types';
import { PLACEHOLDER_ORGANIZATIONS, useStaticContextSource } from '../app/shell/placeholderContext';
import { AuthContext, type AuthContextValue } from '../features/auth/AuthContext';
import { useAccessCheck } from '../shared/lib/access';
import type { WorkContextSource } from '../shared/lib/workContext';
import { PageHeader } from '../shared/ui/PageHeader';
import { AccessDenied, NotFound } from '../shared/ui/StateFrame';

export const LANGUAGES_READ = 'dictionaries.languages.read';
export const LANGUAGES_WRITE = 'dictionaries.languages.write';

/** A signed-in session whose `can` answers from `permissions`. */
export function authValue(
  permissions: readonly string[],
  overrides: Partial<AuthContextValue> = {},
): AuthContextValue {
  return {
    status: 'authenticated',
    reason: null,
    loginFailedCode: null,
    displayName: 'Иван Петров',
    can: (code) => permissions.includes(code),
    signIn: vi.fn(),
    signOut: vi.fn(),
    retryBootstrap: vi.fn(),
    retrySignOut: vi.fn(),
    ...overrides,
  };
}

/** Shows the router's current address, so a test can read where a link led. */
export function LocationProbe() {
  const { pathname } = useLocation();
  return (
    <div data-testid="location" hidden>
      {pathname}
    </div>
  );
}

/** A settings page gated the way the real registry is: it answers «Доступ ограничен» without the code. */
function StubLanguagesPage({ title }: { title: string }) {
  const access = useAccessCheck();
  return access(LANGUAGES_READ, 'instance') ? (
    <>
      <PageHeader title={title} />
      <button type="button">Действие страницы</button>
    </>
  ) : (
    <AccessDenied />
  );
}

/** The shell's own routes with stub pages: each page is a header and one control. */
export function stubRoutes(): ReactNode {
  return (
    <>
      <Route
        path="/home"
        element={
          <>
            <PageHeader title="Главная" />
            <button type="button">Действие страницы</button>
          </>
        }
      />
      <Route path="/dictionaries/languages" element={<StubLanguagesPage title="Языки" />} />
      <Route
        path="/dictionaries/languages/new"
        element={<StubLanguagesPage title="Добавить язык" />}
      />
      <Route path="*" element={<NotFound level="page" />} />
    </>
  );
}

/**
 * `AppLayout` with the test's source, or the placeholder dataset when the
 * test passes none: the shell's own tests run on the placeholder whatever
 * source the product mounts (mod-002 plan-frontend § 2.4). One element kept
 * across `update()`, so switching to a stub keeps the shell's state.
 */
function ShellWithSource({ source }: { source?: WorkContextSource }) {
  const placeholder = useStaticContextSource(PLACEHOLDER_ORGANIZATIONS);
  return <AppLayout contextSource={source ?? placeholder} />;
}

export interface ShellOptions {
  path?: string;
  auth?: AuthContextValue;
  contextSource?: WorkContextSource;
  model?: NavigationModel;
  routes?: ReactNode;
}

/** Mounts `AppLayout` the way `app/routes.tsx` does, under a stub session. */
export function renderShell({
  path = '/home',
  auth = authValue([LANGUAGES_READ, LANGUAGES_WRITE]),
  contextSource,
  model = navigationModel,
  routes = stubRoutes(),
}: ShellOptions = {}) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  function tree(currentAuth: AuthContextValue, currentSource?: WorkContextSource) {
    return (
      <QueryClientProvider client={queryClient}>
        <AuthContext.Provider value={currentAuth}>
          <NavigationModelContext.Provider value={model}>
            <MemoryRouter initialEntries={[path]}>
              <Routes>
                <Route element={<ShellWithSource source={currentSource} />}>{routes}</Route>
              </Routes>
              <LocationProbe />
            </MemoryRouter>
          </NavigationModelContext.Provider>
        </AuthContext.Provider>
      </QueryClientProvider>
    );
  }

  let currentAuth = auth;
  let currentSource = contextSource;
  const result = render(tree(currentAuth, currentSource));
  return {
    ...result,
    /** Re-renders with a new session (a token renewal) or a new context source, keeping state. */
    update(next: { auth?: AuthContextValue; contextSource?: WorkContextSource }) {
      currentAuth = next.auth ?? currentAuth;
      currentSource = next.contextSource ?? currentSource;
      result.rerender(tree(currentAuth, currentSource));
    },
  };
}
