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
const storageKey = (organizationId: string) => `mustask.context.v2.${SUBJECT}.${organizationId}`;

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

function selectorLevels(): string[] {
  return within(screen.getByRole('group', { name: 'Контекст работы' }))
    .getAllByRole('combobox')
    .map((button) => button.getAttribute('aria-label') ?? '');
}

describe('mod-002 design § 1 and feat-028 design § 1: the slice’s twelve routes in the shell', () => {
  it.each([
    ['/organizations', 'Организации'],
    ['/organizations/deactivated', 'Организации'],
    [`/organizations/${ids.alfa}/settings/general`, 'Общие'],
    [`/organizations/${ids.alfa}/projects`, 'Проекты'],
    [`/organizations/${ids.alfa}/projects?status=deactivated`, 'Проекты'],
    [`/organizations/${ids.alfa}/projects/${ids.prm}/general`, 'Общие'],
    [`/organizations/${ids.alfa}/projects/${ids.prm}/teams`, 'Команды'],
    [`/organizations/${ids.alfa}/teams`, 'Команды'],
    [`/organizations/${ids.alfa}/teams/${ids.platformTeam}/overview`, 'Команда: Платформа'],
    [`/organizations/${ids.alfa}/areas`, 'Области'],
    [`/organizations/${ids.alfa}/areas/${ids.platformArea}/overview`, 'Департамент: Платформа'],
  ])('%s opens «%s» as the one h1, its tab titled after it', async (path, title) => {
    serveOrganizations();
    renderRoutes(path);

    // An organization's page draws its header over a skeleton while the codes
    // are read (mod-016 design § 2.1, row 1) and the page's own header replaces
    // it; the page is settled when nothing loads any more.
    await waitFor(() =>
      expect(screen.queryByRole('status', { name: 'Загрузка данных' })).not.toBeInTheDocument(),
    );
    expect(screen.getByRole('heading', { level: 1, name: title })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    await waitFor(() => expect(document.title).toBe(`${title} — Mustask`));
  });

  // feat-043 D-4: the bare address of an object opens the first entry of its menu, replacing itself.
  it.each([
    [`/organizations/${ids.alfa}`, `/organizations/${ids.alfa}/overview`],
    [
      `/organizations/${ids.alfa}/projects/${ids.prm}`,
      `/organizations/${ids.alfa}/projects/${ids.prm}/general`,
    ],
    [
      `/organizations/${ids.alfa}/teams/${ids.platformTeam}`,
      `/organizations/${ids.alfa}/teams/${ids.platformTeam}/overview`,
    ],
  ])('%s alone opens the first entry of its menu', async (path, target) => {
    serveOrganizations();
    renderRoutes(path);

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(target));
  });
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
    renderRoutes(`/organizations/${ids.alfa}/projects/${ids.prm}/general`);

    // The page's own link: «Процесс» is in the settings navigation too, once
    // the organization's codes are read.
    await userEvent.click(
      await within(await screen.findByRole('main')).findByRole('link', { name: 'Процесс' }),
    );

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

describe('mod-002 US-8 Scenario: The address sets the selectors', () => {
  // feat-043 ADR-0032 § 1: the address is the selection; a page of an organization writes it.
  it('the settings of «Пример проекта» in «Альфа» opened: «Альфа», «все», «все», «Пример проекта», remembered', async () => {
    serveOrganizations();
    localStorage.setItem(
      storageKey(ids.beta),
      JSON.stringify({ department: null, team: ids.repTeam, project: ids.rep }),
    );
    renderRoutes(`/organizations/${ids.alfa}/projects/${ids.prm}/general`);

    await waitFor(() =>
      expect(selectorLevels()).toEqual([
        'Организация: Альфа',
        'Департамент: все',
        'Команда: все',
        'Проект: Пример проекта',
      ]),
    );
    await waitFor(() =>
      expect(JSON.parse(localStorage.getItem(storageKey(ids.alfa)) ?? '{}')).toEqual({
        department: null,
        team: null,
        project: ids.prm,
      }),
    );
    // Another organization's memory is its own and stays.
    expect(JSON.parse(localStorage.getItem(storageKey(ids.beta)) ?? '{}')).toEqual({
      department: null,
      team: ids.repTeam,
      project: ids.rep,
    });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('a deactivated organization’s address answers «Организация не найдена»; the selectors and the storage keep «Бета»', async () => {
    serveOrganizations();
    const remembered = { department: null, team: ids.repTeam, project: ids.rep };
    localStorage.setItem(storageKey(ids.beta), JSON.stringify(remembered));
    localStorage.setItem(`mustask.context.v2.${SUBJECT}.organization`, ids.beta);
    renderRoutes(`/organizations/${ids.gamma}/settings/general`);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
    expect(JSON.parse(localStorage.getItem(storageKey(ids.beta)) ?? '{}')).toEqual(remembered);
  });

  it('mod-001’s process address sets its organization', async () => {
    serveOrganizations();
    server.use(http.get(PROCESS_DEFINITION_URL, () => HttpResponse.json(sampleProcessDefinition)));
    renderRoutes(`/organizations/${ids.alfa}/settings/process`);

    await waitFor(() => expect(selectorLevels()[0]).toBe('Организация: Альфа'));
  });
});

describe('mod-002 US-8 Scenario: A remembered selection that is gone falls back (on any page)', () => {
  it('on «Главная», a remembered deleted team falls back to «все» with the notice', async () => {
    const api = serveOrganizations();
    api.data.teams.find((team) => team.id === ids.platformTeam)!.deleted = true;
    localStorage.setItem(
      storageKey(ids.alfa),
      JSON.stringify({ department: null, team: ids.platformTeam, project: ids.prm }),
    );
    localStorage.setItem(`mustask.context.v2.${SUBJECT}.organization`, ids.alfa);
    renderRoutes('/home');

    expect(
      await screen.findByText('Ранее выбранный контекст недоступен. Выбрано: Альфа.'),
    ).toBeInTheDocument();
  });
});
