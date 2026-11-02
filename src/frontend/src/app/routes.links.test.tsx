import { afterEach, describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { AuthContext } from '../features/auth/AuthContext';
import { ids } from '../features/organizations/testFixtures';
import { serveOrganizations } from '../features/organizations/testApi';
import { authValue, LANGUAGES_READ, LocationProbe } from '../test/renderShell';
import { isRetiredAddress } from './navigation/addressModel';
import { AppRoutes } from './routes';

// feat-043 US-1 / BR-NV-6 (QA F-4): no link in the panel targets a retired address. The menu table
// is held by `addressModel.test`; this walks the links the pages themselves draw (page bodies,
// «Обзор» rows, row links, the shell's menus) through the real routes, shell and gate.

const ORG = `/organizations/${ids.alfa}`;
const PAGES: readonly (readonly [string, string])[] = [
  ['the organization’s «Обзор»', `${ORG}/overview`],
  ['«Области»', `${ORG}/areas`],
  ['a department', `${ORG}/areas/${ids.platformArea}/overview`],
  ['a project’s node', `${ORG}/areas/${ids.prmRoot}/overview`],
  ['«Проекты»', `${ORG}/projects`],
  ['a project’s «Общие»', `${ORG}/projects/${ids.prm}/general`],
  ['a project’s «Команды»', `${ORG}/projects/${ids.prm}/teams`],
  ['a project’s «Параметры»', `${ORG}/projects/${ids.prm}/settings`],
  ['«Команды»', `${ORG}/teams`],
  ['a team’s «Обзор»', `${ORG}/teams/${ids.platformTeam}/overview`],
  ['a team’s «Проекты»', `${ORG}/teams/${ids.platformTeam}/projects`],
  ['a team’s «Участники и доступ»', `${ORG}/teams/${ids.platformTeam}/members`],
  ['a team’s «Параметры»', `${ORG}/teams/${ids.platformTeam}/settings`],
  ['a department’s «Параметры»', `${ORG}/areas/${ids.platformArea}/settings`],
  ['«Участники»', `${ORG}/members`],
  ['the organization’s «Общие»', `${ORG}/settings/general`],
  [
    '«Общие» with every object chosen',
    `${ORG}/settings/general?teamId=${ids.platformTeam}&departmentId=${ids.platformArea}&projectId=${ids.prm}`,
  ],
];

afterEach(() => {
  localStorage.clear();
});

function renderApp(path: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={{ ...authValue([LANGUAGES_READ]), subject: 'user-1' }}>
        <MemoryRouter initialEntries={[path]}>
          <AppRoutes />
          <LocationProbe />
        </MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
}

describe('feat-043 QA F-4, BR-NV-6: no rendered link targets a retired address', () => {
  it.each(PAGES)('%s', async (_name, path) => {
    serveOrganizations();
    renderApp(path);

    await screen.findByRole('heading', { level: 1 });
    await waitFor(() =>
      expect(screen.queryByRole('status', { name: 'Загрузка данных' })).not.toBeInTheDocument(),
    );
    const targets = Array.from(document.querySelectorAll<HTMLAnchorElement>('a[href]')).map(
      (link) => new URL(link.getAttribute('href') ?? '', 'http://panel.test'),
    );

    // The scan is not empty: the shell's menu alone links into the organization.
    expect(targets.some((url) => url.pathname.startsWith('/organizations/'))).toBe(true);
    expect(targets.filter((url) => isRetiredAddress(decodeURI(url.pathname)))).toEqual([]);
  });
});
