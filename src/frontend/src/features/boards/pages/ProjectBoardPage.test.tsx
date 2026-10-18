import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, delay, http } from 'msw';
import { theme } from '../../../app/theme';
import { server } from '../../../test/mocks/server';
import { WORK_ITEM_URL } from '../../workitems/testFixtures';
import { serveBoards, type BoardsTestApi } from '../testApi';
import { cardMenuButton, cardOf, cardsIn, columnBox, columnNames, findBoard } from '../testBoard';
import { announcer, checkboxOf, counterOf, listOf } from '../testChecklist';
import { dataTransfer, fireDrag } from '../testDrag';
import { levelButton } from '../testLevels';
import {
  PROJECT_BOARD_URL,
  addStoryOnlyReviewState,
  ids,
  item,
  platformItem,
  projectDataset,
  stateId,
} from '../testFixtures';
import {
  boardAddress,
  projectBacklogAddress,
  projectBoardAddress,
  projectPrefix,
  renderBoards,
} from '../testRender';

// feat-039 US-3: the project's board (BR-PB-1, -2, -4..-6, -9; mod-006 BR-M4-2, -3, -15,
// -18): the team board's columns over every item of the project, in the project's order.
// US-1's data: MSK-US-2 (the first team's area), MSK-US-3 («Платформа»), MSK-US-9
// («Аналитика»), all «Новая» unless a test says otherwise; OPS-US-1 is another project's.

afterEach(() => {
  focusManager.setFocused(undefined);
});

const FAILED_TEXT = 'Не удалось сохранить перемещение. Попробуйте ещё раз.';

/** US-3's data: MSK-US-2 and MSK-US-3 «Новая», MSK-US-9 «Активна». */
function activeDataset() {
  const data = projectDataset();
  const us9 = data.items.find((record) => record.projectId === ids.msk && record.number === 9);
  if (!us9) throw new Error('No MSK-US-9.');
  us9.stateId = stateId('US', 'Активна');
  return data;
}

function patches(api: BoardsTestApi) {
  return api.writes().filter((request) => request.method === 'PATCH');
}

/** Drags the card onto the column by the browser's own drag and drop. */
async function dragCard(board: HTMLElement, identifier: string, header: string) {
  const transfer = dataTransfer();
  const card = cardOf(board, identifier);
  const target = columnBox(board, header);
  await act(async () => fireDrag('dragStart', card, transfer));
  await act(async () => fireDrag('dragOver', target, transfer));
  await act(async () => fireDrag('drop', target, transfer));
  await act(async () => fireDrag('dragEnd', card, transfer));
}

describe('US-3 Scenario: The project’s board holds the backlog’s items in the process’s columns (BR-MX-17, BR-MX-19)', () => {
  it('names the lists «Новая, рабочих элементов: 2», «Активна…: 1», «Разрешена…: 0», «Закрыта…: 0»; «Новая» holds MSK-US-2 and MSK-US-3 of two teams, «Активна» MSK-US-9, and no card of another project', async () => {
    const api = serveBoards(activeDataset());
    renderBoards(projectBoardAddress());

    const board = await findBoard();

    expect(columnNames(board)).toEqual([
      'Новая, рабочих элементов: 2',
      'Активна, рабочих элементов: 1',
      'Разрешена, рабочих элементов: 0',
      'Закрыта, рабочих элементов: 0',
    ]);
    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-2', 'MSK-US-3']);
    expect(cardsIn(board, 'Активна')).toEqual(['MSK-US-9']);
    expect(board).not.toHaveTextContent('OPS-US-1');
    expect(api.reads('/boards/stories').map((request) => request.path)).toEqual([
      `/api/organizations/${ids.example}/projects/${ids.msk}/boards/stories`,
    ]);
  });

  it('the cards of «Платформа»’s board are a part of them: MSK-US-3 alone', async () => {
    serveBoards(activeDataset());
    renderBoards(boardAddress());

    const board = await findBoard();

    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-3']);
    expect(cardsIn(board, 'Активна')).toEqual([]);
  });
});

