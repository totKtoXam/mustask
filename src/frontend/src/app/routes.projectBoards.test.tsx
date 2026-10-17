import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { AuthContext } from '../features/auth/AuthContext';
import { serveBoards } from '../features/boards/testApi';
import { identifiers } from '../features/boards/testBacklog';
import { boardsDataset, item, type BoardsDataset } from '../features/boards/testFixtures';
import {
  ORGANIZATIONS_URL,
  exampleDataset as organizationsDataset,
  ids as organizationIds,
} from '../features/organizations/testFixtures';
import { serveOrganizations } from '../features/organizations/testApi';
import { server } from '../test/mocks/server';
import { authValue, LocationProbe } from '../test/renderShell';
import { AppRoutes } from './routes';

// feat-039 BR-PB-9 through the panel's own router and the real context source: the
// project's backlog and board registered in `app/routes.tsx`, the entries of the navigation
// model marked current on them, the switcher following the project in the address and
// moving the page on a project or organization choice, and leaving it on a team choice
// (mod-015 BR-18, OQ-6 A). «Альфа» has «Пример проекта» (`PRM`, with its first team «Пример
// проекта» and «Платформа») and «Аналитика» (`ANL`); «Бета» has «Отчётность» (`REP`).

const SUBJECT = 'user-1';
const ALFA = `/organizations/${organizationIds.alfa}`;
const PRM = `${ALFA}/projects/${organizationIds.prm}`;
const ANL = `${ALFA}/projects/${organizationIds.anl}`;
const REP = `/organizations/${organizationIds.beta}/projects/${organizationIds.rep}`;
const ANL_TEAMS_URL = `${ORGANIZATIONS_URL}/${organizationIds.alfa}/projects/${organizationIds.anl}/teams`;

function teamAddress(teamId: string, surface: string, level: string): string {
  return `${PRM}/teams/${teamId}/${surface}/${level}`;
}

/** The boards dataset on mod-002’s ids, one story in each project. */
function dataset(): BoardsDataset {
  const data = boardsDataset();
  data.organizations = [
    { id: organizationIds.alfa, name: 'Альфа', active: true },
    { id: organizationIds.beta, name: 'Бета', active: true },
  ];
  data.projects = [
    {
      id: organizationIds.prm,
      organizationId: organizationIds.alfa,
      name: 'Пример проекта',
      key: 'PRM',
      active: true,
    },
    {
      id: organizationIds.anl,
      organizationId: organizationIds.alfa,
      name: 'Аналитика',
      key: 'ANL',
      active: true,
    },
    {
      id: organizationIds.rep,
      organizationId: organizationIds.beta,
      name: 'Отчётность',
      key: 'REP',
      active: true,
    },
  ];
  data.areas = [
    {
      id: organizationIds.prmRoot,
      projectId: organizationIds.prm,
      parentId: null,
      name: 'Пример проекта',
    },
    {
      id: organizationIds.platformArea,
      projectId: organizationIds.prm,
      parentId: organizationIds.prmRoot,
      name: 'Платформа',
    },
    {
      id: organizationIds.anlRoot,
      projectId: organizationIds.anl,
      parentId: null,
      name: 'Аналитика',
    },
    {
      id: organizationIds.repRoot,
      projectId: organizationIds.rep,
      parentId: null,
      name: 'Отчётность',
    },
  ];
  data.teams = [
    {
      id: organizationIds.prmTeam,
      projectId: organizationIds.prm,
      name: 'Пример проекта',
      defaultAreaId: organizationIds.prmRoot,
      first: true,
      active: true,
    },
    {
      id: organizationIds.platformTeam,
      projectId: organizationIds.prm,
      name: 'Платформа',
      defaultAreaId: organizationIds.platformArea,
      first: false,
      active: true,
    },
    {
      id: organizationIds.anlTeam,
      projectId: organizationIds.anl,
      name: 'Аналитика',
      defaultAreaId: organizationIds.anlRoot,
      first: true,
      active: true,
    },
    {
      id: organizationIds.repTeam,
      projectId: organizationIds.rep,
      name: 'Отчётность',
      defaultAreaId: organizationIds.repRoot,
      first: true,
      active: true,
    },
  ];
  data.owners = { [organizationIds.platformArea]: organizationIds.platformTeam };
  data.items = [
    item(organizationIds.prm, 1, 'US', 'Вход по паролю', { areaId: organizationIds.platformArea }),
    item(organizationIds.prm, 2, 'US', 'Сводка проекта', { areaId: organizationIds.prmRoot }),
    item(organizationIds.anl, 1, 'US', 'Отчёт по спринту', { areaId: organizationIds.anlRoot }),
    item(organizationIds.rep, 1, 'US', 'Выгрузка отчёта', { areaId: organizationIds.repRoot }),
  ];
  return data;
}

