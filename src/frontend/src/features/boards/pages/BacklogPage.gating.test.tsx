import { describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import {
  READ_CODES,
  servedFor,
  servedWith,
  type ServedAccess,
} from '../../../test/organizationRoles';
import { organizationsKeys } from '../../organizations/api/queryKeys';
import { serveBoards } from '../testApi';
import {
  CELL,
  actions,
  addControl,
  addRowOf,
  cellOf,
  collapseOneLevel,
  expandControl,
  expandOneLevel,
  findBacklogTable,
  press,
  rowOf,
  rowsOf,
  titleLink,
} from '../testBacklog';
import { dataTransfer, fireDrag } from '../testDrag';
import { WORK_ITEM_PARENT_URL } from '../../workitems/testFixtures';
import {
  API,
  PROBLEM_HEADERS,
  fullTreeDataset,
  ids,
  problem,
  storiesDataset,
} from '../testFixtures';
import { backlogAddress, renderBoards } from '../testRender';

// mod-016 design § 6.2 and § 12 ("Backlog gating"), the backlog as in main
// (feat-027): which of «+», «⋮», the grip and the drop targets a user's codes
// leave. Every bundle lists each code it holds, so a missing code is a code left
// out of the list and never one implied. `fullTreeDataset` on «Эпики» reads,
// expanded: MSK-EPIC-1 → MSK-FEAT-8, MSK-FEAT-6 → …, MSK-US-22; and MSK-EPIC-20.

const WRITE = 'workitems.workitem.write';
const LINK = 'workitems.link.write';
const REORDER = 'boards.backlog.reorder';

/** The reads the page needs and the codes the test names, nothing implied. */
function bundle(...held: string[]): ServedAccess {
  return servedWith('member', [...READ_CODES, ...held]);
}

const READER: ServedAccess = servedWith('viewer', [...READ_CODES]);
const WRITE_AND_LINK = bundle(WRITE, LINK);
const WRITE_AND_REORDER = bundle(WRITE, REORDER);
const LINK_ALONE = bundle(LINK);
const EVERYTHING = bundle(WRITE, LINK, REORDER);

const TOP_LEVEL = ['MSK-EPIC-1', 'MSK-EPIC-20'];
const NESTED = [
  'MSK-FEAT-8',
  'MSK-FEAT-23',
  'MSK-FEAT-6',
  'MSK-US-2',
  'MSK-BUG-5',
  'MSK-TASK-3',
  'MSK-BUG-4',
  'MSK-US-22',
];

async function open(role: ServedAccess) {
  const api = serveBoards(fullTreeDataset());
  const shell = renderBoards(backlogAddress('epics'), { role });
  const table = await findBacklogTable();
  // The process is read after the table: «+» and the page's primary wait for it.
  await waitFor(() => expect(shell.queryClient.isFetching()).toBe(0));
  return { api, shell, table };
}

const drawnGrips = (table: HTMLElement) => table.querySelectorAll('[data-grip]');
const headerPrimary = () => screen.queryAllByRole('button', { name: 'Новый рабочий элемент' });
const plusButtons = () =>
  screen.queryAllByRole('button', {
    name: /^(Добавить: .+|Добавить дочерний элемент) \(MSK-/,
    hidden: true,
  });
const moreButton = (identifier: string) =>
  screen.queryByRole('button', { name: `Ещё действия: ${identifier}`, hidden: true });
const draggableRows = (table: HTMLElement) =>
  rowsOf(table)
    .filter((row) => row.getAttribute('draggable') === 'true')
    .map((row) => row.dataset.identifier);

async function menuOf(identifier: string) {
  await userEvent.click(actions(identifier));
  return screen.findByRole('menu', { name: `Ещё действия: ${identifier}` });
}

const itemNames = (menu: HTMLElement) =>
  within(menu)
    .getAllByRole('menuitem')
    .map((item) => item.textContent);

/** A drag of `identifier` over `target`; resolves whether it began and whether the place takes a drop. */
async function dragOver(
  table: HTMLElement,
  identifier: string,
  target: string,
  half?: 'upper' | 'lower',
) {
  const transfer = dataTransfer();
  const source = rowOf(table, identifier);
  let started = true;
  await act(async () => {
    started = fireDrag('dragStart', source, transfer);
  });
  let takesDrop = false;
  if (started) {
    await act(async () => {
      takesDrop = !fireDrag('dragOver', cellOf(rowOf(table, target), CELL.title), transfer, half);
    });
  }
  return {
    started,
    takesDrop,
    transfer,
    release: async () => {
      await act(async () =>
        fireDrag('drop', cellOf(rowOf(table, target), CELL.title), transfer, half),
      );
      await act(async () => fireDrag('dragEnd', source, transfer));
    },
    end: async () => act(async () => fireDrag('dragEnd', source, transfer)),
  };
}

const dropLines = (table: HTMLElement) =>
  rowsOf(table).filter((row) => row.hasAttribute('data-drop-line'));

describe('mod-016 design § 6.2 Scenario: the reader’s bundle (reads only)', () => {
  it('draws no «Новый рабочий элемент», no «+», no «Ещё действия», no grip, and no row is draggable', async () => {
    const { table } = await open(READER);

    expect(headerPrimary()).toHaveLength(0);
    expect(plusButtons()).toHaveLength(0);
    expect(screen.queryAllByRole('button', { name: /^Ещё действия/, hidden: true })).toHaveLength(
      0,
    );
    expect(drawnGrips(table)).toHaveLength(0);
    expect(draggableRows(table)).toEqual([]);
    expect(rowsOf(table)).toHaveLength(TOP_LEVEL.length + NESTED.length);
  });

  it('keeps what only reads: the header buttons, the chevrons, a click that selects a row, the title links, the team and level drop-downs and «Показать канбан»', async () => {
    const { table } = await open(READER);

    expect(expandOneLevel()).toBeInTheDocument();
    expect(collapseOneLevel()).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Сменить команду' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^Уровень: / })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Показать канбан' })).toBeInTheDocument();
    for (const row of rowsOf(table)) {
      expect(titleLink(row)).toHaveAttribute('href');
    }
    press(rowOf(table, 'MSK-FEAT-6'));
    expect(rowOf(table, 'MSK-FEAT-6')).toHaveAttribute('data-selected', 'true');
    press(expandControl('MSK-EPIC-1'));
    expect(rowsOf(table).map((row) => row.dataset.identifier)).toEqual(TOP_LEVEL);
  });

  it('a drag that is begun is refused: nothing is selected or framed and nothing is sent', async () => {
    const { api, table } = await open(READER);

    const gesture = await dragOver(table, 'MSK-US-22', 'MSK-FEAT-8');

    expect(gesture.started).toBe(false);
    expect(rowsOf(table).some((row) => row.hasAttribute('data-drop-target'))).toBe(false);
    expect(api.writes()).toEqual([]);
  });
});

