import { afterEach, describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, delay, http } from 'msw';
import { theme } from '../../../app/theme';
import { server } from '../../../test/mocks/server';
import { serveBoards } from '../testApi';
import {
  addControl,
  findBacklogTable,
  grip,
  identifiers,
  orders,
  outline,
  rowOf,
  rowsOf,
} from '../testBacklog';
import {
  PROJECT_BACKLOG_URL,
  boardsDataset,
  ids,
  item,
  platformItem,
  projectDataset,
} from '../testFixtures';
import { chooseLevel, levelButton } from '../testLevels';
import {
  backlogAddress,
  boardAddress,
  projectBacklogAddress,
  projectBoardAddress,
  projectPrefix,
  renderBoards,
} from '../testRender';

// feat-039 US-1: the project's backlog (BR-PB-1..-3, -6..-9): every item of the
// project at the level, from every team, on the team page's table, under the team
// page's head, reached from the head's team drop-down. «Мастаск» has the first team
// «Мастаск», «Платформа» and «Аналитика»; `projectDataset` puts a story on each of the
// three and one in the other project «Операции».

afterEach(() => {
  focusManager.setFocused(undefined);
});

const ALL_STORIES = ['MSK-US-2', 'MSK-US-3', 'MSK-US-9'];

function teamButton(): HTMLElement {
  return screen.getByRole('button', { name: 'Сменить команду' });
}

async function openTeamMenu(): Promise<HTMLElement> {
  await userEvent.click(teamButton());
  return screen.findByRole('menu', { name: 'Команды проекта' });
}

describe('US-1 Scenario: A project’s backlog holds every item of the project, from every team (BR-MX-17, BR-PB-2)', () => {
  it('lists the stories of «Мастаск», «Платформа» and «Аналитика» with «Порядок» 1 to 3, and no item of another project, each once; the requests are the project’s own', async () => {
    const api = serveBoards(projectDataset());
    renderBoards(projectBacklogAddress());

    const table = await findBacklogTable();

    expect(identifiers(table)).toEqual(ALL_STORIES);
    expect(orders(table)).toEqual(['1', '2', '3']);
    expect(table).not.toHaveTextContent('OPS-US-1');
    expect(api.reads('/backlogs/stories').map((request) => request.path)).toEqual([
      `/api/organizations/${ids.example}/projects/${ids.msk}/backlogs/stories`,
    ]);
  });

  it('leaves the teams’ own backlogs as they were: «Платформа» still lists MSK-US-3 alone', async () => {
    serveBoards(projectDataset());
    renderBoards(backlogAddress());

    expect(identifiers(await findBacklogTable())).toEqual(['MSK-US-3']);
  });

  it('holds each level’s items: «Фичи» the feature alone, «Эпики» the epic alone, as the stories’ page holds the stories', async () => {
    const data = projectDataset();
    data.items.push(
      item(ids.msk, 20, 'FEAT', 'Вход по ссылке', { areaId: ids.analyticsArea }),
      platformItem(21, 'EPIC', 'Единый вход'),
    );
    serveBoards(data);
    renderBoards(projectBacklogAddress('stories'));
    await findBacklogTable();

    await chooseLevel('Фичи');
    expect(screen.getByTestId('location')).toHaveTextContent(projectBacklogAddress('features'));
    expect(outline(await screen.findByRole('table', { name: 'Бэклог' }))).toEqual(['MSK-FEAT-20']);

    await chooseLevel('Эпики');
    expect(screen.getByTestId('location')).toHaveTextContent(projectBacklogAddress('epics'));
    await waitFor(() =>
      expect(outline(screen.getByRole('table', { name: 'Бэклог' }))).toEqual(['MSK-EPIC-21']),
    );
  });

  it('stands the children beneath their parent whatever the team, a child not among the top-level rows, «Порядок» over the top-level rows only', async () => {
    const data = projectDataset();
    const us3 = data.items.find((record) => record.number === 3);
    const us9 = data.items.find((record) => record.number === 9);
    if (!us3 || !us9) throw new Error('No parent.');
    data.items.push(
      platformItem(4, 'TASK', 'Шаблон письма', { parentId: us3.id }),
      item(ids.msk, 5, 'TASK', 'Настроить SMTP', { areaId: ids.analyticsArea, parentId: us3.id }),
      item(ids.msk, 6, 'BUG', 'Письмо не доходит', { areaId: ids.mskRoot, parentId: us9.id }),
    );
    serveBoards(data);
    renderBoards(projectBacklogAddress());

    const table = await findBacklogTable();

    expect(outline(table)).toEqual([
      'MSK-US-2',
      'MSK-US-3',
      '  MSK-TASK-4',
      '  MSK-TASK-5',
      'MSK-US-9',
      '  MSK-BUG-6',
    ]);
    expect(orders(table)).toEqual(['1', '2', '', '', '3', '']);
  });

  it('keeps the rows’ table: the columns, the first state expanded, and a row’s title opening the item’s card over the page', async () => {
    const data = projectDataset();
    const us3 = data.items.find((record) => record.number === 3);
    if (!us3) throw new Error('No parent.');
    data.items.push(platformItem(4, 'TASK', 'Шаблон письма', { parentId: us3.id }));
    serveBoards(data);
    renderBoards(projectBacklogAddress());

    const table = await findBacklogTable();

    expect(
      within(table)
        .getAllByRole('columnheader')
        .map((header) => header.textContent),
    ).toEqual([
      '',
      'Порядок',
      'Тип',
      'Заголовок',
      'Статус',
      'Команда',
      'Трудоёмкость',
      'Бизнес-ценность',
      'Вид ценности',
    ]);
    expect(rowOf(table, 'MSK-TASK-4')).toBeInTheDocument();
    const title = within(rowOf(table, 'MSK-US-2')).getByRole('link', { hidden: true });
    expect(title).toHaveAttribute(
      'href',
      `/organizations/${ids.example}/projects/${ids.msk}/workitems/MSK-US-2`,
    );
  });
});

