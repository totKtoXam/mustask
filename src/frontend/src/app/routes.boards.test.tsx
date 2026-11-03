import { describe, expect, it } from 'vitest';
import { useEffect } from 'react';
import { MemoryRouter, useLocation } from 'react-router-dom';
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

// mod-006 through the panel's own router and the real context source: the
// team-level routes registered in `app/routes.tsx`, the entries of the
// navigation model, the selectors following a team-level address and moving
// the page on a team choice (spec § Screens and copy; mod-015 BR-18). The
// boards API serves mod-002's «Альфа» / «Пример проекта» (`PRM`) with its
// first team «Пример проекта» and «Платформа».

const SUBJECT = 'user-1';
const PRM = `/organizations/${organizationIds.alfa}/projects/${organizationIds.prm}`;

function teamAddress(teamId: string, surface: string, level: string): string {
  return `/organizations/${organizationIds.alfa}/teams/${teamId}/${surface}/${level}`;
}

/** The boards dataset on mod-002's ids: «Платформа» owns «Платформа», the first team the root. */
function alfaDataset(): BoardsDataset {
  const data = boardsDataset();
  data.organizations = [{ id: organizationIds.alfa, name: 'Альфа', active: true }];
  data.projects = [
    {
      id: organizationIds.prm,
      organizationId: organizationIds.alfa,
      name: 'Пример проекта',
      key: 'PRM',
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
  ];
  return data;
}

/**
 * F1's dataset: «Альфа» also has «Аналитика» (`ANL`, its first team
 * «Аналитика»), and «Бета» has «Отчётность» (`REP`, its first team
 * «Отчётность»), as mod-002's example does; one story in each.
 */
function twoContextsDataset(): BoardsDataset {
  const data = alfaDataset();
  data.organizations.push({ id: organizationIds.beta, name: 'Бета', active: true });
  data.projects.push(
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
  );
  data.areas.push(
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
  );
  data.teams.push(
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
  );
  data.items.push(
    item(organizationIds.anl, 1, 'US', 'Отчёт по спринту', {
      areaId: organizationIds.anlRoot,
      teamId: organizationIds.anlTeam,
    }),
    item(organizationIds.rep, 1, 'US', 'Выгрузка отчёта', {
      areaId: organizationIds.repRoot,
      teamId: organizationIds.repTeam,
    }),
  );
  return data;
}

/** Every address the router showed, in order: what a move passed through. */
function PathHistory({ into }: { into: string[] }) {
  const { pathname } = useLocation();
  useEffect(() => {
    into.push(pathname);
  }, [into, pathname]);
  return null;
}

function renderRoutes(path: string, visited?: string[]) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={{ ...authValue([]), subject: SUBJECT }}>
        <MemoryRouter initialEntries={[path]}>
          <AppRoutes />
          <LocationProbe />
          {visited && <PathHistory into={visited} />}
        </MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
}

const mainNavigation = () => screen.getByRole('navigation', { name: 'Основное меню' });
const selectorGroup = () => screen.getByRole('group', { name: 'Контекст работы' });

function selectorLevels(): string[] {
  return within(selectorGroup())
    .getAllByRole('combobox')
    .map((button) => button.getAttribute('aria-label') ?? '');
}

async function backlogRows(): Promise<string[]> {
  return identifiers(await screen.findByRole('table', { name: 'Бэклог' }));
}

const ALFA = `/organizations/${organizationIds.alfa}`;

async function chooseIn(selector: RegExp, name: string | RegExp) {
  await userEvent.click(within(selectorGroup()).getByRole('combobox', { name: selector }));
  await userEvent.click(await screen.findByRole('option', { name }));
}