describe('mod-016 design § 6.2 Scenario: write and link, without reorder', () => {
  it('draws «Новый рабочий элемент» and «+», and the add row opens', async () => {
    const { table } = await open(WRITE_AND_LINK);

    expect(headerPrimary()).toHaveLength(1);
    expect((await addControl('MSK-EPIC-1')).getAttribute('aria-label')).toContain('(MSK-EPIC-1)');
    press(await addControl('MSK-EPIC-1'));
    expect(addRowOf(table)).not.toBeNull();
  });

  it('draws «Ещё действия» on every row, holding «Изменить родителя…» and «Убрать родителя» only: no «Переместить», no separator', async () => {
    await open(WRITE_AND_LINK);

    for (const identifier of [...TOP_LEVEL, ...NESTED]) {
      expect(moreButton(identifier)).toBeInTheDocument();
    }
    const menu = await menuOf('MSK-EPIC-1');
    expect(itemNames(menu)).toEqual(['Изменить родителя…', 'Убрать родителя']);
    expect(within(menu).queryByText('Переместить')).not.toBeInTheDocument();
    expect(menu.querySelector('.MuiDivider-root')).toBeNull();
  });

  it('makes every row draggable, to row targets, with no drop line', async () => {
    const { table } = await open(WRITE_AND_LINK);

    expect(draggableRows(table)).toEqual(rowsOf(table).map((row) => row.dataset.identifier));
    expect(drawnGrips(table)).toHaveLength(rowsOf(table).length);

    // A top-level row over the upper quarter of another is a row target, not a line.
    const gesture = await dragOver(table, 'MSK-EPIC-20', 'MSK-EPIC-1', 'upper');
    expect(gesture.started).toBe(true);
    expect(gesture.takesDrop).toBe(true);
    expect(dropLines(table)).toHaveLength(0);
    expect(rowOf(table, 'MSK-EPIC-1')).toHaveAttribute('data-drop-target');
    await gesture.end();

    // A nested story over a feature is the parent write's allowed target.
    const nested = await dragOver(table, 'MSK-US-22', 'MSK-FEAT-8');
    expect(nested.takesDrop).toBe(true);
    expect(rowOf(table, 'MSK-FEAT-8')).toHaveAttribute('data-drop-target', 'allowed');
    await nested.end();
  });
});