describe('US-1 Scenario: A row’s add works on the project’s backlog, without a team (BR-PB-3, OQ-5)', () => {
  it('«+» on an epic adds a feature beneath it: the request names the parent and no team, the notice names the item, the item takes the parent’s area', async () => {
    const data = boardsDataset();
    data.items.push(platformItem(1, 'EPIC', 'Единый вход'));
    const api = serveBoards(data);
    renderBoards(projectBacklogAddress('epics'));
    const table = await findBacklogTable();

    await userEvent.click(await addControl('MSK-EPIC-1'));
    const input = await screen.findByRole('textbox', { name: 'Заголовок: Фича (MSK-EPIC-1)' });
    await userEvent.click(input);
    await userEvent.paste('Вход по ссылке');
    await userEvent.keyboard('{Enter}');

    expect(await screen.findByText('Рабочий элемент MSK-FEAT-2 создан.')).toBeInTheDocument();
    const create = api.writes().find((request) => request.path.endsWith('/workitems'));
    expect(create?.body).toMatchObject({ parentId: api.item(ids.msk, 1).id });
    expect(create?.body).not.toHaveProperty('teamId');
    expect(api.item(ids.msk, 2).areaId).toBe(ids.platform);
    await waitFor(() => expect(outline(table)).toEqual(['MSK-EPIC-1', '  MSK-FEAT-2']));
    expect(api.data.projectPlaces).toEqual([]);
    expect(api.data.places).toEqual([]);
  });
});

