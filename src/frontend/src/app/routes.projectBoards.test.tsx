import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AuthContext } from '../features/auth/AuthContext';
import { serveBoards } from '../features/boards/testApi';
import { identifiers } from '../features/boards/testBacklog';
import { boardsDataset, item, type BoardsDataset } from '../features/boards/testFixtures';
import {
  exampleDataset as organizationsDataset,
  ids as organizationIds,
} from '../features/organizations/testFixtures';
import { serveOrganizations } from '../features/organizations/testApi';
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
      active: true,
    },
    {
      id: organizationIds.platformTeam,
      projectId: organizationIds.prm,
      name: 'Платформа',
      active: true,
    },
    {
      id: organizationIds.anlTeam,
      projectId: organizationIds.anl,
      name: 'Аналитика',
      active: true,
    },
    {
      id: organizationIds.repTeam,
      projectId: organizationIds.rep,
      name: 'Отчётность',
      active: true,
    },
  ];
  data.items = [
    item(organizationIds.prm, 1, 'US', 'Вход по паролю', {
      areaId: organizationIds.platformArea,
      teamId: organizationIds.platformTeam,
    }),
    item(organizationIds.prm, 2, 'US', 'Сводка проекта', {
      areaId: organizationIds.prmRoot,
      teamId: organizationIds.prmTeam,
    }),
    item(organizationIds.anl, 1, 'US', 'Отчёт по спринту', {
      areaId: organizationIds.anlRoot,
      teamId: organizationIds.anlTeam,
    }),
    item(organizationIds.rep, 1, 'US', 'Выгрузка отчёта', {
      areaId: organizationIds.repRoot,
      teamId: organizationIds.repTeam,
    }),
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
    .getAllByRole('combobox')
    .map((button) => button.getAttribute('aria-label') ?? '');
}

async function backlogRows(): Promise<string[]> {
  return identifiers(await screen.findByRole('table', { name: 'Бэклог' }));
}

async function choose(level: 'Организация' | 'Проект' | 'Команда', name: string | RegExp) {
  await userEvent.click(
    within(switcher()).getByRole('combobox', { name: new RegExp(`^${level}:`) }),
  );
  await userEvent.click(await screen.findByRole('option', { name }));
}

const noLoading = () =>
  waitFor(() => expect(screen.queryByRole('status', { name: 'Загрузка данных' })).toBeNull());

describe('feat-039 the project’s pages in the shell', () => {
  it.each([
    ['backlogs', 'Бэклог', 'Канбан'],
    ['boards', 'Канбан', 'Бэклог'],
  ])(
    'opens the project’s %s: its entry «%s» current, «%s» not, no trail, and the selectors naming the project, the team «все»',
    async (surface, entry, other) => {
      serveOrganizations();
      serveBoards(dataset(), { projectList: false });
      renderRoutes(`${PRM}/${surface}/features`);

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Пример проекта' }),
      ).toBeInTheDocument();
      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
      await waitFor(() =>
        expect(document.title).toBe(`${surface === 'backlogs' ? 'Бэклоги' : 'Канбан'} — Mustask`),
      );
      // ADR-0032 § 1: the project prefix chooses the project; the team stays «все» until chosen.
      await waitFor(() =>
        expect(switcherLevels()).toEqual([
          'Организация: Альфа',
          'Департамент: все',
          'Команда: все',
          'Проект: Пример проекта',
        ]),
      );
      const current = await within(mainNavigation()).findByRole('link', { name: entry });
      expect(current).toHaveAttribute('aria-current', 'page');
      expect(current).toHaveAttribute('href', `${PRM}/${surface}/stories`);
      expect(within(mainNavigation()).getByRole('link', { name: other })).not.toHaveAttribute(
        'aria-current',
      );
      expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).toBeNull();
    },
  );

  it('lists the project’s backlog as the whole project’s: both stories, the team page’s only its team’s', async () => {
    serveOrganizations();
    serveBoards(dataset(), { projectList: false });
    renderRoutes(`${PRM}/backlogs/stories`);

    expect(await backlogRows()).toEqual(['PRM-US-1', 'PRM-US-2']);
  });

  // feat-043 ADR-0032 § 1: a chosen team rides in the query; the project menu's landing hrefs carry it.
  it('a chosen team rides in `?teamId=` and the project menu’s «Бэклог» and «Канбан» carry it', async () => {
    serveOrganizations();
    serveBoards(dataset(), { projectList: false });
    renderRoutes(`${PRM}/backlogs/epics?teamId=${organizationIds.platformTeam}`);

    expect(await screen.findByRole('heading', { level: 1, name: 'Пример проекта' })).toBeVisible();
    await waitFor(() =>
      expect(switcherLevels()).toEqual([
        'Организация: Альфа',
        'Департамент: все',
        'Команда: Платформа',
        'Проект: Пример проекта',
      ]),
    );
    const entry = within(mainNavigation()).getByRole('link', { name: 'Бэклог' });
    expect(entry).toHaveAttribute('aria-current', 'page');
    expect(entry).toHaveAttribute(
      'href',
      `${PRM}/backlogs/stories?teamId=${organizationIds.platformTeam}`,
    );
    expect(within(mainNavigation()).getByRole('link', { name: 'Канбан' })).toHaveAttribute(
      'href',
      `${PRM}/boards/stories?teamId=${organizationIds.platformTeam}`,
    );
  });
});