// mod-017 lifts feat-039 BR-PB-6 (design § 6.1): the board has no primary, as a team's has none, and its
// first column draws «Новый элемент» for a writer. Rewritten, same scenario, over the new rule.
describe('US-3 Scenario: The head of the project’s board (BR-PB-7; BR-PB-6 lifted)', () => {
  it('reads the project’s name as the h1, the only tab «Канбан», «Уровень: Истории», «Показать бэклог» to the project’s backlog, no primary command and «Новый элемент» in the first column', async () => {
    const data = activeDataset();
    const project = data.projects.find((candidate) => candidate.id === ids.msk);
    if (!project) throw new Error('No project.');
    project.name = 'Альфа';
    serveBoards(data);
    renderBoards(projectBoardAddress());
    const board = await findBoard();

    expect(await screen.findByRole('heading', { level: 1, name: 'Альфа' })).toBeInTheDocument();
    await waitFor(() => expect(document.title).toBe('Канбан — Mustask'));
    const tabs = screen.getByRole('tablist', { name: 'Вид страницы' });
    expect(within(tabs).getAllByRole('tab')).toHaveLength(1);
    expect(within(tabs).getByRole('tab', { name: 'Канбан' })).toHaveAttribute(
      'href',
      projectBoardAddress('stories'),
    );
    expect(levelButton()).toHaveAccessibleName('Уровень: Истории');
    const toBacklog = screen.getByRole('link', { name: 'Показать бэклог' });
    expect(toBacklog).toHaveAttribute('href', projectBacklogAddress('stories'));
    expect(toBacklog.querySelector('[data-testid="ArrowCircleLeftOutlinedIcon"]')).not.toBeNull();
    expect(getComputedStyle(toBacklog).getPropertyValue('--variant-containedBg')).toBe(
      theme.palette.shell.buttonNeutral,
    );
    expect(screen.queryByRole('button', { name: 'Новый рабочий элемент' })).toBeNull();
    expect(
      within(columnBox(board, 'Новая')).getByRole('button', { name: 'Новый элемент' }),
    ).toBeInTheDocument();
  });

  it('the first column draws «Новый элемент» for a member as for an administrator, and none for a reader', async () => {
    serveBoards(activeDataset());
    renderBoards(projectBoardAddress(), { role: 'member' });
    const board = await findBoard();

    expect(within(board).getAllByRole('button', { name: 'Новый элемент' })).toHaveLength(1);
    expect(within(columnBox(board, 'Новая')).getByRole('heading', { level: 2 })).toHaveTextContent(
      'Новая',
    );
    // A member’s cards keep their own controls.
    expect(cardMenuButton('MSK-US-2')).toBeInTheDocument();
    expect(cardOf(board, 'MSK-US-2')).toHaveAttribute('draggable', 'true');
  });
});

describe('US-3 Scenario: The shell on a project’s board (BR-PB-9)', () => {
  it('«Канбан» is current with no trail, «Бэклоги» is not', async () => {
    serveBoards(activeDataset());
    renderBoards(projectBoardAddress('features'));
    await screen.findByRole('heading', { level: 1, name: 'Мастаск' });

    const navigation = screen.getByRole('navigation', { name: 'Основное меню' });
    const boards = within(navigation).getByRole('link', { name: 'Канбан' });
    expect(boards).toHaveAttribute('aria-current', 'page');
    expect(boards).toHaveAttribute('href', boardAddress('stories'));
    expect(within(navigation).getByRole('link', { name: 'Бэклоги' })).not.toHaveAttribute(
      'aria-current',
    );
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).toBeNull();
  });
});

