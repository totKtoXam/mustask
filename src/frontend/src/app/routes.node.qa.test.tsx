import { afterEach, describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { AuthContext } from '../features/auth/AuthContext';
import { API, ids } from '../features/organizations/testFixtures';
import { serveOrganizations } from '../features/organizations/testApi';
import { server } from '../test/mocks/server';
import { servedFor } from '../test/organizationRoles';
import { authValue, LANGUAGES_READ, LocationProbe } from '../test/renderShell';
import { AppRoutes } from './routes';

// feat-043 QA, US-3 «A node's page sets the project selector and shows the project's menu» and
// US-8 «The selectors name the selection, the menu the entry» (BR-NV-4, BR-NV-10): `areas/:areaId`
// is a department, a project's node or a sub-area, resolved by the shell against the offers and
// the node read, through the real routes, shell and gate.

const SUBJECT = 'user-1';
const AREAS = `/organizations/${ids.alfa}/areas`;

afterEach(() => {
  localStorage.clear();
});

function renderRoutes(path: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={{ ...authValue([LANGUAGES_READ]), subject: SUBJECT }}>
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

function menu(): string[] {
  return within(screen.getByRole('navigation', { name: 'Основное меню' }))
    .getAllByRole('link')
    .map((link) => link.textContent ?? '');
}

describe('feat-043 QA, US-3 and US-8: what `areas/:areaId` selects', () => {
  it.each([
    ['a project’s node', ids.prmRoot, 'Структура: Пример проекта'],
    ['a sub-area', ids.apiArea, 'Область: API'],
  ])(
    '%s sets the department and the project, shows the project’s menu and marks «Структура»',
    async (_name, areaId, title) => {
      serveOrganizations();
      renderRoutes(`${AREAS}/${areaId}/overview`);

      expect(await screen.findByRole('heading', { level: 1, name: title })).toBeInTheDocument();
      await waitFor(() =>
        expect(selectors()).toEqual([
          'Организация: Альфа',
          'Департамент: Платформа',
          'Команда: все',
          'Проект: Пример проекта',
        ]),
      );
      await waitFor(() =>
        expect(menu()).toEqual([
          'Главная',
          'Общие',
          'Структура',
          'Рабочие элементы',
          'Канбан',
          'Бэклог',
          'Команды',
          'Настройки',
        ]),
      );
      const links = within(screen.getByRole('navigation', { name: 'Основное меню' })).getAllByRole(
        'link',
      );
      const current = links.filter((link) => link.getAttribute('aria-current') === 'page');
      expect(current.map((link) => link.textContent)).toEqual(['Структура']);
    },
  );

  it('a department keeps the project selector at «все» and shows the department’s menu', async () => {
    serveOrganizations();
    renderRoutes(`${AREAS}/${ids.platformArea}/overview`);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Департамент: Платформа' }),
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(selectors()).toEqual([
        'Организация: Альфа',
        'Департамент: Платформа',
        'Команда: все',
        'Проект: все',
      ]),
    );
    await waitFor(() =>
      expect(menu()).toEqual(['Главная', 'Обзор', 'Проекты', 'Команды', 'Настройки']),
    );
  });

  it('an area of no kind answers «Область не найдена» and chooses nothing', async () => {
    serveOrganizations();
    renderRoutes(`${AREAS}/d0000000-0000-4000-8000-0000000000ff/overview`);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Область не найдена' }),
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(selectors()).toEqual([
        'Организация: Альфа',
        'Департамент: все',
        'Команда: все',
        'Проект: все',
      ]),
    );
  });
});