// mod-017 lifts feat-039 BR-PB-6 (design § 6.1): the project's head carries «Новый рабочий элемент» for
// a writer, and the empty level offers it. Rewritten, same scenario, over the new rule.
describe('US-1 Scenario: The head and the empty states carry the create command for a writer (BR-PB-6 lifted)', () => {
  it('reads the project’s name from the page’s own read as the h1, with the one tab «Бэклог», «Уровень: Истории», «Показать канбан» to the project’s board, and «Новый рабочий элемент»', async () => {
    const data = projectDataset();
    const project = data.projects.find((candidate) => candidate.id === ids.msk);
    if (!project) throw new Error('No project.');
    // The shell’s list says «Мастаск»: the page’s own read, once there, is what the h1 shows.
    project.name = 'Альфа';
    serveBoards(data);
    renderBoards(projectBacklogAddress());
    await findBacklogTable();

    expect(await screen.findByRole('heading', { level: 1, name: 'Альфа' })).toBeInTheDocument();
    await waitFor(() => expect(document.title).toBe('Бэклоги — Mustask'));
    const tabs = screen.getByRole('tablist', { name: 'Вид страницы' });
    expect(within(tabs).getAllByRole('tab')).toHaveLength(1);
    const tab = within(tabs).getByRole('tab', { name: 'Бэклог' });
    expect(tab).toHaveAttribute('aria-selected', 'true');
    expect(tab).toHaveAttribute('href', projectBacklogAddress('stories'));
    expect(levelButton()).toHaveAccessibleName('Уровень: Истории');
    const toBoard = screen.getByRole('link', { name: 'Показать канбан' });
    expect(toBoard).toHaveAttribute('href', projectBoardAddress('stories'));
    expect(toBoard.querySelector('[data-testid="ArrowCircleRightOutlinedIcon"]')).not.toBeNull();
    expect(getComputedStyle(toBoard).getPropertyValue('--variant-containedBg')).toBe(
      theme.palette.shell.buttonNeutral,
    );
    expect(screen.getByRole('button', { name: 'Новый рабочий элемент' })).toBeInTheDocument();
  });

  it('the level drop-down opens the project’s addresses at the other levels', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBacklogAddress('stories'));
    await findBacklogTable();

    await userEvent.click(levelButton());
    const menu = await screen.findByRole('menu', { name: /^Уровень: / });

    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((entry) => entry.getAttribute('href')),
    ).toEqual([
      projectBacklogAddress('epics'),
      projectBacklogAddress('features'),
      projectBacklogAddress('stories'),
    ]);
  });

  it.each([['admin'], ['member']] as const)(
    'an empty level reads «Бэклог пуст» with the writer’s hint and the action for the %s',
    async (role) => {
      serveBoards(projectDataset());
      renderBoards(projectBacklogAddress('epics'), { role });

      expect(await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' })).toBeVisible();
      expect(
        screen.getByText('Добавьте рабочий элемент — он встанет в конец бэклога.'),
      ).toBeInTheDocument();
      // The header's primary and the frame's action.
      expect(screen.getAllByRole('button', { name: 'Новый рабочий элемент' })).toHaveLength(2);
      expect(screen.queryByText('В проекте нет рабочих элементов этого уровня.')).toBeNull();
      expect(screen.getByRole('heading', { level: 1, name: 'Мастаск' })).toBeInTheDocument();
      expect(levelButton()).toHaveAccessibleName('Уровень: Эпики');
    },
  );

  it('an empty level reads «Бэклог пуст» with the reader’s hint and no action for the viewer', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBacklogAddress('epics'), { role: 'viewer' });

    expect(await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' })).toBeVisible();
    expect(
      screen.getByText(
        'Рабочие элементы создают участники организации с ролью «Участник» или «Администратор».',
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Новый рабочий элемент' })).toBeNull();
  });
});

