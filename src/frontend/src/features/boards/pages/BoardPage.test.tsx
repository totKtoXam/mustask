import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, delay, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { CATEGORY_MARKER_CLASS } from '../../../shared/ui/WorkTrackingMarks';
import { serveBoards } from '../testApi';
import { CELL, cellText, findBacklogTable, rowsOf } from '../testBacklog';
import { cardOf, cardsIn, columnList, columnNames, findBoard } from '../testBoard';
import { chooseLevel, levelButton } from '../testLevels';
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

describe('US-4 Scenario Outline: Each level’s board has its own columns', () => {
  it.each([
    ['epics', 'EPIC', ['Новая', 'Активна', 'Разрешена', 'Закрыта']],
    ['features', 'FEAT', ['Новая', 'Активна', 'Разрешена', 'Закрыта']],
    ['stories', 'US', ['Новая', 'Активна', 'Разрешена', 'Закрыта']],
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
        expect(within(box).getByText(name === 'Новая' ? '1' : '0')).toBeInTheDocument();
      }
    },
  );
});

describe('US-4 Scenario Outline: A card sits in the column its type and state map to', () => {
  it.each([
    ['stories', 'US', 'Новая', 'Новая'],
    ['stories', 'BUG', 'Активна', 'Активна'],
    ['stories', 'US', 'Разрешена', 'Разрешена'],
    ['stories', 'BUG', 'Разрешена', 'Разрешена'],
    ['stories', 'BUG', 'Закрыта', 'Закрыта'],
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

describe('bug-004 D-1 Scenario: A story in «Разрешена» and a bug in «Разрешена» stand in one column', () => {
  it('shows MSK-US-3 and MSK-BUG-5 in the one column «Разрешена» with the same dot, and no «Решённые» or «В работе» header', async () => {
    const data = boardsDataset();
    data.items.push(
      platformItem(3, 'US', 'История', { stateId: stateId('US', 'Разрешена') }),
      platformItem(4, 'US', 'Другая', { stateId: stateId('US', 'Активна') }),
      platformItem(5, 'BUG', 'Баг', { stateId: stateId('BUG', 'Разрешена') }),
    );
    serveBoards(data);
    renderBoards(boardAddress());

    const board = await findBoard();
    expect(cardsIn(board, 'Разрешена')).toEqual(['MSK-US-3', 'MSK-BUG-5']);
    expect(cardsIn(board, 'Активна')).toEqual(['MSK-US-4']);
    expect(
      within(board)
        .getAllByRole('heading', { level: 2 })
        .map((heading) => heading.textContent),
    ).toEqual(['Новая', 'Активна', 'Разрешена', 'Закрыта']);

    // The state's dot follows its category, which is one for «Разрешена» on both types.
    const dotOf = (identifier: string) =>
      getComputedStyle(
        cardOf(board, identifier).querySelector(`.${CATEGORY_MARKER_CLASS}`) as HTMLElement,
      ).backgroundColor;
    expect(dotOf('MSK-US-3')).toBe(dotOf('MSK-BUG-5'));
    expect(dotOf('MSK-US-3')).not.toBe(dotOf('MSK-US-4'));
  });
});

describe('US-4 Scenario: The board holds the backlog’s items in the backlog’s order', () => {
  it('«Новая» shows MSK-US-3, MSK-BUG-5, MSK-US-9, count 3, no MSK-US-5; after MSK-US-9 is moved to the top it is first', async () => {
    const data = storiesDataset(['US-3', 'BUG-5', 'US-9']);
    data.items.push(item(ids.msk, 5, 'US', 'Чужая', { areaId: ids.mskRoot }));
    const api = serveBoards(data);
    renderBoards(boardAddress());

    const board = await findBoard();
    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-3', 'MSK-BUG-5', 'MSK-US-9']);
    expect(columnList(board, 'Новая')).toHaveAccessibleName('Новая, рабочих элементов: 3');
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
      expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-9', 'MSK-US-3', 'MSK-BUG-5']),
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

    // A plain click opens the card as a dialog (BoardPage.card.test.tsx); the address stays the
    // item's own page, for a new tab (feat-029 BR-C-1).
    expect(within(card).getByRole('link', { name: 'Неверный счётчик' })).toHaveAttribute(
      'href',
      mskForm('MSK-BUG-5'),
    );
  });
});

describe('US-4 Scenario: A column with no cards stays on the board', () => {
  it('shows all four columns when every story is «Новая», the other three with 0', async () => {
    serveBoards(storiesDataset(['US-3', 'BUG-5']));
    renderBoards(boardAddress());

    expect(columnNames(await findBoard())).toEqual([
      'Новая, рабочих элементов: 2',
      'Активна, рабочих элементов: 0',
      'Разрешена, рабочих элементов: 0',
      'Закрыта, рабочих элементов: 0',
    ]);
  });
});

describe('US-4 Scenario: A level with no items shows the empty frame', () => {
  it('«Платформа» with no «Эпик»: «Канбан пуст» replaces the columns, header and drop-down kept (feat-001 US-6)', async () => {
    serveBoards(storiesDataset());
    renderBoards(boardAddress('epics'));

    expect(await screen.findByRole('heading', { level: 2, name: 'Канбан пуст' })).toBeVisible();
    expect(
      screen.getByText('Новые рабочие элементы этого уровня появятся в колонке «Новая».'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Новый рабочий элемент' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Канбан' })).toBeNull();
    expect(screen.getByRole('heading', { level: 1, name: 'Платформа' })).toBeInTheDocument();
    expect(levelButton()).toHaveAccessibleName('Уровень: Эпики');
  });
});

describe('US-4 Scenario: Entry, levels and the link to the backlog (feat-001 US-6: «Канбан», the drop-down)', () => {
  it('«Канбан» opens `<team>/boards/stories`; the drop-down; «Фичи» → «Показать бэклог» → its «Показать канбан»', async () => {
    serveBoards(storiesDataset());
    renderBoards(mskForm('MSK-US-3'));

    const navigation = screen.getByRole('navigation', { name: 'Основное меню' });
    const entry = within(navigation).getByRole('link', { name: 'Канбан' });
    expect(entry).toHaveAttribute('href', boardAddress('stories'));
    await userEvent.click(entry);

    expect(screen.getByTestId('location')).toHaveTextContent(boardAddress('stories'));
    expect(screen.getByRole('heading', { level: 1, name: 'Платформа' })).toBeInTheDocument();
    await waitFor(() => expect(document.title).toBe('Канбан — Mustask'));
    expect(screen.getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Канбан']);
    expect(levelButton()).toHaveAccessibleName('Уровень: Истории');
    expect(entry).toHaveAttribute('aria-current', 'page');
    // «Канбан» has no primary (spec § Page «Доски»).
    expect(screen.queryByRole('button', { name: 'Новый рабочий элемент' })).toBeNull();

    await chooseLevel('Фичи');
    expect(screen.getByTestId('location')).toHaveTextContent(boardAddress('features'));
    const toBacklog = screen.getByRole('link', { name: 'Показать бэклог' });
    expect(toBacklog.querySelector('[data-testid="ArrowCircleLeftOutlinedIcon"]')).not.toBeNull();
    await userEvent.click(toBacklog);
    expect(screen.getByTestId('location')).toHaveTextContent(backlogAddress('features'));
    expect(screen.getByRole('heading', { level: 1, name: 'Платформа' })).toBeInTheDocument();

    const toBoard = screen.getByRole('link', { name: 'Показать канбан' });
    expect(toBoard.querySelector('[data-testid="ArrowCircleRightOutlinedIcon"]')).not.toBeNull();
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
    expect(screen.getByRole('heading', { level: 1, name: 'Платформа' })).toBeInTheDocument();
    expect(levelButton()).toHaveAccessibleName('Уровень: Истории');
    expect(screen.queryByRole('region', { name: 'Канбан' })).toBeNull();

    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));

    expect(cardsIn(await findBoard(), 'Новая')).toEqual([
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
    // The frame replaces the whole page (design § 3.6): no head beside it — the frame's own
    // is the page's one `h1`, with no tab row and no level drop-down (F-6).
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(screen.queryByRole('tablist', { name: 'Вид страницы' })).toBeNull();
    expect(screen.queryByRole('button', { name: /^Уровень: / })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Сменить команду' })).toBeNull();
    expect(screen.queryByRole('link', { name: 'Показать бэклог' })).toBeNull();
  });
});

describe('spec § Page «Доски», State Loading', () => {
  it('keeps the header and the level drop-down and shows column skeletons inside «Загрузка данных»', async () => {
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
    expect(screen.getByRole('heading', { level: 1, name: 'Платформа' })).toBeInTheDocument();
    expect(levelButton()).toHaveAccessibleName('Уровень: Истории');
    expect(screen.queryByRole('region', { name: 'Канбан' })).toBeNull();

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
    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-3', 'MSK-US-9']);
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
    const [first] = rowsOf(await findBacklogTable());
    if (!first) throw new Error('No row.');
    expect([
      cellText(first, CELL.order),
      first.dataset.identifier,
      cellText(first, CELL.state),
    ]).toEqual(['1', 'MSK-US-9', 'Разрешена']);
  });
});
