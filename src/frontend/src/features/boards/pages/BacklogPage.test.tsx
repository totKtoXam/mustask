import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, delay, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { CATEGORY_MARKER_CLASS } from '../../../shared/ui/WorkTrackingMarks';
import { serveBoards } from '../testApi';
import {
  BACKLOG_URL,
  boardsDataset,
  ids,
  item,
  platformItem,
  stateId,
  storiesDataset,
} from '../testFixtures';
import { chooseLevel, levelButton, levelMenu } from '../testLevels';
import { backlogAddress, mskForm, renderBoards, teamPrefix } from '../testRender';

const HEADERS = [
  'Порядок',
  'Перемещение',
  'ID',
  'Тип',
  'Заголовок',
  'Родитель',
  'Исполнитель',
  'Статус',
  'Действия',
];

async function backlogTable() {
  return screen.findByRole('table', { name: 'Бэклог' });
}

function rowsOf(table: HTMLElement): HTMLElement[] {
  return within(table).getAllByRole('row').slice(1);
}

function cellsOf(row: HTMLElement): string[] {
  return within(row)
    .getAllByRole('cell')
    .map((cell) => cell.textContent ?? '');
}

/** The rows' identifiers, top to bottom. */
function identifiers(table: HTMLElement): string[] {
  return rowsOf(table).map((row) => cellsOf(row)[2] ?? '');
}

function orders(table: HTMLElement): string[] {
  return rowsOf(table).map((row) => cellsOf(row)[0] ?? '');
}

/** US-1's first scenario's items, with MSK-US-7 in «Отчёты», which has no owner of its own. */
function membershipDataset() {
  const data = boardsDataset();
  data.items.push(
    platformItem(1, 'EPIC', 'Единый вход'),
    platformItem(2, 'US', 'Вход по паролю'),
    platformItem(3, 'TASK', 'Настроить сборку'),
    platformItem(6, 'FEAT', 'Отчёты по командам'),
    item(ids.msk, 4, 'BUG', 'Падение при входе', { areaId: ids.api }),
    item(ids.msk, 5, 'US', 'Сводка проекта', { areaId: ids.mskRoot }),
    item(ids.msk, 7, 'US', 'Отчёт за квартал', { areaId: ids.reports }),
  );
  return data;
}

describe('US-1 Scenario: A team’s backlog holds its own items of the level', () => {
  it('lists «Платформа»’s stories, its epic on «Эпики», its feature on «Фичи», and MSK-US-5 on «Мастаск»', async () => {
    serveBoards(membershipDataset());
    renderBoards(backlogAddress('stories'));

    expect(identifiers(await backlogTable())).toEqual(['MSK-US-2', 'MSK-BUG-4']);

    await chooseLevel('Эпики');
    await waitFor(async () => expect(identifiers(await backlogTable())).toEqual(['MSK-EPIC-1']));

    await chooseLevel('Фичи');
    await waitFor(async () => expect(identifiers(await backlogTable())).toEqual(['MSK-FEAT-6']));
  });

  it('MSK-US-5 in «Мастаск» is on the first team’s backlog; MSK-TASK-3 is on none (US-1 Scenario: An area with no owner of its own counts for its nearest owner — MSK-US-7 there too)', async () => {
    serveBoards(membershipDataset());
    renderBoards(backlogAddress('stories', ids.mskTeam));

    expect(identifiers(await backlogTable())).toEqual(['MSK-US-5', 'MSK-US-7']);
  });
});

describe('US-1 Scenario: Closed and resolved items stay on the backlog', () => {
  it('shows MSK-US-2 in «Закрыта» and MSK-BUG-4 in «Разрешена» as rows', async () => {
    const data = boardsDataset();
    data.items.push(
      platformItem(2, 'US', 'Вход по паролю', { stateId: stateId('US', 'Закрыта') }),
      platformItem(4, 'BUG', 'Падение при входе', { stateId: stateId('BUG', 'Разрешена') }),
    );
    serveBoards(data);
    renderBoards(backlogAddress());

    const rows = rowsOf(await backlogTable());
    expect(rows.map((row) => [cellsOf(row)[2], cellsOf(row)[7]])).toEqual([
      ['MSK-US-2', 'Закрыта'],
      ['MSK-BUG-4', 'Разрешена'],
    ]);
  });
});

describe('US-1 Scenario: Items nobody has ordered yet are listed by number', () => {
  it('reads MSK-US-3, MSK-BUG-5, MSK-US-9 with «Порядок» 1, 2, 3', async () => {
    serveBoards(storiesDataset(['US-9', 'BUG-5', 'US-3']));
    renderBoards(backlogAddress());

    const table = await backlogTable();
    expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-BUG-5', 'MSK-US-9']);
    expect(orders(table)).toEqual(['1', '2', '3']);
  });
});