describe('US-1 Scenario: The team drop-down reaches the project’s views (BR-PB-7)', () => {
  it('from a team’s page «Весь проект» closes «Команды проекта» and opens the project’s page at the same level, focus on «Сменить команду»', async () => {
    serveBoards(projectDataset());
    renderBoards(backlogAddress('epics'));
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' });

    const menu = await openTeamMenu();
    const entry = within(menu).getByRole('menuitem', { name: 'Весь проект' });
    expect(entry).toHaveAttribute('href', projectBacklogAddress('epics'));
    await userEvent.click(entry);

    expect(screen.getByTestId('location')).toHaveTextContent(projectBacklogAddress('epics'));
    expect(await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' })).toBeVisible();
    await waitFor(() => expect(teamButton()).toHaveFocus());
  });

  it('on the project’s page the menu lists the teams in their order and «Весь проект» last after a divider, selected with `aria-current="page"`; no team is', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBacklogAddress('epics'));
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' });

    const menu = await openTeamMenu();

    const entries = within(menu).getAllByRole('menuitem');
    expect(entries.map((entry) => entry.textContent)).toEqual([
      'Мастаск',
      'Платформа',
      'Аналитика',
      'Весь проект',
    ]);
    expect(entries.map((entry) => entry.getAttribute('aria-current'))).toEqual([
      null,
      null,
      null,
      'page',
    ]);
    expect(entries.map((entry) => entry.classList.contains('Mui-selected'))).toEqual([
      false,
      false,
      false,
      true,
    ]);
    expect(entries.map((entry) => entry.getAttribute('href'))).toEqual([
      backlogAddress('epics', ids.mskTeam),
      backlogAddress('epics', ids.platformTeam),
      backlogAddress('epics', ids.analyticsTeam),
      projectBacklogAddress('epics'),
    ]);
    // The divider stands between the last team and the entry, as a separator no arrow reaches.
    const separator = within(menu).getByRole('separator');
    expect(entries[2]?.nextElementSibling).toBe(separator);
    expect(separator.nextElementSibling).toBe(entries[3]);
  });

  it('choosing the selected «Весь проект» moves nothing and leaves focus on «Сменить команду»', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBacklogAddress('epics'));
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' });

    await userEvent.click(
      within(await openTeamMenu()).getByRole('menuitem', { name: 'Весь проект' }),
    );

    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
    expect(screen.getByTestId('location')).toHaveTextContent(projectBacklogAddress('epics'));
    await waitFor(() => expect(teamButton()).toHaveFocus());
    expect(screen.getByRole('heading', { level: 2, name: 'Бэклог пуст' })).toBeVisible();
  });

  it('choosing a team opens that team’s page at the same level, focus on the new «Сменить команду»', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBacklogAddress('epics'));
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' });

    await userEvent.click(
      within(await openTeamMenu()).getByRole('menuitem', { name: 'Платформа' }),
    );

    expect(screen.getByTestId('location')).toHaveTextContent(
      backlogAddress('epics', ids.platformTeam),
    );
    expect(await screen.findByRole('heading', { level: 1, name: 'Платформа' })).toBeInTheDocument();
    await waitFor(() => expect(teamButton()).toHaveFocus());
  });

  it('works by keyboard: ArrowDown opens it on «Весь проект», ArrowUp reaches the last team, Escape closes it with focus back on the button', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBacklogAddress('epics'));
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' });

    teamButton().focus();
    await userEvent.keyboard('{ArrowDown}');
    const menu = await screen.findByRole('menu', { name: 'Команды проекта' });
    await waitFor(() =>
      expect(within(menu).getByRole('menuitem', { name: 'Весь проект' })).toHaveFocus(),
    );
    // The divider has no `tabindex`: the arrow skips it.
    await userEvent.keyboard('{ArrowUp}');
    expect(within(menu).getByRole('menuitem', { name: 'Аналитика' })).toHaveFocus();
    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
    expect(teamButton()).toHaveFocus();
  });

  it('on a team’s page ArrowDown from the last team reaches «Весь проект», and End goes to it', async () => {
    serveBoards(projectDataset());
    renderBoards(backlogAddress('epics', ids.analyticsTeam));
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' });

    teamButton().focus();
    await userEvent.keyboard('{ArrowDown}');
    const menu = await screen.findByRole('menu', { name: 'Команды проекта' });
    await waitFor(() =>
      expect(within(menu).getByRole('menuitem', { name: 'Аналитика' })).toHaveFocus(),
    );
    await userEvent.keyboard('{ArrowDown}');
    expect(within(menu).getByRole('menuitem', { name: 'Весь проект' })).toHaveFocus();
    await userEvent.keyboard('{Home}');
    expect(within(menu).getByRole('menuitem', { name: 'Мастаск' })).toHaveFocus();
    await userEvent.keyboard('{End}');
    expect(within(menu).getByRole('menuitem', { name: 'Весь проект' })).toHaveFocus();
  });

  it('on «Канбан» «Весь проект» opens the project’s board at the same level', async () => {
    serveBoards(projectDataset());
    renderBoards(boardAddress('features'));
    await screen.findByRole('heading', { level: 2, name: 'Канбан пуст' });

    await userEvent.click(
      within(await openTeamMenu()).getByRole('menuitem', { name: 'Весь проект' }),
    );

    expect(screen.getByTestId('location')).toHaveTextContent(projectBoardAddress('features'));
    await waitFor(() => expect(teamButton()).toHaveFocus());
  });

  it('shows no button while the shell’s teams are not read: the page works', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBacklogAddress(), {
      context: {
        status: 'loading',
        organizations: [],
        selection: {},
        isReal: true,
        select: () => {},
      },
    });

    expect(identifiers(await findBacklogTable())).toEqual(ALL_STORIES);
    expect(screen.queryByRole('button', { name: 'Сменить команду' })).toBeNull();
    expect(screen.getByRole('heading', { level: 1, name: 'Мастаск' })).toBeInTheDocument();
  });
});

