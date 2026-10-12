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
import {
  addStoryOnlyReviewState,
  boardsDataset,
  catalogText,
  ids,
  platformItem,
  stateId,
} from '../testFixtures';
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
  it('drags MSK-US-2 and MSK-BUG-5 to «Разрешена»: both at once, in the one column, each its own type’s «Разрешена», nothing else changed, the order kept', async () => {
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
    await dragCard(board, 'MSK-BUG-5', 'Разрешена');
    expect(cardsIn(board, 'Разрешена')).toEqual(['MSK-US-2', 'MSK-BUG-5']);
    await waitFor(() => expect(cardMenuButton('MSK-BUG-5')).not.toHaveAttribute('aria-disabled'));

    expect(cardsIn(board, 'Активна')).toEqual([]);
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
  // The seeded «Истории» columns map both types (bug-004 D-3), so the case needs a process the seed does not write:
  // a state only «Пользовательская история» has, «Проверка», in the Resolved category (doc-007's customization is not
  // built; the in-memory API derives the columns from this fixture exactly as the backend derives them).
  let review: { stateId: string; restore(): void } | null = null;

  afterEach(() => {
    review?.restore();
    review = null;
  });

  it('a story-only «Проверка»: MSK-BUG-5 over it has no drop area and the drop is refused, nothing sent; MSK-US-2 drops there', async () => {
    review = addStoryOnlyReviewState();
    const api = serveBoards(activeDataset());
    renderBoards(boardAddress());
    const board = await findBoard();

    const transfer = dataTransfer();
    await act(async () => fireDrag('dragStart', cardOf(board, 'MSK-BUG-5'), transfer));
    expect(columnBox(board, 'Проверка')).not.toHaveAttribute('data-drop-target');
    expect(columnBox(board, 'Активна')).not.toHaveAttribute('data-drop-target');
    expect(columnBox(board, 'Разрешена')).toHaveAttribute('data-drop-target', 'true');
    expect(columnBox(board, 'Новая')).toHaveAttribute('data-drop-target', 'true');
    expect(columnBox(board, 'Закрыта')).toHaveAttribute('data-drop-target', 'true');
    await act(async () => fireDrag('dragEnd', cardOf(board, 'MSK-BUG-5'), transfer));

    expect(await dragCard(board, 'MSK-BUG-5', 'Проверка')).toEqual({
      accepted: false,
      dropTarget: false,
    });
    // Its own column is none either.
    expect(await dragCard(board, 'MSK-US-2', 'Активна')).toEqual({
      accepted: false,
      dropTarget: false,
    });
    expect(cardsIn(board, 'Активна')).toEqual(['MSK-US-2', 'MSK-BUG-5']);
    expect(patches(api)).toEqual([]);

    // The story may go there, and the card menu of the bug does not offer the column.
    expect(await dragCard(board, 'MSK-US-2', 'Проверка')).toMatchObject({ dropTarget: true });
    await waitFor(() => expect(cardMenuButton('MSK-US-2')).not.toHaveAttribute('aria-disabled'));
    expect(api.item(ids.msk, 2).stateId).toBe(review.stateId);
    await userEvent.click(cardMenuButton('MSK-BUG-5'));
    const menu = await screen.findByRole('menu', { name: 'Переместить в колонку' });
    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((entry) => entry.textContent),
    ).toEqual(['Новая', 'Разрешена', 'Закрыта']);
  });

  it('«Разрешена» is a drop area for a bug and for a story alike — the seeded process has no column without a state for either', async () => {
    serveBoards(activeDataset());
    renderBoards(boardAddress());
    const board = await findBoard();

    for (const identifier of ['MSK-US-2', 'MSK-BUG-5']) {
      const transfer = dataTransfer();
      await act(async () => fireDrag('dragStart', cardOf(board, identifier), transfer));
      expect(columnBox(board, 'Разрешена')).toHaveAttribute('data-drop-target', 'true');
      expect(columnBox(board, 'Активна')).not.toHaveAttribute('data-drop-target');
      await act(async () => fireDrag('dragEnd', cardOf(board, identifier), transfer));
    }
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
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Разрешена' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Этот статус недоступен для рабочего элемента этого типа.',
    );
    expect(cardsIn(board, 'Активна')).toEqual(['MSK-US-2', 'MSK-BUG-5']);
  });
});

