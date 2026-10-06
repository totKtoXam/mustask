import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, delay, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { CATEGORY_MARKER_CLASS } from '../../../shared/ui/WorkTrackingMarks';
import { serveBoards } from '../testApi';
import { cardOf, cardsIn, columnList, columnNames, findBoard } from '../testBoard';
import {
  BOARD_URL,
  boardsDataset,
  ids,
  item,
  platformItem,
  stateId,
  storiesDataset,
} from '../testFixtures';
import { boardAddress, backlogAddress, mskForm, renderBoards, teamPrefix } from '../testRender';

// US-4 and US-7's board scenario: «Платформа»'s boards per level.

afterEach(() => {
  focusManager.setFocused(undefined);
});

function tab(name: string) {
  return screen.getByRole('tab', { name });
}

describe('US-4 Scenario Outline: Each level’s board has its own columns', () => {
  it.each([
    ['epics', 'EPIC', ['Новые', 'В работе', 'Разрешена', 'Завершённые']],
    ['features', 'FEAT', ['Новые', 'В работе', 'Разрешена', 'Завершённые']],
    ['stories', 'US', ['Новые', 'В работе', 'Разрешена', 'Решённые', 'Завершённые']],
  ])(
    'the «%s» board of «Платформа» has its columns in order, each header with its count',
    async (level, typeKey, columns) => {
      const data = boardsDataset();
      data.items.push(platformItem(1, typeKey, 'Первый'));
      serveBoards(data);
      renderBoards(boardAddress(level));

      const board = await findBoard();
      expect(
        within(board)
          .getAllByRole('heading', { level: 2 })
          .map((heading) => heading.textContent),
      ).toEqual(columns);
      expect(columnNames(board)).toEqual(
        columns.map((name, index) => `${name}, рабочих элементов: ${index === 0 ? 1 : 0}`),
      );
      for (const name of columns) {
        const box = columnList(board, name).closest('[data-column]') as HTMLElement;
        expect(within(box).getByText(name === 'Новые' ? '1' : '0')).toBeInTheDocument();
      }
    },
  );
});

describe('US-4 Scenario Outline: A card sits in the column its type and state map to', () => {
  it.each([
    ['stories', 'US', 'Новая', 'Новые'],
    ['stories', 'BUG', 'Активна', 'В работе'],
    ['stories', 'US', 'Разрешена', 'Разрешена'],
    ['stories', 'BUG', 'Разрешена', 'Решённые'],
    ['stories', 'BUG', 'Закрыта', 'Завершённые'],
    ['epics', 'EPIC', 'Разрешена', 'Разрешена'],
  ])('on «%s», a %s in «%s» is in «%s»', async (level, typeKey, state, column) => {
    const data = boardsDataset();
    data.items.push(platformItem(7, typeKey, 'Карточка', { stateId: stateId(typeKey, state) }));
    serveBoards(data);
    renderBoards(boardAddress(level));

    const board = await findBoard();
    expect(cardsIn(board, column)).toEqual([`MSK-${typeKey}-7`]);
  });
});

describe('US-4 Scenario: The board holds the backlog’s items in the backlog’s order', () => {
  it('«Новые» shows MSK-US-3, MSK-BUG-5, MSK-US-9, count 3, no MSK-US-5; after MSK-US-9 is moved to the top it is first', async () => {
    const data = storiesDataset(['US-3', 'BUG-5', 'US-9']);
    data.items.push(item(ids.msk, 5, 'US', 'Чужая', { areaId: ids.mskRoot }));
    const api = serveBoards(data);
    renderBoards(boardAddress());

    const board = await findBoard();
    expect(cardsIn(board, 'Новые')).toEqual(['MSK-US-3', 'MSK-BUG-5', 'MSK-US-9']);
    expect(columnList(board, 'Новые')).toHaveAccessibleName('Новые, рабочих элементов: 3');
    expect(within(board).queryByText('MSK-US-5')).toBeNull();

    api.reorderAsAnotherUser(ids.platformTeam, 'stories', {
      itemId: api.item(ids.msk, 9).id,
      position: 'top',
    });
    act(() => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });
    await waitFor(() =>
      expect(cardsIn(board, 'Новые')).toEqual(['MSK-US-9', 'MSK-US-3', 'MSK-BUG-5']),
    );
  });
});