describe('US-1 Scenario: The shell on a project’s backlog (BR-PB-9)', () => {
  it.each([['epics'], ['features'], ['stories']])(
    'at «%s» «Бэклоги» is current with no trail, «Канбан» is not, and pressing «Бэклоги» opens the switcher’s team’s «Истории»',
    async (level) => {
      serveBoards(projectDataset());
      renderBoards(projectBacklogAddress(level));
      await screen.findByRole('heading', { level: 1, name: 'Мастаск' });

      const navigation = screen.getByRole('navigation', { name: 'Основное меню' });
      const backlogs = within(navigation).getByRole('link', { name: 'Бэклоги' });
      expect(backlogs).toHaveAttribute('aria-current', 'page');
      expect(backlogs).toHaveAttribute('href', backlogAddress('stories'));
      expect(within(navigation).getByRole('link', { name: 'Канбан' })).not.toHaveAttribute(
        'aria-current',
      );
      expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).toBeNull();
    },
  );
});

describe('US-1 Scenario: Who reads (BR-PB-5, spec § Roles)', () => {
  it('shows a reader the same rows, with no «+», no «Ещё действия» and no grip, and none drags', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBacklogAddress(), { role: 'viewer' });

    const table = await findBacklogTable();

    expect(identifiers(table)).toEqual(ALL_STORIES);
    expect(screen.queryAllByRole('button', { name: /^Ещё действия/, hidden: true })).toHaveLength(
      0,
    );
    expect(screen.queryAllByRole('button', { name: /^Добавить/, hidden: true })).toHaveLength(0);
    for (const identifier of ALL_STORIES) {
      expect(() => grip(table, identifier)).toThrow();
      expect(rowOf(table, identifier)).not.toHaveAttribute('draggable', 'true');
    }
  });

  it('shows a member a row’s «Ещё действия» and its grip', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBacklogAddress(), { role: 'member' });

    const table = await findBacklogTable();

    expect(rowsOf(table)).toHaveLength(3);
    expect(grip(table, 'MSK-US-2')).toBeInTheDocument();
    expect(rowOf(table, 'MSK-US-2')).toHaveAttribute('draggable', 'true');
  });
});

describe('US-1 Scenario: A missing address is told from the outside in (BR-MX-1)', () => {
  it.each([['tasks'], ['sprints'], ['Epics']])(
    '`<project>/backlogs/%s` is «Страница не найдена», and nothing is read',
    async (level) => {
      const api = serveBoards(projectDataset());
      renderBoards(`${projectPrefix()}/backlogs/${level}`);

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
      ).toBeInTheDocument();
      expect(api.reads('/backlogs/')).toEqual([]);
    },
  );

  it.each([
    [
      'the project is deactivated',
      'Проект не найден',
      `/organizations/${ids.example}/projects/${ids.ops}`,
    ],
    [
      'the project names nothing',
      'Проект не найден',
      `/organizations/${ids.example}/projects/f0000000-0000-4000-8000-0000000000ff`,
    ],
    [
      'the project belongs to another organization',
      'Проект не найден',
      `/organizations/${ids.example}/projects/${ids.otherMsk}`,
    ],
    [
      'the organization names nothing',
      'Организация не найдена',
      `/organizations/e0000000-0000-4000-8000-0000000000ff/projects/${ids.msk}`,
    ],
  ])('shows «%s» as the full-page frame when %s', async (_case, frame, prefix) => {
    const data = projectDataset();
    data.projects = data.projects.map((project) =>
      project.id === ids.ops ? { ...project, active: false } : project,
    );
    serveBoards(data);
    renderBoards(`${prefix}/backlogs/stories`);

    expect(await screen.findByRole('heading', { level: 1, name: frame })).toBeInTheDocument();
    expect(screen.getByText('Проверьте адрес или вернитесь на главную.')).toBeInTheDocument();
    // The frame replaces the whole page: its heading is the page’s one `h1`, no head beside it.
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(screen.queryByRole('tablist', { name: 'Вид страницы' })).toBeNull();
    expect(screen.queryByRole('button', { name: /^Уровень: / })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Сменить команду' })).toBeNull();
    // No team frame can occur on a project page.
    expect(screen.queryByRole('heading', { name: 'Команда не найдена' })).toBeNull();
  });
});