describe('US-3 Scenario: A column lists its cards in the project’s order (mod-006 BR-M4-15, BR-MX-18)', () => {
  it('«Новая» lists MSK-US-3 before MSK-US-2 once the project’s backlog has put it first; a team’s order is not the project’s', async () => {
    const data = activeDataset();
    const us3 = data.items.find((record) => record.number === 3);
    if (!us3) throw new Error('No MSK-US-3.');
    data.projectPlaces.push({ itemId: us3.id, level: 'stories', position: 1 });
    serveBoards(data);
    renderBoards(projectBoardAddress());

    const board = await findBoard();

    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-3', 'MSK-US-2']);
  });
});

describe('US-3 Scenario: A card move writes only the item’s state, and the project’s order is not touched (BR-MX-16, BR-MX-18, mod-006 BR-M4-3)', () => {
  it('«Переместить в колонку» → «Активна» puts MSK-US-2 there at once, focus on its «Ещё действия», announced; the write is the item’s state alone', async () => {
    const api = serveBoards(activeDataset());
    renderBoards(projectBoardAddress());
    const board = await findBoard();
    const before = { ...api.item(ids.msk, 2) };

    await userEvent.click(cardMenuButton('MSK-US-2'));
    const menu = await screen.findByRole('menu', { name: 'Переместить в колонку' });
    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((entry) => entry.textContent),
    ).toEqual(['Активна', 'Разрешена', 'Закрыта']);
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Активна' }));

    expect(cardsIn(board, 'Активна')).toEqual(['MSK-US-2', 'MSK-US-9']);
    expect(cardMenuButton('MSK-US-2')).toHaveFocus();
    expect(announcer()).toHaveTextContent('MSK-US-2 перемещён в колонку «Активна».');
    await waitFor(() => expect(cardMenuButton('MSK-US-2')).not.toHaveAttribute('aria-disabled'));
    const after = api.item(ids.msk, 2);
    expect(after.stateId).toBe(stateId('US', 'Активна'));
    expect([after.title, after.areaId, after.projectId]).toEqual([
      before.title,
      before.areaId,
      before.projectId,
    ]);
    expect(patches(api).map((request) => [request.path, request.body])).toEqual([
      [
        `/api/organizations/${ids.example}/projects/${ids.msk}/workitems/MSK-US-2`,
        { stateId: stateId('US', 'Активна') },
      ],
    ]);
    expect(api.data.places).toEqual([]);
    expect(api.data.projectPlaces).toEqual([]);
    // The project’s board is read again after the write, and shows the card once.
    expect(api.reads('/boards/stories').length).toBeGreaterThanOrEqual(2);
    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-3']);
  });

  it('a drag of the card onto «Активна» does the same', async () => {
    const api = serveBoards(activeDataset());
    renderBoards(projectBoardAddress());
    const board = await findBoard();

    await dragCard(board, 'MSK-US-3', 'Активна');

    expect(cardsIn(board, 'Активна')).toEqual(['MSK-US-3', 'MSK-US-9']);
    await waitFor(() => expect(cardMenuButton('MSK-US-3')).not.toHaveAttribute('aria-disabled'));
    expect(api.item(ids.msk, 3).stateId).toBe(stateId('US', 'Активна'));
  });

  it('a story-only «Проверка»: it is no drop target for a bug and not in the bug’s menu (BR-M4-18), and the story may go there', async () => {
    const review = addStoryOnlyReviewState();
    try {
      const data = projectDataset();
      data.items.push(platformItem(5, 'BUG', 'Неверный счётчик'));
      const api = serveBoards(data);
      renderBoards(projectBoardAddress());
      const board = await findBoard();

      const transfer = dataTransfer();
      await act(async () => fireDrag('dragStart', cardOf(board, 'MSK-BUG-5'), transfer));
      expect(columnBox(board, 'Проверка')).not.toHaveAttribute('data-drop-target');
      expect(columnBox(board, 'Активна')).toHaveAttribute('data-drop-target', 'true');
      await act(async () => fireDrag('dragEnd', cardOf(board, 'MSK-BUG-5'), transfer));

      await userEvent.click(cardMenuButton('MSK-BUG-5'));
      const bugMenu = await screen.findByRole('menu', { name: 'Переместить в колонку' });
      expect(
        within(bugMenu)
          .getAllByRole('menuitem')
          .map((entry) => entry.textContent),
      ).toEqual(['Активна', 'Разрешена', 'Закрыта']);
      await userEvent.keyboard('{Escape}');
      await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());

      await dragCard(board, 'MSK-US-2', 'Проверка');
      await waitFor(() => expect(cardMenuButton('MSK-US-2')).not.toHaveAttribute('aria-disabled'));
      expect(api.item(ids.msk, 2).stateId).toBe(review.stateId);
      expect(cardsIn(board, 'Проверка')).toEqual(['MSK-US-2']);
    } finally {
      review.restore();
    }
  });

  it('with no answer the card returns to its column and «Не удалось сохранить перемещение. Попробуйте ещё раз.» shows', async () => {
    serveBoards(activeDataset());
    server.use(http.patch(WORK_ITEM_URL, () => HttpResponse.error()));
    renderBoards(projectBoardAddress());
    const board = await findBoard();

    await userEvent.click(cardMenuButton('MSK-US-2'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Активна' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(FAILED_TEXT);
    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-2', 'MSK-US-3']);
    expect(cardsIn(board, 'Активна')).toEqual(['MSK-US-9']);
  });
});