describe('mod-006 the backlog route in the shell', () => {
  it('opens «Бэклог» as the one h1 — the team’s name — and tab, its entry current, no trail, and the selectors follow the team in the address', async () => {
    serveOrganizations();
    serveBoards(alfaDataset(), { projectList: false });
    renderRoutes(teamAddress(organizationIds.platformTeam, 'backlogs', 'features'));

    expect(await screen.findByRole('heading', { level: 1, name: 'Платформа' })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    await waitFor(() => expect(document.title).toBe('Бэклоги — Mustask'));
    // feat-043 ADR-0032 § 1: a team address chooses the team; the others stay «все».
    await waitFor(() =>
      expect(selectorLevels()).toEqual([
        'Организация: Альфа',
        'Департамент: все',
        'Команда: Платформа',
        'Проект: все',
      ]),
    );
    const entry = await within(mainNavigation()).findByRole('link', { name: 'Бэклог' });
    expect(entry).toHaveAttribute(
      'href',
      teamAddress(organizationIds.platformTeam, 'backlogs', 'stories'),
    );
    expect(entry).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).toBeNull();
  });

  it('opens «Канбан» (mod-006’s «Доски», feat-001 BR-H-16) as the one h1 — the team’s name — and tab, its entry current, «Бэклог» not, no trail', async () => {
    serveOrganizations();
    serveBoards(alfaDataset(), { projectList: false });
    renderRoutes(teamAddress(organizationIds.platformTeam, 'boards', 'epics'));

    expect(await screen.findByRole('heading', { level: 1, name: 'Платформа' })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    await waitFor(() => expect(document.title).toBe('Канбан — Mustask'));
    await waitFor(() => expect(selectorLevels()[2]).toBe('Команда: Платформа'));
    const boards = await within(mainNavigation()).findByRole('link', { name: 'Канбан' });
    expect(boards).toHaveAttribute(
      'href',
      teamAddress(organizationIds.platformTeam, 'boards', 'stories'),
    );
    expect(boards).toHaveAttribute('aria-current', 'page');
    expect(within(mainNavigation()).getByRole('link', { name: 'Бэклог' })).not.toHaveAttribute(
      'aria-current',
    );
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).toBeNull();
  });

  it('lists «Канбан» and «Бэклог» after «Обзор» in the team’s menu, each landing on the team at its «Истории»', async () => {
    serveOrganizations();
    serveBoards(alfaDataset(), { projectList: false });
    renderRoutes(`${ALFA}/teams/${organizationIds.platformTeam}/overview`);

    const backlogs = await within(mainNavigation()).findByRole('link', { name: 'Бэклог' });
    const links = within(mainNavigation()).getAllByRole('link');
    const overview = within(mainNavigation()).getByRole('link', { name: 'Обзор' });
    const boards = within(mainNavigation()).getByRole('link', { name: 'Канбан' });
    expect(links.indexOf(boards)).toBe(links.indexOf(overview) + 1);
    expect(links.indexOf(backlogs)).toBe(links.indexOf(overview) + 2);
    await userEvent.click(backlogs);

    expect(screen.getByTestId('location')).toHaveTextContent(
      teamAddress(organizationIds.platformTeam, 'backlogs', 'stories'),
    );
    expect(await backlogRows()).toEqual(['PRM-US-1']);

    await userEvent.click(within(mainNavigation()).getByRole('link', { name: 'Канбан' }));
    expect(screen.getByTestId('location')).toHaveTextContent(
      teamAddress(organizationIds.platformTeam, 'boards', 'stories'),
    );
    expect(await screen.findByRole('region', { name: 'Канбан' })).toBeInTheDocument();
  });
});