describe('US-1 Scenario: A row shows the item at a glance and opens it', () => {
  it('reads «Порядок», the handle, the identifier, «Баг» with its icon, the title link, «Родитель» «Нет» (feat-001 BR-H-13), «Не назначен», the state with its dot and «Ещё действия» (feat-001 BR-H-14)', async () => {
    const data = boardsDataset();
    data.items.push(
      platformItem(4, 'BUG', 'Падение при входе', { stateId: stateId('BUG', 'Активна') }),
    );
    serveBoards(data);
    renderBoards(backlogAddress());

    const table = await backlogTable();
    expect(
      within(table)
        .getAllByRole('columnheader')
        .map((header) => header.textContent),
    ).toEqual(HEADERS);
    const [row] = rowsOf(table);
    if (!row) throw new Error('No row.');
    expect(cellsOf(row)).toEqual([
      '1',
      '',
      'MSK-BUG-4',
      'Баг',
      'Падение при входе',
      'Нет',
      'Не назначен',
      'Активна',
      '',
    ]);
    const handle = within(row).getByRole('button', { name: 'Переместить MSK-BUG-4' });
    expect(handle).toHaveAttribute('aria-haspopup', 'menu');
    expect(handle.querySelector('[data-testid="DragIndicatorIcon"]')).not.toBeNull();
    const cells = within(row).getAllByRole('cell');
    expect(cells[3]?.querySelector('[data-testid="BugReportIcon"]')).not.toBeNull();
    expect(cells[7]?.querySelector(`.${CATEGORY_MARKER_CLASS}`)).not.toBeNull();

    await userEvent.click(within(row).getByRole('link', { name: 'Падение при входе' }));

    expect(screen.getByTestId('location')).toHaveTextContent(mskForm('MSK-BUG-4'));
  });

  it('shows an assignee as initials and name, and «Неизвестный пользователь» for one the directory lacks', async () => {
    const data = boardsDataset();
    data.items.push(
      platformItem(2, 'US', 'Вход по паролю', { assigneeId: ids.anna }),
      platformItem(3, 'US', 'Выход', { assigneeId: ids.ghost }),
    );
    serveBoards(data);
    renderBoards(backlogAddress());

    const rows = rowsOf(await backlogTable());
    expect(rows.map((row) => cellsOf(row)[6])).toEqual([
      'АСАнна Смирнова',
      'Неизвестный пользователь',
    ]);
  });
});

describe('US-1 Scenario: The level is chosen by the level drop-down and kept in the address (feat-001 US-6)', () => {
  it('«Бэклоги» in the main navigation opens the team’s «Истории», and each level is its own address', async () => {
    serveBoards(storiesDataset());
    renderBoards(mskForm('MSK-US-3'));

    const navigation = screen.getByRole('navigation', { name: 'Основное меню' });
    const entry = within(navigation).getByRole('link', { name: 'Бэклоги' });
    expect(entry).toHaveAttribute('href', backlogAddress('stories'));
    await userEvent.click(entry);

    expect(screen.getByTestId('location')).toHaveTextContent(backlogAddress('stories'));
    expect(screen.getByRole('heading', { level: 1, name: 'Бэклоги' })).toBeInTheDocument();
    await waitFor(() => expect(document.title).toBe('Бэклоги — Mustask'));
    expect(screen.queryAllByRole('tab')).toEqual([]);
    expect(levelButton()).toHaveAccessibleName('Уровень: Истории');
    await userEvent.click(levelButton());
    const menu = await levelMenu();
    const levels = within(menu).getAllByRole('menuitem');
    expect(levels.map((element) => element.textContent)).toEqual(['Эпики', 'Фичи', 'Истории']);
    expect(levels.map((element) => element.getAttribute('href'))).toEqual([
      backlogAddress('epics'),
      backlogAddress('features'),
      backlogAddress('stories'),
    ]);
    await userEvent.keyboard('{Escape}');
    expect(entry).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).toBeNull();

    // Another level is the entry's page too: still current, still no trail.
    await chooseLevel('Фичи');
    expect(screen.getByTestId('location')).toHaveTextContent(backlogAddress('features'));
    expect(levelButton()).toHaveAccessibleName('Уровень: Фичи');
    expect(within(navigation).getByRole('link', { name: 'Бэклоги' })).toHaveAttribute(
      'aria-current',
      'page',
    );
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).toBeNull();
  });

  it('`<team>/backlogs/tasks` is «Страница не найдена», and nothing is read', async () => {
    const api = serveBoards(storiesDataset());
    renderBoards(backlogAddress('tasks'));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
    ).toBeInTheDocument();
    expect(api.requests).toEqual([]);
  });
});