describe('US-3 Scenario: A card’s checklist is the team board’s (BR-PB-6, feat-032)', () => {
  /** MSK-US-2 with a task of «Платформа» and a task of «Аналитика», both «Новая». */
  function checklistDataset() {
    const data = projectDataset();
    const us2 = data.items.find((record) => record.number === 2);
    if (!us2) throw new Error('No MSK-US-2.');
    data.items.push(
      platformItem(4, 'TASK', 'be', { parentId: us2.id }),
      item(ids.msk, 5, 'TASK', 'fe', { areaId: ids.analyticsArea, parentId: us2.id }),
    );
    return data;
  }

  it('counts the tasks of every team: «0/2»; ticking the task of the other team closes it and changes nothing else', async () => {
    const api = serveBoards(checklistDataset());
    renderBoards(projectBoardAddress());
    const board = await findBoard();
    await waitFor(() => expect(counterOf(board, 'MSK-US-2', 'task')).toHaveTextContent('0/2'));
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    const before = { ...api.item(ids.msk, 4) };

    await userEvent.click(checkboxOf(listOf(board, 'MSK-US-2', 'Задача'), 'MSK-TASK-5'));

    await waitFor(() => expect(counterOf(board, 'MSK-US-2', 'task')).toHaveTextContent('1/2'));
    await waitFor(() =>
      expect(checkboxOf(listOf(board, 'MSK-US-2', 'Задача'), 'MSK-TASK-5')).not.toHaveAttribute(
        'aria-disabled',
      ),
    );
    expect(api.item(ids.msk, 5).stateId).toBe(stateId('TASK', 'Закрыта'));
    expect(api.item(ids.msk, 4)).toEqual(before);
    expect(api.data.places).toEqual([]);
    expect(api.data.projectPlaces).toEqual([]);
  });

  it('«Добавить задачу» creates a task under the card’s item with no team, in the parent’s area, and writes no place', async () => {
    const api = serveBoards(checklistDataset());
    renderBoards(projectBoardAddress());
    const board = await findBoard();
    await waitFor(() => expect(counterOf(board, 'MSK-US-2', 'task')).toHaveTextContent('0/2'));
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));

    await userEvent.click(
      within(cardOf(board, 'MSK-US-2')).getByRole('button', {
        name: 'Добавить задачу (MSK-US-2)',
      }),
    );
    await userEvent.type(
      within(cardOf(board, 'MSK-US-2')).getByRole('textbox', {
        name: 'Заголовок: Задача (MSK-US-2)',
      }),
      'Проверить ссылку{Enter}',
    );

    expect(await screen.findByText('Рабочий элемент MSK-TASK-10 создан.')).toBeInTheDocument();
    const create = api.writes().find((request) => request.path.endsWith('/workitems'));
    expect(create?.body).toMatchObject({ parentId: api.item(ids.msk, 2).id });
    expect(create?.body).not.toHaveProperty('teamId');
    expect(api.item(ids.msk, 10).areaId).toBe(ids.mskRoot);
    expect(api.data.places).toEqual([]);
    expect(api.data.projectPlaces).toEqual([]);
  });
});

