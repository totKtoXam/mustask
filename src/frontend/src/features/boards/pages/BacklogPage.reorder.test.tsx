import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { serveBoards, type BoardsTestApi } from '../testApi';
import {
  CELL,
  actions,
  announcer,
  cellOf,
  findBacklogTable,
  identifiers,
  orders,
  rowOf,
  rowsOf,
} from '../testBacklog';
import { dataTransfer, fireDrag } from '../testDrag';
import { REORDER_URL, catalogText, ids, storiesDataset } from '../testFixtures';
import { backlogAddress, renderBoards } from '../testRender';

// US-2: «Платформа»'s «Истории» backlog reads MSK-US-3, MSK-BUG-5, MSK-US-9,
// MSK-US-11. The page is Анна's; Борис's moves are the in-memory API's own,
// as another user's request would be taken.

const START = ['MSK-US-3', 'MSK-BUG-5', 'MSK-US-9', 'MSK-US-11'];
const STALE_TEXT = catalogText('boards.backlog.reorder_stale');
const FAILED_TEXT = 'Не удалось сохранить перемещение. Попробуйте ещё раз.';

afterEach(() => {
  focusManager.setFocused(undefined);
});

/** Holds every reorder until `release()`, then lets the in-memory API take it. */
function holdReorders() {
  let release: () => void = () => {};
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  server.use(
    http.post(REORDER_URL, async () => {
      await held;
      return undefined;
    }),
  );
  return { release };
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
  return transfer;
}

describe('US-2 Scenario: A drag between two rows persists', () => {
  it('drops MSK-US-11 between MSK-US-3 and MSK-BUG-5: the order at once, «Порядок» 1 to 4, and the same after a reload', async () => {
    const api = serveBoards(storiesDataset());
    const { release } = holdReorders();
    const view = renderBoards(backlogAddress());
    const table = await findBacklogTable();
    expect(identifiers(table)).toEqual(START);

    const transfer = dataTransfer();
    const source = rowOf(table, 'MSK-US-11');
    expect(source).toHaveAttribute('draggable', 'true');
    const over = cellOf(rowOf(table, 'MSK-BUG-5'), CELL.title);
    await act(async () => {
      fireDrag('dragStart', source, transfer);
    });
    expect(transfer.effectAllowed).toBe('move');
    expect(transfer.getData('text/plain')).toBe('MSK-US-11');
    // The upper half of MSK-BUG-5: the 2 px line between it and MSK-US-3.
    let accepted = true;
    await act(async () => {
      accepted = fireDrag('dragOver', over, transfer, 'upper');
    });
    expect(accepted).toBe(false);
    expect(rowOf(table, 'MSK-BUG-5')).toHaveAttribute('data-drop-line', 'top');
    expect(rowsOf(table).filter((row) => row.hasAttribute('data-drop-line'))).toHaveLength(1);
    expect(actions('MSK-US-11')).not.toHaveFocus();
    await act(async () => fireDrag('drop', over, transfer, 'upper'));
    await act(async () => fireDrag('dragEnd', source, transfer));

    // At once, while the reorder is saved.
    expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-US-11', 'MSK-BUG-5', 'MSK-US-9']);
    expect(orders(table)).toEqual(['1', '2', '3', '4']);
    expect(rowsOf(table).some((row) => row.hasAttribute('data-drop-line'))).toBe(false);
    expect(actions('MSK-US-11')).toHaveFocus();
    expect(announcer()).toHaveTextContent('MSK-US-11 перемещён: позиция 2 из 4.');

    release();
    await waitFor(() => expect(actions('MSK-US-11')).not.toHaveAttribute('aria-disabled'));
    expect(reorders(api).map((request) => request.body)).toEqual([
      { itemId: api.item(ids.msk, 11).id, position: 'after', afterItemId: api.item(ids.msk, 3).id },
    ]);
    expect(api.reads('/backlogs/stories')).toHaveLength(2);
    expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-US-11', 'MSK-BUG-5', 'MSK-US-9']);

    // A reload: a fresh page reads the stored order.
    view.unmount();
    renderBoards(backlogAddress());
    expect(identifiers(await findBacklogTable())).toEqual([
      'MSK-US-3',
      'MSK-US-11',
      'MSK-BUG-5',
      'MSK-US-9',
    ]);
  });
});

