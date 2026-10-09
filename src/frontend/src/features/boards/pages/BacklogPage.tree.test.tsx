import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { serveBoards } from '../testApi';
import {
  CELL,
  announcer,
  cellText,
  expandControl,
  findBacklogTable,
  handle,
  identifiers,
  outline,
  parentOf,
  rowOf,
  rowsOf,
} from '../testBacklog';
import { dataTransfer, fireDrag } from '../testDrag';
import { ids, mskRecord, stateId, treeDataset } from '../testFixtures';
import { chooseLevel } from '../testLevels';
import { backlogAddress, mskForm, renderBoards } from '../testRender';

// feat-001 US-4 on US-4's «Data» (`treeDataset`): «Платформа»'s «Эпики» read
// MSK-EPIC-1 and MSK-EPIC-20; MSK-EPIC-1 has MSK-FEAT-8 and MSK-FEAT-6 on
// «Фичи», MSK-FEAT-6 has MSK-US-2 and MSK-BUG-4 on «Истории».

afterEach(() => {
  focusManager.setFocused(undefined);
});

/** Lets the tab lose and regain focus, as the browser reports it. */
async function refocusTab() {
  await act(async () => {
    focusManager.setFocused(false);
  });
  await act(async () => {
    focusManager.setFocused(true);
  });
}

describe('US-4 Scenario: Rows start collapsed, an expand control only where children show', () => {
  it('«Эпики» reads MSK-EPIC-1 and MSK-EPIC-20, collapsed; MSK-EPIC-1 has «Развернуть MSK-EPIC-1», MSK-EPIC-20 none', async () => {
    serveBoards(treeDataset());
    renderBoards(backlogAddress('epics'));

    const table = await findBacklogTable();
    expect(outline(table)).toEqual(['MSK-EPIC-1', 'MSK-EPIC-20']);
    const control = expandControl('MSK-EPIC-1');
    expect(control).toHaveAccessibleName('Развернуть MSK-EPIC-1');
    expect(control).toHaveAttribute('aria-expanded', 'false');
    expect(control.querySelector('[data-testid="ChevronRightIcon"]')).not.toBeNull();
    expect(
      within(rowOf(table, 'MSK-EPIC-20')).queryAllByRole('button', { name: /Развернуть/ }),
    ).toEqual([]);
    // The control sits in «Заголовок» before the title link.
    expect(
      within(rowsOf(table)[0] as HTMLElement).getAllByRole('cell')[CELL.title],
    ).toContainElement(control);
  });
});

