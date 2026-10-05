import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { AuthContext, type AuthContextValue } from '../features/auth/AuthContext';
import { ids } from '../features/organizations/testFixtures';
import { serveOrganizations } from '../features/organizations/testApi';
import { PROCESS_DEFINITION_URL, sampleProcessDefinition } from '../features/process/testFixtures';
import { server } from '../test/mocks/server';
import { authValue, LANGUAGES_READ, LocationProbe } from '../test/renderShell';
import { AppRoutes } from './routes';

const SUBJECT = 'user-1';
const STORAGE_KEY = `mustask.context.${SUBJECT}`;

const organizationRequests: string[] = [];
const onRequest = ({ request }: { request: Request }) => {
  if (request.url.includes('/api/organizations')) {
    organizationRequests.push(new URL(request.url).pathname);
  }
};

beforeEach(() => {
  organizationRequests.length = 0;
  server.events.on('request:start', onRequest);
});

afterEach(() => {
  server.events.removeListener('request:start', onRequest);
});

function renderRoutes(path: string, auth: AuthContextValue = authValue([LANGUAGES_READ])) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={{ ...auth, subject: SUBJECT }}>
        <MemoryRouter initialEntries={[path]}>
          <AppRoutes />
          <LocationProbe />
        </MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
}

function switcherLevels(): string[] {
  return within(screen.getByRole('group', { name: 'Контекст работы' }))
    .getAllByRole('button')
    .map((button) => button.getAttribute('aria-label') ?? '');
}

describe('mod-002 design § 1: the slice’s nine routes in the shell', () => {
  it.each([
    ['/organizations', 'Организации'],
    ['/organizations/deactivated', 'Организации'],
    [`/organizations/${ids.alfa}/settings/general`, 'Общие'],
    [`/organizations/${ids.alfa}/settings/projects`, 'Проекты'],
    [`/organizations/${ids.alfa}/settings/projects/deactivated`, 'Проекты'],
    [`/organizations/${ids.alfa}/projects/${ids.prm}/settings/general`, 'Общие'],
    [`/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams`, 'Команды'],
    [
      `/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams/${ids.platformTeam}`,
      'Команда: Платформа',
    ],
    [`/organizations/${ids.alfa}/projects/${ids.prm}/settings/areas`, 'Области'],
  ])('%s opens «%s» as the one h1, its tab titled after it', async (path, title) => {
    serveOrganizations();
    renderRoutes(path);

    expect(await screen.findByRole('heading', { level: 1, name: title })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    await waitFor(() => expect(document.title).toBe(`${title} — Mustask`));
  });

  it.each([[`/organizations/${ids.alfa}`], [`/organizations/${ids.alfa}/projects/${ids.prm}`]])(
    '%s alone is not a page: «Страница не найдена»',
    async (path) => {
      serveOrganizations();
      renderRoutes(path);

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
      ).toBeInTheDocument();
    },
  );
});

describe('mod-002 US-1 Scenario: An anonymous request is refused', () => {
  it('an anonymous visitor gets the sign-in screen and sends no request to /api/organizations', async () => {
    serveOrganizations();
    renderRoutes(
      '/organizations',
      authValue([], { status: 'anonymous', reason: 'signed_out', displayName: '' }),
    );

    expect(await screen.findByRole('button', { name: 'Войти' })).toBeInTheDocument();
    expect(organizationRequests).toEqual([]);
  });
});

describe('mod-002 US-3 Scenario: The «Процесс» link', () => {
  it('opens mod-001’s screen with «Меню настроек», «Процесс» current and the trail «Настройки / Организация»', async () => {
    serveOrganizations();
    server.use(http.get(PROCESS_DEFINITION_URL, () => HttpResponse.json(sampleProcessDefinition)));
    renderRoutes(`/organizations/${ids.alfa}/projects/${ids.prm}/settings/general`);

    await userEvent.click(await screen.findByRole('link', { name: 'Процесс' }));

    expect(await screen.findByRole('region', { name: 'Эпик' })).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent(
      `/organizations/${ids.alfa}/settings/process`,
    );
    const navigation = await screen.findByRole('navigation', { name: 'Меню настроек' });
    expect(within(navigation).getByRole('link', { name: 'Процесс' })).toHaveAttribute(
      'aria-current',
      'page',
    );
    expect(screen.getByRole('navigation', { name: 'Путь к странице' })).toHaveTextContent(
      'Настройки/Организация',
    );
  });
});

describe('mod-002 US-8 Scenario: The address sets the switcher', () => {
  it('«Бета» chosen; the settings of «Пример проекта» in «Альфа» opened: «Альфа», «Пример проекта», its first team, remembered', async () => {
    serveOrganizations();
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ organizationId: ids.beta, projectId: ids.rep, teamId: ids.repTeam }),
    );
    renderRoutes(`/organizations/${ids.alfa}/projects/${ids.prm}/settings/general`);

    await waitFor(() =>
      expect(switcherLevels()).toEqual([
        'Организация: Альфа',
        'Проект: Пример проекта',
        'Команда: Пример проекта',
      ]),
    );
    await waitFor(() =>
      expect(JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '{}')).toEqual({
        organizationId: ids.alfa,
        projectId: ids.prm,
        teamId: ids.prmTeam,
      }),
    );
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('a deactivated organization’s address answers «Организация не найдена»; the switcher and the storage keep «Бета»', async () => {
    serveOrganizations();
    const remembered = { organizationId: ids.beta, projectId: ids.rep, teamId: ids.repTeam };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(remembered));
    renderRoutes(`/organizations/${ids.gamma}/settings/general`);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(switcherLevels()).toEqual([
        'Организация: Бета',
        'Проект: Отчётность',
        'Команда: Отчётность',
      ]),
    );
    expect(JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '{}')).toEqual(remembered);
  });

  it('mod-001’s process address sets its organization', async () => {
    serveOrganizations();
    server.use(http.get(PROCESS_DEFINITION_URL, () => HttpResponse.json(sampleProcessDefinition)));
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ organizationId: ids.beta }));
    renderRoutes(`/organizations/${ids.alfa}/settings/process`);

    await waitFor(() => expect(switcherLevels()[0]).toBe('Организация: Альфа'));
  });
});

describe('mod-002 US-8 Scenario: A remembered selection that is gone falls back (on any page)', () => {
  it('on «Главная», a remembered deleted team falls back with the notice', async () => {
    const api = serveOrganizations();
    api.data.teams.find((team) => team.id === ids.platformTeam)!.deleted = true;
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ organizationId: ids.alfa, projectId: ids.prm, teamId: ids.platformTeam }),
    );
    renderRoutes('/home');

    expect(
      await screen.findByText(
        'Ранее выбранный контекст недоступен. Выбрано: Альфа / Пример проекта / Пример проекта.',
      ),
    ).toBeInTheDocument();
  });
});
