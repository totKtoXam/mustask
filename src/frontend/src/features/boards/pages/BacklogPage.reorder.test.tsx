import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { serveBoards, type BoardsTestApi } from '../testApi';
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

async function backlogTable() {
  return screen.findByRole('table', { name: 'Бэклог' });
}

function rowsOf(table: HTMLElement): HTMLElement[] {
  return within(table).getAllByRole('row').slice(1);
}

function cellText(row: HTMLElement, index: number): string {
  return within(row).getAllByRole('cell')[index]?.textContent ?? '';
}

function identifiers(table: HTMLElement): string[] {
  return rowsOf(table).map((row) => cellText(row, 2));
}

function orders(table: HTMLElement): string[] {
  return rowsOf(table).map((row) => cellText(row, 0));
}

function rowOf(table: HTMLElement, identifier: string): HTMLElement {
  const row = rowsOf(table).find((candidate) => cellText(candidate, 2) === identifier);
  if (!row) throw new Error(`${identifier} is not a row.`);
  return row;
}

/** A row's handle; found while an open menu hides the page from the accessibility tree too. */
function handle(identifier: string): HTMLElement {
  return screen.getByRole('button', { name: `Переместить ${identifier}`, hidden: true });
}

/** The page's announcement region: the `role="status"` that is not the skeleton. */
function announcer(): HTMLElement {
  const region = screen
    .getAllByRole('status')
    .find((element) => !element.hasAttribute('aria-label'));
  if (!region) throw new Error('No announcement region.');
  return region;
}

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

/** Drags `identifier` by its handle and drops it on `target`'s `half`. */
async function dragAndDrop(
  table: HTMLElement,
  identifier: string,
  target: string,
  half: 'upper' | 'lower',
) {
  const transfer = dataTransfer();
  const source = handle(identifier);
  const over = within(rowOf(table, target)).getAllByRole('cell')[4] as HTMLElement;
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
    const table = await backlogTable();
    expect(identifiers(table)).toEqual(START);

    const transfer = dataTransfer();
    const source = handle('MSK-US-11');
    expect(source).toHaveAttribute('draggable', 'true');
    const over = within(rowOf(table, 'MSK-BUG-5')).getAllByRole('cell')[4] as HTMLElement;
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
    expect(handle('MSK-US-11')).not.toHaveFocus();
    await act(async () => fireDrag('drop', over, transfer, 'upper'));
    await act(async () => fireDrag('dragEnd', source, transfer));

    // At once, while the reorder is saved.
    expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-US-11', 'MSK-BUG-5', 'MSK-US-9']);
    expect(orders(table)).toEqual(['1', '2', '3', '4']);
    expect(rowsOf(table).some((row) => row.hasAttribute('data-drop-line'))).toBe(false);
    expect(handle('MSK-US-11')).toHaveFocus();
    expect(announcer()).toHaveTextContent('MSK-US-11 перемещён: позиция 2 из 4.');

    release();
    await waitFor(() => expect(handle('MSK-US-11')).not.toHaveAttribute('aria-disabled'));
    expect(reorders(api).map((request) => request.body)).toEqual([
      { itemId: api.item(ids.msk, 11).id, position: 'after', afterItemId: api.item(ids.msk, 3).id },
    ]);
    expect(api.reads('/backlogs/stories')).toHaveLength(2);
    expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-US-11', 'MSK-BUG-5', 'MSK-US-9']);

    // A reload: a fresh page reads the stored order.
    view.unmount();
    renderBoards(backlogAddress());
    expect(identifiers(await backlogTable())).toEqual([
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
    const table = await backlogTable();

    await dragAndDrop(table, 'MSK-US-9', 'MSK-US-3', 'upper');
    await waitFor(() =>
      expect(identifiers(table)).toEqual(['MSK-US-9', 'MSK-US-3', 'MSK-BUG-5', 'MSK-US-11']),
    );
    await waitFor(() => expect(handle('MSK-US-9')).not.toHaveAttribute('aria-disabled'));

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
    const table = await backlogTable();

    const transfer = dataTransfer();
    const over = within(rowOf(table, 'MSK-US-9')).getAllByRole('cell')[4] as HTMLElement;
    await act(async () => fireDrag('dragStart', handle('MSK-US-9'), transfer));
    await act(async () => fireDrag('dragOver', over, transfer, 'lower'));
    expect(rowsOf(table).some((row) => row.hasAttribute('data-drop-line'))).toBe(false);
    await act(async () => fireDrag('drop', over, transfer, 'lower'));

    expect(identifiers(table)).toEqual(START);
    expect(reorders(api)).toEqual([]);
  });

  it('a drag ended without a drop — Escape, or a release off the table — moves nothing', async () => {
    const api = serveBoards(storiesDataset());
    renderBoards(backlogAddress());
    const table = await backlogTable();

    const transfer = dataTransfer();
    const source = handle('MSK-US-11');
    const over = within(rowOf(table, 'MSK-US-3')).getAllByRole('cell')[4] as HTMLElement;
    await act(async () => fireDrag('dragStart', source, transfer));
    await act(async () => fireDrag('dragOver', over, transfer, 'upper'));
    expect(rowOf(table, 'MSK-US-3')).toHaveAttribute('data-drop-line', 'top');
    await act(async () => fireDrag('dragEnd', source, transfer));

    expect(rowsOf(table).some((row) => row.hasAttribute('data-drop-line'))).toBe(false);
    expect(identifiers(table)).toEqual(START);
    expect(reorders(api)).toEqual([]);
  });
});

