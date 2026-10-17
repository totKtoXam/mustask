import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { serveBoards, serveProblem, type BoardsTestApi } from '../testApi';
import {
  CELL,
  actions,
  announcer,
  cellOf,
  findBacklogTable,
  identifiers,
  orders,
  rowOf,
} from '../testBacklog';
import { dataTransfer, fireDrag } from '../testDrag';
import {
  PROJECT_REORDER_URL,
  catalogText,
  ids,
  platformItem,
  problem,
  projectDataset,
  stateId,
} from '../testFixtures';
import { backlogAddress, projectBacklogAddress, renderBoards } from '../testRender';

// feat-039 US-2: the project's backlog is reordered as a team's is (BR-PB-4, mod-006
// BR-M4-8, -24..-26) over an order of the project's own. «Мастаск» lists MSK-US-2,
// MSK-US-3, MSK-US-9, and «Платформа» alone MSK-US-3 and MSK-US-4.

const START = ['MSK-US-2', 'MSK-US-3', 'MSK-US-4', 'MSK-US-9'];
const STALE_TEXT = catalogText('boards.backlog.reorder_stale');
const FAILED_TEXT = 'Не удалось сохранить перемещение. Попробуйте ещё раз.';

afterEach(() => {
  focusManager.setFocused(undefined);
});

function dataset() {
  const data = projectDataset();
  data.items.push(platformItem(4, 'US', 'Вход по ссылке'));
  return data;
}

function reorders(api: BoardsTestApi) {
  return api.writes().filter((request) => request.path.endsWith('/reorder'));
}

/** Drags the row `identifier` and drops it on `target`'s `half`. */
async function dragAndDrop(
  table: HTMLElement,
  identifier: string,
  target: string,
  half: 'upper' | 'lower',
) {
  const transfer = dataTransfer();
  const source = rowOf(table, identifier);
  const over = cellOf(rowOf(table, target), CELL.title);
  // One event per act: the browser renders between them.
  await act(async () => fireDrag('dragStart', source, transfer));
  await act(async () => fireDrag('dragOver', over, transfer, half));
  await act(async () => fireDrag('drop', over, transfer, half));
  await act(async () => fireDrag('dragEnd', source, transfer));
}

describe('US-2 Scenario: A reorder by keyboard (BR-MX-18, mod-006 BR-M4-8)', () => {
  it('«Ещё действия: MSK-US-9» → «В начало» lists it first, «Порядок» 1 to 4, focus stays, the status region announces the position; the request is the project’s and the item is untouched', async () => {
    const api = serveBoards(dataset());
    renderBoards(projectBacklogAddress());
    const table = await findBacklogTable();
    const changedAt = api.item(ids.msk, 9).changedAt;
    expect(identifiers(table)).toEqual(START);

    await userEvent.click(actions('MSK-US-9'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));

    expect(identifiers(table)).toEqual(['MSK-US-9', 'MSK-US-2', 'MSK-US-3', 'MSK-US-4']);
    expect(orders(table)).toEqual(['1', '2', '3', '4']);
    expect(actions('MSK-US-9')).toHaveFocus();
    expect(announcer()).toHaveTextContent('MSK-US-9 перемещён: позиция 1 из 4.');
    await waitFor(() => expect(actions('MSK-US-9')).not.toHaveAttribute('aria-disabled'));
    expect(reorders(api).map((request) => [request.path, request.body])).toEqual([
      [
        `/api/organizations/${ids.example}/projects/${ids.msk}/backlogs/stories/reorder`,
        { itemId: api.item(ids.msk, 9).id, position: 'top' },
      ],
    ]);
    expect(api.writes().map((request) => request.method)).toEqual(['POST']);
    expect(api.item(ids.msk, 9).changedAt).toBe(changedAt);
    expect(api.projectOrder(ids.msk)).toEqual(['MSK-US-9', 'MSK-US-2', 'MSK-US-3', 'MSK-US-4']);
    // The backlog is read again after the write, and it stands as the project’s order gives it.
    expect(api.reads('/backlogs/stories').length).toBeGreaterThanOrEqual(2);
    expect(identifiers(table)).toEqual(['MSK-US-9', 'MSK-US-2', 'MSK-US-3', 'MSK-US-4']);
  });

  it('«В начало» and «Выше» are disabled on the first row, «Ниже» and «В конец» on the last', async () => {
    serveBoards(dataset());
    renderBoards(projectBacklogAddress());
    await findBacklogTable();
    const disabled = (menu: HTMLElement) =>
      within(menu)
        .getAllByRole('menuitem')
        .filter((entry) => entry.getAttribute('aria-disabled') === 'true')
        .map((entry) => entry.textContent);

    await userEvent.click(actions('MSK-US-2'));
    expect(disabled(await screen.findByRole('menu', { name: 'Ещё действия: MSK-US-2' }))).toEqual([
      'В начало',
      'Выше',
      'Убрать родителя',
    ]);
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());

    await userEvent.click(actions('MSK-US-9'));
    expect(disabled(await screen.findByRole('menu', { name: 'Ещё действия: MSK-US-9' }))).toEqual([
      'Ниже',
      'В конец',
      'Убрать родителя',
    ]);
  });
});