describe('feat-039 BR-PB-9 (c): a choice in the selectors on a project’s page', () => {
  it.each([
    ['backlogs', 'features'],
    ['boards', 'epics'],
  ])(
    'on the project’s %s at «%s», choosing «Аналитика» at the project selector opens the same page of that project at the same level',
    async (surface, level) => {
      serveOrganizations();
      serveBoards(dataset(), { projectList: false });
      renderRoutes(`${PRM}/${surface}/${level}`);
      await waitFor(() => expect(switcherLevels()[3]).toBe('Проект: Пример проекта'));

      await choose('Проект', /^Аналитика/);

      await waitFor(() =>
        expect(screen.getByTestId('location')).toHaveTextContent(`${ANL}/${surface}/${level}`),
      );
      await waitFor(() => expect(switcherLevels()[3]).toBe('Проект: Аналитика'));
      const entry = await within(mainNavigation()).findByRole('link', {
        name: surface === 'backlogs' ? 'Бэклог' : 'Канбан',
      });
      expect(entry).toHaveAttribute('aria-current', 'page');
      expect(await screen.findByRole('button', { name: /^Уровень: / })).toHaveAccessibleName(
        level === 'features' ? 'Уровень: Фичи' : 'Уровень: Эпики',
      );
    },
  );

  it('choosing «Аналитика» on the backlog lists that project’s stories', async () => {
    serveOrganizations();
    serveBoards(dataset(), { projectList: false });
    renderRoutes(`${PRM}/backlogs/stories`);
    await backlogRows();

    await choose('Проект', /^Аналитика/);

    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent(`${ANL}/backlogs/stories`),
    );
    await waitFor(async () => expect(await backlogRows()).toEqual(['ANL-US-1']));
  });

  // The organization choice opens that organization's «Обзор» (ADR-0032 § 5): the page and level are not kept.
  it('choosing «Бета» at the organization selector opens its «Обзор», every other selector «все»', async () => {
    serveOrganizations();
    serveBoards(dataset(), { projectList: false });
    renderRoutes(`${PRM}/backlogs/features`);
    await waitFor(() => expect(switcherLevels()[0]).toBe('Организация: Альфа'));

    await choose('Организация', 'Бета');

    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent(
        `/organizations/${organizationIds.beta}/overview`,
      ),
    );
    await waitFor(() =>
      expect(switcherLevels()).toEqual([
        'Организация: Бета',
        'Департамент: все',
        'Команда: все',
        'Проект: все',
      ]),
    );
  });

  it('choosing a team at the team selector keeps the project’s page and puts the team in the query', async () => {
    serveOrganizations();
    serveBoards(dataset(), { projectList: false });
    renderRoutes(`${PRM}/backlogs/features`);
    await waitFor(() => expect(switcherLevels()[3]).toBe('Проект: Пример проекта'));
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' }).catch(() => undefined);

    await choose('Команда', 'Платформа');

    await waitFor(() => expect(switcherLevels()[2]).toBe('Команда: Платформа'));
    expect(screen.getByTestId('location')).toHaveTextContent(`${PRM}/backlogs/features`);
    expect(screen.getByTestId('location').getAttribute('data-search')).toBe(
      `?teamId=${organizationIds.platformTeam}`,
    );
    expect(within(mainNavigation()).getByRole('link', { name: 'Бэклог' })).toHaveAttribute(
      'aria-current',
      'page',
    );
  });
});

describe('feat-039 BR-PB-9: the project’s entries need no team', () => {
  it('an organization with no team: the backlog lists its stories and the project menu still shows both entries', async () => {
    const organizations = organizationsDataset();
    organizations.teams = [];
    organizations.links = [];
    serveOrganizations(organizations);
    serveBoards(dataset(), { projectList: false });
    renderRoutes(`${ANL}/backlogs/stories`);

    expect(await backlogRows()).toEqual(['ANL-US-1']);
    await waitFor(() => expect(switcherLevels()[3]).toBe('Проект: Аналитика'));
    expect(await within(mainNavigation()).findByRole('link', { name: 'Бэклог' })).toBeVisible();
    expect(within(mainNavigation()).getByRole('link', { name: 'Канбан' })).toBeVisible();
  });
});

describe('feat-039 BR-PB-1: the project’s addresses that are not pages', () => {
  it.each([['backlogs'], ['boards']])(
    'an unknown level of the project’s %s answers «Страница не найдена», no entry current, nothing read',
    async (surface) => {
      serveOrganizations();
      const api = serveBoards(dataset(), { projectList: false });
      renderRoutes(`${PRM}/${surface}/tasks`);

      await noLoading();
      expect(
        await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
      ).toBeInTheDocument();
      await within(mainNavigation()).findByRole('link', { name: 'Бэклог' });
      for (const name of ['Канбан', 'Бэклог']) {
        expect(within(mainNavigation()).getByRole('link', { name })).not.toHaveAttribute(
          'aria-current',
        );
      }
      expect(api.reads(`/${surface}/`)).toEqual([]);
    },
  );

  it('the project’s address without a level is not a page of the entry either', async () => {
    serveOrganizations();
    serveBoards(dataset(), { projectList: false });
    renderRoutes(`${PRM}/backlogs`);

    await noLoading();
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
    ).toBeInTheDocument();
  });
});