describe('US-2 Scenario Outline: The handle’s menu moves a row by keyboard', () => {
  it.each([
    ['В начало', ['MSK-BUG-5', 'MSK-US-3', 'MSK-US-9', 'MSK-US-11'], 1],
    ['Выше', ['MSK-BUG-5', 'MSK-US-3', 'MSK-US-9', 'MSK-US-11'], 1],
    ['Ниже', ['MSK-US-3', 'MSK-US-9', 'MSK-BUG-5', 'MSK-US-11'], 3],
    ['В конец', ['MSK-US-3', 'MSK-US-9', 'MSK-US-11', 'MSK-BUG-5'], 4],
  ])(
    'Enter on «Переместить MSK-BUG-5», «%s»: the rows read %j, focus stays, position %i is announced',
    async (entry, order, position) => {
      const api = serveBoards(storiesDataset());
      renderBoards(backlogAddress());
      const table = await backlogTable();
      expect(announcer().textContent).toBe('');

      handle('MSK-BUG-5').focus();
      await userEvent.keyboard('{Enter}');
      const menu = await screen.findByRole('menu', { name: 'Переместить MSK-BUG-5' });
      expect(handle('MSK-BUG-5')).toHaveAttribute('aria-expanded', 'true');
      const choice = within(menu).getByRole('menuitem', { name: entry });
      choice.focus();
      expect(handle('MSK-BUG-5')).not.toHaveFocus();
      await userEvent.keyboard('{Enter}');

      expect(identifiers(table)).toEqual(order);
      expect(orders(table)).toEqual(['1', '2', '3', '4']);
      await waitFor(() => expect(handle('MSK-BUG-5')).toHaveFocus());
      expect(announcer()).toHaveTextContent(`MSK-BUG-5 перемещён: позиция ${position} из 4.`);

      await waitFor(() => expect(handle('MSK-BUG-5')).not.toHaveAttribute('aria-disabled'));
      expect(identifiers(table)).toEqual(order);
      expect(handle('MSK-BUG-5')).toHaveFocus();
      expect(api.order(ids.platformTeam)).toEqual(order);
      expect(screen.queryByRole('alert')).toBeNull();
    },
  );
});

describe('US-2 Scenario: Entries that would not move a row are disabled', () => {
  it('«В начало» and «Выше» on the first row, «Ниже» and «В конец» on the last', async () => {
    serveBoards(storiesDataset());
    renderBoards(backlogAddress());
    await backlogTable();

    const disabled = (menu: HTMLElement) =>
      within(menu)
        .getAllByRole('menuitem')
        .filter((entry) => entry.getAttribute('aria-disabled') === 'true')
        .map((entry) => entry.textContent);

    await userEvent.click(handle('MSK-US-3'));
    const first = await screen.findByRole('menu', { name: 'Переместить MSK-US-3' });
    expect(
      within(first)
        .getAllByRole('menuitem')
        .map((entry) => entry.textContent),
    ).toEqual(['В начало', 'Выше', 'Ниже', 'В конец']);
    expect(disabled(first)).toEqual(['В начало', 'Выше']);
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
    expect(handle('MSK-US-3')).toHaveFocus();

    await userEvent.click(handle('MSK-US-11'));
    const last = await screen.findByRole('menu', { name: 'Переместить MSK-US-11' });
    expect(disabled(last)).toEqual(['Ниже', 'В конец']);
  });
});

