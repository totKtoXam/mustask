import { describe, expect, it } from 'vitest';
import { useEffect } from 'react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http } from 'msw';
import { AuthContext } from '../features/auth/AuthContext';
import { serveBoards } from '../features/boards/testApi';
import { identifiers } from '../features/boards/testBacklog';
import { boardsDataset, item, type BoardsDataset } from '../features/boards/testFixtures';
import {
  ORGANIZATIONS_URL,
  exampleDataset as organizationsDataset,
  ids as organizationIds,
  problem as organizationsProblem,
} from '../features/organizations/testFixtures';
import {
  serveOrganizations,
  serveProblem as serveOrganizationsProblem,
} from '../features/organizations/testApi';
import { server } from '../test/mocks/server';
import { authValue, LocationProbe } from '../test/renderShell';
import { AppRoutes } from './routes';

// mod-006 through the panel's own router and the real context source: the
// team-level routes registered in `app/routes.tsx`, the entries of the
// navigation model, the switcher following a team-level address and moving
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
const switcher = () => screen.getByRole('group', { name: 'Контекст работы' });

function switcherLevels(): string[] {
  return within(switcher())
    .getAllByRole('button')
    .map((button) => button.getAttribute('aria-label') ?? '');
}

async function backlogRows(): Promise<string[]> {
  return identifiers(await screen.findByRole('table', { name: 'Бэклог' }));
}

