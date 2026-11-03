import { afterEach, describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import { AuthContext } from '../features/auth/AuthContext';
import { API, PROBLEM_HEADERS, ids, problem } from '../features/organizations/testFixtures';
import { serveOrganizations } from '../features/organizations/testApi';
import { server } from '../test/mocks/server';
import { authValue, LANGUAGES_READ, LocationProbe } from '../test/renderShell';
import { RETIRED_ADDRESSES } from './navigation/addressModel';
import { AppRoutes } from './routes';

// feat-043 QA, US-1 «A retired address answers «Страница не найдена» and redirects nowhere»
// (BR-NV-6, BR-NV-7) through the real routes, shell and gate: every address of the retired table,
// and the addresses that belong to feat-048 and mod-007 and are not served before them.

const ORG = ids.alfa;
const FILL: Record<string, string> = {
  organizationId: ORG,
  teamId: ids.platformTeam,
  areaId: ids.platformArea,
  projectId: ids.prm,
};

function concrete(pattern: string): string {
  return pattern.replace(/:(\w+)/g, (_match, name: string) => FILL[name] ?? 'x1');
}

const RETIRED = RETIRED_ADDRESSES.map((retired) => [retired.was, concrete(retired.pattern)]);
const NOT_YET = [
  ['the organization’s «Рабочие элементы» (feat-048)', `/organizations/${ORG}/workitems`],
  ['the organization’s «Канбан» (feat-048)', `/organizations/${ORG}/boards/stories`],
  ['the organization’s «Бэклог» (feat-048)', `/organizations/${ORG}/backlogs/stories`],
  [
    'a department’s «Рабочие элементы» (feat-048)',
    `/organizations/${ORG}/areas/${ids.platformArea}/workitems`,
  ],
  [
    'a department’s «Канбан» (feat-048)',
    `/organizations/${ORG}/areas/${ids.platformArea}/boards/stories`,
  ],
  [
    'a department’s «Бэклог» (feat-048)',
    `/organizations/${ORG}/areas/${ids.platformArea}/backlogs/stories`,
  ],
  [
    'a department’s «Участники» (mod-019)',
    `/organizations/${ORG}/areas/${ids.platformArea}/members`,
  ],
  [
    'a team’s «Рабочие элементы» (feat-048)',
    `/organizations/${ORG}/teams/${ids.platformTeam}/workitems`,
  ],
  ['a team’s «Спринты» (mod-007)', `/organizations/${ORG}/teams/${ids.platformTeam}/sprints`],
  ['a project’s «Спринты» (mod-007)', `/organizations/${ORG}/projects/${ids.prm}/sprints`],
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

async function settled() {
  await waitFor(() =>
    expect(screen.queryByRole('status', { name: 'Загрузка данных' })).not.toBeInTheDocument(),
  );
}

describe('feat-043 QA, BR-NV-6: a retired address answers «Страница не найдена»', () => {
  it.each([...RETIRED, ...NOT_YET])(
    '%s: %s keeps its address and marks no entry',
    async (_name, path) => {
      serveOrganizations();
      renderApp(path as string);

      await settled();
      expect(
        await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
      ).toBeInTheDocument();
      expect(screen.getByTestId('location')).toHaveTextContent(path as string);
      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
      const navigation = screen.getByRole('navigation', { name: 'Основное меню' });
      expect(navigation.querySelector('[aria-current="page"]')).toBeNull();
    },
  );

  it.each(RETIRED)('%s: %s is «Организация не найдена» to a non-member', async (_name, path) => {
    serveOrganizations();
    server.use(
      http.get(`${API}/api/organizations/:organizationId/access`, () =>
        HttpResponse.json(problem('organizations.organization.not_found'), {
          status: 404,
          headers: PROBLEM_HEADERS,
        }),
      ),
    );
    renderApp(path as string);

    await settled();
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent(path as string);
  });
});