describe('US-3 Scenario: Who moves cards (BR-PB-5)', () => {
  it('shows a reader every card with no «Ещё действия», none draggable, and the checklist’s checkboxes disabled', async () => {
    const data = projectDataset();
    const us2 = data.items.find((record) => record.number === 2);
    if (!us2) throw new Error('No MSK-US-2.');
    data.items.push(platformItem(4, 'TASK', 'be', { parentId: us2.id }));
    serveBoards(data);
    renderBoards(projectBoardAddress(), { role: 'viewer' });
    const board = await findBoard();

    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-2', 'MSK-US-3', 'MSK-US-9']);
    for (const identifier of ['MSK-US-2', 'MSK-US-3', 'MSK-US-9']) {
      const card = cardOf(board, identifier);
      expect(within(card).queryAllByRole('button', { name: /^Ещё действия/ })).toHaveLength(0);
      expect(card).toHaveAttribute('draggable', 'false');
    }
    await waitFor(() => expect(counterOf(board, 'MSK-US-2', 'task')).toBeInTheDocument());
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    expect(checkboxOf(listOf(board, 'MSK-US-2', 'Задача'), 'MSK-TASK-4')).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    expect(
      within(cardOf(board, 'MSK-US-2')).queryByRole('button', {
        name: 'Добавить задачу (MSK-US-2)',
      }),
    ).toHaveAttribute('aria-disabled', 'true');
  });
});

describe('US-3 Scenario: An empty board (BR-PB-6)', () => {
  it.each([
    ['admin', true],
    ['viewer', false],
  ] as const)(
    'reads «Канбан пуст» with the first column’s own name in the hint, the action for a writer only: the %s',
    async (role, writer) => {
      serveBoards(projectDataset());
      renderBoards(projectBoardAddress('epics'), { role });

      expect(await screen.findByRole('heading', { level: 2, name: 'Канбан пуст' })).toBeVisible();
      expect(
        screen.getByText('Новые рабочие элементы этого уровня появятся в колонке «Новая».'),
      ).toBeInTheDocument();
      expect(screen.queryAllByRole('button', { name: 'Новый рабочий элемент' })).toHaveLength(
        writer ? 1 : 0,
      );
      expect(screen.getByRole('heading', { level: 1, name: 'Мастаск' })).toBeInTheDocument();
      expect(levelButton()).toHaveAccessibleName('Уровень: Эпики');
    },
  );
});

describe('US-3 Scenario: The same missing addresses as the backlog’s (BR-MX-1)', () => {
  it.each([['tasks'], ['sprints']])(
    '`<project>/boards/%s` is «Страница не найдена», and nothing is read',
    async (level) => {
      const api = serveBoards(projectDataset());
      renderBoards(`${projectPrefix()}/boards/${level}`);

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
      ).toBeInTheDocument();
      expect(api.reads('/boards/')).toEqual([]);
    },
  );

  it('the board of a deactivated project reads «Проект не найден», the frame replacing the whole page', async () => {
    const data = projectDataset();
    data.projects = data.projects.map((project) =>
      project.id === ids.ops ? { ...project, active: false } : project,
    );
    serveBoards(data);
    renderBoards(`/organizations/${ids.example}/projects/${ids.ops}/boards/stories`);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Проект не найден' }),
    ).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(screen.queryByRole('tablist', { name: 'Вид страницы' })).toBeNull();
    expect(screen.queryByRole('heading', { name: 'Команда не найдена' })).toBeNull();
  });
});