describe('US-4 Scenario: A card shows the item at a glance and opens it', () => {
  it('shows the «Баг» stripe and icon, MSK-BUG-5, the title link, the dot with «Активна», «АС» with «Анна Смирнова» and «Ещё действия: MSK-BUG-5»', async () => {
    const data = boardsDataset();
    data.items.push(
      platformItem(5, 'BUG', 'Неверный счётчик', {
        stateId: stateId('BUG', 'Активна'),
        assigneeId: ids.anna,
      }),
    );
    serveBoards(data);
    renderBoards(boardAddress());

    const board = await findBoard();
    const card = cardOf(board, 'MSK-BUG-5');
    expect(card.querySelector('[data-testid="BugReportIcon"]')).not.toBeNull();
    expect(getComputedStyle(card).borderLeftWidth).toBe('4px');
    expect(within(card).getByText('MSK-BUG-5')).toBeInTheDocument();
    expect(within(card).getByText('Активна')).toBeInTheDocument();
    expect(card.querySelector(`.${CATEGORY_MARKER_CLASS}`)).not.toBeNull();
    expect(within(card).getByText('АС')).toBeInTheDocument();
    expect(within(card).getByText('Анна Смирнова')).toBeInTheDocument();
    expect(within(card).getByRole('button', { name: 'Ещё действия: MSK-BUG-5' })).toHaveAttribute(
      'aria-haspopup',
      'menu',
    );

    await userEvent.click(within(card).getByRole('link', { name: 'Неверный счётчик' }));

    expect(screen.getByTestId('location')).toHaveTextContent(mskForm('MSK-BUG-5'));
  });
});

describe('US-4 Scenario: A column with no cards stays on the board', () => {
  it('shows all five columns when every story is «Новая», the other four with 0', async () => {
    serveBoards(storiesDataset(['US-3', 'BUG-5']));
    renderBoards(boardAddress());

    expect(columnNames(await findBoard())).toEqual([
      'Новые, рабочих элементов: 2',
      'В работе, рабочих элементов: 0',
      'Разрешена, рабочих элементов: 0',
      'Решённые, рабочих элементов: 0',
      'Завершённые, рабочих элементов: 0',
    ]);
  });
});

describe('US-4 Scenario: A level with no items shows the empty frame', () => {
  it('«Платформа» with no «Эпик»: the frame replaces the columns, header and tabs kept', async () => {
    serveBoards(storiesDataset());
    renderBoards(boardAddress('epics'));

    expect(
      await screen.findByRole('heading', { level: 2, name: 'На доске нет рабочих элементов' }),
    ).toBeVisible();
    expect(
      screen.getByText('Новые рабочие элементы этого уровня появятся в колонке «Новые».'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Новый рабочий элемент' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Доска' })).toBeNull();
    expect(screen.getByRole('heading', { level: 1, name: 'Доски' })).toBeInTheDocument();
    expect(tab('Эпики')).toHaveAttribute('aria-selected', 'true');
  });
});

describe('US-4 Scenario: Entry, tabs and the link to the backlog', () => {
  it('«Доски» opens `<team>/boards/stories`; the tabs; «Фичи» → «Показать бэклог» → its «Показать доску»', async () => {
    serveBoards(storiesDataset());
    renderBoards(mskForm('MSK-US-3'));

    const navigation = screen.getByRole('navigation', { name: 'Основное меню' });
    const entry = within(navigation).getByRole('link', { name: 'Доски' });
    expect(entry).toHaveAttribute('href', boardAddress('stories'));
    await userEvent.click(entry);

    expect(screen.getByTestId('location')).toHaveTextContent(boardAddress('stories'));
    expect(screen.getByRole('heading', { level: 1, name: 'Доски' })).toBeInTheDocument();
    await waitFor(() => expect(document.title).toBe('Доски — Mustask'));
    expect(screen.getAllByRole('tab').map((element) => element.textContent)).toEqual([
      'Эпики',
      'Фичи',
      'Истории',
    ]);
    expect(tab('Истории')).toHaveAttribute('aria-selected', 'true');
    expect(entry).toHaveAttribute('aria-current', 'page');
    // «Доски» has no primary (spec § Page «Доски»).
    expect(screen.queryByRole('button', { name: 'Новый рабочий элемент' })).toBeNull();

    await userEvent.click(tab('Фичи'));
    expect(screen.getByTestId('location')).toHaveTextContent(boardAddress('features'));
    const toBacklog = screen.getByRole('link', { name: 'Показать бэклог' });
    expect(toBacklog.querySelector('[data-testid="FormatListBulletedIcon"]')).not.toBeNull();
    await userEvent.click(toBacklog);
    expect(screen.getByTestId('location')).toHaveTextContent(backlogAddress('features'));
    expect(screen.getByRole('heading', { level: 1, name: 'Бэклоги' })).toBeInTheDocument();

    const toBoard = screen.getByRole('link', { name: 'Показать доску' });
    expect(toBoard.querySelector('[data-testid="ViewWeekOutlinedIcon"]')).not.toBeNull();
    await userEvent.click(toBoard);
    expect(screen.getByTestId('location')).toHaveTextContent(boardAddress('features'));
  });

  it('`<team>/boards/tasks` answers «Страница не найдена», and nothing is read', async () => {
    const api = serveBoards(storiesDataset());
    renderBoards(boardAddress('tasks'));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
    ).toBeInTheDocument();
    expect(api.requests).toEqual([]);
  });
});

