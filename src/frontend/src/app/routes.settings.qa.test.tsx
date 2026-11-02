import { afterEach, describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AuthContext } from '../features/auth/AuthContext';
import { ids } from '../features/organizations/testFixtures';
import { serveOrganizations } from '../features/organizations/testApi';
import { authValue, LANGUAGES_READ, LocationProbe } from '../test/renderShell';
import { AppRoutes } from './routes';

// feat-043 QA round 2, F-3 fix (ADR-0032 §§ 1, 5): the organization's settings pages carry the
// whole selection in the query (`teamId`, `departmentId`, `projectId`); `projectId` is read there
// only. Probes of what the new query touches around it.

const ORG = `/organizations/${ids.alfa}`;

afterEach(() => {
  localStorage.clear();
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

function selectors(): string[] {
  return within(screen.getByRole('group', { name: 'Контекст работы' }))
    .getAllByRole('combobox')
    .map((button) => button.getAttribute('aria-label') ?? '');
}

function search(): string {
  return screen.getByTestId('location').getAttribute('data-search') ?? '';
}

describe('feat-043 QA round 2, F-3: the selection in the settings address', () => {
  it('moving inside the settings navigation keeps the selection in the address', async () => {
    serveOrganizations();
    renderRoutes(`${ORG}/settings/general?teamId=${ids.platformTeam}&projectId=${ids.prm}`);
    await waitFor(() => expect(selectors()[3]).toBe('Проект: Пример проекта'));
    const menu = await screen.findByRole('navigation', { name: 'Меню настроек' });
    await userEvent.click(await within(menu).findByRole('link', { name: 'Процесс' }));

    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent('/settings/process'),
    );
    expect(search()).toContain(`projectId=${ids.prm}`);
    expect(search()).toContain(`teamId=${ids.platformTeam}`);
    expect(selectors()).toEqual([
      'Организация: Альфа',
      'Департамент: все',
      'Команда: Платформа',
      'Проект: Пример проекта',
    ]);
  });

  // Round 2 F-7: ADR-0032 § 1 reads `projectId` on the settings pages only; the code reads it on
  // every address with no object prefix. `it.fails` passes while that stands.
  it.fails(
    'a project in the query is not the selection on the organization’s «Обзор»',
    async () => {
      serveOrganizations();
      renderRoutes(`${ORG}/overview?projectId=${ids.prm}`);

      await screen.findByRole('heading', { level: 1 });
      await waitFor(() => expect(selectors()[0]).toBe('Организация: Альфа'));
      // ADR-0032 § 1: «read only there», on the settings pages.
      expect(selectors()[3]).toBe('Проект: все');
    },
  );

  it('a project in the settings query that names nothing is dropped to «все», as a gone team is', async () => {
    serveOrganizations();
    renderRoutes(`${ORG}/settings/general?projectId=00000000-0000-4000-8000-0000000000ff`);

    await screen.findByRole('heading', { level: 1 });
    await waitFor(() => expect(selectors()[0]).toBe('Организация: Альфа'));
    expect(selectors()[3]).toBe('Проект: все');
  });
});