describe('US-3 Scenario: The read’s states (design § 7)', () => {
  it('keeps the head complete and shows the board skeleton «Загрузка данных» while the board is read', async () => {
    serveBoards(activeDataset());
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(PROJECT_BOARD_URL, async () => {
        await held;
        await delay(0);
        return undefined;
      }),
    );
    renderBoards(projectBoardAddress());

    expect(await screen.findByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Мастаск' })).toBeInTheDocument();
    // feat-040 BR-TS-11: the project's head has no team drop-down.
    expect(screen.queryByRole('button', { name: 'Сменить команду' })).toBeNull();
    expect(screen.getByRole('tab', { name: 'Канбан' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Показать бэклог' })).toBeInTheDocument();

    release();
    expect(await findBoard()).toBeInTheDocument();
    expect(screen.queryByRole('status', { name: 'Загрузка данных' })).toBeNull();
  });

  it('shows the server’s text with «Повторить» in place of the board, the head kept, and the cards after a retry', async () => {
    serveBoards(activeDataset());
    server.use(
      http.get(
        PROJECT_BOARD_URL,
        () =>
          HttpResponse.json(
            { status: 500, title: 'Ошибка', detail: 'Сервис не ответил.' },
            { status: 500 },
          ),
        { once: true },
      ),
    );
    renderBoards(projectBoardAddress());

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Сервис не ответил.');
    expect(screen.getByRole('heading', { level: 1, name: 'Мастаск' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Канбан' })).toBeNull();

    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));

    expect(cardsIn(await findBoard(), 'Новая')).toEqual(['MSK-US-2', 'MSK-US-3']);
  });

  it('shows `boards.level.not_found` as the banner, not a frame', async () => {
    serveBoards(activeDataset());
    server.use(
      http.get(PROJECT_BOARD_URL, () =>
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
    renderBoards(projectBoardAddress('epics'));

    expect(await screen.findByRole('alert')).toHaveTextContent('Уровень бэклога не найден.');
    expect(screen.getByRole('heading', { level: 1, name: 'Мастаск' })).toBeInTheDocument();
  });

  it('a re-read that fails leaves the board with its cards and the error above it (state 9); a retry that succeeds takes the error away', async () => {
    serveBoards(activeDataset());
    renderBoards(projectBoardAddress());
    const board = await findBoard();
    server.use(
      http.get(
        PROJECT_BOARD_URL,
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
    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-2', 'MSK-US-3']);
    expect(cardsIn(board, 'Активна')).toEqual(['MSK-US-9']);

    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));

    await waitFor(() => expect(screen.queryByRole('alert')).toBeNull());
    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-2', 'MSK-US-3']);
  });

  it('reads again when the tab regains focus and shows a change made elsewhere, with no notice', async () => {
    const api = serveBoards(activeDataset());
    renderBoards(projectBoardAddress());
    const board = await findBoard();
    expect(cardsIn(board, 'Активна')).toEqual(['MSK-US-9']);

    api.item(ids.msk, 9).stateId = stateId('US', 'Закрыта');
    focusManager.setFocused(false);
    focusManager.setFocused(true);

    await waitFor(() => expect(cardsIn(board, 'Закрыта')).toEqual(['MSK-US-9']));
    expect(screen.queryByRole('alert')).toBeNull();
  });
});

// feat-040 BR-TS-11 retires feat-039 BR-PB-7: the project's head has no team drop-down, so the test
// «lists the teams and «Весь проект», selected; choosing «Платформа» opens that team’s board at the
// same level» is retired by name; the head's absence is held beside the project backlog's
// (`ProjectBacklogPage.test.tsx`, "The project’s head has no team drop-down").