describe('US-5 Scenario: The card’s menu moves it by keyboard', () => {
  it('MSK-BUG-5 in «Новая»: «Переместить в колонку» lists «Активна», «Разрешена», «Закрыта»; «Разрешена» moves it, focus follows, it is announced', async () => {
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
    ).toEqual(['Активна', 'Разрешена', 'Закрыта']);
    expect(cardMenuButton('MSK-BUG-5')).toHaveAttribute('aria-expanded', 'true');
    within(menu).getByRole('menuitem', { name: 'Разрешена' }).focus();
    expect(cardMenuButton('MSK-BUG-5')).not.toHaveFocus();
    await userEvent.keyboard('{Enter}');

    expect(cardsIn(board, 'Разрешена')).toEqual(['MSK-BUG-5']);
    expect(cardsIn(board, 'Новая')).toEqual([]);
    // The card is drawn anew in «Разрешена»: focus is on its button there.
    const moved = within(columnList(board, 'Разрешена')).getByRole('button', {
      name: 'Ещё действия: MSK-BUG-5',
      hidden: true,
    });
    await waitFor(() => expect(moved).toHaveFocus());
    expect(announcer()).toHaveTextContent('MSK-BUG-5 перемещён в колонку «Разрешена».');

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
  it('MSK-US-9 dropped on «Активна» stands between MSK-US-3 and MSK-US-11, and shows «Активна» at once', async () => {
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
    expect(cardsIn(board, 'Активна')).toEqual(['MSK-US-3', 'MSK-US-11']);

    await dragCard(board, 'MSK-US-9', 'Активна');

    expect(cardsIn(board, 'Активна')).toEqual(['MSK-US-3', 'MSK-US-9', 'MSK-US-11']);
    // The state's name comes from the process read beside the board.
    expect(within(cardOf(board, 'MSK-US-9')).getByText('Активна')).toBeInTheDocument();
    expect(
      within(columnList(board, 'Активна')).getByRole('button', {
        name: 'Ещё действия: MSK-US-9',
      }),
    ).toHaveFocus();
    release();
    await waitFor(() => expect(cardMenuButton('MSK-US-9')).not.toHaveAttribute('aria-disabled'));
    expect(cardsIn(board, 'Активна')).toEqual(['MSK-US-3', 'MSK-US-9', 'MSK-US-11']);
  });
});

describe('US-5 Scenario: Two users move the same card, the later decides', () => {
  it('Анна moves MSK-US-2 to «Закрыта», Борис then to «Активна»: it is in «Активна», no notice', async () => {
    const data = boardsDataset();
    data.items.push(platformItem(2, 'US', 'Вход по паролю'));
    const api = serveBoards(data);
    renderBoards(boardAddress());
    const board = await findBoard();

    await userEvent.click(cardMenuButton('MSK-US-2'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Закрыта' }));
    await waitFor(() => expect(cardMenuButton('MSK-US-2')).not.toHaveAttribute('aria-disabled'));
    expect(cardsIn(board, 'Закрыта')).toEqual(['MSK-US-2']);

    // Борис, whose board still showed «Новая».
    api.item(ids.msk, 2).stateId = stateId('US', 'Активна');
    act(() => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });

    await waitFor(() => expect(cardsIn(board, 'Активна')).toEqual(['MSK-US-2']));
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
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Закрыта' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(FAILED_TEXT);
    expect(cardsIn(board, 'Активна')).toEqual(['MSK-US-2', 'MSK-BUG-5']);
    expect(cardsIn(board, 'Закрыта')).toEqual([]);
  });
});

describe('US-5 Scenario: One move at a time', () => {
  it('while a move is saved every «Ещё действия» is off and no card can be dragged; once the answer arrives the board is read again', async () => {
    const api = serveBoards(activeDataset());
    const { release } = holdMoves();
    renderBoards(boardAddress());
    const board = await findBoard();

    await userEvent.click(cardMenuButton('MSK-US-2'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Закрыта' }));

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
    expect(cardsIn(board, 'Закрыта')).toEqual(['MSK-US-2']);
  });
});
