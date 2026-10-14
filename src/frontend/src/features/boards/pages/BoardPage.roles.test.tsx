import { describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { servedFor, type TestRole } from '../../../test/organizationRoles';
import { organizationsKeys } from '../../organizations/api/queryKeys';
import { WORK_ITEM_URL } from '../../workitems/testFixtures';
import { serveBoards } from '../testApi';
import { cardMenuButton, cardOf, columnBox, findBoard } from '../testBoard';
import { dataTransfer, fireDrag } from '../testDrag';
import {
  API,
  PROBLEM_HEADERS,
  boardsDataset,
  ids,
  platformItem,
  problem,
  stateId,
  storiesDataset,
} from '../testFixtures';
import { boardAddress, renderBoards } from '../testRender';

// mod-016 design § 6.2, § 6.3, § 6.4 (plan-frontend § 3.5): on the board
// «Новый элемент», the card menu «Ещё действия: <id>» and dragging a card need
// `workitems.workitem.write`. A reader keeps the columns and the cards with their
// title links; «Канбан пуст» keeps its hint for everyone and its action only for
// holders.

function activeDataset() {
  const data = boardsDataset();
  data.items.push(
    platformItem(2, 'US', 'Вход по паролю', {
      stateId: stateId('US', 'Активна'),
      assigneeId: ids.anna,
    }),
    platformItem(3, 'US', 'Выход'),
  );
  return data;
}

const ACCESS_URL = `${API}/api/organizations/:organizationId/access`;
const EMPTY_HINT = 'Новые рабочие элементы этого уровня появятся в колонке «Новая».';

describe.each([
  ['admin', true],
  ['member', true],
  ['viewer', false],
] as const)('mod-016 design § 6.2 Scenario: the board for the %s', (role: TestRole, writes) => {
  it(
    writes
      ? 'draws «Новый элемент» in the first column and «Ещё действия: <id>» on every card, the cards draggable'
      : 'draws no «Новый элемент», no «Ещё действия» and no draggable card; the columns, the cards and their title links stay',
    async () => {
      serveBoards(activeDataset());
      renderBoards(boardAddress(), { role });
      const board = await findBoard();

      const create = within(board).queryAllByRole('button', { name: 'Новый элемент' });
      expect(create).toHaveLength(writes ? 1 : 0);
      for (const identifier of ['MSK-US-2', 'MSK-US-3']) {
        const card = cardOf(board, identifier);
        expect(within(card).queryAllByRole('button', { name: /^Ещё действия/ })).toHaveLength(
          writes ? 1 : 0,
        );
        expect(card).toHaveAttribute('draggable', writes ? 'true' : 'false');
        expect(within(card).getByRole('link')).toHaveAttribute('href');
      }
      expect(within(columnBox(board, 'Активна')).getByRole('heading', { level: 2 })).toBeVisible();
    },
  );

  it(
    writes
      ? '«Канбан пуст» keeps its hint and gets the frame’s action «Новый рабочий элемент»'
      : '«Канбан пуст» keeps its hint and has no action',
    async () => {
      serveBoards(storiesDataset());
      const shell = renderBoards(boardAddress('epics'), { role });

      expect(await screen.findByRole('heading', { level: 2, name: 'Канбан пуст' })).toBeVisible();
      expect(screen.getByText(EMPTY_HINT)).toBeVisible();
      // The process is read after the frame: the primary and the action wait for it.
      await waitFor(() => expect(shell.queryClient.isFetching()).toBe(0));
      expect(screen.queryAllByRole('button', { name: 'Новый рабочий элемент' })).toHaveLength(
        writes ? 1 : 0,
      );
    },
  );
});

describe('mod-016 design § 6.2 Scenario: a card’s moves', () => {
  it('a member moves a card from its menu', async () => {
    serveBoards(activeDataset());
    renderBoards(boardAddress(), { role: 'member' });
    await findBoard();

    await userEvent.click(cardMenuButton('MSK-US-3'));

    const menu = await screen.findByRole('menu', { name: 'Переместить в колонку' });
    expect(within(menu).getAllByRole('menuitem').length).toBeGreaterThan(0);
  });

  it('a card dragged by a reader is refused: no column is offered and nothing is sent', async () => {
    const api = serveBoards(activeDataset());
    renderBoards(boardAddress(), { role: 'viewer' });
    const board = await findBoard();

    const transfer = dataTransfer();
    let started = true;
    await act(async () => {
      started = fireDrag('dragStart', cardOf(board, 'MSK-US-3'), transfer);
    });
    const target = columnBox(board, 'Активна');
    await act(async () => fireDrag('dragOver', target, transfer));
    await act(async () => fireDrag('drop', target, transfer));

    expect(started).toBe(false);
    expect(target).not.toHaveAttribute('data-drop-target');
    expect(api.writes()).toEqual([]);
  });
});

// mod-016 design § 2.3, § 2.4, § 12 ("Notices"): a card move refused for the right is the
// board's error notice; the codes are read again; a changed role replaces that notice
// with the role-change notice, an unchanged role keeps it.

const REFUSED = 'Недостаточно прав';
const ROLE_CHANGED = 'Ваша роль в организации «Пример организации» изменилась: «Читатель».';

function refuseMoves() {
  server.use(
    http.patch(WORK_ITEM_URL, () =>
      HttpResponse.json(problem('organizations.access.forbidden'), {
        status: 403,
        headers: PROBLEM_HEADERS,
      }),
    ),
  );
}

async function moveCardToWork() {
  await userEvent.click(cardMenuButton('MSK-US-3'));
  const menu = await screen.findByRole('menu', { name: 'Переместить в колонку' });
  await userEvent.click(within(menu).getByRole('menuitem', { name: 'Активна' }));
}

describe('mod-016 design § 2.4 Scenario: a refused card move, then the codes are read again', () => {
  it('a changed role: the role-change notice stands alone, the refusal notice is gone and the controls with it', async () => {
    serveBoards(activeDataset());
    renderBoards(boardAddress(), { role: 'member' });
    const board = await findBoard();
    refuseMoves();
    server.use(http.get(ACCESS_URL, () => HttpResponse.json(servedFor('viewer'))));

    await moveCardToWork();

    expect(await screen.findByText(ROLE_CHANGED)).toBeVisible();
    await waitFor(() => expect(screen.queryByText(REFUSED)).not.toBeInTheDocument());
    await waitFor(() =>
      expect(
        within(board).queryByRole('button', { name: 'Новый элемент' }),
      ).not.toBeInTheDocument(),
    );
    expect(screen.getAllByRole('alert')).toHaveLength(1);
  });

  it('an unchanged role: the refusal notice stays and no role-change notice is raised', async () => {
    serveBoards(activeDataset());
    renderBoards(boardAddress(), { role: 'member' });
    await findBoard();
    refuseMoves();

    await moveCardToWork();

    expect(await screen.findByText(REFUSED)).toBeVisible();
    await waitFor(() =>
      expect(screen.queryByText(/Ваша роль в организации/)).not.toBeInTheDocument(),
    );
    expect(screen.getByText(REFUSED)).toBeVisible();
  });
});

describe('mod-016 design § 7 Scenario: a card shows an ex-member assignee like any other', () => {
  it('«Анна Смирнова» left the organization: the card reads her name with no mark', async () => {
    const data = activeDataset();
    const user = data.users.find((candidate) => candidate.id === ids.anna);
    if (user) user.member = false;
    serveBoards(data);
    renderBoards(boardAddress());
    const board = await findBoard();

    const card = cardOf(board, 'MSK-US-2');

    expect(card).toHaveTextContent('Анна Смирнова');
    expect(card).not.toHaveTextContent(/неактивен|не в организации/);
  });
});

describe('mod-016 design § 2.2 Scenario: the card menu goes with the code', () => {
  it('an open card menu closes and the card draws no «Ещё действия» once a re-read shows the reader’s bundle', async () => {
    serveBoards(activeDataset());
    const shell = renderBoards(boardAddress(), { role: 'member' });
    const board = await findBoard();
    await userEvent.click(cardMenuButton('MSK-US-3'));
    expect(await screen.findByRole('menu', { name: 'Переместить в колонку' })).toBeVisible();

    const served = servedFor('viewer');
    act(() => {
      shell.queryClient.setQueryData(organizationsKeys.access(ids.example), {
        kind: 'member',
        role: 'viewer',
        permissions: served.permissions,
      });
    });

    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument());
    expect(
      within(cardOf(board, 'MSK-US-3')).queryByRole('button', { name: /^Ещё действия/ }),
    ).not.toBeInTheDocument();
    expect(within(board).queryByRole('button', { name: 'Новый элемент' })).not.toBeInTheDocument();
  });
});