describe('US-2 Scenario: A drop at either end', () => {
  it('drops MSK-US-9 above the first row, then below the last row: it is first, then last', async () => {
    const api = serveBoards(storiesDataset());
    renderBoards(backlogAddress());
    const table = await findBacklogTable();

    await dragAndDrop(table, 'MSK-US-9', 'MSK-US-3', 'upper');
    await waitFor(() =>
      expect(identifiers(table)).toEqual(['MSK-US-9', 'MSK-US-3', 'MSK-BUG-5', 'MSK-US-11']),
    );
    await waitFor(() => expect(actions('MSK-US-9')).not.toHaveAttribute('aria-disabled'));

    await dragAndDrop(table, 'MSK-US-9', 'MSK-US-11', 'lower');
    await waitFor(() =>
      expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-BUG-5', 'MSK-US-11', 'MSK-US-9']),
    );
    expect(reorders(api).map((request) => (request.body as { position: string }).position)).toEqual(
      ['top', 'bottom'],
    );
    expect(api.order(ids.platformTeam)).toEqual(['MSK-US-3', 'MSK-BUG-5', 'MSK-US-11', 'MSK-US-9']);
  });

  it('a drop on the dragged row’s own edge moves nothing, draws no line and sends nothing', async () => {
    const api = serveBoards(storiesDataset());
    renderBoards(backlogAddress());
    const table = await findBacklogTable();

    const transfer = dataTransfer();
    const over = cellOf(rowOf(table, 'MSK-US-9'), CELL.title);
    await act(async () => fireDrag('dragStart', rowOf(table, 'MSK-US-9'), transfer));
    await act(async () => fireDrag('dragOver', over, transfer, 'lower'));
    expect(rowsOf(table).some((row) => row.hasAttribute('data-drop-line'))).toBe(false);
    await act(async () => fireDrag('drop', over, transfer, 'lower'));

    expect(identifiers(table)).toEqual(START);
    expect(reorders(api)).toEqual([]);
  });

  it('a drag ended without a drop — Escape, or a release off the table — moves nothing', async () => {
    const api = serveBoards(storiesDataset());
    renderBoards(backlogAddress());
    const table = await findBacklogTable();

    const transfer = dataTransfer();
    const source = rowOf(table, 'MSK-US-11');
    const over = cellOf(rowOf(table, 'MSK-US-3'), CELL.title);
    await act(async () => fireDrag('dragStart', source, transfer));
    await act(async () => fireDrag('dragOver', over, transfer, 'upper'));
    expect(rowOf(table, 'MSK-US-3')).toHaveAttribute('data-drop-line', 'top');
    await act(async () => fireDrag('dragEnd', source, transfer));

    expect(rowsOf(table).some((row) => row.hasAttribute('data-drop-line'))).toBe(false);
    expect(identifiers(table)).toEqual(START);
    expect(reorders(api)).toEqual([]);
  });
});

