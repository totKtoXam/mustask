import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { WORK_ITEM_URL } from '../../workitems/testFixtures';
import { server } from '../../../test/mocks/server';
import { serveBoards, type BoardsTestApi } from '../testApi';
import { cardMenuButton, cardOf, cardsIn, columnBox, columnList, findBoard } from '../testBoard';
import { dataTransfer, fireDrag } from '../testDrag';
import { boardsDataset, catalogText, ids, platformItem, stateId } from '../testFixtures';
import { boardAddress, renderBoards } from '../testRender';

// US-5: moving a card on «Платформа»'s «Истории» board. The page is Анна's;
// Борис's moves are the in-memory API's own, as another user's request would be.

const FAILED_TEXT = 'Не удалось сохранить перемещение. Попробуйте ещё раз.';

afterEach(() => {
  focusManager.setFocused(undefined);
});

/** MSK-US-2 and MSK-BUG-5 in «Активна», MSK-US-3 «Новая». */
function activeDataset() {
  const data = boardsDataset();
  data.items.push(
    platformItem(2, 'US', 'Вход по паролю', {
      stateId: stateId('US', 'Активна'),
      assigneeId: ids.anna,
    }),
    platformItem(3, 'US', 'Выход'),
    platformItem(5, 'BUG', 'Неверный счётчик', { stateId: stateId('BUG', 'Активна') }),
  );
  return data;
}

function patches(api: BoardsTestApi) {
  return api.writes().filter((request) => request.method === 'PATCH');
}

/** The page's announcement region: the `role="status"` that is not the skeleton. */
function announcer(): HTMLElement {
  const region = screen
    .getAllByRole('status')
    .find((element) => !element.hasAttribute('aria-label'));
  if (!region) throw new Error('No announcement region.');
  return region;
}

/** Holds every PATCH until `release()`, then lets the in-memory API take it. */
function holdMoves() {
  let release: () => void = () => {};
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  server.use(
    http.patch(WORK_ITEM_URL, async () => {
      await held;
      return undefined;
    }),
  );
  return { release };
}

/** Drags the card onto the column by the browser's own drag and drop; `accepted`: the column took it. */
async function dragCard(board: HTMLElement, identifier: string, header: string) {
  const transfer = dataTransfer();
  const card = cardOf(board, identifier);
  const target = columnBox(board, header);
  await act(async () => fireDrag('dragStart', card, transfer));
  let accepted = false;
  await act(async () => {
    accepted = !fireDrag('dragOver', target, transfer);
  });
  const dropTarget = target.getAttribute('data-drop-target') === 'true';
  await act(async () => fireDrag('drop', target, transfer));
  await act(async () => fireDrag('dragEnd', card, transfer));
  return { accepted, dropTarget };
}

describe('US-5 Scenario: A move writes the state its column maps for the card’s type', () => {
  it('drags MSK-US-2 to «Разрешена» and MSK-BUG-5 to «Решённые»: both at once, both «Разрешена», nothing else changed, the order kept', async () => {
    const api = serveBoards(activeDataset());
    renderBoards(boardAddress());
    const board = await findBoard();
    const before = {
      us: { ...api.item(ids.msk, 2) },
      bug: { ...api.item(ids.msk, 5) },
    };

    await dragCard(board, 'MSK-US-2', 'Разрешена');
    expect(cardsIn(board, 'Разрешена')).toEqual(['MSK-US-2']);
    await waitFor(() => expect(cardMenuButton('MSK-US-2')).not.toHaveAttribute('aria-disabled'));
    await dragCard(board, 'MSK-BUG-5', 'Решённые');
    expect(cardsIn(board, 'Решённые')).toEqual(['MSK-BUG-5']);
    await waitFor(() => expect(cardMenuButton('MSK-BUG-5')).not.toHaveAttribute('aria-disabled'));

    expect(cardsIn(board, 'В работе')).toEqual([]);
    const us = api.item(ids.msk, 2);
    const bug = api.item(ids.msk, 5);
    expect(us.stateId).toBe(stateId('US', 'Разрешена'));
    expect(bug.stateId).toBe(stateId('BUG', 'Разрешена'));
    expect([us.title, us.assigneeId, us.areaId]).toEqual([
      before.us.title,
      before.us.assigneeId,
      before.us.areaId,
    ]);
    expect(us.changedAt).not.toBe(before.us.changedAt);
    expect(bug.changedAt).not.toBe(before.bug.changedAt);
    // US-5 "A form opened before the move keeps the moved state": the move
    // sends the state alone, so a form's later save of other fields keeps it
    // (mod-003 BR-M3-25).
    expect(patches(api).map((request) => request.body)).toEqual([
      { stateId: stateId('US', 'Разрешена') },
      { stateId: stateId('BUG', 'Разрешена') },
    ]);
    expect(patches(api).map((request) => request.path)).toEqual([
      `/api/organizations/${ids.example}/projects/${ids.msk}/workitems/MSK-US-2`,
      `/api/organizations/${ids.example}/projects/${ids.msk}/workitems/MSK-BUG-5`,
    ]);
    expect(api.data.places).toEqual([]);
    expect(api.order(ids.platformTeam)).toEqual(['MSK-US-2', 'MSK-US-3', 'MSK-BUG-5']);
  });
});