describe('US-4 Scenario: Failure and not found are the backlog’s', () => {
  it('a failed read shows the server’s text with «Повторить» in place of the columns, and the columns after a retry', async () => {
    serveBoards(storiesDataset());
    server.use(
      http.get(
        BOARD_URL,
        () =>
          HttpResponse.json(
            { status: 500, title: 'Ошибка', detail: 'Сервис не ответил.' },
            { status: 500 },
          ),
        { once: true },
      ),
    );
    renderBoards(boardAddress());

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Сервис не ответил.');
    expect(screen.getByRole('heading', { level: 1, name: 'Доски' })).toBeInTheDocument();
    expect(screen.getAllByRole('tab')).toHaveLength(3);
    expect(screen.queryByRole('region', { name: 'Доска' })).toBeNull();

    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));

    expect(cardsIn(await findBoard(), 'Новые')).toEqual([
      'MSK-US-3',
      'MSK-BUG-5',
      'MSK-US-9',
      'MSK-US-11',
    ]);
  });

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
  ])('a board address whose %s shows «%s»', async (_part, frame, prefix) => {
    const data = storiesDataset();
    data.teams = data.teams.map((team) =>
      team.id === ids.analyticsTeam ? { ...team, active: false } : team,
    );
    data.projects = data.projects.map((project) =>
      project.id === ids.ops ? { ...project, active: false } : project,
    );
    serveBoards(data);
    renderBoards(`${prefix}/boards/stories`);

    expect(await screen.findByRole('heading', { level: 1, name: frame })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1, name: 'Доски' })).toBeNull();
  });
});

describe('spec § Page «Доски», State Loading', () => {
  it('keeps the header and tabs and shows column skeletons inside «Загрузка данных»', async () => {
    serveBoards(storiesDataset());
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(BOARD_URL, async () => {
        await held;
        await delay(0);
        return undefined;
      }),
    );
    renderBoards(boardAddress());

    expect(await screen.findByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Доски' })).toBeInTheDocument();
    expect(screen.getAllByRole('tab')).toHaveLength(3);
    expect(screen.queryByRole('region', { name: 'Доска' })).toBeNull();

    release();
    expect(await findBoard()).toBeInTheDocument();
    expect(screen.queryByRole('status', { name: 'Загрузка данных' })).toBeNull();
  });
});

describe('US-7 Scenario: A state set on the form moves the card (pass B)', () => {
  it('MSK-US-9 set to «Разрешена» on its form is in «Разрешена» at the next read, and its row keeps «Порядок» 1', async () => {
    const data = storiesDataset(['US-9', 'US-3']);
    const api = serveBoards(data);
    const view = renderBoards(boardAddress());
    const board = await findBoard();
    expect(cardsIn(board, 'Новые')).toEqual(['MSK-US-3', 'MSK-US-9']);
    api.reorderAsAnotherUser(ids.platformTeam, 'stories', {
      itemId: api.item(ids.msk, 9).id,
      position: 'top',
    });

    api.item(ids.msk, 9).stateId = stateId('US', 'Разрешена');
    act(() => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });

    await waitFor(() => expect(cardsIn(board, 'Разрешена')).toEqual(['MSK-US-9']));
    expect(screen.queryByRole('alert')).toBeNull();

    view.unmount();
    renderBoards(backlogAddress());
    const table = await screen.findByRole('table', { name: 'Бэклог' });
    const [first] = within(table).getAllByRole('row').slice(1);
    if (!first) throw new Error('No row.');
    const cells = within(first)
      .getAllByRole('cell')
      .map((cell) => cell.textContent);
    expect([cells[0], cells[2], cells[6]]).toEqual(['1', 'MSK-US-9', 'Разрешена']);
  });
});