// feat-043 design § 3: a choice keeps the current entry when the new menu has it — the same
// segment and the same level — else opens the first entry (the old "not the level" rule is retired).
describe('mod-006 US-1 Scenario: Switching the team keeps the entry and the level', () => {
  it('on «Платформа»’s «Фичи», choosing «Пример проекта» at the team selector opens its «Фичи» backlog', async () => {
    serveOrganizations();
    serveBoards(alfaDataset(), { projectList: false });
    renderRoutes(teamAddress(organizationIds.platformTeam, 'backlogs', 'features'));
    await waitFor(() => expect(selectorLevels()[2]).toBe('Команда: Платформа'));

    await chooseIn(/^Команда:/, 'Пример проекта');

    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent(
        teamAddress(organizationIds.prmTeam, 'backlogs', 'features'),
      ),
    );
    expect(await screen.findByRole('button', { name: 'Уровень: Фичи' })).toBeInTheDocument();
  });

  it('the same on a board: «Платформа»’s «Эпики» board, «Пример проекта» chosen, opens its «Эпики» board', async () => {
    serveOrganizations();
    serveBoards(alfaDataset(), { projectList: false });
    renderRoutes(teamAddress(organizationIds.platformTeam, 'boards', 'epics'));
    await waitFor(() => expect(selectorLevels()[2]).toBe('Команда: Платформа'));

    await chooseIn(/^Команда:/, 'Пример проекта');

    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent(
        teamAddress(organizationIds.prmTeam, 'boards', 'epics'),
      ),
    );
  });
});

// feat-040 BR-TS-10 and mod-015 BR-18 are rewritten over feat-043: a project choice on a team's
// page opens the project's page of the same entry, narrowed by the team; an organization choice
// opens that organization's «Обзор». The switch resolver's teams read, its «Нет команд» and its
// slow and failing cases are retired with it (the selectors read `selector-offers` once).
describe('mod-015 BR-18 on a team-level page: a project or organization choice keeps the entry (review F1)', () => {
  it.each([
    ['backlogs', 'features'],
    ['boards', 'epics'],
  ])(
    'on «%s», choosing «Аналитика» at the project selector opens the project’s page of the entry at the same level, a team the project does not have reset',
    async (surface, level) => {
      serveOrganizations();
      serveBoards(twoContextsDataset(), { projectList: false });
      renderRoutes(teamAddress(organizationIds.platformTeam, surface, level));
      await waitFor(() => expect(selectorLevels()[2]).toBe('Команда: Платформа'));

      await chooseIn(/^Проект:/, /^Аналитика/);

      await waitFor(() =>
        expect(screen.getByTestId('location')).toHaveTextContent(
          `${ALFA}/projects/${organizationIds.anl}/${surface}/${level}`,
        ),
      );
      // «Платформа» is not linked to «Аналитика», so the choice resets the team (ADR-0032 § 3).
      expect(screen.getByTestId('location').getAttribute('data-search')).toBe('');
    },
  );

  it.each([['backlogs'], ['boards']])(
    'on «%s», choosing «Бета» at the organization selector opens that organization’s «Обзор» with «все» in the rest',
    async (surface) => {
      serveOrganizations();
      serveBoards(twoContextsDataset(), { projectList: false });
      const visited: string[] = [];
      const address = teamAddress(organizationIds.platformTeam, surface, 'epics');
      renderRoutes(address, visited);
      await waitFor(() => expect(selectorLevels()[2]).toBe('Команда: Платформа'));

      await chooseIn(/^Организация:/, 'Бета');

      const landing = `/organizations/${organizationIds.beta}/overview`;
      await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(landing));
      await waitFor(() =>
        expect(selectorLevels()).toEqual([
          'Организация: Бета',
          'Департамент: все',
          'Команда: все',
          'Проект: все',
        ]),
      );
      expect(visited).toEqual([address, landing]);
    },
  );
});