describe('mod-016 design § 6.2 Scenario: write and reorder, without link', () => {
  it('draws «Новый рабочий элемент» but no «+» — the create carries the parent', async () => {
    await open(WRITE_AND_REORDER);

    expect(headerPrimary()).toHaveLength(1);
    expect(plusButtons()).toHaveLength(0);
  });

  it('draws «Ещё действия» on top-level rows only, holding the move group alone', async () => {
    await open(WRITE_AND_REORDER);

    for (const identifier of TOP_LEVEL) {
      expect(moreButton(identifier)).toBeInTheDocument();
    }
    for (const identifier of NESTED) {
      expect(moreButton(identifier)).not.toBeInTheDocument();
    }
    const menu = await menuOf('MSK-EPIC-1');
    expect(within(menu).getByText('Переместить')).toBeInTheDocument();
    expect(itemNames(menu)).toEqual(['В начало', 'Выше', 'Ниже', 'В конец']);
    expect(menu.querySelector('.MuiDivider-root')).toBeNull();
  });

  it('makes top-level rows draggable to the drop line, with no row targets; nested rows are not draggable', async () => {
    const { api, table } = await open(WRITE_AND_REORDER);

    expect(draggableRows(table)).toEqual(TOP_LEVEL);
    expect(drawnGrips(table)).toHaveLength(TOP_LEVEL.length);

    const nested = await dragOver(table, 'MSK-US-22', 'MSK-FEAT-8');
    expect(nested.started).toBe(false);

    const onRow = await dragOver(table, 'MSK-EPIC-20', 'MSK-FEAT-8');
    expect(onRow.started).toBe(true);
    expect(onRow.takesDrop).toBe(false);
    expect(rowsOf(table).some((row) => row.hasAttribute('data-drop-target'))).toBe(false);
    // Released over a row, nothing is sent.
    await onRow.release();
    expect(api.writes()).toEqual([]);

    const onLine = await dragOver(table, 'MSK-EPIC-20', 'MSK-EPIC-1', 'upper');
    expect(onLine.takesDrop).toBe(true);
    expect(rowOf(table, 'MSK-EPIC-1')).toHaveAttribute('data-drop-line', 'top');
    expect(rowOf(table, 'MSK-EPIC-1')).not.toHaveAttribute('data-drop-target');
    await onLine.end();
  });
});

describe('mod-016 design § 6.2 Scenario: link alone', () => {
  it('draws no «Новый рабочий элемент» and no «+»', async () => {
    await open(LINK_ALONE);

    expect(headerPrimary()).toHaveLength(0);
    expect(plusButtons()).toHaveLength(0);
  });

  it('draws «Ещё действия» on every row with the parent items', async () => {
    await open(LINK_ALONE);

    for (const identifier of [...TOP_LEVEL, ...NESTED]) {
      expect(moreButton(identifier)).toBeInTheDocument();
    }
    expect(itemNames(await menuOf('MSK-EPIC-1'))).toEqual([
      'Изменить родителя…',
      'Убрать родителя',
    ]);
  });

  it('makes every row draggable to row targets, and the drop line is not offered', async () => {
    const { table } = await open(LINK_ALONE);

    expect(draggableRows(table)).toEqual(rowsOf(table).map((row) => row.dataset.identifier));
    const gesture = await dragOver(table, 'MSK-EPIC-20', 'MSK-EPIC-1', 'upper');
    expect(dropLines(table)).toHaveLength(0);
    expect(gesture.takesDrop).toBe(true);
    await gesture.end();
  });
});

describe('mod-016 design § 6.2 Scenario: all three codes (the administrator’s and the member’s bundles)', () => {
  it('draws every control: the primary, «+», both groups of «⋮» with the separator between, a grip on each row', async () => {
    const { table } = await open(EVERYTHING);

    expect(headerPrimary()).toHaveLength(1);
    expect(await addControl('MSK-EPIC-1')).toBeInTheDocument();
    expect(drawnGrips(table)).toHaveLength(rowsOf(table).length);
    const menu = await menuOf('MSK-EPIC-1');
    expect(itemNames(menu)).toEqual([
      'В начало',
      'Выше',
      'Ниже',
      'В конец',
      'Изменить родителя…',
      'Убрать родителя',
    ]);
    expect(menu.querySelector('.MuiDivider-root')).not.toBeNull();
  });
});