describe('US-1 Scenario: The read’s states (design § 7)', () => {
  it('keeps the head complete and shows the registry skeleton «Загрузка данных» while the backlog is read', async () => {
    serveBoards(projectDataset());
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(PROJECT_BACKLOG_URL, async () => {
        await held;
        await delay(0);
        return undefined;
      }),
    );
    renderBoards(projectBacklogAddress());

    const skeleton = await screen.findByRole('status', { name: 'Загрузка данных' });
    expect(
      within(skeleton)
        .getAllByRole('columnheader')
        .map((header) => header.textContent),
    ).toEqual([
      '',
      'Порядок',
      'Тип',
      'Заголовок',
      'Статус',
      'Команда',
      'Трудоёмкость',
      'Бизнес-ценность',
      'Вид ценности',
    ]);
    expect(screen.getByRole('heading', { level: 1, name: 'Мастаск' })).toBeInTheDocument();
    expect(teamButton()).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Бэклог' })).toBeInTheDocument();
    expect(levelButton()).toHaveAccessibleName('Уровень: Истории');
    expect(screen.getByRole('link', { name: 'Показать канбан' })).toBeInTheDocument();

    release();
    expect(await findBacklogTable()).toBeInTheDocument();
    expect(screen.queryByRole('status', { name: 'Загрузка данных' })).toBeNull();
  });

  it('shows the server’s text with «Повторить» in place of the table, the head kept, and the rows after a retry', async () => {
    const api = serveBoards(projectDataset());
    server.use(
      http.get(
        PROJECT_BACKLOG_URL,
        () =>
          HttpResponse.json(
            { status: 500, title: 'Ошибка', detail: 'Сервис не ответил.' },
            { status: 500 },
          ),
        { once: true },
      ),
    );
    renderBoards(projectBacklogAddress());

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Сервис не ответил.');
    expect(screen.getByRole('heading', { level: 1, name: 'Мастаск' })).toBeInTheDocument();
    expect(levelButton()).toHaveAccessibleName('Уровень: Истории');
    expect(screen.queryByRole('table', { name: 'Бэклог' })).toBeNull();

    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));

    expect(identifiers(await findBacklogTable())).toEqual(ALL_STORIES);
    expect(api.reads('/backlogs/stories')).toHaveLength(1);
  });

  it('shows `boards.level.not_found` — a process without the level — as the banner, not a frame', async () => {
    serveBoards(projectDataset());
    server.use(
      http.get(PROJECT_BACKLOG_URL, () =>
        HttpResponse.json(
          {
            status: 404,
            title: 'boards.level.not_found',
            detail: 'Уровень бэклога не найден.',
            errorCode: 'boards.level.not_found',
          },
          { status: 404 },
        ),
      ),
    );
    renderBoards(projectBacklogAddress('epics'));

    expect(await screen.findByRole('alert')).toHaveTextContent('Уровень бэклога не найден.');
    expect(screen.getByRole('heading', { level: 1, name: 'Мастаск' })).toBeInTheDocument();
  });

  it('a re-read that fails leaves the table with its rows and the error above it; a retry that succeeds takes the error away', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBacklogAddress());
    const table = await findBacklogTable();
    server.use(
      http.get(
        PROJECT_BACKLOG_URL,
        () =>
          HttpResponse.json(
            { status: 500, title: 'Ошибка', detail: 'Сервис не ответил.' },
            { status: 500 },
          ),
        { once: true },
      ),
    );

    focusManager.setFocused(false);
    focusManager.setFocused(true);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Сервис не ответил.');
    expect(identifiers(table)).toEqual(ALL_STORIES);

    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));

    await waitFor(() => expect(screen.queryByRole('alert')).toBeNull());
    expect(identifiers(table)).toEqual(ALL_STORIES);
  });

  it('reads again when the tab regains focus and shows an item another user added', async () => {
    const api = serveBoards(projectDataset());
    renderBoards(projectBacklogAddress());
    const table = await findBacklogTable();
    expect(identifiers(table)).toEqual(ALL_STORIES);

    api.data.items.push(item(ids.msk, 12, 'US', 'Выход из системы', { areaId: ids.mskRoot }));
    focusManager.setFocused(false);
    focusManager.setFocused(true);

    await waitFor(() => expect(identifiers(table)).toEqual([...ALL_STORIES, 'MSK-US-12']));
  });
});