describe('feat-043 QA, US-1 «An address in another letter case or percent-encoded» (BR-NV-8)', () => {
  it.each([
    ['another letter case', `/Organizations/${ids.alfa}/Teams`],
    ['a percent-encoded letter', `/organizations/${ids.alfa}/%74eams`],
  ])('%s opens «Команды» with its entry marked', async (_name, path) => {
    serveOrganizations();
    renderRoutes(path);

    await waitFor(() =>
      expect(screen.queryByRole('status', { name: 'Загрузка данных' })).not.toBeInTheDocument(),
    );
    expect(await screen.findByRole('heading', { level: 1, name: 'Команды' })).toBeInTheDocument();
    await waitFor(() => {
      const links = within(screen.getByRole('navigation', { name: 'Основное меню' })).getAllByRole(
        'link',
      );
      expect(
        links
          .filter((link) => link.getAttribute('aria-current') === 'page')
          .map((l) => l.textContent),
      ).toEqual(['Команды']);
    });
  });

  it('a team and a project whose ids are written in capitals are found and shown by the selectors', async () => {
    serveOrganizations();
    renderRoutes(
      `/organizations/${ids.alfa.toUpperCase()}/projects/${ids.prm.toUpperCase()}/general?teamId=${ids.platformTeam.toUpperCase()}`,
    );

    await waitFor(() =>
      expect(selectors()).toEqual([
        'Организация: Альфа',
        'Департамент: все',
        'Команда: Платформа',
        'Проект: Пример проекта',
      ]),
    );
  });
});

describe('feat-043 QA, US-1/US-3/US-4/US-5: «Параметры» by role', () => {
  const SETTINGS = [
    ['a department', `${AREAS}/${ids.platformArea}/settings`],
    ['a team', `/organizations/${ids.alfa}/teams/${ids.platformTeam}/settings`],
    ['a project', `/organizations/${ids.alfa}/projects/${ids.prm}/settings`],
  ] as const;

  it.each(
    (['member', 'viewer'] as const).flatMap((role) =>
      SETTINGS.map(([name, path]) => [role, name, path] as const),
    ),
  )('a %s sees «Доступ ограничен» at the «Параметры» of %s', async (role, _name, path) => {
    serveOrganizations();
    server.use(
      http.get(`${API}/api/organizations/:organizationId/access`, () =>
        HttpResponse.json(servedFor(role)),
      ),
    );
    renderRoutes(path);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Доступ ограничен' }),
    ).toBeInTheDocument();
  });

  const PROJECT_GENERAL = `/organizations/${ids.alfa}/projects/${ids.prm}/general?teamId=${ids.platformTeam}`;

  async function settingsMenu() {
    return screen.findByRole('navigation', { name: 'Меню настроек' });
  }

  it('the settings navigation of an address that names the objects holds their groups, each with «Параметры»', async () => {
    serveOrganizations();
    renderRoutes(
      `/organizations/${ids.alfa}/projects/${ids.prm}/settings?teamId=${ids.platformTeam}`,
    );
    const menu = await settingsMenu();

    expect(await within(menu).findByText('Проект')).toBeInTheDocument();
    expect(within(menu).getByText('Команда')).toBeInTheDocument();
    expect(within(menu).getByText('Организация')).toBeInTheDocument();
    expect(within(menu).getAllByRole('link', { name: 'Параметры' })).toHaveLength(2);
  });

  // QA defect F-3 (qa.md), fixed: «Настройки» carries the selection in the address.
  it('choosing «Настройки» with a project and a team chosen keeps them and their groups', async () => {
    serveOrganizations();
    renderRoutes(PROJECT_GENERAL);
    await waitFor(() => expect(selectors()[3]).toBe('Проект: Пример проекта'));
    await userEvent.click(
      within(screen.getByRole('navigation', { name: 'Основное меню' })).getByRole('link', {
        name: 'Настройки',
      }),
    );
    const menu = await settingsMenu();

    expect(await within(menu).findByText('Проект', {}, { timeout: 1500 })).toBeInTheDocument();
    expect(selectors()).toEqual([
      'Организация: Альфа',
      'Департамент: все',
      'Команда: Платформа',
      'Проект: Пример проекта',
    ]);
  });
});
