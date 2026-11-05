import { afterEach, describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { WORK_ITEM_URL } from '../../workitems/testFixtures';
import { server } from '../../../test/mocks/server';
import { serveBoards } from '../testApi';
import { cardMenuButton, cardsIn, findBoard } from '../testBoard';
import { checkboxOf, counterOf, listOf, rowsOf } from '../testChecklist';
import {
  boardsDataset,
  checklistDataset,
  ids,
  platformItem,
  problem,
  stateId,
} from '../testFixtures';
import { boardAddress, renderBoards } from '../testRender';

// mod-005 US-2 and US-3 on a board: a deleted item is on no board and in no checklist or counter,
// and a card or a tick that was drawn before the deletion meets its refusal with the server's
// own text, puts itself back and reads the board again (design § 7, § 8; spec § Surfaces and
// writes). The move and the tick are one state save, so the refusals are the card's own.

afterEach(() => {
  focusManager.setFocused(undefined);
});

const DELETED_TEXT = 'Рабочий элемент удалён и доступен только для чтения.';
const NOT_FOUND_TEXT = 'Рабочий элемент не найден.';

function refuseSaves(code: string): void {
  const body = problem(code);
  server.use(http.patch(WORK_ITEM_URL, () => HttpResponse.json(body, { status: body.status })));
}

/** US-1's board with the list of tasks of MSK-US-2 open. */
async function openTasks() {
  const api = serveBoards(checklistDataset());
  renderBoards(boardAddress());
  const board = await findBoard();
  await waitFor(() => expect(counterOf(board, 'MSK-US-9', 'task')).toBeInTheDocument());
  await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
  return { api, board, list: () => listOf(board, 'MSK-US-2', 'Задача') };
}

describe('mod-005 US-2 Scenario: A deleted child is neither listed nor counted', () => {
  it('MSK-TASK-7, deleted, is not in the checklist and the counter reads the three live tasks', async () => {
    const { board, list } = await openTasks();

    expect(rowsOf(list())).not.toContain('MSK-TASK-7');
    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveTextContent('1/3');
  });
});

describe('mod-005 US-3 Scenario: A stale checklist row cannot be ticked', () => {
  it('409 `…deleted`: the row is put back, the server’s text shows, and the board is read again without it', async () => {
    const { api, board, list } = await openTasks();
    // Another user deleted MSK-TASK-4 after this board was read.
    api.item(ids.msk, 4).stateId = stateId('TASK', 'Удалена');
    refuseSaves('workitems.workitem.deleted');
    const reads = api.reads('/boards/stories').length;

    await userEvent.click(checkboxOf(list(), 'MSK-TASK-4'));

    expect(await screen.findByRole('alert')).toHaveTextContent(DELETED_TEXT);
    await waitFor(() => expect(api.reads('/boards/stories').length).toBeGreaterThan(reads));
    await waitFor(() => expect(rowsOf(list())).not.toContain('MSK-TASK-4'));
    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveTextContent('1/2');
    expect(api.item(ids.msk, 4).stateId).toBe(stateId('TASK', 'Удалена'));
  });

  it('404 `workitems.workitem.not_found` (purged meanwhile): the server’s text shows and the row is gone after the read', async () => {
    const { api, board, list } = await openTasks();
    api.data.items.splice(api.data.items.indexOf(api.item(ids.msk, 4)), 1);
    refuseSaves('workitems.workitem.not_found');

    await userEvent.click(checkboxOf(list(), 'MSK-TASK-4'));

    expect(await screen.findByRole('alert')).toHaveTextContent(NOT_FOUND_TEXT);
    await waitFor(() => expect(rowsOf(list())).not.toContain('MSK-TASK-4'));
    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveTextContent('1/2');
  });
});

describe('mod-005 US-3 Scenario: A stale board card of a deleted item cannot be moved', () => {
  /** MSK-BUG-5 «Новая» on «Истории» of «Платформа», the card to move. */
  function bugDataset() {
    const data = boardsDataset();
    data.items.push(platformItem(5, 'BUG', 'Неверный счётчик'));
    return data;
  }

  async function moveToResolved() {
    cardMenuButton('MSK-BUG-5').focus();
    await userEvent.keyboard('{Enter}');
    const menu = await screen.findByRole('menu', { name: 'Переместить в колонку' });
    within(menu).getByRole('menuitem', { name: 'Разрешена' }).focus();
    await userEvent.keyboard('{Enter}');
  }

  it('409 `…deleted`: the card goes back, the server’s text shows, and the board is read again without the card', async () => {
    const api = serveBoards(bugDataset());
    renderBoards(boardAddress());
    const board = await findBoard();
    expect(cardsIn(board, 'Новая')).toContain('MSK-BUG-5');
    // Another user deleted it after this board was read.
    api.item(ids.msk, 5).stateId = stateId('BUG', 'Удалена');
    refuseSaves('workitems.workitem.deleted');
    const reads = api.reads('/boards/stories').length;

    await moveToResolved();

    expect(await screen.findByRole('alert')).toHaveTextContent(DELETED_TEXT);
    await waitFor(() => expect(api.reads('/boards/stories').length).toBeGreaterThan(reads));
    await waitFor(() =>
      expect(
        screen.queryByRole('button', { name: 'Ещё действия: MSK-BUG-5', hidden: true }),
      ).not.toBeInTheDocument(),
    );
    expect(api.item(ids.msk, 5).stateId).toBe(stateId('BUG', 'Удалена'));
  });

  it('404 `workitems.workitem.not_found` (purged meanwhile): the server’s text shows and the card is gone after the read', async () => {
    const api = serveBoards(bugDataset());
    renderBoards(boardAddress());
    await findBoard();
    api.data.items.splice(api.data.items.indexOf(api.item(ids.msk, 5)), 1);
    refuseSaves('workitems.workitem.not_found');

    await moveToResolved();

    expect(await screen.findByRole('alert')).toHaveTextContent(NOT_FOUND_TEXT);
    await waitFor(() =>
      expect(
        screen.queryByRole('button', { name: 'Ещё действия: MSK-BUG-5', hidden: true }),
      ).not.toBeInTheDocument(),
    );
  });
});