describe('US-4 Scenario: Expanding shows the children of the level below in their order', () => {
  it('MSK-FEAT-8 and MSK-FEAT-6 beneath MSK-EPIC-1, one tier in, no «Порядок» or handle, «Родитель» MSK-EPIC-1; the control reads «Свернуть», focus kept', async () => {
    serveBoards(treeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    await userEvent.click(expandControl('MSK-EPIC-1'));

    expect(outline(table)).toEqual(['MSK-EPIC-1', '  MSK-FEAT-8', '  MSK-FEAT-6', 'MSK-EPIC-20']);
    for (const child of ['MSK-FEAT-8', 'MSK-FEAT-6']) {
      const row = rowOf(table, child);
      expect(cellText(row, CELL.order)).toBe('');
      expect(within(row).queryByRole('button', { name: `Переместить ${child}` })).toBeNull();
      expect(parentOf(table, child)).toBe('MSK-EPIC-1');
      const indent = within(row).getAllByRole('cell')[CELL.title]?.firstElementChild;
      expect(indent && getComputedStyle(indent).paddingLeft).toBe('24px');
    }
    const control = expandControl('MSK-EPIC-1');
    expect(control).toHaveAccessibleName('Свернуть MSK-EPIC-1');
    expect(control).toHaveAttribute('aria-expanded', 'true');
    expect(control.querySelector('[data-testid="ExpandMoreIcon"]')).not.toBeNull();
    expect(control).toHaveFocus();
    // MSK-FEAT-8 has no story beneath it: a gap stands where the control would.
    expect(
      within(rowOf(table, 'MSK-FEAT-8')).queryByRole('button', { name: /Развернуть/ }),
    ).toBeNull();
  });
});

describe('US-4 Scenario: A nested row expands one tier further', () => {
  it('MSK-US-2 and MSK-BUG-4 beneath MSK-FEAT-6, two tiers in', async () => {
    serveBoards(treeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await userEvent.click(expandControl('MSK-EPIC-1'));

    await userEvent.click(expandControl('MSK-FEAT-6'));

    expect(outline(table)).toEqual([
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      '  MSK-FEAT-6',
      '    MSK-US-2',
      '    MSK-BUG-4',
      'MSK-EPIC-20',
    ]);
    const indent = within(rowOf(table, 'MSK-BUG-4')).getAllByRole('cell')[CELL.title]
      ?.firstElementChild;
    expect(indent && getComputedStyle(indent).paddingLeft).toBe('48px');
    expect(parentOf(table, 'MSK-US-2')).toBe('MSK-FEAT-6');
  });
});

describe('US-4 Scenario: Collapsing hides the children and changes nothing', () => {
  it('«Свернуть MSK-EPIC-1» leaves MSK-EPIC-1 and MSK-EPIC-20, and nothing is written', async () => {
    const api = serveBoards(treeDataset());
    const stored = JSON.stringify(api.data);
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await userEvent.click(expandControl('MSK-EPIC-1'));
    await userEvent.click(expandControl('MSK-FEAT-6'));

    await userEvent.click(expandControl('MSK-EPIC-1'));

    expect(outline(table)).toEqual(['MSK-EPIC-1', 'MSK-EPIC-20']);
    // No order, link or «Изменён» time has changed.
    expect(api.writes()).toEqual([]);
    expect(JSON.stringify(api.data)).toBe(stored);
  });
});

describe('US-4 Scenario Outline: A link that does not nest leaves the child a top-level row elsewhere', () => {
  it('nothing of «Платформа» shows MSK-FEAT-21, MSK-US-22 or MSK-FEAT-23 beneath it', async () => {
    serveBoards(treeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await userEvent.click(expandControl('MSK-EPIC-1'));
    await userEvent.click(expandControl('MSK-FEAT-6'));

    for (const child of ['MSK-FEAT-21', 'MSK-US-22', 'MSK-FEAT-23']) {
      expect(identifiers(table)).not.toContain(child);
    }
  });

  it.each([
    ['MSK-FEAT-21', 'another team’s backlog', 'features', ids.mskTeam, 'MSK-EPIC-1'],
    ['MSK-US-22', 'parent two levels up', 'stories', ids.platformTeam, 'MSK-EPIC-1'],
    ['MSK-FEAT-23', 'parent on its own level', 'features', ids.platformTeam, 'MSK-FEAT-8'],
  ])(
    '%s (%s) is a top-level row of its backlog, «Родитель» %s',
    async (child, _why, level, team, parent) => {
      serveBoards(treeDataset());
      renderBoards(backlogAddress(level, team));
      const table = await findBacklogTable();

      const row = rowOf(table, child);
      expect(row).toHaveAttribute('data-tier', '0');
      expect(cellText(row, CELL.order)).not.toBe('');
      expect(parentOf(table, child)).toBe(parent);
    },
  );
});

describe('US-4 Scenario: Unparented rows are normal', () => {
  it('«Фичи»: MSK-FEAT-23’s «Родитель» reads MSK-FEAT-8, MSK-FEAT-6’s MSK-EPIC-1, each a link to the form with its title as tooltip; no row is a warning', async () => {
    serveBoards(treeDataset());
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();

    expect(outline(table)).toEqual(['MSK-FEAT-8', 'MSK-FEAT-6', 'MSK-FEAT-23']);
    expect(rowsOf(table).map((row) => cellText(row, CELL.parent))).toEqual([
      'MSK-EPIC-1',
      'MSK-EPIC-1',
      'MSK-FEAT-8',
    ]);
    const link = within(rowOf(table, 'MSK-FEAT-23')).getByRole('link', { name: 'MSK-FEAT-8' });
    expect(link).toHaveAttribute('href', mskForm('MSK-FEAT-8'));
    expect(link).toHaveAccessibleDescription('Вход через SSO');
    expect(table.querySelector('.Mui-error, [role="alert"]')).toBeNull();

    await userEvent.click(link);
    expect(screen.getByTestId('location')).toHaveTextContent(mskForm('MSK-FEAT-8'));
  });

  it('a row with no parent reads «Нет», as text, not a link', async () => {
    serveBoards(treeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    expect(parentOf(table, 'MSK-EPIC-20')).toBe('Нет');
    expect(
      within(
        within(rowOf(table, 'MSK-EPIC-20')).getAllByRole('cell')[CELL.parent] as HTMLElement,
      ).queryByRole('link'),
    ).toBeNull();
  });
});

describe('US-4 Scenario: «Истории» rows do not expand in this cut', () => {
  it('with MSK-TASK-3 under MSK-US-2, no «Истории» row has an expand control (OQ-5)', async () => {
    const data = treeDataset();
    mskRecord(data, 3).parentId = mskRecord(data, 2).id;
    serveBoards(data);
    renderBoards(backlogAddress('stories'));
    const table = await findBacklogTable();

    expect(outline(table)).toEqual(['MSK-US-2', 'MSK-BUG-4', 'MSK-US-22']);
    expect(within(table).queryAllByRole('button', { name: /^(Развернуть|Свернуть) / })).toEqual([]);
    expect(rowsOf(table).map((row) => cellText(row, CELL.parent))).toEqual([
      'MSK-FEAT-6',
      'MSK-FEAT-6',
      'MSK-EPIC-1',
    ]);
  });
});

describe('US-4 Scenario: What is expanded survives the page’s re-reads, not a reload', () => {
  it('MSK-EPIC-1 stays expanded after a reorder and a refocus; another level and back, or a reload, start collapsed', async () => {
    const api = serveBoards(treeDataset());
    const view = renderBoards(backlogAddress('epics'));
    let table = await findBacklogTable();
    await userEvent.click(expandControl('MSK-EPIC-1'));

    await userEvent.click(handle('MSK-EPIC-20'));
    await userEvent.click(
      within(await screen.findByRole('menu', { name: 'Переместить MSK-EPIC-20' })).getByRole(
        'menuitem',
        { name: 'В начало' },
      ),
    );
    await waitFor(() =>
      expect(outline(table)).toEqual(['MSK-EPIC-20', 'MSK-EPIC-1', '  MSK-FEAT-8', '  MSK-FEAT-6']),
    );
    const reads = api.reads('/backlogs/epics').length;
    await refocusTab();
    await waitFor(() => expect(api.reads('/backlogs/epics').length).toBeGreaterThan(reads));
    expect(outline(table)).toEqual(['MSK-EPIC-20', 'MSK-EPIC-1', '  MSK-FEAT-8', '  MSK-FEAT-6']);
    expect(expandControl('MSK-EPIC-1')).toHaveAttribute('aria-expanded', 'true');

    await chooseLevel('Фичи');
    await waitFor(async () =>
      expect(identifiers(await findBacklogTable())).toEqual([
        'MSK-FEAT-8',
        'MSK-FEAT-6',
        'MSK-FEAT-23',
      ]),
    );
    await chooseLevel('Эпики');
    await waitFor(async () =>
      expect(outline(await findBacklogTable())).toEqual(['MSK-EPIC-20', 'MSK-EPIC-1']),
    );

    await userEvent.click(expandControl('MSK-EPIC-1'));
    view.unmount();
    renderBoards(backlogAddress('epics'));
    table = await findBacklogTable();
    expect(outline(table)).toEqual(['MSK-EPIC-20', 'MSK-EPIC-1']);
  });
});

describe('US-4 Scenario: A reorder moves a row with its shown children', () => {
  it('dragging MSK-EPIC-20 draws a line only above a block, never between nested rows', async () => {
    serveBoards(treeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await userEvent.click(expandControl('MSK-EPIC-1'));

    const transfer = dataTransfer();
    await act(async () => fireDrag('dragStart', handle('MSK-EPIC-20'), transfer));
    const over = (identifier: string) =>
      within(rowOf(table, identifier)).getAllByRole('cell')[CELL.title] as HTMLElement;
    const lines = () =>
      rowsOf(table)
        .filter((row) => row.hasAttribute('data-drop-line'))
        .map((row) => `${cellText(row, CELL.identifier)}:${row.dataset.dropLine}`);

    // Anywhere over MSK-EPIC-1's block, its upper half: above MSK-EPIC-1.
    await act(async () => fireDrag('dragOver', over('MSK-FEAT-8'), transfer, 'upper'));
    expect(lines()).toEqual(['MSK-EPIC-1:top']);
    // Its lower half is the gap MSK-EPIC-20 already stands in: no line.
    await act(async () => fireDrag('dragOver', over('MSK-FEAT-6'), transfer, 'lower'));
    expect(lines()).toEqual([]);
    await act(async () => fireDrag('dragEnd', handle('MSK-EPIC-20'), transfer));
  });

  it('dragging an expanded MSK-EPIC-1 below MSK-EPIC-20 takes its children along', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await userEvent.click(expandControl('MSK-EPIC-1'));

    const transfer = dataTransfer();
    const target = within(rowOf(table, 'MSK-EPIC-20')).getAllByRole('cell')[
      CELL.title
    ] as HTMLElement;
    await act(async () => fireDrag('dragStart', handle('MSK-EPIC-1'), transfer));
    await act(async () => fireDrag('dragOver', target, transfer, 'lower'));
    expect(rowOf(table, 'MSK-EPIC-20')).toHaveAttribute('data-drop-line', 'bottom');
    await act(async () => fireDrag('drop', target, transfer, 'lower'));
    await act(async () => fireDrag('dragEnd', handle('MSK-EPIC-1'), transfer));

    await waitFor(() =>
      expect(outline(table)).toEqual(['MSK-EPIC-20', 'MSK-EPIC-1', '  MSK-FEAT-8', '  MSK-FEAT-6']),
    );
    expect(api.writes().map((request) => request.body)).toEqual([
      { itemId: mskRecord(api.data, 1).id, position: 'bottom' },
    ]);
  });

  it('«Выше» in MSK-EPIC-20’s handle menu reads MSK-EPIC-20, MSK-EPIC-1, MSK-FEAT-8, MSK-FEAT-6 and announces «позиция 1 из 2»', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await userEvent.click(expandControl('MSK-EPIC-1'));

    await userEvent.click(handle('MSK-EPIC-20'));
    const menu = await screen.findByRole('menu', { name: 'Переместить MSK-EPIC-20' });
    expect(within(menu).getByRole('menuitem', { name: 'Ниже' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Выше' }));

    expect(outline(table)).toEqual(['MSK-EPIC-20', 'MSK-EPIC-1', '  MSK-FEAT-8', '  MSK-FEAT-6']);
    expect(announcer()).toHaveTextContent('MSK-EPIC-20 перемещён: позиция 1 из 2.');
    await waitFor(() =>
      expect(api.order(ids.platformTeam, 'epics')).toEqual(['MSK-EPIC-20', 'MSK-EPIC-1']),
    );
    expect(rowsOf(table).map((row) => cellText(row, CELL.order))).toEqual(['1', '2', '', '']);
    // The link stays: a reorder writes no parent.
    expect(mskRecord(api.data, 8).parentId).toBe(mskRecord(api.data, 1).id);
  });
});

describe('US-4 Scenario: A link changed elsewhere shows at the next read', () => {
  it('MSK-FEAT-6’s parent cleared in another tab: on refocus it is no longer beneath MSK-EPIC-1, and MSK-FEAT-8 is', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await userEvent.click(expandControl('MSK-EPIC-1'));

    mskRecord(api.data, 6).parentId = null;
    await refocusTab();

    await waitFor(() =>
      expect(outline(table)).toEqual(['MSK-EPIC-1', '  MSK-FEAT-8', 'MSK-EPIC-20']),
    );
  });
});

describe('US-4 Scenario: The expand control works by keyboard', () => {
  it('Enter expands and Space collapses, focus staying on the control', async () => {
    serveBoards(treeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    expandControl('MSK-EPIC-1').focus();
    await userEvent.keyboard('{Enter}');
    expect(outline(table)).toEqual(['MSK-EPIC-1', '  MSK-FEAT-8', '  MSK-FEAT-6', 'MSK-EPIC-20']);
    expect(expandControl('MSK-EPIC-1')).toHaveFocus();

    await userEvent.keyboard(' ');
    expect(outline(table)).toEqual(['MSK-EPIC-1', 'MSK-EPIC-20']);
    expect(expandControl('MSK-EPIC-1')).toHaveFocus();
    expect(expandControl('MSK-EPIC-1')).toHaveAccessibleName('Развернуть MSK-EPIC-1');
  });

  it('the controls are tab stops in row order: the handle, then the expand control, then the title', async () => {
    serveBoards(treeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    handle('MSK-EPIC-1').focus();
    await userEvent.tab();
    expect(expandControl('MSK-EPIC-1')).toHaveFocus();
    await userEvent.tab();
    expect(screen.getByRole('link', { name: 'Вход и регистрация' })).toHaveFocus();
  });
});

describe('US-7 Scenario: A child of a deleted parent is unparented on the backlog', () => {
  it('MSK-EPIC-1 in «Удалена»: MSK-FEAT-6’s and MSK-FEAT-8’s «Родитель» read «Нет»; «Эпики» has no MSK-EPIC-1 and nothing nests', async () => {
    const data = treeDataset();
    mskRecord(data, 1).stateId = stateId('EPIC', 'Удалена');
    serveBoards(data);
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();

    expect(parentOf(table, 'MSK-FEAT-6')).toBe('Нет');
    expect(parentOf(table, 'MSK-FEAT-8')).toBe('Нет');
    expect(parentOf(table, 'MSK-FEAT-23')).toBe('MSK-FEAT-8');

    await chooseLevel('Эпики');
    await waitFor(async () => expect(outline(await findBacklogTable())).toEqual(['MSK-EPIC-20']));
    expect(screen.queryByRole('button', { name: /^Развернуть / })).toBeNull();
  });
});