describe('US-2 Scenario: Reordering changes nothing on the work item', () => {
  it('sends the reorder alone — no save of the item — and leaves its «Изменён» time', async () => {
    const api = serveBoards(storiesDataset());
    renderBoards(backlogAddress());
    await backlogTable();
    const changedAt = api.item(ids.msk, 11).changedAt;

    await userEvent.click(handle('MSK-US-11'));
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
    const table = await backlogTable();

    await userEvent.click(handle('MSK-US-11'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));
    await waitFor(() => expect(handle('MSK-US-11')).not.toHaveAttribute('aria-disabled'));
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
    const table = await backlogTable();

    await userEvent.click(handle('MSK-US-9'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));
    await waitFor(() => expect(handle('MSK-US-9')).not.toHaveAttribute('aria-disabled'));
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
    const table = await backlogTable();
    // MSK-BUG-5's Область is set to «Мастаск» on its form meanwhile.
    api.item(ids.msk, 5).areaId = ids.mskRoot;

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
    await backlogTable();
    api.item(ids.msk, 11).areaId = ids.reports;

    await userEvent.click(handle('MSK-US-11'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(STALE_TEXT);
    expect(identifiers(await backlogTable())).toEqual(['MSK-US-3', 'MSK-BUG-5', 'MSK-US-9']);
    expect(api.data.places).toEqual([]);
  });
});

describe('US-2 Scenario: A failed save puts the row back', () => {
  it('with no answer the row returns to its former place and «Не удалось сохранить перемещение…» shows', async () => {
    serveBoards(storiesDataset());
    server.use(http.post(REORDER_URL, () => HttpResponse.error()));
    renderBoards(backlogAddress());
    const table = await backlogTable();

    await userEvent.click(handle('MSK-US-11'));
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
    const table = await backlogTable();

    await userEvent.click(handle('MSK-US-11'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(FAILED_TEXT);
    expect(identifiers(table)).toEqual(START);

    await waitFor(() => expect(handle('MSK-US-11')).not.toHaveAttribute('aria-disabled'));
    await userEvent.click(handle('MSK-US-11'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));
    await waitFor(() =>
      expect(screen.getByRole('alert')).toHaveTextContent('Проверьте правильность запроса.'),
    );
    expect(identifiers(table)).toEqual(START);
  });

  it('a team deleted meanwhile shows no notice: the page is replaced by «Команда не найдена»', async () => {
    const api = serveBoards(storiesDataset());
    renderBoards(backlogAddress());
    await backlogTable();
    api.data.teams = api.data.teams.map((team) =>
      team.id === ids.platformTeam ? { ...team, active: false } : team,
    );

    await userEvent.click(handle('MSK-US-11'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Команда не найдена' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('alert')).toBeNull();
  });
});

describe('US-2 Scenario: One move at a time', () => {
  it('while a reorder is saved every handle is off and no row can be dragged, until the backlog is read again', async () => {
    const api = serveBoards(storiesDataset());
    const { release } = holdReorders();
    renderBoards(backlogAddress());
    const table = await backlogTable();

    await userEvent.click(handle('MSK-US-11'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));

    for (const identifier of START) {
      expect(handle(identifier)).toHaveAttribute('aria-disabled', 'true');
      expect(handle(identifier)).toHaveAttribute('draggable', 'false');
    }
    // Neither the menu nor a drag starts another move.
    await userEvent.click(handle('MSK-US-3'));
    expect(screen.queryByRole('menu')).toBeNull();
    const transfer = dataTransfer();
    const over = within(rowOf(table, 'MSK-US-3')).getAllByRole('cell')[4] as HTMLElement;
    let started = true;
    await act(async () => {
      started = fireDrag('dragStart', handle('MSK-US-9'), transfer);
    });
    expect(started).toBe(false);
    await act(async () => fireDrag('dragOver', over, transfer, 'upper'));
    expect(rowsOf(table).some((row) => row.hasAttribute('data-drop-line'))).toBe(false);
    await act(async () => fireDrag('drop', over, transfer, 'upper'));
    const readsWhileSaving = api.reads('/backlogs/stories').length;

    release();
    await waitFor(() => expect(handle('MSK-US-3')).not.toHaveAttribute('aria-disabled'));
    expect(reorders(api)).toHaveLength(1);
    expect(api.reads('/backlogs/stories')).toHaveLength(readsWhileSaving + 1);
    for (const identifier of START) {
      expect(handle(identifier)).toHaveAttribute('draggable', 'true');
    }
    expect(identifiers(table)).toEqual(['MSK-US-11', 'MSK-US-3', 'MSK-BUG-5', 'MSK-US-9']);
  });
});