describe('US-5 Scenario: A column without a state for the type is no drop target', () => {
  it('MSK-BUG-5 over «Разрешена» and MSK-US-2 over «Решённые»: no drop area, the drop refused, nothing sent', async () => {
    const api = serveBoards(activeDataset());
    renderBoards(boardAddress());
    const board = await findBoard();

    const transfer = dataTransfer();
    await act(async () => fireDrag('dragStart', cardOf(board, 'MSK-BUG-5'), transfer));
    expect(columnBox(board, 'Разрешена')).not.toHaveAttribute('data-drop-target');
    expect(columnBox(board, 'В работе')).not.toHaveAttribute('data-drop-target');
    expect(columnBox(board, 'Решённые')).toHaveAttribute('data-drop-target', 'true');
    expect(columnBox(board, 'Новые')).toHaveAttribute('data-drop-target', 'true');
    await act(async () => fireDrag('dragEnd', cardOf(board, 'MSK-BUG-5'), transfer));

    expect(await dragCard(board, 'MSK-BUG-5', 'Разрешена')).toEqual({
      accepted: false,
      dropTarget: false,
    });
    expect(await dragCard(board, 'MSK-US-2', 'Решённые')).toEqual({
      accepted: false,
      dropTarget: false,
    });
    // Its own column is none either.
    expect(await dragCard(board, 'MSK-US-2', 'В работе')).toEqual({
      accepted: false,
      dropTarget: false,
    });
    expect(cardsIn(board, 'В работе')).toEqual(['MSK-US-2', 'MSK-BUG-5']);
    expect(patches(api)).toEqual([]);
  });

  it('a refusal of the state — `workitems.workitem.state_not_allowed` — shows its text and puts the card back', async () => {
    serveBoards(activeDataset());
    server.use(
      http.patch(WORK_ITEM_URL, () =>
        HttpResponse.json(
          {
            status: 409,
            title: 'workitems.workitem.state_not_allowed',
            detail: catalogText('workitems.workitem.state_not_allowed'),
            errorCode: 'workitems.workitem.state_not_allowed',
          },
          { status: 409 },
        ),
      ),
    );
    renderBoards(boardAddress());
    const board = await findBoard();

    await userEvent.click(cardMenuButton('MSK-BUG-5'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Решённые' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Этот статус недоступен для рабочего элемента этого типа.',
    );
    expect(cardsIn(board, 'В работе')).toEqual(['MSK-US-2', 'MSK-BUG-5']);
  });
});

describe('US-5 Scenario: The card’s menu moves it by keyboard', () => {
  it('MSK-BUG-5 in «Новые»: «Переместить в колонку» lists «В работе», «Решённые», «Завершённые»; «Решённые» moves it, focus follows, it is announced', async () => {
    const data = boardsDataset();
    data.items.push(platformItem(5, 'BUG', 'Неверный счётчик'));
    const api = serveBoards(data);
    renderBoards(boardAddress());
    const board = await findBoard();
    expect(announcer().textContent).toBe('');

    cardMenuButton('MSK-BUG-5').focus();
    await userEvent.keyboard('{Enter}');
    const menu = await screen.findByRole('menu', { name: 'Переместить в колонку' });
    expect(within(menu).getByText('Переместить в колонку')).toBeInTheDocument();
    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((entry) => entry.textContent),
    ).toEqual(['В работе', 'Решённые', 'Завершённые']);
    expect(cardMenuButton('MSK-BUG-5')).toHaveAttribute('aria-expanded', 'true');
    within(menu).getByRole('menuitem', { name: 'Решённые' }).focus();
    expect(cardMenuButton('MSK-BUG-5')).not.toHaveFocus();
    await userEvent.keyboard('{Enter}');

    expect(cardsIn(board, 'Решённые')).toEqual(['MSK-BUG-5']);
    expect(cardsIn(board, 'Новые')).toEqual([]);
    // The card is drawn anew in «Решённые»: focus is on its button there.
    const moved = within(columnList(board, 'Решённые')).getByRole('button', {
      name: 'Ещё действия: MSK-BUG-5',
      hidden: true,
    });
    await waitFor(() => expect(moved).toHaveFocus());
    expect(announcer()).toHaveTextContent('MSK-BUG-5 перемещён в колонку «Решённые».');

    await waitFor(() => expect(cardMenuButton('MSK-BUG-5')).not.toHaveAttribute('aria-disabled'));
    expect(cardMenuButton('MSK-BUG-5')).toHaveFocus();
    expect(api.item(ids.msk, 5).stateId).toBe(stateId('BUG', 'Разрешена'));
    expect(within(cardOf(board, 'MSK-BUG-5')).getByText('Разрешена')).toBeInTheDocument();
  });

  it('Escape closes the menu with nothing moved and focus back on the button', async () => {
    const api = serveBoards(activeDataset());
    renderBoards(boardAddress());
    await findBoard();

    await userEvent.click(cardMenuButton('MSK-US-3'));
    await screen.findByRole('menu', { name: 'Переместить в колонку' });
    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
    expect(cardMenuButton('MSK-US-3')).toHaveFocus();
    expect(patches(api)).toEqual([]);
  });
});