describe('US-1 Scenario: An empty backlog shows the empty frame, header and level drop-down kept', () => {
  it('shows «Бэклог пуст» with its hint and «Новый рабочий элемент» on «Аналитика»’s «Эпики»', async () => {
    serveBoards(storiesDataset());
    renderBoards(backlogAddress('epics', ids.analyticsTeam));

    expect(await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' })).toBeVisible();
    expect(
      screen.getByText('Добавьте рабочий элемент — он встанет в конец бэклога.'),
    ).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Новый рабочий элемент' })).toHaveLength(2);
    expect(screen.getByRole('heading', { level: 1, name: 'Бэклоги' })).toBeInTheDocument();
    expect(levelButton()).toHaveAccessibleName('Уровень: Эпики');
    expect(screen.queryByRole('table', { name: 'Бэклог' })).not.toBeInTheDocument();
  });
});

describe('US-1 Scenario: A failed read is retried in place, header and level drop-down kept', () => {
  it('shows the server’s text with «Повторить» in place of the table, and the rows after a retry', async () => {
    const api = serveBoards(storiesDataset());
    server.use(
      http.get(
        BACKLOG_URL,
        () =>
          HttpResponse.json(
            { status: 500, title: 'Ошибка', detail: 'Сервис не ответил.' },
            { status: 500 },
          ),
        { once: true },
      ),
    );
    renderBoards(backlogAddress());

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Сервис не ответил.');
    expect(screen.getByRole('heading', { level: 1, name: 'Бэклоги' })).toBeInTheDocument();
    expect(levelButton()).toHaveAccessibleName('Уровень: Истории');
    expect(screen.queryByRole('table', { name: 'Бэклог' })).not.toBeInTheDocument();

    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));

    expect(identifiers(await backlogTable())).toEqual([
      'MSK-US-3',
      'MSK-BUG-5',
      'MSK-US-9',
      'MSK-US-11',
    ]);
    expect(api.reads('/backlogs/stories')).toHaveLength(1);
  });

  it('shows `boards.level.not_found` — a process without the level — as the banner, not a frame (api-contract § 4)', async () => {
    serveBoards(storiesDataset());
    server.use(
      http.get(BACKLOG_URL, () =>
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
    renderBoards(backlogAddress('epics'));

    expect(await screen.findByRole('alert')).toHaveTextContent('Уровень бэклога не найден.');
    expect(screen.getByRole('heading', { level: 1, name: 'Бэклоги' })).toBeInTheDocument();
  });
});

describe('spec § Page «Бэклоги», State Loading', () => {
  it('keeps the header and the level drop-down and shows the skeleton under the real headers inside «Загрузка данных»', async () => {
    serveBoards(storiesDataset());
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(BACKLOG_URL, async () => {
        await held;
        await delay(0);
        return undefined;
      }),
    );
    renderBoards(backlogAddress());

    const skeleton = await screen.findByRole('status', { name: 'Загрузка данных' });
    expect(
      within(skeleton)
        .getAllByRole('columnheader')
        .map((header) => header.textContent),
    ).toEqual(['Порядок', '', 'ID', 'Тип', 'Заголовок', 'Родитель', 'Исполнитель', 'Статус', '']);
    expect(screen.getByRole('heading', { level: 1, name: 'Бэклоги' })).toBeInTheDocument();
    expect(levelButton()).toHaveAccessibleName('Уровень: Истории');
    // The primary waits for the read: the level's types come with it.
    expect(screen.queryByRole('button', { name: 'Новый рабочий элемент' })).toBeNull();

    release();
    expect(await backlogTable()).toBeInTheDocument();
    expect(screen.queryByRole('status', { name: 'Загрузка данных' })).toBeNull();
  });
});

describe('US-1 Scenario Outline: An address whose context is not found', () => {
  it.each([
    ['team id is malformed', 'Команда не найдена', teamPrefix('not-a-uuid')],
    ['team has been deleted', 'Команда не найдена', teamPrefix(ids.analyticsTeam)],
    ['team belongs to another project', 'Команда не найдена', teamPrefix(ids.opsTeam)],
    [
      'project has been deactivated',
      'Проект не найден',
      `/organizations/${ids.example}/projects/${ids.ops}/teams/${ids.opsTeam}`,
    ],
    [
      'organization id names no organization',
      'Организация не найдена',
      `/organizations/e0000000-0000-4000-8000-0000000000ff/projects/${ids.msk}/teams/${ids.platformTeam}`,
    ],
  ])('a backlog address whose %s shows «%s»', async (_part, frame, prefix) => {
    const data = storiesDataset();
    data.teams = data.teams.map((team) =>
      team.id === ids.analyticsTeam ? { ...team, active: false } : team,
    );
    data.projects = data.projects.map((project) =>
      project.id === ids.ops ? { ...project, active: false } : project,
    );
    serveBoards(data);
    renderBoards(`${prefix}/backlogs/stories`);

    expect(await screen.findByRole('heading', { level: 1, name: frame })).toBeInTheDocument();
    expect(screen.getByText('Проверьте адрес или вернитесь на главную.')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1, name: 'Бэклоги' })).toBeNull();
  });
});