function renderRoutes(path: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={{ ...authValue([]), subject: SUBJECT }}>
        <MemoryRouter initialEntries={[path]}>
          <AppRoutes />
          <LocationProbe />
        </MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
}

const mainNavigation = () => screen.getByRole('navigation', { name: 'Основное меню' });
const switcher = () => screen.getByRole('group', { name: 'Контекст работы' });

function switcherLevels(): string[] {
  return within(switcher())
    .getAllByRole('button')
    .map((button) => button.getAttribute('aria-label') ?? '');
}

async function backlogRows(): Promise<string[]> {
  return identifiers(await screen.findByRole('table', { name: 'Бэклог' }));
}

async function choose(level: 'Организация' | 'Проект' | 'Команда', name: string) {
  await userEvent.click(within(switcher()).getByRole('button', { name: new RegExp(`^${level}:`) }));
  await userEvent.click(await screen.findByRole('menuitemradio', { name }));
}

describe('feat-039 the project’s pages in the shell', () => {
  it.each([
    ['backlogs', 'Бэклоги', 'Канбан'],
    ['boards', 'Канбан', 'Бэклоги'],
  ])(
    'opens the project’s %s: its entry «%s» current, «%s» not, no trail, and the switcher naming the project and its default team — never «Весь проект»',
    async (surface, entry, other) => {
      serveOrganizations();
      serveBoards(dataset());
      renderRoutes(`${PRM}/${surface}/features`);

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Пример проекта' }),
      ).toBeInTheDocument();
      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
      await waitFor(() => expect(document.title).toBe(`${entry} — Mustask`));
      await waitFor(() =>
        expect(switcherLevels()).toEqual([
          'Организация: Альфа',
          'Проект: Пример проекта',
          'Команда: Пример проекта',
        ]),
      );
      const current = await within(mainNavigation()).findByRole('link', { name: entry });
      expect(current).toHaveAttribute('aria-current', 'page');
      expect(current).toHaveAttribute(
        'href',
        teamAddress(organizationIds.prmTeam, surface, 'stories'),
      );
      expect(within(mainNavigation()).getByRole('link', { name: other })).not.toHaveAttribute(
        'aria-current',
      );
      expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).toBeNull();
      expect(within(switcher()).queryByText('Весь проект')).toBeNull();
    },
  );

  it('lists the project’s backlog as the whole project’s: both stories, the team page’s only its team’s', async () => {
    serveOrganizations();
    serveBoards(dataset());
    renderRoutes(`${PRM}/backlogs/stories`);

    expect(await backlogRows()).toEqual(['PRM-US-1', 'PRM-US-2']);
  });

  it('after «Весь проект» from «Платформа»’s page the switcher still names «Платформа» and «Бэклоги» opens its «Истории»', async () => {
    serveOrganizations();
    serveBoards(dataset());
    renderRoutes(teamAddress(organizationIds.platformTeam, 'backlogs', 'epics'));
    await waitFor(() => expect(switcherLevels()[2]).toBe('Команда: Платформа'));
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' });

    await userEvent.click(screen.getByRole('button', { name: 'Сменить команду' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Весь проект' }));

    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent(`${PRM}/backlogs/epics`),
    );
    expect(await screen.findByRole('heading', { level: 1, name: 'Пример проекта' })).toBeVisible();
    expect(switcherLevels()).toEqual([
      'Организация: Альфа',
      'Проект: Пример проекта',
      'Команда: Платформа',
    ]);
    const entry = within(mainNavigation()).getByRole('link', { name: 'Бэклоги' });
    expect(entry).toHaveAttribute('aria-current', 'page');
    expect(entry).toHaveAttribute(
      'href',
      teamAddress(organizationIds.platformTeam, 'backlogs', 'stories'),
    );
  });
});