describe('mod-006 the not-found frames on a team-level address', () => {
  it.each([['backlogs'], ['boards']])(
    'an unknown level of «%s» answers «Страница не найдена», no entry current',
    async (surface) => {
      serveOrganizations();
      serveBoards(alfaDataset(), { projectList: false });
      renderRoutes(teamAddress(organizationIds.platformTeam, surface, 'tasks'));

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
      ).toBeInTheDocument();
      await within(mainNavigation()).findByRole('link', { name: 'Бэклог' });
      for (const name of ['Канбан', 'Бэклог']) {
        expect(within(mainNavigation()).getByRole('link', { name })).not.toHaveAttribute(
          'aria-current',
        );
      }
    },
  );

  // feat-040 BR-TS-1: the project-nested team address is no route; the page is not found whether or
  // not its project and team exist, with no redirect, and nothing of it is read.
  it.each([
    ['backlogs', organizationIds.platformTeam],
    ['boards', organizationIds.platformTeam],
    ['backlogs', 'not-a-uuid'],
  ])(
    'the retired address of «%s» of team %s answers «Страница не найдена», no redirect, no read',
    async (surface, teamId) => {
      serveOrganizations();
      const api = serveBoards(alfaDataset(), { projectList: false });
      const retired = `${PRM}/teams/${teamId}/${surface}/stories`;
      renderRoutes(retired);

      await waitFor(() =>
        expect(screen.queryByRole('status', { name: 'Загрузка данных' })).toBeNull(),
      );
      expect(
        await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
      ).toBeInTheDocument();
      await within(mainNavigation()).findByRole('link', { name: 'Бэклог' });
      expect(screen.getByTestId('location')).toHaveTextContent(retired);
      expect(api.reads(`/${surface}/`)).toEqual([]);
      for (const name of ['Канбан', 'Бэклог']) {
        expect(within(mainNavigation()).getByRole('link', { name })).not.toHaveAttribute(
          'aria-current',
        );
      }
    },
  );

  it.each([['backlogs'], ['boards']])(
    'with no team chosen, «%s» in the project’s menu lands on the project at its «Истории»',
    async (surface) => {
      serveOrganizations();
      serveBoards(twoContextsDataset(), { projectList: false });
      renderRoutes(`${PRM}/general`);

      const entry = await within(mainNavigation()).findByRole('link', {
        name: surface === 'backlogs' ? 'Бэклог' : 'Канбан',
      });
      expect(entry).toHaveAttribute('href', `${PRM}/${surface}/stories`);
      await userEvent.click(entry);
      await waitFor(() =>
        expect(screen.getByTestId('location')).toHaveTextContent(`${PRM}/${surface}/stories`),
      );
    },
  );

  // feat-043 D-1: mod-017's «Без команды» is the filter value `none` in the same parameter; it is
  // no team, so the project's views answer with their rows and the team selector stays «все».
  it.each(['?teamId=none', `?teamId=none&teamId=${organizationIds.platformTeam}`])(
    'the project’s backlog at %s shows its rows, not «Команда не найдена»',
    async (query) => {
      serveOrganizations();
      serveBoards(alfaDataset(), { projectList: false });
      renderRoutes(`${PRM}/backlogs/stories${query}`);

      // The page is the project's own backlog, with its filter on «Без команды».
      expect(
        await screen.findByRole('heading', { level: 1, name: 'Пример проекта' }),
      ).toBeInTheDocument();
      expect(await screen.findByText(/^сужено:/)).toBeInTheDocument();
      expect(screen.queryByRole('heading', { level: 1, name: 'Команда не найдена' })).toBeNull();
      await waitFor(() => expect(selectorLevels()[3]).toBe('Проект: Пример проекта'));
    },
  );

  it('a deleted team’s backlog answers «Команда не найдена», and the team selector no longer lists it (US-7)', async () => {
    const organizations = organizationsDataset();
    organizations.teams = organizations.teams.map((team) =>
      team.id === organizationIds.platformTeam ? { ...team, deleted: true } : team,
    );
    serveOrganizations(organizations);
    const boards = alfaDataset();
    boards.teams = boards.teams.map((team) =>
      team.id === organizationIds.platformTeam ? { ...team, active: false } : team,
    );
    serveBoards(boards, { projectList: false });
    renderRoutes(teamAddress(organizationIds.platformTeam, 'backlogs', 'stories'));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Команда не найдена' }),
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(selectorLevels()).toEqual([
        'Организация: Альфа',
        'Департамент: все',
        'Команда: все',
        'Проект: все',
      ]),
    );
    await userEvent.click(within(selectorGroup()).getByRole('combobox', { name: /^Команда:/ }));
    const names = (await screen.findAllByRole('option')).map((option) => option.textContent);
    expect(names).toEqual(expect.arrayContaining(['Аналитика', 'Пример проекта', 'Старый проект']));
    expect(names).not.toContain('Платформа');
  });
});
