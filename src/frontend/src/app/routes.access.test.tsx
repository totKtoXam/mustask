import { afterEach, describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { AuthContext } from '../features/auth/AuthContext';
import { organizationsKeys } from '../features/organizations/api/queryKeys';
import {
  exampleDataset,
  ids,
  API,
  PROBLEM_HEADERS,
  problem,
} from '../features/organizations/testFixtures';
import { serveOrganizations } from '../features/organizations/testApi';
import { serveMemberships } from '../features/organizations/testMembership';
import { recordAppearances } from '../test/appearances';
import { server } from '../test/mocks/server';
import { servedFor, withoutCodes } from '../test/organizationRoles';
import { authValue, LANGUAGES_READ, LocationProbe } from '../test/renderShell';
import { AppRoutes } from './routes';

// mod-016 design § 2.1, US-2, US-4: the real routes, the real shell and the
// real gate. Every organization page is decided by the served access of the
// organization in its address; the lists carry no organization and stand
// outside any gate.

const ORG = ids.alfa;
const PROJECT = ids.prm;
const TEAM = ids.platformTeam;
const CREATE = 'organizations.organization.create';
const SUBJECT = 'user-1';
const STORAGE_KEY = `mustask.context.${SUBJECT}`;

afterEach(() => {
  localStorage.clear();
});

function renderApp(path: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const result = render(
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={{ ...authValue([LANGUAGES_READ, CREATE]), subject: SUBJECT }}>
        <MemoryRouter initialEntries={[path]}>
          <AppRoutes />
          <LocationProbe />
        </MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
  return { ...result, queryClient };
}

function answerAccess(respond: () => Response) {
  server.use(http.get(`${API}/api/organizations/:organizationId/access`, () => respond()));
}

function notFound() {
  return HttpResponse.json(problem('organizations.organization.not_found'), {
    status: 404,
    headers: PROBLEM_HEADERS,
  });
}

const ORGANIZATION_PAGES: [string, string][] = [
  ['«Общие»', `/organizations/${ORG}/settings/general`],
  ['«Проекты»', `/organizations/${ORG}/settings/projects`],
  ['«Деактивированные» проекты', `/organizations/${ORG}/settings/projects/deactivated`],
  ['«Процесс»', `/organizations/${ORG}/settings/process`],
  ['«Общие» проекта', `/organizations/${ORG}/projects/${PROJECT}/settings/general`],
  ['«Команды»', `/organizations/${ORG}/projects/${PROJECT}/settings/teams`],
  ['страница команды', `/organizations/${ORG}/projects/${PROJECT}/settings/teams/${TEAM}`],
  ['«Области»', `/organizations/${ORG}/projects/${PROJECT}/settings/areas`],
  ['«Рабочие элементы»', `/organizations/${ORG}/projects/${PROJECT}/workitems`],
  ['рабочий элемент', `/organizations/${ORG}/projects/${PROJECT}/workitems/PRM-1`],
  ['«Канбан»', `/organizations/${ORG}/projects/${PROJECT}/teams/${TEAM}/boards/stories`],
  ['«Бэклоги»', `/organizations/${ORG}/projects/${PROJECT}/teams/${TEAM}/backlogs/stories`],
];

describe('mod-016 US-2 Scenario: a non-member meets «Организация не найдена» at every depth', () => {
  it.each(ORGANIZATION_PAGES)(
    '%s answers it, and none of the page’s own reads starts',
    async (_name, path) => {
      serveOrganizations();
      answerAccess(notFound);

      renderApp(path);

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
      ).toBeInTheDocument();
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      expect(screen.getByTestId('location')).toHaveTextContent(path);
    },
  );
});

describe('mod-016 US-4 Scenario: a page by address without its code', () => {
  it('«Деактивированные» проекты are «Доступ ограничен» for a member, who holds no project.deactivate', async () => {
    serveOrganizations();
    answerAccess(() => HttpResponse.json(servedFor('member')));

    renderApp(`/organizations/${ORG}/settings/projects/deactivated`);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Доступ ограничен' }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        'У вас нет прав для просмотра этого раздела. Обратитесь к администратору, чтобы запросить доступ.',
      ),
    ).toBeInTheDocument();
  });

  it('is the page itself for the administrator, who holds it', async () => {
    serveOrganizations();

    renderApp(`/organizations/${ORG}/settings/projects/deactivated`);

    // The page replaces the wrapper's header when it mounts, so the content
    // is awaited first.
    expect(await screen.findByText('Старый проект')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Проекты' })).toBeInTheDocument();
    expect(screen.queryByText('Доступ ограничен')).not.toBeInTheDocument();
  });

  it('is «Доступ ограничен» for a page whose read code the role lacks, whatever else it holds', async () => {
    serveOrganizations();
    answerAccess(() => HttpResponse.json(withoutCodes('viewer', 'process.definition.read')));

    renderApp(`/organizations/${ORG}/settings/process`);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Доступ ограничен' }),
    ).toBeInTheDocument();
  });
});