describe('US-5 Scenario: The card lands at its backlog place in the new column', () => {
  it('MSK-US-9 dropped on «В работе» stands between MSK-US-3 and MSK-US-11, and shows «Активна» at once', async () => {
    const data = boardsDataset();
    data.items.push(
      platformItem(3, 'US', 'Три', { stateId: stateId('US', 'Активна') }),
      platformItem(9, 'US', 'Девять'),
      platformItem(11, 'US', 'Одиннадцать', { stateId: stateId('US', 'Активна') }),
    );
    serveBoards(data);
    const { release } = holdMoves();
    renderBoards(boardAddress());
    const board = await findBoard();
    expect(cardsIn(board, 'В работе')).toEqual(['MSK-US-3', 'MSK-US-11']);

    await dragCard(board, 'MSK-US-9', 'В работе');

    expect(cardsIn(board, 'В работе')).toEqual(['MSK-US-3', 'MSK-US-9', 'MSK-US-11']);
    // The state's name comes from the process read beside the board.
    expect(within(cardOf(board, 'MSK-US-9')).getByText('Активна')).toBeInTheDocument();
    expect(
      within(columnList(board, 'В работе')).getByRole('button', {
        name: 'Ещё действия: MSK-US-9',
      }),
    ).toHaveFocus();
    release();
    await waitFor(() => expect(cardMenuButton('MSK-US-9')).not.toHaveAttribute('aria-disabled'));
    expect(cardsIn(board, 'В работе')).toEqual(['MSK-US-3', 'MSK-US-9', 'MSK-US-11']);
  });
});

describe('US-5 Scenario: Two users move the same card, the later decides', () => {
  it('Анна moves MSK-US-2 to «Завершённые», Борис then to «В работе»: it is in «Активна», no notice', async () => {
    const data = boardsDataset();
    data.items.push(platformItem(2, 'US', 'Вход по паролю'));
    const api = serveBoards(data);
    renderBoards(boardAddress());
    const board = await findBoard();

    await userEvent.click(cardMenuButton('MSK-US-2'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Завершённые' }));
    await waitFor(() => expect(cardMenuButton('MSK-US-2')).not.toHaveAttribute('aria-disabled'));
    expect(cardsIn(board, 'Завершённые')).toEqual(['MSK-US-2']);

    // Борис, whose board still showed «Новые».
    api.item(ids.msk, 2).stateId = stateId('US', 'Активна');
    act(() => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });

    await waitFor(() => expect(cardsIn(board, 'В работе')).toEqual(['MSK-US-2']));
    expect(screen.queryByRole('alert')).toBeNull();
  });
});

describe('US-5 Scenario: A failed move puts the card back', () => {
  it('with no answer the card returns to its former column and «Не удалось сохранить перемещение…» shows', async () => {
    serveBoards(activeDataset());
    server.use(http.patch(WORK_ITEM_URL, () => HttpResponse.error()));
    renderBoards(boardAddress());
    const board = await findBoard();

    await userEvent.click(cardMenuButton('MSK-US-2'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Завершённые' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(FAILED_TEXT);
    expect(cardsIn(board, 'В работе')).toEqual(['MSK-US-2', 'MSK-BUG-5']);
    expect(cardsIn(board, 'Завершённые')).toEqual([]);
  });
});

describe('US-5 Scenario: One move at a time', () => {
  it('while a move is saved every «Ещё действия» is off and no card can be dragged; once the answer arrives the board is read again', async () => {
    const api = serveBoards(activeDataset());
    const { release } = holdMoves();
    renderBoards(boardAddress());
    const board = await findBoard();

    await userEvent.click(cardMenuButton('MSK-US-2'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Завершённые' }));

    for (const identifier of ['MSK-US-2', 'MSK-US-3', 'MSK-BUG-5']) {
      expect(cardMenuButton(identifier)).toHaveAttribute('aria-disabled', 'true');
      expect(cardOf(board, identifier)).toHaveAttribute('draggable', 'false');
    }
    await userEvent.click(cardMenuButton('MSK-US-3'));
    expect(screen.queryByRole('menu')).toBeNull();
    const transfer = dataTransfer();
    let started = true;
    await act(async () => {
      started = fireDrag('dragStart', cardOf(board, 'MSK-US-3'), transfer);
    });
    expect(started).toBe(false);
    const reads = api.reads('/boards/stories').length;

    release();
    await waitFor(() => expect(cardMenuButton('MSK-US-3')).not.toHaveAttribute('aria-disabled'));
    expect(api.reads('/boards/stories')).toHaveLength(reads + 1);
    expect(patches(api)).toHaveLength(1);
    expect(cardOf(board, 'MSK-US-3')).toHaveAttribute('draggable', 'true');
    expect(cardsIn(board, 'Завершённые')).toEqual(['MSK-US-2']);
  });
});
