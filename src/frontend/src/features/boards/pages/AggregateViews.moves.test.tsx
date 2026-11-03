import { afterEach, describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { dataTransfer, fireDrag } from '../testDrag';
import { ids, projectDataset } from '../testFixtures';
import { serveBoards } from '../testApi';
import { teamContext } from '../testRender';
import {
  header,
  mskCard,
  mskRow,
  orgAddress,
  renderAggregate,
  serveAggregate,
  serveWrites,
  storyColumns,
} from '../testAggregate';

// feat-048 US-10 (design § 5, § 6, OQ-6 A): the move menu and the drag of the by-project backlog
// with the part-loaded rules, the board's move between columns, and the switch on the shipped
// project pages (OQ-7/OQ-8 A).

afterEach(() => localStorage.clear());

const BY_PROJECT = [header('project', 'p-msk', 'Мастаск', 3, { key: 'MSK' })];

function rowsMenu(identifier: string): HTMLElement {
  const row = screen.getByText(identifier).closest('tr') as HTMLElement;
  return within(row).getByRole('button', { name: 'Ещё действия' });
}

function handle(identifier: string): HTMLElement {
  return document.querySelector(`[data-drag-row="${identifier}"]`) as HTMLElement;
}

describe('US-10 Scenario: the move menu of a by-project backlog group', () => {
  it('sends «Выше» as a reorder of the project and moves the row at once', async () => {
    serveAggregate({
      groups: { project: BY_PROJECT },
      rowsOf: () => [mskRow(1), mskRow(2), mskRow(3)],
    });
    const writes = serveWrites();
    renderAggregate(orgAddress('backlogs'));
    await screen.findByText('MSK-US-2');

    await userEvent.click(rowsMenu('MSK-US-2'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Выше' }));

    await waitFor(() => expect(writes).toHaveLength(1));
    expect(writes[0]?.path).toContain(`/projects/${ids.msk}/backlogs/stories/reorder`);
    expect(writes[0]?.body).toEqual({ itemId: 'row-2', position: 'top' });
    await waitFor(() => {
      const order = screen.getAllByText(/^MSK-US-\d$/).map((cell) => cell.textContent);
      expect(order).toEqual(['MSK-US-2', 'MSK-US-1', 'MSK-US-3']);
    });
  });

  it('on a part-loaded group disables «Ниже» on the last loaded row with its reason, and sends «В конец» as the project’s end', async () => {
    serveAggregate({
      groups: { project: BY_PROJECT },
      rowsOf: () => [mskRow(1), mskRow(2)],
    });
    const writes = serveWrites();
    renderAggregate(orgAddress('backlogs'));
    await screen.findByText('MSK-US-2');

    await userEvent.click(rowsMenu('MSK-US-2'));
    const down = await screen.findByRole('menuitem', { name: /^Ниже/ });
    expect(down).toHaveAttribute('aria-disabled', 'true');
    expect(down).toHaveTextContent('Сначала нажмите «Показать ещё»');
    await userEvent.click(screen.getByRole('menuitem', { name: 'В конец' }));

    await waitFor(() => expect(writes).toHaveLength(1));
    expect(writes[0]?.body).toEqual({ itemId: 'row-2', position: 'bottom' });
    expect(
      await screen.findByText(
        'Элемент перенесён в конец бэклога проекта. Он появится после «Показать ещё».',
      ),
    ).toBeInTheDocument();
    expect(screen.queryByText('MSK-US-2')).not.toBeInTheDocument();
  });

  it('has no move menu and no drag handle without the move permission’s grouping (by team is change-time order)', async () => {
    serveAggregate({
      groups: { team: [header('team', null, null, 3)] },
      rowsOf: () => [mskRow(1), mskRow(2), mskRow(3)],
    });
    localStorage.setItem(`mustask.grouping.v1.user-1.${ids.example}.backlogs.organization`, 'team');
    renderAggregate(orgAddress('backlogs'));
    await screen.findByText('MSK-US-2');

    expect(screen.queryByRole('button', { name: 'Ещё действия' })).not.toBeInTheDocument();
    expect(handle('MSK-US-2')).toBeNull();
    expect(
      screen.getByText(/При этой группировке строки идут по времени изменения/),
    ).toBeInTheDocument();
  });
});

describe('US-10 Scenario: dragging a row of a by-project backlog group', () => {
  it('drops between loaded rows: a drop on the lower half of the last row of a fully loaded group is the bottom', async () => {
    serveAggregate({
      groups: { project: BY_PROJECT },
      rowsOf: () => [mskRow(1), mskRow(2), mskRow(3)],
    });
    const writes = serveWrites();
    renderAggregate(orgAddress('backlogs'));
    await screen.findByText('MSK-US-3');

    const transfer = dataTransfer();
    fireDrag('dragStart', handle('MSK-US-1'), transfer);
    fireDrag('dragOver', handle('MSK-US-3'), transfer, 'lower');
    expect(handle('MSK-US-3')).toHaveAttribute('data-drop-line', 'bottom');
    fireDrag('drop', handle('MSK-US-3'), transfer, 'lower');

    await waitFor(() => expect(writes).toHaveLength(1));
    expect(writes[0]?.body).toEqual({ itemId: 'row-1', position: 'bottom' });
  });

  it('drops after the row above the line, and a line on the row’s own edge sends nothing', async () => {
    serveAggregate({
      groups: { project: BY_PROJECT },
      rowsOf: () => [mskRow(1), mskRow(2), mskRow(3)],
    });
    const writes = serveWrites();
    renderAggregate(orgAddress('backlogs'));
    await screen.findByText('MSK-US-3');

    const own = dataTransfer();
    fireDrag('dragStart', handle('MSK-US-3'), own);
    fireDrag('dragOver', handle('MSK-US-3'), own, 'upper');
    fireDrag('drop', handle('MSK-US-3'), own, 'upper');
    expect(writes).toHaveLength(0);
    fireDrag('dragEnd', handle('MSK-US-3'), own);

    const transfer = dataTransfer();
    fireDrag('dragStart', handle('MSK-US-3'), transfer);
    fireDrag('dragOver', handle('MSK-US-1'), transfer, 'lower');
    fireDrag('drop', handle('MSK-US-1'), transfer, 'lower');
    await waitFor(() => expect(writes).toHaveLength(1));
    expect(writes[0]?.body).toEqual({ itemId: 'row-3', position: 'after', afterItemId: 'row-1' });
  });

  it('on a part-loaded group the end of the loaded rows is not a drop target', async () => {
    serveAggregate({
      groups: { project: BY_PROJECT },
      rowsOf: () => [mskRow(1), mskRow(2)],
    });
    const writes = serveWrites();
    renderAggregate(orgAddress('backlogs'));
    await screen.findByText('MSK-US-2');

    const transfer = dataTransfer();
    fireDrag('dragStart', handle('MSK-US-1'), transfer);
    const allowed = fireDrag('dragOver', handle('MSK-US-2'), transfer, 'lower');
    expect(allowed).toBe(true); // not default-prevented: a drop here is refused by the browser
    expect(handle('MSK-US-2')).not.toHaveAttribute('data-drop-line');
    fireDrag('drop', handle('MSK-US-2'), transfer, 'lower');
    expect(writes).toHaveLength(0);
  });
});

function boardGroups() {
  return {
    project: [header('project', 'p-msk', 'Мастаск', 2, { key: 'MSK', columns: storyColumns() })],
  };
}

describe('US-10 Scenario: the board between columns', () => {
  it('names the columns by the groups read and moves a card with «Переместить в колонку»', async () => {
    const api = serveAggregate({
      groups: boardGroups(),
      cardsOf: (_group, column) => (column === 'Proposed-1' ? [mskCard(1)] : []),
    });
    const writes = serveWrites();
    renderAggregate(orgAddress('boards'));

    const proposed = await screen.findByRole('group', { name: 'Новая' });
    expect(screen.getByRole('group', { name: 'Активна' })).toBeInTheDocument();
    await userEvent.click(await within(proposed).findByRole('button', { name: 'Ещё действия' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Активна' }));

    await waitFor(() => expect(writes).toHaveLength(1));
    expect(writes[0]?.method).toBe('PATCH');
    expect((writes[0]?.body as { stateId: string }).stateId).toBeTruthy();
    await waitFor(() => expect(within(proposed).queryByText('MSK-US-1')).not.toBeInTheDocument());
    // D-3 A: the target cell is read again from its first page.
    await waitFor(() =>
      expect(api.of('cells').filter((r) => r.params.get('column') === 'InProgress-1').length).toBe(
        2,
      ),
    );
  });

  it('moves a card by dragging it into another column of the lane, and not into a column the type has no target in', async () => {
    serveAggregate({
      groups: boardGroups(),
      cardsOf: (_group, column) => (column === 'Proposed-1' ? [mskCard(1)] : []),
    });
    const writes = serveWrites();
    renderAggregate(orgAddress('boards'));
    const proposed = await screen.findByRole('group', { name: 'Новая' });
    const active = screen.getByRole('group', { name: 'Активна' });
    await within(proposed).findByText('MSK-US-1');

    const card = document.querySelector('[data-drag-card="MSK-US-1"]') as HTMLElement;
    const transfer = dataTransfer();
    fireDrag('dragStart', card, transfer);
    // Its own column is no target.
    expect(fireDrag('dragOver', proposed, transfer)).toBe(true);
    expect(fireDrag('dragOver', active, transfer)).toBe(false);
    fireDrag('drop', active, transfer);

    await waitFor(() => expect(writes).toHaveLength(1));
    expect(writes[0]?.method).toBe('PATCH');
  });
});

describe('OQ-7/OQ-8 A: the switch on the shipped project pages', () => {
  const PROJECT_BACKLOG = `/organizations/${ids.example}/projects/${ids.msk}/backlogs/stories`;

  it('shows the shipped backlog under «Без группировки» with the switch offering «По команде» only besides', async () => {
    serveBoards(projectDataset());
    serveAggregate({ groups: {} });
    renderAggregate(PROJECT_BACKLOG, { context: teamContext({ teamId: undefined }) });

    const button = await screen.findByRole('button', { name: 'Группировка: Без группировки' });
    await userEvent.click(button);
    expect(screen.getAllByRole('menuitemradio').map((item) => item.textContent)).toEqual([
      'Без группировки',
      'По команде',
    ]);
  });

  it('reads the aggregate groups of the project by team when «По команде» is chosen', async () => {
    serveBoards(projectDataset());
    const api = serveAggregate({
      groups: { team: [header('team', null, null, 2), header('team', 't-1', 'Платформа', 1)] },
      rowsOf: () => [mskRow(1)],
    });
    renderAggregate(PROJECT_BACKLOG, { context: teamContext({ teamId: undefined }) });

    await userEvent.click(
      await screen.findByRole('button', { name: 'Группировка: Без группировки' }),
    );
    await userEvent.click(screen.getByRole('menuitemradio', { name: 'По команде' }));

    expect(await screen.findByRole('button', { name: /Без команды/ })).toBeInTheDocument();
    const read = api.of('groups').at(-1);
    expect(read?.params.get('grouping')).toBe('team');
    expect(read?.params.get('projectId')).toBe(ids.msk);
  });
});