describe('mod-006 the backlog route in the shell', () => {
  it('opens «Бэклоги» as the one h1 — the team’s name — and tab, its entry current, no trail, and the switcher follows the team in the address', async () => {
    serveOrganizations();
    serveBoards(alfaDataset(), { projectList: false });
    renderRoutes(teamAddress(organizationIds.platformTeam, 'backlogs', 'features'));

    expect(await screen.findByRole('heading', { level: 1, name: 'Платформа' })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    await waitFor(() => expect(document.title).toBe('Бэклоги — Mustask'));
    // feat-040 BR-TS-10: a team address sets the organization and the team; the project level shows
    // its current selection, the level's first entry when nothing is remembered.
    await waitFor(() =>
      expect(switcherLevels()).toEqual([
        'Организация: Альфа',
        'Проект: Аналитика',
        'Команда: Платформа',
      ]),
    );
    const entry = await within(mainNavigation()).findByRole('link', { name: 'Бэклоги' });
    expect(entry).toHaveAttribute(
      'href',
      teamAddress(organizationIds.platformTeam, 'backlogs', 'stories'),
    );
    expect(entry).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).toBeNull();
  });

  it('opens «Канбан» (mod-006’s «Доски», feat-001 BR-H-16) as the one h1 — the team’s name — and tab, its entry current, «Бэклоги» not, no trail', async () => {
    serveOrganizations();
    serveBoards(alfaDataset(), { projectList: false });
    renderRoutes(teamAddress(organizationIds.platformTeam, 'boards', 'epics'));

    expect(await screen.findByRole('heading', { level: 1, name: 'Платформа' })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    await waitFor(() => expect(document.title).toBe('Канбан — Mustask'));
    await waitFor(() => expect(switcherLevels()[2]).toBe('Команда: Платформа'));
    const boards = await within(mainNavigation()).findByRole('link', { name: 'Канбан' });
    expect(boards).toHaveAttribute(
      'href',
      teamAddress(organizationIds.platformTeam, 'boards', 'stories'),
    );
    expect(boards).toHaveAttribute('aria-current', 'page');
    expect(within(mainNavigation()).getByRole('link', { name: 'Бэклоги' })).not.toHaveAttribute(
      'aria-current',
    );
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).toBeNull();
  });

  it('lists «Канбан» and «Бэклоги» after «Рабочие элементы» in the work group once a team is chosen', async () => {
    serveOrganizations();
    serveBoards(alfaDataset(), { projectList: false });
    // The user's last choice: «Платформа» (a chosen team stays, whatever project the address names).
    localStorage.setItem(
      `mustask.context.${SUBJECT}`,
      JSON.stringify({
        organizationId: organizationIds.alfa,
        projectId: organizationIds.prm,
        teamId: organizationIds.platformTeam,
      }),
    );
    renderRoutes(`${PRM}/settings/general`);

    const backlogs = await within(mainNavigation()).findByRole('link', { name: 'Бэклоги' });
    const links = within(mainNavigation()).getAllByRole('link');
    const workItems = within(mainNavigation()).getByRole('link', { name: 'Рабочие элементы' });
    const boards = within(mainNavigation()).getByRole('link', { name: 'Канбан' });
    expect(links.indexOf(boards)).toBe(links.indexOf(workItems) + 1);
    expect(links.indexOf(backlogs)).toBe(links.indexOf(workItems) + 2);
    await userEvent.click(backlogs);

    // The entry lands on the team chosen, at the team's address (feat-040 BR-TS-1, BR-TS-10).
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

describe('mod-006 US-1 Scenario: Switching the team keeps the entry, not the level', () => {
  it('on «Платформа»’s «Фичи», choosing «Пример проекта» at the team level opens its «Истории» backlog', async () => {
    serveOrganizations();
    serveBoards(alfaDataset(), { projectList: false });
    renderRoutes(teamAddress(organizationIds.platformTeam, 'backlogs', 'features'));
    await waitFor(() => expect(switcherLevels()[2]).toBe('Команда: Платформа'));

    await userEvent.click(within(switcher()).getByRole('button', { name: /^Команда:/ }));
    await userEvent.click(await screen.findByRole('menuitemradio', { name: 'Пример проекта' }));

    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent(
        teamAddress(organizationIds.prmTeam, 'backlogs', 'stories'),
      ),
    );
    expect(await backlogRows()).toEqual(['PRM-US-2']);
    expect(screen.getByRole('button', { name: 'Уровень: Истории' })).toBeInTheDocument();
  });

  it('the same on a board: «Платформа»’s «Эпики» board, «Пример проекта» chosen, opens its «Истории» board', async () => {
    serveOrganizations();
    serveBoards(alfaDataset(), { projectList: false });
    renderRoutes(teamAddress(organizationIds.platformTeam, 'boards', 'epics'));
    await waitFor(() => expect(switcherLevels()[2]).toBe('Команда: Платформа'));

    await userEvent.click(within(switcher()).getByRole('button', { name: /^Команда:/ }));
    await userEvent.click(await screen.findByRole('menuitemradio', { name: 'Пример проекта' }));

    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent(
        teamAddress(organizationIds.prmTeam, 'boards', 'stories'),
      ),
    );
    expect(await screen.findByRole('region', { name: 'Канбан' })).toBeInTheDocument();
  });
});

// feat-040 BR-TS-10: a team's page is at the team's address and names no project, so a project choice
// moves nothing there. Retired by name, with the rule: «on «%s», choosing «Аналитика» at the project
// level opens the «Истории» landing of its first team» and «a project whose teams list comes back
// empty opens «Главная»: the entry is not visible there (BR-18)»; the rest of the block is rewritten
// over an organization choice, whose landing is the new organization's team level's default entry.
describe('mod-015 BR-18 on a team-level page: a project or organization choice keeps the entry (review F1)', () => {
  const REP = `/organizations/${organizationIds.beta}`;
  const BETA_TEAMS_URL = `${ORGANIZATIONS_URL}/${organizationIds.beta}/teams`;

  /** The landing a surface opens, as the page's own content shows it. */
  async function expectLanding(surface: string, identifier: string) {
    if (surface === 'backlogs') {
      expect(await backlogRows()).toEqual([identifier]);
    } else {
      expect(await screen.findByRole('region', { name: 'Канбан' })).toBeInTheDocument();
    }
    expect(screen.getByRole('button', { name: 'Уровень: Истории' })).toBeInTheDocument();
  }

  async function choose(level: 'Организация' | 'Проект', name: string) {
    await userEvent.click(
      within(switcher()).getByRole('button', { name: new RegExp(`^${level}:`) }),
    );
    await userEvent.click(await screen.findByRole('menuitemradio', { name }));
  }

  /** On «Платформа»'s «Фичи» backlog or «Эпики» board, the switcher synced to it. */
  async function onPlatform(surface: string, visited?: string[]) {
    const level = surface === 'backlogs' ? 'features' : 'epics';
    const address = teamAddress(organizationIds.platformTeam, surface, level);
    renderRoutes(address, visited);
    await waitFor(() => expect(switcherLevels()[2]).toBe('Команда: Платформа'));
    return address;
  }

  it.each([['backlogs'], ['boards']])(
    'on «%s», choosing «Аналитика» at the project level moves nothing: the page stays, and the chosen team stays (feat-040 BR-TS-10)',
    async (surface) => {
      serveOrganizations();
      serveBoards(twoContextsDataset(), { projectList: false });
      const visited: string[] = [];
      const address = await onPlatform(surface, visited);

      await choose('Проект', 'Аналитика');

      await waitFor(() =>
        expect(switcherLevels()).toEqual([
          'Организация: Альфа',
          'Проект: Аналитика',
          'Команда: Платформа',
        ]),
      );
      expect(screen.getByTestId('location')).toHaveTextContent(address);
      expect(visited).toEqual([address]);
    },
  );

  it.each([['backlogs'], ['boards']])(
    'on «%s», choosing «Бета» at the organization level opens the «Истории» landing of its team level’s first entry',
    async (surface) => {
      serveOrganizations();
      serveBoards(twoContextsDataset(), { projectList: false });
      const visited: string[] = [];
      const address = await onPlatform(surface, visited);

      await choose('Организация', 'Бета');

      const landing = `${REP}/teams/${organizationIds.repTeam}/${surface}/stories`;
      await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(landing));
      await expectLanding(surface, 'REP-US-1');
      await waitFor(() =>
        expect(switcherLevels()).toEqual([
          'Организация: Бета',
          'Проект: Отчётность',
          'Команда: Отчётность',
        ]),
      );
      expect(visited).toEqual([address, landing]);
    },
  );

  it('an organization with no team opens «Главная» (mod-002 design § 2.2), the switcher saying «Нет команд»', async () => {
    const organizations = organizationsDataset();
    organizations.teams = organizations.teams.map((team) =>
      team.id === organizationIds.repTeam ? { ...team, deleted: true } : team,
    );
    serveOrganizations(organizations);
    serveBoards(twoContextsDataset(), { projectList: false });
    await onPlatform('backlogs');

    await choose('Организация', 'Бета');

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/home'));
    expect(await screen.findByRole('heading', { level: 1, name: 'Главная' })).toBeInTheDocument();
    expect(await within(switcher()).findByText('Нет команд')).toBeInTheDocument();
    // The project level is drawn, whatever the team level holds (BR-TS-10).
    expect(within(switcher()).getByRole('button', { name: 'Проект: Отчётность' })).toBeVisible();
  });

  it('an organization with no project but a team opens that team’s landing (feat-040 BR-TS-10)', async () => {
    const organizations = organizationsDataset();
    organizations.projects = organizations.projects.map((project) =>
      project.id === organizationIds.rep ? { ...project, active: false } : project,
    );
    serveOrganizations(organizations);
    serveBoards(twoContextsDataset(), { projectList: false });
    await onPlatform('backlogs');

    await choose('Организация', 'Бета');

    const landing = `${REP}/teams/${organizationIds.repTeam}/backlogs/stories`;
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(landing));
    expect(await within(switcher()).findByText('Нет проектов')).toBeInTheDocument();
    expect(within(switcher()).getByRole('button', { name: 'Команда: Отчётность' })).toBeVisible();
  });

  it('a teams read that fails opens «Главная», the switcher showing «Не удалось загрузить команды» (plan-frontend § 4.5)', async () => {
    serveOrganizations();
    serveBoards(twoContextsDataset(), { projectList: false });
    await onPlatform('backlogs');
    serveOrganizationsProblem(
      'get',
      BETA_TEAMS_URL,
      organizationsProblem('organizations.validation_failed'),
    );

    await choose('Организация', 'Бета');

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/home'));
    expect(await within(switcher()).findByText('Не удалось загрузить команды')).toBeInTheDocument();
  });

  it('a slow teams read keeps the backlog on screen, with no «Главная» and no other address in between', async () => {
    serveOrganizations();
    serveBoards(twoContextsDataset(), { projectList: false });
    let release: () => void = () => {};
    let arrived = false;
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(BETA_TEAMS_URL, async () => {
        arrived = true;
        await held;
        return undefined;
      }),
    );
    const visited: string[] = [];
    const address = await onPlatform('backlogs', visited);

    await choose('Организация', 'Бета');
    await waitFor(() => expect(arrived).toBe(true));
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(screen.getByTestId('location')).toHaveTextContent(address);
    expect(await screen.findByRole('heading', { level: 1, name: 'Платформа' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Уровень: Фичи' })).toBeInTheDocument();

    release();
    const landing = `${REP}/teams/${organizationIds.repTeam}/backlogs/stories`;
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(landing));
    await expectLanding('backlogs', 'REP-US-1');
    expect(visited).toEqual([address, landing]);
  });
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
      await within(mainNavigation()).findByRole('link', { name: 'Бэклоги' });
      for (const name of ['Канбан', 'Бэклоги']) {
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

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
      ).toBeInTheDocument();
      await within(mainNavigation()).findByRole('link', { name: 'Бэклоги' });
      expect(screen.getByTestId('location')).toHaveTextContent(retired);
      expect(api.reads(`/${surface}/`)).toEqual([]);
      for (const name of ['Канбан', 'Бэклоги']) {
        expect(within(mainNavigation()).getByRole('link', { name })).not.toHaveAttribute(
          'aria-current',
        );
      }
    },
  );

  it.each([['backlogs'], ['boards']])(
    'with no team chosen, «%s» lands on the team level’s first entry at its address (feat-040 BR-TS-10)',
    async (surface) => {
      serveOrganizations();
      serveBoards(twoContextsDataset(), { projectList: false });
      renderRoutes(`${PRM}/settings/general`);

      const entry = await within(mainNavigation()).findByRole('link', {
        name: surface === 'backlogs' ? 'Бэклоги' : 'Канбан',
      });
      expect(entry).toHaveAttribute(
        'href',
        teamAddress(organizationIds.anlTeam, surface, 'stories'),
      );
      await userEvent.click(entry);
      await waitFor(() =>
        expect(screen.getByTestId('location')).toHaveTextContent(
          teamAddress(organizationIds.anlTeam, surface, 'stories'),
        ),
      );
    },
  );

  it('a deleted team’s backlog answers «Команда не найдена», and the switcher no longer lists it (US-7)', async () => {
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
    // The user was last in «Пример проекта»; an address whose team is not found sets nothing.
    localStorage.setItem(
      `mustask.context.${SUBJECT}`,
      JSON.stringify({
        organizationId: organizationIds.alfa,
        projectId: organizationIds.prm,
        teamId: organizationIds.prmTeam,
      }),
    );
    renderRoutes(teamAddress(organizationIds.platformTeam, 'backlogs', 'stories'));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Команда не найдена' }),
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(switcherLevels()).toEqual([
        'Организация: Альфа',
        'Проект: Пример проекта',
        'Команда: Пример проекта',
      ]),
    );
    await userEvent.click(within(switcher()).getByRole('button', { name: /^Команда:/ }));
    // feat-040 BR-TS-10: the organization's teams, by name (none is the user's own), the deleted one gone.
    expect(
      (await screen.findAllByRole('menuitemradio')).map((option) => option.textContent),
    ).toEqual(['Аналитика', 'Пример проекта', 'Старый проект']);
  });
});