describe('US-2 Scenario Outline: «Ещё действия» moves a row by keyboard (feat-027 BR-T-13: the handle’s menu moved into «⋮»)', () => {
  it.each([
    ['В начало', ['MSK-BUG-5', 'MSK-US-3', 'MSK-US-9', 'MSK-US-11'], 1],
    ['Выше', ['MSK-BUG-5', 'MSK-US-3', 'MSK-US-9', 'MSK-US-11'], 1],
    ['Ниже', ['MSK-US-3', 'MSK-US-9', 'MSK-BUG-5', 'MSK-US-11'], 3],
    ['В конец', ['MSK-US-3', 'MSK-US-9', 'MSK-US-11', 'MSK-BUG-5'], 4],
  ])(
    'Enter on «Ещё действия: MSK-BUG-5», «%s»: the rows read %j, focus returns to «⋮», position %i is announced',
    async (entry, order, position) => {
      const api = serveBoards(storiesDataset());
      renderBoards(backlogAddress());
      const table = await findBacklogTable();
      expect(announcer().textContent).toBe('');

      actions('MSK-BUG-5').focus();
      await userEvent.keyboard('{Enter}');
      const menu = await screen.findByRole('menu', { name: 'Ещё действия: MSK-BUG-5' });
      expect(actions('MSK-BUG-5')).toHaveAttribute('aria-expanded', 'true');
      const choice = within(menu).getByRole('menuitem', { name: entry });
      choice.focus();
      expect(actions('MSK-BUG-5')).not.toHaveFocus();
      await userEvent.keyboard('{Enter}');

      expect(identifiers(table)).toEqual(order);
      expect(orders(table)).toEqual(['1', '2', '3', '4']);
      await waitFor(() => expect(actions('MSK-BUG-5')).toHaveFocus());
      expect(announcer()).toHaveTextContent(`MSK-BUG-5 перемещён: позиция ${position} из 4.`);

      await waitFor(() => expect(actions('MSK-BUG-5')).not.toHaveAttribute('aria-disabled'));
      expect(identifiers(table)).toEqual(order);
      expect(actions('MSK-BUG-5')).toHaveFocus();
      expect(api.order(ids.platformTeam)).toEqual(order);
      expect(screen.queryByRole('alert')).toBeNull();
    },
  );
});