describe('mod-016 plan § 3.5 Scenario: «+» goes while an add row is open', () => {
  it('the add row closes with its «+», the typed title is dropped and focus goes to the main region; the code returning does not bring the title back', async () => {
    const { shell, table } = await open(EVERYTHING);
    press(await addControl('MSK-EPIC-1'));
    const row = addRowOf(table);
    expect(row).not.toBeNull();
    const field = within(row as HTMLElement).getByRole('textbox');
    await userEvent.type(field, 'Черновик');
    expect(field).toHaveValue('Черновик');

    act(() => {
      shell.queryClient.setQueryData(organizationsKeys.access(ids.example), {
        kind: 'member',
        role: 'member',
        permissions: WRITE_AND_REORDER.permissions,
      });
    });

    await waitFor(() => expect(addRowOf(table)).toBeNull());
    expect(plusButtons()).toHaveLength(0);
    await waitFor(() => expect(document.getElementById('main-content')).toHaveFocus());

    act(() => {
      shell.queryClient.setQueryData(organizationsKeys.access(ids.example), {
        kind: 'member',
        role: 'member',
        permissions: EVERYTHING.permissions,
      });
    });
    press(await addControl('MSK-EPIC-1'));
    expect(within(addRowOf(table) as HTMLElement).getByRole('textbox')).toHaveValue('');
  });
});

describe('mod-016 design § 2.2, § 6.2 Scenario: the type picker goes with the code that opened it', () => {
  it('closes the open menu when «Новый рабочий элемент» goes with the code, sends focus to the main region and opens no dialog', async () => {
    serveBoards(storiesDataset());
    const shell = renderBoards(backlogAddress('stories'), { role: bundle(WRITE) });
    await findBacklogTable();
    await waitFor(() => expect(shell.queryClient.isFetching()).toBe(0));
    await userEvent.click(await screen.findByRole('button', { name: 'Новый рабочий элемент' }));
    expect(await screen.findByRole('menu', { name: 'Новый рабочий элемент' })).toBeVisible();

    act(() => {
      shell.queryClient.setQueryData(organizationsKeys.access(ids.example), {
        kind: 'member',
        role: 'viewer',
        permissions: READER.permissions,
      });
    });

    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument());
    expect(headerPrimary()).toHaveLength(0);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    await waitFor(() => expect(document.getElementById('main-content')).toHaveFocus());
  });
});

// mod-016 design § 2.3, § 2.4, § 12 ("Notices"): a refused action that has no dialog
// shows the server's «Недостаточно прав» in the page's error notice; the codes are read
// again; a changed role replaces that notice with the role-change notice, an unchanged
// role keeps it. The backlog's «Убрать родителя» is such an action.

const REFUSED = 'Недостаточно прав';
const ROLE_CHANGED = 'Ваша роль в организации «Пример организации» изменилась: «Читатель».';
const ACCESS_URL = `${API}/api/organizations/:organizationId/access`;

function refuseParentWrites() {
  server.use(
    http.put(WORK_ITEM_PARENT_URL, () =>
      HttpResponse.json(problem('organizations.access.forbidden'), {
        status: 403,
        headers: PROBLEM_HEADERS,
      }),
    ),
  );
}

async function clearParent(identifier: string) {
  const menu = await menuOf(identifier);
  await userEvent.click(within(menu).getByRole('menuitem', { name: 'Убрать родителя' }));
}

describe('mod-016 design § 2.4 Scenario: a refusal on the backlog, then the codes are read again', () => {
  it('a changed role: the role-change notice stands alone, the refusal notice is gone', async () => {
    await open(EVERYTHING);
    refuseParentWrites();
    server.use(http.get(ACCESS_URL, () => HttpResponse.json(servedFor('viewer'))));

    await clearParent('MSK-FEAT-8');

    expect(await screen.findByText(ROLE_CHANGED)).toBeVisible();
    await waitFor(() => expect(screen.queryByText(REFUSED)).not.toBeInTheDocument());
    // The controls the role lost are gone with no reload.
    await waitFor(() => expect(plusButtons()).toHaveLength(0));
    expect(screen.getAllByRole('alert')).toHaveLength(1);
  });

  it('an unchanged role: the refusal notice stays and no role-change notice is raised', async () => {
    await open(EVERYTHING);
    refuseParentWrites();

    await clearParent('MSK-FEAT-8');

    expect(await screen.findByText(REFUSED)).toBeVisible();
    await waitFor(() =>
      expect(screen.queryByText(/Ваша роль в организации/)).not.toBeInTheDocument(),
    );
    expect(screen.getByText(REFUSED)).toBeVisible();
  });
});