describe('feat-039 BR-PB-9 (c): a choice in the switcher on a project’s page', () => {
  it.each([
    ['backlogs', 'features'],
    ['boards', 'epics'],
  ])(
    'on the project’s %s at «%s», choosing «Аналитика» at the project level opens the same page of that project at the same level',
    async (surface, level) => {
      serveOrganizations();
      serveBoards(dataset());
      renderRoutes(`${PRM}/${surface}/${level}`);
      await waitFor(() => expect(switcherLevels()[1]).toBe('Проект: Пример проекта'));

      await choose('Проект', 'Аналитика');

      await waitFor(() =>
        expect(screen.getByTestId('location')).toHaveTextContent(`${ANL}/${surface}/${level}`),
      );
      await waitFor(() =>
        expect(switcherLevels()).toEqual([
          'Организация: Альфа',
          'Проект: Аналитика',
          'Команда: Аналитика',
        ]),
      );
      const entry = await within(mainNavigation()).findByRole('link', {
        name: surface === 'backlogs' ? 'Бэклоги' : 'Канбан',
      });
      expect(entry).toHaveAttribute('aria-current', 'page');
      expect(screen.getByRole('button', { name: /^Уровень: / })).toHaveAccessibleName(
        level === 'features' ? 'Уровень: Фичи' : 'Уровень: Эпики',
      );
    },
  );

  it('choosing «Аналитика» on the backlog lists that project’s stories', async () => {
    serveOrganizations();
    serveBoards(dataset());
    renderRoutes(`${PRM}/backlogs/stories`);
    await backlogRows();

    await choose('Проект', 'Аналитика');

    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent(`${ANL}/backlogs/stories`),
    );
    await waitFor(async () => expect(await backlogRows()).toEqual(['ANL-US-1']));
  });

  it('choosing «Бета» at the organization level opens the same page of its first project at the same level', async () => {
    serveOrganizations();
    serveBoards(dataset());
    renderRoutes(`${PRM}/backlogs/features`);
    await waitFor(() => expect(switcherLevels()[0]).toBe('Организация: Альфа'));

    await choose('Организация', 'Бета');

    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent(`${REP}/backlogs/features`),
    );
    await waitFor(() =>
      expect(switcherLevels()).toEqual([
        'Организация: Бета',
        'Проект: Отчётность',
        'Команда: Отчётность',
      ]),
    );
    expect(await within(mainNavigation()).findByRole('link', { name: 'Бэклоги' })).toHaveAttribute(
      'aria-current',
      'page',
    );
  });

  it('an organization with no project opens «Главная»', async () => {
    const organizations = organizationsDataset();
    organizations.projects = organizations.projects.map((project) =>
      project.id === organizationIds.rep ? { ...project, active: false } : project,
    );
    serveOrganizations(organizations);
    serveBoards(dataset());
    renderRoutes(`${PRM}/backlogs/features`);
    await waitFor(() => expect(switcherLevels()[0]).toBe('Организация: Альфа'));

    await choose('Организация', 'Бета');

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/home'));
  });

  it('choosing «Платформа» at the team level moves nothing: the page stays, the switcher and both entries take the team', async () => {
    serveOrganizations();
    serveBoards(dataset());
    renderRoutes(`${PRM}/backlogs/features`);
    await waitFor(() => expect(switcherLevels()[2]).toBe('Команда: Пример проекта'));
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' });

    await choose('Команда', 'Платформа');

    await waitFor(() => expect(switcherLevels()[2]).toBe('Команда: Платформа'));
    expect(screen.getByTestId('location')).toHaveTextContent(`${PRM}/backlogs/features`);
    const backlogs = within(mainNavigation()).getByRole('link', { name: 'Бэклоги' });
    expect(backlogs).toHaveAttribute('aria-current', 'page');
    expect(backlogs).toHaveAttribute(
      'href',
      teamAddress(organizationIds.platformTeam, 'backlogs', 'stories'),
    );
    expect(within(mainNavigation()).getByRole('link', { name: 'Канбан' })).toHaveAttribute(
      'href',
      teamAddress(organizationIds.platformTeam, 'boards', 'stories'),
    );
    expect(screen.getByRole('heading', { level: 1, name: 'Пример проекта' })).toBeVisible();
  });
});

describe('feat-039 BR-PB-9: with no team at the switcher there is no current entry, and the page works', () => {
  it('a project whose teams list comes back empty: the backlog lists its stories, «Бэклоги» and «Канбан» are not shown', async () => {
    serveOrganizations();
    serveBoards(dataset());
    server.use(http.get(ANL_TEAMS_URL, () => HttpResponse.json([])));
    renderRoutes(`${ANL}/backlogs/stories`);

    expect(await backlogRows()).toEqual(['ANL-US-1']);
    await waitFor(() => expect(switcherLevels()[1]).toBe('Проект: Аналитика'));
    expect(within(mainNavigation()).queryByRole('link', { name: 'Бэклоги' })).toBeNull();
    expect(within(mainNavigation()).queryByRole('link', { name: 'Канбан' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Сменить команду' })).toBeNull();
  });
});

describe('feat-039 BR-PB-1: the project’s addresses that are not pages', () => {
  it.each([['backlogs'], ['boards']])(
    'an unknown level of the project’s %s answers «Страница не найдена», no entry current, nothing read',
    async (surface) => {
      serveOrganizations();
      const api = serveBoards(dataset());
      renderRoutes(`${PRM}/${surface}/tasks`);

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
      ).toBeInTheDocument();
      await within(mainNavigation()).findByRole('link', { name: 'Бэклоги' });
      for (const name of ['Канбан', 'Бэклоги']) {
        expect(within(mainNavigation()).getByRole('link', { name })).not.toHaveAttribute(
          'aria-current',
        );
      }
      expect(api.reads(`/${surface}/`)).toEqual([]);
    },
  );

  it('the project’s address without a level is not a page of the entry either', async () => {
    serveOrganizations();
    serveBoards(dataset());
    renderRoutes(`${PRM}/backlogs`);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
    ).toBeInTheDocument();
  });
});