describe('US-2 Scenario: Entries that would not move a row are disabled', () => {
  it('«В начало» and «Выше» on the first row, «Ниже» and «В конец» on the last, under the subheader «Переместить»', async () => {
    serveBoards(storiesDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();

    const disabled = (menu: HTMLElement) =>
      within(menu)
        .getAllByRole('menuitem')
        .filter((entry) => entry.getAttribute('aria-disabled') === 'true')
        .map((entry) => entry.textContent);

    await userEvent.click(actions('MSK-US-3'));
    const first = await screen.findByRole('menu', { name: 'Ещё действия: MSK-US-3' });
    expect(
      within(first)
        .getAllByRole('menuitem')
        .map((entry) => entry.textContent),
    ).toEqual(['В начало', 'Выше', 'Ниже', 'В конец', 'Изменить родителя…', 'Убрать родителя']);
    // «Убрать родителя» waits for a parent: MSK-US-3 has none.
    expect(disabled(first)).toEqual(['В начало', 'Выше', 'Убрать родителя']);
    expect(within(first).getByText('Переместить')).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
    expect(actions('MSK-US-3')).toHaveFocus();

    await userEvent.click(actions('MSK-US-11'));
    const last = await screen.findByRole('menu', { name: 'Ещё действия: MSK-US-11' });
    expect(disabled(last)).toEqual(['Ниже', 'В конец', 'Убрать родителя']);
  });

  it('the four moves keep «Переместить» as their description, so a screen reader hears what «Выше» is about, and the E2E names are unchanged (F-12)', async () => {
    serveBoards(storiesDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();

    await userEvent.click(actions('MSK-US-9'));
    const menu = await screen.findByRole('menu', { name: 'Ещё действия: MSK-US-9' });

    for (const name of ['В начало', 'Выше', 'Ниже', 'В конец']) {
      const move = within(menu).getByRole('menuitem', { name });
      expect(move).toHaveAccessibleName(name);
      expect(move).toHaveAccessibleDescription('Переместить');
    }
    // The parent entries are not moves.
    expect(within(menu).getByRole('menuitem', { name: 'Изменить родителя…' })).not.toHaveAttribute(
      'aria-describedby',
    );
  });
});

describe('US-2 Scenario: Reordering changes nothing on the work item', () => {
  it('sends the reorder alone — no save of the item — and leaves its «Изменён» time', async () => {
    const api = serveBoards(storiesDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();
    const changedAt = api.item(ids.msk, 11).changedAt;

    await userEvent.click(actions('MSK-US-11'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));
    await waitFor(() => expect(api.order(ids.platformTeam)[0]).toBe('MSK-US-11'));

    expect(api.writes().map((request) => request.method)).toEqual(['POST']);
    expect(api.item(ids.msk, 11).changedAt).toBe(changedAt);
  });
});

describe('US-2 Scenario: Two users reorder different items, both moves count (and US-7 Scenario: Nothing is pushed to an open page)', () => {
  it('Анна moves MSK-US-11 to the top, Борис drops MSK-US-3 below MSK-US-9: no notice, and Анна sees both once her page is read again', async () => {
    const api = serveBoards(storiesDataset());
    renderBoards(backlogAddress());
    const table = await findBacklogTable();

    await userEvent.click(actions('MSK-US-11'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));
    await waitFor(() => expect(actions('MSK-US-11')).not.toHaveAttribute('aria-disabled'));
    expect(identifiers(table)).toEqual(['MSK-US-11', 'MSK-US-3', 'MSK-BUG-5', 'MSK-US-9']);

    // Борис, whose page still shows the old order.
    api.reorderAsAnotherUser(ids.platformTeam, 'stories', {
      itemId: api.item(ids.msk, 3).id,
      position: 'after',
      afterItemId: api.item(ids.msk, 9).id,
    });
    expect(api.order(ids.platformTeam)).toEqual(['MSK-US-11', 'MSK-BUG-5', 'MSK-US-9', 'MSK-US-3']);

    // Nothing is pushed: Анна's page is unchanged and shows no notice.
    const reads = api.reads('/backlogs/stories').length;
    expect(identifiers(table)).toEqual(['MSK-US-11', 'MSK-US-3', 'MSK-BUG-5', 'MSK-US-9']);
    expect(screen.queryByRole('alert')).toBeNull();

    // She returns to the browser tab: the backlog is read again (BR-M4-28).
    act(() => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });
    await waitFor(() =>
      expect(identifiers(table)).toEqual(['MSK-US-11', 'MSK-BUG-5', 'MSK-US-9', 'MSK-US-3']),
    );
    expect(api.reads('/backlogs/stories')).toHaveLength(reads + 1);
    expect(screen.queryByRole('alert')).toBeNull();
  });
});

describe('US-2 Scenario: Two users move the same item, the later decides', () => {
  it('Анна moves MSK-US-9 to the top, Борис then to the bottom: it is last, the others keep their order, no notice', async () => {
    const api = serveBoards(storiesDataset());
    renderBoards(backlogAddress());
    const table = await findBacklogTable();

    await userEvent.click(actions('MSK-US-9'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));
    await waitFor(() => expect(actions('MSK-US-9')).not.toHaveAttribute('aria-disabled'));
    expect(identifiers(table)).toEqual(['MSK-US-9', 'MSK-US-3', 'MSK-BUG-5', 'MSK-US-11']);
    api.reorderAsAnotherUser(ids.platformTeam, 'stories', {
      itemId: api.item(ids.msk, 9).id,
      position: 'bottom',
    });

    act(() => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });
    await waitFor(() =>
      expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-BUG-5', 'MSK-US-11', 'MSK-US-9']),
    );
    expect(screen.queryByRole('alert')).toBeNull();
  });
});

describe('US-2 Scenario: A drop next to an item that has left is refused', () => {
  it('reads the backlog again first — without MSK-BUG-5 — then shows the stale notice; no place changes', async () => {
    const api = serveBoards(storiesDataset());
    renderBoards(backlogAddress());
    const table = await findBacklogTable();
    // MSK-BUG-5's «Команда» is set to «Мастаск» on its form meanwhile (mod-017 BR-OT-13).
    api.giveTeam(ids.msk, 5, ids.mskTeam);

    await dragAndDrop(table, 'MSK-US-11', 'MSK-BUG-5', 'lower');

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(STALE_TEXT);
    // The read came first: the rows already lack MSK-BUG-5 when the notice shows.
    expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-US-9', 'MSK-US-11']);
    expect(api.data.places).toEqual([]);
  });

  it('the same when the moved item itself has left the backlog', async () => {
    const api = serveBoards(storiesDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();
    api.giveTeam(ids.msk, 11, ids.mskTeam);

    await userEvent.click(actions('MSK-US-11'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(STALE_TEXT);
    expect(identifiers(await findBacklogTable())).toEqual(['MSK-US-3', 'MSK-BUG-5', 'MSK-US-9']);
    expect(api.data.places).toEqual([]);
  });
});

describe('US-2 Scenario: A failed save puts the row back', () => {
  it('with no answer the row returns to its former place and «Не удалось сохранить перемещение…» shows', async () => {
    serveBoards(storiesDataset());
    server.use(http.post(REORDER_URL, () => HttpResponse.error()));
    renderBoards(backlogAddress());
    const table = await findBacklogTable();

    await userEvent.click(actions('MSK-US-11'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(FAILED_TEXT);
    expect(identifiers(table)).toEqual(START);
    expect(orders(table)).toEqual(['1', '2', '3', '4']);
  });

  it('an answer without a code shows the same text; any other code its own `detail`', async () => {
    serveBoards(storiesDataset());
    // MSW takes the handlers of one `use` in the order given: the 500 first.
    server.use(
      http.post(REORDER_URL, () => HttpResponse.json({ status: 500 }, { status: 500 }), {
        once: true,
      }),
      http.post(
        REORDER_URL,
        () =>
          HttpResponse.json(
            {
              status: 400,
              title: 'boards.validation_failed',
              detail: catalogText('boards.validation_failed'),
              errorCode: 'boards.validation_failed',
            },
            { status: 400 },
          ),
        { once: true },
      ),
    );
    renderBoards(backlogAddress());
    const table = await findBacklogTable();

    await userEvent.click(actions('MSK-US-11'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(FAILED_TEXT);
    expect(identifiers(table)).toEqual(START);

    await waitFor(() => expect(actions('MSK-US-11')).not.toHaveAttribute('aria-disabled'));
    await userEvent.click(actions('MSK-US-11'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));
    await waitFor(() =>
      expect(screen.getByRole('alert')).toHaveTextContent('Проверьте правильность запроса.'),
    );
    expect(identifiers(table)).toEqual(START);
  });

  it('a team deleted meanwhile shows no notice: the page is replaced by «Команда не найдена»', async () => {
    const api = serveBoards(storiesDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();
    api.data.teams = api.data.teams.map((team) =>
      team.id === ids.platformTeam ? { ...team, active: false } : team,
    );

    await userEvent.click(actions('MSK-US-11'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Команда не найдена' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('alert')).toBeNull();
  });
});

describe('US-2 Scenario: One move at a time', () => {
  it('while a reorder is saved every «⋮» is off and no row can be dragged, until the backlog is read again', async () => {
    const api = serveBoards(storiesDataset());
    const { release } = holdReorders();
    renderBoards(backlogAddress());
    const table = await findBacklogTable();

    await userEvent.click(actions('MSK-US-11'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));

    for (const identifier of START) {
      expect(actions(identifier)).toHaveAttribute('aria-disabled', 'true');
      expect(rowOf(table, identifier)).toHaveAttribute('draggable', 'false');
    }
    // Neither the menu nor a drag starts another move.
    await userEvent.click(actions('MSK-US-3'));
    expect(screen.queryByRole('menu')).toBeNull();
    const transfer = dataTransfer();
    const over = cellOf(rowOf(table, 'MSK-US-3'), CELL.title);
    let started = true;
    await act(async () => {
      started = fireDrag('dragStart', rowOf(table, 'MSK-US-9'), transfer);
    });
    expect(started).toBe(false);
    await act(async () => fireDrag('dragOver', over, transfer, 'upper'));
    expect(rowsOf(table).some((row) => row.hasAttribute('data-drop-line'))).toBe(false);
    await act(async () => fireDrag('drop', over, transfer, 'upper'));
    const readsWhileSaving = api.reads('/backlogs/stories').length;

    release();
    await waitFor(() => expect(actions('MSK-US-3')).not.toHaveAttribute('aria-disabled'));
    expect(reorders(api)).toHaveLength(1);
    expect(api.reads('/backlogs/stories')).toHaveLength(readsWhileSaving + 1);
    for (const identifier of START) {
      expect(rowOf(table, identifier)).toHaveAttribute('draggable', 'true');
    }
    expect(identifiers(table)).toEqual(['MSK-US-11', 'MSK-US-3', 'MSK-BUG-5', 'MSK-US-9']);
  });
});