describe('mod-016 US-4 Scenario: the first rows of a page wait for the codes', () => {
  it('«Проекты» shows its header and a table skeleton, not «Доступ ограничен», while the codes are unread', async () => {
    serveOrganizations();
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(`${API}/api/organizations/:organizationId/access`, async () => {
        await held;
        return HttpResponse.json(servedFor('admin'));
      }),
    );

    renderApp(`/organizations/${ORG}/settings/projects`);

    expect(await screen.findByRole('heading', { level: 1, name: 'Проекты' })).toBeInTheDocument();
    expect(screen.getByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    expect(screen.queryByText('Доступ ограничен')).not.toBeInTheDocument();

    release();
    expect(await screen.findByText('Пример проекта')).toBeInTheDocument();
  });

  it('«Общие» shows its facts once the codes are read', async () => {
    serveOrganizations();

    renderApp(`/organizations/${ORG}/settings/general`);

    const main = await screen.findByRole('main');
    expect(await within(main).findByText('Название')).toBeInTheDocument();
    expect(await within(main).findByText('Альфа')).toBeInTheDocument();
  });
});

describe('mod-016 design § 2.6, § 12 Scenario: whose codes — the selection lags the address', () => {
  // The switcher remembers A (the user administers it); a link opens a project
  // page of B, where the user is a reader. B's projects cannot be read, so the
  // address never sets the selection and it stays on A for as long as the page
  // is open: the page of B must be decided by B's codes at every frame.
  it('shows no write control on a page of B while the selection stays on A, where the user is an administrator', async () => {
    const data = exampleDataset();
    data.actingUserId = SUBJECT;
    data.memberships = [
      { organizationId: ids.alfa, userId: SUBJECT, role: 'admin' },
      { organizationId: ids.beta, userId: SUBJECT, role: 'viewer' },
    ];
    serveOrganizations(data);
    serveMemberships(data);
    server.use(
      http.get(`${API}/api/organizations/${ids.beta}/projects`, () =>
        HttpResponse.json({ status: 500 }, { status: 500 }),
      ),
    );
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ organizationId: ids.alfa, projectId: ids.prm, teamId: ids.prmTeam }),
    );
    const add = recordAppearances('Добавить команду');

    const { queryClient } = renderApp(
      `/organizations/${ids.beta}/projects/${ids.rep}/settings/teams`,
    );

    // B's reader sees the teams; A's administrator codes reach the tab through the shell.
    expect(await screen.findByRole('table', { name: 'Команды' })).toBeInTheDocument();
    await waitFor(() =>
      expect(queryClient.getQueryData(organizationsKeys.access(ids.alfa))).toMatchObject({
        kind: 'member',
        role: 'admin',
      }),
    );
    expect(queryClient.getQueryData(organizationsKeys.access(ids.beta))).toMatchObject({
      kind: 'member',
      role: 'viewer',
    });
    await waitFor(() => expect(queryClient.isFetching()).toBe(0));
    expect(screen.getByRole('button', { name: /^Организация: Альфа/ })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Добавить команду' })).not.toBeInTheDocument();
    expect(add.count()).toBe(0);
    add.stop();
  });
});

describe('mod-016 design § 9 Scenario: a project restore answers 404 «Организация не найдена»', () => {
  it('turns the page into the frame at once and shows no Snackbar over it', async () => {
    const user = userEvent.setup();
    const data = exampleDataset();
    data.actingUserId = SUBJECT;
    data.memberships = [{ organizationId: ids.alfa, userId: SUBJECT, role: 'admin' }];
    serveOrganizations(data);
    serveMemberships(data);
    renderApp(`/organizations/${ids.alfa}/settings/projects/deactivated`);
    const restore = await screen.findByRole('button', { name: 'Восстановить «Старый проект»' });

    // The user was removed from the organization while the page was open.
    data.memberships = [];
    await user.click(restore);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    // The notice, were it raised, would be raised with the rejection; let the
    // access re-read and the list read that follow it settle, then look again.
    await new Promise((resolve) => setTimeout(resolve, 100));
    expect(screen.getByRole('heading', { level: 1, name: 'Организация не найдена' })).toBeVisible();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByText('Организация не найдена.')).not.toBeInTheDocument();
  });
});

describe('mod-016 US-2 Scenario: the organization lists stand outside any gate', () => {
  it('lists the user’s organizations although the access of the selected one answers 404', async () => {
    serveOrganizations();
    answerAccess(notFound);

    renderApp('/organizations');

    expect(await screen.findByRole('table', { name: 'Активные организации' })).toBeInTheDocument();
    expect(
      screen.queryByRole('heading', { name: 'Организация не найдена' }),
    ).not.toBeInTheDocument();
  });

  it('opens the deactivated tab without reading any access', async () => {
    serveOrganizations();

    renderApp('/organizations/deactivated');

    expect(
      await screen.findByRole('table', { name: 'Деактивированные организации' }),
    ).toBeInTheDocument();
  });
});