describe('US-2 Scenario: A reorder by pointer, from any point of the row', () => {
  it('drags MSK-US-9 by its «Заголовок» above the first row: it is first, as «В начало» does', async () => {
    const api = serveBoards(dataset());
    renderBoards(projectBacklogAddress());
    const table = await findBacklogTable();

    await dragAndDrop(table, 'MSK-US-9', 'MSK-US-2', 'upper');

    await waitFor(() =>
      expect(identifiers(table)).toEqual(['MSK-US-9', 'MSK-US-2', 'MSK-US-3', 'MSK-US-4']),
    );
    await waitFor(() => expect(actions('MSK-US-9')).not.toHaveAttribute('aria-disabled'));
    expect(reorders(api).map((request) => request.body)).toEqual([
      { itemId: api.item(ids.msk, 9).id, position: 'top' },
    ]);
  });
});

describe('US-2 Scenario: The project’s order and a team’s order are independent (BR-MX-18, ADR-0028 rule 9)', () => {
  it('moving MSK-US-4 to the top of the project’s backlog leaves «Платформа»’s as it was, and the team’s reorder leaves the project’s', async () => {
    const api = serveBoards(dataset());
    renderBoards(projectBacklogAddress());
    await findBacklogTable();

    await userEvent.click(actions('MSK-US-4'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));
    await waitFor(() => expect(api.projectOrder(ids.msk)[0]).toBe('MSK-US-4'));

    expect(api.projectOrder(ids.msk)).toEqual(['MSK-US-4', 'MSK-US-2', 'MSK-US-3', 'MSK-US-9']);
    expect(api.order(ids.platformTeam)).toEqual(['MSK-US-3', 'MSK-US-4']);
    expect(api.data.places).toEqual([]);

    api.reorderAsAnotherUser(ids.platformTeam, 'stories', {
      itemId: api.item(ids.msk, 4).id,
      position: 'top',
    });
    expect(api.order(ids.platformTeam)).toEqual(['MSK-US-4', 'MSK-US-3']);
    expect(api.projectOrder(ids.msk)).toEqual(['MSK-US-4', 'MSK-US-2', 'MSK-US-3', 'MSK-US-9']);
  });

  it('a team’s page does not show the project’s order: «Платформа» still reads MSK-US-3, MSK-US-4 after the project’s reorder', async () => {
    const api = serveBoards(dataset());
    api.reorderProjectAsAnotherUser(ids.msk, 'stories', {
      itemId: api.item(ids.msk, 4).id,
      position: 'top',
    });
    renderBoards(backlogAddress());

    expect(identifiers(await findBacklogTable())).toEqual(['MSK-US-3', 'MSK-US-4']);
  });
});

describe('US-2 Scenario: A reorder naming an item that is not on this backlog is refused (mod-006 BR-M4-26)', () => {
  it('reads the backlog again first — without the item that left — then shows the stale notice, the row back; no place changes', async () => {
    const api = serveBoards(dataset());
    renderBoards(projectBacklogAddress());
    const table = await findBacklogTable();
    // MSK-US-3 enters the Removed category meanwhile (another user, on its form).
    api.item(ids.msk, 3).stateId = stateId('US', 'Удалена');

    await dragAndDrop(table, 'MSK-US-9', 'MSK-US-3', 'lower');

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(STALE_TEXT);
    expect(identifiers(table)).toEqual(['MSK-US-2', 'MSK-US-4', 'MSK-US-9']);
    expect(api.data.projectPlaces).toEqual([]);
  });

  it('shows the server’s stale refusal as the notice and puts the row back where it was', async () => {
    serveBoards(dataset());
    serveProblem('post', PROJECT_REORDER_URL, problem('boards.backlog.reorder_stale'));
    renderBoards(projectBacklogAddress());
    const table = await findBacklogTable();

    await userEvent.click(actions('MSK-US-9'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(STALE_TEXT);
    await waitFor(() => expect(identifiers(table)).toEqual(START));
    expect(orders(table)).toEqual(['1', '2', '3', '4']);
  });

  it('with no answer the row returns to its former place and «Не удалось сохранить перемещение…» shows', async () => {
    serveBoards(dataset());
    server.use(http.post(PROJECT_REORDER_URL, () => HttpResponse.error()));
    renderBoards(projectBacklogAddress());
    const table = await findBacklogTable();

    await userEvent.click(actions('MSK-US-9'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(FAILED_TEXT);
    expect(identifiers(table)).toEqual(START);
    expect(orders(table)).toEqual(['1', '2', '3', '4']);
  });
});

describe('US-2 Scenario: Who may reorder (BR-PB-5)', () => {
  it('shows a member «Ещё действия» with the four moves on a top-level row, a reader none', async () => {
    serveBoards(dataset());
    const member = renderBoards(projectBacklogAddress(), { role: 'member' });
    await findBacklogTable();
    await userEvent.click(actions('MSK-US-3'));
    const menu = await screen.findByRole('menu', { name: 'Ещё действия: MSK-US-3' });
    expect(
      within(menu)
        .getAllByRole('menuitem')
        .slice(0, 4)
        .map((entry) => entry.textContent),
    ).toEqual(['В начало', 'Выше', 'Ниже', 'В конец']);
    member.unmount();

    serveBoards(dataset());
    renderBoards(projectBacklogAddress(), { role: 'viewer' });
    await findBacklogTable();
    expect(screen.queryAllByRole('button', { name: /^Ещё действия/, hidden: true })).toHaveLength(
      0,
    );
  });

  it('a refusal by the scoped check (403) shows its notice at once and the row comes back', async () => {
    serveBoards(dataset());
    serveProblem('post', PROJECT_REORDER_URL, problem('organizations.access.forbidden'));
    renderBoards(projectBacklogAddress());
    const table = await findBacklogTable();

    await userEvent.click(actions('MSK-US-9'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));

    expect(await screen.findByText('Недостаточно прав')).toBeInTheDocument();
    await waitFor(() => expect(identifiers(table)).toEqual(START));
  });
});
