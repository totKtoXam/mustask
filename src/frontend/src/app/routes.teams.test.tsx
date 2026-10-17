import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { AuthContext } from '../features/auth/AuthContext';
import { ids } from '../features/organizations/testFixtures';
import { serveOrganizations } from '../features/organizations/testApi';
import { server } from '../test/mocks/server';
import { authValue, LANGUAGES_READ, LocationProbe } from '../test/renderShell';
import { AppRoutes } from './routes';

// feat-028 QA: the retired project-nested team page (US-1 "the project-nested team page and routes are gone", BR-OL-12, design § 9).

const requests: string[] = [];
const onRequest = ({ request }: { request: Request }) => {
  requests.push(`${request.method} ${new URL(request.url).pathname}`);
};

beforeEach(() => {
  requests.length = 0;
  server.events.on('request:start', onRequest);
});

afterEach(() => {
  server.events.removeListener('request:start', onRequest);
});

function renderRoutes(path: string) {
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

describe('feat-028 US-1 Scenario: The project-nested team page is gone', () => {
  const retired = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams/${ids.platformTeam}`;

  it('answers «Страница не найдена» for a team the project links, and redirects nowhere', async () => {
    serveOrganizations();
    renderRoutes(retired);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
    ).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent(retired);
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
  });

  it('reads no team of that address: the page asks the server for no team under the project', async () => {
    serveOrganizations();
    renderRoutes(retired);

    await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' });
    await waitFor(() =>
      expect(screen.queryByRole('status', { name: 'Загрузка данных' })).toBeNull(),
    );

    expect(requests.filter((request) => request.includes(`/projects/${ids.prm}/teams/`))).toEqual(
      [],
    );
  });
});
